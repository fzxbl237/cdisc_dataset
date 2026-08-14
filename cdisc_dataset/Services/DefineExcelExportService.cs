using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services.Interface;
using ClosedXML.Excel;
using SqlSugar;

namespace cdisc_dataset.Services;

public class DefineExcelExportService(
    ISqlSugarClient sqlSugar,
    ICurrentProjectService currentProjectService) : IDefineExcelExportService
{
    public async Task ExportAsync(Stream outputStream)
    {
        var project = currentProjectService.CurrentProject
            ?? throw new InvalidOperationException("Please select a project before exporting.");
        var dataType = currentProjectService.CdiscDataType;
        var projectId = project.Id;

        var datasets = await sqlSugar.Queryable<Dataset>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType)
            .OrderBy(x => x.Name)
            .ToListAsync();
        var variables = await sqlSugar.Queryable<Variable>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType)
            .OrderBy(x => x.DatasetName)
            .OrderBy(x => x.Order)
            .ToListAsync();
        var valueLevels = await sqlSugar.Queryable<ValueLevel>()
            .Includes(x => x.WhereClauses)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType)
            .OrderBy(x => x.Dataset)
            .OrderBy(x => x.Variable)
            .OrderBy(x => x.Order)
            .ToListAsync();
        var codeLists = await sqlSugar.Queryable<CodeList>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType)
            .OrderBy(x => x.UniqueId)
            .ToListAsync();
        var terms = await sqlSugar.Queryable<Term>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType)
            .OrderBy(x => x.Order)
            .ToListAsync();
        var dictionaries = await sqlSugar.Queryable<Models.Dictionary>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType)
            .OrderBy(x => x.UniqueId)
            .ToListAsync();
        var methods = await sqlSugar.Queryable<Method>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType)
            .OrderBy(x => x.UniqueId)
            .ToListAsync();
        var comments = await sqlSugar.Queryable<Comment>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType)
            .OrderBy(x => x.UniqueId)
            .ToListAsync();
        var documents = await sqlSugar.Queryable<Document>()
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType)
            .OrderBy(x => x.UniqueId)
            .ToListAsync();

        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            CreateWorkbookLayout(workbook);
            WriteDefineSheet(workbook.Worksheet("Define"), project, dataType);
            WriteDatasetsSheet(workbook.Worksheet("Datasets"), datasets);
            WriteVariablesSheet(workbook.Worksheet("Variables"), variables);
            WriteValueLevelsSheet(workbook.Worksheet("ValueLevel"), valueLevels);
            WriteCodeListsSheet(workbook.Worksheet("Codelists"), codeLists, terms);
            WriteDictionariesSheet(workbook.Worksheet("Dictionaries"), dictionaries);
            WriteMethodsSheet(workbook.Worksheet("Methods"), methods);
            WriteCommentsSheet(workbook.Worksheet("Comments"), comments);
            WriteDocumentsSheet(workbook.Worksheet("Documents"), documents);
            workbook.SaveAs(outputStream);
        });
    }

    private static void CreateWorkbookLayout(XLWorkbook workbook)
    {
        CreateDefineSheet(workbook);
        CreateTableSheet(workbook, "Datasets", ["Dataset", "Label", "Class", "SubClass", "Structure", "Key Variables", "Standard", "Has No Data", "Repeating", "Reference Data", "Comment", "Developer Notes"], [12, 24, 16, 16, 51, 59, 16, 13, 13, 16, 20, 24]);
        CreateTableSheet(workbook, "Variables", ["Order", "Dataset", "Variable", "Label", "Data Type", "Length", "Significant Digits", "Format", "Mandatory", "Assigned Value", "Codelist", "Common", "Origin", "Source", "Pages", "Method", "Predecessor", "Role", "Has No Data", "Comment", "Developer Notes"], [12, 19, 19, 24, 17, 10, 16, 11, 12, 16, 13, 13, 13, 18, 12, 14, 15, 16, 18, 16, 24]);
        CreateTableSheet(workbook, "ValueLevel", ["Order", "Dataset", "Variable", "Where Clause", "Label", "Data Type", "Length", "Significant Digits", "Format", "Mandatory", "Assigned Value", "Codelist", "Origin", "Source", "Pages", "Method", "Predecessor", "Comment", "Developer Notes"], [12, 19, 19, 46, 29, 12, 10, 16, 11, 12, 16, 13, 13, 18, 12, 14, 15, 16, 24]);
        CreateTableSheet(workbook, "Codelists", ["ID", "Name", "NCI Codelist Code", "Data Type", "Terminology", "Comment", "Order", "Term", "NCI Term Code", "Decoded Value", "Developer Notes"], [30, 37, 18, 12, 18, 16, 10, 26, 18, 26, 24]);
        CreateTableSheet(workbook, "Dictionaries", ["ID", "Name", "Data Type", "Dictionary", "Version"], [16, 25, 16, 25, 16]);
        CreateTableSheet(workbook, "Methods", ["ID", "Name", "Type", "Description", "Expression Context", "Expression Code", "Document", "Pages"], [16, 25, 12, 47, 19, 25, 20, 12]);
        CreateTableSheet(workbook, "Comments", ["ID", "Description", "Document", "Pages"], [20, 63, 20, 12]);
        CreateTableSheet(workbook, "Documents", ["ID", "Title", "Href"], [20, 63, 20]);
    }

    private static void CreateDefineSheet(XLWorkbook workbook)
    {
        var worksheet = workbook.AddWorksheet("Define");
        var attributes = new[] { "StudyName", "StudyDescription", "ProtocolName", "StandardName", "StandardVersion", "Language", string.Empty, "Legend", string.Empty };
        worksheet.Cell(1, 1).Value = "Attribute";
        worksheet.Cell(1, 2).Value = "Value";
        for (var row = 0; row < attributes.Length; row++)
            worksheet.Cell(row + 2, 1).Value = attributes[row];
        worksheet.Column(1).Width = 20;
        worksheet.Column(2).Width = 74;
        StyleHeader(worksheet.Range(1, 1, 1, 2));
        worksheet.SheetView.FreezeRows(1);
        worksheet.Range(1, 1, 1, 2).SetAutoFilter();
    }

    private static void CreateTableSheet(XLWorkbook workbook, string name, IReadOnlyList<string> headers, IReadOnlyList<double> widths)
    {
        var worksheet = workbook.AddWorksheet(name);
        for (var column = 0; column < headers.Count; column++)
        {
            worksheet.Cell(1, column + 1).Value = headers[column];
            worksheet.Column(column + 1).Width = widths[column];
        }

        StyleHeader(worksheet.Range(1, 1, 1, headers.Count));
        worksheet.SheetView.FreezeRows(1);
        worksheet.Range(1, 1, 1, headers.Count).SetAutoFilter();
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("FF9900");
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.Black;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void WriteDefineSheet(IXLWorksheet worksheet, Project project, CdiscDataType dataType)
    {
        worksheet.Cell("B2").Value = project.ProjectCode ?? string.Empty;
        worksheet.Cell("B3").Value = project.ProtocolDescription ?? string.Empty;
        worksheet.Cell("B4").Value = project.ProtocolCode ?? string.Empty;
        worksheet.Cell("B5").Value = dataType == CdiscDataType.Sdtm ? "SDTM-IG" : "ADaM-IG";
        worksheet.Cell("B6").Value = dataType == CdiscDataType.Sdtm
            ? GetDescription(project.SdtmIgVersion)
            : GetDescription(project.AdamIgVersion);
        worksheet.Cell("B7").Value = project.Language == Language.Zh ? "zh" : "en";
        worksheet.Cell("B9").Value = "Highlighted cells are required for Define-XML 2.1 and can be ignored for prior versions.";
        worksheet.Cell("B10").Value = "Highlighted cells are used by ADaM only and can be left empty otherwise.";

        var defineXmlLegend = worksheet.Cell("B9");
        defineXmlLegend.Style.Fill.BackgroundColor = XLColor.FromHtml("FCE4D6");
        defineXmlLegend.Style.Alignment.WrapText = true;

        var adamLegend = worksheet.Cell("B10");
        adamLegend.Style.Fill.BackgroundColor = XLColor.FromHtml("BDD7EE");
        adamLegend.Style.Alignment.WrapText = true;
    }

    private static void WriteDatasetsSheet(IXLWorksheet worksheet, IEnumerable<Dataset> datasets)
    {
        WriteRows(worksheet, 12, datasets.Select(dataset => new object?[]
        {
            dataset.Name, dataset.Label, dataset.Class, dataset.SubClass, dataset.Structure,
            dataset.KeyVariables, dataset.Standard, dataset.HasNoData, dataset.Repeating,
            dataset.ReferenceData, dataset.CommentUniqueId, dataset.DeveloperNotes
        }));
    }

    private static void WriteVariablesSheet(IXLWorksheet worksheet, IEnumerable<Variable> variables)
    {
        WriteRows(worksheet, 21, variables.Select(variable => new object?[]
        {
            variable.Order, variable.DatasetName, variable.VariableName, variable.Label,
            variable.DataType, variable.Length, variable.SignificantDigits, variable.Format,
            variable.Mandatory, variable.AssignedValue, variable.CodeListUniqueId,
            variable.Common, variable.Origin, variable.Source, variable.Pages,
            variable.MethodUniqueId, variable.Predecessor, variable.Role, variable.HasNoData,
            variable.CommentUniqueId, variable.DeveloperNotes
        }));

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow < 2)
            return;

        worksheet.Range(2, 12, lastRow, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("BDD7EE");
        worksheet.Range(2, 14, lastRow, 14).Style.Fill.BackgroundColor = XLColor.FromHtml("FCE4D6");
        worksheet.Range(2, 19, lastRow, 19).Style.Fill.BackgroundColor = XLColor.FromHtml("FCE4D6");
    }

    private static void WriteValueLevelsSheet(IXLWorksheet worksheet, IEnumerable<ValueLevel> valueLevels)
    {
        WriteRows(worksheet, 19, valueLevels.Select(valueLevel => new object?[]
        {
            valueLevel.Order, valueLevel.Dataset, valueLevel.Variable, BuildWhereClause(valueLevel),
            valueLevel.Label, valueLevel.Type, valueLevel.Length, valueLevel.Digits,
            valueLevel.Format, valueLevel.Mandatory, null, valueLevel.CodeListUniqueId,
            valueLevel.Origin, valueLevel.Source, valueLevel.Pages, valueLevel.MethodUniqueId,
            valueLevel.Predecessor, valueLevel.CommentUniqueId, valueLevel.DeveloperNotes
        }));
    }

    private static void WriteCodeListsSheet(
        IXLWorksheet worksheet,
        IReadOnlyList<CodeList> codeLists,
        IReadOnlyList<Term> terms)
    {
        var termsByCodeListId = terms.GroupBy(term => term.CodeListId)
            .ToDictionary(group => group.Key, group => group.OrderBy(term => term.Order).ToList());
        var rows = new List<object?[]>();
        foreach (var codeList in codeLists)
        {
            if (!termsByCodeListId.TryGetValue(codeList.Id, out var codeListTerms) || codeListTerms.Count == 0)
            {
                rows.Add([codeList.UniqueId, codeList.Name, codeList.Code, codeList.Type,
                    codeList.Terminology, codeList.CommentUniqueId, null, null, null,
                    null, codeList.DeveloperNotes]);
                continue;
            }

            foreach (var term in codeListTerms)
            {
                rows.Add([codeList.UniqueId, codeList.Name, codeList.Code, codeList.Type,
                    codeList.Terminology, codeList.CommentUniqueId, term.Order, term.Name,
                    term.Code, term.DecodedValue, codeList.DeveloperNotes]);
            }
        }

        WriteRows(worksheet, 11, rows);
    }

    private static void WriteDictionariesSheet(IXLWorksheet worksheet, IEnumerable<Models.Dictionary> dictionaries)
    {
        WriteRows(worksheet, 5, dictionaries.Select(dictionary => new object?[]
        {
            dictionary.UniqueId, dictionary.Name, dictionary.DataType,
            dictionary.DictionaryName, dictionary.Version
        }));
    }

    private static void WriteMethodsSheet(IXLWorksheet worksheet, IEnumerable<Method> methods)
    {
        WriteRows(worksheet, 8, methods.Select(method => new object?[]
        {
            method.UniqueId, method.Name, method.Type, method.Description,
            method.ExpressionContext, method.ExpressionCode, method.DocumentUniqueId, method.Pages
        }));
    }

    private static void WriteCommentsSheet(IXLWorksheet worksheet, IEnumerable<Comment> comments)
    {
        WriteRows(worksheet, 4, comments.Select(comment => new object?[]
        {
            comment.UniqueId, comment.Description, comment.DocumentUniqueId, comment.Pages
        }));
    }

    private static void WriteDocumentsSheet(IXLWorksheet worksheet, IEnumerable<Document> documents)
    {
        WriteRows(worksheet, 3, documents.Select(document => new object?[]
        {
            document.UniqueId, document.Title, document.Href
        }));
    }

    private static void WriteRows(IXLWorksheet worksheet, int columnCount, IEnumerable<object?[]> rows)
    {
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow >= 2)
            worksheet.Range(2, 1, lastRow, columnCount).Clear(XLClearOptions.Contents);

        var rowNumber = 2;
        foreach (var values in rows)
        {
            for (var columnNumber = 1; columnNumber <= columnCount; columnNumber++)
                SetValue(worksheet.Cell(rowNumber, columnNumber), values[columnNumber - 1]);

            rowNumber++;
        }

        if (rowNumber > 2)
        {
            var dataRange = worksheet.Range(2, 1, rowNumber - 1, columnCount);
            dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            dataRange.Style.Alignment.WrapText = true;
        }

        worksheet.AutoFilter.Clear();
        worksheet.Range(1, 1, Math.Max(rowNumber - 1, 1), columnCount).SetAutoFilter();
    }

    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Value = string.Empty;
                break;
            case string text:
                cell.Value = text;
                break;
            case int number:
                cell.Value = number;
                break;
            case float number:
                cell.Value = number;
                break;
            case double number:
                cell.Value = number;
                break;
            case bool flag:
                cell.Value = flag;
                break;
            default:
                cell.Value = value.ToString() ?? string.Empty;
                break;
        }
    }

    private static string BuildWhereClause(ValueLevel valueLevel)
    {
        if (!string.IsNullOrWhiteSpace(valueLevel.WhereClause))
            return valueLevel.WhereClause;

        return valueLevel.WhereClauses?
            .Where(whereClause => !string.IsNullOrWhiteSpace(whereClause.Variable))
            .Select(whereClause => $"{whereClause.Variable} {whereClause.Comparator} {whereClause.Values}".Trim())
            .Aggregate((left, right) => $"{left} AND {right}") ?? string.Empty;
    }

    private static string GetDescription<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        return typeof(TEnum).GetField(value.ToString())?
            .GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString();
    }
}
