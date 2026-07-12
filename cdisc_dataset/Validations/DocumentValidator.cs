using cdisc_dataset.Models.Dto;
using FluentValidation;

namespace cdisc_dataset.Validations;

public class DocumentValidator : AbstractValidator<DocumentDto>
{
    public DocumentValidator()
    {
        RuleFor(x => x.UniqueId)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Id should be not empty");

        RuleFor(x => x.UniqueId)
            .Empty()
            .When(o => o.HasUniqueIdDuplicate)
            .WithSeverity(Severity.Error)
            .WithMessage("Duplicate Id");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Title should be not empty");

        RuleFor(x => x.Title)
            .Empty()
            .When(o => o.HasTitleDuplicate)
            .WithSeverity(Severity.Error)
            .WithMessage("Duplicate Title");

        RuleFor(x => x.Href)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Href should be not empty");
    }
}
