using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using FluentValidation.Results;

namespace cdisc_dataset.Services.Interface;

public interface IIssueService
{
    Task SyncIssuesAsync<TDto>(TDto dto, string entityType, int entityId,int projectId,CdiscDataType cdiscDataType, IEnumerable<ValidationFailure> failures)
        where TDto : BaseDto;

    Task SyncErrorDictionaryAsync(string entityType, int entityId,int projectId,CdiscDataType cdiscDataType, Dictionary<string, List<DataGridValidationResult>> errorDictionary);

    Task RestoreErrorsAsync<TDto>(TDto dto, string entityType, int entityId, int projectId, CdiscDataType cdiscDataType)
        where TDto : BaseDto;

    Task<List<IssueDto>> GetIssuesAsync(string entityType, int entityId, int projectId, CdiscDataType cdiscDataType);
}
