using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using AtomUI.Controls;
using PatChes.Models.Dto;
using PatChes.Services;
using PatChes.Services.Interface;
using PatChes.Validations.Form;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using FluentValidation;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace PatChes.ViewModels.Dialogs;

public partial class DocumentViewModel : ObservableObject, IDialogHostAware
{
    private readonly IMessageService _messageService;
    private readonly FormDocumentValidator _formDocumentValidator;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IValidator<DocumentDto> _validator;

    public string? DialogHostName { get; set; }

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private DocumentDto _document = new();

    [ObservableProperty]
    private IList<IFormValidator> _validators = [];

    public DocumentViewModel(
        IMessageService messageService,
        FormDocumentValidator formDocumentValidator,
        ICurrentProjectService currentProjectService,
        IValidator<DocumentDto> validator)
    {
        _messageService = messageService;
        _formDocumentValidator = formDocumentValidator;
        _currentProjectService = currentProjectService;
        _validator = validator;
    }

    public async Task OnDialogOpenedAsync(IDialogParameters? parameters, CancellationToken cancellationToken)
    {
        parameters ??= new DialogParameters();
        if (parameters.ContainsKey("Title"))
            Title = parameters.GetValue<string>("Title");

        Document = parameters.ContainsKey("Model")
            ? parameters.GetValue<DocumentDto>("Model")
            : new DocumentDto();
        Document.ProjectId = _currentProjectService.CurrentProject?.Id ?? 0;
        Document.CdiscDataType = _currentProjectService.CdiscDataType;

        _formDocumentValidator.Document = Document;
        _formDocumentValidator.Validator = _validator;
        Validators.Add(_formDocumentValidator);
    }

    [RelayCommand]
    private async Task Confirm()
    {
        var validationResult = await _validator.ValidateAsync(Document);
        if (!validationResult.IsValid)
        {
            _messageService.Error(validationResult.Errors.First().ErrorMessage);
            return;
        }

        DialogHost.Close(DialogHostName ?? "Root", new DialogHostResult
        {
            Result = DialogButtonResult.Yes,
            Parameters = new DialogParameters { { "Model", Document } }
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName ?? "Root", new DialogHostResult { Result = DialogButtonResult.Cancel });
    }
}
