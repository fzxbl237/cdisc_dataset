using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AtomUI.Controls;
using AtomUI.Controls.Data;
using AtomUI.Controls.Utils;
using cdisc_dataset.Services;
using cdisc_dataset.Services.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class AssignCommentVariablesViewModel(IVariableService variableService) : ObservableObject, IDialogHostAware
{
    public string? DialogHostName { get; set; } = "Root";

    public DefaultFilterValueSelector VariableFilterValueSelector { get; } = data =>
        (data as IListItemData)?.Content;

    [ObservableProperty]
    private List<IListItemData> _variables = [];

    [ObservableProperty]
    private ObservableCollection<EntityKey> _targetKeys = [];

    public async void OnDialogOpened(IDialogParameters parameters)
    {
        var variables = await variableService.GetAllVariableDtosAsync();
        Variables = variables
            .Where(variable => variable.CommentId == 0)
            .OrderBy(variable => variable.DatasetName)
            .ThenBy(variable => variable.Order)
            .ThenBy(variable => variable.VariableName)
            .Select(variable => (IListItemData)new ListItemData
            {
                ItemKey = variable.Id.ToString(),
                Content = $"{variable.DatasetName}.{variable.VariableName} - {variable.Label}"
            })
            .ToList();
        TargetKeys.Clear();
    }

    [RelayCommand]
    private void Confirm()
    {
        var variableIds = TargetKeys
            .Select(key => int.TryParse(key.Value, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
        DialogHost.Close(DialogHostName ?? "Root", new DialogResult
        {
            Result = ButtonResult.Yes,
            Parameters = new DialogParameters { { "VariableIds", variableIds } }
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName ?? "Root", new DialogResult { Result = ButtonResult.Cancel });
    }
}
