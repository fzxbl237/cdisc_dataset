using AsyncNavigation.Abstractions;
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using cdisc_dataset.ViewModels.Defines;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;

namespace cdisc_dataset.Views.Defines;

public partial class TermView : UserControl, IActivatableView, IView
{
    public TermView()
    {
        InitializeComponent();
        this.WhenActivated((MultipleDisposable disposables) =>
        {
            if (DataContext is TermViewModel vm)
            {
                // ʹ�� Post �ӳٵ� Dispatcher ����ʱִ�У��� TabStrip ���������
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await vm.ExecuteLoadingAsync(async () =>
                    {
                        await Task.Delay(250); // �ӳ�ȷ����������
                        await vm.LoadTermsAsync();
                    });
                }, DispatcherPriority.Background);
            }
        });
    }
    
}