using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
namespace cdisc_dataset.Services.Interface;

public interface IDatasetService
{
    Task<List<DatasetDto>> GetAllDatasetsAsync();
    Task<List<DatasetDto>> GetAllDatasetDtosWithoutErorrAsync();
    Task<List<Dataset>> GetAllDatasetsWithoutErorrAsync();
    Task<List<Dataset>> GetAllDatasetsWithoutErrorAsync();
    Task<List<string?>> GetDatasetNamesAsync();
    Task<List<string?>> GetAvailableDatasetNamesAsync();
    Task<List<Dataset>> GetAvailableTemplateDatasetsAsync();
    Task<List<Dataset>> GetTemplateDatasetsWithVariablesByNamesAsync(IReadOnlyList<string?> names);
    Task<Dataset?> GetDatasetByName(string? datasetName);
    Task<Dataset?> GetStandardSdtmDatasetByNameAsync(string? datasetName);
    Task<List<DatasetDto>> GetAllDatasetDtosWithoutErrorAsync();
    Task<DatasetDto> InsertDatasetAsync(DatasetDto datasetDto);
    
    Task<bool> InsertDatasetsAsync(List<Dataset> datasets);
    
    Task<int> UpdateDatasetAsync(DatasetDto datasetDto);
    Task<int> SaveDatasetsAsync(IReadOnlyList<DatasetDto> datasetDtos);
    Task<bool> DeleteDatasetAsync(DatasetDto datasetDto);
    
    Task<bool> DeleteDatasetsByProjectIdAsync(int projectId);
    
    Task InsertDatasetsWithVariablesAsync(IReadOnlyList<Dataset> datasets);
}