using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;

namespace cdisc_dataset.Services.Interface;

public interface IVariableService
{
    Task<List<VariableDto>> GetAllVariableDtosAsync();
    Task<List<VariableDto>> GetAllVariableDtosWithoutErorrAsync();
    Task<List<Variable>> GetAllVariablesWithoutErorrAsync();
    
    Task<List<Variable>> GetAllVariablesByDatasetIdAsync(int datasetId);
    Task<List<Variable>> GetAllVariablesByDatasetIdWithoutErorrAsync(int datasetId);
    
    Task<Variable?> GetVariableByDatasetIdAndVariableNameWithoutError(int datasetId, string? variableName);
    Task<Variable?> GetStandardVariableByDatasetAndVariableNameAsync(string? datasetName, string? variableName, CdiscDataType cdiscDataType);
    
    Task<List<VariableDto>> GetAvailableVariablesAsync(string? datasetName);
    
    Task<VariableDto> InsertVariableAsync(VariableDto variableDto);
    Task<int> UpdateVariableAsync(VariableDto variableDto);
    Task<int> SaveVariablesAsync(IReadOnlyList<VariableDto> variableDtos);
    Task<int> DeleteVariableAsync(VariableDto variable);
}