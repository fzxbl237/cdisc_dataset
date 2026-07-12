using System.Reactive.Disposables;
using AtomUI.Data;
using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Templates;

namespace cdisc_dataset.Controls;

public class Window:AtomUI.Desktop.Controls.Window
{
    private static readonly StyledProperty<object?> RightAddOnProperty =
        AvaloniaProperty.Register<Window, object?>(nameof(RightAddOn));
    
    public object? RightAddOn
    {
        get => GetValue(RightAddOnProperty);
        set => SetValue(RightAddOnProperty, value);
    }

    protected override void NotifyConfigureTitleBar(WindowTitleBar titleBar)
    {
        base.NotifyConfigureTitleBar(titleBar);
        BindUtils.RelayBind(this, RightAddOnProperty, titleBar, WindowTitleBar.RightAddOnProperty);
    }

    // protected override WindowTitleBar? NotifyCreateTitleBar(WindowTitleBar? oldTitleBar, CompositeDisposable disposables)
    // {
    //     var windowTitleBar = new WindowTitleBar{Name = "New Title Bar"};
    //     disposables.Add(BindUtils.RelayBind(this,RightAddOnProperty,windowTitleBar,WindowTitleBar.RightAddOnProperty));
    //     return windowTitleBar;
    // }
}