using System.Threading.Tasks;
using AsyncNavigation.Abstractions;
using Avalonia.Controls;
using Avalonia.Threading;
using PatChes.ViewModels.Defines;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;

namespace PatChes.Views.Defines;

public partial class DefineIssueView : UserControl, IActivatableView, IView
{
    public DefineIssueView()
    {
        InitializeComponent();
        this.WhenActivated((MultipleDisposable disposables) =>
        {
            if (DataContext is DefineIssueViewModel vm)
            {
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await vm.ExecuteLoadingAsync(vm.LoadDataAsync);
                }, DispatcherPriority.Background);
            }
        });
    }
}
