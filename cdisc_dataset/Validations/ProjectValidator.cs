using cdisc_dataset.Models.Dto;
using FluentValidation;
using FluentValidation.Results;

namespace cdisc_dataset.Validations;

public class ProjectValidator : AbstractValidator<ProjectDto>
{
    public ProjectValidator()
    {
        RuleFor(x => x.ProjectCode)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Project number cannot be empty");

        RuleFor(x => x.ProtocolCode)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Protocol number cannot be empty");
        
        RuleFor(x => x.ProtocolDescription)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Protocol description cannot be empty");
        
        RuleFor(x => x.DrugCode)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Drug code cannot be empty");
    }
}