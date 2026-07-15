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
                    vm.IsLoading = true;
                    await Task.Delay(250);
                    await vm.LoadDataAsync();
                    vm.IsLoading = false;
                }, DispatcherPriority.Background);
            }
        });
    }
}
