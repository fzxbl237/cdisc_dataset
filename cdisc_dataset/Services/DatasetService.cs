using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services.Interface;
using MapsterMapper;
using SqlSugar;

namespace cdisc_dataset.Services;

public class DatasetService(
    ISqlSugarClient sqlSugar,
    IMapper mapper,
    IIssueService issueService,
    ICurrentProjectService currentProjectService) : IDatasetService
{

    public async Task<List<DatasetDto>> GetAllDatasetsAsync()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;

        var list = await sqlSugar.Queryable<Dataset>()
            .Includes(o=>o.Comment)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType).ToListAsync();
        var dtos = mapper.Map<List<DatasetDto>>(list);
        await issueService.RestoreIssuesAsync(dtos.Cast<BaseDto>(), nameof(DatasetDto), dto => dto.Id);
        return dtos;
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

    public async Task<List<Dataset>> GetAvailableTemplateDatasetsAsync()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;

        var existingNames = await sqlSugar.Queryable<Dataset>()
            .Where(o => o.ProjectId == projectId &&
                        o.CdiscDataType == dataType &&
                        !string.IsNullOrWhiteSpace(o.Name))
            .Select(o => o.Name)
            .ToListAsync();

        return await sqlSugar.Queryable<Dataset>()
            .Where(o => o.ProjectId == 0 &&
                        o.CdiscDataType == dataType &&
                        !string.IsNullOrWhiteSpace(o.Name) &&
                        !string.IsNullOrWhiteSpace(o.Label) &&
                        !existingNames.Contains(o.Name))
            .ToListAsync();
    }

    public async Task<List<Dataset>> GetTemplateDatasetsWithVariablesByNamesAsync(IReadOnlyList<string?> names)
    {
        if (names.Count == 0) return [];

        var dataType = currentProjectService.CdiscDataType;
        var nameList = names.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();

        return await sqlSugar.Queryable<Dataset>()
            .Includes(o => o.Variables)
            .Where(o => o.ProjectId == 0 &&
                        o.CdiscDataType == dataType &&
                        !string.IsNullOrWhiteSpace(o.Name) &&
                        nameList.Contains(o.Name))
            .ToListAsync();
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

        return await sqlSugar.Queryable<Dataset>()
            .Includes(o => o.Comment)
            .FirstAsync(x => x.ProjectId == 0 && x.CdiscDataType == CdiscDataType.Sdtm && x.Name == datasetName);
    }

    public async Task<List<DatasetDto>> GetAllDatasetDtosWithoutErorrAsync()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;

        var list = await sqlSugar.Queryable<Dataset>()
            .Includes(o=>o.Comment)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .ToListAsync();
        return mapper.Map<List<DatasetDto>>(list);
    }

    public async Task<List<DatasetDto>> GetAllDatasetDtosWithoutErrorAsync()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;

        var list = await sqlSugar.Queryable<Dataset>()
            .Includes(o => o.Comment)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .ToListAsync();
        return mapper.Map<List<DatasetDto>>(list);
    }

    public async Task<DatasetDto> InsertDatasetAsync(DatasetDto datasetDto)
    {
        var dataset = mapper.Map<Dataset>(datasetDto);
        var entity = await sqlSugar.Insertable(dataset).ExecuteReturnEntityAsync();
        return mapper.Map<DatasetDto>(entity);
    }

    public async Task<int> UpdateDatasetAsync(DatasetDto datasetDto)
    {
        return await sqlSugar.Updateable(mapper.Map<Dataset>(datasetDto)).ExecuteCommandAsync();
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
        return 1;
    }

    public async Task<bool> DeleteDatasetAsync(DatasetDto datasetDto)
    {
        return await sqlSugar.DeleteNav(mapper.Map<Dataset>(datasetDto))
            .Include(o => o.Variables)
            .ExecuteCommandAsync();
    }

    public async Task InsertDatasetsWithVariablesAsync(IReadOnlyList<Dataset> datasets)
    {
        foreach (var dataset in datasets)
        {
            await sqlSugar.InsertNav(dataset).Include(o => o.Variables).ExecuteReturnEntityAsync();
        }
    }
}