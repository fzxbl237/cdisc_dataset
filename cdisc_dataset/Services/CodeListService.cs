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

public class CodeListService(ISqlSugarClient sqlSugar, IMapper mapper, IIssueService issueService,ICurrentProjectService currentProjectService) : ICodeListService
{

    private (int ProjectId, CdiscDataType DataType) GetCurrentProjectContext()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;
        return (projectId, dataType);
    }
    
    public async Task<List<CodeListDto>> GetAllCodeListDtosAsync()
    {
        var (currentProjectId, currentDataType) = GetCurrentProjectContext();
        var list = await sqlSugar.Queryable<CodeList>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Terms)
            .Where(x => x.ProjectId == currentProjectId && x.CdiscDataType==currentDataType)
            .Select(o =>
                new CodeListDto()
                {
                    Terms = o.Terms
                }
            ,true).ToListAsync();
        //var dtos = _mapper.Map<List<CodeListDto>>(list);
        await issueService.RestoreIssuesAsync(list.Cast<BaseDto>(), nameof(CodeListDto), dto => dto.Id);
        return list;
    }
    public async Task<List<CodeListDto>> GetAllCodeListDtosWithoutErorrAsync(int projectId, CdiscDataType dataType)
    {
        var list = await sqlSugar.Queryable<CodeList>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Terms)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors).ToListAsync();
        return mapper.Map<List<CodeListDto>>(list);
    }
    public async Task<List<CodeList>> GetAllCodeListsWithoutErorrAsync(int projectId, CdiscDataType dataType)
    {
        return await sqlSugar.Queryable<CodeList>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Terms)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors).ToListAsync();
    }
    public async Task<List<CodeList>> GetAllCodeListsAsync(int projectId, CdiscDataType dataType)
    {
        var list = await sqlSugar.Queryable<CodeList>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Terms)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType).ToListAsync();
        return list;
    }

    public async Task<CodeListStd> GetCodeListStdAsync(string version, string codeListCode)
    {
        return await sqlSugar.Queryable<CodeListStd>()
            .Where(o => o.Terminology == version && o.Code == codeListCode)
            .FirstAsync();
    }


    public async Task<bool> DeleteCodeListAsync(CodeListDto codeList)
    {
        return await sqlSugar.DeleteNav(mapper.Map<CodeList>(codeList))
            .Include(o => o.Terms)
            .ExecuteCommandAsync();
    }

    public async Task<int> UpdateCodeListAsync(CodeListDto codeListDto)
    {
        return await sqlSugar.Updateable(mapper.Map<CodeList>(codeListDto)).ExecuteCommandAsync();
    }

    public async Task<CodeListDto> InsertCodeListAsync(CodeList codeList)
    {
        var entity = await sqlSugar.InsertNav(codeList)
            .Include(o => o.Terms)
            .ExecuteReturnEntityAsync();
        return mapper.Map<CodeListDto>(entity);
    }

    public async Task<CodeListDto> InsertCodeListAsync(CodeListDto codeListDto)
    {
        var codeList = mapper.Map<CodeList>(codeListDto);
        return await InsertCodeListAsync(codeList);
    }

    public async Task<List<string?>> GetTerminologiesAsync()
    {
        //TODO need distinguish sdtm and adam?;
        var list = await sqlSugar
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
        var updateDynamicObject = mapper.Map<List<CodeList>>(codeLists);
        var result = await sqlSugar.Updateable(updateDynamicObject).ExecuteCommandAsync();

        await issueService.SyncIssuesAsync(codeLists, nameof(CodeListDto), dto => dto.Id);

        return result;
    }

    public async Task<VariableCodeList?> GetCodeListRefByVariableAsync(string? variableName)
    {
        return  await sqlSugar.AsTenant().QueryableWithAttr<VariableCodeList>()
            .Where(o=>o.VariableName == variableName)
            .FirstAsync();
    }

    public async Task<CodeListTerm?> GetCodeListTermAsync(string? codeListOid, string? term)
    {
        return await  sqlSugar.AsTenant().QueryableWithAttr<CodeListTerm>()
            .AsWithAttr().Where(o=>o.CodeListRef == codeListOid &&  o.CodeValue == term)
            .FirstAsync();
    }

    public async Task<List<CodeListTerm>> GetCodeListTermsAsync(string? codeListOid)
    {
        return  await sqlSugar.AsTenant().QueryableWithAttr<CodeListTerm>()
            .AsWithAttr().Where(o=>o.CodeListRef == codeListOid)
            .ToListAsync();
    }

    public async Task<CodeListReference?> GetCodeListReferenceByOidAsync(string? codeListOid)
    {
         return  await sqlSugar.AsTenant().QueryableWithAttr<CodeListReference>()
            .AsWithAttr().Where(o=>o.CodeListRef == codeListOid)
            .FirstAsync();
    }
    
    
}