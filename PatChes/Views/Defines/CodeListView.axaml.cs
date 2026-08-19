using AsyncNavigation.Abstractions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PatChes.ViewModels.Defines;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;

namespace PatChes.Views.Defines;

public partial class CodeListView : UserControl, IActivatableView, IView
{
    public CodeListView()
    {
        InitializeComponent();
        this.WhenActivated((MultipleDisposable disposables)  =>
        {
            if (DataContext is CodeListViewModel vm)
            {
                // ʹ�� Post �ӳٵ� Dispatcher ����ʱִ�У��� TabStrip ���������
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await vm.ExecuteLoadingAsync(async () =>
                    {
                        await Task.Delay(250); // �����ӳ�ȷ��������ʼ
                        await vm.LoadCodeLists();
                        await vm.LoadTerminologies();
                    });

                }, DispatcherPriority.Background);
            }
        });
    }
}