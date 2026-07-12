using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using cdisc_dataset.ViewModels;

namespace cdisc_dataset.Views;

public partial class SdtmDefineView : UserControl
{
    public SdtmDefineView()
    {
        InitializeComponent();
    }
    
    // protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    // {
    //     base.OnAttachedToVisualTree(e);
    //     var topLevel = TopLevel.GetTopLevel(this);
    //     if (this.DataContext is SdtmDefineViewModel viewModel)
    //     {
    //         viewModel.WindowMessageManager = new WindowMessageManager(topLevel)
    //         {
    //             MaxItems = 10
    //         };
    //     }
    //
    // }
}