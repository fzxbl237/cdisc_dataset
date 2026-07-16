using Avalonia.Controls;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using cdisc_dataset.ViewModels.Defines;
using ReactiveUI;

namespace cdisc_dataset.Views.Defines;

public partial class ValueLevelsView : UserControl, IActivatableView
{
    public ValueLevelsView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            if (DataContext is ValueLevelsViewModel vm)
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
