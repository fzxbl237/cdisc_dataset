using System;
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

public class MethodService(ISqlSugarClient sqlSugar, IMapper mapper, IIssueService issueService, ICurrentProjectService currentProjectService, ILookupStore lookupStore) : IMethodService
{
    private (int ProjectId, CdiscDataType DataType) GetCurrentProjectContext()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;
        return (projectId, dataType);
    }

    public async Task<List<MethodDto>> GetAllMethodDtosAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        var dtos = await sqlSugar.Queryable<Method>()
            .Includes(o=>o.Document)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType)
            .Select(o => new MethodDto
            {
                Document = o.Document
            }, true)
            .ToListAsync();
        await issueService.RestoreIssuesAsync(dtos.Cast<BaseDto>(), nameof(MethodDto), dto => dto.Id);

        return dtos;
    }

    public async Task<List<MethodDto>> GetAllMethodDtosWithoutErorrAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Method>()
            .Includes(o=>o.Document)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType && !x.HasErrors)
            .Select(o => new MethodDto
            {
                Document = o.Document
            }, true)
            .ToListAsync();
    }

    public async Task<List<Method>> GetAllMethodsWithoutErorrAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        var list = await sqlSugar.Queryable<Method>()
            .Includes(o=>o.Document)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType && !x.HasErrors)
            .ToListAsync();

        return list;
    }

    public async Task<int> DeleteMethodAsync(MethodDto methodDto)
    {
        var result = await sqlSugar.Deleteable(mapper.Map<Method>(methodDto)).ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.Method);
        return result;
    }

    public async Task<int> UpdateMethodAsync(MethodDto methodDto)
    {
        var result = await sqlSugar.Updateable(mapper.Map<Method>(methodDto)).ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.Method);
        return result;
    }

    public async Task<MethodDto> InsertMethodAsync(Method method)
    {
        var entity = await sqlSugar.Insertable(method).ExecuteReturnEntityAsync();
        await lookupStore.RefreshAsync(LookupKind.Method);
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
        await lookupStore.RefreshAsync(LookupKind.Method);
        return inserted + updated;
    }
}
