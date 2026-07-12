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

public class MethodService(ISqlSugarClient sqlSugar, IMapper mapper,IIssueService issueService) : IMethodService
{

    public async Task<List<MethodDto>> GetAllMethodDtosAsync(int projectId, CdiscDataType dataType)
    {
        var list = await sqlSugar.Queryable<Method>()
            .Includes(o=>o.Document)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType)
            .ToListAsync();

        var dtos = mapper.Map<List<MethodDto>>(list);
        await issueService.RestoreIssuesAsync(dtos.Cast<BaseDto>(), nameof(MethodDto), dto => dto.Id);

        return dtos;
    }

    public async Task<List<MethodDto>> GetAllMethodDtosWithoutErorrAsync(int projectId, CdiscDataType dataType)
    {
        var list = await sqlSugar.Queryable<Method>()
            .Includes(o=>o.Document)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType && !x.HasErrors)
            .ToListAsync();

        return mapper.Map<List<MethodDto>>(list);
    }

    public async Task<List<Method>> GetAllMethodsWithoutErorrAsync(int projectId, CdiscDataType dataType)
    {
        var list = await sqlSugar.Queryable<Method>()
            .Includes(o=>o.Document)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType && !x.HasErrors)
            .ToListAsync();

        return list;
    }

    public async Task<int> DeleteMethodAsync(MethodDto methodDto)
    {
        return await sqlSugar.Deleteable(mapper.Map<Method>(methodDto)).ExecuteCommandAsync();
    }

    public async Task<int> UpdateMethodAsync(MethodDto methodDto)
    {
        return await sqlSugar.Updateable(mapper.Map<Method>(methodDto)).ExecuteCommandAsync();
    }

    public async Task<MethodDto> InsertMethodAsync(Method method)
    {
        var entity = await sqlSugar.Insertable(method).ExecuteReturnEntityAsync();
        return mapper.Map<MethodDto>(entity);
    }

    public async Task<MethodDto> InsertMethodAsync(MethodDto methodDto)
    {
        var method = mapper.Map<Method>(methodDto);
        return await InsertMethodAsync(method);
    }

    public async Task<int> SaveMethodsAsync(List<MethodDto> methods)
    {
        var list = mapper.Map<List<Method>>(methods);
        var storage = await sqlSugar.Storageable(list).ToStorageAsync();
        var inserted = await storage.AsInsertable.ExecuteCommandAsync();
        var updated = await storage.AsUpdateable.ExecuteCommandAsync();
        await issueService.SyncIssuesAsync(methods, nameof(MethodDto), dto=>dto.Id);
        return inserted + updated;
    }
}
