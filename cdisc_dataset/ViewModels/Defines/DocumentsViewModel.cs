using AsyncNavigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using cdisc_dataset.Validations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;
using NavigationContext = AsyncNavigation.NavigationContext;

namespace cdisc_dataset.ViewModels.Defines;

public partial class DocumentsViewModel : ConfirmNavigationViewModelBase
{
    private readonly IMessageService _messageService;
    private readonly IDocumentService _documentService;
    private readonly IIssueService _issueService;
    private readonly IDialogHostService _dialogHostService;
    private readonly cdisc_dataset.Services.IDialogService _dialogService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IValidator<DocumentDto> _validator;
    private readonly IReferenceDeletionService _referenceDeletionService;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private bool _isErrorOnly;

    private readonly List<DocumentDto> _allDocuments = [];
    public AvaloniaList<DocumentDto> Documents { get; } = [];

    public DocumentsViewModel(
        IMessageService messageService,
        IDocumentService documentService,
        IIssueService issueService,
        IDialogHostService dialogHostService,
        cdisc_dataset.Services.IDialogService dialogService,
        ICurrentProjectService currentProjectService,
        IValidator<DocumentDto> validator,
        IReferenceDeletionService referenceDeletionService)
    {
        _messageService = messageService;
        _documentService = documentService;
        _issueService = issueService;
        _dialogHostService = dialogHostService;
        _dialogService = dialogService;
        _currentProjectService = currentProjectService;
        _validator = validator;
        _referenceDeletionService = referenceDeletionService;

    }

    partial void OnIsErrorOnlyChanged(bool value) => RefreshDocuments();

    private void RefreshDocuments()
    {
        Documents.Clear();
        Documents.AddRange(_allDocuments
            .Where(document => !IsErrorOnly || document.HasErrors)
            .OrderBy(document => document.UniqueId, StringComparer.OrdinalIgnoreCase));
    }

    private void DocumentDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DocumentDto documentDto || string.IsNullOrWhiteSpace(e.PropertyName))
            return;

        if (e.PropertyName == nameof(DocumentDto.HasChanged))
            return;

        if (e.PropertyName == nameof(DocumentDto.HasErrors))
        {
            RefreshDocuments();
            return;
        }

        var isDuplicateFlagChange = e.PropertyName switch
        {
            nameof(DocumentDto.IsUniqueIdDuplicate) => nameof(DocumentDto.UniqueId),
            nameof(DocumentDto.IsTitleDuplicate) => nameof(DocumentDto.Title),
            nameof(DocumentDto.IsHrefDuplicate) => nameof(DocumentDto.Href),
            _ => null
        };

        if (isDuplicateFlagChange != null)
        {
            Observable.StartAsync(() => _validator.ValidateDtoAsync(documentDto, isDuplicateFlagChange));
            return;
        }

        if (e.PropertyName is not (
            nameof(DocumentDto.UniqueId) or
            nameof(DocumentDto.Title) or
            nameof(DocumentDto.Href)))
        {
            return;
        }

        Observable.StartAsync(async () =>
        {
            switch (e.PropertyName)
            {
                case nameof(DocumentDto.UniqueId):
                    MarkDuplicates();
                    await _validator.ValidateDtoAsync(documentDto, nameof(DocumentDto.UniqueId));
                    break;
                case nameof(DocumentDto.Title):
                    MarkDuplicates();
                    await _validator.ValidateDtoAsync(documentDto, nameof(DocumentDto.Title));
                    break;
                case nameof(DocumentDto.Href):
                    MarkDuplicates();
                    await _validator.ValidateDtoAsync(documentDto, nameof(DocumentDto.Href));
                    break;
                default:
                    return;
            }
        });

        documentDto.HasChanged = true;
        HasChanges = true;
    }

    private void RegisterDocumentDtoPropertyChanged(DocumentDto documentDto)
    {
        documentDto.PropertyChanged += DocumentDtoOnPropertyChanged;
    }

    private void UnregisterDocumentDtoPropertyChanged(DocumentDto documentDto)
    {
        documentDto.PropertyChanged -= DocumentDtoOnPropertyChanged;
    }

    private void MarkDuplicates()
    {
        foreach (var document in _allDocuments)
        {
            document.IsUniqueIdDuplicate = false;
            document.IsTitleDuplicate = false;
            document.IsHrefDuplicate = false;
        }

        _allDocuments.MarkDuplicates(
            document => document.UniqueId ?? string.Empty,
            (document, isDuplicate) => document.IsUniqueIdDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
        _allDocuments.MarkDuplicates(
            document => document.Title ?? string.Empty,
            (document, isDuplicate) => document.IsTitleDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
        _allDocuments.MarkDuplicates(
            document => document.Href ?? string.Empty,
            (document, isDuplicate) => document.IsHrefDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
    }

    [RelayCommand]
    private async Task ImportFromSettingsAsync()
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        var result = await _dialogHostService.ShowDialogAsync("ImportSettingDocumentsDialog", null);
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<List<int>>("TemplateDocumentIds", out var templateDocumentIds))
        {
            return;
        }

        var importedCount = await _documentService.ImportSettingDocumentsAsync(templateDocumentIds);
        if (importedCount == 0)
        {
            _messageService.Info("No selected documents are available for import.");
            return;
        }

        await LoadDocuments();
        _messageService.Success($"{importedCount} document(s) imported from settings successfully.");
    }

    [RelayCommand]
    private async Task AddDocumentAsync()
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        var result = await _dialogService.ShowAddDocumentModelAsync(
            new DocumentDto { ProjectId = _currentProjectService.CurrentProject.Id, CdiscDataType = _currentProjectService.CdiscDataType });
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<DocumentDto>("Model", out var document))
            return;

        await _documentService.InsertDocumentAsync(document);
        await _validator.ValidateDtoAsync(document);
        RegisterDocumentDtoPropertyChanged(document);
        _allDocuments.Add(document);
        MarkDuplicates();
        RefreshDocuments();
        //HasChanges = true;
        _messageService.Success("Document added successfully.");
    }

    [RelayCommand]
    private async Task EditDocumentAsync(DocumentDto documentDto)
    {
        var editedDocument = new DocumentDto
        {
            Id = documentDto.Id,
            ProjectId = documentDto.ProjectId,
            CdiscDataType = documentDto.CdiscDataType,
            UniqueId = documentDto.UniqueId,
            Title = documentDto.Title,
            Href = documentDto.Href,
        };

        var result = await _dialogService.ShowEditDocumentModelAsync(editedDocument);
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<DocumentDto>("Model", out var updatedDocument))
            return;

        await _documentService.UpdateDocumentAsync(updatedDocument);
        await _validator.ValidateDtoAsync(updatedDocument);
        UnregisterDocumentDtoPropertyChanged(documentDto);
        RegisterDocumentDtoPropertyChanged(updatedDocument);
        var index = _allDocuments.IndexOf(documentDto);
        if (index >= 0)
            _allDocuments[index] = updatedDocument;
        MarkDuplicates();
        RefreshDocuments();
        //HasChanges = true;
        _messageService.Success("Document updated successfully.");
    }

    [RelayCommand]
    private async Task DeleteAsync(DocumentDto documentDto)
    {
        var clearReferences = await _referenceDeletionService.ConfirmReferenceDeletionAsync(
            $"Delete document {documentDto.Title}?",
            "Document",
            await _documentService.ConfirmDocumentReferenceAsync(documentDto));
        if (clearReferences == null)
            return;

        await _documentService.DeleteDocumentDtoAsync(documentDto, clearReferences.Value);
        UnregisterDocumentDtoPropertyChanged(documentDto);
        _allDocuments.Remove(documentDto);
        MarkDuplicates();
        RefreshDocuments();
        //HasChanges = true;
        _messageService.Success("Document deleted successfully.");
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selectedDocuments = _allDocuments.Where(o => o.IsSelected).ToList();
        if (selectedDocuments.Count == 0)
        {
            _messageService.Info("Please select at least one document to delete.");
            return;
        }

        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Selected Documents" },
            { "Message", $"Are you sure you want to delete {selectedDocuments.Count} selected document(s)?" }
        });
        if (result.Result != DialogButtonResult.OK)
            return;

        foreach (var document in selectedDocuments)
        {
            await _documentService.DeleteDocumentDtoAsync(document);
            UnregisterDocumentDtoPropertyChanged(document);
            _allDocuments.Remove(document);
        }

        MarkDuplicates();
        RefreshDocuments();
        _messageService.Success($"{selectedDocuments.Count} document(s) deleted successfully.");
    }

    [RelayCommand]
    private async Task Save()
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        await _documentService.SaveDocumentsAsync(_allDocuments);
        HasChanges = false;
        _messageService.Success("Documents saved successfully.");
        await LoadDocuments();
    }

    [RelayCommand]
    private async Task Discard()
    {
        if (!HasChanges || _currentProjectService.CurrentProject == null)
            return;

        await LoadDocuments();
        HasChanges = false;
    }

    public override Task OnNavigatedToAsync(NavigationContext navigationContext)
    {
        return Task.CompletedTask;
    }

    public async Task LoadDataAsync()
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        await LoadDocuments();
    }

    public override Task OnNavigatedFromAsync(NavigationContext navigationContext)
    {
        foreach (var documentDto in _allDocuments)
            UnregisterDocumentDtoPropertyChanged(documentDto);

        return Task.CompletedTask;
    }

    public override void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }

    public async Task LoadDocuments()
    {
        foreach (var documentDto in _allDocuments)
            UnregisterDocumentDtoPropertyChanged(documentDto);

        var dtoList = await _documentService.GetAllDocumentDtosAsync();
        foreach (var document in dtoList)
        {
            await _validator.ValidateDtoAsync(document);
            RegisterDocumentDtoPropertyChanged(document);
        }

        _allDocuments.Clear();
        _allDocuments.AddRange(dtoList);
        MarkDuplicates();
        RefreshDocuments();
        HasChanges = false;
    }

    // private async Task ValidateDocumentDto(DocumentDto documentDto)
    // {
    //     documentDto.ClearErrors();
    //     var result = await _validator.ValidateAsync(documentDto);
    //     foreach (var validationFailure in result.Errors)
    //     {
    //         documentDto.SetError(validationFailure.PropertyName,
    //             new Avalonia.Controls.DataGridValidationResult(validationFailure.ErrorMessage,
    //                 validationFailure.Severity == Severity.Error
    //                     ? Avalonia.Controls.DataGridValidationSeverity.Error
    //                     : Avalonia.Controls.DataGridValidationSeverity.Warning));
    //     }
    // }

}

public record DocumentAutoCompleteOption : AutoCompleteOption
{
    public Document? Document { get; set; }
}
