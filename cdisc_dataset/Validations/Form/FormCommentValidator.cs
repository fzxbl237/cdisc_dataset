using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Controls;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Services.Interface;
using FluentValidation;

namespace cdisc_dataset.Validations.Form;

public class FormCommentValidator : AbstractFormValidator
{
    private readonly ICurrentProjectService _currentProjectService;
    private readonly ICommentService _commentService;
    private readonly IValidator<CommentDto> _validator;
    public CommentDto? CommentDto { get; set; }
    
    public bool IsInEditMode { get; set; }
    

    public FormCommentValidator(ICurrentProjectService currentProjectService,ICommentService commentService,IValidator<CommentDto> validator)
    {
        _currentProjectService = currentProjectService;
        _commentService = commentService;
        _validator = validator;
    }

    protected override async Task<bool> ValidateCoreAsync(string fieldName, object? value, CancellationToken cancellationToken)
    {
        if (CommentDto == null)
        {
            return true;
        }

        if (fieldName.Equals("UniqueId") && !IsInEditMode)
        {
            bool exist = await _commentService.CommentExistsAsync(CommentDto.UniqueId ?? string.Empty);
            if (exist)
            {
                Message = "Comment Id already exists";
                return false;
            }
        }

        var validationResult = await _validator.ValidateAsync(CommentDto, options =>
        {
            if (!string.IsNullOrWhiteSpace(fieldName))
            {
                options.IncludeProperties(fieldName);
            }
        }, cancellationToken);

        if (validationResult.IsValid)
        {
            return true;
        }

        var validationFailure = validationResult.Errors.FirstOrDefault();
        if (validationFailure != null)
        {
            Message = validationFailure.ErrorMessage;
            WarningOnly = validationFailure.Severity == Severity.Warning;
        }

        return false;
    }
}
