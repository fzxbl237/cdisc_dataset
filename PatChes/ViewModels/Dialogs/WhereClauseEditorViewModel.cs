using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Controls;
using AtomUI.Controls.Data;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using PatChes.Controls.DataGrid;
using PatChes.Extensions;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Models.Enums;
using PatChes.Services;
using PatChes.Services.Interface;
using PatChes.ViewModels.Defines;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace PatChes.ViewModels.Dialogs;

public partial class WhereClauseEditorViewModel : ObservableObject, IDialogHostAware, IDataGridDynamicEditorProvider
{
    private readonly IVariableService _variableService;
    private readonly ITermService _termService;
    public string DialogHostName { get; set; } = "Root";

    private FrozenDictionary<string, Variable>? _frozenVariableDictionary;
    private bool _isReindexing;

    public AvaloniaList<WhereClauseDto> WhereClauses { get; } = [];

    public WhereClauseEditorViewModel(IVariableService variableService,ITermService  termService)
    {
        _variableService = variableService;
        _termService = termService;
        WhereClauses.CollectionChanged += OnWhereClausesCollectionChanged;
    }

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private int _valueLevelId;
    
    [ObservableProperty]
    private ValueLevelDto? _valueLevelDto;
    
    [ObservableProperty] private AvaloniaList<IAutoCompleteOption> _variableOptions = [];

    [ObservableProperty]
    private string? _expressionPreview;

    public AvaloniaList<string> Comparators { get; } = ["EQ", "NE", "LT", "LE", "GT", "GE", "IN", "NOTIN"];

    public async Task OnDialogOpenedAsync(IDialogParameters? parameters, CancellationToken cancellationToken)
    {
        parameters ??= new DialogParameters();
        WhereClauses.Clear();

        if (parameters.ContainsKey("Title"))
            Title = parameters.GetValue<string>("Title");
        
        if (parameters.ContainsKey("ValueLevel"))
            ValueLevelDto = parameters.GetValue<ValueLevelDto>("ValueLevel");

        if (parameters.ContainsKey("ValueLevelId"))
            ValueLevelId = parameters.GetValue<int>("ValueLevelId");

        var clauses = parameters.ContainsKey("WhereClauses")
            ? parameters.GetValue<IList<WhereClauseDto>>("WhereClauses")
            : [];
        
        for (var i = 0; i < clauses.Count; i++)
        {
            clauses[i].Seq = i + 1;
        }
        
        await ApplyTermOptions(clauses);
        UpdatePreview();
        
        if (ValueLevelDto != null)
        {
            await LoadLookups(ValueLevelDto.DatasetId);
        }

    }

    private async Task ApplyTermOptions(IList<WhereClauseDto> list)
    {
        foreach (var whereClauseDto in list)
        {
            if (whereClauseDto.VariableEntity?.CodeList != null)
            {
                var terms = await _termService.GetTermsByCodeListIdAsync(whereClauseDto.VariableEntity?.CodeListId);
                if (terms != null)
                {
                    var selectOptions = terms.Where(o => !string.IsNullOrWhiteSpace(o.Name))
                        .Select(o => new SelectOption()
                        {
                            Header = o.Name,
                            Content = o.Name,
                        }).ToList();
                    whereClauseDto.Terms.AddRange(selectOptions);
                }

                whereClauseDto.HasCodeListValues = true;
            }
            whereClauseDto.SyncValueAndSelection();
        }
        
        WhereClauses.AddRange(list);
    }
    
    private async Task LoadLookups(int datasetId)
    {
        var variables = await _variableService.GetAllVariablesByDatasetIdAsync(datasetId);
        _frozenVariableDictionary = variables
            .Where(o => !string.IsNullOrWhiteSpace(o.VariableName))
            .ToFrozenDictionary(o => o.VariableName ?? string.Empty, o => o);
        VariableOptions.Clear();
        VariableOptions.AddRange(variables
            .Where(o => !string.IsNullOrWhiteSpace(o.VariableName))
            .Select(o => new ValueLevelAutoCompleteOption
            {
                Header = $"{o.VariableName} {o.Label}",
                Content = o.VariableName,
                Variable = o
            }).ToList());
    }

    private async Task ApplyVariableEntity(WhereClauseDto? clause)
    {
        if (clause == null || string.IsNullOrWhiteSpace(clause.Variable))
        {
            if (clause != null)
                clause.VariableEntity = null;
            return;
        }

        if (_frozenVariableDictionary != null &&
            _frozenVariableDictionary.TryGetValue(clause.Variable ?? string.Empty, out var variableEntity))
        {
            clause.VariableEntity = variableEntity;
            var terms = await _termService.GetTermsByCodeListIdAsync(clause.VariableEntity?.CodeListId);
            clause.Terms.Clear();
            if (terms != null)
            {
                var selectOptions = terms.Where(o => !string.IsNullOrWhiteSpace(o.Name))
                    .Select(o => new SelectOption()
                    {
                        Header = o.Name,
                        Content = o.Name,
                    }).ToList();
                clause.Terms.AddRange(selectOptions);
            }

            clause.HasCodeListValues = clause.Terms.Count > 0;
        }
        else
        {
            clause.VariableEntity = null;
            clause.Terms.Clear();
            clause.HasCodeListValues = false;
        }
    }

    private void OnWhereClausesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (WhereClauseDto clause in e.OldItems)
                clause.PropertyChanged -= OnWhereClausePropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (WhereClauseDto clause in e.NewItems)
                clause.PropertyChanged += OnWhereClausePropertyChanged;
        }

        ReindexDeleteFlags();
        UpdatePreview();
    }

    private void OnWhereClausePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not WhereClauseDto clause)
            return;

        switch (e.PropertyName)
        {
            case nameof(WhereClauseDto.Variable):
                Observable.StartAsync(async () =>
                {
                    await ApplyVariableEntity(clause);
                    clause.ResetValueState();
                    UpdatePreview();
                });
                break;
            case nameof(WhereClauseDto.Comparator):
                clause.ResetValueState();
                clause.Values = string.Empty;
                UpdatePreview();
                break;
            case nameof(WhereClauseDto.Values):
                UpdatePreview();
                break;
        }
    }

    private void ReindexDeleteFlags()
    {
        if (_isReindexing)
            return;

        _isReindexing = true;
        try
        {
            for (var index = 0; index < WhereClauses.Count; index++)
            {
                var clause = WhereClauses[index];
                clause.Seq = index + 1;
                clause.CanDelete = index != 0;
            }
        }
        finally
        {
            _isReindexing = false;
        }
    }

    private void UpdatePreview()
    {
        ExpressionPreview = BuildExpression(WhereClauses);
    }

    private static string BuildExpression(IEnumerable<WhereClauseDto> clauses)
    {
        var parts = clauses
            .Where(c => !string.IsNullOrWhiteSpace(c.Variable) 
                        && !string.IsNullOrWhiteSpace(c.Comparator) 
                        && !string.IsNullOrWhiteSpace(c.Values))
            .Select(c => $"{c.Variable} {c.Comparator} {c.Values}".Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return parts.Count == 0 ? string.Empty : string.Join(" and ", parts);
    }

    private static WhereClauseDto CreateNewClause(bool canDelete = true) => new()
    {
        Uuid = Guid.NewGuid().ToString(),
        CanDelete = canDelete
    };
    
    public Task<Control?> CreateEditorAsync(
        DataGridDynamicEditorContext context,
        CancellationToken cancellationToken)
    {
        if (context.DataItem is not WhereClauseDto clause)
            return Task.FromResult<Control?>(null);

        var comparator = clause.Comparator?.Trim().ToUpperInvariant();
        var hasTermOptions = clause.Terms.Count > 0;
        if (hasTermOptions && comparator is "IN" or "NOTIN")
        {
            var select = new Select
            {
                OptionsSource = clause.Terms,
                PlaceholderText = "Values"
            };
            SetSelectMode(select, "Multiple");
            select.Bind(Select.SelectedOptionsProperty, new Binding(nameof(WhereClauseDto.SelectedTerms))
            {
                Source = clause,
                Mode = BindingMode.TwoWay
            });
            return Task.FromResult<Control?>(select);
        }

        if (hasTermOptions && comparator is "EQ" or "NE")
        {
            var select = new Select
            {
                OptionsSource = clause.Terms,
                PlaceholderText = "Values"
            };
            SetSelectMode(select, "Single");
            select.Bind(Select.SelectedOptionProperty, new Binding(nameof(WhereClauseDto.SelectedTerm))
            {
                Source = clause,
                Mode = BindingMode.TwoWay
            });
            return Task.FromResult<Control?>(select);
        }

        var lineEdit = new LineEdit
        {
            PlaceholderText = "Values"
        };
        lineEdit.Bind(LineEdit.TextProperty, new Binding(nameof(WhereClauseDto.Values))
        {
            Source = clause,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        return Task.FromResult<Control?>(lineEdit);
    }

    private static void SetSelectMode(Select select, string modeName)
    {
        var modeProperty = typeof(Select).GetProperty("Mode");
        if (modeProperty?.PropertyType.IsEnum == true)
            modeProperty.SetValue(select, Enum.Parse(modeProperty.PropertyType, modeName));
    }

    [RelayCommand]
    private void AddAfter(WhereClauseDto current)
    {
        var currentIndex = WhereClauses.IndexOf(current);
        if (currentIndex < 0)
            return;

        WhereClauses.Insert(currentIndex + 1, CreateNewClause());
    }

    [RelayCommand]
    private void Delete(WhereClauseDto current)
    {
        if (!current.CanDelete)
            return;

        if (WhereClauses.Count <= 1)
            return;

        WhereClauses.Remove(current);
    }

    [RelayCommand]
    private void Confirm()
    {
        DialogHost.Close(DialogHostName, new DialogHostResult
        {
            Result = DialogButtonResult.Yes,
            Parameters = new DialogParameters
            {
                { "WhereClauses", WhereClauses.ToList() },
                { "ExpressionPreview", ExpressionPreview ?? string.Empty }
            }
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName, new DialogHostResult { Result = DialogButtonResult.Cancel });
    }
}

