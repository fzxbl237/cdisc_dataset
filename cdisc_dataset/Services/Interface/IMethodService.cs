using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;

namespace cdisc_dataset.Services.Interface;

public interface IMethodService
{
    Task<List<MethodDto>> GetAllMethodDtosAsync(int projectId, CdiscDataType dataType);
    Task<List<MethodDto>> GetAllMethodDtosWithoutErorrAsync(int projectId, CdiscDataType dataType);
    Task<List<Method>> GetAllMethodsWithoutErorrAsync(int projectId, CdiscDataType dataType);
    Task<int> DeleteMethodAsync(MethodDto methodDto);
    Task<int> UpdateMethodAsync(MethodDto methodDto);
    Task<MethodDto> InsertMethodAsync(Method method);
    Task<MethodDto> InsertMethodAsync(MethodDto methodDto);
    Task<int> SaveMethodsAsync(List<MethodDto> methods);
}