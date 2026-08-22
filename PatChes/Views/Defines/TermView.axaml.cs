using AsyncNavigation.Abstractions;
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PatChes.ViewModels.Defines;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;

namespace PatChes.Views.Defines;

public partial class TermView : UserControl, IActivatableView, IView
{
    public TermView()
    {
        InitializeComponent();
        //TermsGrid.PreparingCellForEdit += OnPreparingCellForEdit;
        this.WhenActivated((MultipleDisposable disposables) =>
        {
            if (DataContext is TermViewModel vm)
            {
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await vm.ExecuteLoadingAsync(async () =>
                    {
                        await Task.Delay(250);
                        await vm.LoadTermsAsync();
                    });
                }, DispatcherPriority.Background);
            }
        });
    }

    // private void OnPreparingCellForEdit(object? sender, PatChes.Controls.DataGrid.DataGridPreparingCellForEditEventArgs e)
    // {
    //     if (DataContext is TermViewModel vm)
    //         vm.PreparingCellForEditCommand.Execute(e);
    // }
    
}