using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using cdisc_dataset.ViewModels.Defines;
using ReactiveUI;

namespace cdisc_dataset.Views.Defines;

public partial class DocumentsView : UserControl, IActivatableView
{
    public DocumentsView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            if (DataContext is DocumentsViewModel vm)
            {
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await vm.ExecuteLoadingAsync(async () =>
                    {
                        await Task.Delay(250);
                        await vm.LoadDataAsync();
                    });
                }, DispatcherPriority.Background);
            }
        });
    }
}
