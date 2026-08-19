using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using DialogHostAvalonia;
using Microsoft.Extensions.DependencyInjection;
using AsyncNavigation.Abstractions;
using AsyncNavigation.Core;

namespace PatChes.Services;

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
        DialogOpenedEventHandler eventHandler = async (_, eventArgs) =>
        {
            await viewModel.OnDialogOpenedAsync(parameters, default);
            eventArgs.Session.UpdateContent(dialogContent);
        };
        var dialogResult = (IDialogResult)await DialogHost.Show(dialogContent, dialogHostName, eventHandler);
        await viewModel.OnDialogClosingAsync(dialogResult, default);
        await viewModel.OnDialogClosedAsync(dialogResult, default);

        if (dialogResult.Parameters != null)
            return dialogResult;

        return new DialogHostResult
        {
            Result = dialogResult.Result,
            Status = dialogResult.Status
        };
    }
}