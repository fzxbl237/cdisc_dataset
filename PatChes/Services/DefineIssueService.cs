using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using PatChes.Services.Interface;
using SqlSugar;

namespace PatChes.Services;

public sealed class DefineIssueService(ISqlSugarClient sqlSugar) : IDefineIssueService
{
    public async Task<List<DefineIssueDto>> GetProjectIssuesAsync(int projectId, CdiscDataType cdiscDataType)
    {
        return await sqlSugar.Queryable<DefineIssue>()
            .Where(issue => issue.ProjectId == projectId && issue.CdiscDataType == cdiscDataType)
            .OrderBy(issue => issue.Severity == "Error" ? 0 : 1)
            .OrderBy(issue => issue.Id)
            .Select<DefineIssueDto>()
            .ToListAsync();
    }

    public async Task<int> DeleteIssuesAsync(int projectId, CdiscDataType cdiscDataType, IReadOnlyList<int> issueIds)
    {
        if (issueIds.Count == 0)
            return 0;

        return await sqlSugar.Deleteable<DefineIssue>()
            .Where(issue => issue.ProjectId == projectId
                            && issue.CdiscDataType == cdiscDataType
                            && issueIds.Contains(issue.Id))
            .ExecuteCommandAsync();
    }
}
