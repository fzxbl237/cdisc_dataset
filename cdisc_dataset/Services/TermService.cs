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

public class TermService(ISqlSugarClient sqlSugar, IMapper mapper, IIssueService issueService, ICurrentProjectService currentProjectService) : ITermService
{

    private (int ProjectId, CdiscDataType DataType) GetCurrentProjectContext()
    {
        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        var dataType = currentProjectService.CdiscDataType;
        return (projectId, dataType);
    }

    public async Task<List<TermDto>> GetAllTermDtosAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        var list = await sqlSugar.Queryable<Term>()
            .Includes(o=>o.CodeList)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType==dataType)
            .Select(o=>new TermDto(){CodeList = o.CodeList},true)
            .ToListAsync();
        await issueService.RestoreIssuesAsync(list.Cast<BaseDto>(), nameof(TermDto), dto => dto.Id);
        return list;
    }

    public async Task<List<TermDto>> GetAllTermDtosWithoutErorrAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        var list = await sqlSugar.Queryable<Term>()
            .Includes(o=>o.CodeList)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .Select(o=>new TermDto(){CodeList = o.CodeList},true)
            .ToListAsync();
        return list;
    }

    public async Task<List<Term>> GetAllTermsWithoutErorrAsync()
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        return await sqlSugar.Queryable<Term>()
            .Includes(o=>o.CodeList)
            .Where(x => x.ProjectId == projectId && x.CdiscDataType == dataType && !x.HasErrors)
            .ToListAsync();
    }

    public async Task<List<Term>?> GetTermsByCodeListIdAsync(int? codeListId)
    {
        return await sqlSugar.Queryable<Term>()
            .Includes(o => o.CodeList)
            .Where(x => x.CodeListId == codeListId)
            .ToListAsync();
    }

    public async Task<List<string?>> GetTermCodesByCodeListIdAsync(string? codeListId)
    {
        var (projectId, dataType) = GetCurrentProjectContext();
        var list = await sqlSugar.Queryable<Term>()
            .Includes(o=>o.CodeList)
            .Where(x => x.ProjectId == projectId 
                        && x.CdiscDataType == dataType
                        && !string.IsNullOrWhiteSpace(x.Code)
                        && x.CodeListUniqueId==codeListId).ToListAsync();
        return list.Select(o=>o.Code).Distinct().ToList();
    }

    public async Task<TermStd?> GetTermStdAsync(string? codeListCode, string? term)
    {
        var termStds = await sqlSugar.Queryable<TermStd>()
            .Where(o => o.CodelistCode == codeListCode)
            .Where(o => o.Name == term)
            .ToListAsync();
        if (termStds.Count > 0)
            return termStds.First();
        return null;
    }

    public async Task<List<TermStd>?> GetTermStdsAsync(string? codeListCode, List<string?> codes)
    {
        return await sqlSugar.Queryable<TermStd>()
            .Where(o => o.CodelistCode == codeListCode)
            .Where(o => codes.Contains(o.Code))
            .ToListAsync();
    }

    public async Task<List<TermStd>?> GetExclusiveTermStdsAsync(string? forCodeListId, string? withCodeListId,string? codeListCode)
    {
        var withTerms = await GetTermCodesByCodeListIdAsync(withCodeListId);
        var forTerms = await GetTermCodesByCodeListIdAsync(forCodeListId);
        var list = withTerms.Except(forTerms).ToList();
        return await GetTermStdsAsync(codeListCode,list);
    }

    public async Task<int> DeleteTermAsync(TermDto term)
    {
        return await sqlSugar.Deleteable(mapper.Map<Term>(term))
            .ExecuteCommandAsync();
    }

    public async Task<int> UpdateTermAsync(TermDto termDto)
    {
        return await sqlSugar.Updateable(mapper.Map<Term>(termDto)).ExecuteCommandAsync();
    }

    public async Task<TermDto> InsertTermAsync(Term term)
    {
        var entity = await sqlSugar.Insertable(term)
            .ExecuteReturnEntityAsync();
        return mapper.Map<TermDto>(entity);
    }

    public async Task<TermDto> InsertTermAsync(TermDto termDto)
    {
        var term = mapper.Map<Term>(termDto);
        return await InsertTermAsync(term);
    }

    public async Task<int> InsertTermsAsync(List<Term> terms)
    {
        return await sqlSugar.Insertable(terms)
            .ExecuteCommandAsync();
    }


    public async Task<int> SaveTermsAsync(List<TermDto> terms)
    {
        var list = mapper.Map<List<Term>>(terms);
        var x= await sqlSugar.Storageable(list).ToStorageAsync();
        var res1 = await x.AsInsertable.ExecuteCommandAsync();
        var res2 = await x.AsUpdateable.ExecuteCommandAsync();

        await issueService.SyncIssuesAsync(terms, nameof(TermDto), dto => dto.Id);

        return res1+res2;
    }
}