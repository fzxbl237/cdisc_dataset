using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
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
using DynamicData;
using DynamicData.Binding;
using FluentValidation;
using Net.Pinnacle21.Define.Parser;
using Prism.Dialogs;
using Prism.Navigation.Regions;
using DataGridCellPointerPressedEventArgs = Avalonia.Controls.DataGridCellPointerPressedEventArgs;
using DataGridPreparingCellForEditEventArgs = Avalonia.Controls.DataGridPreparingCellForEditEventArgs;

namespace cdisc_dataset.ViewModels.Defines;

[RegionMemberLifetime(KeepAlive = false)]
public partial class ValueLevelsViewModel : ConfirmNavigationViewModelBase
{
    private readonly IMessageService _messageService;
    private readonly IValueLevelService _valueLevelService;
    private readonly IDatasetService _datasetService;
    private readonly IVariableService _variableService;
    private readonly ICodeListService _codeListService;
    private readonly IMethodService _methodService;
    private readonly IDocumentService _documentService;
    private readonly ICommentService _commentService;
    private readonly IDialogHostService _dialogHostService;
    private readonly IValidator<ValueLevelDto> _validator;

    [ObservableProperty]
    private Project? _currentProject;

    [ObservableProperty]
    private CdiscDataType _cdiscDataType;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private string? _searchText;

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
        ICodeListService codeListService,
        IMethodService methodService,
        IDocumentService documentService,
        ICommentService commentService,
        IDialogHostService dialogHostService,
        IValidator<ValueLevelDto> validator)
    {
        _messageService = messageService;
        _valueLevelService = valueLevelService;
        _datasetService = datasetService;
        _variableService = variableService;
        _codeListService = codeListService;
        _methodService = methodService;
        _documentService = documentService;
        _commentService = commentService;
        _dialogHostService = dialogHostService;
        _validator = validator;

        var filter = this.WhenValueChanged(t => t.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Select(BuildFilter);

        _sourceCache.Connect()
            .Filter(filter)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _valueLevels, SortExpressionComparer<ValueLevelDto>.Ascending(o => o.Dataset ?? string.Empty)
                .ThenByAscending(o => o.Variable ?? string.Empty)
                .ThenByAscending(o=>o.Order))
            .DisposeMany()
            .Subscribe();

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.WhereClause, false)
            .Subscribe((change) =>
            {
                var valueLevelDto = UpdateWhereClauses(change.Sender);
                Observable.StartAsync(async () =>
                {
                    var whereClauseDtos = valueLevelDto.WhereClauses;
                    valueLevelDto.IsWhereClauseEffective = true;
                    if (whereClauseDtos != null)
                    {
                        foreach (var whereClauseDto in whereClauseDtos)
                        {
                            var variable = await _variableService.GetVariableByDatasetIdAndVariableNameWithoutError(valueLevelDto.DatasetId,whereClauseDto.Variable);
                            if (variable == null && valueLevelDto.IsWhereClauseEffective)
                            {
                                valueLevelDto.IsWhereClauseEffective = false;
                            }
                            else
                            {
                                whereClauseDto.VariableEntity = variable;
                                whereClauseDto.VariableId = variable?.Id??0;
                            }
                        }
                    }
                    await _validator.ValidateDtoAsync(valueLevelDto, "WhereClause");
                    _sourceCache.AddOrUpdate(valueLevelDto);
                });
            });
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Type, false)
            .Subscribe((change) =>
            {
                var valueLevelDto = UpdateWhereClauses(change.Sender);
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(valueLevelDto, "Type");
                    await _validator.ValidateDtoAsync(valueLevelDto, "Length");
                    await _validator.ValidateDtoAsync(valueLevelDto, "Digits");
                    _sourceCache.AddOrUpdate(valueLevelDto);
                });
            });

        _sourceCache.Connect().WhenAnyPropertyChanged().Subscribe(_ => HasChanges = true);
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Dataset, false)
            .Subscribe(change =>
            {
                var valueLevelDto = change.Sender;
                Observable.StartAsync(async () =>
                {
                    var datasetEntity = await _datasetService.GetDatasetByName(valueLevelDto.Dataset);
                    valueLevelDto.DatasetEntity = datasetEntity;
                    valueLevelDto.DatasetId = datasetEntity?.Id ?? 0;
                    await _validator.ValidateDtoAsync(valueLevelDto, "Dataset");
                    _sourceCache.AddOrUpdate(valueLevelDto);
                });
            });
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Variable, false)
            .Subscribe(change =>
            {
                var valueLevelDto = change.Sender;
                Observable.StartAsync(async () =>
                {
                    var variableEntity = await _variableService
                        .GetVariableByDatasetIdAndVariableNameWithoutError(
                            valueLevelDto.DatasetId,
                            valueLevelDto.Variable);
                    valueLevelDto.VariableEntity = variableEntity;
                    valueLevelDto.VariableId = variableEntity?.Id ?? 0;
                    await _validator.ValidateDtoAsync(valueLevelDto, "Variable");
                    _sourceCache.AddOrUpdate(valueLevelDto);
                });
            });
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Label, false)
            .Subscribe(change =>
            {
                var valueLevelDto = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(valueLevelDto, "Label");
                    _sourceCache.AddOrUpdate(valueLevelDto);
                });
            });
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Origin, false)
            .Subscribe(change =>
            {
                var valueLevelDto = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(valueLevelDto, "Pages");
                    await _validator.ValidateDtoAsync(valueLevelDto, "MethodUniqueId");
                    _sourceCache.AddOrUpdate(valueLevelDto);
                });
            });
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Pages, false)
            .Subscribe(change =>
            {
                var valueLevelDto = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(valueLevelDto, "Pages");
                    _sourceCache.AddOrUpdate(valueLevelDto);
                });
            });
        
        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Source, false)
            .Subscribe(change =>
            {
                var valueLevelDto = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(valueLevelDto, "Pages");
                    _sourceCache.AddOrUpdate(valueLevelDto);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.MethodUniqueId, false)
            .Subscribe(change =>
            {
                var valueLevelDto = change.Sender;
                var methodOption = MethodOptions
                    .OfType<ValueLevelAutoCompleteOption>()
                    .FirstOrDefault(o => string.Equals((string?)o.Content, valueLevelDto.MethodUniqueId, StringComparison.OrdinalIgnoreCase));

                valueLevelDto.Method = methodOption?.Method;
                valueLevelDto.MethodId = methodOption?.Method?.Id ?? 0;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(valueLevelDto, "MethodUniqueId");
                    _sourceCache.AddOrUpdate(valueLevelDto);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.CommentUniqueId, false)
            .Subscribe(change =>
            {
                var valueLevelDto = change.Sender;
                var commentOption = CommentOptions
                    .OfType<ValueLevelAutoCompleteOption>()
                    .FirstOrDefault(o => string.Equals((string?)o.Content, valueLevelDto.CommentUniqueId, StringComparison.OrdinalIgnoreCase));

                valueLevelDto.Comment = commentOption?.Comment;
                valueLevelDto.CommentId = commentOption?.Comment?.Id ?? 0;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(valueLevelDto, "CommentUniqueId");
                    _sourceCache.AddOrUpdate(valueLevelDto);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Length, false)
            .Subscribe(change =>
            {
                var valueLevelDto = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(valueLevelDto, "Length");
                    _sourceCache.AddOrUpdate(valueLevelDto);
                });
            });

        _sourceCache.Connect()
            .WhenPropertyChanged(o => o.Digits, false)
            .Subscribe(change =>
            {
                var valueLevelDto = change.Sender;
                Observable.StartAsync(async () =>
                {
                    await _validator.ValidateDtoAsync(valueLevelDto, "Digits");
                    _sourceCache.AddOrUpdate(valueLevelDto);
                });
            });
        
    }
    
    private ValueLevelDto UpdateWhereClauses(ValueLevelDto dto)
    {
        var whereClauseParser = new WhereClauseParser();
        if (string.IsNullOrWhiteSpace(dto.WhereClause)) return dto;
        var orConjunction = whereClauseParser.Parse(dto.WhereClause);
        var andConjunction = orConjunction.Conjunctions.FirstOrDefault();
        AvaloniaList<WhereClauseDto> res = [];
        if (andConjunction != null)
        {
            foreach (var andConjunctionComparison in andConjunction.Comparisons)
            {
                var literal = andConjunctionComparison.Comparator.GetLiteral();
                var identifier = andConjunctionComparison.Identifier;
                var value = string.Join(", ",andConjunctionComparison.Values);
                var whereClauseDto = new WhereClauseDto()
                {
                    Values = value,
                    Comparator = literal,
                    Variable = identifier
                };
                res.Add(whereClauseDto);

            }
        }
        dto.WhereClauses = res;
        return dto;
    }

    [RelayCommand]
    private void AddValueLevel()
    {
        if (CurrentProject == null)
            return;

        var valueLevel = new ValueLevelDto
        {
            ProjectId = CurrentProject.Id,
            CdiscDataType = CdiscDataType,
            Order = GetNextOrder()
        };

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
        if (result.Result != ButtonResult.OK)
            return;

        await _valueLevelService.DeleteValueLevelAsync(valueLevelDto);
        _sourceCache.Remove(valueLevelDto);
        HasChanges = true;
        _messageService.Success("Delete Success");
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
            _messageService.Error("Dataset cannot be empty before editing WhereClause");
            return;
        }

        if (string.IsNullOrWhiteSpace(valueLevel.Variable))
        {
            _messageService.Error("Variable cannot be empty before editing WhereClause");
            return;
        }
        
        //var dto = UpdateWhereClauses(valueLevel);
        var dialogParameters = new DialogParameters
        {
            { "Title", "WhereClause Editor" },
            { "ValueLevel", valueLevel },
            { "ValueLevelId", valueLevel.Id },
            { "WhereClauses", valueLevel.WhereClauses?.ToList() ?? [new WhereClauseDto()] }
        };

        var result = await _dialogHostService.ShowDialogAsync("WhereClauseEditorDialog", dialogParameters);
        if (result.Result != ButtonResult.Yes)
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
    private async Task Save()
    {
        foreach (var valueLevel in ValueLevels)
        {
            valueLevel.ProjectId = CurrentProject?.Id ?? valueLevel.ProjectId;
            valueLevel.CdiscDataType = CdiscDataType;
            valueLevel.WhereClauseExist = !string.IsNullOrWhiteSpace(valueLevel.WhereClause);
        }

        await _valueLevelService.SaveValueLevelsAsync(ValueLevels.ToList());
        HasChanges = false;
        _messageService.Success("ValueLevels Save Success");
        if (CurrentProject != null)
            await LoadValueLevels(CurrentProject.Id, CdiscDataType);
    }

    [RelayCommand]
    private async Task Discard()
    {
        if (!HasChanges || CurrentProject == null)
            return;

        await LoadValueLevels(CurrentProject.Id, CdiscDataType);
    }

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        var navigationContextParameters = navigationContext.Parameters;
        navigationContextParameters.TryGetValue("CdiscDataType", out CdiscDataType cdiscDataType);
        navigationContextParameters.TryGetValue("CurrentProject", out Project? currentProject);
        CdiscDataType = cdiscDataType;
        CurrentProject = currentProject;
        if (CdiscDataType == CdiscDataType.Sdtm)
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
    }

    public async Task LoadDataAsync()
    {
        if (CurrentProject == null)
            return;

        await LoadLookups(CurrentProject.Id, CdiscDataType);
        await LoadValueLevels(CurrentProject.Id, CdiscDataType);
    }

    public override void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
    {
        continuationCallback(true);
    }

    public async Task LoadValueLevels(int id, CdiscDataType cdiscDataType)
    {
        var dtoList = await _valueLevelService.GetAllValueLevelDtosAsync();
        foreach (var dto in dtoList)
        {
            await _validator.ValidateDtoAsync(dto);
        }

        _sourceCache.Edit(o =>
        {
            o.Clear();
            o.AddOrUpdate(dtoList);
        });

        HasChanges = false;
    }

    public async Task LoadLookups(int id, CdiscDataType cdiscDataType)
    {
        var datasets = await _datasetService.GetAllDatasetsWithoutErrorAsync();
        DatasetOptions.Clear();
        DatasetOptions.AddRange(datasets
            .Where(o => !string.IsNullOrWhiteSpace(o.Name))
            .Select(o => new ValueLevelAutoCompleteOption
            {
                Header = $"{o.Name} {o.Label}",
                Content = o.Name,
                Dataset = o
            }));

        var variables = await _variableService.GetAllVariablesWithoutErorrAsync();
        VariableOptions.Clear();
        VariableOptions.AddRange(variables
            .Where(o => !string.IsNullOrWhiteSpace(o.VariableName))
            .Select(o => new ValueLevelAutoCompleteOption
            {
                Header = $"{o.VariableName} {o.Label}",
                Content = o.VariableName,
                Variable = o
            }));

        VariableOptions.Clear();
        VariableOptions.AddRange(VariableOptions);

        var codeLists = await _codeListService.GetAllCodeListsWithoutErorrAsync();
        var methods = await _methodService.GetAllMethodsWithoutErorrAsync();
        var comments = await _commentService.GetAllCommentsWithoutErorrAsync();

        CodeListOptions.Clear();
        CodeListOptions.AddRange(codeLists.Select(o => new ValueLevelAutoCompleteOption
        {
            Header =  $"{o.UniqueId} {o.Name}",
            Content = o.UniqueId,
            CodeList = o
        }));

        MethodOptions.Clear();
        MethodOptions.AddRange(methods.Select(o => new ValueLevelAutoCompleteOption
        {
            Header = $"{o.UniqueId} {o.Name}",
            Content = o.UniqueId ,
            Method = o
        }));

        CommentOptions.Clear();
        CommentOptions.AddRange(comments.Select(o => new ValueLevelAutoCompleteOption
        {
            Header =  $"{o.UniqueId} {o.Description}",
            Content = o.UniqueId,
            Comment = o
        }));
    }

    private static Func<ValueLevelDto, bool> BuildFilter(string? searchText)
        => SearchFilterExtensions.BuildSearchFilter<ValueLevelDto>(
            searchText,
            x => x.Dataset,
            x => x.Variable,
            x => x.Label,
            x => x.Type,
            x => x.WhereClause,
            x => x.Pages,
            x => x.MethodUniqueId);

   
}

public record ValueLevelAutoCompleteOption : AutoCompleteOption
{
    public Dataset? Dataset { get; set; }
    public Variable? Variable { get; set; }
    public CodeList? CodeList { get; set; }
    public Method? Method { get; set; }
    public Comment? Comment { get; set; }
}
