using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using AtomUI.Controls;
using AtomUI.Controls.Data;
using PatChes.Services;
using PatChes.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace PatChes.ViewModels.Dialogs;

public partial class ImportSettingDatasetsViewModel(IDatasetService datasetService) : ObservableObject, IDialogHostAware
{
    public string? DialogHostName { get; set; } = "Root";

    [ObservableProperty]
    private List<IListItemData> _datasets = [];

    [ObservableProperty]
    private ObservableCollection<EntityKey> _targetKeys = [];

    public async Task OnDialogOpenedAsync(IDialogParameters? parameters, CancellationToken cancellationToken)
    {
        parameters ??= new DialogParameters();
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
        var result = new DialogHostResult
        {
            Result = DialogButtonResult.Yes,
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
        DialogHost.Close(DialogHostName ?? "Root", new DialogHostResult { Result = DialogButtonResult.Cancel });
    }
}
