using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Controls;
using PatChes.Models.Dto;
using FluentValidation;

namespace PatChes.Validations.Form;

public class FormDocumentValidator : AbstractFormValidator
{
    public DocumentDto? Document { get; set; }

    public IValidator<DocumentDto>? Validator { get; set; }

    protected override async Task<bool> ValidateCoreAsync(string fieldName, object? value, CancellationToken cancellationToken)
    {
        if (Document == null || Validator == null)
            return true;

        var validationResult = await Validator.ValidateAsync(Document, options =>
        {
            if (!string.IsNullOrWhiteSpace(fieldName))
                options.IncludeProperties(fieldName);
        }, cancellationToken);

        if (validationResult.IsValid)
            return true;

        var validationFailure = validationResult.Errors.FirstOrDefault();
        if (validationFailure != null)
        {
            Message = validationFailure.ErrorMessage;
            WarningOnly = validationFailure.Severity == Severity.Warning;
        }

        return false;
    }
}
