using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using cdisc_dataset.Extensions;
using cdisc_dataset.Models;
using cdisc_dataset.Models.Dto;
using cdisc_dataset.Models.Enums;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Prism.Dialogs;
using SqlSugar;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class VariableViewModel(
    ISqlSugarClient sqlSugar,
    IMessageService messageService,
    ICurrentProjectService currentProjectService,
    IVariableService  variableService,
    IDatasetService datasetService) : ObservableObject, IDialogHostAware
{

    public string? DialogHostName { get; set; } = "Root";

    private int CurrentProjectId => currentProjectService.CurrentProject?.Id ?? 0;
    
    private CdiscDataType CdiscDataType => currentProjectService.CdiscDataType;

    [ObservableProperty]
    private string? _selectedDatasetName;

    [ObservableProperty]
    private VariableDto? _selectedAvailableVariable;

    [ObservableProperty]
    private VariableDto? _selectedVariableToAdd;

    public AvaloniaList<string?> DatasetNames { get; } = [];

    public AvaloniaList<VariableDto> AvailableVariables { get; } = [];

    public AvaloniaList<VariableDto> VariablesToAdd { get; } = [];

    public async void OnDialogOpened(IDialogParameters parameters)
    {
        await LoadDatasetNames();

        if (parameters.TryGetValue("DatasetName", out string? datasetName) &&
            !string.IsNullOrWhiteSpace(datasetName) &&
            DatasetNames.Contains(datasetName))
        {
            SelectedDatasetName = datasetName;
        }
        else
        {
            SelectedDatasetName = DatasetNames.FirstOrDefault();
        }
    }

    partial void OnSelectedDatasetNameChanged(string? value)
    {
        LoadAvailableVariables().Await();
    }

    private async Task LoadDatasetNames()
    {
        DatasetNames.Clear();
        var datasetNames = await datasetService.GetDatasetNamesAsync();
        DatasetNames.AddRange(datasetNames.OrderBy(o=>o).ToList());
    }

    private async Task LoadAvailableVariables()
    {
        AvailableVariables.Clear();
        SelectedAvailableVariable = null;
        SelectedVariableToAdd = null;

        if (string.IsNullOrWhiteSpace(SelectedDatasetName)) return;

        var availableVariables = await variableService.GetAvailableVariablesAsync(SelectedDatasetName);

        foreach (var variable in availableVariables)
        {
            variable.ProjectId = CurrentProjectId;
            variable.CdiscDataType = CdiscDataType;
        }
        AvailableVariables.AddRange(availableVariables);
    }

    [RelayCommand]
    private void MoveToRight()
    {
        if (SelectedAvailableVariable == null) return;

        var variable = SelectedAvailableVariable;
        AvailableVariables.Remove(variable);
        VariablesToAdd.Add(variable);
        SelectedAvailableVariable = null;
        SelectedVariableToAdd = variable;
    }

    [RelayCommand]
    private void MoveToLeft()
    {
        if (SelectedVariableToAdd == null) return;

        var variable = SelectedVariableToAdd;
        VariablesToAdd.Remove(variable);
        AvailableVariables.Add(variable);
        var ordered = AvailableVariables.OrderBy(o => o.Order).ThenBy(o => o.VariableName).ToList();
        AvailableVariables.Clear();
        AvailableVariables.AddRange(ordered);
        SelectedVariableToAdd = null;
        SelectedAvailableVariable = variable;
    }

    [RelayCommand]
    private void Save()
    {
        if (VariablesToAdd.Count == 0)
        {
            messageService.Error("请选择需要添加的Variable");
            return;
        }

        foreach (var variable in VariablesToAdd)
        {
            variable.Id = 0;
            variable.ProjectId = CurrentProjectId;
            variable.CdiscDataType = CdiscDataType.Sdtm;
            variable.DatasetName = SelectedDatasetName;
        }

        var dialogResult = new DialogResult
        {
            Result = ButtonResult.Yes,
            Parameters = new DialogParameters { { "Variables", VariablesToAdd.ToList() } }
        };
        DialogHost.Close("Root", dialogResult);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close("Root", new DialogResult { Result = ButtonResult.Cancel });
    }
}
