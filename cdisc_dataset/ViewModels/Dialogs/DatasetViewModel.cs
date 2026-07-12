using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Desktop.Controls;
using Avalonia.Collections;
using cdisc_dataset.Models;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class DatasetViewModel(
    IDatasetService datasetService,
    ICurrentProjectService currentProjectService) : ObservableObject, IDialogHostAware
{
    public string? DialogHostName { get; set; } = "Root";

    [ObservableProperty]
    private AvaloniaList<ISelectOption>? _options;

    [ObservableProperty]
    private IList<ISelectOption> _selectedOptions = [];

    public async void OnDialogOpened(IDialogParameters parameters)
    {
        var datasets = await datasetService.GetAvailableTemplateDatasetsAsync();
        List<ISelectOption> res = [];
        foreach (var dataset in datasets)
        {
            res.Add(new SelectOption { Header = dataset.Name, Content = dataset.Label });
        }

        Options = new AvaloniaList<ISelectOption>(res);
    }

    [RelayCommand]
    private async Task Save()
    {
        var list = SelectedOptions.Select(o => (string?)o.Header).ToList();
        var datasets = await datasetService.GetTemplateDatasetsWithVariablesByNamesAsync(list);

        var projectId = currentProjectService.CurrentProject?.Id ?? 0;
        foreach (var dataset in datasets)
        {
            dataset.Id = 0;
            dataset.ProjectId = projectId;
            dataset.CdiscDataType = currentProjectService.CdiscDataType;
            if (dataset.Variables != null)
            {
                foreach (var variable in dataset.Variables)
                {
                    variable.Id = 0;
                    variable.ProjectId = projectId;
                    variable.CdiscDataType = currentProjectService.CdiscDataType;
                }
            }
        }

        var dialogResult = new DialogResult
        {
            Result = ButtonResult.Yes,
            Parameters = new DialogParameters { { "Datasets", datasets } }
        };
        DialogHost.Close("Root", dialogResult);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close("Root", new DialogResult { Result = ButtonResult.Cancel });
    }
}