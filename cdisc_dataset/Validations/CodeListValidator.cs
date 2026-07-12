using cdisc_dataset.Models.Dto;
using FluentValidation;

namespace cdisc_dataset.Validations;

public class CodeListValidator : AbstractValidator<CodeListDto>
{
    public CodeListValidator()
    {
        
        RuleFor(x => x.CommentUniqueId)
            .Must((x, s) => string.IsNullOrWhiteSpace(s) 
                            || s.Equals(x.Comment?.UniqueId))
            .WithSeverity(Severity.Warning)
            .WithMessage("Comment unique id is not exist");
        
        RuleFor(x => x.UniqueId)
            .Empty()
            .When(o=>o.Terms==null||o.Terms.Count==0)
            .WithSeverity(Severity.Warning)
            .WithMessage("Count of terms in this codelist cannot be 0");
        
        RuleFor(x=>x.UniqueId).NotEmpty()
            .WithSeverity(severity:Severity.Error).WithMessage("CodeList Id cannot be empty");
        
        RuleFor(x=>x.Name).NotEmpty()
            .WithSeverity(severity:Severity.Error).WithMessage("CodeList Name cannot be empty");
        
        RuleFor(x=>x.UniqueId).Empty()
            .When(o=>o.IsDuplicate)
            .WithSeverity(Severity.Error)
            .WithMessage("CodeList Id is duplicate");
        
        RuleFor(x=>x.Name).Empty()
            .When(o=>o.IsNameDuplicate)
            .WithSeverity(Severity.Error)
            .WithMessage("CodeList Name is duplicate");
        
        //TODO need custom comboBoxColumn to validation Terminology rather than Code?
        RuleFor(x=>x.Code).Empty()
            .When(o=>string.IsNullOrWhiteSpace(o.Terminology))
            .WithSeverity(Severity.Error)
            .WithMessage("Terminology cannot be empty when Code is not empty");
        
        RuleFor(x=>x.Code).NotEmpty()
            .When(o=>!string.IsNullOrWhiteSpace(o.Terminology))
            .WithSeverity(Severity.Error)
            .WithMessage("Terminology should be empty when Code is empty");
        
        
    }
}