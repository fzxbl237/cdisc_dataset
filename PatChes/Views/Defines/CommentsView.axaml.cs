using AsyncNavigation.Abstractions;
using Avalonia;
using Avalonia.Controls;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PatChes.ViewModels.Defines;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;

namespace PatChes.Views.Defines;

public partial class CommentsView : UserControl, IActivatableView, IView
{
    public CommentsView()
    {
        InitializeComponent();
        this.WhenActivated((MultipleDisposable disposables) =>
        {
            if (DataContext is CommentsViewModel vm)
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