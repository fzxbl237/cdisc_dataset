using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using PatChes.Models;
using PatChes.Models.Dto;
using PatChes.Services;
using PatChes.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Dm.util;
using DynamicData;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;
using SqlSugar;

namespace PatChes.ViewModels.Dialogs;

public partial class EditKeyVariablesViewModel(IVariableService variableService) : ObservableObject, IDialogHostAware
{
    public string DialogHostName { get; set; } = "Root";
    
    private DatasetDto _datasetDto;

    [ObservableProperty]
    private AvaloniaList<ISelectOption> _options = [];
    
    [ObservableProperty]
    private IList<ISelectOption> _selectedOptions =[];

    public async Task OnDialogOpenedAsync(IDialogParameters? parameters, CancellationToken cancellationToken)
    {
        parameters ??= new DialogParameters();
        if (parameters.ContainsKey("DatasetDto"))
        {
            _datasetDto = parameters.GetValue<DatasetDto>("DatasetDto");
            if(_datasetDto.Id!=0)
                await LoadVariables(_datasetDto.Id);
        }
    }

    private async Task LoadVariables(int id)
    {
        var list = await variableService.GetAllVariablesByDatasetIdAsync(id);
        List<string> variableNames = [];
        if (!string.IsNullOrWhiteSpace(_datasetDto.KeyVariables))
        {
            variableNames.AddRange(_datasetDto.KeyVariables.Split(", "));
        }
        
        List<ISelectOption> res = [];
        List<ISelectOption> selectOptions = [];
        foreach (var variable in list)
        {
            var selectOption = new SelectOption() { Header = variable.VariableName,Content = variable.Label };
            res.Add(selectOption);
            if (variableNames.Contains(selectOption.Header))
            {
                selectOptions.Add(selectOption);
            }
        }
        Options.AddRange(res);
        SelectedOptions = selectOptions;
    }
    
    [RelayCommand]
    private void Save()
    {
        var keyVariables = string.Join(", ", SelectedOptions.Select(o => o.Header));
        var dialogResult = new DialogHostResult
        {
            Result = DialogButtonResult.Yes,
            Parameters = new DialogParameters { { "KeyVariables", keyVariables } }
        };
        DialogHost.Close("Root",dialogResult );
    }
    
    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close("Root",new DialogHostResult{Result = DialogButtonResult.Cancel} );
    }
    
    
}