using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services.Interface;
using MapsterMapper;
using SqlSugar;

namespace cdisc_dataset.Services;

public class VariableService(ISqlSugarClient sqlSugar, IMapper mapper, ICurrentProjectService currentProjectService) : IVariableService
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
        //var allVariableDtos = mapper.Map<List<VariableDto>>(list);
        return list;
    }

    public async Task<List<VariableDto>> GetAllVariableDtosWithoutErorrAsync()
    {
        var (currentProjectId, currentDataType) = GetCurrentProjectContext();
        var list = await sqlSugar.Queryable<Variable>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Method)
            .Includes(o=>o.CodeList)
            .Includes(o=>o.Dictionary)
            .Where(x => x.ProjectId == currentProjectId && x.CdiscDataType == currentDataType && !x.HasErrors).ToListAsync();
        return mapper.Map<List<VariableDto>>(list);
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

        return await sqlSugar.Queryable<Variable>()
            .Where(x => x.ProjectId == 0 &&
                        x.CdiscDataType == cdiscDataType &&
                        x.DatasetName == datasetName &&
                        x.VariableName == variableName)
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

    public async Task<VariableDto> InsertVariableAsync(VariableDto variableDto)
    {
        var variable = mapper.Map<Variable>(variableDto);
        var entity = await sqlSugar.Insertable(variable).ExecuteReturnEntityAsync();
        return mapper.Map<VariableDto>(entity);
    }

    public async Task<int> UpdateVariableAsync(VariableDto variableDto)
    {
        return await sqlSugar.Updateable(mapper.Map<Variable>(variableDto)).ExecuteCommandAsync();
    }

    public async Task<int> SaveVariablesAsync(IReadOnlyList<VariableDto> variableDtos)
    {
        var sw = Stopwatch.StartNew();
        var list = await Task.Run(() => mapper.Map<List<Variable>>(variableDtos));
        await sqlSugar.Utilities.PageEachAsync(list, 200, async pageList =>
        {
            var storage = await sqlSugar.Storageable(pageList).ToStorageAsync();
            var inserted = await storage.AsInsertable.ExecuteCommandAsync();
            var updated = await storage.AsUpdateable.ExecuteCommandAsync();
        });
        var cost = sw.ElapsedMilliseconds;
        sw.Restart();
        Console.WriteLine($"cost:{cost}");
        return 1;
    }

    public async Task<int> DeleteVariableAsync(VariableDto variable)
    {
        return await sqlSugar.Deleteable<Variable>(mapper.Map<Variable>(variable))
            .ExecuteCommandAsync();
    }
}