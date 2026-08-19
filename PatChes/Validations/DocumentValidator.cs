using System.IO;
using System.Linq;
using PatChes.Models.Dto;
using FluentValidation;

namespace PatChes.Validations;

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
            .When(o => o.IsUniqueIdDuplicate)
            .WithSeverity(Severity.Error)
            .WithMessage("Duplicate Id");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Title should be not empty");

        RuleFor(x => x.Title)
            .Empty()
            .When(o => o.IsTitleDuplicate)
            .WithSeverity(Severity.Error)
            .WithMessage("Duplicate Title");

        RuleFor(x => x.Href)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Href should be not empty");

        RuleFor(x => x.Href)
            .Must(HasValidFileExtension)
            .When(x => !string.IsNullOrWhiteSpace(x.Href))
            .WithSeverity(Severity.Error)
            .WithMessage("Href must include a valid file extension");

        RuleFor(x => x.Href)
            .Empty()
            .When(o => o.IsHrefDuplicate)
            .WithSeverity(Severity.Error)
            .WithMessage("Duplicate Href");
    }

    private static bool HasValidFileExtension(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return false;

        var path = href.Trim().Replace('\\', '/');
        var queryStart = path.IndexOfAny(['?', '#']);
        if (queryStart >= 0)
            path = path[..queryStart];

        var fileName = path.Split('/').LastOrDefault();
        if (string.IsNullOrWhiteSpace(fileName) || fileName.EndsWith('.'))
            return false;

        var extension = Path.GetExtension(fileName);
        return extension.Length > 1 && extension[1..].All(char.IsLetterOrDigit);
    }
}
