using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Controls;
using AtomUI.Controls.Utils;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using cdisc_dataset.Validations.Form;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using FluentValidation;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class MethodViewModel : ObservableObject, IDialogHostAware
{
    private readonly IMessageService _messageService;
    private readonly IDocumentService _documentService;
    private readonly FormMethodValidator _formMethodValidator;
    private readonly IValidator<MethodDto> _validator;

    private FrozenDictionary<string, Document>? _frozenDocumentDictionary;
    private int _projectId;
    private CdiscDataType _cdiscDataType;

    public string? DialogHostName { get; set; }

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private MethodDto _method = new();

    [ObservableProperty]
    private MethodDocumentSelectOption? _selectedDocumentOption;

    [ObservableProperty]
    private bool _isInEditMode;
    
    [ObservableProperty] 
    private IList<IFormValidator>  _validators = [];

    [ObservableProperty] private AvaloniaList<string> _types = ["Computation", "Imputation"];
    
    public AvaloniaList<ISelectOption> DocumentOptions { get; } = [];
    

    public MethodViewModel(
        IMessageService messageService,
        IDocumentService documentService,
        FormMethodValidator formMethodValidator,
        IValidator<MethodDto> validator)
    {
        _messageService = messageService;
        _documentService = documentService;
        _formMethodValidator = formMethodValidator;
        _validator = validator;
    }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        if (parameters.ContainsKey("Title"))
            Title = parameters.GetValue<string>("Title");

        if (parameters.ContainsKey("ProjectId"))
            _projectId = parameters.GetValue<int>("ProjectId");

        if (parameters.ContainsKey("CdiscDataType"))
            _cdiscDataType = parameters.GetValue<CdiscDataType>("CdiscDataType");

        Method = parameters.ContainsKey("Model") ? parameters.GetValue<MethodDto>("Model") : new MethodDto();
        IsInEditMode = Method.Id != 0;
        Method.ProjectId = _projectId;
        Method.CdiscDataType = _cdiscDataType;
        
        _formMethodValidator.MethodDto = Method;
        _formMethodValidator.Validator = _validator;
        Validators.Add(_formMethodValidator);
        LoadDocuments().Await();
    }

    partial void OnSelectedDocumentOptionChanged(MethodDocumentSelectOption? value)
    {
        if (value?.Document == null)
        {
            Method.Document = null;
            Method.DocumentId = 0;
            Method.DocumentUniqueId = null;
            return;
        }
        Method.Document = value.Document;
        Method.DocumentId = value.Document.Id;
        Method.DocumentUniqueId = value.Document.UniqueId;
    }

    private async Task LoadDocuments()
    {
        var documents = await _documentService.GetAllDocumentsWithoutErorrAsync();
        _frozenDocumentDictionary = documents
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);

        List<ISelectOption> options = [];
        foreach (var document in documents)
        {
            var option = new MethodDocumentSelectOption
            {
                Header = document.UniqueId,
                Content =  $"{document.UniqueId} {document.Title}",
                Title = document.Title,
                Document = document
            };
            options.Add(option);
        }

        DocumentOptions.Clear();
        DocumentOptions.AddRange(options);

        if (!string.IsNullOrWhiteSpace(Method.DocumentUniqueId)
            && _frozenDocumentDictionary.TryGetValue(Method.DocumentUniqueId, out var selectedDocument))
        {
            SelectedDocumentOption = DocumentOptions
                .OfType<MethodDocumentSelectOption>()
                .FirstOrDefault(o => o.Document?.Id == selectedDocument.Id);
            Method.Document = selectedDocument;
            Method.DocumentId = selectedDocument.Id;
        }
    }

    [RelayCommand]
    private async Task Confirm()
    {
        var validationResult = await _validator.ValidateAsync(Method);
        if (!validationResult.IsValid)
        {
            _messageService.Error(validationResult.Errors.First().ErrorMessage);
            return;
        }

        var dialogResult = new DialogResult
        {
            Result = ButtonResult.Yes,
            Parameters = new DialogParameters { { "Model", Method } }
        };
        DialogHost.Close(DialogHostName ?? "Root", dialogResult);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName ?? "Root", new DialogResult { Result = ButtonResult.Cancel });
    }
}

public record MethodDocumentSelectOption : SelectOption
{
    public string? Title { get; set; }
    public Document? Document { get; set; }
}
