using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Controls;
using AtomUI.Controls.Data;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using cdisc_dataset.ViewModels.Defines;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using DynamicData;
using DynamicData.Binding;
using Prism.Common;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class WhereClauseEditorViewModel : ObservableObject, IDialogHostAware
{
    private readonly IVariableService _variableService;
    private readonly ITermService _termService;
    public string DialogHostName { get; set; } = "Root";

    private readonly SourceCache<WhereClauseDto, string> _whereClauseSource = new(x => x.Uuid);
    private readonly ReadOnlyObservableCollection<WhereClauseDto> _whereClauses;
    private readonly CompositeDisposable _disposables = new();
    
    private FrozenDictionary<string, Variable>? _frozenVariableDictionary;
    
    private readonly BehaviorSubject<IComparer<WhereClauseDto>> _sortSubject;
    
    private readonly Subject<Unit> _resortSignal = new();

    public ReadOnlyObservableCollection<WhereClauseDto> WhereClauses => _whereClauses;

    public WhereClauseEditorViewModel(IVariableService variableService,ITermService  termService)
    {
        _variableService = variableService;
        _termService = termService;
        _sortSubject = new BehaviorSubject<IComparer<WhereClauseDto>>(SortExpressionComparer<WhereClauseDto>
            .Ascending(p => p.Seq));
        _whereClauseSource.Connect()
            .AutoRefresh(x=>x.Seq)
            .ObserveOn(new SynchronizationContextScheduler(SynchronizationContext.Current!))
            .SortAndBind(out _whereClauses,_sortSubject,
                new SortAndBindOptions(){UseReplaceForUpdates = false})
            .Subscribe()
            .DisposeWith(_disposables);

        // _whereClauseSource.Connect()
        //     .WhenAnyPropertyChanged()
        //     .Subscribe((change) =>
        //     {
        //         UpdatePreview();
        //     })
        //     .DisposeWith(_disposables);

        _whereClauseSource.Connect()
            .WhenPropertyChanged(x => x.Variable, false)
            .Subscribe(change =>
            {
                Observable.StartAsync(async () =>
                {
                    await ApplyVariableEntity(change.Sender);
                    change.Sender.ResetValueState();
                });
                UpdatePreview();
                // ApplyVariableEntity(change.Sender).Await();
                // change.Sender.ResetValueState();

            })
            .DisposeWith(_disposables);

        _whereClauseSource.Connect()
            .WhenPropertyChanged(x => x.Comparator, false)
            .Subscribe(change =>
            {
                change.Sender.ResetValueState();
                UpdatePreview();
            })
            .DisposeWith(_disposables);
        
        _whereClauseSource.Connect()
            .WhenPropertyChanged(x => x.Values, false)
            .Subscribe(change =>
            {
                UpdatePreview();
            })
            .DisposeWith(_disposables);
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

    public void OnDialogOpened(IDialogParameters parameters)
    {
        _whereClauseSource.Clear();

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
        
        ApplyTermOptions(clauses).Await();
        UpdatePreview();
        
        if (ValueLevelDto != null)
        {
            LoadLookups(ValueLevelDto.DatasetId).Await();
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
        
        _whereClauseSource.Edit(o=>o.AddOrUpdate(list));
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
            if (terms != null)
            {
                var selectOptions = terms.Where(o => !string.IsNullOrWhiteSpace(o.Name))
                    .Select(o => new SelectOption()
                    {
                        Header = o.Name,
                        Content = o.Name,
                    }).ToList();
                clause.Terms.Clear();
                clause.Terms.AddRange(selectOptions);
            }
        }
        else
        {
            clause.VariableEntity = null;
        }
    }

    private void ReindexDeleteFlags()
    {
        _whereClauseSource.Edit(list =>
        {
            int seq = 0;
            foreach (var whereClauseDto in list.Items.OrderBy(o=>o.Seq))
            {
                if (seq == 0)
                    whereClauseDto.CanDelete = false;
                whereClauseDto.Seq = ++seq;
            }
        });
    }


    private void UpdatePreview()
    {
        ExpressionPreview = BuildExpression(_whereClauseSource.Items);
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
    
    [RelayCommand]
    private void AddAfter(WhereClauseDto current)
    {
        ReindexDeleteFlags();
        var currentSeq = current.Seq;
        _whereClauseSource.Edit(inner =>
        {
            foreach (var whereClauseDto in inner.Items.Where(o=>o.Seq > currentSeq))
            {
                whereClauseDto.Seq+=1;
            }
            var newClause = CreateNewClause();
            newClause.Seq = currentSeq+1;
            inner.AddOrUpdate(newClause);
        });
        
    }

    [RelayCommand]
    private void Delete(WhereClauseDto current)
    {
        if (!current.CanDelete)
            return;

        if (WhereClauses.Count <= 1)
            return;

        _whereClauseSource.RemoveKey(current.Uuid);
        ReindexDeleteFlags();
        UpdatePreview();
        // _sortSubject.OnNext(SortExpressionComparer<WhereClauseDto>
        //     .Ascending(p => p.Seq));
    }

    [RelayCommand]
    private void Confirm()
    {
        _disposables.Dispose();
        DialogHost.Close(DialogHostName, new DialogResult
        {
            Result = ButtonResult.Yes,
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
        _disposables.Dispose();
        DialogHost.Close(DialogHostName, new DialogResult { Result = ButtonResult.Cancel });
    }
}

