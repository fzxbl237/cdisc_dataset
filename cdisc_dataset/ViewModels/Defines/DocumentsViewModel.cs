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
using Prism.Dialogs;
using NavigationContext = AsyncNavigation.NavigationContext;

namespace cdisc_dataset.ViewModels.Defines;

public partial class DocumentsViewModel : ConfirmNavigationViewModelBase
{
    private readonly IMessageService _messageService;
    private readonly IDocumentService _documentService;
    private readonly IIssueService _issueService;
    private readonly IDialogHostService _dialogHostService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IValidator<DocumentDto> _validator;

    [ObservableProperty]
    private Project? _currentProject;

    [ObservableProperty]
    private CdiscDataType _cdiscDataType;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private string? _searchText;

    public AvaloniaList<DocumentDto> Documents { get; } = [];

    public DocumentsViewModel(
        IMessageService messageService,
        IDocumentService documentService,
        IIssueService issueService,
        IDialogHostService dialogHostService,
        ICurrentProjectService currentProjectService,
        IValidator<DocumentDto> validator)
    {
        _messageService = messageService;
        _documentService = documentService;
        _issueService = issueService;
        _dialogHostService = dialogHostService;
        _currentProjectService = currentProjectService;
        _validator = validator;

    }

    private void DocumentDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DocumentDto documentDto || string.IsNullOrWhiteSpace(e.PropertyName))
            return;

        if (e.PropertyName == nameof(DocumentDto.HasChanged))
            return;

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
        foreach (var document in Documents)
        {
            document.IsUniqueIdDuplicate = false;
            document.IsTitleDuplicate = false;
            document.IsHrefDuplicate = false;
        }

        Documents.MarkDuplicates(
            document => document.UniqueId ?? string.Empty,
            (document, isDuplicate) => document.IsUniqueIdDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
        Documents.MarkDuplicates(
            document => document.Title ?? string.Empty,
            (document, isDuplicate) => document.IsTitleDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
        Documents.MarkDuplicates(
            document => document.Href ?? string.Empty,
            (document, isDuplicate) => document.IsHrefDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
    }

    [RelayCommand]
    private async Task ImportFromSettingsAsync()
    {
        if (CurrentProject == null)
            return;

        var result = await _dialogHostService.ShowDialogAsync("ImportSettingDocumentsDialog", null);
        if (result.Result != ButtonResult.Yes ||
            !result.Parameters.TryGetValue<List<int>>("TemplateDocumentIds", out var templateDocumentIds))
        {
            return;
        }

        var importedCount = await _documentService.ImportSettingDocumentsAsync(templateDocumentIds);
        if (importedCount == 0)
        {
            _messageService.Info("No selected documents are available for import");
            return;
        }

        await LoadDocuments(CurrentProject.Id, CdiscDataType);
        _messageService.Success($"Imported {importedCount} document(s) from settings");
    }

    [RelayCommand]
    private async Task AddDocumentAsync()
    {
        if (CurrentProject == null)
            return;

        var result = await _dialogHostService.ShowDialogAsync("DocumentDialog", new DialogParameters
        {
            { "Title", "Add Document" },
            { "Model", new DocumentDto { ProjectId = CurrentProject.Id, CdiscDataType = CdiscDataType } }
        });
        if (result.Result != ButtonResult.Yes || !result.Parameters.ContainsKey("Model"))
            return;

        var document = result.Parameters.GetValue<DocumentDto>("Model");
        await _documentService.InsertDocumentAsync(document);
        await _validator.ValidateDtoAsync(document);
        RegisterDocumentDtoPropertyChanged(document);
        Documents.Add(document);
        MarkDuplicates();
        //HasChanges = true;
        _messageService.Success("Document added");
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

        var result = await _dialogHostService.ShowDialogAsync("DocumentDialog", new DialogParameters
        {
            { "Title", "Edit Document" },
            { "Model", editedDocument }
        });
        if (result.Result != ButtonResult.Yes || !result.Parameters.ContainsKey("Model"))
            return;

        var updatedDocument = result.Parameters.GetValue<DocumentDto>("Model");
        await _documentService.UpdateDocumentAsync(updatedDocument);
        await _validator.ValidateDtoAsync(updatedDocument);
        UnregisterDocumentDtoPropertyChanged(documentDto);
        RegisterDocumentDtoPropertyChanged(updatedDocument);
        var index = Documents.IndexOf(documentDto);
        if (index >= 0)
            Documents[index] = updatedDocument;
        MarkDuplicates();
        //HasChanges = true;
        _messageService.Success("Document updated");
    }

    [RelayCommand]
    private async Task DeleteAsync(DocumentDto documentDto)
    {
        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Document" },
            { "Message", $"Are you sure you want to delete document {documentDto.Title}?" }
        });
        if (result.Result != ButtonResult.OK)
            return;

        await _documentService.DeleteDocumentDtoAsync(documentDto);
        UnregisterDocumentDtoPropertyChanged(documentDto);
        Documents.Remove(documentDto);
        MarkDuplicates();
        //HasChanges = true;
        _messageService.Success("Delete successfully");
    }

    [RelayCommand]
    private async Task Save()
    {
        if (CurrentProject == null)
            return;

        await _documentService.SaveDocumentsAsync(Documents.ToList());
        HasChanges = false;
        _messageService.Success("Documents Save Success");
        await LoadDocuments(CurrentProject.Id, CdiscDataType);
    }

    [RelayCommand]
    private async Task Discard()
    {
        if (!HasChanges || CurrentProject == null)
            return;

        await LoadDocuments(CurrentProject.Id, CdiscDataType);
        HasChanges = false;
    }

    public override Task OnNavigatedToAsync(NavigationContext navigationContext)
    {
        CdiscDataType = _currentProjectService.CdiscDataType;
        CurrentProject = _currentProjectService.CurrentProject;
        return Task.CompletedTask;
    }

    public async Task LoadDataAsync()
    {
        if (CurrentProject == null)
            return;

        await LoadDocuments(CurrentProject.Id, CdiscDataType);
    }

    public override Task OnNavigatedFromAsync(NavigationContext navigationContext)
    {
        foreach (var documentDto in Documents)
            UnregisterDocumentDtoPropertyChanged(documentDto);

        return Task.CompletedTask;
    }

    public override void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }

    public async Task LoadDocuments(int id, CdiscDataType cdiscDataType)
    {
        foreach (var documentDto in Documents)
            UnregisterDocumentDtoPropertyChanged(documentDto);

        var dtoList = await _documentService.GetAllDocumentDtosAsync();
        foreach (var document in dtoList)
        {
            await _validator.ValidateDtoAsync(document);
            RegisterDocumentDtoPropertyChanged(document);
        }

        Documents.Clear();
        Documents.AddRange(dtoList.OrderBy(document => document.UniqueId, StringComparer.OrdinalIgnoreCase));
        MarkDuplicates();
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
