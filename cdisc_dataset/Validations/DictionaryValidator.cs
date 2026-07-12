using cdisc_dataset.Models.Dto;
using FluentValidation;
using FluentValidation.Results;

namespace cdisc_dataset.Validations;

public class DictionaryValidator : AbstractValidator<DictionaryDto>
{
    public DictionaryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Dictionary name cannot be empty");

        RuleFor(x => x.UniqueId)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Dictionary unique id cannot be empty");
        
        RuleFor(x => x.UniqueId)
            .Empty()
            .When(o => o.HasUniqueIdDuplicate)
            .WithSeverity(Severity.Error)
            .WithMessage("Dictionary unique id is duplicate");

        RuleFor(x => x.Name)
            .Empty()
            .When(o => o.HasNameDuplicate)
            .WithSeverity(Severity.Error)
            .WithMessage("Dictionary name is duplicate");
        
        RuleFor(x => x.DictionaryName)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Dictionary name cannot be empty");
        
        RuleFor(x => x.Version)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Dictionary version cannot be empty");
    }
}
