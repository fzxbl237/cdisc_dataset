using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Controls;
using PatChes.Extensions;
using PatChes.Models.Dto;
using FluentValidation;

namespace PatChes.Validations.Form;

public class FormMethodValidator:AbstractFormValidator
{

    public MethodDto? MethodDto { get; set; }
    
    public IValidator<MethodDto>? Validator { get; set; }
    

    protected override async Task<bool> ValidateCoreAsync(string fieldName, object? value, CancellationToken cancellationToken)
    {
        if (MethodDto != null && Validator != null)
        {
            var validationResult = await Validator.ValidateAsync(MethodDto,options =>
            {
                if (!string.IsNullOrWhiteSpace(fieldName))
                {
                    options.IncludeProperties(fieldName);
                }
            },cancellationToken);
            var validationResultIsValid = validationResult.IsValid;
            if (!validationResultIsValid)
            {
                var validationFailure = validationResult.Errors.FirstOrDefault();
                if (validationFailure != null)
                {
                    Message = validationFailure.ErrorMessage;
                    WarningOnly = validationFailure.Severity == Severity.Warning;
                }

                return await Task.FromResult(false);
            }
            
        }
        return await Task.FromResult(true);
    }
}