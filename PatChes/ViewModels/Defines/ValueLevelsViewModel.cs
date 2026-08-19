using AsyncNavigation;
using System;
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
using PatChes.Extensions;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using PatChes.Services;
using PatChes.Services.Interface;
using PatChes.Validations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using FluentValidation;
using MapsterMapper;
using Net.Pinnacle21.Define.Parser;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;
using NavigationContext = AsyncNavigation.NavigationContext;
using DataGridCellPointerPressedEventArgs = Avalonia.Controls.DataGridCellPointerPressedEventArgs;
using DataGridPreparingCellForEditEventArgs = Avalonia.Controls.DataGridPreparingCellForEditEventArgs;

namespace PatChes.ViewModels.Defines;

public partial class ValueLevelsViewModel : ConfirmNavigationViewModelBase
{
    private readonly IMessageService _messageService;
    private readonly IValueLevelService _valueLevelService;
    private readonly IDatasetService _datasetService;
    private readonly IVariableService _variableService;
    private readonly IDocumentService _documentService;
    private readonly ICommentService _commentService;
    private readonly IReferenceDeletionService _referenceDeletionService;
    private readonly IDialogHostService _dialogHostService;
    private readonly PatChes.Services.IDialogService _dialogService;
    private readonly ICurrentProjectService _currentProjectService;
    private readonly IMapper _mapper;
    private readonly IValidator<ValueLevelDto> _validator;

    [ObservableProperty]
    private bool _hasChanges;

    private bool _suppressChangeTracking;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private bool _isErrorOnly;

    [ObservableProperty] private AvaloniaList<IAutoCompleteOption> _datasetOptions = [];
    [ObservableProperty] private AvaloniaList<IAutoCompleteOption> _variableOptions = [];
    [ObservableProperty] private AvaloniaList<IAutoCompleteOption> _codeListOptions = [];
    [ObservableProperty] private AvaloniaList<IAutoCompleteOption> _methodOptions = [];
    [ObservableProperty] private AvaloniaList<IAutoCompleteOption> _commentOptions = [];
    
    public AvaloniaList<string> DataTypes { get; set; } = ["text", "integer", "float","datetime","date","time",
        "partialDate","partialTime","partialDateTime","incompleteDatetime","durationDatetime","intervalDatetime"];
    
    public AvaloniaList<string> Yns { get; set; } = ["Yes", "No"]; 
    
    public AvaloniaList<string> Sources {get;set;} = ["","Investigator","Subject"];
    public AvaloniaList<string> Origins { get; set; } = [];

    private readonly SourceCache<ValueLevelDto, int> _sourceCache = new(o => o.Id);
    private readonly ReadOnlyObservableCollection<ValueLevelDto> _valueLevels;
    public ReadOnlyObservableCollection<ValueLevelDto> ValueLevels => _valueLevels;

    public ValueLevelsViewModel(
        IMessageService messageService,
        IValueLevelService valueLevelService,
        IDatasetService datasetService,
        IVariableService variableService,
        IDocumentService documentService,
        ICommentService commentService,
        IReferenceDeletionService referenceDeletionService,
        IDialogHostService dialogHostService,
        PatChes.Services.IDialogService dialogService,
        ICurrentProjectService currentProjectService,
        IMapper mapper,
        IValidator<ValueLevelDto> validator,
        ILookupStore lookupStore)
    {
        _messageService = messageService;
        _valueLevelService = valueLevelService;
        _datasetService = datasetService;
        _variableService = variableService;
        _documentService = documentService;
        _commentService = commentService;
        _referenceDeletionService = referenceDeletionService;
        _dialogHostService = dialogHostService;
        _dialogService = dialogService;
        _currentProjectService = currentProjectService;
        _mapper = mapper;
        _validator = validator;

        var filter = this.WhenValueChanged(t => t.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select(_ => BuildFilter());

        _sourceCache.Connect()
            .AutoRefresh(o => o.HasErrors)
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _valueLevels, SortExpressionComparer<ValueLevelDto>.Ascending(o => o.Dataset ?? string.Empty)
                .ThenByAscending(o => o.Variable ?? string.Empty)
                .ThenByAscending(o=>o.Order))
            .DisposeMany()
            .Subscribe();

        lookupStore.Datasets
            .ToCollection()
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .Subscribe(RebuildDatasetOptions);

        lookupStore.CodeLists
            .ToCollection()
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .Subscribe(RebuildCodeListOptions);

        lookupStore.Methods
            .ToCollection()
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .Subscribe(RebuildMethodOptions);

        lookupStore.Comments
            .ToCollection()
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .Subscribe(RebuildCommentOptions);

    }

    private void ValueLevelDtoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ValueLevelDto valueLevelDto || string.IsNullOrWhiteSpace(e.PropertyName))
            return;

        if (e.PropertyName == nameof(ValueLevelDto.HasChanged))
            return;

        Observable.StartAsync(async () =>
        {
            switch (e.PropertyName)
            {
                case nameof(ValueLevelDto.WhereClause):
                {
                    valueLevelDto = await UpdateWhereClausesAsync(valueLevelDto);
                    valueLevelDto.IsWhereClauseEffective = valueLevelDto.WhereClauses?
                        .All(whereClauseDto => whereClauseDto.VariableEntity != null) ?? true;
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.WhereClause));
                    break;
                }
                case nameof(ValueLevelDto.Type):
                    valueLevelDto = await UpdateWhereClausesAsync(valueLevelDto);
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.Type));
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.Length));
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.Digits));
                    break;
                case nameof(ValueLevelDto.Dataset):
                {
                    var datasetEntity = await _datasetService.GetDatasetByName(valueLevelDto.Dataset);
                    valueLevelDto.DatasetEntity = datasetEntity;
                    valueLevelDto.DatasetId = datasetEntity?.Id ?? 0;
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.Dataset));
                    break;
                }
                case nameof(ValueLevelDto.Variable):
                {
                    var variableEntity = await _variableService.GetVariableByDatasetIdAndVariableNameWithoutError(
                        valueLevelDto.DatasetId, valueLevelDto.Variable);
                    valueLevelDto.VariableEntity = variableEntity;
                    valueLevelDto.VariableId = variableEntity?.Id ?? 0;
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.Variable));
                    break;
                }
                case nameof(ValueLevelDto.Label):
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.Label));
                    break;
                case nameof(ValueLevelDto.Origin):
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.Pages));
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.MethodUniqueId));
                    break;
                case nameof(ValueLevelDto.Pages):
                case nameof(ValueLevelDto.Source):
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.Pages));
                    break;
                case nameof(ValueLevelDto.Format):
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.Format));
                    break;
                case nameof(ValueLevelDto.MethodUniqueId):
                {
                    var methodOption = MethodOptions
                        .OfType<ValueLevelAutoCompleteOption>()
                        .FirstOrDefault(o => string.Equals((string?)o.Content, valueLevelDto.MethodUniqueId,
                            StringComparison.OrdinalIgnoreCase));
                    valueLevelDto.Method = methodOption?.Method;
                    valueLevelDto.MethodId = methodOption?.Method?.Id ?? 0;
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.MethodUniqueId));
                    break;
                }
                case nameof(ValueLevelDto.CommentUniqueId):
                {
                    var commentOption = CommentOptions
                        .OfType<ValueLevelAutoCompleteOption>()
                        .FirstOrDefault(o => string.Equals((string?)o.Content, valueLevelDto.CommentUniqueId,
                            StringComparison.OrdinalIgnoreCase));
                    valueLevelDto.Comment = commentOption?.Comment;
                    valueLevelDto.CommentId = commentOption?.Comment?.Id ?? 0;
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.CommentUniqueId));
                    break;
                }
                case nameof(ValueLevelDto.Length):
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.Length));
                    break;
                case nameof(ValueLevelDto.Digits):
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.Digits));
                    break;
                default:
                    return;
            }

            _sourceCache.AddOrUpdate(valueLevelDto);
        });

        if (!_suppressChangeTracking)
        {
            valueLevelDto.HasChanged = true;
            HasChanges = true;
        }
    }

    private void RegisterValueLevelDtoPropertyChanged(ValueLevelDto valueLevelDto)
    {
       valueLevelDto.PropertyChanged += ValueLevelDtoOnPropertyChanged;
    }

    private void UnregisterValueLevelDtoPropertyChanged(ValueLevelDto valueLevelDto)
    {
        valueLevelDto.PropertyChanged -= ValueLevelDtoOnPropertyChanged;
    }
    
    private async Task<ValueLevelDto> UpdateWhereClausesAsync(ValueLevelDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.WhereClause))
            return dto;

        var whereClauseParser = new WhereClauseParser();
        var orConjunction = whereClauseParser.Parse(dto.WhereClause);
        var andConjunction = orConjunction.Conjunctions.FirstOrDefault();
        AvaloniaList<WhereClauseDto> res = [];
        var datasetEntity = await _datasetService.GetDatasetByName(dto.Dataset);
        dto.DatasetEntity = datasetEntity;
        dto.DatasetId = datasetEntity?.Id ?? 0;

        if (andConjunction != null)
        {
            foreach (var andConjunctionComparison in andConjunction.Comparisons)
            {
                var identifier = andConjunctionComparison.Identifier;
                var variableEntity = datasetEntity == null
                    ? null
                    : await _variableService.GetVariableByDatasetIdAndVariableNameWithoutError(
                        datasetEntity.Id, identifier);

                res.Add(new WhereClauseDto
                {
                    Values = string.Join(", ", andConjunctionComparison.Values),
                    Comparator = andConjunctionComparison.Comparator.GetLiteral(),
                    Variable = identifier,
                    VariableEntity = variableEntity,
                    VariableId = variableEntity?.Id ?? 0
                });
            }
        }

        dto.WhereClauses = res;
        return dto;
    }

    [RelayCommand]
    private void AddValueLevel()
    {
        var currentProject = _currentProjectService.CurrentProject;
        if (currentProject == null)
            return;

        var valueLevel = new ValueLevelDto
        {
            ProjectId = currentProject.Id,
            CdiscDataType = _currentProjectService.CdiscDataType,
            Order = GetNextOrder()
        };

        RegisterValueLevelDtoPropertyChanged(valueLevel);
        _sourceCache.AddOrUpdate(valueLevel);
        HasChanges = true;
    }
    
    [RelayCommand]
    private async Task DeleteAsync(ValueLevelDto valueLevelDto)
    {
        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Value Level" },
            { "Message", $"Are you sure you want to delete value level {valueLevelDto.Dataset}/{valueLevelDto.Variable}?" }
        });
        if (result.Result != DialogButtonResult.OK)
            return;

        await _valueLevelService.DeleteValueLevelAsync(valueLevelDto);
        UnregisterValueLevelDtoPropertyChanged(valueLevelDto);
        _sourceCache.Remove(valueLevelDto);
        HasChanges = true;
        _messageService.Success("Value level deleted successfully.");
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selectedValueLevels = _sourceCache.Items.Where(o => o.IsSelected).ToList();
        if (selectedValueLevels.Count == 0)
        {
            _messageService.Info("Please select at least one value level to delete.");
            return;
        }

        var result = await _dialogHostService.ShowDialogAsync("ConfirmDialog", new DialogParameters
        {
            { "Title", "Delete Selected Value Levels" },
            { "Message", $"Are you sure you want to delete {selectedValueLevels.Count} selected value level(s)?" }
        });
        if (result.Result != DialogButtonResult.OK)
            return;

        foreach (var valueLevel in selectedValueLevels)
        {
            await _valueLevelService.DeleteValueLevelAsync(valueLevel);
            UnregisterValueLevelDtoPropertyChanged(valueLevel);
        }

        _sourceCache.Remove(selectedValueLevels);
        HasChanges = true;
        _messageService.Success($"{selectedValueLevels.Count} value level(s) deleted successfully.");
    }

    private int GetNextOrder()
    {
        return _sourceCache.Items.Any() ? _sourceCache.Items.Max(x => x.Order) + 1 : 1;
    }

    // [RelayCommand]
    // private void AddWhereClause(ValueLevelDto valueLevel)
    // {
    // }

    [RelayCommand]
    private async Task OpenWhereClauseEditor(ValueLevelDto valueLevel)
    {
        if (string.IsNullOrWhiteSpace(valueLevel.Dataset))
        {
            _messageService.Error("Dataset name is required before editing the WHERE clause.");
            return;
        }

        if (string.IsNullOrWhiteSpace(valueLevel.Variable))
        {
            _messageService.Error("Variable name is required before editing the WHERE clause.");
            return;
        }
        
        ValueLevelDto dto;
        _suppressChangeTracking = true;
        try
        {
            dto = await UpdateWhereClausesAsync(valueLevel);
        }
        finally
        {
            _suppressChangeTracking = false;
        }

        var dialogParameters = new DialogParameters
        {
            { "Title", "WhereClause Editor" },
            { "ValueLevel", dto },
            { "ValueLevelId", dto.Id },
            { "WhereClauses", dto.WhereClauses?.ToList() ?? [new WhereClauseDto()] }
        };

        var result = await _dialogHostService.ShowDialogAsync("WhereClauseEditorDialog", dialogParameters);
        if (result.Result != DialogButtonResult.Yes)
            return;

        if (result.Parameters.TryGetValue<List<WhereClauseDto>>("WhereClauses", out var whereClauses))
        {
            valueLevel.WhereClauses = new AvaloniaList<WhereClauseDto>(whereClauses);
        }

        if (result.Parameters.TryGetValue<string>("ExpressionPreview", out var expressionPreview))
        {
            valueLevel.WhereClause = expressionPreview;
        }

        valueLevel.WhereClauseExist = !string.IsNullOrWhiteSpace(valueLevel.WhereClause);
        _sourceCache.AddOrUpdate(valueLevel);
        HasChanges = true;
    }

    [RelayCommand]
    private void DeleteWhereClause(ValueLevelDto valueLevel)
    {
        valueLevel.WhereClause = string.Empty;
        valueLevel.WhereClauseExist = false;
        valueLevel.WhereClauses = null;
        HasChanges = true;
    }
    
    [RelayCommand]
    private async Task PreparingCellForEdit(DataGridCellPointerPressedEventArgs e)
    {
        if(e.Column.Header is null) return;
        if (e.Column.Header.ToString() != "Variable") return;
        if (e.Row.DataContext is not ValueLevelDto valueLevelDto) return;
        var variables = await _variableService
            .GetAllVariablesByDatasetIdWithoutErorrAsync(valueLevelDto.DatasetId);
        VariableOptions.Clear();
        VariableOptions.AddRange(variables
            .Where(o => !string.IsNullOrWhiteSpace(o.VariableName))
            .Select(o => new ValueLevelAutoCompleteOption
            {
                Header = $"{o.VariableName} {o.Label}",
                Content = o.VariableName,
                Variable = o
            }));
       
    }

    [RelayCommand]
    private async Task AddCommentAsync(ValueLevelDto valueLevel)
    {
        var defaultId = string.Join(".", new[] { "COM", valueLevel.Dataset, valueLevel.Variable }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var result = await _dialogService.ShowAddCommentModelAsync(defaultId);
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<CommentDto>("Model", out var comment))
            return;

        var entity = await _commentService.InsertCommentAsync(comment);
        valueLevel.Comment = _mapper.Map<Comment>(entity);
        valueLevel.CommentId = entity.Id;
        valueLevel.CommentUniqueId = entity.UniqueId;
        _sourceCache.AddOrUpdate(valueLevel);
        await _valueLevelService.UpdateValueLevelAsync(valueLevel);
        _messageService.Success("Comment added successfully.");
    }

    [RelayCommand]
    private async Task EditCommentAsync(ValueLevelDto valueLevel)
    {
        if (valueLevel.Comment is null)
            return;

        var result = await _dialogService.ShowEditCommentModelAsync(_mapper.Map<CommentDto>(valueLevel.Comment));
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<CommentDto>("Model", out var comment))
            return;

        var entity = await _commentService.UpdateCommentAsync(comment);
        valueLevel.Comment = entity;
        valueLevel.CommentId = entity.Id;
        valueLevel.CommentUniqueId = entity.UniqueId;
        _sourceCache.AddOrUpdate(valueLevel);
        await _valueLevelService.UpdateValueLevelAsync(valueLevel);
        _messageService.Success("Comment updated successfully.");
    }

    [RelayCommand]
    private async Task DeleteCommentAsync(ValueLevelDto valueLevel)
    {
        if (valueLevel.Comment == null || !await _referenceDeletionService.ConfirmAndDeleteCommentAsync(valueLevel.Comment))
            return;

        var affectedValueLevels = _sourceCache.Items
            .Where(item => item.CommentId == valueLevel.Comment.Id)
            .ToList();
        foreach (var affectedValueLevel in affectedValueLevels)
        {
            affectedValueLevel.Comment = null;
            affectedValueLevel.CommentId = 0;
            affectedValueLevel.CommentUniqueId = string.Empty;
        }
        _sourceCache.Edit(cache => cache.AddOrUpdate(affectedValueLevels));
        _messageService.Success("Comment deleted successfully.");
    }

    [RelayCommand]
    private async Task DeleteMethodAsync(ValueLevelDto valueLevel)
    {
        if (valueLevel.Method == null || !await _referenceDeletionService.ConfirmAndDeleteMethodAsync(valueLevel.Method))
            return;

        var affectedValueLevels = _sourceCache.Items
            .Where(item => item.MethodId == valueLevel.Method.Id)
            .ToList();
        foreach (var affectedValueLevel in affectedValueLevels)
        {
            affectedValueLevel.Method = null;
            affectedValueLevel.MethodId = 0;
            affectedValueLevel.MethodUniqueId = string.Empty;
        }
        _sourceCache.Edit(cache => cache.AddOrUpdate(affectedValueLevels));
        _messageService.Success("Method deleted successfully.");
    }

    [RelayCommand]
    private async Task Save()
    {
        foreach (var valueLevel in ValueLevels)
        {
            valueLevel.ProjectId = _currentProjectService.CurrentProject?.Id ?? valueLevel.ProjectId;
            valueLevel.CdiscDataType = _currentProjectService.CdiscDataType;
            valueLevel.WhereClauseExist = !string.IsNullOrWhiteSpace(valueLevel.WhereClause);
        }

        await _valueLevelService.SaveValueLevelsAsync(ValueLevels.ToList());
        HasChanges = false;
        _messageService.Success("Value levels saved successfully.");
        if (_currentProjectService.CurrentProject != null)
            await LoadValueLevels();
    }

    [RelayCommand]
    private async Task Discard()
    {
        if (!HasChanges || _currentProjectService.CurrentProject == null)
            return;

        await LoadValueLevels();
    }

    public override Task OnNavigatedToAsync(NavigationContext navigationContext)
    {
        if (_currentProjectService.CdiscDataType == CdiscDataType.Sdtm)
        {
            Origins.AddRange([
                "", "Collected", "Derived", "Assigned", "Protocol", "Predecessor"
            ]);
        }
        else
        {
            Origins.AddRange([
                "", "Derived", "Assigned", "Predecessor"
            ]);
        }
        return Task.CompletedTask;
    }

    public async Task LoadDataAsync()
    {
        if (_currentProjectService.CurrentProject == null)
            return;
        await LoadValueLevels();

    }

    public override Task OnNavigatedFromAsync(NavigationContext navigationContext)
    {
        foreach (var valueLevelDto in _sourceCache.Items)
            UnregisterValueLevelDtoPropertyChanged(valueLevelDto);

        return Task.CompletedTask;
    }

    public override void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }

    public async Task LoadValueLevels()
    {
        foreach (var valueLevelDto in _sourceCache.Items)
            UnregisterValueLevelDtoPropertyChanged(valueLevelDto);

        var dtoList = await _valueLevelService.GetAllValueLevelDtosAsync();
        RebuildVariableOptions(await _variableService.GetAllVariablesWithoutErorrAsync());
        foreach (var dto in dtoList)
        {
            await _validator.ValidateDtoAsync(dto);
            RegisterValueLevelDtoPropertyChanged(dto);
        }
        _sourceCache.Edit(cache =>
        {
            cache.Clear();
            cache.AddOrUpdate(dtoList);
        });

        HasChanges = false;
    }

    private void RebuildDatasetOptions(IReadOnlyCollection<Dataset> datasets)
    {
        DatasetOptions.Clear();
        DatasetOptions.AddRange(datasets
            .Where(o => !string.IsNullOrWhiteSpace(o.Name))
            .Select(o => new ValueLevelAutoCompleteOption
            {
                Header = $"{o.Name} {o.Label}",
                Content = o.Name,
                Dataset = o
            }));
    }

    private void RebuildVariableOptions(IReadOnlyCollection<Variable> variables)
    {
        VariableOptions.Clear();
        VariableOptions.AddRange(variables
            .Where(o => !string.IsNullOrWhiteSpace(o.VariableName))
            .Select(o => new ValueLevelAutoCompleteOption
            {
                Header = $"{o.VariableName} {o.Label}",
                Content = o.VariableName,
                Variable = o
            }));
    }

    private void RebuildCodeListOptions(IReadOnlyCollection<CodeList> codeLists)
    {
        CodeListOptions.Clear();
        CodeListOptions.AddRange(codeLists.Select(o => new ValueLevelAutoCompleteOption
        {
            Header = $"{o.UniqueId} {o.Name}",
            Content = o.UniqueId,
            CodeList = o
        }));
    }

    private void RebuildMethodOptions(IReadOnlyCollection<Method> methods)
    {
        MethodOptions.Clear();
        MethodOptions.AddRange(methods.Select(o => new ValueLevelAutoCompleteOption
        {
            Header = $"{o.UniqueId} {o.Name}",
            Content = o.UniqueId,
            Method = o
        }));
    }

    private void RebuildCommentOptions(IReadOnlyCollection<Comment> comments)
    {
        CommentOptions.Clear();
        CommentOptions.AddRange(comments
            .Where(o => !o.HasErrors)
            .Select(o => new ValueLevelAutoCompleteOption
            {
                Header = $"{o.UniqueId} {o.Description}",
                Content = o.UniqueId,
                Comment = o
            }));
    }

    partial void OnIsErrorOnlyChanged(bool value) => _sourceCache.Refresh();

    private Func<ValueLevelDto, bool> BuildFilter()
    {
        var searchFilter = SearchFilterExtensions.BuildSearchFilter<ValueLevelDto>(
            SearchText,
            x => x.Dataset,
            x => x.Variable,
            x => x.Label,
            x => x.Type,
            x => x.WhereClause,
            x => x.Pages,
            x => x.MethodUniqueId);
        return valueLevel => (!IsErrorOnly || valueLevel.HasErrors) && searchFilter(valueLevel);
    }

   
}

public record ValueLevelAutoCompleteOption : AutoCompleteOption
{
    public Dataset? Dataset { get; set; }
    public Variable? Variable { get; set; }
    public CodeList? CodeList { get; set; }
    public Method? Method { get; set; }
    public Comment? Comment { get; set; }
}
