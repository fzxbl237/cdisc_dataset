using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using cdisc_dataset.Constants;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentValidation;
using MapsterMapper;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;
using NavigationContext = AsyncNavigation.NavigationContext;

namespace cdisc_dataset.ViewModels.Defines;

public partial class DatasetsViewModel : ConfirmNavigationViewModelBase
{
    private readonly IMessageService _messageService;
    private readonly IDatasetService _datasetService;
    private readonly ICommentService _commentService;
    private readonly IReferenceDeletionService _referenceDeletionService;
    private readonly IDialogHostService _dialogHostService;
    private readonly cdisc_dataset.Services.IDialogService _dialogService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IMapper _mapper;
    private readonly IValidator<DatasetDto> _validator;
    public AvaloniaList<string> Yns { get; set; } = ["Yes", "No"];
    public AvaloniaList<string> Classes { get; set; } = [];
    public AvaloniaList<string> Standards { get; set; } = [];
    public AvaloniaList<IAutoCompleteOption> CommentOptions { get; set; } = [];

    private FrozenDictionary<string, Comment>? _frozenCommentDictionary;

    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private bool _isErrorOnly;
    [ObservableProperty] private bool _hasChanges;
    [ObservableProperty] private bool _isInitialLoadCompleted;
    [ObservableProperty] private bool _showLoading = true;

    private readonly SourceCache<DatasetDto, int> _sourceCache = new(o => o.Id);
    private readonly ReadOnlyObservableCollection<DatasetDto> _datasets;
    public ReadOnlyObservableCollection<DatasetDto> Datasets => _datasets;

    public DatasetsViewModel(
        IMessageService messageService,
        IDatasetService datasetService,
        ICommentService commentService,
        IReferenceDeletionService referenceDeletionService,
        ICurrentProjectService currentProjectService,
        IDialogHostService dialogHostService,
        cdisc_dataset.Services.IDialogService dialogService,
        IMapper mapper,
        IValidator<DatasetDto> validator,
        ILookupStore lookupStore)
    {
        _messageService = messageService;
        _datasetService = datasetService;
        _commentService = commentService;
        _referenceDeletionService = referenceDeletionService;
        _currentProjectService = currentProjectService;
        _dialogHostService = dialogHostService;
        _dialogService = dialogService;
        _mapper = mapper;
        _validator = validator;

        _sourceCache.Connect()
            .AutoRefresh(o => o.HasErrors)
            .Filter(o => !IsErrorOnly || o.HasErrors)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _datasets, SortExpressionComparer<DatasetDto>.Ascending(o => o.Name ?? string.Empty))
            .DisposeMany()
            .Subscribe();

        lookupStore.Comments
            .ToCollection()
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .Subscribe(RebuildCommentLookups);
    }

    public async Task LoadInitialDataAsync() {
        if (IsInitialLoadCompleted)
        {
            return;
        }

        ShowLoading = true;
        try
        {
            await LoadDatasetsAsync();
            IsInitialLoadCompleted = true;
        }
        finally
        {
            ShowLoading = false;
        }
    }

    partial void OnIsErrorOnlyChanged(bool value) => _sourceCache.Refresh();

    private void DatasetDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DatasetDto datasetDto)
            return;

        var isEditable = IsEditableProperty(e.PropertyName);
        if (!isEditable)
            return;

        HandleDatasetDtoPropertyChangedAsync(datasetDto, e.PropertyName).AwaitWithOpt();
        datasetDto.HasChanged = true;
        HasChanges = true;
    }

    private static bool IsEditableProperty(string? propertyName)
    {
        return propertyName is nameof(DatasetDto.Name)
            or nameof(DatasetDto.Label)
            or nameof(DatasetDto.Class)
            or nameof(DatasetDto.SubClass)
            or nameof(DatasetDto.Structure)
            or nameof(DatasetDto.KeyVariables)
            or nameof(DatasetDto.Standard)
            or nameof(DatasetDto.HasNoData)
            or nameof(DatasetDto.Repeating)
            or nameof(DatasetDto.ReferenceData)
            or nameof(DatasetDto.CommentUniqueId)
            or nameof(DatasetDto.DeveloperNotes);
    }

    private async Task HandleDatasetDtoPropertyChangedAsync(DatasetDto datasetDto, string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(DatasetDto.Name):
                MarkDuplicates();
                await _validator.ValidateDtoAsync(datasetDto, nameof(DatasetDto.Name), nameof(DatasetDto.Standard));
                break;
            case nameof(DatasetDto.Standard):
                await _validator.ValidateDtoAsync(datasetDto, nameof(DatasetDto.Name));
                break;
            case nameof(DatasetDto.Label):
                await _validator.ValidateDtoAsync(datasetDto, nameof(DatasetDto.Label));
                break;
            case nameof(DatasetDto.Class):
                await _validator.ValidateDtoAsync(datasetDto, nameof(DatasetDto.Class));
                break;
            case nameof(DatasetDto.SubClass):
                await _validator.ValidateDtoAsync(datasetDto, nameof(DatasetDto.SubClass));
                break;
            case nameof(DatasetDto.Repeating):
                await _validator.ValidateDtoAsync(datasetDto, nameof(DatasetDto.Repeating));
                break;
            case nameof(DatasetDto.CommentUniqueId):
                HandleCommentUniqueIdChanged(datasetDto);
                await _validator.ValidateDtoAsync(datasetDto, nameof(DatasetDto.CommentUniqueId));
                break;
        }
    }

    private void HandleCommentUniqueIdChanged(DatasetDto datasetDto)
    {
        if (_frozenCommentDictionary != null &&
            _frozenCommentDictionary.TryGetValue(datasetDto.CommentUniqueId ?? string.Empty, out var comment))
        {
            datasetDto.Comment = comment;
            datasetDto.CommentId = comment.Id;
        }
        else
        {
            datasetDto.Comment = null;
            datasetDto.CommentId = 0;
        }
    }

    private void RegisterDatasetDtoPropertyChanged(DatasetDto datasetDto)
    {
        datasetDto.PropertyChanged += DatasetDtoOnPropertyChanged;
    }

    private void UnregisterDatasetDtoPropertyChanged(DatasetDto datasetDto)
    {
        datasetDto.PropertyChanged -= DatasetDtoOnPropertyChanged;
    }

    private void MarkDuplicates()
    {
        _sourceCache.Items.MarkDuplicates(
            dataset => dataset.Name ?? string.Empty,
            (dataset, isDuplicate) => dataset.IsDuplicate = isDuplicate,
            key => !string.IsNullOrWhiteSpace(key));
    }

    public async Task LoadDatasetsAsync()
    {
        foreach (var datasetDto in Datasets)
            UnregisterDatasetDtoPropertyChanged(datasetDto);

        var list = await _datasetService.GetAllDatasetsAsync();
        foreach (var datasetDto in list)
        {
            await _validator.ValidateDtoAsync(datasetDto);
            RegisterDatasetDtoPropertyChanged(datasetDto);
        }

        _sourceCache.Edit(cache =>
        {
            cache.Clear();
            cache.AddOrUpdate(list);
        });
        MarkDuplicates();
        HasChanges = false;
    }

    private void RebuildCommentLookups(IReadOnlyCollection<Comment> comments)
    {
        var validComments = comments
            .Where(o => !o.HasErrors && !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToList();

        _frozenCommentDictionary = validComments
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);

        CommentOptions.Clear();
        CommentOptions.AddRange(validComments.Select(o => new DatasetAutoCompleteOption
        {
            Header = $"{o.UniqueId} {o.Description}",
            Content = o.UniqueId,
            Comment = o
        }));
    }

    [RelayCommand]
    private async Task GenerateSuppAsync(DatasetDto dataset)
    {
        if (_currentProjectService.CurrentProject == null || !dataset.CanGenerateSupp)
            return;

        var suppName = $"SUPP{dataset.Name}";
        
        if (await _datasetService.GetDatasetByName(suppName) != null)
        {
            _messageService.Error($"Dataset {suppName} already exists.");
            return;
        }

        var suppDataset = await _datasetService.GetSettingDatasetWithVariablesByNameAsync("SUPPQUAL");
        if (suppDataset == null)
        {
            _messageService.Error("SUPPQUAL template was not found in settings.");
            return;
        }

        var suppLabel = $"{suppDataset.Label} of {dataset.Name}";

        var projectId = _currentProjectService.CurrentProject.Id;
        suppDataset.Id = 0;
        suppDataset.Name = suppName;
        suppDataset.Label = suppLabel;
        suppDataset.ProjectId = projectId;
        suppDataset.CdiscDataType = _currentProjectService.CdiscDataType;
        suppDataset.Variables = suppDataset.Variables?
            .Where(variable => variable.Core is "Required" or "Expected" or "Permissible")
            .ToList() ?? [];

        foreach (var variable in suppDataset.Variables)
        {
            variable.Id = 0;
            variable.DatasetId = 0;
            variable.DatasetName = suppName;
            variable.ProjectId = projectId;
            variable.CdiscDataType = _currentProjectService.CdiscDataType;
        }

        await _datasetService.InsertDatasetsWithVariablesAsync([suppDataset]);
        await LoadDatasetsAsync();
        _messageService.Success($"Dataset {suppName} generated successfully.");
    }

    [RelayCommand]
    private async Task DeleteAsync(DatasetDto dataset)
    {
        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Dataset" },
            { "Message", $"Are you sure you want to delete dataset {dataset.Name}?" }
        });
        if (result.Result != DialogButtonResult.OK)
            return;

        UnregisterDatasetDtoPropertyChanged(dataset);
        await _datasetService.DeleteDatasetAsync(dataset);
        _sourceCache.Remove(dataset);
        MarkDuplicates();
        HasChanges = true;
        _messageService.Success("Dataset deleted successfully.");
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selectedDatasets = _sourceCache.Items.Where(o => o.IsSelected).ToList();
        if (selectedDatasets.Count == 0)
        {
            _messageService.Info("Please select at least one dataset to delete.");
            return;
        }

        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Selected Datasets" },
            { "Message", $"Are you sure you want to delete {selectedDatasets.Count} selected dataset(s)?" }
        });
        if (result.Result != DialogButtonResult.OK)
            return;

        foreach (var dataset in selectedDatasets)
        {
            UnregisterDatasetDtoPropertyChanged(dataset);
            await _datasetService.DeleteDatasetAsync(dataset);
        }

        _sourceCache.Remove(selectedDatasets);
        MarkDuplicates();
        HasChanges = true;
        _messageService.Success($"{selectedDatasets.Count} dataset(s) deleted successfully.");
    }

    [RelayCommand]
    private async Task Save()
    {
        if (!HasChanges) return;
        await _datasetService.SaveDatasetsAsync(_sourceCache.Items.Where(dataset => dataset.HasChanged).ToList());
        HasChanges = false;
        _messageService.Success("Datasets saved successfully.");
        await LoadDatasetsAsync();
    }

    [RelayCommand]
    private async Task Discard()
    {
        if (!HasChanges || _currentProjectService.CurrentProject == null) return;

        ShowLoading = true;
        try
        {
            await LoadDatasetsAsync();
            await Task.Delay(250);
        }
        finally
        {
            ShowLoading = false;
        }
    }

    [RelayCommand]
    private async Task EditKeyVariablesAsync(DatasetDto dataset)
    {
        var dialogParameters = new DialogParameters { { "DatasetDto", dataset } };
        var result = await _dialogHostService.ShowDialogAsync("EditKeyVariables", dialogParameters);
        if (result.Parameters.TryGetValue<string>("KeyVariables", out string? keyVariables))
        {
            dataset.KeyVariables = keyVariables;
        }
    }

    [RelayCommand]
    private async Task AddCommentAsync(DatasetDto dataset)
    {
        var result = await _dialogService.ShowAddCommentModelAsync($"COM.{dataset.Name}");
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<CommentDto>("Model", out var comment))
            return;

        var entity = await _commentService.InsertCommentAsync(comment);
        dataset.CommentUniqueId = entity.UniqueId;
        dataset.Comment = _mapper.Map<Comment>(entity);
        dataset.CommentId = entity.Id;
        await _datasetService.UpdateDatasetAsync(dataset);
        _messageService.Success("Comment added successfully.");
    }

    [RelayCommand]
    private async Task EditCommentAsync(DatasetDto dataset)
    {
        if (dataset.Comment == null)
            return;

        var result = await _dialogService.ShowEditCommentModelAsync(_mapper.Map<CommentDto>(dataset.Comment));
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<CommentDto>("Model", out var model))
            return;

        var entity = await _commentService.UpdateCommentAsync(model);
        dataset.Comment = entity;
        dataset.CommentId = entity.Id;
        dataset.CommentUniqueId = entity.UniqueId;
        await _datasetService.UpdateDatasetAsync(dataset);
        _messageService.Success("Comment updated successfully.");
    }

    [RelayCommand]
    private async Task DeleteCommentAsync(DatasetDto dataset)
    {
        if (dataset.Comment == null || !await _referenceDeletionService.ConfirmAndDeleteCommentAsync(dataset.Comment))
            return;

        var affectedDatasets = _sourceCache.Items
            .Where(item => item.CommentId == dataset.Comment.Id)
            .ToList();
        foreach (var affectedDataset in affectedDatasets)
        {
            affectedDataset.Comment = null;
            affectedDataset.CommentId = 0;
            affectedDataset.CommentUniqueId = string.Empty;
        }
        _sourceCache.Edit(cache => cache.AddOrUpdate(affectedDatasets));
        _messageService.Success("Comment deleted successfully.");
    }

    [RelayCommand]
    private async Task ImportSettingDatasetsAsync()
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        var result = await _dialogHostService.ShowDialogAsync("ImportSettingDatasetsDialog", null);
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<List<string>>("DatasetNames", out var selectedNames))
            return;

        var existingNames = (await _datasetService.GetDatasetNamesAsync())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var datasets = (await _datasetService.GetSettingDatasetsWithVariablesByNamesAsync(selectedNames))
            .Where(dataset => !string.IsNullOrWhiteSpace(dataset.Name) && !existingNames.Contains(dataset.Name))
            .ToList();
        if (datasets.Count == 0)
        {
            _messageService.Error("The selected datasets already exist in the current project.");
            return;
        }

        var projectId = _currentProjectService.CurrentProject.Id;
        foreach (var dataset in datasets)
        {
            dataset.Id = 0;
            dataset.ProjectId = projectId;
            dataset.CdiscDataType = _currentProjectService.CdiscDataType;
            dataset.Variables = dataset.Variables?
                .Where(variable => variable.Core is "Required" or "Expected" or "Permissible")
                .ToList() ?? [];

            foreach (var variable in dataset.Variables)
            {
                variable.Id = 0;
                variable.DatasetId = 0;
                variable.DatasetName = dataset.Name;
                variable.ProjectId = projectId;
                variable.CdiscDataType = _currentProjectService.CdiscDataType;
            }
        }

        await _datasetService.InsertDatasetsWithVariablesAsync(datasets);
        await LoadDatasetsAsync();
        _messageService.Success($"{datasets.Count} dataset(s) imported successfully.");
    }

    public override async Task OnNavigatedFromAsync(NavigationContext navigationContext)
    {
        await base.OnNavigatedFromAsync(navigationContext);

        foreach (var datasetDto in Datasets)
            UnregisterDatasetDtoPropertyChanged(datasetDto);
    }

    public override async Task OnNavigatedToAsync(NavigationContext navigationContext)
    {
        if (_currentProjectService.CdiscDataType == CdiscDataType.Sdtm)
        {
            if (!Classes.SequenceEqual(ConstantOptions.Classes))
            {
                Classes.Clear();
                Classes.AddRange([.. ConstantOptions.Classes]);
            }

            if (!Standards.SequenceEqual(ConstantOptions.SdtmStandards))
            {
                Standards.Clear();
                Standards.AddRange([.. ConstantOptions.SdtmStandards]);
            }
        }
        if (IsInitialLoadCompleted)
        {
            foreach (var datasetDto in Datasets)
                RegisterDatasetDtoPropertyChanged(datasetDto);
        }

        // return Task.CompletedTask;
    }

    public override void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }
}

public record DatasetAutoCompleteOption : AutoCompleteOption
{
    public Comment? Comment { get; set; }
}
