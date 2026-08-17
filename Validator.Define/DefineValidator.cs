using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Validator.Define;

public sealed class DefineValidator : IDefineValidator
{
    private static readonly XNamespace Odm = "http://www.cdisc.org/ns/odm/v1.3";
    private static readonly XNamespace Def = "http://www.cdisc.org/ns/def/v2.1";
    private static readonly HashSet<string> ItemDataTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "integer", "float", "datetime", "date", "time", "partialDate", "partialTime",
        "partialDatetime", "incompleteDatetime", "durationDatetime", "intervalDatetime"
    };
    private static readonly HashSet<string> CodeListDataTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "integer", "float"
    };

    public DefineValidationResult Validate(string xml, DefineValidationOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(options);

        var diagnostics = new List<DefineDiagnostic>();
        ValidateXmlDeclaration(xml, diagnostics);
        ValidateSchema(xml, options, diagnostics);

        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            Add(diagnostics, "OD0001", "XML", "XML is not well-formed: " + exception.Message);
            return new DefineValidationResult(Distinct(diagnostics));
        }

        ValidateDocument(document, diagnostics);
        return new DefineValidationResult(Distinct(diagnostics));
    }

    private static void ValidateXmlDeclaration(string xml, ICollection<DefineDiagnostic> diagnostics)
    {
        if (!xml.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            Add(diagnostics, "OD0010", "XML", "Missing XML declaration.");

        var declaration = Regex.Match(xml, "^\\s*<\\?xml\\s+version\\s*=\\s*['\\\"][^'\\\"]+['\\\"](?:\\s+encoding\\s*=\\s*['\\\"](?<encoding>[^'\\\"]+)['\\\"])?", RegexOptions.IgnoreCase);
        if (declaration.Success && declaration.Groups["encoding"].Success &&
            !new[] { "UTF-8", "UTF-16", "ISO-8859-1" }.Contains(declaration.Groups["encoding"].Value, StringComparer.OrdinalIgnoreCase))
        {
            Add(diagnostics, "OD0011", "XML", "Invalid XML encoding.");
        }
    }

    private static void ValidateSchema(string xml, DefineValidationOptions options, ICollection<DefineDiagnostic> diagnostics)
    {
        var schemas = new XmlSchemaSet();
        schemas.Add(Odm.NamespaceName, options.OdmSchemaPath);
        schemas.Add(Odm.NamespaceName, options.DefineSchemaPath);
        schemas.Compile();

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemas,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.ValidationEventHandler += (_, args) =>
        {
            var exception = args.Exception;
            var location = exception == null ? "XML" : $"Line {exception.LineNumber}, Position {exception.LinePosition}";
            Add(diagnostics, "DD0001", location, args.Message,
                args.Severity == XmlSeverityType.Warning ? DefineDiagnosticSeverity.Warning : DefineDiagnosticSeverity.Error);
        };

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            using var reader = XmlReader.Create(stream, settings);
            while (reader.Read()) { }
        }
        catch (XmlException exception)
        {
            Add(diagnostics, "OD0001", $"Line {exception.LineNumber}, Position {exception.LinePosition}", exception.Message);
        }
        catch (XmlSchemaException exception)
        {
            Add(diagnostics, "DD0001", $"Line {exception.LineNumber}, Position {exception.LinePosition}", exception.Message);
        }
    }

    private static void ValidateDocument(XDocument document, ICollection<DefineDiagnostic> diagnostics)
    {
        var root = document.Root;
        if (root == null || root.Name != Odm + "ODM")
        {
            Add(diagnostics, "OD0012", "ODM", "Invalid root element; Define.xml must contain an ODM root element.");
            return;
        }

        ValidateNamespaces(root, diagnostics);
        ValidateRoot(root, diagnostics);
        ValidateTranslatedTextLanguages(root, diagnostics);

        var studies = root.Elements(Odm + "Study").ToList();
        DuplicateAttribute(studies, "OID", "OD0022", "Study", diagnostics);
        ValidateRequiredStructure(root, studies, diagnostics);

        var metadata = root.Descendants(Odm + "MetaDataVersion").ToList();
        DuplicateAttribute(metadata, "OID", "OD0027", "MetaDataVersion", diagnostics);
        foreach (var version in metadata)
            ValidateMetadataVersion(version, diagnostics);
    }

    private static void ValidateRequiredStructure(XElement root, IReadOnlyList<XElement> studies, ICollection<DefineDiagnostic> diagnostics)
    {
        if (studies.Count == 0)
        {
            Add(diagnostics, "DD0006", Location(root), "Missing required Study element.");
            return;
        }

        foreach (var study in studies)
        {
            var metadata = study.Elements(Odm + "MetaDataVersion").ToList();
            if (metadata.Count == 0)
                Add(diagnostics, "DD0006", Location(study), "Missing required MetaDataVersion element.");

            var globals = study.Element(Odm + "GlobalVariables");
            if (globals != null)
            {
                RequiredText(globals, Odm + "StudyName", "DD0006", diagnostics);
                RequiredText(globals, Odm + "StudyDescription", "DD0006", diagnostics);
                RequiredText(globals, Odm + "ProtocolName", "DD0006", diagnostics);
            }

            foreach (var version in metadata)
            {
                if (version.Element(Def + "Standards") == null)
                    Add(diagnostics, "DD0006", Location(version), "Missing required Standards element.");
                if (!version.Elements(Odm + "ItemDef").Any())
                    Add(diagnostics, "DD0006", Location(version), "Missing required ItemDef element.");
                var groups = version.Elements(Odm + "ItemGroupDef").ToList();
                if (groups.Count == 0)
                    Add(diagnostics, "DD0006", Location(version), "Missing required ItemGroupDef element.");
                foreach (var group in groups.Where(x => !EqualsValue(x.Attribute(Def + "HasNoData")?.Value, "Yes")))
                {
                    if (!group.Elements(Odm + "ItemRef").Any())
                        Add(diagnostics, "DD0006", Location(group), "Missing required ItemRef element.");
                }
            }
        }
    }

    private static void ValidateNamespaces(XElement root, ICollection<DefineDiagnostic> diagnostics)
    {
        if (root.GetDefaultNamespace() != Odm)
            Add(diagnostics, "DD0002", Location(root), "Missing or invalid ODM namespace reference.");
        if (root.GetNamespaceOfPrefix("def") != Def)
            Add(diagnostics, "DD0002", Location(root), "Missing or invalid Define-XML 2.1 namespace reference.");
        var requiresXLink = root.Descendants(Def + "DocumentRef").Any() || root.Descendants(Def + "leaf").Any();
        if (requiresXLink && root.GetNamespaceOfPrefix("xlink")?.NamespaceName != "http://www.w3.org/1999/xlink")
            Add(diagnostics, "DD0002", Location(root), "Missing or invalid XLink namespace reference.");
    }

    private static void ValidateTranslatedTextLanguages(XElement root, ICollection<DefineDiagnostic> diagnostics)
    {
        var validLanguages = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .Select(x => x.TwoLetterISOLanguageName)
            .Where(x => !string.IsNullOrWhiteSpace(x) && x != "iv")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var translatedText in root.Descendants(Odm + "TranslatedText"))
        {
            var language = translatedText.Attribute(XNamespace.Xml + "lang")?.Value;
            if (!string.IsNullOrWhiteSpace(language) && !validLanguages.Contains(language.Split('-')[0]))
                Add(diagnostics, "OD0021", Location(translatedText), $"Invalid Language value '{language}'.");
        }
    }

    private static void ValidateRoot(XElement root, ICollection<DefineDiagnostic> diagnostics)
    {
        Required(root, "ODMVersion", "DD0003", diagnostics);
        Required(root, "FileType", "DD0003", diagnostics);
        Required(root, "FileOID", "DD0003", diagnostics);
        Required(root, "CreationDateTime", "DD0003", diagnostics);
        if (!EqualsValue(root.Attribute("FileType")?.Value, "Snapshot"))
            Add(diagnostics, "DD0019", Location(root), "Invalid FileType value; it must be Snapshot.");
    }

    private static void ValidateMetadataVersion(XElement metadata, ICollection<DefineDiagnostic> diagnostics)
    {
        var defineVersion = metadata.Attribute(Def + "DefineVersion")?.Value;
        if (string.IsNullOrWhiteSpace(defineVersion) || !Regex.IsMatch(defineVersion, @"^2\.1\.\d+$"))
            Add(diagnostics, "DD0020", Location(metadata), "Invalid def:DefineVersion value; Define-XML 2.1 requires 2.1.n.");

        var standards = metadata.Elements(Def + "Standards").Elements(Def + "Standard").ToList();
        ValidateStandards(metadata, standards, diagnostics);

        var itemGroups = metadata.Elements(Odm + "ItemGroupDef").ToList();
        var items = metadata.Elements(Odm + "ItemDef").ToList();
        var codeLists = metadata.Elements(Odm + "CodeList").ToList();
        var methods = metadata.Elements(Odm + "MethodDef").ToList();
        var valueLists = metadata.Elements(Def + "ValueListDef").ToList();
        var leaves = metadata.Descendants(Def + "leaf").ToList();

        DuplicateAttribute(itemGroups, "OID", "OD0030", "ItemGroupDef", diagnostics);
        DuplicateAttribute(items, "OID", "OD0031", "ItemDef", diagnostics);
        DuplicateAttribute(codeLists, "OID", "OD0032", "CodeList", diagnostics);
        DuplicateAttribute(methods, "OID", "DD0013", "MethodDef", diagnostics);
        DuplicateAttribute(valueLists, "OID", "DD0014", "ValueListDef", diagnostics);
        DuplicateAttribute(leaves, "ID", "DD0012", "def:leaf", diagnostics);

        var itemOids = Oids(items);
        var codeListOids = Oids(codeLists);
        var methodOids = Oids(methods);
        var valueListOids = Oids(valueLists);
        var leafIds = leaves.Select(x => x.Attribute("ID")?.Value).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);

        foreach (var group in itemGroups)
            ValidateItemGroup(group, itemOids, leafIds, diagnostics);
        foreach (var item in items)
            ValidateItem(item, codeListOids, methodOids, valueListOids, leafIds, diagnostics);
        foreach (var valueList in valueLists)
            ValidateValueList(valueList, itemOids, diagnostics);
        foreach (var codeList in codeLists)
            ValidateCodeList(codeList, diagnostics);
        foreach (var method in methods)
            ValidateDocumentReferences(method, leafIds, diagnostics);
        foreach (var comment in metadata.Elements(Def + "CommentDef"))
            ValidateDocumentReferences(comment, leafIds, diagnostics);

        ValidateUnreferencedObjects(metadata, items, codeLists, methods, valueLists, leaves, diagnostics);
        ValidateComments(metadata, diagnostics);
        ValidateMethods(methods, diagnostics);
    }

    private static void ValidateUnreferencedObjects(
        XElement metadata,
        IReadOnlyList<XElement> items,
        IReadOnlyList<XElement> codeLists,
        IReadOnlyList<XElement> methods,
        IReadOnlyList<XElement> valueLists,
        IReadOnlyList<XElement> leaves,
        ICollection<DefineDiagnostic> diagnostics)
    {
        var referencedItems = metadata.Descendants(Odm + "ItemRef")
            .Select(x => x.Attribute("ItemOID")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var item in items.Where(x => !referencedItems.Contains(x.Attribute("OID")?.Value ?? string.Empty)))
            Add(diagnostics, "DD0067", Location(item), $"Variable '{item.Attribute("OID")?.Value}' is not referenced.");

        var referencedValueLists = metadata.Descendants(Def + "ValueListRef")
            .Select(x => x.Attribute("ValueListOID")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var valueList in valueLists.Where(x => !referencedValueLists.Contains(x.Attribute("OID")?.Value ?? string.Empty)))
            Add(diagnostics, "DD0081", Location(valueList), $"Value Level metadata '{valueList.Attribute("OID")?.Value}' is not referenced.");

        var referencedCodeLists = metadata.Descendants(Odm + "CodeListRef")
            .Select(x => x.Attribute("CodeListOID")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var codeList in codeLists.Where(x => !referencedCodeLists.Contains(x.Attribute("OID")?.Value ?? string.Empty)))
            Add(diagnostics, "DD0082", Location(codeList), $"Codelist '{codeList.Attribute("OID")?.Value}' is not referenced.");

        var referencedMethods = metadata.Descendants(Odm + "MethodRef")
            .Select(x => x.Attribute("MethodOID")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var method in methods.Where(x => !referencedMethods.Contains(x.Attribute("OID")?.Value ?? string.Empty)))
            Add(diagnostics, "DD0080", Location(method), $"Method '{method.Attribute("OID")?.Value}' is not referenced.");

        var referencedLeaves = metadata.Descendants(Def + "DocumentRef")
            .Select(x => x.Attribute("leafID")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var leaf in leaves.Where(x => !referencedLeaves.Contains(x.Attribute("ID")?.Value ?? string.Empty)))
            Add(diagnostics, "DD0078", Location(leaf), $"Document '{leaf.Attribute("ID")?.Value}' is not referenced.");
    }

    private static void ValidateComments(XElement metadata, ICollection<DefineDiagnostic> diagnostics)
    {
        var comments = metadata.Elements(Def + "CommentDef").ToList();
        DuplicateAttribute(comments, "OID", "DD0083", "CommentDef", diagnostics);
        var commentOids = Oids(comments);
        var references = metadata.Descendants().Select(x => x.Attribute(Def + "CommentOID")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
        foreach (var comment in comments)
        {
            if (string.IsNullOrWhiteSpace(comment.Element(Odm + "Description")?.Element(Odm + "TranslatedText")?.Value))
                Add(diagnostics, "DD0057", Location(comment), $"Comment '{comment.Attribute("OID")?.Value}' is missing Description.");
            if (!references.Contains(comment.Attribute("OID")?.Value ?? string.Empty))
                Add(diagnostics, "DD0079", Location(comment), $"Comment '{comment.Attribute("OID")?.Value}' is not referenced.");
        }

        foreach (var reference in metadata.Descendants().Where(x => x.Attribute(Def + "CommentOID") != null))
        {
            var oid = reference.Attribute(Def + "CommentOID")?.Value;
            if (string.IsNullOrWhiteSpace(oid) || !commentOids.Contains(oid))
                Add(diagnostics, "DD0071", Location(reference), "Referenced Comment is missing.");
        }
    }

    private static void ValidateMethods(IReadOnlyList<XElement> methods, ICollection<DefineDiagnostic> diagnostics)
    {
        foreach (var method in methods)
        {
            if (string.IsNullOrWhiteSpace(method.Element(Odm + "Description")?.Element(Odm + "TranslatedText")?.Value))
                Add(diagnostics, "DD0057", Location(method), $"Method '{method.Attribute("OID")?.Value}' is missing Description.");
            var type = method.Attribute("Type")?.Value;
            if (!string.IsNullOrWhiteSpace(type) && !EqualsValue(type, "Computation") && !EqualsValue(type, "Imputation"))
                Add(diagnostics, "DD0104", Location(method), $"Invalid Type value '{type}' for Method.");
        }
    }

    private static void ValidateStandards(XElement metadata, IReadOnlyCollection<XElement> standards, ICollection<DefineDiagnostic> diagnostics)
    {
        foreach (var standard in standards)
        {
            var name = standard.Attribute("Name")?.Value;
            var version = standard.Attribute("Version")?.Value;
            if (string.IsNullOrWhiteSpace(name))
                Add(diagnostics, "DD0021", Location(standard), "Invalid Standard Name value.");
            if (string.IsNullOrWhiteSpace(version))
                Add(diagnostics, "DD0022", Location(standard), "Invalid Standard Version value.");
        }

        var hasIg = standards.Any(x => EqualsValue(x.Attribute("Type")?.Value, "IG") &&
            (StartsWith(x.Attribute("Name")?.Value, "SDTMIG") || StartsWith(x.Attribute("Name")?.Value, "ADaMIG") || StartsWith(x.Attribute("Name")?.Value, "SENDIG")));
        var hasCt = standards.Any(x => EqualsValue(x.Attribute("Type")?.Value, "CT"));
        if (!hasIg || !hasCt)
            Add(diagnostics, "DD0150", Location(metadata), "Missing expected Standard; Define-XML 2.1 requires both IG and CT standards.");
    }

    private static void ValidateItemGroup(XElement group, IReadOnlySet<string> itemOids, IReadOnlySet<string> leafIds, ICollection<DefineDiagnostic> diagnostics)
    {
        YesNo(group, "Repeating", "OD0072", diagnostics);
        YesNo(group, "IsReferenceData", "OD0073", diagnostics);
        Required(group, Def + "Structure", "DD0003", diagnostics);
        Required(group, "SASDatasetName", "DD0003", diagnostics);

        var name = group.Attribute("Name")?.Value;
        var domain = group.Attribute("Domain")?.Value;
        var sasDatasetName = group.Attribute("SASDatasetName")?.Value;
        if (IsSdtmOrSend(group) && string.IsNullOrWhiteSpace(domain))
            Add(diagnostics, "DD0045", Location(group), $"Dataset '{name}' is missing Domain.");
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(domain) && !string.IsNullOrWhiteSpace(sasDatasetName) &&
            !name.StartsWith("SUPP", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("SQAP", StringComparison.OrdinalIgnoreCase) &&
            !IsSplitDataset(name, domain) && (!EqualsValue(name, domain) || !EqualsValue(name, sasDatasetName)))
        {
            Add(diagnostics, "DD0049", Location(group), $"Name/Domain/SASDatasetName mismatch for dataset '{name}'.");
        }
        if (string.IsNullOrWhiteSpace(group.Element(Odm + "Description")?.Element(Odm + "TranslatedText")?.Value))
            Add(diagnostics, "DD0057", Location(group), $"Dataset '{name}' is missing Description.");
        if (string.IsNullOrWhiteSpace(group.Attribute(Def + "ArchiveLocationID")?.Value) && !EqualsValue(group.Attribute(Def + "HasNoData")?.Value, "Yes"))
            Add(diagnostics, "DD0056", Location(group), $"Dataset '{name}' is missing def:ArchiveLocationID.");

        var itemRefs = group.Elements(Odm + "ItemRef").ToList();
        DuplicateAttribute(itemRefs, "ItemOID", "OD0041", "ItemRef", diagnostics);
        DuplicateAttribute(itemRefs, "OrderNumber", "OD0042", "ItemRef order number", diagnostics);
        DuplicateAttribute(itemRefs.Where(x => !string.IsNullOrWhiteSpace(x.Attribute("KeySequence")?.Value)), "KeySequence", "DD0041", "ItemRef KeySequence", diagnostics);
        foreach (var itemRef in itemRefs)
        {
            var oid = itemRef.Attribute("ItemOID")?.Value;
            if (string.IsNullOrWhiteSpace(oid) || !itemOids.Contains(oid))
                Add(diagnostics, "OD0046", Location(itemRef), "Referenced ItemDef is missing.");
        }

        var archiveId = group.Attribute(Def + "ArchiveLocationID")?.Value;
        if (!string.IsNullOrWhiteSpace(archiveId) && !leafIds.Contains(archiveId))
            Add(diagnostics, "DD0018", Location(group), "def:ArchiveLocationID does not reference a defined def:leaf.");
    }

    private static void ValidateItem(XElement item, IReadOnlySet<string> codeListOids, IReadOnlySet<string> methodOids, IReadOnlySet<string> valueListOids, IReadOnlySet<string> leafIds, ICollection<DefineDiagnostic> diagnostics)
    {
        var type = item.Attribute("DataType")?.Value;
        if (string.IsNullOrWhiteSpace(type) || !ItemDataTypes.Contains(type))
            Add(diagnostics, "OD0075", Location(item), "Invalid Data Type value for variable.");
        var requiresLength = EqualsValue(type, "integer") || EqualsValue(type, "float") || EqualsValue(type, "text");
        if (requiresLength && string.IsNullOrWhiteSpace(item.Attribute("Length")?.Value))
            Add(diagnostics, "OD0070", Location(item), "Missing Length value.");
        if (!requiresLength && !string.IsNullOrWhiteSpace(item.Attribute("Length")?.Value))
            Add(diagnostics, "DD0068", Location(item), $"Length is not allowed for DataType '{type}'.");
        if (EqualsValue(type, "float") && string.IsNullOrWhiteSpace(item.Attribute("SignificantDigits")?.Value))
            Add(diagnostics, "OD0071", Location(item), "Missing Significant Digits value.");
        if (!EqualsValue(type, "float") && !string.IsNullOrWhiteSpace(item.Attribute("SignificantDigits")?.Value))
            Add(diagnostics, "DD0069", Location(item), $"SignificantDigits is not allowed for DataType '{type}'.");
        if (string.IsNullOrWhiteSpace(item.Attribute("SASFieldName")?.Value))
            Add(diagnostics, "DD0070", Location(item), "Missing SASFieldName value.");
        if (string.IsNullOrWhiteSpace(item.Element(Odm + "Description")?.Element(Odm + "TranslatedText")?.Value))
            Add(diagnostics, "DD0058", Location(item), $"Variable '{item.Attribute("OID")?.Value}' is missing Label.");

        YesNo(item, "Mandatory", "OD0074", diagnostics);
        foreach (var reference in item.Elements(Odm + "CodeListRef"))
        {
            var oid = reference.Attribute("CodeListOID")?.Value;
            if (string.IsNullOrWhiteSpace(oid) || !codeListOids.Contains(oid))
                Add(diagnostics, "OD0048", Location(reference), "Referenced Codelist is missing.");
        }
        foreach (var reference in item.Elements(Odm + "MethodRef"))
        {
            var oid = reference.Attribute("MethodOID")?.Value;
            if (string.IsNullOrWhiteSpace(oid) || !methodOids.Contains(oid))
                Add(diagnostics, "DD0016", Location(reference), "Referenced Method is missing.");
        }
        foreach (var reference in item.Elements(Def + "ValueListRef"))
        {
            var oid = reference.Attribute("ValueListOID")?.Value;
            if (string.IsNullOrWhiteSpace(oid) || !valueListOids.Contains(oid))
                Add(diagnostics, "DD0017", Location(reference), "Referenced Value Level metadata is missing.");
        }
        ValidateOrigin(item, methodOids, leafIds, diagnostics);
        ValidateDocumentReferences(item, leafIds, diagnostics);
    }

    private static void ValidateOrigin(XElement item, IReadOnlySet<string> methodOids, IReadOnlySet<string> leafIds, ICollection<DefineDiagnostic> diagnostics)
    {
        var origin = item.Element(Def + "Origin");
        if (origin == null)
            return;

        var type = origin.Attribute("Type")?.Value;
        var source = origin.Attribute("Source")?.Value;
        if (string.IsNullOrWhiteSpace(type))
        {
            Add(diagnostics, "DD0072", Location(origin), "Missing Origin Type value.");
            return;
        }

        if (EqualsValue(type, "Predecessor") && string.IsNullOrWhiteSpace(origin.Element(Odm + "Description")?.Element(Odm + "TranslatedText")?.Value))
            Add(diagnostics, "DD0061", Location(origin), "Missing Predecessor value.");

        var documentRefs = origin.Elements(Def + "DocumentRef").ToList();
        if (EqualsValue(type, "Collected") && (EqualsValue(source, "Investigator") || EqualsValue(source, "Subject")))
        {
            if (documentRefs.Count == 0)
                Add(diagnostics, "DD0035", Location(origin), "Missing Pages value for Collected origin.");
            foreach (var documentRef in documentRefs)
            {
                var id = documentRef.Attribute("leafID")?.Value;
                if (string.IsNullOrWhiteSpace(id) || !leafIds.Contains(id))
                    Add(diagnostics, "DD0015", Location(documentRef), "Referenced Document is missing.");
                if (!documentRef.Elements(Def + "PDFPageRef").Any())
                    Add(diagnostics, "DD0035", Location(documentRef), "Missing Pages value for Collected origin.");
            }
        }

        if (EqualsValue(type, "Derived"))
        {
            var methodRefs = item.Elements(Odm + "MethodRef").ToList();
            if (methodRefs.Count == 0)
                Add(diagnostics, "DD0042", Location(item), "Missing Method reference for Derived variable.");
            foreach (var reference in methodRefs)
            {
                var oid = reference.Attribute("MethodOID")?.Value;
                if (string.IsNullOrWhiteSpace(oid) || !methodOids.Contains(oid))
                    Add(diagnostics, "DD0016", Location(reference), "Referenced Method is missing.");
            }
        }
    }

    private static void ValidateValueList(XElement valueList, IReadOnlySet<string> itemOids, ICollection<DefineDiagnostic> diagnostics)
    {
        foreach (var itemRef in valueList.Elements(Odm + "ItemRef"))
        {
            var oid = itemRef.Attribute("ItemOID")?.Value;
            if (string.IsNullOrWhiteSpace(oid) || !itemOids.Contains(oid))
                Add(diagnostics, "OD0046", Location(itemRef), "Referenced ItemDef is missing.");
        }
    }

    private static void ValidateCodeList(XElement codeList, ICollection<DefineDiagnostic> diagnostics)
    {
        var type = codeList.Attribute("DataType")?.Value;
        if (string.IsNullOrWhiteSpace(type) || !CodeListDataTypes.Contains(type))
            Add(diagnostics, "OD0076", Location(codeList), "Invalid Data Type value for codelist.");

        var terms = codeList.Elements().Where(x => x.Name == Odm + "EnumeratedItem" || x.Name == Odm + "CodeListItem").ToList();
        var external = codeList.Elements(Def + "ExternalCodeList").Any();
        if (terms.Count == 0 && !external)
            Add(diagnostics, "OD0081", Location(codeList), $"Codelist '{codeList.Attribute("OID")?.Value}' is empty.");

        DuplicateAttribute(terms, "CodedValue", "OD0079", "Codelist term", diagnostics);
        var hasDecodedValues = terms.Any(x => !string.IsNullOrWhiteSpace(x.Element(Odm + "Decode")?.Element(Odm + "TranslatedText")?.Value));
        var hasEnumeratedValues = terms.Any(x => string.IsNullOrWhiteSpace(x.Element(Odm + "Decode")?.Element(Odm + "TranslatedText")?.Value));
        if (hasDecodedValues && hasEnumeratedValues)
            Add(diagnostics, "OD0082", Location(codeList), "Codelist contains inconsistent decoded-value definitions.");

        foreach (var term in terms)
        {
            var value = term.Attribute("CodedValue")?.Value;
            if (!MatchesDataType(value, type))
                Add(diagnostics, "OD0077", Location(term), "Codelist Term Data Type mismatch.");
        }

        var format = codeList.Attribute("SASFormatName")?.Value;
        if (EqualsValue(type, "text") && !string.IsNullOrWhiteSpace(format) && !format.StartsWith('$'))
            Add(diagnostics, "OD0078", Location(codeList), "Invalid SASFormatName value for text Codelist.");
    }

    private static void ValidateDocumentReferences(XElement element, IReadOnlySet<string> leafIds, ICollection<DefineDiagnostic> diagnostics)
    {
        foreach (var reference in element.Descendants(Def + "DocumentRef"))
        {
            var id = reference.Attribute("leafID")?.Value;
            if (string.IsNullOrWhiteSpace(id) || !leafIds.Contains(id))
                Add(diagnostics, "DD0015", Location(reference), "Referenced Document is missing.");
            foreach (var page in reference.Elements(Def + "PDFPageRef"))
            {
                if (string.IsNullOrWhiteSpace(page.Attribute("PageRefs")?.Value) &&
                    (string.IsNullOrWhiteSpace(page.Attribute("FirstPage")?.Value) || string.IsNullOrWhiteSpace(page.Attribute("LastPage")?.Value)))
                {
                    Add(diagnostics, "DD0037", Location(page), "Missing or invalid page range.");
                }
            }
        }
    }

    private static bool MatchesDataType(string? value, string? dataType) => dataType?.ToLowerInvariant() switch
    {
        "integer" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
        "float" => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
        _ => true
    };

    private static HashSet<string> Oids(IEnumerable<XElement> elements) => elements
        .Select(x => x.Attribute("OID")?.Value)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToHashSet(StringComparer.Ordinal);

    private static void Required(XElement element, XName name, string ruleId, ICollection<DefineDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(element.Attribute(name)?.Value))
            Add(diagnostics, ruleId, Location(element), $"Missing required '{name.LocalName}' value for '{element.Name.LocalName}'.");
    }

    private static void RequiredText(XElement parent, XName name, string ruleId, ICollection<DefineDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(parent.Element(name)?.Value))
            Add(diagnostics, ruleId, Location(parent), $"Missing required '{name.LocalName}' value.");
    }

    private static bool IsSdtmOrSend(XElement group)
    {
        var standardOid = group.Attribute(Def + "StandardOID")?.Value;
        return standardOid?.Contains("SDTM", StringComparison.OrdinalIgnoreCase) == true ||
               standardOid?.Contains("SEND", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsSplitDataset(string name, string? domain) =>
        !string.IsNullOrWhiteSpace(domain) && name.Length > domain.Length && name.Length <= domain.Length + 2 &&
        name.StartsWith(domain, StringComparison.OrdinalIgnoreCase);

    private static void YesNo(XElement element, XName name, string ruleId, ICollection<DefineDiagnostic> diagnostics)
    {
        var value = element.Attribute(name)?.Value;
        if (!string.IsNullOrWhiteSpace(value) && !EqualsValue(value, "Yes") && !EqualsValue(value, "No"))
            Add(diagnostics, ruleId, Location(element), $"Invalid {name.LocalName} value; it must be Yes or No.");
    }

    private static void DuplicateAttribute(IEnumerable<XElement> elements, XName attribute, string ruleId, string objectName, ICollection<DefineDiagnostic> diagnostics)
    {
        foreach (var duplicate in elements.Where(x => !string.IsNullOrWhiteSpace(x.Attribute(attribute)?.Value))
                     .GroupBy(x => x.Attribute(attribute)!.Value, StringComparer.Ordinal)
                     .Where(x => x.Count() > 1))
        {
            foreach (var element in duplicate)
                Add(diagnostics, ruleId, Location(element), $"Duplicate {objectName} {attribute.LocalName} '{duplicate.Key}'.");
        }
    }

    private static IReadOnlyList<DefineDiagnostic> Distinct(IEnumerable<DefineDiagnostic> diagnostics) => diagnostics
        .DistinctBy(x => (x.RuleId, x.Severity, x.Location, x.Message), StringTupleComparer.Instance)
        .ToList();

    private static void Add(ICollection<DefineDiagnostic> diagnostics, string ruleId, string location, string message, DefineDiagnosticSeverity severity = DefineDiagnosticSeverity.Error) =>
        diagnostics.Add(new DefineDiagnostic(ruleId, severity, location, message));

    private static string Location(XObject node) => node is IXmlLineInfo info && info.HasLineInfo()
        ? $"Line {info.LineNumber}, Position {info.LinePosition}"
        : node is XElement element ? element.Name.LocalName : "XML";

    private static bool EqualsValue(string? left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static bool StartsWith(string? value, string prefix) => value?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true;

    private sealed class StringTupleComparer : IEqualityComparer<(string RuleId, DefineDiagnosticSeverity Severity, string Location, string Message)>
    {
        public static readonly StringTupleComparer Instance = new();
        public bool Equals((string RuleId, DefineDiagnosticSeverity Severity, string Location, string Message) x, (string RuleId, DefineDiagnosticSeverity Severity, string Location, string Message) y) =>
            x.Severity == y.Severity && string.Equals(x.RuleId, y.RuleId, StringComparison.Ordinal) &&
            string.Equals(x.Location, y.Location, StringComparison.Ordinal) && string.Equals(x.Message, y.Message, StringComparison.Ordinal);
        public int GetHashCode((string RuleId, DefineDiagnosticSeverity Severity, string Location, string Message) value) =>
            HashCode.Combine(value.RuleId, value.Severity, value.Location, value.Message);
    }
}
