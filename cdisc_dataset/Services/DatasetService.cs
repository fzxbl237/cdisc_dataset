using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Models.Settings;
using cdisc_dataset.Services.Interface;
using MapsterMapper;
using SqlSugar;

namespace cdisc_dataset.Services;

public class DatasetService(
    ISqlSugarClient sqlSugar,
    IMapper mapper,
    IIssueService issueService,
    ICurrentProjectService currentProjectService,
    ILookupStore lookupStore) : IDatasetService
{

    public async Task<List<DatasetDto>> GetAllDatasetsAsync()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;

        var list = await sqlSugar.Queryable<Dataset>()
            .Includes(o=>o.Comment)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType)
            .Select(o=> new DatasetDto(){
                Comment = o.Comment
            },true).ToListAsync();
        //await issueService.RestoreIssuesAsync(dtos.Cast<BaseDto>(), nameof(DatasetDto), dto => dto.Id);
        return list;
    }

    public async Task<List<Dataset>> GetAllDatasetsWithoutErorrAsync()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;

        return await sqlSugar.Queryable<Dataset>()
            .Includes(o=>o.Comment)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors).ToListAsync();
    }

    public async Task<List<Dataset>> GetAllDatasetsWithoutErrorAsync()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;

        return await sqlSugar.Queryable<Dataset>()
            .Includes(o => o.Comment)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .ToListAsync();
    }

    public async Task<List<string?>> GetDatasetNamesAsync()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;

        return await sqlSugar.Queryable<Dataset>()
            .Where(o => o.ProjectId == projectId &&
                        o.CdiscDataType == dataType &&
                        !string.IsNullOrWhiteSpace(o.Name))
            .Select(o => o.Name)
            .ToListAsync();
    }

    public async Task<List<Dataset>> GetStandardDatasetsAsync()
    {
        var dataType = currentProjectService.CdiscDataType;
        return await sqlSugar.Queryable<Dataset>()
            .Where(o => o.ProjectId == 0 && o.CdiscDataType == dataType)
            .Select(o => new Dataset
            {
                Name = o.Name,
                Label = o.Label
            })
            .ToListAsync();
    }

    public async Task<List<string?>> GetAvailableDatasetNamesAsync()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;

        var existingDatasetNames = await sqlSugar.Queryable<Dataset>()
            .Where(o => o.ProjectId == projectId &&
                        o.CdiscDataType == dataType &&
                        !string.IsNullOrWhiteSpace(o.Name))
            .Select(o => o.Name)
            .ToListAsync();

        return await sqlSugar.Queryable<Dataset>()
            .Where(o => o.ProjectId == 0 &&
                        o.CdiscDataType == dataType &&
                        !string.IsNullOrWhiteSpace(o.Name) &&
                        !existingDatasetNames.Contains(o.Name))
            .Select(o => o.Name)
            .ToListAsync();
    }

    public async Task<List<Dataset>> GetAvailableSettingDatasetsAsync()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;
        var existingNames = await sqlSugar.Queryable<Dataset>()
            .Where(o => o.ProjectId == projectId && o.CdiscDataType == dataType && !string.IsNullOrWhiteSpace(o.Name))
            .Select(o => o.Name)
            .ToListAsync();

        var templates = await sqlSugar.AsTenant().QueryableWithAttr<DatasetTemplate>()
            .Where(o => !string.IsNullOrWhiteSpace(o.Name) && !existingNames.Contains(o.Name))
            .ToListAsync();
        return templates.Select(MapSettingDataset).ToList();
    }

    public async Task<List<Dataset>> GetSettingDatasetsWithVariablesByNamesAsync(IReadOnlyList<string> names)
    {
        var nameList = names.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToList();
        if (nameList.Count == 0) return [];

        var templates = await sqlSugar.AsTenant().QueryableWithAttr<DatasetTemplate>()
            .Includes(o => o.Variables)
            .Where(o => o.Name != null && nameList.Contains(o.Name))
            .ToListAsync();
        return templates.Select(MapSettingDataset).ToList();
    }

    public async Task<Dataset?> GetSettingDatasetWithVariablesByNameAsync(string datasetName)
    {
        var templates = await GetSettingDatasetsWithVariablesByNamesAsync([datasetName]);
        return templates.FirstOrDefault();
    }

    private static Dataset MapSettingDataset(DatasetTemplate template)
    {
        return new Dataset
        {
            Name = template.Name,
            Label = template.Label,
            Class = template.Class,
            SubClass = template.SubClass,
            Structure = template.Structure,
            KeyVariables = template.KeyVariables,
            Standard = template.Standard,
            HasNoData = template.HasNoData,
            Repeating = template.Repeating,
            ReferenceData = template.ReferenceData,
            Language = template.Language,
            Variables = template.Variables?.Select(variable => new Variable
            {
                Order = variable.Order,
                DatasetName = variable.DatasetName,
                VariableName = variable.VariableName,
                Label = variable.Label,
                DataType = variable.DataType,
                Mandatory = variable.Mandatory,
                Role = variable.Role,
                Core = variable.Core
            }).ToList()
        };
    }

    public async Task<Dataset?> GetDatasetByName(string? datasetName)
    {
        if (string.IsNullOrWhiteSpace(datasetName))
            return null;

        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;

        return await sqlSugar.Queryable<Dataset>()
            .Includes(o => o.Comment)
            .FirstAsync(x => x.ProjectId == projectId && x.CdiscDataType == dataType && x.Name == datasetName);
    }

    public async Task<Dataset?> GetStandardSdtmDatasetByNameAsync(string? datasetName)
    {
        if (string.IsNullOrWhiteSpace(datasetName))
            return null;

        return await sqlSugar.AsTenant().QueryableWithAttr<DatasetTemplate>()
                .Select<Dataset>()
                .FirstAsync(x => x.CdiscDataType == CdiscDataType.Sdtm 
                                 && x.Name == datasetName) ;
    }

    public async Task<List<DatasetDto>> GetAllDatasetDtosWithoutErorrAsync()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;

        return await sqlSugar.Queryable<Dataset>()
            .Includes(o=>o.Comment)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .Select(o => new DatasetDto
            {
                Comment = o.Comment
            }, true)
            .ToListAsync();
    }

    public async Task<List<DatasetDto>> GetAllDatasetDtosWithoutErrorAsync()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;

        return await sqlSugar.Queryable<Dataset>()
            .Includes(o => o.Comment)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .Select(o => new DatasetDto
            {
                Comment = o.Comment
            }, true)
            .ToListAsync();
    }

    public async Task<DatasetDto> InsertDatasetAsync(DatasetDto datasetDto)
    {
        var dataset = mapper.Map<Dataset>(datasetDto);
        var entity = await sqlSugar.Insertable(dataset).ExecuteReturnEntityAsync();
        await lookupStore.RefreshAsync(LookupKind.Dataset);
        return mapper.Map<DatasetDto>(entity);
    }

    public async Task<bool> InsertDatasetsAsync(List<Dataset> datasets)
    {
        var result = await sqlSugar.InsertNav(datasets)
            .Include(o=>o.Variables)
            .ThenInclude(v=>v.CodeList)
            .ThenInclude(c=>c.Terms)
            .ExecuteCommandAsync();
        await RefreshDatasetAndVariableLookupsAsync();
        return result;
    }

    public async Task<int> UpdateDatasetAsync(DatasetDto datasetDto)
    {
        var result = await sqlSugar.Updateable(mapper.Map<Dataset>(datasetDto)).ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.Dataset);
        return result;
    }

    public async Task<int> SaveDatasetsAsync(IReadOnlyList<DatasetDto> datasetDtos)
    {
        var list = mapper.Map<List<Dataset>>(datasetDtos);
        await sqlSugar.Utilities.PageEachAsync(list, 200, async pageList =>
        {
            var storage = await sqlSugar.Storageable(pageList).ToStorageAsync();
            await storage.AsInsertable.ExecuteCommandAsync();
            await storage.AsUpdateable.ExecuteCommandAsync();
        });
        await lookupStore.RefreshAsync(LookupKind.Dataset);
        return 1;
    }

    public async Task<bool> DeleteDatasetAsync(DatasetDto datasetDto)
    {
        var result = await sqlSugar.DeleteNav(mapper.Map<Dataset>(datasetDto))
            .Include(o => o.Variables)
            .ExecuteCommandAsync();
        await RefreshDatasetAndVariableLookupsAsync();
        return result;
    }

    public async Task<bool> DeleteDatasetsByProjectIdAsync(int projectId)
    {
        await sqlSugar.Ado.BeginTranAsync();
        try
        {
            var valueLevelIds = await sqlSugar.Queryable<ValueLevel>()
                .Where(o => o.ProjectId == projectId)
                .Select(o => o.Id)
                .ToListAsync();

            await sqlSugar.Deleteable<Issue>()
                .Where(o => o.ProjectId == projectId)
                .ExecuteCommandAsync();
            if (valueLevelIds.Count > 0)
            {
                await sqlSugar.Deleteable<WhereClause>()
                    .Where(o => valueLevelIds.Contains(o.ValueLevelId))
                    .ExecuteCommandAsync();
            }

            await sqlSugar.Deleteable<ValueLevel>()
                .Where(o => o.ProjectId == projectId)
                .ExecuteCommandAsync();
            await sqlSugar.Deleteable<Term>()
                .Where(o => o.ProjectId == projectId)
                .ExecuteCommandAsync();
            await sqlSugar.Deleteable<Variable>()
                .Where(o => o.ProjectId == projectId)
                .ExecuteCommandAsync();
            await sqlSugar.Deleteable<CodeList>()
                .Where(o => o.ProjectId == projectId)
                .ExecuteCommandAsync();
            await sqlSugar.Deleteable<Dataset>()
                .Where(o => o.ProjectId == projectId)
                .ExecuteCommandAsync();
            await sqlSugar.Deleteable<Method>()
                .Where(o => o.ProjectId == projectId)
                .ExecuteCommandAsync();
            await sqlSugar.Deleteable<Comment>()
                .Where(o => o.ProjectId == projectId)
                .ExecuteCommandAsync();
            await sqlSugar.Deleteable<Document>()
                .Where(o => o.ProjectId == projectId)
                .ExecuteCommandAsync();
            await sqlSugar.Deleteable<Dictionary>()
                .Where(o => o.ProjectId == projectId)
                .ExecuteCommandAsync();

            await sqlSugar.Ado.CommitTranAsync();
            await lookupStore.RefreshAllAsync();
            return true;
        }
        catch
        {
            await sqlSugar.Ado.RollbackTranAsync();
            throw;
        }
    }

    public async Task InsertDatasetsWithVariablesAsync(IReadOnlyList<Dataset> datasets)
    {
        foreach (var dataset in datasets)
        {
            await sqlSugar.InsertNav(dataset).Include(o => o.Variables).ExecuteReturnEntityAsync();
        }

        await RefreshDatasetAndVariableLookupsAsync();
    }

    private async Task RefreshDatasetAndVariableLookupsAsync()
    {
        await lookupStore.RefreshAsync(LookupKind.Dataset);
        await lookupStore.RefreshAsync(LookupKind.Variable);
    }
}