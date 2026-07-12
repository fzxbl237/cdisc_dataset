using System.Threading.Tasks;
using Avalonia.Controls;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using cdisc_dataset.Views;
using DryIoc;
using FluentValidation;
using Prism.DryIoc;
using Prism.Ioc;

namespace cdisc_dataset.Extensions;

public static class ValidationExtensions
{
    public static async Task ValidateDtoAsync<TDto>(this IValidator<TDto> validator, TDto dto, string? propertyName = null)
        where TDto : BaseDto
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            dto.ClearErrors();
        }
        else
        {
            dto.RemoveError(propertyName);
        }

        var result = await validator.ValidateAsync(dto, options =>
        {
            if (!string.IsNullOrWhiteSpace(propertyName))
            {
                options.IncludeProperties(propertyName);
            }
        });

        foreach (var failure in result.Errors)
        {
            dto.SetError(
                failure.PropertyName,
                new DataGridValidationResult(
                    failure.ErrorMessage,
                    failure.Severity == Severity.Error
                        ? DataGridValidationSeverity.Error
                        : DataGridValidationSeverity.Warning));
        }

        // var entityId = dto.GetEntityId();
        // if (entityId is null)
        // {
        //     return;
        // }

        // var application = App.Current;
        // if (application is PrismApplication prismApplication)
        // {
        //     var service = prismApplication.Container.Resolve<IssueService>();
        //     await service.SyncIssuesAsync(dto, typeof(TDto).Name, entityId, result.Errors);
        // }

    }

    public static void ValidateDto<TDto>(this IValidator<TDto> validator, TDto dto, string? propertyName = null)
        where TDto : BaseDto
    {
        validator.ValidateDtoAsync(dto,propertyName).Await();
    }
}
