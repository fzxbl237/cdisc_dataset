using AsyncNavigation.Abstractions;
using System;
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
        await vm.ExecuteLoadingAsync(async () =>
        {
            await vm.LoadInitialDataAsync();
        });
    }
}