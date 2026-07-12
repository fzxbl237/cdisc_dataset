using Avalonia;
using System;
using System.Reflection;
using AtomUI.Controls;
using AtomUI.Theme;
using AtomUI.Theme.Language;
using ReactiveUI.Avalonia;

namespace cdisc_dataset;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI(rxAppBuilder =>
            { 
                // Enable ReactiveUI
                rxAppBuilder
                    .WithViewsFromAssembly(Assembly.GetExecutingAssembly())
                    ;
            })
            .With(new Win32PlatformOptions())
            .LogToTrace();
    }
}