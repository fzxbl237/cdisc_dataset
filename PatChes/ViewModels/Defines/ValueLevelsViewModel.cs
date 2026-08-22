using AsyncNavigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
using LiteDB;
using MapsterMapper;
using P21.Validator.Api.Options;
using P21.Validator.Data;
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
    private readonly ILiteDatabase _liteDatabase;
    private readonly IValueLevelService _valueLevelService;
    private readonly IVariableService _variableService;
    private readonly IDatasetService _datasetService;
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
        ILiteDatabase liteDatabase,
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
        _liteDatabase = liteDatabase;
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
                    MarkDuplicates();
                    await ValidateWhereClauseDuplicatesAsync();
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
                    MarkDuplicates();
                    await ValidateWhereClauseDuplicatesAsync();
                    break;
                }
                case nameof(ValueLevelDto.Variable):
                {
                    var variableEntity = await _variableService.GetVariableByDatasetIdAndVariableNameWithoutError(
                        valueLevelDto.DatasetId, valueLevelDto.Variable);
                    valueLevelDto.VariableEntity = variableEntity;
                    valueLevelDto.VariableId = variableEntity?.Id ?? 0;
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.Variable));
                    MarkDuplicates();
                    await ValidateWhereClauseDuplicatesAsync();
                    break;
                }
                case nameof(ValueLevelDto.WhereClauses):
                    MarkDuplicates();
                    await ValidateWhereClauseDuplicatesAsync();
                    break;
                case nameof(ValueLevelDto.IsWhereClauseDuplicate):
                    await _validator.ValidateDtoAsync(valueLevelDto, nameof(ValueLevelDto.WhereClause));
                    break;
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

    private void MarkDuplicates()
    {
        foreach (var valueLevel in _sourceCache.Items)
        {
            valueLevel.IsWhereClauseDuplicate = false;
            foreach (var whereClause in valueLevel.WhereClauses ?? [])
                whereClause.IsDuplicate = false;
        }

        _sourceCache.Items.MarkDuplicates(
            valueLevel => (
                valueLevel.Dataset ?? string.Empty,
                valueLevel.Variable ?? string.Empty,
                valueLevel.WhereClause ?? string.Empty),
            (valueLevel, isDuplicate) =>
            {
                valueLevel.IsWhereClauseDuplicate = isDuplicate;
                foreach (var whereClause in valueLevel.WhereClauses ?? [])
                    whereClause.IsDuplicate = isDuplicate;
            },
            key => !string.IsNullOrWhiteSpace(key.Item1)
                   && !string.IsNullOrWhiteSpace(key.Item2)
                   && !string.IsNullOrWhiteSpace(key.Item3));
    }

    private async Task ValidateWhereClauseDuplicatesAsync()
    {
        foreach (var valueLevel in _sourceCache.Items)
            await _validator.ValidateDtoAsync(valueLevel, nameof(ValueLevelDto.WhereClause));
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
    private async Task BuildValueLevelsAsync()
    {
        var result = await _dialogHostService.ShowDialogAsync("BuildValueLevelsDialog", new DialogParameters());
        if (result.Result != DialogButtonResult.Yes
            || !result.Parameters.TryGetValue<VariableDto>("Variable", out var variable)
            || !result.Parameters.TryGetValue<VariableDto>("WhereClauseVariable", out var whereClauseVariable)
            || variable == null
            || whereClauseVariable == null)
        {
            return;
        }

        if (variable.DatasetId == 0 || whereClauseVariable.DatasetId == 0
            || variable.DatasetId != whereClauseVariable.DatasetId)
        {
            _messageService.Error("Variable and where clause variable must belong to the same Dataset.");
            return;
        }

        var projectId = _currentProjectService.CurrentProject?.Id ?? 0;
        if (projectId == 0)
            return;

        var projectFile = _liteDatabase.GetCollection<ProjectFile>("project_files")
            .Query()
            .Where(file => file.ProjectId == projectId && file.FileType == ProjectFileType.Sdtm)
            .ToList()
            .FirstOrDefault(file => string.Equals(
                Path.GetFileNameWithoutExtension(file.FileName),
                Path.GetFileNameWithoutExtension(variable.DatasetName),
                StringComparison.OrdinalIgnoreCase));
        if (projectFile == null)
        {
            _messageService.Error($"SDTM XPT file for {variable.DatasetName} was not found in the current project.");
            return;
        }

        if (!_liteDatabase.FileStorage.Exists(projectFile.StorageId.ToString()))
        {
            _messageService.Error($"SDTM XPT file {projectFile.FileName} is registered but its stored content is missing.");
            return;
        }

        var xptData = await Task.Run(() => ReadXptVariables(projectFile));
        if (!xptData.TryGetValue(variable.VariableName ?? string.Empty, out var targetData)
            || !xptData.TryGetValue(whereClauseVariable.VariableName ?? string.Empty, out var whereClauseData))
        {
            _messageService.Error("The selected variable was not found in the Dataset XPT file.");
            return;
        }

        var whereClauseEntity = await _variableService.GetVariableByDatasetIdAndVariableNameWithoutError(
            whereClauseVariable.DatasetId,
            whereClauseVariable.VariableName);
        var terms = whereClauseData.Values.ToList();
        if (terms.Count == 0)
        {
            _messageService.Warning("No terms were found for the selected where clause variable.");
            return;
        }

        var dataset = await _datasetService.GetDatasetByName(variable.DatasetName);
        var targetEntity = await _variableService.GetVariableByDatasetIdAndVariableNameWithoutError(
            variable.DatasetId,
            variable.VariableName);
        if (dataset == null || targetEntity == null)
        {
            _messageService.Error("The selected Dataset or variable could not be resolved.");
            return;
        }

        var existing = _sourceCache.Items
            .Where(item => string.Equals(item.Dataset, dataset.Name, StringComparison.OrdinalIgnoreCase)
                           && string.Equals(item.Variable, targetEntity.VariableName, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.WhereClause ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nextOrder = GetNextOrder();
        var dataType = targetData.DataType ?? targetEntity.DataType;
        var isDateTimeDataType = IsDateTimeDataType(dataType);
        var generated = new List<ValueLevelDto>();
        foreach (var term in terms)
        {
            var whereClause = $"{whereClauseEntity?.VariableName ?? whereClauseVariable.VariableName} EQ {term}";
            if (!existing.Add(whereClause))
                continue;

            var valueLevel = new ValueLevelDto
            {
                ProjectId = projectId,
                CdiscDataType = _currentProjectService.CdiscDataType,
                Order = nextOrder++,
                Dataset = dataset.Name,
                DatasetId = dataset.Id,
                DatasetEntity = dataset,
                Variable = targetEntity.VariableName,
                VariableId = targetEntity.Id,
                VariableEntity = targetEntity,
                WhereClause = whereClause,
                Label = targetData.Label ?? targetEntity.Label,
                Type = dataType,
                Length = isDateTimeDataType ? null : targetData.Length ?? targetEntity.Length,
                Digits = targetData.SignificantDigits ?? targetEntity.SignificantDigits,
                Format = isDateTimeDataType ? null : targetData.Format ?? targetEntity.Format,
                Mandatory = targetEntity.Mandatory ?? "No",
                CodeListId = targetEntity.CodeListId,
                CodeList = targetEntity.CodeList,
                CodeListUniqueId = targetEntity.CodeListUniqueId,
                Origin = targetEntity.Origin,
                Source = targetEntity.Source,
                WhereClauseExist = true
            };

            generated.Add(valueLevel);
        }

        if (generated.Count == 0)
        {
            _messageService.Info("No new value levels were generated.");
            return;
        }

        var entities = new List<ValueLevelDto>(generated.Count);
        foreach (var valueLevel in generated)
        {
            var entity = await _valueLevelService.InsertValueLevelAsync(valueLevel);
            RegisterValueLevelDtoPropertyChanged(entity);
            entities.Add(entity);
        }

        _sourceCache.AddOrUpdate(entities);
        MarkDuplicates();
        HasChanges = false;
        _messageService.Success($"Built and saved {entities.Count} value level(s).");
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
        MarkDuplicates();
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
        MarkDuplicates();
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
        MarkDuplicates();
        HasChanges = true;
        _messageService.Success($"{selectedValueLevels.Count} value level(s) deleted successfully.");
    }

    private int GetNextOrder()
    {
        return _sourceCache.Items.Any() ? _sourceCache.Items.Max(x => x.Order) + 1 : 1;
    }

    private Dictionary<string, XptVariableData> ReadXptVariables(ProjectFile projectFile)
    {
        var storedFile = _liteDatabase.FileStorage.FindById(projectFile.StorageId.ToString());
        if (storedFile == null)
            return new Dictionary<string, XptVariableData>(StringComparer.OrdinalIgnoreCase);

        using var memoryStream = new MemoryStream();
        storedFile.CopyTo(memoryStream);
        memoryStream.Position = 0;

        var validationOptions = ValidationOptions.CreateBuilder().Build();
        var factory = new DataEntryFactory(validationOptions);
        var options = SourceOptions.builder()
            .WithName(Path.GetFileNameWithoutExtension(projectFile.FileName).ToUpperInvariant())
            .WithMemoryStream(memoryStream)
            .WithType(SourceOptions.StandardTypes.SasTransport)
            .Build();

        using var dataSource = new SasTransportDataSource(options, factory);
        var variableNames = dataSource.GetVariables().ToList();
        var allRecords = new List<DataRecord>();
        while (dataSource.HasRecords())
        {
            var records = dataSource.GetRecords();
            if (records.Count == 0)
                break;
            allRecords.AddRange(records);
        }

        var result = new Dictionary<string, XptVariableData>(StringComparer.OrdinalIgnoreCase);
        foreach (var variableName in variableNames)
        {
            var dataType = allRecords.Count == 0 ? null : allRecords.InferDataType(variableName);
            var length = Convert.ToInt32(dataSource.GetVariableProperty(variableName, DataSource.VariableProperty.Length) ?? 0);
            var values = allRecords
                .Select(record => record.GetValue(variableName))
                .Where(entry => entry?.HasValue == true)
                .Select(entry => entry!.ToString().Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var format = IsDateTimeDataType(dataType)
                ? null
                : (string?)dataSource.GetVariableProperty(variableName, DataSource.VariableProperty.Format);

            result[variableName] = new XptVariableData(
                (string?)dataSource.GetVariableProperty(variableName, DataSource.VariableProperty.Label),
                format,
                dataType,
                dataType == "float" ? allRecords.GetDecimalPlaces(variableName) : null,
                IsDateTimeDataType(dataType) || length == 0 ? null : length,
                values);
        }

        return result;
    }

    private static bool IsDateTimeDataType(string? dataType)
    {
        return dataType is not null && dataType.Equals("datetime", StringComparison.OrdinalIgnoreCase)
               || dataType is not null && dataType.Equals("date", StringComparison.OrdinalIgnoreCase)
               || dataType is not null && dataType.Equals("time", StringComparison.OrdinalIgnoreCase)
               || dataType is not null && dataType.Equals("partialDate", StringComparison.OrdinalIgnoreCase)
               || dataType is not null && dataType.Equals("partialTime", StringComparison.OrdinalIgnoreCase)
               || dataType is not null && dataType.Equals("partialDatetime", StringComparison.OrdinalIgnoreCase)
               || dataType is not null && dataType.Equals("partialDateTime", StringComparison.OrdinalIgnoreCase)
               || dataType is not null && dataType.Equals("incompleteDatetime", StringComparison.OrdinalIgnoreCase)
               || dataType is not null && dataType.Equals("durationDatetime", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record XptVariableData(
        string? Label,
        string? Format,
        string? DataType,
        int? SignificantDigits,
        int? Length,
        IReadOnlyList<string> Values);

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
        MarkDuplicates();
        await ValidateWhereClauseDuplicatesAsync();
        HasChanges = true;
    }

    [RelayCommand]
    private async Task DeleteWhereClause(ValueLevelDto valueLevel)
    {
        valueLevel.WhereClause = string.Empty;
        valueLevel.WhereClauseExist = false;
        valueLevel.WhereClauses = null;
        MarkDuplicates();
        await ValidateWhereClauseDuplicatesAsync();
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
    private async Task LinkVariablesAsync(ValueLevelDto valueLevel)
    {
        if (valueLevel.Comment == null || valueLevel.CommentId == 0 || string.IsNullOrWhiteSpace(valueLevel.CommentUniqueId))
            return;

        var result = await _dialogHostService.ShowDialogAsync("AssignCommentVariablesDialog", new DialogParameters
        {
            { "CommentId", valueLevel.CommentId },
            { "CommentUniqueId", valueLevel.CommentUniqueId }
        });
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<List<int>>("VariableIds", out var variableIds))
            return;

        var assignedCount = await _variableService.AssignCommentToVariablesAsync(
            valueLevel.CommentId,
            valueLevel.CommentUniqueId,
            variableIds);
        _messageService.Success($"Assigned comment to {assignedCount} variable(s).");
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
    private async Task LinkMethodVariablesAsync(ValueLevelDto valueLevel)
    {
        if (valueLevel.Method == null || valueLevel.MethodId == 0 || string.IsNullOrWhiteSpace(valueLevel.MethodUniqueId))
            return;

        var result = await _dialogHostService.ShowDialogAsync("AssignVariablesDialog", new DialogParameters());
        if (result.Result != DialogButtonResult.Yes ||
            !result.Parameters.TryGetValue<List<int>>("VariableIds", out var variableIds))
            return;

        var assignedCount = await _variableService.AssignMethodToVariablesAsync(
            valueLevel.MethodId,
            valueLevel.MethodUniqueId,
            variableIds);
        _messageService.Success($"Assigned method to {assignedCount} variable(s).");
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
        MarkDuplicates();
        await ValidateWhereClauseDuplicatesAsync();

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

        MarkDuplicates();
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
