using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Services.Interface;

namespace cdisc_dataset.Extensions;

public static class DtoIssueExtensions
{
    public static async Task RestoreIssuesAsync(this IIssueService issueService, IEnumerable<BaseDto> dtos, string entityType, Func<BaseDto, int> entityIdSelector)
    {
        foreach (var dto in dtos)
        {
            var entityId = entityIdSelector(dto);
            if (entityId <= 0)
            {
                continue;
            }

            await issueService.RestoreErrorsAsync(dto, entityType, entityId, dto.ProjectId, dto.CdiscDataType);
        }
    }

    public static async Task SyncIssuesAsync(this IIssueService issueService, IEnumerable<BaseDto> dtos, string entityType, Func<BaseDto, int> entityIdSelector)
    {
        foreach (var dto in dtos)
        {
            var entityId = entityIdSelector(dto);
            if (entityId <= 0)
            {
                continue;
            }
            await issueService.SyncErrorDictionaryAsync(entityType, entityId,dto.ProjectId,dto.CdiscDataType, dto.Errors);
        }
    }
}
