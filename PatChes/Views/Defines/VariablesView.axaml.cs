using System.Threading.Tasks;
using AsyncNavigation.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using PatChes.ViewModels.Defines;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;

namespace PatChes.Views.Defines;

public partial class VariablesView : UserControl, IActivatableView, IView
{
    public VariablesView()
    {
        InitializeComponent();
        this.WhenActivated((MultipleDisposable disposables) =>
        {
            if (DataContext is VariablesViewModel vm)
            {
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await vm.ExecuteLoadingAsync(async () =>
                    {
                        await Task.Delay(250);
                        await vm.LoadVariablesAsync();
                    });
                });
            }
        });
    }
}