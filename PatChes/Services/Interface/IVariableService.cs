using System.Collections.Generic;
using System.Threading.Tasks;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using PatChes.Models.Settings;

namespace PatChes.Services.Interface;

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
    Task<List<VariableTemplate>> GetAvailableSettingVariableTemplatesAsync();
    Task<int> ImportSettingVariablesAsync(IReadOnlyList<int> templateVariableIds);
    
    Task<List<Variable>> LinkMethodToMatchingVariablesAsync(Method method, string matchMode, string matchText);
    Task<int> AssignMethodToVariablesAsync(int methodId, string methodUniqueId, IReadOnlyList<int> variableIds);
    Task<int> AssignCommentToVariablesAsync(int commentId, string commentUniqueId, IReadOnlyList<int> variableIds);
    Task<VariableDto> InsertVariableAsync(VariableDto variableDto);
    Task<int> UpdateVariableAsync(VariableDto variableDto);
    Task<int> SaveVariablesAsync(IReadOnlyList<VariableDto> variableDtos);
    Task<int> DeleteVariableAsync(VariableDto variable);
}