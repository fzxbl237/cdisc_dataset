using System;
using System.Linq;
using System.Threading.Tasks;
using PatChes.Controls.DataGrid;
using PatChes.Models.Dto;
using FluentValidation;

namespace PatChes.Extensions;

public static class ValidationExtensions
{
    public static async Task ValidateDtoAsync<TDto>(this IValidator<TDto> validator, TDto dto, params string[]? propertyNames)
        where TDto : BaseDto
    {
        var selectedProperties = propertyNames?
            .Where(propertyName => !string.IsNullOrWhiteSpace(propertyName))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var validateAllProperties = selectedProperties is null || selectedProperties.Length == 0;

        if (validateAllProperties)
        {
            dto.ClearErrors();
        }
        else
        {
            foreach (var propertyName in selectedProperties!)
            {
                dto.RemoveError(propertyName);
            }
        }

        var result = await validator.ValidateAsync(dto, options =>
        {
            if (!validateAllProperties)
            {
                options.IncludeProperties(selectedProperties!);
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
    }
}
