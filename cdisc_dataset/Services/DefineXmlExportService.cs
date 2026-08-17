using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services.Interface;
using cdisc_dataset.Utils;
using SqlSugar;

namespace cdisc_dataset.Services;

public sealed class DefineXmlExportService(
    ISqlSugarClient sqlSugar,
    ICurrentProjectService currentProjectService) : IDefineXmlExportService
{
    private const string Odm = "http://www.cdisc.org/ns/odm/v1.3";
    private const string Def = "http://www.cdisc.org/ns/def/v2.1";
    private const string XLink = "http://www.w3.org/1999/xlink";
    private const string Xml = "http://www.w3.org/XML/1998/namespace";

    public async Task ExportAsync(Stream outputStream) => await WriteAsync(outputStream);

    public async Task<string> GenerateXmlAsync()
    {
        await using var outputStream = new MemoryStream();
        await WriteAsync(outputStream);
        return Encoding.UTF8.GetString(outputStream.ToArray());
    }

    private async Task WriteAsync(Stream outputStream)
    {
        var project = currentProjectService.CurrentProject
            ?? throw new InvalidOperationException("Please select a project before exporting.");
        var type = currentProjectService.CdiscDataType;
        var projectId = project.Id;

        var datasets = await sqlSugar.Queryable<Dataset>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == type)
            .OrderBy(x => x.Name).ToListAsync();
        var variables = await sqlSugar.Queryable<Variable>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == type)
            .OrderBy(x => x.DatasetName).OrderBy(x => x.Order).ToListAsync();
        var valueLevels = await sqlSugar.Queryable<ValueLevel>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == type)
            .OrderBy(x => x.Dataset).OrderBy(x => x.Variable).OrderBy(x => x.Order).ToListAsync();
        var valueLevelIds = valueLevels.Select(x => x.Id).ToList();
        var whereClauses = valueLevelIds.Count == 0
            ? []
            : await sqlSugar.Queryable<WhereClause>().Where(x => valueLevelIds.Contains(x.ValueLevelId)).ToListAsync();
        var codeLists = await sqlSugar.Queryable<CodeList>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == type)
            .OrderBy(x => x.UniqueId).ToListAsync();
        var codeListIds = codeLists.Select(x => x.Id).ToList();
        var terms = codeListIds.Count == 0
            ? []
            : await sqlSugar.Queryable<Term>().Where(x => codeListIds.Contains(x.CodeListId))
                .OrderBy(x => x.CodeListId).OrderBy(x => x.Order).ToListAsync();
        var dictionaries = await sqlSugar.Queryable<Models.Dictionary>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == type)
            .OrderBy(x => x.UniqueId).ToListAsync();
        var methods = await sqlSugar.Queryable<Method>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == type)
            .OrderBy(x => x.UniqueId).ToListAsync();
        var comments = await sqlSugar.Queryable<Comment>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == type)
            .OrderBy(x => x.UniqueId).ToListAsync();
        var documents = await sqlSugar.Queryable<Document>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == type)
            .OrderBy(x => x.UniqueId).ToListAsync();


        var whereByValueLevel = whereClauses.GroupBy(x => x.ValueLevelId)
            .ToDictionary(x => x.Key, x => x.ToList());
        await using var generatedStream = new MemoryStream();
        await Task.Run(() => WriteXml(generatedStream, project, type, datasets, variables, valueLevels,
            whereByValueLevel, codeLists, terms, dictionaries, methods, comments, documents));

        var xml = Encoding.UTF8.GetString(generatedStream.ToArray());
        xml = NormalizeXmlFormatting(xml);
        var bytes = new UTF8Encoding(false).GetBytes(xml);
        await outputStream.WriteAsync(bytes);
    }

    private static void WriteXml(Stream output, Project project, CdiscDataType type,
        IReadOnlyList<Dataset> datasets, IReadOnlyList<Variable> variables,
        IReadOnlyList<ValueLevel> valueLevels, IReadOnlyDictionary<int, List<WhereClause>> whereByValueLevel,
        IReadOnlyList<CodeList> codeLists, IReadOnlyList<Term> terms,
        IReadOnlyList<Models.Dictionary> dictionaries, IReadOnlyList<Method> methods,
        IReadOnlyList<Comment> comments, IReadOnlyList<Document> documents)
    {
        var standard = type == CdiscDataType.Sdtm ? "SDTMIG" : "ADaMIG";
        var version = type == CdiscDataType.Sdtm ? Description(project.SdtmIgVersion) : Description(project.AdamIgVersion);
        var study = Part(project.ProjectCode, "STUDY");
        var fileOid = $"{study}.{standard}.{version}";
        var lang = project.Language == Language.Zh ? "zh" : "en";
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false), Indent = true, IndentChars = "   ",
            NewLineChars = "\r\n", NewLineHandling = NewLineHandling.Replace, CloseOutput = false
        };

        using var w = XmlWriter.Create(output, settings);
        w.WriteStartDocument();
        w.WriteProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"define2-1-0.xsl\"");
        w.WriteStartElement("ODM", Odm);
        w.WriteAttributeString("xmlns", "xlink", null, XLink);
        w.WriteAttributeString("xmlns", Odm);
        w.WriteAttributeString("xmlns", "def", null, Def);
        w.WriteAttributeString("ODMVersion", "1.3.2");
        w.WriteAttributeString("FileType", "Snapshot");
        w.WriteAttributeString("FileOID", fileOid);
        w.WriteAttributeString("CreationDateTime", DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));
        w.WriteAttributeString("SourceSystem", "cdisc_dataset");
        w.WriteAttributeString("SourceSystemVersion", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown");
        w.WriteAttributeString("def", "Context", Def, "Submission");

        w.WriteStartElement("Study", Odm); w.WriteAttributeString("OID", fileOid);
        w.WriteStartElement("GlobalVariables", Odm);
        Element(w, "StudyName", project.ProjectCode); Element(w, "StudyDescription", project.ProtocolDescription); Element(w, "ProtocolName", project.ProtocolCode);
        w.WriteEndElement();
        w.WriteStartElement("MetaDataVersion", Odm);
        w.WriteAttributeString("OID", $"MDV.{fileOid}"); w.WriteAttributeString("Name", $"Study {study} Data Definitions");
        w.WriteAttributeString("def", "DefineVersion", Def, "2.1.0");
        WriteStandards(w, type, standard, version, codeLists);
        WriteDocuments(w, documents);
        WriteValueLists(w, valueLevels, variables, whereByValueLevel);
        WriteWhereClauses(w, valueLevels, variables, whereByValueLevel);
        WriteItemGroups(w, datasets, variables, standard, version, lang);
        WriteItems(w, datasets, variables, valueLevels, whereByValueLevel, dictionaries,
            FindAnnotatedCrf(documents)?.UniqueId, lang);
        WriteCodeLists(w, type, codeLists, terms, dictionaries, lang);
        WriteMethods(w, methods, documents, lang);
        WriteComments(w, comments, documents, lang);
        WriteLeaves(w, documents);
        w.WriteEndElement(); w.WriteEndElement(); w.WriteEndElement(); w.WriteEndDocument();
    }

    private static void WriteStandards(XmlWriter w, CdiscDataType type, string standard, string version, IReadOnlyList<CodeList> lists)
    {
        w.WriteStartElement("def", "Standards", Def);
        var ct = lists.Select(x => x.Terminology).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (!string.IsNullOrWhiteSpace(ct))
        {
            var ctVersion = TerminologyVersion(ct);
            w.WriteStartElement("def", "Standard", Def); Attr(w, "OID", ControlledTerminologyStandardOid(type, ctVersion));
            Attr(w, "Name", "CDISC/NCI"); Attr(w, "Type", "CT"); Attr(w, "Version", ctVersion); Attr(w, "PublishingSet", type == CdiscDataType.Sdtm ? "SDTM" : "ADaM"); Attr(w, "Status", "FINAL"); w.WriteEndElement();
        }
        w.WriteStartElement("def", "Standard", Def); Attr(w, "OID", ImplementationGuideStandardOid(standard, version)); Attr(w, "Name", standard); Attr(w, "Type", "IG"); Attr(w, "Version", version); Attr(w, "Status", "FINAL"); w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void WriteDocuments(XmlWriter w, IReadOnlyList<Document> documents)
    {
        var acrf = FindAnnotatedCrf(documents);
        if (acrf != null) { w.WriteStartElement("def", "AnnotatedCRF", Def); DocumentRef(w, acrf.UniqueId); w.WriteEndElement(); }
        var rest = documents.Where(x => x != acrf).ToList();
        if (rest.Count == 0) return;
        w.WriteStartElement("def", "SupplementalDoc", Def); foreach (var d in rest) DocumentRef(w, d.UniqueId); w.WriteEndElement();
    }

    private static Document? FindAnnotatedCrf(IEnumerable<Document> documents) =>
        documents.FirstOrDefault(x => string.Equals(x.UniqueId, "acrf", StringComparison.OrdinalIgnoreCase) ||
                                      x.Title?.Contains("Annotated CRF", StringComparison.OrdinalIgnoreCase) == true);

    private static void DocumentRef(XmlWriter w, string? id, string? pages = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        w.WriteStartElement("def", "DocumentRef", Def);
        Attr(w, "leafID", Leaf(id));
        if (!string.IsNullOrWhiteSpace(pages))
        {
            w.WriteStartElement("def", "PDFPageRef", Def);
            Attr(w, "Type", "PhysicalRef");
            Attr(w, "PageRefs", pages.Trim());
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteValueLists(XmlWriter w, IReadOnlyList<ValueLevel> levels, IReadOnlyList<Variable> variables, IReadOnlyDictionary<int, List<WhereClause>> whereById)
    {
        foreach (var group in levels.Where(x => HasConditions(x, whereById) && !string.IsNullOrWhiteSpace(x.Dataset) && !string.IsNullOrWhiteSpace(x.Variable)).GroupBy(x => (x.Dataset!, x.Variable!)))
        {
            w.WriteStartElement("def", "ValueListDef", Def); Attr(w, "OID", $"VL.{Part(group.Key.Item1, "DATASET")}.{Part(group.Key.Item2, "VARIABLE")}");
            foreach (var level in group.OrderBy(x => x.Order))
            {
                w.WriteStartElement("ItemRef", Odm); Attr(w, "ItemOID", ValueItem(level, variables, whereById)); Attr(w, "OrderNumber", level.Order.ToString(CultureInfo.InvariantCulture)); Attr(w, "Mandatory", YesNo(level.Mandatory));
                w.WriteStartElement("def", "WhereClauseRef", Def); Attr(w, "WhereClauseOID", WhereOid(level, variables, whereById)); w.WriteEndElement(); w.WriteEndElement();
            }
            w.WriteEndElement();
        }
    }

    private static void WriteWhereClauses(XmlWriter w, IReadOnlyList<ValueLevel> levels, IReadOnlyList<Variable> variables, IReadOnlyDictionary<int, List<WhereClause>> whereById)
    {
        foreach (var level in levels.Where(x => HasConditions(x, whereById)).GroupBy(x => WhereOid(x, variables, whereById)).Select(x => x.First()))
        {
            var clauses = GetConditions(level, whereById);
            w.WriteStartElement("def", "WhereClauseDef", Def); Attr(w, "OID", WhereOid(level, variables, whereById));
            foreach (var clause in clauses)
            {
                w.WriteStartElement("RangeCheck", Odm); Attr(w, "SoftHard", "Soft"); Attr(w, "def", "ItemOID", Def, $"IT.{Part(level.Dataset, "DATASET")}.{Part(clause.Variable, "VARIABLE")}"); Attr(w, "Comparator", Comparator(clause.Comparator));
                foreach (var value in Values(clause.Values)) Element(w, "CheckValue", value);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
    }

    private static void WriteItemGroups(XmlWriter w, IReadOnlyList<Dataset> datasets, IReadOnlyList<Variable> variables, string standard, string version, string lang)
    {
        foreach (var dataset in datasets
                     .OrderBy(x => DatasetClassOrder(x.Class))
                     .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var name = Part(dataset.Name, $"DATASET{dataset.Id}");
            w.WriteStartElement("ItemGroupDef", Odm); Attr(w, "OID", $"IG.{name}"); Attr(w, "Domain", DatasetDomain(name)); Attr(w, "Name", name); Attr(w, "Repeating", YesNo(dataset.Repeating)); Attr(w, "IsReferenceData", YesNo(dataset.ReferenceData)); Attr(w, "SASDatasetName", name); Attr(w, "Purpose", "Tabulation");
            Optional(w, "def", "Structure", Def, dataset.Structure);
            if (IsNoData(dataset.HasNoData))
                Attr(w, "def", "HasNoData", Def, "Yes");
            else if (HasArchiveLocation(name, dataset.HasNoData))
                Attr(w, "def", "ArchiveLocationID", Def, $"LF.{name}");
            Attr(w, "def", "StandardOID", Def, ImplementationGuideStandardOid(standard, version)); Optional(w, "def", "CommentOID", Def, Comment(dataset.CommentUniqueId)); Description(w, dataset.Label, lang);
            foreach (var variable in variables.Where(x => string.Equals(x.DatasetName, dataset.Name, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Order))
            {
                var variableName = Part(variable.VariableName, "VARIABLE");
                w.WriteStartElement("ItemRef", Odm); Attr(w, "ItemOID", $"IT.{name}.{variableName}"); Attr(w, "Mandatory", variable.Mandatory ?? string.Empty); Attr(w, "OrderNumber", variable.Order.ToString(CultureInfo.InvariantCulture));
                var keySequence = KeySequence(dataset.KeyVariables, variables, dataset.Name, variableName);
                Optional(w, null, "KeySequence", null, keySequence);
                Optional(w, null, "Role", null, variable.Role);
                if (IsNoData(variable.HasNoData)) Attr(w, "def", "HasNoData", Def, "Yes");
                w.WriteEndElement();
            }
            if (!string.IsNullOrWhiteSpace(dataset.Class))
            {
                w.WriteStartElement("def", "Class", Def);
                Attr(w, "Name", dataset.Class.Trim());
                w.WriteEndElement();
            }
            if (HasArchiveLocation(name, dataset.HasNoData))
            {
                w.WriteStartElement("def", "leaf", Def);
                Attr(w, "ID", $"LF.{name}");
                Attr(w, "xlink", "href", XLink, $"{name.ToLowerInvariant()}.xpt");
                Element(w, "def:title", $"{name.ToLowerInvariant()}.xpt");
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
    }

    private static void WriteItems(XmlWriter w, IReadOnlyList<Dataset> datasets, IReadOnlyList<Variable> variables, IReadOnlyList<ValueLevel> levels, IReadOnlyDictionary<int, List<WhereClause>> whereByValueLevel, IReadOnlyList<Models.Dictionary> dictionaries, string? annotatedCrfId, string lang)
    {
        var datasetOrder = datasets
            .OrderBy(x => DatasetClassOrder(x.Class))
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select((x, index) => new { x.Name, Index = index })
            .ToDictionary(x => x.Name ?? string.Empty, x => x.Index, StringComparer.OrdinalIgnoreCase);
        foreach (var v in variables
                     .OrderBy(x => datasetOrder.TryGetValue(x.DatasetName ?? string.Empty, out var order) ? order : int.MaxValue)
                     .ThenBy(x => x.Order))
        {
            var dataset = Part(v.DatasetName, "DATASET"); var name = Part(v.VariableName, "VARIABLE");
            var hasValueList = levels.Any(x => HasConditions(x, whereByValueLevel) && string.Equals(x.Dataset, v.DatasetName, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Variable, v.VariableName, StringComparison.OrdinalIgnoreCase));
            Item(w, $"IT.{dataset}.{name}", name, name, v.DataType, v.Length, v.SignificantDigits, v.Format, v.Label, v.CodeListUniqueId, v.DictionaryUniqueId, v.Origin, v.Source, v.Pages, v.Predecessor, v.MethodUniqueId, v.CommentUniqueId, hasValueList ? $"VL.{dataset}.{name}" : null, dictionaries, annotatedCrfId, lang);
        }
        foreach (var v in levels.Where(x => HasConditions(x, whereByValueLevel)))
        {
            var dataset = Part(v.Dataset, "DATASET");
            var itemOid = ValueItem(v, variables, whereByValueLevel);
            var name = itemOid[("IT." + dataset + ".").Length..];
            var sasFieldName = Part(v.Variable, "VARIABLE");
            Item(w, itemOid, name, sasFieldName, v.Type, v.Length, v.Digits, v.Format, v.Label, v.CodeListUniqueId, null, v.Origin, v.Source, v.Pages, v.Predecessor, v.MethodUniqueId, v.CommentUniqueId, null, dictionaries, annotatedCrfId, lang);
        }
    }

    private static void Item(XmlWriter w, string oid, string name, string sasFieldName, string? dataType, int? length, int? digits, string? format, string? label, string? codeList, string? dictionaryId, string? origin, string? source, string? pages, string? predecessor, string? method, string? comment, string? valueList, IReadOnlyList<Models.Dictionary> dictionaries, string? annotatedCrfId, string lang)
    {
        w.WriteStartElement("ItemDef", Odm); Attr(w, "OID", oid); Attr(w, "Name", name); Attr(w, "DataType", DataType(dataType)); Optional(w, null, "Length", null, length?.ToString(CultureInfo.InvariantCulture)); Optional(w, null, "SignificantDigits", null, digits?.ToString(CultureInfo.InvariantCulture)); Attr(w, "SASFieldName", sasFieldName); Optional(w, "def", "DisplayFormat", Def, format); Optional(w, "def", "CommentOID", Def, Comment(comment)); Description(w, label, lang);
        if (!string.IsNullOrWhiteSpace(codeList))
        {
            w.WriteStartElement("CodeListRef", Odm);
            Attr(w, "CodeListOID", $"CL.{Part(codeList, "CODELIST")}");
            w.WriteEndElement();
        }
        else if (!string.IsNullOrWhiteSpace(dictionaryId) && dictionaries.Any(x => string.Equals(x.UniqueId, dictionaryId, StringComparison.OrdinalIgnoreCase)))
        {
            w.WriteStartElement("CodeListRef", Odm);
            Attr(w, "CodeListOID", $"CL.{Part(dictionaryId, "DICTIONARY")}");
            w.WriteEndElement();
        }
        if (!string.IsNullOrWhiteSpace(origin) || !string.IsNullOrWhiteSpace(source) || !string.IsNullOrWhiteSpace(predecessor))
        {
            w.WriteStartElement("def", "Origin", Def);
            Attr(w, "Type", string.IsNullOrWhiteSpace(origin) ? "Other" : origin);
            Optional(w, null, "Source", null, source);
            if (string.Equals(origin, "Predecessor", StringComparison.OrdinalIgnoreCase))
                Description(w, predecessor, lang);
            if (string.Equals(origin, "Collected", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(source, "Investigator", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(source, "Subject", StringComparison.OrdinalIgnoreCase)))
            {
                DocumentRef(w, annotatedCrfId, pages);
            }
            w.WriteEndElement();
        }
        if (!string.IsNullOrWhiteSpace(valueList)) { w.WriteStartElement("def", "ValueListRef", Def); Attr(w, "ValueListOID", valueList); w.WriteEndElement(); }
        if (!string.IsNullOrWhiteSpace(method)) { w.WriteStartElement("MethodRef", Odm); Attr(w, "MethodOID", $"MT.{Part(method, "METHOD")}"); w.WriteEndElement(); }
        w.WriteEndElement();
    }

    private static void WriteCodeLists(XmlWriter w, CdiscDataType type, IReadOnlyList<CodeList> lists, IReadOnlyList<Term> terms, IReadOnlyList<Models.Dictionary> dictionaries, string lang)
    {
        foreach (var dictionary in dictionaries)
        {
            w.WriteStartElement("CodeList", Odm);
            Attr(w, "OID", $"CL.{Part(dictionary.UniqueId, "DICTIONARY")}");
            Attr(w, "Name", dictionary.Name ?? Part(dictionary.UniqueId, "DICTIONARY"));
            Attr(w, "DataType", DataType(dictionary.DataType));
            w.WriteStartElement("ExternalCodeList", Odm);
            Attr(w, "Dictionary", dictionary.DictionaryName ?? dictionary.Name ?? Part(dictionary.UniqueId, "DICTIONARY"));
            Optional(w, null, "Version", null, dictionary.Version);
            w.WriteEndElement();
            w.WriteEndElement();
        }

        foreach (var list in lists)
        {
            w.WriteStartElement("CodeList", Odm); Attr(w, "OID", $"CL.{Part(list.UniqueId, "CODELIST")}"); Attr(w, "Name", list.Name ?? Part(list.UniqueId, "CODELIST")); Attr(w, "DataType", DataType(list.Type)); Optional(w, "def", "CommentOID", Def, Comment(list.CommentUniqueId));
            var terminologyVersion = lists.Select(x => x.Terminology).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            if (!string.IsNullOrWhiteSpace(terminologyVersion))
                Attr(w, "def", "StandardOID", Def,
                    ControlledTerminologyStandardOid(type, TerminologyVersion(terminologyVersion)));
            var listTerms = terms.Where(x => x.CodeListId == list.Id).OrderBy(x => x.Order).ToList();
            var isEnumeratedList = listTerms.All(x => string.IsNullOrWhiteSpace(x.Code) && string.IsNullOrWhiteSpace(x.DecodedValue));
            if (listTerms.Count == 0)
            {
                w.WriteStartElement("EnumeratedItem", Odm);
                Attr(w, "CodedValue", string.Empty);
                Attr(w, "def", "ExtendedValue", Def, "Yes");
                w.WriteEndElement();
            }
            foreach (var term in listTerms)
            {
                w.WriteStartElement(isEnumeratedList ? "EnumeratedItem" : "CodeListItem", Odm); Attr(w, "CodedValue", term.Name); Attr(w, "OrderNumber", term.Order.ToString(CultureInfo.InvariantCulture));
                if (!isEnumeratedList) { w.WriteStartElement("Decode", Odm); Translated(w, term.DecodedValue, lang); w.WriteEndElement(); }
                if (!string.IsNullOrWhiteSpace(term.Code))
                {
                    w.WriteStartElement("Alias", Odm); Attr(w, "Name", term.Code); Attr(w, "Context", "nci:ExtCodeID"); w.WriteEndElement();
                }
                w.WriteEndElement();
            }
            if (!string.IsNullOrWhiteSpace(list.Code))
            {
                w.WriteStartElement("Alias", Odm); Attr(w, "Name", list.Code); Attr(w, "Context", "nci:ExtCodeID"); w.WriteEndElement();
            }
            w.WriteEndElement();
        }
    }

    private static void WriteMethods(XmlWriter w, IReadOnlyList<Method> methods, IReadOnlyList<Document> documents, string lang)
    {
        foreach (var method in methods)
        {
            w.WriteStartElement("MethodDef", Odm);
            Attr(w, "OID", $"MT.{Part(method.UniqueId, "METHOD")}");
            Attr(w, "Name", method.Name ?? Part(method.UniqueId, "METHOD"));
            Attr(w, "Type", method.Type ?? "Computation");
            Description(w, method.Description, lang);
            if (!string.IsNullOrWhiteSpace(method.ExpressionCode))
            {
                w.WriteStartElement("FormalExpression", Odm);
                Optional(w, null, "Context", null, method.ExpressionContext);
                w.WriteString(method.ExpressionCode);
                w.WriteEndElement();
            }
            if (DocumentExists(documents, method.DocumentUniqueId))
                DocumentRef(w, method.DocumentUniqueId, method.Pages);
            w.WriteEndElement();
        }
    }

    private static void WriteComments(XmlWriter w, IReadOnlyList<Comment> comments, IReadOnlyList<Document> documents, string lang)
    {
        foreach (var comment in comments)
        {
            w.WriteStartElement("def", "CommentDef", Def);
            Attr(w, "OID", $"COM.{Part(comment.UniqueId, "COMMENT")}");
            Description(w, comment.Description, lang);
            if (DocumentExists(documents, comment.DocumentUniqueId))
                DocumentRef(w, comment.DocumentUniqueId, comment.Pages);
            w.WriteEndElement();
        }
    }

    private static void WriteLeaves(XmlWriter w, IReadOnlyList<Document> documents)
    {
        foreach (var d in documents) { w.WriteStartElement("def", "leaf", Def); Attr(w, "ID", Leaf(d.UniqueId)); Attr(w, "xlink", "href", XLink, d.Href ?? string.Empty); Element(w, "def:title", d.Title ?? d.Href ?? d.UniqueId); w.WriteEndElement(); }
    }

    private static void Description(XmlWriter w, string? value, string lang) { if (string.IsNullOrWhiteSpace(value)) return; w.WriteStartElement("Description", Odm); Translated(w, value, lang); w.WriteEndElement(); }
    private static void Translated(XmlWriter w, string? value, string lang) { w.WriteStartElement("TranslatedText", Odm); w.WriteString(value ?? string.Empty); w.WriteEndElement(); }
    private static void Element(XmlWriter w, string name, string? value) { if (name.StartsWith("def:", StringComparison.Ordinal)) w.WriteElementString("def", name[4..], Def, value ?? string.Empty); else w.WriteElementString(name, Odm, value ?? string.Empty); }
    private static void Attr(XmlWriter w, string name, string value) => w.WriteAttributeString(name, value);
    private static void Attr(XmlWriter w, string prefix, string name, string ns, string value) => w.WriteAttributeString(prefix, name, ns, value);
    private static void Optional(XmlWriter w, string? prefix, string name, string? ns, string? value) { if (!string.IsNullOrWhiteSpace(value)) w.WriteAttributeString(prefix, name, ns, value.Trim()); }
    private static string KeySequence(string? keyVariables, IReadOnlyList<Variable> variables, string? datasetName, string variableName)
    {
        if (string.IsNullOrWhiteSpace(keyVariables))
            return string.Empty;

        var existing = variables
            .Where(x => string.Equals(x.DatasetName, datasetName, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.VariableName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keys = keyVariables.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(existing.Contains)
            .ToList();
        var index = keys.FindIndex(x => string.Equals(x, variableName, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? (index + 1).ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private static bool DocumentExists(IEnumerable<Document> documents, string? uniqueId) =>
        !string.IsNullOrWhiteSpace(uniqueId) &&
        documents.Any(x => string.Equals(x.UniqueId, uniqueId, StringComparison.OrdinalIgnoreCase));

    private static string ImplementationGuideStandardOid(string standard, string version) =>
        $"STD.{OidPart(standard, "STANDARD")}.{OidPart(version, "VERSION")}";

    private static string ControlledTerminologyStandardOid(CdiscDataType type, string version) =>
        $"STD.{(type == CdiscDataType.Sdtm ? "SDTM" : "ADaM")}.CT.{OidPart(version, "VERSION")}";

    private static string OidPart(string? value, string fallback) =>
        Regex.Replace(Part(value, fallback), @"\s+", ".");

    private static string Part(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private static int DatasetClassOrder(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "TRIAL DESIGN" => 0,
        "SPECIAL PURPOSE" => 1,
        "INTERVENTIONS" => 2,
        "EVENTS" => 3,
        "FINDINGS" => 4,
        "FINDINGS ABOUT" => 5,
        "RELATIONSHIP" => 6,
        _ => 7
    };
    private static string DatasetDomain(string name) => name.StartsWith("SUPP", StringComparison.OrdinalIgnoreCase) && name.Length == 6 ? name[4..] : name;
    private static bool HasTransportLeaf(string name) => string.Equals(name, "SU", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "SR", StringComparison.OrdinalIgnoreCase);
    private static bool HasArchiveLocation(string name, string? hasNoData) => !IsNoData(hasNoData) || HasTransportLeaf(name);
    private static bool IsNoData(string? value) => string.Equals(YesNo(value), "Yes", StringComparison.Ordinal);
    private static string Comment(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : $"COM.{Part(value, "COMMENT")}";
    private static string Leaf(string? value) => $"LF.{Part(value, "DOCUMENT")}";
    private static string YesNo(string? value) => string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1" ? "Yes" : "No";
    private static string DataType(string? value) => value?.Trim().ToLowerInvariant() switch { "char" or "character" or "string" => "text", "num" or "numeric" or "decimal" => "float", "integer" => "integer", "date" => "date", "datetime" => "datetime", "durationdatetime" => "durationDatetime", "time" => "time", var x when !string.IsNullOrWhiteSpace(x) => x, _ => "text" };
    private static string Comparator(string? value) => value?.Trim().ToUpperInvariant() switch { "=" or "==" => "EQ", "!=" or "<>" => "NE", ">" => "GT", ">=" => "GE", "<" => "LT", "<=" => "LE", var x when !string.IsNullOrWhiteSpace(x) => x, _ => "EQ" };
    private static IEnumerable<string> Values(string? value) => string.IsNullOrWhiteSpace(value) ? [string.Empty] : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string WhereOid(ValueLevel level, IReadOnlyList<Variable> variables, IReadOnlyDictionary<int, List<WhereClause>> map) =>
        $"WC.{Part(level.Dataset, "DATASET")}.{string.Join('.', Conditions(level, variables, map, includeDataset: false))}";

    private static string ValueItem(ValueLevel level, IReadOnlyList<Variable> variables, IReadOnlyDictionary<int, List<WhereClause>> map) =>
        $"IT.{Part(level.Dataset, "DATASET")}.{Part(level.Variable, "VARIABLE")}.{string.Join('.', Conditions(level, variables, map, includeDataset: true))}";

    private static IEnumerable<string> Conditions(ValueLevel level, IReadOnlyList<Variable> variables, IReadOnlyDictionary<int, List<WhereClause>> map, bool includeDataset)
    {
        var clauses = GetOidConditions(level, variables, map);
        var dataset = Part(level.Dataset, "DATASET");
        var descriptors = clauses.Select((x, index) =>
        {
            var prefix = includeDataset || index > 0 ? $"{dataset}." : string.Empty;
            return $"{prefix}{Part(x.Variable, "VARIABLE")}.{Comparator(x.Comparator)}";
        });
        var values = string.Concat(clauses.Select(x => Part(x.Values, "VALUE")));
        return descriptors.Append(values);
    }

    private static bool HasConditions(ValueLevel level, IReadOnlyDictionary<int, List<WhereClause>> map) =>
        ParseWhereClause(level.WhereClause).Count > 0 || (map.TryGetValue(level.Id, out var stored) && stored.Count > 0);

    private static List<WhereClause> GetConditions(ValueLevel level, IReadOnlyDictionary<int, List<WhereClause>> map) =>
        map.TryGetValue(level.Id, out var stored) && stored.Count > 0 ? stored : ParseWhereClause(level.WhereClause);

    private static List<WhereClause> GetOidConditions(ValueLevel level, IReadOnlyList<Variable> variables, IReadOnlyDictionary<int, List<WhereClause>> map)
    {
        var orderByVariable = variables
            .Where(x => string.Equals(x.DatasetName, level.Dataset, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.VariableName ?? string.Empty, x => x.Order, StringComparer.OrdinalIgnoreCase);
        return GetConditions(level, map)
            .OrderBy(x => orderByVariable.TryGetValue(x.Variable ?? string.Empty, out var order) ? order : int.MaxValue)
            .ToList();
    }

    private static List<WhereClause> ParseWhereClause(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return [];

        var clauses = new List<WhereClause>();
        foreach (var part in Regex.Split(expression.Trim(), @"\s+AND\s+", RegexOptions.IgnoreCase))
        {
            var match = Regex.Match(part.Trim(),
                @"^(?<variable>(?:[A-Za-z_][A-Za-z0-9_]*\.)?[A-Za-z_][A-Za-z0-9_]*)\s+(?<comparator>EQ|NE|LT|LE|GT|GE|IN|NOT\s*IN|IS\s+NULL|IS\s+NOT\s+NULL|=|!=|<=|<|>=|>)\s*(?<values>.*)$",
                RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;

            clauses.Add(new WhereClause
            {
                Variable = match.Groups["variable"].Value.Split('.').Last(),
                Comparator = match.Groups["comparator"].Value,
                Values = UnwrapOuterParentheses(match.Groups["values"].Value.Trim())
            });
        }
        return clauses;
    }

    private static string UnwrapOuterParentheses(string value) =>
        value.Length >= 2 && value[0] == '(' && value[^1] == ')' ? value[1..^1].Trim() : value;

    private static string TerminologyVersion(string value)
    {
        var match = Regex.Match(value.Trim(), @"(?<version>\d{4}-\d{2}-\d{2})$");
        return match.Success ? match.Groups["version"].Value : value.Trim();
    }

    private static string NormalizeXmlFormatting(string xml)
    {
        xml = xml.Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?>", "<?xml version=\"1.0\" encoding=\"UTF-8\"?>", StringComparison.Ordinal);
        return xml.Replace(" />", "/>", StringComparison.Ordinal);
    }

    private static string Description<T>(T value) where T : struct, Enum => typeof(T).GetField(value.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString();
}
