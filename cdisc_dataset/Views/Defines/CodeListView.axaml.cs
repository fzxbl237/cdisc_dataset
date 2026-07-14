using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using cdisc_dataset.ViewModels.Defines;
using ReactiveUI;

namespace cdisc_dataset.Views.Defines;

public partial class CodeListView : UserControl,IActivatableView
{
    public CodeListView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            if (DataContext is CodeListViewModel vm)
            {
                Observable.StartAsync(async () =>
                {
                    await vm.LoadCodeLists();
                    await vm.LoadComments();
                });

            }
        });
    }
}