using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using cdisc_dataset.ViewModels.Defines;
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
                // 使用 Post 延迟到 Dispatcher 空闲时执行，让 TabStrip 动画先完成
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await Task.Delay(250); // 延迟确保动画流畅
                    await vm.LoadVariablesAsync();
                    await vm.LoadLookups();
                }, DispatcherPriority.Background);
            }
        });
    }
}