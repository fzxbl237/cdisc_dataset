using System;
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
    public async Task<List<CodeListDto>> GetAllCodeListDtosWithoutErorrAsync()
    {
        var (currentProjectId, currentDataType) = GetCurrentProjectContext();
        var list = await sqlSugar.Queryable<CodeList>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Terms)
            .Where(x => x.ProjectId == currentProjectId && x.CdiscDataType == currentDataType && !x.HasErrors).ToListAsync();
        return mapper.Map<List<CodeListDto>>(list);
    }
    public async Task<List<CodeList>> GetAllCodeListsWithoutErorrAsync()
    {
        var (currentProjectId, currentDataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<CodeList>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Terms)
            .Where(x => x.ProjectId == currentProjectId && x.CdiscDataType == currentDataType && !x.HasErrors).ToListAsync();
    }
    public async Task<List<CodeList>> GetAllCodeListsAsync()
    {
        var (currentProjectId, currentDataType) = GetCurrentProjectContext();
        var list = await sqlSugar.Queryable<CodeList>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Terms)
            .Where(x => x.ProjectId == currentProjectId && x.CdiscDataType==currentDataType).ToListAsync();
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

    public async Task MergeCodeListsAsync(CodeListDto mergedCodeList, List<int> sourceCodeListIds)
    {
        var sourceIds = sourceCodeListIds.Distinct().ToList();
        if (sourceIds.Count < 2 || !sourceIds.Contains(mergedCodeList.Id))
            throw new ArgumentException("At least two code lists, including the retained code list, are required.");

        var (projectId, dataType) = GetCurrentProjectContext();
        var sourceCodeLists = await sqlSugar.Queryable<CodeList>()
            .Where(o => sourceIds.Contains(o.Id)
                        && o.ProjectId == projectId
                        && o.CdiscDataType == dataType)
            .ToListAsync();
        if (sourceCodeLists.Count != sourceIds.Count)
            throw new InvalidOperationException("One or more code lists are no longer available.");

        var retainedCodeList = sourceCodeLists.FirstOrDefault(o => o.Id == mergedCodeList.Id)
                               ?? throw new InvalidOperationException("The retained code list is no longer available.");
        retainedCodeList.UniqueId = mergedCodeList.UniqueId;
        retainedCodeList.Name = mergedCodeList.Name;

        var terms = (mergedCodeList.Terms ?? [])
            .GroupBy(o => (o.Name, o.Code, o.DecodedValue))
            .Select((group, index) =>
            {
                var term = group.First();
                return new Term
                {
                    Name = term.Name,
                    CommentId = term.CommentId,
                    CommentUniqueId = term.CommentUniqueId,
                    CodeListId = retainedCodeList.Id,
                    CodeListUniqueId = retainedCodeList.UniqueId,
                    Order = index + 1,
                    Code = term.Code,
                    DecodedValue = term.DecodedValue,
                    HasErrors = term.HasErrors,
                    IsNameDuplicate = term.IsNameDuplicate,
                    DecodedValueConsistent = term.DecodedValueConsistent,
                    CdiscDataType = dataType,
                    ProjectId = projectId
                };
            })
            .ToList();
        var deletedCodeListIds = sourceIds.Where(o => o != retainedCodeList.Id).ToList();

        await sqlSugar.Ado.BeginTranAsync();
        try
        {
            await sqlSugar.Updateable(retainedCodeList).ExecuteCommandAsync();
            await sqlSugar.Updateable<Variable>()
                .SetColumns(o => new Variable
                {
                    CodeListId = retainedCodeList.Id,
                    CodeListUniqueId = retainedCodeList.UniqueId
                })
                .Where(o => sourceIds.Contains(o.CodeListId)
                            && o.ProjectId == projectId
                            && o.CdiscDataType == dataType)
                .ExecuteCommandAsync();
            await sqlSugar.Updateable<ValueLevel>()
                .SetColumns(o => new ValueLevel { CodeListId = retainedCodeList.Id })
                .Where(o => sourceIds.Contains(o.CodeListId)
                            && o.ProjectId == projectId
                            && o.CdiscDataType == dataType)
                .ExecuteCommandAsync();
            await sqlSugar.Deleteable<Term>()
                .Where(o => sourceIds.Contains(o.CodeListId)
                            && o.ProjectId == projectId
                            && o.CdiscDataType == dataType)
                .ExecuteCommandAsync();
            if (terms.Count > 0)
                await sqlSugar.Insertable(terms).ExecuteCommandAsync();
            if (deletedCodeListIds.Count > 0)
                await sqlSugar.Deleteable<CodeList>()
                    .Where(o => deletedCodeListIds.Contains(o.Id)
                                && o.ProjectId == projectId
                                && o.CdiscDataType == dataType)
                    .ExecuteCommandAsync();

            await sqlSugar.Ado.CommitTranAsync();
        }
        catch
        {
            await sqlSugar.Ado.RollbackTranAsync();
            throw;
        }
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

    public async Task<List<CodeListReference>> GetAllCodeListReferencesAsync()
    {
        return await sqlSugar.AsTenant()
            .QueryableWithAttr<CodeListReference>()
            .AsWithAttr()
            .Where(o => !string.IsNullOrWhiteSpace(o.CodeListRef))
            .OrderBy(o => o.CodeListRef)
            .ToListAsync();
    }
    
    
}