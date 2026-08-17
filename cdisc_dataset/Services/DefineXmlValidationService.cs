using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Services.Interface;
using SqlSugar;
using Validator.Define;

namespace cdisc_dataset.Services;

public sealed class DefineXmlValidationService(
    IDefineXmlExportService defineXmlExportService,
    ICurrentProjectService currentProjectService,
    ISqlSugarClient sqlSugar,
    IDefineValidator defineValidator) : IDefineXmlValidationService
{
    private const string EntityType = "Define";
    private const int EntityId = 0;

    public async Task<DefineXmlValidationResult> ValidateAsync()
    {
        var project = currentProjectService.CurrentProject
            ?? throw new InvalidOperationException("Please select a project before validating Define XML.");
        var xml = await defineXmlExportService.GenerateXmlAsync();
        var validationResult = await Task.Run(() => defineValidator.Validate(xml, new DefineValidationOptions(
            GetOdmSchemaPath(),
            GetDefineSchemaPath())));
        var now = DateTime.UtcNow;
        var issues = validationResult.Diagnostics.Select(diagnostic => new Issue
        {
            ProjectId = project.Id,
            CdiscDataType = currentProjectService.CdiscDataType,
            EntityType = EntityType,
            EntityId = EntityId,
            PropertyName = diagnostic.Location,
            ErrorMessage = diagnostic.Message,
            Severity = diagnostic.Severity == DefineDiagnosticSeverity.Error ? "Error" : "Warning",
            IssueCode = diagnostic.RuleId,
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        await sqlSugar.Ado.UseTranAsync(async () =>
        {
            await sqlSugar.Deleteable<Issue>()
                .Where(x => x.ProjectId == project.Id &&
                            x.CdiscDataType == currentProjectService.CdiscDataType &&
                            x.EntityType == EntityType &&
                            x.EntityId == EntityId)
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
}
