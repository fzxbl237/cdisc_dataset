using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using DialogHostAvalonia;
using Microsoft.Extensions.DependencyInjection;
using Prism.Dialogs;

namespace cdisc_dataset.Services;

public class DialogHostService : IDialogHostService
{
    private readonly IServiceProvider _serviceProvider;

    public DialogHostService(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public async Task<IDialogResult> ShowDialogAsync(string name, IDialogParameters? parameters, string dialogHostName = "Root")
    {
        parameters ??= new DialogParameters();
        var dialogContent = _serviceProvider.GetRequiredKeyedService<Control>(name);
        if (dialogContent.DataContext is not IDialogHostAware viewModel)
            throw new InvalidOperationException("A dialog's ViewModel must implement IDialogHostAware.");

        viewModel.DialogHostName = dialogHostName;
        DialogOpenedEventHandler eventHandler = (_, eventArgs) =>
        {
            viewModel.OnDialogOpened(parameters);
            eventArgs.Session.UpdateContent(dialogContent);
        };
        return (IDialogResult)await DialogHost.Show(dialogContent, dialogHostName, eventHandler);
    }
}