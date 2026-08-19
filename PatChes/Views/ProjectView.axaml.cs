using AsyncNavigation.Abstractions;
using AtomUI.Controls;
using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PatChes.ViewModels;

namespace PatChes.Views;

public partial class ProjectView : UserControl, IView
{
    public ProjectView()
    {
        InitializeComponent();
    }
}