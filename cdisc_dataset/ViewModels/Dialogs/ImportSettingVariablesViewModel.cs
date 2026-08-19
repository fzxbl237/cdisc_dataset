using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AtomUI.Controls;
using AtomUI.Controls.Data;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class ImportSettingVariablesViewModel(IVariableService variableService) : ObservableObject, IDialogHostAware
{
    public string? DialogHostName { get; set; } = "Root";

    [ObservableProperty]
    private List<IListItemData> _variables = [];

    [ObservableProperty]
    private ObservableCollection<EntityKey> _targetKeys = [];

    public async Task OnDialogOpenedAsync(IDialogParameters? parameters, CancellationToken cancellationToken)
    {
        parameters ??= new DialogParameters();
        var templates = await variableService.GetAvailableSettingVariableTemplatesAsync();
        Variables = templates
            .Select(template => (IListItemData)new ListItemData
            {
                ItemKey = template.Id.ToString(),
                Content = $"{template.DatasetName}.{template.VariableName} - {template.Label}"
            })
            .ToList();
        TargetKeys.Clear();
    }

    [RelayCommand]
    private void Save()
    {
        DialogHost.Close(DialogHostName ?? "Root", new DialogHostResult
        {
            Result = DialogButtonResult.Yes,
            Parameters = new DialogParameters
            {
                {
                    "TemplateVariableIds",
                    TargetKeys
                        .Select(key => int.TryParse(key.Value, out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList()
                }
            }
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName ?? "Root", new DialogHostResult { Result = DialogButtonResult.Cancel });
    }
}
