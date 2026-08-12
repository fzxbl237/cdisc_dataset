using AsyncNavigation;
using AsyncNavigation.Avalonia;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace cdisc_dataset.Navigation;

public sealed class DefineNavigationIndicatorProvider : IInnerIndicatorProvider
{
    private const string DefineRegionName = "SdtmDefineRegion";

    public bool HasLoadingIndicator(NavigationContext navigationContext)
    {
        return navigationContext.RegionName == DefineRegionName;
    }

    public Control GetLoadingIndicator(NavigationContext navigationContext)
    {
        var message = new TextBlock
        {
            Text = $"Loading {navigationContext.ViewName}...",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#303133")),
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var progressBar = new ProgressBar
        {
            IsIndeterminate = true,
            Width = 180,
            Height = 4,
            Margin = new Thickness(0, 12, 0, 0),
        };

        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                message,
                progressBar,
            },
        };

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F8FAFC")),
            Child = panel,
        };
    }

    public bool HasErrorIndicator(NavigationContext navigationContext)
    {
        return false;
    }

    public Control GetErrorIndicator(NavigationContext navigationContext)
    {
        return new Grid();
    }
}
