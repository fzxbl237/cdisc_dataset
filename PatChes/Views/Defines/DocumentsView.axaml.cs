using AsyncNavigation.Abstractions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using PatChes.ViewModels.Defines;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;

namespace PatChes.Views.Defines;

public partial class DocumentsView : UserControl, IActivatableView, IView
{
    public DocumentsView()
    {
        InitializeComponent();
        this.WhenActivated((MultipleDisposable disposables) =>
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
