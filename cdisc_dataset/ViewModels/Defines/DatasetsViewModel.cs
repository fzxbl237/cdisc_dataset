using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
using Prism.Dialogs;
using NavigationContext = AsyncNavigation.NavigationContext;

namespace cdisc_dataset.ViewModels.Defines;

public partial class DatasetsViewModel : ConfirmNavigationViewModelBase
{
    private readonly IMessageService _messageService;
    private readonly IDatasetService _datasetService;
    private readonly ICommentService _commentService;
    private readonly IDialogHostService _dialogHostService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IMapper _mapper;
    private readonly IValidator<DatasetDto> _validator;
    public AvaloniaList<string> Yns { get; set; } = ["Yes", "No"];
    public AvaloniaList<string> Classes { get; set; } = [];
    public AvaloniaList<string> Standards { get; set; } = [];
    public AvaloniaList<IAutoCompleteOption> CommentOptions { get; set; } = [];

    private FrozenDictionary<string, Comment>? _frozenCommentDictionary;

    [ObservableProperty] private string? _searchText;
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
        ICurrentProjectService currentProjectService,
        IDialogHostService dialogHostService,
        IMapper mapper,
        IValidator<DatasetDto> validator)
    {
        _messageService = messageService;
        _datasetService = datasetService;
        _commentService = commentService;
        _currentProjectService = currentProjectService;
        _dialogHostService = dialogHostService;
        _mapper = mapper;
        _validator = validator;

        _sourceCache.Connect()
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _datasets, SortExpressionComparer<DatasetDto>.Ascending(o => o.Name ?? string.Empty))
            .DisposeMany()
            .Subscribe();
    }

    public async Task LoadInitialDataAsync()
    {
        if (IsInitialLoadCompleted)
        {
            return;
        }

        var totalSw = Stopwatch.StartNew();
        ShowLoading = true;
        try
        {
            await LoadDatasetsAsync();
            IsInitialLoadCompleted = true;
        }
        finally
        {
            ShowLoading = false;
            totalSw.Stop();
            Debug.WriteLine($"[PerfTrace] datasets-initial-load total={totalSw.ElapsedMilliseconds}ms");
        }
    }

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
        var totalSw = Stopwatch.StartNew();

        var unregisterSw = Stopwatch.StartNew();
        foreach (var datasetDto in Datasets)
            UnregisterDatasetDtoPropertyChanged(datasetDto);
        unregisterSw.Stop();

        var dbSw = Stopwatch.StartNew();
        var list = await _datasetService.GetAllDatasetsAsync();
        dbSw.Stop();

        var validateSw = Stopwatch.StartNew();
        long validateMaxMs = 0;
        foreach (var datasetDto in list)
        {
            var oneSw = Stopwatch.StartNew();
            await _validator.ValidateDtoAsync(datasetDto);
            oneSw.Stop();
            if (oneSw.ElapsedMilliseconds > validateMaxMs)
                validateMaxMs = oneSw.ElapsedMilliseconds;
            RegisterDatasetDtoPropertyChanged(datasetDto);
        }
        validateSw.Stop();

        var applySw = Stopwatch.StartNew();
        _sourceCache.Edit(cache =>
        {
            cache.Clear();
            cache.AddOrUpdate(list);
        });
        MarkDuplicates();
        HasChanges = false;
        applySw.Stop();

        totalSw.Stop();
        var avgMs = list.Count == 0 ? 0 : validateSw.ElapsedMilliseconds / list.Count;
        Debug.WriteLine($"[PerfTrace] datasets-load count={list.Count} unregister={unregisterSw.ElapsedMilliseconds}ms db={dbSw.ElapsedMilliseconds}ms validate={validateSw.ElapsedMilliseconds}ms avg={avgMs}ms max={validateMaxMs}ms apply={applySw.ElapsedMilliseconds}ms total={totalSw.ElapsedMilliseconds}ms");
    }

    public async Task LoadLookups()
    {
        if (_currentProjectService.CurrentProject == null)
        {
            return;
        }

        var comments = await _commentService.GetAllCommentsWithoutErorrAsync();

        _frozenCommentDictionary = comments
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .ToFrozenDictionary(o => o.UniqueId ?? string.Empty, o => o);

        CommentOptions.Clear();
        CommentOptions.AddRange(comments
            .Where(o => !string.IsNullOrWhiteSpace(o.UniqueId))
            .Select(o => new DatasetAutoCompleteOption
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
        if (result.Result != ButtonResult.OK)
            return;

        UnregisterDatasetDtoPropertyChanged(dataset);
        await _datasetService.DeleteDatasetAsync(dataset);
        _sourceCache.Remove(dataset);
        MarkDuplicates();
        HasChanges = true;
        _messageService.Success("Delete successfully.");
    }

    [RelayCommand]
    private async Task Save()
    {
        if (!HasChanges) return;
        await _datasetService.SaveDatasetsAsync(_sourceCache.Items.Where(dataset => dataset.HasChanged).ToList());
        HasChanges = false;
        _messageService.Success("Saved successfully.");
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
    private async Task EditKeyVariables(DatasetDto dataset)
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
        var dialogParameters = new DialogParameters
        {
            { "Title", "Add Comment" },
            { "DefaultId", $"COM.{dataset.Name}" }
        };
        var result = await _dialogHostService.ShowDialogAsync("CommentDialog", dialogParameters);
        if (result.Parameters.TryGetValue<CommentDto>("Model", out CommentDto? comment))
        {
            var entity = await _commentService.InsertCommentAsync(comment);
            dataset.CommentUniqueId = entity.UniqueId;
            dataset.Comment = _mapper.Map<Comment>(entity);
            dataset.CommentId = entity.Id;
            await _datasetService.UpdateDatasetAsync(dataset);
            await LoadLookups();
            _messageService.Success("Comment add successful");
        }
    }

    [RelayCommand]
    private async Task ModifyCommentAsync(DatasetDto dataset)
    {
        if (dataset.Comment == null) return;
        var commentDto = _mapper.Map<CommentDto>(dataset.Comment);
        var dialogParameters = new DialogParameters
        {
            { "Title", "Modify Comment" },
            { "Model", commentDto }
        };
        var result = await _dialogHostService.ShowDialogAsync("CommentDialog", dialogParameters);
        if (result.Parameters.TryGetValue<CommentDto>("Model", out CommentDto? model))
        {
            var entity = await _commentService.UpdateCommentAsync(model);
            dataset.Comment = entity;
            dataset.CommentId = entity.Id;
            dataset.CommentUniqueId = entity.UniqueId;
            await _datasetService.UpdateDatasetAsync(dataset);
            _messageService.Success("Comment modify successfully");
        }
    }

    [RelayCommand]
    private async Task ImportSettingDatasetsAsync()
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        var result = await _dialogHostService.ShowDialogAsync("ImportSettingDatasetsDialog", null);
        if (result.Result != ButtonResult.Yes ||
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
        _messageService.Success($"Imported {datasets.Count} dataset(s) successfully.");
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
        await LoadLookups();

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
