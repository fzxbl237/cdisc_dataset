using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using cdisc_dataset.ViewModels.Defines;
using Microsoft.Identity.Client;
using ReactiveUI;

namespace cdisc_dataset.Views.Defines;

public partial class VariablesView : UserControl,IActivatableView
{
    public VariablesView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            if (DataContext is VariablesViewModel vm)
            {
                Observable.StartAsync(async () =>
                {
                   await vm.LoadVariablesAsync();
                });

            }
        });
    }
}