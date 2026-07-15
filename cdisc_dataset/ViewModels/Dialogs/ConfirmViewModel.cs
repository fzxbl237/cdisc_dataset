using cdisc_dataset.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Prism.Dialogs;

namespace cdisc_dataset.ViewModels.Dialogs;

public partial class ConfirmViewModel : ObservableObject, IDialogHostAware
{
    public string DialogHostName { get; set; } = "Root";

    [ObservableProperty]
    private string _title = "Confirm";

    [ObservableProperty]
    private string _message = "Are you sure?";

    public void OnDialogOpened(IDialogParameters parameters)
    {
        if (parameters.TryGetValue("Title", out string? title) && !string.IsNullOrWhiteSpace(title))
        {
            Title = title;
        }

        if (parameters.TryGetValue("Message", out string? message) && !string.IsNullOrWhiteSpace(message))
        {
            Message = message;
        }
    }

    [RelayCommand]
    private void Confirm()
    {
        DialogHost.Close(DialogHostName, new DialogResult(ButtonResult.OK));
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogHost.Close(DialogHostName, new DialogResult(ButtonResult.Cancel));
    }
}
