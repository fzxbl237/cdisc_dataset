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

public class ValueLevelService(ISqlSugarClient sqlSugar, IMapper mapper, IIssueService issueService) : IValueLevelService
{
    public async Task<List<ValueLevelDto>> GetAllValueLevelDtosAsync(int projectId, CdiscDataType dataType)
    {
        var list = await sqlSugar.Queryable<ValueLevel>()
            .Includes(o => o.DatasetEntity)
            .Includes(o => o.VariableEntity)
            .Includes(o=>o.WhereClauses,wc=>wc.VariableEntity,var=>var.CodeList)
            .Includes(o => o.CodeList)
            .Includes(o => o.Method)
            .Includes(o => o.Comment)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType)
            .ToListAsync();

        var dtos = mapper.Map<List<ValueLevelDto>>(list);
        await issueService.RestoreIssuesAsync(dtos.Cast<BaseDto>(), nameof(ValueLevelDto), dto => dto.Id);

        return dtos;
    }

    public async Task<List<ValueLevelDto>> GetAllValueLevelDtosWithoutErorrAsync(int projectId, CdiscDataType dataType)
    {
        var list = await sqlSugar.Queryable<ValueLevel>()
            .Includes(o => o.CodeList)
            .Includes(o => o.WhereClauses)
            .Includes(o => o.Method)
            .Includes(o => o.Comment)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .ToListAsync();

        return mapper.Map<List<ValueLevelDto>>(list);
    }

    public async Task<List<ValueLevel>> GetAllValueLevelsWithoutErorrAsync(int projectId, CdiscDataType dataType)
    {
        return await sqlSugar.Queryable<ValueLevel>()
            .Includes(o => o.CodeList)
            .Includes(o => o.Method)
            .Includes(o => o.Comment)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .ToListAsync();
    }

    public async Task<int> DeleteValueLevelAsync(ValueLevelDto valueLevelDto)
    {
        return await sqlSugar.Deleteable(mapper.Map<ValueLevel>(valueLevelDto)).ExecuteCommandAsync();
    }

    public async Task<int> UpdateValueLevelAsync(ValueLevelDto valueLevelDto)
    {
        return await sqlSugar.Updateable(mapper.Map<ValueLevel>(valueLevelDto)).ExecuteCommandAsync();
    }

    public async Task<ValueLevelDto> InsertValueLevelAsync(ValueLevel valueLevel)
    {
        var entity = await sqlSugar.Insertable(valueLevel).ExecuteReturnEntityAsync();
        return mapper.Map<ValueLevelDto>(entity);
    }

    public async Task<ValueLevelDto> InsertValueLevelAsync(ValueLevelDto valueLevelDto)
    {
        var valueLevel = mapper.Map<ValueLevel>(valueLevelDto);
        return await InsertValueLevelAsync(valueLevel);
    }

    public async Task<bool> SaveValueLevelsAsync(List<ValueLevelDto> valueLevels)
    {
        var list = mapper.Map<List<ValueLevel>>(valueLevels);
        var executeCommandAsync = await sqlSugar.UpdateNav(list, new UpdateNavRootOptions() { IsInsertRoot = true })
            .Include(x => x.WhereClauses).ExecuteCommandAsync();
        // var storage = await sqlSugar.Storageable(list).ToStorageAsync();
        // var inserted = await storage.AsInsertable.ExecuteCommandAsync();
        // var updated = await storage.AsUpdateable.ExecuteCommandAsync();
        await issueService.SyncIssuesAsync(valueLevels, nameof(ValueLevelDto), dto => dto.Id);
        return executeCommandAsync;
    }
}
