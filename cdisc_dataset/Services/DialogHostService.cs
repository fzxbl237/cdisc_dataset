using System;
using System.Threading.Tasks;
using DialogHostAvalonia;
using Prism.Dialogs;
using Prism.Ioc;
using Prism.Mvvm;
using Dialog = AtomUI.Desktop.Controls.Dialog;

namespace cdisc_dataset.Services;

public class DialogHostService: DialogService, IDialogHostService
{
    private readonly IContainerExtension containerExtension;

    public DialogHostService(IContainerExtension containerExtension) : base(containerExtension)
    {
        this.containerExtension = containerExtension;
    }
    
    public async Task<IDialogResult> ShowDialog(string name, IDialogParameters? parameters, string dialogHostName = "Root")
    {
        parameters ??= new DialogParameters();

        //从容器当中去除弹出窗口的实例
        var content = containerExtension.Resolve<object>(name);

        //验证实例的有效性 
        if (!(content is Avalonia.Controls.Control dialogContent))
            throw new NullReferenceException("A dialog's content must be an Avalonia.Controls.Control");

        if (dialogContent is { DataContext: null } view && ViewModelLocator.GetAutoWireViewModel(view) is null)
            ViewModelLocator.SetAutoWireViewModel(view, true);

        if (!(dialogContent.DataContext is IDialogHostAware viewModel))
            throw new NullReferenceException("A dialog's ViewModel must implement the IDialogAware interface");

        viewModel.DialogHostName = dialogHostName;

        DialogOpenedEventHandler eventHandler = (sender, eventArgs) =>
        {
            if (viewModel is IDialogHostAware aware)
            {
                aware.OnDialogOpened(parameters);
            }
            eventArgs.Session.UpdateContent(content);
        };

        return (IDialogResult)await DialogHost.Show(dialogContent, viewModel.DialogHostName, eventHandler);
    }
    
    
}