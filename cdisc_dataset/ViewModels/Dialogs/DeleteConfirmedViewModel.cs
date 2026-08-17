using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using cdisc_dataset.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class DeleteConfirmedViewModel : ObservableObject, IDialogHostAware
{
    public string DialogHostName { get; set; } = "Root";

    [ObservableProperty]
    private string _title = "Delete referenced item?";

    [ObservableProperty]
    private string _confirmText = "删除数据并清空引用";

    [ObservableProperty]
    private string _preserveReferencesText = "仅删除数据，保留引用";

    [ObservableProperty]
    private ObservableCollection<ReferenceGroup> _referenceGroups = [];

    public bool HasReferences => ReferenceGroups.Count > 0;

    public void OnDialogOpened(IDialogParameters parameters)
    {
        parameters.TryGetValue("Title", out string? title);
        parameters.TryGetValue("EntityType", out string? entityType);
        parameters.TryGetValue("References", out Dictionary<string, string>? references);

        Title = title ?? Title;
        ConfirmText = "删除模型并清空引用";
        PreserveReferencesText = "仅删除模型，保留引用";
        ReferenceGroups = new ObservableCollection<ReferenceGroup>(
            (references ?? [])
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Value))
            .Select(reference => new ReferenceGroup(reference.Key, reference.Value)));
        OnPropertyChanged(nameof(HasReferences));
    }

    [RelayCommand]
    private void PreserveReferences()
    {
        DialogHost.Close(DialogHostName, new DialogResult(ButtonResult.Yes));
    }

    [RelayCommand]
    private void Save()
    {
        DialogHost.Close(DialogHostName, new DialogResult(ButtonResult.OK));
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName, new DialogResult { Result = ButtonResult.Cancel });
    }
}

public sealed record ReferenceGroup(string Name, string Value);
