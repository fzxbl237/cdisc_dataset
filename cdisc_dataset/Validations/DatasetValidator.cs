using System.Text;
using Avalonia.Metadata;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using FluentValidation;
using FluentValidation.Results;
using SqlSugar;

namespace cdisc_dataset.Validations;

public class DatasetValidator:AbstractValidator<DatasetDto>
{
    private readonly ISqlSugarClient _sqlSugar;

    public DatasetValidator(ISqlSugarClient sqlSugar)
    {
        _sqlSugar = sqlSugar;
        RuleFor(x => x.CommentUniqueId).NotEmpty().When(x=>x.Comment!=null).WithSeverity(Severity.Warning)
            .WithMessage("Comment unique id is required");
        
        RuleFor(x => x.CommentUniqueId).Must((x, s) => string.IsNullOrWhiteSpace(s) 
            || s.Equals(x.Comment?.UniqueId)).WithSeverity(Severity.Warning)
            .WithMessage("Comment unique id is not exist");
        

        RuleFor(x => x.Label).CustomAsync(async (x, context, token) =>
        {
            DatasetDto dto = context.InstanceToValidate;

            var std = await sqlSugar.Queryable<Dataset>()
                .Where(o => o.Name == dto.Name && o.ProjectId == 0 && o.CdiscDataType == dto.CdiscDataType)
                .FirstAsync();
            if (std != null)
            {
                if (dto.Label != std.Label)
                {
                    var validationFailure = new ValidationFailure("Label", 
                        $"Label is not standard, standard label should be [{std.Label}]")
                        {
                            Severity = Severity.Warning
                        };
                    context.AddFailure(validationFailure);
                }

                if (dto.Class != std.Class)
                {
                    var validationFailure = new ValidationFailure("Class", 
                        $"Class is not standard, standard class should be [{std.Class}]")
                    {
                        Severity = Severity.Warning
                    };
                    context.AddFailure(validationFailure);
                }
                
                if (dto.SubClass != std.SubClass)
                {
                    var validationFailure = new ValidationFailure("SubClass", 
                        $"SubClass is not standard, standard sub class should be [{std.SubClass}]")
                    {
                        Severity = Severity.Warning
                    };
                    context.AddFailure(validationFailure);
                }
                
                if (dto.Repeating != std.Repeating)
                {
                    var validationFailure = new ValidationFailure("Repeating", 
                        $"Repeating is not standard, standard repeating should be [{std.Repeating}]")
                    {
                        Severity = Severity.Warning
                    };
                    context.AddFailure(validationFailure);
                }
            }
        });
        
        RuleFor(x=>x.Label).Must( (s) => string.IsNullOrWhiteSpace(s) || Encoding.UTF8.GetByteCount(s)<=40)
            .WithSeverity(Severity.Warning)
            .WithMessage("Length of label cannot exceed 40");
        
        RuleFor(x=>x.Label).NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Label is required");
        
        RuleFor(x=>x.Name).MustAsync( async (x, s, token) =>
            {
                var stdDomains = await sqlSugar.Queryable<Dataset>()
                    .Where(o=> o.ProjectId==0 && o.CdiscDataType == x.CdiscDataType).Select(o=>o.Name).ToListAsync();
                if (stdDomains != null)
                    return stdDomains.Contains(s);
                return true;
            }).WithSeverity(Severity.Warning)
            .WithMessage( "Name is not standard");
        
        RuleFor(x=>x.Name).NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Name is required");
        RuleFor(x=>x.Name).Empty()
            .When(o=>o.IsDuplicate)
            .WithSeverity(Severity.Error)
            .WithMessage("Name is duplicate");
        
        RuleFor(x=>x.Class).NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Class is required");
        
        RuleFor(x=>x.Class).NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Class is required");
        
        RuleFor(x=>x.Structure).NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Structure is required");
        
        RuleFor(x=>x.KeyVariables).NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Key Variables is required");
        
        RuleFor(x=>x.Repeating).NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Repeating is required");
    }
}