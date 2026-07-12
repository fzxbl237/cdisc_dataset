using System.Collections.Generic;
using cdisc_dataset.Models.Dto;
using FluentValidation;
using FluentValidation.Results;

namespace cdisc_dataset.Validations;

public class ValueLevelValidator : AbstractValidator<ValueLevelDto>
{
    public ValueLevelValidator()
    {
        RuleFor(x => x.Dataset)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("ValueLevel dataset cannot be empty");
        
        RuleFor(x=>x.Dataset).Empty()
            .When(o=>o.DatasetEntity?.Name!=o.Dataset)
            .WithSeverity(Severity.Error)
            .WithMessage("Dataset is not be defined or has error");

        RuleFor(x => x.Variable)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("ValueLevel variable cannot be empty");
        
        RuleFor(x=>x.Variable).Empty()
            .When(o=>o.VariableEntity?.VariableName!=o.Variable)
            .WithSeverity(Severity.Error)
            //TODO: ErrorCode需要规范和统一
            .WithErrorCode("KT0001")
            .WithMessage("Variable is not be defined or has error");

        RuleFor(x => x)
            .Must(x =>
            {
                var variableOrigin = x.VariableEntity?.Origin?.Trim();
                var valueLevelOrigin = x.Origin?.Trim();

                return string.IsNullOrWhiteSpace(variableOrigin) ||
                       string.IsNullOrWhiteSpace(valueLevelOrigin) ||
                       string.Equals(variableOrigin, valueLevelOrigin, System.StringComparison.OrdinalIgnoreCase);
            })
            .WithSeverity(Severity.Error)
            .WithMessage("When Origin is provided at both the Variable and Value Level, then Origin Type values must match.");
        
        RuleFor(x=>x.WhereClause).Empty()
            .When(o=>!o.IsWhereClauseEffective)
            .WithSeverity(Severity.Warning)
            .WithErrorCode("KT0001")
            .WithMessage("It is not a effective where clause");
        
        RuleFor(x => x.WhereClause)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("ValueLevel where clause cannot be empty");

        RuleFor(x => x.Pages)
            .NotEmpty()
            .When(x => string.Equals(x.Origin?.Trim(), "Collected", System.StringComparison.OrdinalIgnoreCase) &&
                       (string.Equals(x.Source?.Trim(), "Investigator", System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Source?.Trim(), "Subject", System.StringComparison.OrdinalIgnoreCase)))
            .WithSeverity(Severity.Error)
            .WithMessage("Pages is required when Origin Type is 'Collected' and Source is 'Investigator' or 'Subject'.");

        RuleFor(x => x.Label)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("ValueLevel label cannot be empty");

        RuleFor(x => x.Type)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("ValueLevel type cannot be empty");

        RuleFor(x => x.Length)
            .Empty()
            .When(x =>
            {
                List<string> types = ["integer", "float", "text"];
                return !string.IsNullOrWhiteSpace(x.Type) &&
                       !types.Contains(x.Type.Trim().ToLower());
            })
            .WithSeverity(Severity.Error)
            .WithMessage("The Length attribute must be empty when DataType is not integer, float, or text.");

        RuleFor(x => x.Length)
            .NotEmpty()
            .When(x =>
            {
                List<string> types = ["integer", "float", "text"];
                return !string.IsNullOrWhiteSpace(x.Type) &&
                       types.Contains(x.Type.Trim().ToLower());
            })
            .WithSeverity(Severity.Warning)
            .WithMessage("The Length attribute is required when DataType is integer, float, or text.");
        
        RuleFor(x => x.Length)
            .ExclusiveBetween(1, 200)
            .When(x => x.Length.HasValue)
            .WithSeverity(Severity.Error)
            .WithMessage("Length must be between 1 and 200");

        RuleFor(x => x.Digits)
            .Empty()
            .When(x => !string.IsNullOrWhiteSpace(x.Type) && x.Type.Trim().ToLower() != "float")
            .WithSeverity(Severity.Error)
            .WithMessage("The Significant Digits attribute must be empty when DataType is not float.");

        RuleFor(x => x.Digits)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.Type) && x.Type.Trim().ToLower() == "float")
            .WithSeverity(Severity.Warning)
            .WithMessage("Missing Significant Digits value");

        RuleFor(x => x.MethodUniqueId)
            .NotEmpty()
            .When(x=>!string.IsNullOrWhiteSpace(x.Origin) && x.Origin.Equals("Derived", System.StringComparison.OrdinalIgnoreCase))
            .WithSeverity(Severity.Error)
            .WithMessage("The Method is required when Origin is Derived");
        
        RuleFor(x=>x.MethodUniqueId).Empty()
            .When(o=>o.Method?.UniqueId!=o.MethodUniqueId)
            .WithSeverity(Severity.Error)
            .WithMessage("Method is not be defined or has error");
        
        RuleFor(x=>x.CommentUniqueId).Empty()
            .When(o=>o.Comment?.UniqueId!=o.CommentUniqueId)
            .WithSeverity(Severity.Error)
            .WithMessage("Comment is not be defined or has error");

        RuleFor(x => x.CodeListUniqueId)
            .NotEmpty()
            .When(x => x.CodeListId != 0 || x.CodeList != null)
            .WithSeverity(Severity.Error)
            .WithMessage("ValueLevel code list cannot be empty");
        
        RuleFor(x => x.Pages)
            .Must(pages =>
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
            })
            .WithSeverity(Severity.Error)
            .WithMessage("Pages must be a space-separated collection of unique ascending numbers");
    }
    
    
}
