using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using PatChes.Models;
using PatChes.Services.Interface;
using SqlSugar;
using Validator.Define;

namespace PatChes.Services;

public sealed class DefineXmlValidationService(
    IDefineXmlExportService defineXmlExportService,
    ICurrentProjectService currentProjectService,
    ISqlSugarClient sqlSugar,
    IDefineValidator defineValidator) : IDefineXmlValidationService
{
    private const string EntityType = "Define";

    public async Task<DefineXmlValidationResult> ValidateAsync()
    {
        var project = currentProjectService.CurrentProject
            ?? throw new InvalidOperationException("Please select a project before validating Define XML.");
        var xml = await defineXmlExportService.GenerateXmlAsync();
        var validationResult = await Task.Run(() => defineValidator.Validate(xml, new DefineValidationOptions(
            GetOdmSchemaPath(),
            GetDefineSchemaPath())));
        var now = DateTime.UtcNow;
        var contextResolver = DefineIssueContextResolver.Create(xml);
        var issues = validationResult.Diagnostics.Select(diagnostic =>
        {
            var context = contextResolver.Resolve(diagnostic.Location);
            return new DefineIssue
            {
                ProjectId = project.Id,
                CdiscDataType = currentProjectService.CdiscDataType,
                Domain = EntityType.ToUpperInvariant(),
                Variables = context.Variables,
                Values = context.Values,
                Pinnacle21Id = diagnostic.RuleId,
                Message = diagnostic.Message,
                Severity = diagnostic.Severity == DefineDiagnosticSeverity.Error ? "Error" : "Warning",
                CreatedAt = now,
                UpdatedAt = now
            };
        }).ToList();

        await sqlSugar.Ado.UseTranAsync(async () =>
        {
            await sqlSugar.Deleteable<DefineIssue>()
                .Where(x => x.ProjectId == project.Id &&
                            x.CdiscDataType == currentProjectService.CdiscDataType)
                .ExecuteCommandAsync();

            if (issues.Count > 0)
                await sqlSugar.Insertable(issues).ExecuteCommandAsync();
        });

        return new DefineXmlValidationResult(
            validationResult.ErrorCount,
            validationResult.WarningCount,
            validationResult.Diagnostics.Select(diagnostic => new DefineXmlValidationIssue(
                diagnostic.Location,
                diagnostic.Message,
                diagnostic.Severity == DefineDiagnosticSeverity.Error ? "Error" : "Warning",
                diagnostic.RuleId)).ToList());
    }

    private static string GetOdmSchemaPath() => GetSchemaPath(
        "cdisc-odm-1.3.2",
        "ODM1-3-2.xsd");

    private static string GetDefineSchemaPath() => GetSchemaPath(
        "cdisc-define-2.1",
        "define2-1-0.xsd");

    private static string GetSchemaPath(string directory, string fileName)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "DefineXmlSchema",
            directory,
            fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException("Define-XML 2.1 schema files are unavailable.", path);

        return path;
    }

    private sealed class DefineIssueContextResolver
    {
        private static readonly XNamespace Odm = "http://www.cdisc.org/ns/odm/v1.3";
        private static readonly XNamespace Def = "http://www.cdisc.org/ns/def/v2.1";
        private static readonly Regex LocationPattern = new(
            @"^Line (?<line>\d+), Position \d+$",
            RegexOptions.CultureInvariant);

        private readonly IReadOnlyList<XElement> _elements;
        private readonly IReadOnlyDictionary<string, XElement> _itemsByOid;
        private readonly IReadOnlyDictionary<string, XElement> _groupsByItemOid;
        private readonly IReadOnlyDictionary<string, XElement> _codeListsByOid;
        private readonly IReadOnlyDictionary<string, XElement> _valueListOwnersByValueItemOid;

        private DefineIssueContextResolver(XDocument document)
        {
            _elements = document.Descendants().ToList();
            _itemsByOid = ByOid(Odm + "ItemDef");
            _codeListsByOid = ByOid(Odm + "CodeList");
            _groupsByItemOid = document.Descendants(Odm + "ItemGroupDef")
                .SelectMany(group => group.Elements(Odm + "ItemRef")
                    .Select(itemRef => new { ItemOid = itemRef.Attribute("ItemOID")?.Value, Group = group }))
                .Where(x => !string.IsNullOrWhiteSpace(x.ItemOid))
                .GroupBy(x => x.ItemOid!, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First().Group, StringComparer.Ordinal);
            _valueListOwnersByValueItemOid = document.Descendants(Odm + "ItemDef")
                .Select(item => new { Item = item, ValueListOid = item.Element(Def + "ValueListRef")?.Attribute("ValueListOID")?.Value })
                .Where(x => !string.IsNullOrWhiteSpace(x.ValueListOid))
                .SelectMany(x => document.Descendants(Def + "ValueListDef")
                    .Where(valueList => string.Equals(valueList.Attribute("OID")?.Value, x.ValueListOid, StringComparison.Ordinal))
                    .SelectMany(valueList => valueList.Elements(Odm + "ItemRef")
                        .Select(itemRef => new { ItemOid = itemRef.Attribute("ItemOID")?.Value, x.Item })))
                .Where(x => !string.IsNullOrWhiteSpace(x.ItemOid))
                .GroupBy(x => x.ItemOid!, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First().Item, StringComparer.Ordinal);

            IReadOnlyDictionary<string, XElement> ByOid(XName name) => document.Descendants(name)
                .Select(element => new { Oid = element.Attribute("OID")?.Value, Element = element })
                .Where(x => !string.IsNullOrWhiteSpace(x.Oid))
                .GroupBy(x => x.Oid!, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First().Element, StringComparer.Ordinal);
        }

        public static DefineIssueContextResolver Create(string xml) =>
            new(XDocument.Parse(xml, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace));

        public DefineIssueContext Resolve(string location)
        {
            var match = LocationPattern.Match(location);
            if (!match.Success || !int.TryParse(match.Groups["line"].Value, out var line))
                return default;

            var element = _elements
                .Where(x => LineNumber(x) <= line)
                .OrderByDescending(LineNumber)
                .FirstOrDefault();
            if (element == null)
                return default;

            var item = ResolveItem(element);
            var group = ResolveGroup(element, item);
            var codeList = ResolveCodeList(element, item);
            var names = new List<string>();
            var values = new List<string>();

            Add("Dataset Name", group?.Attribute("Name")?.Value);
            Add("Variable", item?.Attribute("Name")?.Value);
            if (item == null && codeList != null)
                Add("CodeList OID", CodeListOidValue(codeList));
            else
                Add("CodeList", codeList?.Attribute("Name")?.Value);

            return names.Count == 0 ? default : new DefineIssueContext(
                string.Join(", ", names),
                string.Join(", ", values));

            void Add(string name, string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;

                names.Add(name);
                values.Add(value);
            }
        }

        private XElement? ResolveItem(XElement element)
        {
            var item = element.AncestorsAndSelf(Odm + "ItemDef").FirstOrDefault();
            if (item != null)
                return item;

            var itemOid = element.AncestorsAndSelf()
                .Select(x => x.Attribute("ItemOID")?.Value ?? x.Attribute(Def + "ItemOID")?.Value)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            return itemOid != null && _itemsByOid.TryGetValue(itemOid, out item) ? item : null;
        }

        private XElement? ResolveGroup(XElement element, XElement? item)
        {
            var group = element.AncestorsAndSelf(Odm + "ItemGroupDef").FirstOrDefault();
            if (group != null || item == null)
                return group;

            var itemOid = item.Attribute("OID")?.Value;
            if (itemOid != null && _groupsByItemOid.TryGetValue(itemOid, out group))
                return group;

            if (itemOid != null && _valueListOwnersByValueItemOid.TryGetValue(itemOid, out var owner))
            {
                var ownerOid = owner.Attribute("OID")?.Value;
                if (ownerOid != null && _groupsByItemOid.TryGetValue(ownerOid, out group))
                    return group;
            }

            return null;
        }

        private XElement? ResolveCodeList(XElement element, XElement? item)
        {
            var codeList = element.AncestorsAndSelf(Odm + "CodeList").FirstOrDefault();
            if (codeList != null)
                return codeList;

            var codeListOid = element.AncestorsAndSelf(Odm + "CodeListRef")
                .Select(x => x.Attribute("CodeListOID")?.Value)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?? item?.Element(Odm + "CodeListRef")?.Attribute("CodeListOID")?.Value;
            return codeListOid != null && _codeListsByOid.TryGetValue(codeListOid, out codeList)
                ? codeList
                : null;
        }

        private static string? CodeListOidValue(XElement codeList) =>
            codeList.Attribute("OID")?.Value;

        private static int LineNumber(XElement element) => element is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
            ? lineInfo.LineNumber
            : 0;
    }

    private readonly record struct DefineIssueContext(string? Variables, string? Values);
}
