using AsyncNavigation.Abstractions;
using Avalonia;
using Avalonia.Controls;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using cdisc_dataset.ViewModels.Defines;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;

namespace cdisc_dataset.Views.Defines;

public partial class DictionariesView : UserControl, IActivatableView, IView
{
    public DictionariesView()
    {
        InitializeComponent();
        this.WhenActivated((MultipleDisposable disposables) =>
        {
            if (DataContext is DictionariesViewModel vm)
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