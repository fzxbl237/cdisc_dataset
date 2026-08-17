using System;
using AtomUI.Desktop.Controls;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace cdisc_dataset.Views;

public partial class DefinePreviewWindow : Controls.Window
{
    private string? _html;
    private NativeWebView? _webView;
    private Control? _loadingSpin;

    public DefinePreviewWindow(string? html = null)
    {
        _html = html;
        InitializeComponent();
        Opened += OnOpened;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnOpened(object? sender, EventArgs e)
    {
        _webView = this.FindControl<NativeWebView>("WebView")
            ?? throw new InvalidOperationException("NativeWebView control was not found in the preview window.");
        _loadingSpin = this.FindControl<Control>("LoadingSpin")
            ?? throw new InvalidOperationException("LoadingSpin control was not found in the preview window.");

        if (_html != null)
            Dispatcher.UIThread.Post(() => NavigateToHtml(_webView, _html), DispatcherPriority.Loaded);
    }

    public void SetHtml(string html)
    {
        _html = html;
        if (_webView == null)
            return;

        Dispatcher.UIThread.Post(() => NavigateToHtml(_webView, html), DispatcherPriority.Loaded);
    }

    private void NavigateToHtml(NativeWebView webView, string html)
    {
        webView.NavigateToString(html, new Uri("about:blank"));
        webView.IsVisible = true;
        if (_loadingSpin != null && _loadingSpin is Spin spinner)
            spinner.IsSpinning = false;
    }
}
