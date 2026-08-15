using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Models.Settings;
using cdisc_dataset.Services.Interface;
using MapsterMapper;
using SqlSugar;

namespace cdisc_dataset.Services;

public class VariableService(ISqlSugarClient sqlSugar, IMapper mapper, ICurrentProjectService currentProjectService, ILookupStore lookupStore) : IVariableService
{

    private (int ProjectId, CdiscDataType DataType) GetCurrentProjectContext()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;
        return (projectId, dataType);
    }

    public async Task<List<VariableDto>> GetAllVariableDtosAsync()
    {
        var (currentProjectId, currentDataType) = GetCurrentProjectContext();
        var list = await sqlSugar.Queryable<Variable>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Method)
            .Includes(o=>o.CodeList)
            .Includes(o=>o.Dictionary)
            .Where(x => x.ProjectId == currentProjectId && x.CdiscDataType == currentDataType)
            .Select(o=> new VariableDto(){
                CodeList = o.CodeList,
                Method = o.Method,
                Dictionary = o.Dictionary,
                Comment = o.Comment
            },true).ToListAsync();
        return list;
    }

    public async Task<List<VariableDto>> GetAllVariableDtosWithoutErorrAsync()
    {
        var (currentProjectId, currentDataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Variable>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Method)
            .Includes(o=>o.CodeList)
            .Includes(o=>o.Dictionary)
            .Where(x => x.ProjectId == currentProjectId && x.CdiscDataType == currentDataType && !x.HasErrors)
            .Select(o => new VariableDto
            {
                CodeList = o.CodeList,
                Method = o.Method,
                Dictionary = o.Dictionary,
                Comment = o.Comment
            }, true)
            .ToListAsync();
    }

    public async Task<List<Variable>> GetAllVariablesWithoutErorrAsync()
    {
        var (currentProjectId, currentDataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Variable>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Method)
            .Includes(o=>o.CodeList)
            .Includes(o=>o.Dictionary)
            .Where(x => x.ProjectId == currentProjectId && x.CdiscDataType == currentDataType && !x.HasErrors).ToListAsync();
    }

    public async Task<List<Variable>> GetAllVariablesByDatasetIdAsync(int datasetId)
    {
        var (currentProjectId, currentDataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Variable>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Method)
            .Includes(o=>o.CodeList)
            .Includes(o=>o.Dictionary)
            .Where(x => x.ProjectId == currentProjectId && x.CdiscDataType == currentDataType && x.DatasetId == datasetId)
            .ToListAsync();
    }

    public async Task<List<Variable>> GetAllVariablesByDatasetIdWithoutErorrAsync(int datasetId)
    {
        var (currentProjectId, currentDataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Variable>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Method)
            .Includes(o=>o.CodeList)
            .Includes(o=>o.Dictionary)
            .Where(x => x.ProjectId == currentProjectId && x.CdiscDataType == currentDataType && x.DatasetId == datasetId && !x.HasErrors).ToListAsync();
    }
    
    public async Task<Variable?> GetVariableByDatasetIdAndVariableNameWithoutError(int datasetId, string? variableName)
    {
        var (currentProjectId, currentDataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Variable>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Method)
            .Includes(o=>o.CodeList,cl=>cl.Terms)
            .Includes(o=>o.Dictionary)
            .Where(x => x.ProjectId == currentProjectId && x.CdiscDataType == currentDataType && x.DatasetId == datasetId && x.VariableName == variableName && !x.HasErrors)
            .FirstAsync();
    }

    public async Task<Variable?> GetStandardVariableByDatasetAndVariableNameAsync(string? datasetName, string? variableName, CdiscDataType cdiscDataType)
    {
        if (string.IsNullOrWhiteSpace(datasetName) || string.IsNullOrWhiteSpace(variableName))
            return null;

        return await sqlSugar.AsTenant().QueryableWithAttr<VariableTemplate>()
            .Where(x =>
                        x.CdiscDataType == cdiscDataType &&
                        x.DatasetName == datasetName &&
                        x.VariableName == variableName)
            .Select<Variable>()
            .FirstAsync();
    }

    public async Task<List<VariableDto>> GetAvailableVariablesAsync(string? datasetName)
    {
        if (string.IsNullOrWhiteSpace(datasetName)) return [];

        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;

        var existingVariableNames = await sqlSugar.Queryable<Variable>()
            .Where(o => o.ProjectId == projectId &&
                        o.CdiscDataType == dataType &&
                        o.DatasetName == datasetName &&
                        !string.IsNullOrWhiteSpace(o.VariableName))
            .Select(o => o.VariableName)
            .ToListAsync();

        var list = await sqlSugar.Queryable<Variable>()
            .Where(o => o.ProjectId == 0 &&
                        o.CdiscDataType == dataType &&
                        o.DatasetName == datasetName &&
                        !string.IsNullOrWhiteSpace(o.VariableName) &&
                        !existingVariableNames.Contains(o.VariableName))
            .Select<VariableDto>()
            .ToListAsync();

        return list;
    }

    public async Task<List<VariableTemplate>> GetAvailableSettingVariableTemplatesAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        if (projectId == 0)
            return [];

        var datasets = await sqlSugar.Queryable<Dataset>()
            .Where(dataset => dataset.ProjectId == projectId && dataset.CdiscDataType == dataType)
            .Select(dataset => new { dataset.Id, dataset.Name })
            .ToListAsync();
        var datasetIdsByName = datasets
            .Where(dataset => !string.IsNullOrWhiteSpace(dataset.Name))
            .ToDictionary(dataset => dataset.Name!, dataset => dataset.Id);
        if (datasetIdsByName.Count == 0)
            return [];

        var existingKeys = (await sqlSugar.Queryable<Variable>()
                .Where(variable => variable.ProjectId == projectId && variable.CdiscDataType == dataType)
                .Select(variable => new { variable.DatasetName, variable.VariableName })
                .ToListAsync())
            .Where(variable => !string.IsNullOrWhiteSpace(variable.DatasetName) && !string.IsNullOrWhiteSpace(variable.VariableName))
            .Select(variable => $"{variable.DatasetName}\u001f{variable.VariableName}")
            .ToHashSet();

        var templates = await sqlSugar.AsTenant().QueryableWithAttr<VariableTemplate>()
            .Where(template => template.CdiscDataType == dataType &&
                               template.DatasetName != null &&
                               datasetIdsByName.Keys.Contains(template.DatasetName) &&
                               template.VariableName != null)
            .ToListAsync();

        return templates
            .Where(template => !existingKeys.Contains($"{template.DatasetName}\u001f{template.VariableName}"))
            .OrderBy(template => template.DatasetName)
            .ThenBy(template => template.Order)
            .ThenBy(template => template.VariableName)
            .ToList();
    }

    public async Task<int> ImportSettingVariablesAsync(IReadOnlyList<int> templateVariableIds)
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        var templateIds = templateVariableIds.Distinct().Where(id => id > 0).ToList();
        if (projectId == 0 || templateIds.Count == 0)
            return 0;

        var datasets = await sqlSugar.Queryable<Dataset>()
            .Where(dataset => dataset.ProjectId == projectId && dataset.CdiscDataType == dataType)
            .Select(dataset => new { dataset.Id, dataset.Name })
            .ToListAsync();
        var datasetIdsByName = datasets
            .Where(dataset => !string.IsNullOrWhiteSpace(dataset.Name))
            .ToDictionary(dataset => dataset.Name!, dataset => dataset.Id);

        var existingKeys = (await sqlSugar.Queryable<Variable>()
                .Where(variable => variable.ProjectId == projectId && variable.CdiscDataType == dataType)
                .Select(variable => new { variable.DatasetName, variable.VariableName })
                .ToListAsync())
            .Where(variable => !string.IsNullOrWhiteSpace(variable.DatasetName) && !string.IsNullOrWhiteSpace(variable.VariableName))
            .Select(variable => $"{variable.DatasetName}\u001f{variable.VariableName}")
            .ToHashSet();

        var templates = await sqlSugar.AsTenant().QueryableWithAttr<VariableTemplate>()
            .Where(template => templateIds.Contains(template.Id) && template.CdiscDataType == dataType)
            .ToListAsync();
        var variables = templates
            .Where(template => !string.IsNullOrWhiteSpace(template.DatasetName) &&
                               !string.IsNullOrWhiteSpace(template.VariableName) &&
                               datasetIdsByName.ContainsKey(template.DatasetName) &&
                               !existingKeys.Contains($"{template.DatasetName}\u001f{template.VariableName}"))
            .Select(template => new Variable
            {
                Order = template.Order,
                DatasetName = template.DatasetName,
                DatasetId = datasetIdsByName[template.DatasetName!],
                VariableName = template.VariableName,
                Label = template.Label,
                DataType = template.DataType,
                Mandatory = template.Mandatory,
                Role = template.Role,
                Core = template.Core,
                ProjectId = projectId,
                CdiscDataType = dataType
            })
            .ToList();
        if (variables.Count == 0)
            return 0;

        var result = await sqlSugar.Insertable(variables).ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.Variable);
        return result;
    }

    public async Task<VariableDto> InsertVariableAsync(VariableDto variableDto)
    {
        var variable = mapper.Map<Variable>(variableDto);
        var entity = await sqlSugar.Insertable(variable).ExecuteReturnEntityAsync();
        await lookupStore.RefreshAsync(LookupKind.Variable);
        return mapper.Map<VariableDto>(entity);
    }

    public async Task<int> UpdateVariableAsync(VariableDto variableDto)
    {
        var result = await sqlSugar.Updateable(mapper.Map<Variable>(variableDto)).ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.Variable);
        return result;
    }

    public async Task<int> SaveVariablesAsync(IReadOnlyList<VariableDto> variableDtos)
    {
        var list = await Task.Run(() => mapper.Map<List<Variable>>(variableDtos));
        await sqlSugar.Utilities.PageEachAsync(list, 200, async pageList =>
        {
            var storage = await sqlSugar.Storageable(pageList).ToStorageAsync();
            var inserted = await storage.AsInsertable.ExecuteCommandAsync();
            var updated = await storage.AsUpdateable.ExecuteCommandAsync();
        });
        await lookupStore.RefreshAsync(LookupKind.Variable);
        return 1;
    }

    public async Task<int> DeleteVariableAsync(VariableDto variable)
    {
        var result = await sqlSugar.Deleteable<Variable>(mapper.Map<Variable>(variable))
            .ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.Variable);
        return result;
    }
}