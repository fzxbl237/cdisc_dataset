using System.Reactive.Linq;
using System.Threading.Tasks;
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
                    await Task.Delay(300);
                    await vm.LoadCodeLists();
                    await vm.LoadComments();
                    await vm.LoadTerminologies();
                });

            }
        });
    }
}