using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using PatChes.Models.Dto;
using PatChes.Services;
using PatChes.Services.Interface;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace PatChes.ViewModels.Dialogs;

public partial class BuildValueLevelsViewModel(IVariableService variableService) : ObservableObject, IDialogHostAware
{
    public string? DialogHostName { get; set; } = "Root";

    public AvaloniaList<VariableOption> Variables { get; } = [];
    public AvaloniaList<VariableOption> WhereClauseVariables { get; } = [];

    [ObservableProperty]
    private VariableOption? _selectedVariable;

    [ObservableProperty]
    private VariableOption? _selectedWhereClauseVariable;

    public async Task OnDialogOpenedAsync(IDialogParameters? parameters, CancellationToken cancellationToken)
    {
        var variables = await variableService.GetAllVariableDtosAsync();
        var options = variables
            .Where(variable => !string.IsNullOrWhiteSpace(variable.DatasetName)
                               && !string.IsNullOrWhiteSpace(variable.VariableName))
            .OrderBy(variable => variable.DatasetName)
            .ThenBy(variable => variable.VariableName)
            .Select(CreateOption)
            .ToList();

        Variables.Clear();
        Variables.AddRange(options);
        WhereClauseVariables.Clear();
        WhereClauseVariables.AddRange(options.Where(option => option.Variable.CodeList != null));
        SelectedVariable = null;
        SelectedWhereClauseVariable = null;
    }

    private static VariableOption CreateOption(VariableDto variable) => new()
    {
        Header = $"{variable.DatasetName}.{variable.VariableName}",
        Content = $"{variable.DatasetName}.{variable.VariableName} {variable.Label}",
        Variable = variable
    };

    [RelayCommand]
    private void Confirm()
    {
        var variable = SelectedVariable?.Variable;
        var whereClauseVariable = SelectedWhereClauseVariable?.Variable;
        if (variable == null || whereClauseVariable == null)
            return;

        DialogHost.Close(DialogHostName ?? "Root", new DialogHostResult
        {
            Result = DialogButtonResult.Yes,
            Parameters = new DialogParameters
            {
                { "Variable", variable },
                { "WhereClauseVariable", whereClauseVariable }
            }
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName ?? "Root", new DialogHostResult
        {
            Result = DialogButtonResult.Cancel
        });
    }
}
