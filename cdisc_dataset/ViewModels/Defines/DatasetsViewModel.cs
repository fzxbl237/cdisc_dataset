using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Controls;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using Avalonia.Controls;
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
using Prism.Navigation.Regions;

namespace cdisc_dataset.ViewModels.Defines;

[RegionMemberLifetime(KeepAlive = false)]
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

    private readonly SourceCache<DatasetDto, int> _sourceCache = new(o => o.Id);

    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private bool _hasChanges;
    [ObservableProperty] private CdiscDataType _cdiscDataType;
    [ObservableProperty] private bool _isLoading;

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

        var filter = this.WhenValueChanged(t => t.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select(BuildFilter);
        _sourceCache.Connect()
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _datasets, SortExpressionComparer<DatasetDto>.Ascending(o => o.Name ?? string.Empty))
            .DisposeMany()
            .Subscribe();

        _sourceCache.Connect().WhenAnyPropertyChanged().Subscribe(o =>
        {
            o?.HasChanged = true;
            HasChanges = true;
        });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Name, false)
            .Subscribe(change =>
            {
                var datasetDto = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(datasetDto, "Name");
                    await _validator.ValidateDtoAsync(datasetDto, "Standard");
                    _sourceCache.AddOrUpdate(datasetDto);
                    UpdateDuplicateFlags();
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Label, false)
            .Subscribe(change =>
            {
                var datasetDto = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(datasetDto, "Label");
                    _sourceCache.AddOrUpdate(datasetDto);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Class, false)
            .Subscribe(change =>
            {
                var datasetDto = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(datasetDto, "Class");
                    _sourceCache.AddOrUpdate(datasetDto);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.SubClass, false)
            .Subscribe(change =>
            {
                var datasetDto = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(datasetDto, "SubClass");
                    _sourceCache.AddOrUpdate(datasetDto);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Repeating, false)
            .Subscribe(change =>
            {
                var datasetDto = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(datasetDto, "Repeating");
                    _sourceCache.AddOrUpdate(datasetDto);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.CommentUniqueId, false)
            .Subscribe(change =>
            {
                var datasetDto = change.Sender;

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

                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(datasetDto, "CommentUniqueId");
                    _sourceCache.AddOrUpdate(datasetDto);
                });
            });
    }

    private void UpdateDuplicateFlags()
    {
        var all = _sourceCache.Items.ToList();
        var nameGroups = all.GroupBy(o => o.Name ?? string.Empty).ToList();
        foreach (var group in nameGroups)
        {
            var isDuplicate = group.Count() > 1;
            foreach (var dto in group)
            {
                if (dto.IsDuplicate != isDuplicate)
                {
                    dto.IsDuplicate = isDuplicate;
                    _sourceCache.AddOrUpdate(dto);
                }
            }
        }
    }

    private static Func<DatasetDto, bool> BuildFilter(string? searchText)
    {
        if (string.IsNullOrEmpty(searchText)) return _ => true;
        return o => Contains(searchText, o.Name)
                    || Contains(searchText, o.Label)
                    || Contains(searchText, o.Class)
                    || Contains(searchText, o.SubClass)
                    || Contains(searchText, o.Structure);
    }

    private static bool Contains(string? searchText, string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Contains(searchText!, StringComparison.OrdinalIgnoreCase);
    }

    public async Task LoadDatasets()
    {
        IsLoading = true;
        var list = await _datasetService.GetAllDatasetsAsync();
        foreach (var datasetDto in list)
        {
            await _validator.ValidateDtoAsync(datasetDto);
        }
        _sourceCache.Edit(o =>
        {
            o.Clear();
            o.Load(list);
        });
        UpdateDuplicateFlags();
        HasChanges = false;
        IsLoading = false;
    }

    public async Task LoadLookups()
    {
        if (_currentProjectService.CurrentProject == null) return;

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
    private async Task Delete(DatasetDto dataset)
    {
        await _datasetService.DeleteDatasetAsync(dataset);
        _sourceCache.Edit(o => o.Remove(dataset));
        _messageService.Success("删除成功");
    }

    [RelayCommand]
    private async Task Save()
    {
        if (!HasChanges) return;
        await _datasetService.SaveDatasetsAsync(_sourceCache.Items.Where(o => o.HasChanged).ToList());
        HasChanges = false;
        _messageService.Success("保存成功");
        await LoadDatasets();
    }

    [RelayCommand]
    private async Task Discard()
    {
        if (!HasChanges || _currentProjectService.CurrentProject == null) return;
        await LoadDatasets();
    }

    [RelayCommand]
    private async Task EditKeyVariables(DatasetDto dataset)
    {
        var dialogParameters = new DialogParameters { { "DatasetDto", dataset } };
        var result = await _dialogHostService.ShowDialog("EditKeyVariables", dialogParameters);
        if (result.Parameters.TryGetValue<string>("KeyVariables", out string? keyVariables))
        {
            dataset.KeyVariables = keyVariables;
            _sourceCache.AddOrUpdate(dataset);
        }
    }

    [RelayCommand]
    private async Task AddComment(DatasetDto dataset)
    {
        var dialogParameters = new DialogParameters
        {
            { "Title", "Add Comment" },
            { "DefaultId", $"COM.{dataset.Name}" }
        };
        var result = await _dialogHostService.ShowDialog("CommentDialog", dialogParameters);
        if (result.Parameters.TryGetValue<CommentDto>("Model", out CommentDto? comment))
        {
            var entity = await _commentService.InsertCommentAsync(comment);
            dataset.Comment = _mapper.Map<Comment>(entity);
            dataset.CommentId = entity.Id;
            dataset.CommentUniqueId = entity.UniqueId;
            _sourceCache.AddOrUpdate(dataset);
            await _datasetService.UpdateDatasetAsync(dataset);
            _messageService.Success("Comment添加成功");
        }
    }

    [RelayCommand]
    private async Task ModifyComment(DatasetDto dataset)
    {
        if (dataset.Comment == null) return;
        var commentDto = _mapper.Map<CommentDto>(dataset.Comment);
        var dialogParameters = new DialogParameters
        {
            { "Title", "Modify Comment" },
            { "Model", commentDto }
        };
        var result = await _dialogHostService.ShowDialog("CommentDialog", dialogParameters);
        if (result.Parameters.TryGetValue<CommentDto>("Model", out CommentDto? model))
        {
            var entity = await _commentService.UpdateCommentAsync(model);
            dataset.Comment = entity;
            dataset.CommentId = entity.Id;
            dataset.CommentUniqueId = entity.UniqueId;
            _sourceCache.AddOrUpdate(dataset);
            await _datasetService.UpdateDatasetAsync(dataset);
            _messageService.Success("Comment更新成功");
        }
    }

    [RelayCommand]
    private async Task AddDataset()
    {
        if (_currentProjectService.CurrentProject == null) return;

        var result = await _dialogHostService.ShowDialog("DatasetDialog", new DialogParameters());
        if (result.Result != ButtonResult.Yes ||
            !result.Parameters.TryGetValue<List<Dataset>>("Datasets", out var datasets) ||
            datasets.Count == 0)
        {
            return;
        }

        foreach (var dataset in datasets)
        {
            dataset.ProjectId = _currentProjectService.CurrentProject.Id;
            dataset.CdiscDataType = _currentProjectService.CdiscDataType;
        }

        await _datasetService.InsertDatasetsWithVariablesAsync(datasets);
        await LoadDatasets();
        _messageService.Success("Datasets添加成功");
    }

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        var navigationContextParameters = navigationContext.Parameters;
        navigationContextParameters.TryGetValue("CdiscDataType", out CdiscDataType cdiscDataType);
        CdiscDataType = cdiscDataType;

        if (CdiscDataType == CdiscDataType.Sdtm)
        {
            Classes.Clear();
            Standards.Clear();
            Classes.AddRange([.. ConstantOptions.Classes]);
            Standards.AddRange([.. ConstantOptions.SdtmStandards]);
        }

    }

    public async Task LoadDataAsync()
    {
        if (_currentProjectService.CurrentProject == null)
            return;

        await LoadLookups();
        await LoadDatasets();
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