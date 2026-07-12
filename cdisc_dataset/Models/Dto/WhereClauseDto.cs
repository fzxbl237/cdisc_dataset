using System;
using System.Collections.Generic;
using System.Linq;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace cdisc_dataset.Models.Dto;

public partial class WhereClauseDto:ObservableObject
{
    [ObservableProperty] private int _id;
    
    [ObservableProperty] private int _seq;
    
    [ObservableProperty] private string _uuid = Guid.NewGuid().ToString();
    
    [ObservableProperty] private int _valueLevelId;
    
    [ObservableProperty] private string? _variable;

    [ObservableProperty] private Variable? _variableEntity;

    partial void OnVariableEntityChanged(Variable? value)
    {
        HasCodeListValues = value?.CodeList != null;
    }
    
    [ObservableProperty] private int _variableId;
    
    [ObservableProperty] private string? _comparator;
    
    [ObservableProperty] private string? _values;

    [ObservableProperty] private bool _canDelete = true;

    [ObservableProperty] private AvaloniaList<ISelectOption> _terms = [];
    
    [ObservableProperty] private IList<ISelectOption>? _selectedTerms = [];
    
    [ObservableProperty] private ISelectOption? _selectedTerm;

    [ObservableProperty]
    private bool _hasCodeListValues;

    [ObservableProperty]
    private bool _isMultiSelectValues;

    [ObservableProperty]
    private bool _showSelectValues;

    [ObservableProperty]
    private bool _showLineEditValues = true;

    // partial void OnComparatorChanged(string? value)
    // {
    //     ResetValueState();
    // }
    //
    // partial void OnVariableChanged(string? value)
    // {
    //     ResetValueState();
    // }
    //
    // partial void OnHasCodeListValuesChanged(bool value)
    // {
    //     ResetValueState();
    // }

    partial void OnSelectedTermChanged(ISelectOption? value)
    {
        if (value != null)
        {
            Values = GetOptionText(value);
        }
    }

    partial void OnSelectedTermsChanged(IList<ISelectOption>? value)
    {
        if (IsMultiSelectValues && value != null)
        {
            Values = string.Join(", ", value.Select(GetOptionText).Where(s => !string.IsNullOrWhiteSpace(s)));
        }
    }

    public void ResetValueState()
    {
        var comparator = Comparator?.Trim().ToUpperInvariant();
        IsMultiSelectValues = HasCodeListValues && (comparator == "IN" || comparator == "NOTIN");
        ShowSelectValues = !IsMultiSelectValues && HasCodeListValues && (comparator == "EQ" || comparator == "NE" || comparator == "IN" || comparator == "NOTIN");
        ShowLineEditValues = !ShowSelectValues && !IsMultiSelectValues;
        SelectedTerm = null;
        SelectedTerms?.Clear();
    }

    public static string GetOptionText(ISelectOption option)
    {
        return (option.Header as string) ?? option.Content?.ToString() ?? string.Empty;
    }

    public void SyncValueAndSelection()
    {
        var comparator = Comparator?.Trim().ToUpperInvariant();

        if (HasCodeListValues)
        {
            if (comparator is "EQ" or "NE")
            {
                if (SelectedTerm == null && !string.IsNullOrWhiteSpace(Values))
                {
                    SelectedTerm = Terms.FirstOrDefault(o => string.Equals(GetOptionText(o), Values.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                Values = SelectedTerm != null ? GetOptionText(SelectedTerm) : Values?.Trim() ?? string.Empty;
                SelectedTerms?.Clear();
                IsMultiSelectValues = false;
                ShowSelectValues = true;
                ShowLineEditValues = false;
                return;
            }

            if (comparator is "IN" or "NOTIN")
            {
                if (SelectedTerms?.Count == 0 && !string.IsNullOrWhiteSpace(Values))
                {
                    var values = Values.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    IList<ISelectOption> res = [];
                    foreach (var value in values)
                    {
                        var option = Terms.FirstOrDefault(o => string.Equals(GetOptionText(o), value, StringComparison.OrdinalIgnoreCase));
                        if (option != null)
                            res.Add(option);
                    }

                    SelectedTerms = res;
                }

                Values = SelectedTerms?.Count > 0
                    ? string.Join(", ", SelectedTerms.Select(GetOptionText).Where(s => !string.IsNullOrWhiteSpace(s)))
                    : Values?.Trim() ?? string.Empty;
                SelectedTerm = null;
                IsMultiSelectValues = true;
                ShowSelectValues = false;
                ShowLineEditValues = false;
                return;
            }
        }

        SelectedTerm = null;
        SelectedTerms?.Clear();
        IsMultiSelectValues = false;
        ShowSelectValues = false;
        ShowLineEditValues = true;
        Values = Values?.Trim() ?? string.Empty;
    }
}
