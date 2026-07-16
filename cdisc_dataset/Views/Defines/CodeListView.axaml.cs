using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
                // 使用 Post 延迟到 Dispatcher 空闲时执行，让 TabStrip 动画先完成
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await vm.ExecuteLoadingAsync(async () =>
                    {
                        await Task.Delay(250); // 短暂延迟确保动画开始
                        await vm.LoadCodeLists();
                        await vm.LoadComments();
                        await vm.LoadTerminologies();
                    });

                }, DispatcherPriority.Background);
            }
        });
    }
}