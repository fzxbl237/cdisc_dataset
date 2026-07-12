using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Controls;
using cdisc_dataset.Models.Dto;
using FluentValidation;

namespace cdisc_dataset.Validations.Form;

public class FormValueLevelValidator : AbstractFormValidator
{
    public ValueLevelDto? ValueLevelDto { get; set; }

    public IValidator<ValueLevelDto>? Validator { get; set; }

    protected override async Task<bool> ValidateCoreAsync(string fieldName, object? value, CancellationToken cancellationToken)
    {
        if (ValueLevelDto != null && Validator != null)
        {
            var validationResult = await Validator.ValidateAsync(ValueLevelDto, options =>
            {
                if (!string.IsNullOrWhiteSpace(fieldName))
                {
                    options.IncludeProperties(fieldName);
                }
            }, cancellationToken);

            if (!validationResult.IsValid)
            {
                var validationFailure = validationResult.Errors.FirstOrDefault();
                if (validationFailure != null)
                {
                    Message = validationFailure.ErrorMessage;
                    WarningOnly = validationFailure.Severity == Severity.Warning;
                }

                return false;
            }
        }

        return true;
    }


}
