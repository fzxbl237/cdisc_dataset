using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Controls;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using cdisc_dataset.Validations.Form;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using FluentValidation;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Dialogs;

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

    public void OnDialogOpened(IDialogParameters parameters)
    {
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

        DialogHost.Close(DialogHostName ?? "Root", new DialogResult
        {
            Result = ButtonResult.Yes,
            Parameters = new DialogParameters { { "Model", Document } }
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName ?? "Root", new DialogResult { Result = ButtonResult.Cancel });
    }
}
