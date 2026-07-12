using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using FluentValidation;
using FluentValidation.Results;

namespace cdisc_dataset.Validations;

public class TermValidator:AbstractValidator<TermDto>
{

    public TermValidator()
    {
        RuleFor(x=>x.Order).NotEmpty()
            .WithSeverity(severity:Severity.Warning).WithMessage("Order cannot be empty");
        
        RuleFor(x=>x.CodeListUniqueId).NotEmpty()
            .WithSeverity(severity:Severity.Error).WithMessage("CodeList cannot be empty");
        
        RuleFor(x=>x.Name).NotEmpty()
            .WithSeverity(severity:Severity.Error).WithMessage("Term cannot be empty");
        
        RuleFor(x=>x.Name).Empty()
            .When(o=>o.IsNameDuplicate)
            .WithSeverity(Severity.Error)
            .WithMessage("Term is duplicate");

        // RuleFor(x => x.IsNameDuplicate)
        //     .Custom((duplicate, context) =>
        //     {
        //         if (duplicate)
        //         {
        //             context.AddFailure("Name", "Term is duplicate");
        //         }
        //     });
        
        RuleFor(x=>x.DecodedValue).Must(o=>false)
            .When(o=>!o.DecodedValueConsistent)
            .WithSeverity(Severity.Error)
            .WithMessage("Decoded values must be all empty or all exist");
        
        RuleFor(x=>x.CodeListUniqueId).Empty()
            .When(o=>o.CodeList?.UniqueId!=o.CodeListUniqueId)
            .WithSeverity(Severity.Error)
            .WithMessage("CodeList is not be defined");
        
    }
    
}