using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Window = cdisc_dataset.Controls.Window;

namespace cdisc_dataset.Views;

public partial class MainWindow : Window
{
    private bool _isPointerPressedOnTitleBar;

    private PointerPressedEventArgs? _lastMousePressedEventArgs;
    

    public MainWindow()
    {
        InitializeComponent();
    }
    
    private void InitializeComponent(){
        AvaloniaXamlLoader.Load(this);
    }
    

    private void TitleBarOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPointerPressedOnTitleBar = true;
            _lastMousePressedEventArgs = e;
        }
    }

    private void TitleBarOnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerPressedOnTitleBar || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _isPointerPressedOnTitleBar = false;
        if(_lastMousePressedEventArgs!=null)
            BeginMoveDrag(_lastMousePressedEventArgs);
    }

    private void TitleBarOnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPointerPressedOnTitleBar = false;
    }

    private void TitleBarOnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isPointerPressedOnTitleBar = false;
    }
}