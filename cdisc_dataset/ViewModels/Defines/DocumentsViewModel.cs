using System;
using System.Collections.ObjectModel;
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
using cdisc_dataset.Services.Interface;
using cdisc_dataset.Validations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentValidation;
using Prism.Navigation.Regions;

namespace cdisc_dataset.ViewModels.Defines;

[RegionMemberLifetime(KeepAlive = false)]
public partial class DocumentsViewModel : ConfirmNavigationViewModelBase
{
    private readonly IMessageService _messageService;
    private readonly IDocumentService _documentService;
    private readonly IIssueService _issueService;
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
        IValidator<DocumentDto> validator)
    {
        _messageService = messageService;
        _documentService = documentService;
        _issueService = issueService;
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

        _documentSourceCache.Connect()
            .WhenAnyPropertyChanged()
            .Subscribe(_ =>
            {
                if (!HasChanges)
                    HasChanges = true;
            });

        _documentSourceCache.Connect()
            .WhenPropertyChanged(o => o.UniqueId, false)
            .Subscribe(change =>
            {
                var changeSender = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender,"UniqueId");
                    _documentSourceCache.AddOrUpdate(changeSender);
                });
                MarkDuplicates();
            });

        _documentSourceCache.Connect()
            .WhenPropertyChanged(o => o.Title, false)
            .Subscribe(change =>
            {
                var changeSender = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender,"Title");
                    _documentSourceCache.AddOrUpdate(changeSender);
                });
                MarkDuplicates();
            });
        
        _documentSourceCache.Connect()
            .WhenPropertyChanged(o => o.Href, false)
            .Subscribe(change =>
            {
                var changeSender = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender,"Href");
                    _documentSourceCache.AddOrUpdate(changeSender);
                });
            });

        _documentSourceCache.Connect()
            .WhenPropertyChanged(o => o.HasUniqueIdDuplicate, false)
            .Subscribe(change =>
            {
                var changeSender = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender,"UniqueId");
                    _documentSourceCache.AddOrUpdate(changeSender);
                });
            });

        _documentSourceCache.Connect()
            .WhenPropertyChanged(o => o.HasTitleDuplicate, false)
            .Subscribe(change =>
            {
                var changeSender = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(changeSender,"Title");
                    _documentSourceCache.AddOrUpdate(changeSender);
                });
            });
    }

    [RelayCommand]
    private async Task AddDocument()
    {
        if (CurrentProject == null)
            return;

        var dto = new DocumentDto()
        {
            ProjectId = CurrentProject.Id,
            CdiscDataType = CdiscDataType
        };
        
        await _validator.ValidateDtoAsync(dto);
        _documentSourceCache.AddOrUpdate(dto);
        MarkDuplicates();
        HasChanges = true;
        _messageService.Success("添加成功");
    }

    [RelayCommand]
    private async Task Delete(DocumentDto documentDto)
    {
        await _documentService.DeleteDocumentDtoAsync(documentDto);
        _documentSourceCache.Remove(documentDto);
        MarkDuplicates();
        _messageService.Success("删除成功");
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

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        var navigationContextParameters = navigationContext.Parameters;
        navigationContextParameters.TryGetValue("CdiscDataType", out CdiscDataType cdiscDataType);
        navigationContextParameters.TryGetValue("CurrentProject", out Project? currentProject);
        CdiscDataType = cdiscDataType;
        CurrentProject = currentProject;
        if (CurrentProject != null)
        {
            LoadDocuments(CurrentProject.Id, CdiscDataType).Await();
        }
    }

    public override void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    public override void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }

    private async Task LoadDocuments(int id, CdiscDataType cdiscDataType)
    {
        var dtoList = await _documentService.GetAllDocumentDtosAsync();
        foreach (var document in dtoList)
        {
            await _validator.ValidateDtoAsync(document);
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
