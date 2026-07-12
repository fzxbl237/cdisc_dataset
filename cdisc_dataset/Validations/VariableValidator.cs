using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using FluentValidation;
using FluentValidation.Results;

namespace cdisc_dataset.Validations;

public class VariableValidator : AbstractValidator<VariableDto>
{
    public VariableValidator()
    {
        RuleFor(x => x.DatasetName)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Variable dataset cannot be empty");

        RuleFor(x => x.VariableName)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Variable name cannot be empty");

        RuleFor(x => x.Label)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Variable label cannot be empty");

        RuleFor(x => x.DataType)
            .NotEmpty()
            .WithSeverity(Severity.Error)
            .WithMessage("Variable type cannot be empty");

        RuleFor(x => x.MethodUniqueId)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.Origin) && x.Origin.Equals("Derived", System.StringComparison.OrdinalIgnoreCase))
            .WithSeverity(Severity.Error)
            .WithMessage("The Method is required when Origin is Derived");

        RuleFor(x => x.Origin)
            .Equal("Protocol")
            .When(x =>
                x.CdiscDataType == CdiscDataType.Sdtm &&
                !string.IsNullOrWhiteSpace(x.VariableName) &&
                x.VariableName.Equals("STUDYID", System.StringComparison.OrdinalIgnoreCase))
            .WithSeverity(Severity.Warning)
            .WithMessage("Origin for STUDYID variable is not set to Protocol");

        RuleFor(x => x.MethodUniqueId)
            .Empty()
            .When(x => x.Method?.UniqueId != x.MethodUniqueId)
            .WithSeverity(Severity.Error)
            .WithMessage("Method is not be defined or has error");

        RuleFor(x => x.CommentUniqueId)
            .Empty()
            .When(x => x.Comment?.UniqueId != x.CommentUniqueId)
            .WithSeverity(Severity.Error)
            .WithMessage("Comment is not be defined or has error");

        RuleFor(x => x.CodeListUniqueId)
            .Empty()
            .When(x => x.CodeList?.UniqueId != x.CodeListUniqueId &&  x.Dictionary?.UniqueId != x.CodeListUniqueId)
            .WithSeverity(Severity.Error)
            .WithMessage("Variable code list or dictionary is not be defined or has error");

        RuleFor(x => x.CodeListUniqueId)
            .Must((x,codeListUniqueId) =>
                !string.IsNullOrWhiteSpace(x.Dictionary?.DictionaryName) &&
                x.Dictionary.DictionaryName.Trim().Equals("MedDRA", System.StringComparison.OrdinalIgnoreCase))
            .When(x =>
            {
                var variableName = x.VariableName?.Trim();
                if (string.IsNullOrWhiteSpace(variableName))
                {
                    return false;
                }

                return Regex.IsMatch(variableName, @"^(AE|MH)(LLT|LLTCD|DECOD|PTCD|HLT|HLTCD|HLGT|HLGTCD|BODSYS|BDSYCD|SOC|SOCCD)$", RegexOptions.IgnoreCase);
            })
            .WithSeverity(Severity.Error)
            .WithMessage("Variables starting with AE or MH and ending with LLT, LLTCD, DECOD, PTCD, HLT, HLTCD, HLGT, HLGTCD, BODSYS, BDSYCD, SOC, or SOCCD must reference MedDRA dictionary");
        
        RuleFor(x => x.Length)
            .Empty()
            .When(x =>
            {
                List<string> types = ["integer", "float", "text"];
                return !string.IsNullOrWhiteSpace(x.DataType) &&
                       !types.Contains(x.DataType.Trim().ToLower());
            })
            .WithSeverity(Severity.Error)
            .WithMessage("The Length attribute must be empty when DataType is not integer, float, or text.");

        RuleFor(x => x.Length)
            .NotEmpty()
            .When(x =>
            {
                List<string> types = ["integer", "float", "text"];
                return !string.IsNullOrWhiteSpace(x.DataType) &&
                       types.Contains(x.DataType.Trim().ToLower());
            })
            .WithSeverity(Severity.Warning)
            .WithMessage("The Length attribute is required when DataType is integer, float, or text.");

        RuleFor(x => x.SignificantDigits)
            .Empty()
            .When(x => !string.IsNullOrWhiteSpace(x.DataType) && x.DataType.Trim().ToLower() != "float")
            .WithSeverity(Severity.Error)
            .WithMessage("The Significant Digits attribute must be empty when DataType is not float.");

        RuleFor(x => x.SignificantDigits)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.DataType) && x.DataType.Trim().ToLower() == "float")
            .WithSeverity(Severity.Warning)
            .WithMessage("Missing Significant Digits value");

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

        RuleFor(x => x.Length)
            .ExclusiveBetween(1, 200)
            .When(x => x.Length.HasValue)
            .WithSeverity(Severity.Error)
            .WithMessage("Length must be between 1 and 200");
        
    }
}
