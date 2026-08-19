using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PatChes.Extensions;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using PatChes.Models.Settings;
using PatChes.Services.Interface;
using MapsterMapper;
using SqlSugar;

namespace PatChes.Services;

public class CodeListService(ISqlSugarClient sqlSugar, IMapper mapper, IIssueService issueService,ICurrentProjectService currentProjectService, ILookupStore lookupStore) : ICodeListService
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
        await issueService.RestoreIssuesAsync(list.Cast<BaseDto>(), nameof(CodeListDto), dto => dto.Id);
        return list;
    }
    public async Task<List<CodeListDto>> GetAllCodeListDtosWithoutErorrAsync()
    {
        var (currentProjectId, currentDataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<CodeList>()
            .Includes(o=>o.Comment)
            .Includes(o=>o.Terms)
            .Where(x => x.ProjectId == currentProjectId && x.CdiscDataType == currentDataType && !x.HasErrors)
            .Select(o => new CodeListDto
            {
                Comment = o.Comment,
                Terms = o.Terms
            }, true)
            .ToListAsync();
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


    public async Task<Dictionary<string, string>> ConfirmCodeListReferenceAsync(CodeListDto codeList)
    {
        var references = new Dictionary<string, string>();
        var variables = await sqlSugar.Queryable<Variable>()
            .Where(x => x.ProjectId == codeList.ProjectId && x.CdiscDataType == codeList.CdiscDataType && x.CodeListId == codeList.Id)
            .Select(x => $"{x.DatasetName}.{x.VariableName}")
            .ToListAsync();
        var valueLevels = await sqlSugar.Queryable<ValueLevel>()
            .Where(x => x.ProjectId == codeList.ProjectId && x.CdiscDataType == codeList.CdiscDataType && x.CodeListId == codeList.Id)
            .Select(x => $"{x.Dataset}.{x.Variable}")
            .ToListAsync();

        if (variables.Count > 0) references.Add("Variables", string.Join(", ", variables));
        if (valueLevels.Count > 0) references.Add("ValueLevels", string.Join(", ", valueLevels));
        return references;
    }

    public async Task<bool> DeleteCodeListAsync(CodeListDto codeList, bool clearReferences = true)
    {
        if (clearReferences)
        {
            await sqlSugar.Updateable<Variable>()
                .SetColumns(x => new Variable { CodeListId = 0, CodeListUniqueId = string.Empty })
                .Where(x => x.ProjectId == codeList.ProjectId && x.CdiscDataType == codeList.CdiscDataType && x.CodeListId == codeList.Id)
                .ExecuteCommandAsync();
            await sqlSugar.Updateable<ValueLevel>()
                .SetColumns(x => new ValueLevel { CodeListId = 0 })
                .Where(x => x.ProjectId == codeList.ProjectId && x.CdiscDataType == codeList.CdiscDataType && x.CodeListId == codeList.Id)
                .ExecuteCommandAsync();
        }

        var result = await sqlSugar.DeleteNav(mapper.Map<CodeList>(codeList))
            .Include(o => o.Terms)
            .ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.CodeList);
        return result;
    }

    public async Task<int> UpdateCodeListAsync(CodeListDto codeListDto)
    {
        var result = await sqlSugar.Updateable(mapper.Map<CodeList>(codeListDto)).ExecuteCommandAsync();
        await lookupStore.RefreshAsync(LookupKind.CodeList);
        return result;
    }

    public async Task<int> UpdateCodeListWithTermsAsync(CodeListDto codeListDto)
    {
        var codeList = mapper.Map<CodeList>(codeListDto);
        var terms = codeList.Terms ?? [];
        foreach (var term in terms)
        {
            term.Id = 0;
            term.CodeListId = codeList.Id;
            term.CodeListUniqueId = codeList.UniqueId;
            term.ProjectId = codeList.ProjectId;
            term.CdiscDataType = codeList.CdiscDataType;
        }

        await sqlSugar.Ado.BeginTranAsync();
        try
        {
            var result = await sqlSugar.Updateable(codeList).ExecuteCommandAsync();
            await sqlSugar.Deleteable<Term>()
                .Where(o => o.ProjectId == codeList.ProjectId
                            && o.CdiscDataType == codeList.CdiscDataType
                            && o.CodeListId == codeList.Id)
                .ExecuteCommandAsync();
            if (terms.Count > 0)
                await sqlSugar.Insertable(terms).ExecuteCommandAsync();
            await sqlSugar.Ado.CommitTranAsync();
            await lookupStore.RefreshAsync(LookupKind.CodeList);
            return result;
        }
        catch
        {
            await sqlSugar.Ado.RollbackTranAsync();
            throw;
        }
    }

    public async Task<CodeListDto> InsertCodeListAsync(CodeList codeList)
    {
        var entity = await sqlSugar.InsertNav(codeList)
            .Include(o => o.Terms)
            .ExecuteReturnEntityAsync();
        await lookupStore.RefreshAsync(LookupKind.CodeList);
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

        await lookupStore.RefreshAsync(LookupKind.CodeList);
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

        await lookupStore.RefreshAsync(LookupKind.CodeList);
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