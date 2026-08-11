using AsyncNavigation;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using cdisc_dataset.Validations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using Prism.Dialogs;
using DynamicData.Binding;
using FluentValidation;
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

    private readonly SourceCache<DocumentDto, int> _documentSourceCache = new(o => o.Id);

    private readonly ReadOnlyObservableCollection<DocumentDto> _documents;
    public ReadOnlyObservableCollection<DocumentDto> Documents => _documents;

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

        var filter = this.WhenValueChanged(t => t.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select(BuildFilter);

        _documentSourceCache.Connect()
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _documents, SortExpressionComparer<DocumentDto>.Ascending(o => o.UniqueId))
            .DisposeMany()
            .Subscribe();

    }

    private void DocumentDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DocumentDto documentDto || string.IsNullOrWhiteSpace(e.PropertyName))
            return;

        if (e.PropertyName == nameof(DocumentDto.HasChanged))
            return;

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
                    await _validator.ValidateDtoAsync(documentDto, nameof(DocumentDto.Href));
                    break;
                case nameof(DocumentDto.HasUniqueIdDuplicate):
                    await _validator.ValidateDtoAsync(documentDto, nameof(DocumentDto.UniqueId));
                    break;
                case nameof(DocumentDto.HasTitleDuplicate):
                    await _validator.ValidateDtoAsync(documentDto, nameof(DocumentDto.Title));
                    break;
                default:
                    return;
            }

            _documentSourceCache.AddOrUpdate(documentDto);
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

    [RelayCommand]
    private async Task AddDocument()
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
        await _validator.ValidateDtoAsync(document);
        RegisterDocumentDtoPropertyChanged(document);
        _documentSourceCache.AddOrUpdate(document);
        MarkDuplicates();
        HasChanges = true;
        _messageService.Success("Document added");
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
        _documentSourceCache.Remove(documentDto);
        MarkDuplicates();
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
        foreach (var documentDto in _documentSourceCache.Items)
            UnregisterDocumentDtoPropertyChanged(documentDto);

        return Task.CompletedTask;
    }

    public override void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }

    public async Task LoadDocuments(int id, CdiscDataType cdiscDataType)
    {
        foreach (var documentDto in _documentSourceCache.Items)
            UnregisterDocumentDtoPropertyChanged(documentDto);

        var dtoList = await _documentService.GetAllDocumentDtosAsync();
        foreach (var document in dtoList)
        {
            await _validator.ValidateDtoAsync(document);
            RegisterDocumentDtoPropertyChanged(document);
        }

        _documentSourceCache.Edit(o =>
        {
            o.Clear();
            o.AddOrUpdate(dtoList);
        });

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

    private void MarkDuplicates()
    {
        _documentSourceCache.Items.MarkDuplicates(
            o => o.UniqueId ?? string.Empty,
            (document, isDuplicate) => document.HasUniqueIdDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));

        _documentSourceCache.Items.MarkDuplicates(
            o => o.Title ?? string.Empty,
            (document, isDuplicate) => document.HasTitleDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
    }
    

    private static Func<DocumentDto, bool> BuildFilter(string? searchText)
        => SearchFilterExtensions.BuildSearchFilter<DocumentDto>(
            searchText,
            x => x.UniqueId,
            x => x.Title,
            x => x.Href);
    
    
}

public record DocumentAutoCompleteOption : AutoCompleteOption
{
    public Document? Document { get; set; }
}
