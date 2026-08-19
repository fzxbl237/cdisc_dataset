using System.Collections.Generic;
using System.Threading.Tasks;
using PatChes.Models;
using PatChes.Models.Dto;

namespace PatChes.Services.Interface;

public interface IMethodService
{
    Task<List<MethodDto>> GetAllMethodDtosAsync();
    Task<List<MethodDto>> GetAllMethodDtosWithoutErorrAsync();
    Task<List<Method>> GetAllMethodsWithoutErorrAsync();
    Task<Dictionary<string, string>> ConfirmMethodReferenceAsync(MethodDto methodDto);
    Task<int> DeleteMethodAsync(MethodDto methodDto, bool clearReferences = true);
    Task<int> UpdateMethodAsync(MethodDto methodDto);
    Task<MethodDto> InsertMethodAsync(Method method);
    Task<MethodDto> InsertMethodAsync(MethodDto methodDto, bool linkMatchingVariables, string? variableMatchMode, string? variableMatchText);
    Task<int> SaveMethodsAsync(List<MethodDto> methods);
}