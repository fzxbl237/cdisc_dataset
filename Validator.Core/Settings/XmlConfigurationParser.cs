using System.Text.RegularExpressions;
using System.Xml;
using P21.Validator.Api.Models;
using P21.Validator.Core.Util;

namespace P21.Validator.Core.Settings;

public sealed class XmlConfigurationParser : ConfigurationParser
{
    private enum DefineVersion
    {
        V1,
        V2
    }

    private const string ConfigNamespaceUri = "http://www.opencdisc.org/schema/validator";
    private const string NciNamespaceUri = "http://ncicb.nci.nih.gov/xml/odm/EVS/CDISC";
    private const string XlinkNamespaceUri = "http://www.w3.org/1999/xlink";
    private const string GroupingSeparator = "\u001C";
    private const string PairGroupingSeparator = "\u001F";

    
    private readonly ICollection<FileInfo> _configurationFiles;
    private readonly FileInfo? _defineFile;
    private readonly Dictionary<string, string> _defaults;
    private readonly List<ErrorAccumulator> _errors = new();
    private string _cwd = string.Empty;

    public XmlConfigurationParser(ICollection<FileInfo> configurations, FileInfo? define, Dictionary<string, string>? defaults)
    {
        _configurationFiles = configurations;
        _defineFile = define;
        _defaults = defaults ?? new Dictionary<string, string>();
    }

    public IReadOnlyCollection<ErrorAccumulator> GetErrors() => _errors.AsReadOnly();

    public bool Parse(ConfigurationManager manager)
    {
        var result = true;

        try
        {
            if (_defineFile != null)
            {
                var errors = new ErrorAccumulator("define", _defineFile.Name);
                if (_defineFile.Exists)
                {
                    var document = LoadDocument(_defineFile.FullName);
                    var version = DetermineDefineVersion(document);
                    if (version.HasValue)
                    {
                        ParseDefineLevelContent(document, true, manager, version.Value, errors);
                    }
                    else
                    {
                        errors.Record("Unable to determine the define.xml document version");
                    }
                }
                else
                {
                    errors.Record("The provided path is not a file");
                }

                if (!errors.HasErrors())
                {
                    _errors.Add(errors);
                }
            }
        }
        catch
        {
            // Ignore define errors
        }

        foreach (var configuration in _configurationFiles)
        {
            if (configuration == null)
            {
                continue;
            }

            var errors = new ErrorAccumulator("config", configuration.Name);
            if (configuration.Exists)
            {
                var document = LoadDocument(configuration.FullName);
                _cwd = configuration.DirectoryName ?? string.Empty;

                if (VerifyNamespace(document, ConfigNamespaceUri))
                {
                    var version = DetermineDefineVersion(document);
                    if (version.HasValue)
                    {
                        ParseDefineLevelContent(document, false, manager, version.Value, errors);
                    }

                    ParseConfigurationLevelContent(document, manager);
                }
            }

            if (!errors.HasErrors())
            {
                _errors.Add(errors);
            }
        }

        if (result)
        {
            manager.Complete();
        }

        return result;
    }

    private bool ParseDefineLevelContent(XmlDocument document, bool isDefineDocument, ConfigurationManager manager, DefineVersion version, ErrorAccumulator errors)
    {
        var metadata = Only(document, "MetaDataVersion");
        if (metadata == null)
        {
            errors.Record("The number of MetaDataVersion sections was not one");
            return false;
        }

        var contextPrefix = string.Empty;
        if (!isDefineDocument)
        {
            manager.Register(new ConfigurationManager.ConfigurationMetadata(
                metadata.GetAttribute("StandardName"),
                metadata.GetAttribute("StandardVersion"))
            );

            contextPrefix = metadata.GetAttribute("Prefix");
        }

        var context = new ParseContext(version, isDefineDocument, contextPrefix);
        ParseCodeAndValueLists(context, metadata, errors);

        var itemDefs = Every(metadata, "ItemDef");
        if (itemDefs.Count == 0)
        {
            return false;
        }

        ParseItemDefs(context, itemDefs);

        var prototypes = new KeyMap<PrototypeCriteria>();
        if (!isDefineDocument)
        {
            foreach (XmlElement prototypeDef in Every(metadata, "Prototype", ConfigNamespaceUri))
            {
                var oid = prototypeDef.GetAttribute("ItemGroupOID");
                var name = prototypeDef.GetAttribute("DatasetName");
                var keys = Helpers.TrimSplit(prototypeDef.GetAttribute("KeyVariables"), ",");
                prototypes.Put(oid, new PrototypeCriteria(name, keys));
            }
        }

        var itemGroupDefs = Every(metadata, "ItemGroupDef");
        if (itemGroupDefs.Count == 0)
        {
            return false;
        }

        return ParseItemGroupDefs(context, manager, itemGroupDefs, prototypes);
    }

    private void ParseItemDefs(ParseContext context, IReadOnlyCollection<XmlElement> itemDefs)
    {
        var resolvers = new List<ValueList.Resolver>();

        foreach (var itemDef in itemDefs)
        {
            var oid = itemDef.GetAttribute("OID");
            var definition = new ElementDefinition(Definition.Target.Variable, itemDef, itemDef.GetAttribute("Name"),
                context.IsDefaultConfig ? string.Empty : context.Prefix);

            if (definition.HasProperty("DataType"))
            {
                var type = definition.GetProperty("DataType");
                var basicType = "Char";
                var regexType = ".*";

                if (type.Equals("Integer", StringComparison.OrdinalIgnoreCase) || type.Equals("Float", StringComparison.OrdinalIgnoreCase))
                {
                    basicType = "Num";
                }

                if (type.Equals("DateTime", StringComparison.OrdinalIgnoreCase))
                {
                    regexType = "^(-|[0-9]{4})(-(-|0[0-9]|1[0-2])(-(-|[0-2][0-9]|3[0-1])(T(-|[0-1][0-9]|2[0-3])" +
                                "(:(-|[0-5][0-9])(:[0-5][0-9])?)?)?)?)?"
                    +
                    "(/(-|[0-9]{4})(-(-|0[0-9]|1[0-2])(-(-|[0-2][0-9]|3[0-1])(T(-|[0-1][0-9]|2[0-3])(:(-|[0-5][0-9])(:[0-5][0-9])?)?)?)?)?)?$";
                }
                else if (type.Equals("Date", StringComparison.OrdinalIgnoreCase))
                {
                    regexType = "^(-|[0-9]{4})(-(-|0[0-9]|1[0-2])(-(-|[0-2][0-9]|3[0-1]))?)?(/(-|[0-9]{4})(-(-|0[0-9]|1[0-2])(-(-|[0-2][0-9]|3[0-1]))?)?)?$";
                }
                else if (type.Equals("Time", StringComparison.OrdinalIgnoreCase))
                {
                    regexType = "^T(-|[0-1][0-9]|2[0-3])(:(-|[0-5][0-9])(:[0-5][0-9])?)?(/T(-|[0-1][0-9]|2[0-4])(:(-|[0-5][0-9])(:[0-5][0-9])?)?)?$";
                }
                else if (type.Equals("Integer", StringComparison.OrdinalIgnoreCase))
                {
                    regexType = "-?[0-9]+";
                }
                else if (type.Equals("Float", StringComparison.OrdinalIgnoreCase))
                {
                    regexType = "-?[0-9]+(\\.[0-9]+)?";
                }

                definition.SetProperty("Type", type);
                definition.SetProperty("Type.Basic", basicType);
                definition.SetProperty("Type.Regex", regexType);
            }

            var codelistRefs = Every(itemDef, "CodeListRef");
            if (context.Version == DefineVersion.V2)
            {
                definition.SetProperty("Label", ReadDescription(itemDef));
            }

            definition.ClearPrefix();
            definition.SetProperty(context.Prefix, "Y");

            if (!context.IsDefine)
            {
                definition.SetPrefix(context.Prefix);
            }

            if (codelistRefs.Count > 0)
            {
                var codelistRef = codelistRefs[0];
                var codeListOid = codelistRef.GetAttribute("CodeListOID").ToUpperInvariant();
                var codelist = context.GetCodeList(codeListOid);
                if (codelist != null)
                {
                    if (!codelist.IsExternal())
                    {
                        var codes = new List<string>();
                        var decodes = new List<string>();

                        foreach (var code in codelist)
                        {
                            codes.Add(code.GetValue());
                            if (code.HasDecode())
                            {
                                decodes.Add(code.GetValue() + PairGroupingSeparator + code.GetDecode());
                            }
                        }

                        definition.SetProperty("CodeList", "Y");
                        definition.SetProperty("CodeList.Name", codelist.GetName());
                        definition.SetProperty("CodeList.Values", string.Join(GroupingSeparator, codes));
                        definition.SetProperty("CodeList.Delimiter", GroupingSeparator);
                        definition.SetProperty("CodeList.PairDelimiter", PairGroupingSeparator);
                        definition.SetProperty("CodeList.Extensible", codelist.IsExtensible() ? "Y" : "N");

                        if (decodes.Count > 0)
                        {
                            definition.SetProperty("CodeList.PairedValues", string.Join(GroupingSeparator, decodes));
                        }

                        if (context.IsDefine)
                        {
                            definition.SetProperty("Define.WithCodeList", "Y");
                        }
                    }
                    else
                    {
                        definition.SetProperty("CodeList.External", "Y");
                        definition.SetProperty("CodeList.Name", codelist.GetName());
                        definition.SetProperty("CodeList.Dictionary", codelist.GetDictionary());
                        definition.SetProperty("CodeList.Version", codelist.GetVersion());
                    }
                }
            }

            if (context.Version == DefineVersion.V2)
            {
                var valueListRef = Only(itemDef, "ValueListRef", "http://www.cdisc.org/ns/def/v2.0");
                if (valueListRef != null)
                {
                    var valueList = context.GetValueList(valueListRef.GetAttribute("ValueListOID").ToUpperInvariant());
                    if (valueList != null)
                    {
                        resolvers.Add(valueList.ResolverFor(definition));
                    }
                }
            }

            definition.ClearPrefix();
            context.Add(oid, definition);
        }

        foreach (var resolver in resolvers)
        {
            resolver.Resolve(context.Variables);
        }
    }

    private bool ParseItemGroupDefs(ParseContext context, ConfigurationManager manager, IReadOnlyCollection<XmlElement> itemGroupDefs, KeyMap<PrototypeCriteria> prototypes)
    {
        var result = true;

        foreach (var itemGroupDef in itemGroupDefs)
        {
            Template? template = null;
            var name = itemGroupDef.GetAttribute("Name");
            var oid = itemGroupDef.GetAttribute("OID");

            if (manager.Defines(name))
            {
                template = manager.GetConfiguration(name);
            }

            var isPrototype = prototypes.ContainsKey(oid);
            if (isPrototype)
            {
                template ??= new Template(name, prototypes.Get(oid));
                if (template == null)
                {
                    return false;
                }
            }
            else if (template == null)
            {
                template = new Template(name, null);
            }

            var current = new ElementDefinition(Definition.Target.Domain, itemGroupDef);
            if (context.Version == DefineVersion.V2)
            {
                current.SetProperty("Label", ReadDescription(itemGroupDef));
            }

            Definition.CopyTo(current, template, "DomainKeys");

            if (context.IsDefine)
            {
                template.SetProperty("Define", "Y");
                template.MarkDefined();

                var keys = CleanDomainKeys(itemGroupDef.GetAttribute("DomainKeys"));
                if (keys != null)
                {
                    template.SetProperty("Define.DomainKeys", keys);
                }
            }
            else
            {
                template.SetProperty("Config", "Y");
            }

            var keyVariables = new SortedDictionary<int, string>();
            foreach (XmlElement itemRef in Every(itemGroupDef, "ItemRef"))
            {
                var referenceOid = itemRef.GetAttribute("ItemOID");
                var reference = new ElementDefinition(Definition.Target.Variable, itemRef,
                    context.IsDefaultConfig ? string.Empty : context.Prefix);
                var definition = context.GetVariable(referenceOid);
                if (definition == null)
                {
                    result = false;
                    continue;
                }

                reference.ClearPrefix();
                var variable = Definition.CreateFrom(definition, reference);
                var variableName = variable.GetTargetName();

                if (template.HasVariable(variableName))
                {
                    var copy = Definition.CreateFrom(template.GetVariable(variableName)!, variable);
                    if (!context.IsDefine && variable.HasProperty("OrderNumber"))
                    {
                        copy.SetProperty(context.Prefix + ".OrderNumber", variable.GetProperty("OrderNumber"));
                    }

                    variable = copy;
                }
                else if (!context.IsDefine && variable.HasProperty("OrderNumber"))
                {
                    variable.SetProperty(context.Prefix + ".OrderNumber", variable.GetProperty("OrderNumber"));
                }

                if (context.Version == DefineVersion.V2 && variable.HasProperty("KeySequence"))
                {
                    if (int.TryParse(variable.GetProperty("KeySequence"), out var sequence))
                    {
                        keyVariables[sequence] = variableName;
                    }
                }

                template.DefineVariable(variable);
            }

            if (keyVariables.Count > 0)
            {
                template.SetProperty((context.IsDefaultConfig ? string.Empty : context.Prefix) + "DomainKeys",
                    string.Join(",", keyVariables.Values));
            }

            if (!isPrototype)
            {
                manager.Define(template);
            }
            else
            {
                manager.Prototype(template);
            }
        }

        return result;
    }

    private void ParseCodeAndValueLists(ParseContext context, XmlElement metadata, ErrorAccumulator errors)
    {
        ParseCodeListElements(context, Every(metadata, "CodeList"), errors);

        if (context.Version == DefineVersion.V2)
        {
            ParseValueListElements(context,
                Every(metadata, "ValueListDef", "http://www.cdisc.org/ns/def/v2.0"),
                Every(metadata, "WhereClauseDef", "http://www.cdisc.org/ns/def/v2.0"),
                errors);
        }

        if (context.IsDefine)
        {
            return;
        }

        foreach (var terminologyReference in Every(metadata, "TerminologyRef", ConfigNamespaceUri))
        {
            var attribute = terminologyReference.GetAttribute("href",XlinkNamespaceUri);
            var path = ReplaceSystemProperties(attribute);
            var include = new FileInfo(path);
            if (!include.Exists)
            {
                include = new FileInfo(Path.Combine(_cwd, path));
            }

            if (include.Exists)
            {
                var document = LoadDocument(include.FullName);
                var metaDataVersionElement = Only(document, "MetaDataVersion");
                if (metaDataVersionElement == null)
                {
                    continue;
                }

                ParseCodeListElements(context, Every(metaDataVersionElement, "CodeList"), errors);
                ParseValueListElements(context,
                    Every(metaDataVersionElement, "ValueListDef", "http://www.cdisc.org/ns/def/v2.0"),
                    Every(metaDataVersionElement, "WhereClauseDef", "http://www.cdisc.org/ns/def/v2.0"),
                    errors);
                ParseItemDefs(context, Every(metaDataVersionElement, "ItemDef"));
            }
            else
            {
                errors.Record($"{include.FullName} can't be read / does not exist");
            }
        }
    }

    private void ParseCodeListElements(ParseContext context, IReadOnlyCollection<XmlElement> codelistDefs, ErrorAccumulator errors)
    {
        foreach (var codelistDef in codelistDefs)
        {
            Codelist? codelist = null;
            var name = codelistDef.GetAttribute("Name");
            var type = codelistDef.GetAttribute("DataType");
            var oid = codelistDef.GetAttribute("OID").ToUpperInvariant();
            var extensible = codelistDef.GetAttribute("CodeListExtensible",NciNamespaceUri);

            errors.StartContext("CodeList", oid);

            var codeDefs = Every(codelistDef, "CodeListItem");
            if (codeDefs.Count == 0 && context.Version == DefineVersion.V2)
            {
                codeDefs = Every(codelistDef, "EnumeratedItem");
            }

            if (codeDefs.Count > 0)
            {
                codelist = new Codelist(name, type, oid, string.Equals(extensible, "yes", StringComparison.OrdinalIgnoreCase));
                foreach (var codeDef in codeDefs)
                {
                    var code = new Codelist.Code(codeDef.GetAttribute("CodedValue"));
                    foreach (var decodeDef in Every(codeDef, "Decode"))
                    {
                        var translatedText = Only(decodeDef, "TranslatedText");
                        if (translatedText == null)
                        {
                            continue;
                        }

                        var lang = translatedText.GetAttribute("lang");
                        if (string.IsNullOrEmpty(lang))
                        {
                            lang = "en";
                        }

                        code.SetDecode(translatedText.InnerText, lang);
                    }

                    codelist.AddCode(code);
                }
            }
            else
            {
                var externalCodeList = Every(codelistDef, "ExternalCodeList");
                if (externalCodeList.Count == 1)
                {
                    var codeDef = externalCodeList[0];
                    codelist = new Codelist(name, type, oid, codeDef.GetAttribute("Dictionary"), codeDef.GetAttribute("Version"));
                }
                else
                {
                    errors.Record("There are multiple ExternalCodeList elements");
                }
            }

            if (codelist != null)
            {
                context.Add(codelist);
            }
            else
            {
                errors.Record("The codelist lacks valid children elements");
            }

            errors.EndContext();
        }
    }

    private void ParseValueListElements(ParseContext context, IReadOnlyCollection<XmlElement> valueListDefs, IReadOnlyCollection<XmlElement> whereClauseDefs, ErrorAccumulator errors)
    {
        var whereClauses = whereClauseDefs.ToDictionary(def => def.GetAttribute("OID"), def => def);
        foreach (var valueListDef in valueListDefs)
        {
            var valueList = new ValueList(valueListDef.GetAttribute("OID"));
            foreach (XmlElement itemRef in Every(valueListDef, "ItemRef"))
            {
                var clause = valueList.AddClause(itemRef.GetAttribute("ItemOID"), itemRef.GetAttribute("Mandatory"));
                var whereClauseRef = Only(itemRef, "WhereClauseRef", "http://www.cdisc.org/ns/def/v2.0");
                if (whereClauseRef == null)
                {
                    errors.Record($"ValueList {valueListDef.GetAttribute("OID")} ItemRef {itemRef.GetAttribute("ItemOID")} does not contain a WhereClauseRef");
                    continue;
                }

                var whereClauseDef = whereClauses.GetValueOrDefault(whereClauseRef.GetAttribute("WhereClauseOID"));
                if (whereClauseDef == null)
                {
                    errors.Record($"No WhereClauseDef with OID {whereClauseRef.GetAttribute("WhereClauseOID")} exists");
                    continue;
                }

                foreach (XmlElement rangeCheck in Every(whereClauseDef, "RangeCheck"))
                {
                    var values = new HashSet<string>();
                    foreach (XmlElement checkValue in Every(rangeCheck, "CheckValue"))
                    {
                        values.Add(checkValue.InnerText);
                    }

                    clause.AddCheck(rangeCheck.GetAttribute("ItemOID"), rangeCheck.GetAttribute("Comparator"), values);
                }
            }

            context.Add(valueList);
        }
    }

    private bool ParseConfigurationLevelContent(XmlDocument document, ConfigurationManager manager)
    {
        var metaDataVersionElement = Only(document, "MetaDataVersion");
        if (metaDataVersionElement == null)
        {
            return false;
        }

        var validationRuleElement = Only(metaDataVersionElement, "ValidationRules", ConfigNamespaceUri);
        if (validationRuleElement == null)
        {
            return true;
        }

        var rules = new KeyMap<RuleDefinition>();
        foreach (XmlElement ruleElement in Each(validationRuleElement.ChildNodes))
        {
            var id = ruleElement.GetAttribute("ID");
            if (rules.ContainsKey(id))
            {
                continue;
            }

            var type = ruleElement.LocalName;
            var severity = Diagnostic.FromString(ruleElement.GetAttribute("Type"));
            var rule = new RuleDefinition(id, type, severity);

            foreach (XmlElement conditionElement in Every(ruleElement, "Condition", ConfigNamespaceUri))
            {
                var domain = string.IsNullOrWhiteSpace(conditionElement.GetAttribute("ItemGroupDef")) ? null : conditionElement.GetAttribute("ItemGroupDef");
                var context = string.IsNullOrWhiteSpace(conditionElement.GetAttribute("Context")) ? null : conditionElement.GetAttribute("Context");
                severity = Diagnostic.FromString(conditionElement.GetAttribute("Type"));
                rule = rule.WithConditionalSeverity(new RuleDefinition.ConditionalSeverity(domain, context, severity));
            }

            foreach (XmlAttribute attribute in ruleElement.Attributes)
            {
                var name = attribute.LocalName;
                if (!name.Equals("ID", StringComparison.OrdinalIgnoreCase))
                {
                    rule = rule.WithProperty(name, ReplaceSystemProperties(attribute.Value));
                }
            }

            manager.Store(new ElementDefinition(Definition.Target.Rule, ruleElement));
            rules.Put(id, rule);
        }

        foreach (XmlElement itemGroupDefElement in Every(metaDataVersionElement, "ItemGroupDef"))
        {
            var name = itemGroupDefElement.GetAttribute("Name");
            Template? itemGroupDef;

            if (manager.Defines(name))
            {
                itemGroupDef = manager.GetConfiguration(name);
            }
            else if (manager.Prototypes(name))
            {
                itemGroupDef = manager.GetPrototype(name);
            }
            else
            {
                continue;
            }

            var validationRuleRefs = itemGroupDefElement.GetElementsByTagName("ValidationRuleRef",ConfigNamespaceUri);
            if (validationRuleRefs.Count == 0)
            {
                continue;
            }

            foreach (XmlElement validationRuleRef in Each(validationRuleRefs))
            {
                ParseConfigurationRule(rules, new ElementDefinition(Definition.Target.Rule, validationRuleRef, validationRuleRef.GetAttribute("Name")), itemGroupDef!);
            }

            itemGroupDef!.MarkConfigured();
        }

        return true;
    }

    private void ParseConfigurationRule(KeyMap<RuleDefinition> rules, Definition reference, Template template)
    {
        var id = reference.GetProperty("RuleID");
        var isActive = !reference.GetProperty("Active").Equals("No", StringComparison.OrdinalIgnoreCase);
        var isMetadata = reference.GetProperty("Target").Equals("Metadata", StringComparison.OrdinalIgnoreCase);

        var rule = rules.Get(id);
        if (rule == null)
        {
            return;
        }

        if (!isActive || rule.GetProperty("Active").Equals("No", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!isMetadata)
        {
            isMetadata = rule.GetProperty("Target").Equals("Metadata", StringComparison.OrdinalIgnoreCase);
        }

        var target = isMetadata ? SourceDetails.Reference.Metadata : SourceDetails.Reference.Data;
        template.DefineRule(target, rule);
    }

    private string ReplaceSystemProperties(string value)
    {
        value = value.Replace("%System.ConfigDirectory%", _cwd, StringComparison.OrdinalIgnoreCase);
        foreach (var property in _defaults)
        {
            value = Regex.Replace(value, "(?i)%" + Regex.Escape("System." + property.Key) + "%", property.Value);
        }

        return value;
    }

    private string ReadDescription(XmlElement element)
    {
        XmlElement? chosenDescription = null;
        foreach (XmlElement description in Every(element, "Description"))
        {
            if (description.ParentNode == element)
            {
                chosenDescription = description;
                break;
            }
        }

        if (chosenDescription == null)
        {
            return string.Empty;
        }

        var text = Only(chosenDescription, "TranslatedText");
        return text?.InnerText ?? string.Empty;
    }

    private DefineVersion? DetermineDefineVersion(XmlDocument document)
    {
        if (VerifyNamespace(document, "http://www.cdisc.org/ns/def/v1.0"))
        {
            return DefineVersion.V1;
        }

        if (VerifyNamespace(document, "http://www.cdisc.org/ns/def/v2.0"))
        {
            return DefineVersion.V2;
        }

        return null;
    }

    private bool VerifyNamespace(XmlDocument document, string namespaceUri)
    {
        var namespacePrefix = document.DocumentElement?.GetPrefixOfNamespace(namespaceUri);
        return !string.IsNullOrWhiteSpace(namespacePrefix);
    }

    private static string? CleanDomainKeys(string raw)
    {
        var pieces = Regex.Split(raw, "\\W+");
        if (pieces.Length == 1 || string.IsNullOrEmpty(pieces[0]))
        {
            return null;
        }

        return string.Join(",", pieces);
    }

    private static XmlDocument LoadDocument(string path)
    {
        var doc = new XmlDocument();
        doc.Load(path);
        return doc;
    }

    private static XmlElement? Only(XmlDocument document, string name)
    {
        var list = document.GetElementsByTagName(name);
        return list.Count == 1 ? (XmlElement)list[0] : null;
    }

    private static XmlElement? Only(XmlElement element, string name, string? ns = null)
    {
        var list = ns == null ? element.GetElementsByTagName(name) : element.GetElementsByTagName(name, ns);
        return list.Count == 1 ? (XmlElement)list[0] : null;
    }

    private static List<XmlElement> Every(XmlElement element, string name, string? ns = null)
    {
        var list = ns == null ? element.GetElementsByTagName(name) : element.GetElementsByTagName(name, ns);
        return list.Cast<XmlElement>().ToList();
    }

    private static List<XmlElement> Each(XmlNodeList list)
    {
        var results = new List<XmlElement>();
        foreach (XmlNode node in list)
        {
            if (node is XmlElement element)
            {
                results.Add(element);
            }
        }

        return results;
    }

    private sealed class ParseContext
    {
        private const string DefaultPrefix = "Config";
        private readonly Dictionary<string, Codelist> _codelists = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ValueList> _valueLists = new(StringComparer.OrdinalIgnoreCase);
        public readonly KeyMap<Definition> Variables = new();

        public ParseContext(DefineVersion version, bool isDefine, string prefix)
        {
            Version = version;
            Prefix = string.IsNullOrEmpty(prefix) ? (isDefine ? "Define" : "Config") : prefix;
            IsDefine = isDefine;
            IsDefaultConfig = Prefix == DefaultPrefix;
        }

        public DefineVersion Version { get; }
        public string Prefix { get; }
        public bool IsDefine { get; }
        public bool IsDefaultConfig { get; }

        public void Add(Codelist codelist) => _codelists[codelist.GetOid()] = codelist;

        public void Add(ValueList valueList) => _valueLists[valueList.GetOid()] = valueList;

        public void Add(string oid, Definition variable) => Variables.Put(oid, variable);

        public Codelist? GetCodeList(string oid) => _codelists.TryGetValue(oid, out var codelist) ? codelist : null;

        public ValueList? GetValueList(string oid) => _valueLists.TryGetValue(oid, out var list) ? list : null;

        public Definition? GetVariable(string oid) => Variables.Get(oid);
    }
}
