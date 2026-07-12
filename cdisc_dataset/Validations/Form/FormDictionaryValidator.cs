using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Controls;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Services.Interface;
using FluentValidation;

namespace cdisc_dataset.Validations.Form;

public class FormDictionaryValidator(
    IDictionaryService dictionaryService,
    IValidator<DictionaryDto> validator)
    : AbstractFormValidator
{

    public DictionaryDto? Dictionary { get; set; }
    public bool IsInEditMode { get; set; }

    protected override async Task<bool> ValidateCoreAsync(string fieldName, object? value, CancellationToken cancellationToken)
    {
        if (Dictionary == null)
        {
            return true;
        }
        
        

        if (fieldName.Equals("UniqueId") && !IsInEditMode)
        {
            var exists = await dictionaryService.DictionaryExistsAsync(Dictionary.UniqueId ?? string.Empty);
            if (exists)
            {
                Message = "Dictionary Id already exists";
                return false;
            }
        }

        var replace = Regex.Replace(fieldName, @"\d", "");
        var validationResult = await validator.ValidateAsync(Dictionary, options =>
        {
            if (!string.IsNullOrWhiteSpace(replace))
            {
                options.IncludeProperties(replace);
            }
        }, cancellationToken);

        if (validationResult.IsValid)
        {
            return true;
        }

        var validationFailure = validationResult.Errors.FirstOrDefault();
        if (validationFailure != null)
        {
            Message = validationFailure.ErrorMessage;
            WarningOnly = validationFailure.Severity == Severity.Warning;
        }

        return false;
    }
}
