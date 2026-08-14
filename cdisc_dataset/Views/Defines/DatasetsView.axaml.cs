using AsyncNavigation.Abstractions;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using cdisc_dataset.ViewModels.Defines;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;

namespace cdisc_dataset.Views.Defines;

public partial class DatasetsView : UserControl, IActivatableView, IView
{
    public DatasetsView()
    {
        InitializeComponent();
        this.WhenActivated((MultipleDisposable disposables) =>
        {
            if (DataContext is DatasetsViewModel vm && !vm.IsInitialLoadCompleted)
                _ = LoadAsync(vm);
        });
    }

    private async Task LoadAsync(DatasetsViewModel vm)
    {
        var sw = Stopwatch.StartNew();
        await vm.ExecuteLoadingAsync(async () =>
        {
            await vm.LoadInitialDataAsync();
        });
        var dataReadyMs = sw.ElapsedMilliseconds;
        Debug.WriteLine($"[PerfTrace] datasets-data-ready={dataReadyMs}ms");

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Debug.WriteLine($"[PerfTrace] datasets-ui-ready total={sw.ElapsedMilliseconds}ms data-ready={dataReadyMs}ms render={sw.ElapsedMilliseconds - dataReadyMs}ms");
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }
}