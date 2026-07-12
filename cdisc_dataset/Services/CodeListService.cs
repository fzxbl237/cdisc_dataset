using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Models.Settings;
using cdisc_dataset.Services.Interface;
using MapsterMapper;
using SqlSugar;

namespace cdisc_dataset.Services;

public class CodeListService(ISqlSugarClient sqlSugar, IMapper mapper, IIssueService issueService) : ICodeListService
{
    private readonly ISqlSugarClient _sqlSugar = sqlSugar;
    private readonly IMapper _mapper = mapper;
    private readonly IIssueService _issueService = issueService;

    public async Task<List<CodeListDto>> GetAllCodeListDtosAsync(int projectId, CdiscDataType dataType)
    {
        var list = await _sqlSugar.Queryable<CodeList>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Terms)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType).ToListAsync();
        var dtos = _mapper.Map<List<CodeListDto>>(list);
        await _issueService.RestoreIssuesAsync(dtos.Cast<BaseDto>(), nameof(CodeListDto), dto => dto.Id);
        return dtos;
    }
    public async Task<List<CodeListDto>> GetAllCodeListDtosWithoutErorrAsync(int projectId, CdiscDataType dataType)
    {
        var list = await _sqlSugar.Queryable<CodeList>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Terms)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors).ToListAsync();
        return _mapper.Map<List<CodeListDto>>(list);
    }
    public async Task<List<CodeList>> GetAllCodeListsWithoutErorrAsync(int projectId, CdiscDataType dataType)
    {
        return await _sqlSugar.Queryable<CodeList>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Terms)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors).ToListAsync();
    }
    public async Task<List<CodeList>> GetAllCodeListsAsync(int projectId, CdiscDataType dataType)
    {
        var list = await _sqlSugar.Queryable<CodeList>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Terms)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType).ToListAsync();
        return list;
    }

    public async Task<CodeListStd> GetCodeListStdAsync(string version, string codeListCode)
    {
        return await _sqlSugar.Queryable<CodeListStd>()
            .Where(o => o.Terminology == version && o.Code == codeListCode)
            .FirstAsync();
    }


    public async Task<bool> DeleteCodeListAsync(CodeListDto codeList)
    {
        return await _sqlSugar.DeleteNav(_mapper.Map<CodeList>(codeList))
            .Include(o => o.Terms)
            .ExecuteCommandAsync();
    }

    public async Task<int> UpdateCodeListAsync(CodeListDto codeListDto)
    {
        return await _sqlSugar.Updateable(_mapper.Map<CodeList>(codeListDto)).ExecuteCommandAsync();
    }

    public async Task<CodeListDto> InsertCodeListAsync(CodeList codeList)
    {
        var entity = await _sqlSugar.InsertNav(codeList)
            .Include(o => o.Terms)
            .ExecuteReturnEntityAsync();
        return _mapper.Map<CodeListDto>(entity);
    }

    public async Task<CodeListDto> InsertCodeListAsync(CodeListDto codeListDto)
    {
        var codeList = _mapper.Map<CodeList>(codeListDto);
        return await InsertCodeListAsync(codeList);
    }

    public async Task<List<string?>> GetTerminologiesAsync()
    {
        //TODO need distinguish sdtm and adam?;
        var list = await _sqlSugar
            .Queryable<CodeListStd>()
            .OrderByDescending(o=>o.Terminology)
            .Select(o=>o.Terminology)
            .Distinct()
            .ToListAsync();
        list.Insert(0,string.Empty);
        return list;
    }

    public async Task<int> SaveCodeListsAsync(List<CodeListDto> codeLists)
    {
        var updateDynamicObject = _mapper.Map<List<CodeList>>(codeLists);
        var result = await _sqlSugar.Updateable(updateDynamicObject).ExecuteCommandAsync();

        await _issueService.SyncIssuesAsync(codeLists, nameof(CodeListDto), dto => dto.Id);

        return result;
    }

    public async Task<VariableCodeList?> GetCodeListRefByVariableAsync(string? variableName)
    {
        return  await sqlSugar.Queryable<VariableCodeList>()
            .AsWithAttr().Where(o=>o.VariableName == variableName)
            .FirstAsync();
    }

    public async Task<CodeListTerm?> GetCodeListTermAsync(string? codeListOid, string? term)
    {
        return  await sqlSugar.Queryable<CodeListTerm>()
            .AsWithAttr().Where(o=>o.CodeListRef == codeListOid &&  o.CodeValue == term)
            .FirstAsync();
    }

    // public async Task<bool> VariableHasCodeListAsync(string? variableName)
    // {
    //     return await _sqlSugar.Queryable<VariableCodeList>().AnyAsync(o => o.VariableName == variableName);
    // }
    
    
}