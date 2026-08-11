using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Controls;
using AtomUI.Controls.Data;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class ImportSettingDatasetsViewModel(IDatasetService datasetService) : ObservableObject, IDialogHostAware
{
    public string? DialogHostName { get; set; } = "Root";

    [ObservableProperty]
    private List<IListItemData> _datasets = [];

    [ObservableProperty]
    private ObservableCollection<EntityKey> _targetKeys = [];

    public async void OnDialogOpened(IDialogParameters parameters)
    {
        var datasets = await datasetService.GetAvailableSettingDatasetsAsync();
        Datasets = datasets
            .Where(dataset => !string.IsNullOrWhiteSpace(dataset.Name))
            .OrderBy(dataset => dataset.Name)
            .Select(dataset => (IListItemData)new ListItemData
            {
                ItemKey = dataset.Name!,
                Content = string.IsNullOrWhiteSpace(dataset.Label)
                    ? dataset.Name
                    : $"{dataset.Name} - {dataset.Label}"
            })
            .ToList();
        TargetKeys.Clear();
    }

    [RelayCommand]
    private void Save()
    {
        var result = new DialogResult
        {
            Result = ButtonResult.Yes,
            Parameters = new DialogParameters
            {
                { "DatasetNames", TargetKeys.Select(key => key.Value).ToList() }
            }
        };
        DialogHost.Close(DialogHostName ?? "Root", result);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName ?? "Root", new DialogResult { Result = ButtonResult.Cancel });
    }
}
