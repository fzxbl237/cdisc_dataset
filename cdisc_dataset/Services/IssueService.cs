using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services.Interface;
using FluentValidation;
using FluentValidation.Results;
using MapsterMapper;
using SqlSugar;

namespace cdisc_dataset.Services;

public class IssueService(ISqlSugarClient sqlSugar, IMapper mapper) : IIssueService
{
    public async Task SyncIssuesAsync<TDto>(TDto dto, string entityType, int entityId,int projectId,CdiscDataType cdiscDataType, IEnumerable<ValidationFailure> failures)
        where TDto : BaseDto
    {
        var failureList = failures.ToList();
        var now = DateTime.UtcNow;

        var errorDictionary = failureList
            .GroupBy(x => x.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new DataGridValidationResult(
                    x.ErrorMessage,
                    x.Severity == Severity.Error
                        ? DataGridValidationSeverity.Error
                        : DataGridValidationSeverity.Warning)).ToList());
        
        dto.SetErrorDictionary(errorDictionary);
        
        await SyncErrorDictionaryAsync(entityType, entityId,projectId,cdiscDataType, errorDictionary);
    }

    public async Task SyncErrorDictionaryAsync(string entityType, int entityId,int projectId,CdiscDataType cdiscDataType, Dictionary<string, List<DataGridValidationResult>> errorDictionary)
    {
        var now = DateTime.UtcNow;

        var issues = errorDictionary
            .SelectMany(pair => pair.Value.Select(result => new Issue
            {
                EntityType = entityType,
                EntityId = entityId,
                ProjectId = projectId,
                CdiscDataType = cdiscDataType,
                PropertyName = pair.Key,
                ErrorMessage = result.Message,
                Severity = result.Severity==DataGridValidationSeverity.Error?"Error":"Warning",
                CreatedAt = now,
                UpdatedAt = now
            }))
            .ToList();

        await sqlSugar.Deleteable<Issue>()
            .Where(x => x.EntityType == entityType && x.EntityId == entityId && x.ProjectId == projectId && x.CdiscDataType == cdiscDataType)
            .ExecuteCommandAsync();

        if (issues.Count > 0)
        {
            await sqlSugar.Insertable(issues).ExecuteCommandAsync();
        }
    }

    public async Task RestoreErrorsAsync<TDto>(TDto dto, string entityType, int entityId, int projectId, CdiscDataType cdiscDataType)
        where TDto : BaseDto
    {
        var issues = await sqlSugar.Queryable<Issue>()
            .Where(x => x.EntityType == entityType && x.EntityId == entityId && x.ProjectId == projectId && x.CdiscDataType == cdiscDataType)
            .ToListAsync();

        dto.SetErrorDictionary(issues
            .GroupBy(x => x.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new DataGridValidationResult(
                    x.ErrorMessage,
                    x.Severity == "Error"
                        ? DataGridValidationSeverity.Error
                        : DataGridValidationSeverity.Warning)).ToList()));
    }

    public async Task<List<IssueDto>> GetIssuesAsync(string entityType, int entityId, int projectId, CdiscDataType cdiscDataType)
    {
        return await sqlSugar.Queryable<Issue>()
            .Where(x => x.EntityType == entityType && x.EntityId == entityId && x.ProjectId == projectId && x.CdiscDataType == cdiscDataType)
            .Select<IssueDto>()
            .ToListAsync();
    }
}
