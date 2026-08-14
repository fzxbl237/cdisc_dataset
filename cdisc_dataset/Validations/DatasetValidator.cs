using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Metadata;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Models.Settings;
using FluentValidation;
using FluentValidation.Results;
using SqlSugar;

namespace cdisc_dataset.Validations;

public class DatasetValidator:AbstractValidator<DatasetDto>
{
    private readonly ISqlSugarClient _sqlSugar;
    private readonly ConcurrentDictionary<CdiscDataType, Lazy<Task<IReadOnlyDictionary<string, Dataset>>>> _standardDatasetCache = new();
    private readonly ConcurrentDictionary<CdiscDataType, Lazy<Task<HashSet<string>>>> _standardTemplateNameCache = new();

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

            var std = await GetStandardDatasetAsync(dto);
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
            }
        });
        
        RuleFor(x => x.SubClass).CustomAsync(async (x, context, token) =>
        {
            DatasetDto dto = context.InstanceToValidate;

            var std = await GetStandardDatasetAsync(dto);
            if (std != null)
            {
                if (dto.SubClass != std.SubClass)
                {
                    var validationFailure = new ValidationFailure("SubClass", 
                        $"SubClass is not standard, standard sub class should be [{std.SubClass}]")
                    {
                        Severity = Severity.Warning
                    };
                    context.AddFailure(validationFailure);
                }
            }
        });
        
        
        
        RuleFor(x => x.Class).CustomAsync(async (x, context, token) =>
        {
            DatasetDto dto = context.InstanceToValidate;

            var std = await GetStandardDatasetAsync(dto);
            if (std != null)
            {
                if (dto.Class != std.Class)
                {
                    var validationFailure = new ValidationFailure("Class", 
                        $"Class is not standard, standard class should be [{std.Class}]")
                    {
                        Severity = Severity.Warning
                    };
                    context.AddFailure(validationFailure);
                }
            }
        });
        
        RuleFor(x => x.Repeating).CustomAsync(async (x, context, token) =>
        {
            DatasetDto dto = context.InstanceToValidate;

            var std = await GetStandardDatasetAsync(dto);
            if (std != null)
            {
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
                var stdDomains = await GetStandardTemplateNamesAsync(x.CdiscDataType);
                var domain = !string.IsNullOrWhiteSpace(s) && s.StartsWith("SUPP") ? "SUPPQUAL" : s;
                return stdDomains.Contains(domain ?? string.Empty) == !string.IsNullOrWhiteSpace(x.Standard);
            }).WithSeverity(Severity.Warning)
            .WithMessage( "Name must be standard when the Standard column is Provider, and non‑standard otherwise");
        
        RuleFor(x=>x.Name).CustomAsync( async (s, context, token) =>
            {
                DatasetDto dto = context.InstanceToValidate;
                if (!string.IsNullOrWhiteSpace(s) && s.StartsWith("SUPP"))
                {
                    var domain = s.Replace("SUPP", "");
                    var exist=await sqlSugar.Queryable<Dataset>()
                        .AnyAsync(o=>o.CdiscDataType == dto.CdiscDataType 
                                     && o.ProjectId == dto.ProjectId
                                     && o.Name == domain);

                    if (!exist)
                    {
                        var validationFailure = new ValidationFailure("Name", 
                            $"{s} do not should exist when {domain} is not in this project")
                        {
                            Severity = Severity.Error
                        };
                        context.AddFailure(validationFailure);
                    }

                }
            });
        
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

    private async Task<Dataset?> GetStandardDatasetAsync(DatasetDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return null;

        var datasets = await GetStandardDatasetsAsync(dto.CdiscDataType);
        return datasets.TryGetValue(dto.Name, out var std) ? std : null;
    }

    private Task<IReadOnlyDictionary<string, Dataset>> GetStandardDatasetsAsync(CdiscDataType dataType)
    {
        var lazy = _standardDatasetCache.GetOrAdd(dataType, dt =>
            new Lazy<Task<IReadOnlyDictionary<string, Dataset>>>(() => LoadStandardDatasetsAsync(dt)));
        return lazy.Value;
    }

    private async Task<IReadOnlyDictionary<string, Dataset>> LoadStandardDatasetsAsync(CdiscDataType dataType)
    {
        var list = await _sqlSugar.Queryable<Dataset>()
            .Where(o => o.ProjectId == 0 && o.CdiscDataType == dataType)
            .ToListAsync();

        return list
            .Where(o => !string.IsNullOrWhiteSpace(o.Name))
            .GroupBy(o => o.Name!)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private Task<HashSet<string>> GetStandardTemplateNamesAsync(CdiscDataType dataType)
    {
        var lazy = _standardTemplateNameCache.GetOrAdd(dataType, dt =>
            new Lazy<Task<HashSet<string>>>(() => LoadStandardTemplateNamesAsync(dt)));
        return lazy.Value;
    }

    private async Task<HashSet<string>> LoadStandardTemplateNamesAsync(CdiscDataType dataType)
    {
        var names = await _sqlSugar.AsTenant().QueryableWithAttr<DatasetTemplate>()
            .Where(o => o.CdiscDataType == dataType)
            .Select(o => o.Name)
            .ToListAsync();

        return names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .ToHashSet(StringComparer.Ordinal);
    }
}