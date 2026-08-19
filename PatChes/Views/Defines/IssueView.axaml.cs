using System.Threading.Tasks;
using AsyncNavigation.Abstractions;
using Avalonia.Controls;
using Avalonia.Threading;
using PatChes.ViewModels.Defines;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;

namespace PatChes.Views.Defines;

public partial class IssueView : UserControl, IActivatableView, IView
{
    public IssueView()
    {
        InitializeComponent();
        this.WhenActivated((MultipleDisposable disposables) =>
        {
            if (DataContext is IssueViewModel vm)
            {
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await vm.ExecuteLoadingAsync(vm.LoadDataAsync);
                }, DispatcherPriority.Background);
            }
        });
    }
}
