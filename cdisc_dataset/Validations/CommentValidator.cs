using System.Collections.Generic;
using System.Linq;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Services.Interface;
using FluentValidation;

namespace cdisc_dataset.Validations;

public class CommentValidator : AbstractValidator<CommentDto>
{
    private readonly ICurrentProjectService _currentProjectService;

    public CommentValidator(ICurrentProjectService  currentProjectService)
    {
        _currentProjectService = currentProjectService;
        RuleFor(x => x.UniqueId)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Id should be not empty");
        
        RuleFor(x => x.Description)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Description should be not empty");

        RuleFor(x => x.UniqueId)
            .Empty()
            .When(o => o.HasUniqueIdDuplicate)
            .WithSeverity(Severity.Error)
            .WithMessage("Duplicate Id");

        RuleFor(x => x.Pages)
            .NotEmpty()
            .When(o => !string.IsNullOrWhiteSpace(o.DocumentUniqueId))
            .WithSeverity(Severity.Error)
            .WithMessage("Pages should be not empty when Document is set");

        RuleFor(x => x.DocumentUniqueId)
            .NotEmpty()
            .When(o => !string.IsNullOrWhiteSpace(o.Pages))
            .WithSeverity(Severity.Error)
            .WithMessage("Document should be not null when Pages is set");

        RuleFor(x => x.DocumentUniqueId)
            .Empty()
            .When(o => o.Document?.UniqueId != o.DocumentUniqueId)
            .WithSeverity(Severity.Error)
            .WithMessage("Document is not be defined");

        RuleFor(x => x.Pages)
            .Must(IsValidPages)
            .WithSeverity(Severity.Error)
            .WithMessage("Pages must be a space-separated collection of unique ascending numbers");
    }

    private static bool IsValidPages(string? pages)
    {
        if (string.IsNullOrWhiteSpace(pages))
        {
            return true;
        }

        var pageNumbers = pages.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (pageNumbers.Length == 0)
        {
            return false;
        }

        var previous = int.MinValue;
        var seen = new HashSet<int>();

        foreach (var page in pageNumbers)
        {
            if (!int.TryParse(page, out var number))
            {
                return false;
            }

            if (!seen.Add(number) || number <= previous)
            {
                return false;
            }

            previous = number;
        }

        return true;
    }
}
