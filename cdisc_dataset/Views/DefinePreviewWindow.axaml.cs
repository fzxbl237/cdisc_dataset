using System;
using AtomUI.Desktop.Controls;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using cdisc_dataset.Utils;

namespace cdisc_dataset.Views;

public partial class DefinePreviewWindow : Controls.Window
{
    private string? _html;
    private NativeWebView? _webView;
    private Control? _loadingSpin;

    public DefinePreviewWindow(string? html = null)
    {
        _html = html;
        DefinePreviewDiagnostics.Info($"Preview window constructing. HtmlLength={_html?.Length ?? 0}.");
        InitializeComponent();
        DefinePreviewDiagnostics.Info("Preview window XAML initialized.");
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            _webView = this.FindControl<NativeWebView>("WebView")
                ?? throw new InvalidOperationException("NativeWebView control was not found in the preview window.");
            _loadingSpin = this.FindControl<Control>("LoadingSpin")
                ?? throw new InvalidOperationException("LoadingSpin control was not found in the preview window.");
            DefinePreviewDiagnostics.Info("Preview window opened. NativeWebView found; scheduling navigation.");

            if (_html != null)
                Dispatcher.UIThread.Post(() => NavigateToHtml(_webView, _html), DispatcherPriority.Loaded);
        }
        catch (Exception exception)
        {
            DefinePreviewDiagnostics.Error("Preview window initialization failed.", exception);
            throw;
        }
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
        try
        {
            DefinePreviewDiagnostics.Info("Starting WebView navigation.");
            webView.NavigateToString(html, new Uri("about:blank"));
            webView.IsVisible = true;
            if (_loadingSpin != null && _loadingSpin is Spin spinner)
                spinner.IsSpinning = false;
            DefinePreviewDiagnostics.Info("WebView NavigateToString returned; loading spin hidden.");
        }
        catch (Exception exception)
        {
            DefinePreviewDiagnostics.Error("WebView NavigateToString failed.", exception);
            throw;
        }
    }

    private void OnClosed(object? sender, EventArgs e) =>
        DefinePreviewDiagnostics.Info("Preview window closed.");
}
