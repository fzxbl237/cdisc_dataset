using Avalonia;
using Avalonia.Controls;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using cdisc_dataset.ViewModels.Defines;
using ReactiveUI;

namespace cdisc_dataset.Views.Defines;

public partial class CommentsView : UserControl, IActivatableView
{
    public CommentsView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            if (DataContext is CommentsViewModel vm)
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