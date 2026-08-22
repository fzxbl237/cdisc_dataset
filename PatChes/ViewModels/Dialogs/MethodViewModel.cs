using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using AtomUI.Controls;
using AtomUI.Controls.Utils;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using PatChes.Extensions;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
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

public partial class MethodViewModel : ObservableObject, IDialogHostAware
{
    private readonly IMessageService _messageService;
    private readonly IDocumentService _documentService;
    private readonly FormMethodValidator _formMethodValidator;
    private readonly IValidator<MethodDto> _validator;
    private readonly ICurrentProjectService _currentProjectService;

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
        ICurrentProjectService currentProjectService,
        FormMethodValidator formMethodValidator,
        IValidator<MethodDto> validator)
    {
        _messageService = messageService;
        _documentService = documentService;
        _currentProjectService = currentProjectService;
        _formMethodValidator = formMethodValidator;
        _validator = validator;
    }

    public async Task OnDialogOpenedAsync(IDialogParameters? parameters, CancellationToken cancellationToken)
    {
        parameters ??= new DialogParameters();
        if (parameters.ContainsKey("Title"))
            Title = parameters.GetValue<string>("Title");

        _projectId = _currentProjectService.CurrentProject?.Id ?? 0;
        _cdiscDataType = _currentProjectService.CdiscDataType;

        Method.PropertyChanged -= MethodOnPropertyChanged;
        Method = parameters.ContainsKey("Model") ? parameters.GetValue<MethodDto>("Model") : new MethodDto();
        Method.PropertyChanged += MethodOnPropertyChanged;
        IsInEditMode = Method.Id != 0;
        Method.ProjectId = _projectId;
        Method.CdiscDataType = _cdiscDataType;
        
        _formMethodValidator.MethodDto = Method;
        _formMethodValidator.Validator = _validator;
        Validators.Add(_formMethodValidator);
        await LoadDocuments();
    }

    private void MethodOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MethodDto.UniqueId))
            Method.Name = $"Algorithm to derive {Method.UniqueId}";
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

        var dialogResult = new DialogHostResult
        {
            Result = DialogButtonResult.Yes,
            Parameters = new DialogParameters { { "Model", new MethodDto
            {
                Id = Method.Id,
                ProjectId = Method.ProjectId,
                CdiscDataType = Method.CdiscDataType,
                UniqueId = Method.UniqueId,
                Name = Method.Name,
                Type = Method.Type,
                Description = Method.Description,
                ExpressionContext = Method.ExpressionContext,
                ExpressionCode = Method.ExpressionCode,
                Pages = Method.Pages,
                DocumentId = Method.DocumentId,
                Document = Method.Document,
                DocumentUniqueId = Method.DocumentUniqueId,
                HasUniqueIdDuplicate = Method.HasUniqueIdDuplicate,
                HasNameDuplicate = Method.HasNameDuplicate
            } }
        }
        };
        DialogHost.Close(DialogHostName ?? "Root", dialogResult);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName ?? "Root", new DialogHostResult { Result = DialogButtonResult.Cancel });
    }
}

public class MethodDocumentSelectOption : SelectOption
{
    public string? Title { get; set; }
    public Document? Document { get; set; }
}
