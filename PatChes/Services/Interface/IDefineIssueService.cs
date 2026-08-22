using System.Collections.Generic;
using System.Threading.Tasks;
using PatChes.Models.Dto;
using PatChes.Models.Enums;

namespace PatChes.Services.Interface;

public interface IDefineIssueService
{
    Task<List<DefineIssueDto>> GetProjectIssuesAsync(int projectId, CdiscDataType cdiscDataType);
    Task<int> DeleteIssuesAsync(int projectId, CdiscDataType cdiscDataType, IReadOnlyList<int> issueIds);
}
