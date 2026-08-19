using System.Collections.Generic;
using System.Threading.Tasks;
using PatChes.Controls.DataGrid;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using FluentValidation.Results;

namespace PatChes.Services.Interface;

public interface IIssueService
{
    Task SyncIssuesAsync<TDto>(TDto dto, string entityType, int entityId,int projectId,CdiscDataType cdiscDataType, IEnumerable<ValidationFailure> failures)
        where TDto : BaseDto;

    Task SyncErrorDictionaryAsync(string entityType, int entityId,int projectId,CdiscDataType cdiscDataType, Dictionary<string, List<DataGridValidationResult>> errorDictionary);

    Task RestoreErrorsAsync<TDto>(TDto dto, string entityType, int entityId, int projectId, CdiscDataType cdiscDataType)
        where TDto : BaseDto;

    Task<List<IssueDto>> GetIssuesAsync(string entityType, int entityId, int projectId, CdiscDataType cdiscDataType);

    Task<List<IssueDto>> GetProjectIssuesAsync(int projectId, CdiscDataType cdiscDataType);

    Task<int> DeleteIssuesAsync(int projectId, CdiscDataType cdiscDataType, IReadOnlyList<int> issueIds);
}
