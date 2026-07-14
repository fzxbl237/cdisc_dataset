using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;

namespace cdisc_dataset.Services.Interface;

public interface IMethodService
{
    Task<List<MethodDto>> GetAllMethodDtosAsync();
    Task<List<MethodDto>> GetAllMethodDtosWithoutErorrAsync();
    Task<List<Method>> GetAllMethodsWithoutErorrAsync();
    Task<int> DeleteMethodAsync(MethodDto methodDto);
    Task<int> UpdateMethodAsync(MethodDto methodDto);
    Task<MethodDto> InsertMethodAsync(Method method);
    Task<MethodDto> InsertMethodAsync(MethodDto methodDto);
    Task<int> SaveMethodsAsync(List<MethodDto> methods);
}