using System.Collections;
using AtomUI.Data;
using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ComboBox = AtomUI.Desktop.Controls.ComboBox;
using DataGridBoundColumn = Avalonia.Controls.DataGridBoundColumn;
using DataGridCell = Avalonia.Controls.DataGridCell;
using TextBlock = Avalonia.Controls.TextBlock;
using TextBox = Avalonia.Controls.TextBox;

namespace cdisc_dataset.Controls;

public class DataGridAtomComboBoxColumn:DataGridBoundColumn
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<DataGridAtomComboBoxColumn, IEnumerable?>(nameof(ItemsSource));
    
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
    protected override Control GenerateElement(DataGridCell cell, object dataItem)
    {
        var textBlock = new AtomUI.Desktop.Controls.TextBlock()
        {
            Name = "ComboBoxTextBlock",
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(12,0,12,0),
            TextWrapping = TextWrapping.Wrap,
            
        };

        if (Binding != null && dataItem != DataGridCollectionView.NewItemPlaceholder)
        {
            textBlock.Bind(TextBlock.TextProperty, Binding);
        }

        return textBlock;
    }
    
    public DataGridAtomComboBoxColumn()
    {
        BindingTarget = SelectingItemsControl.SelectedItemProperty;
    }

    protected override object PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs)
    {
        if (editingElement is ComboBox comboBox)
        {
            string uneditedText = comboBox.Text ?? string.Empty;
            return uneditedText;
        }

        return string.Empty;
    }

    protected override Control GenerateEditingElementDirect(DataGridCell cell, object dataItem)
    {
        var comboBox = new ComboBox()
        {
            Name = "CellComboBox",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(5, 2, 5, 2),
        };
        SyncEditProperties(comboBox);
        
        
        return comboBox;
    }
    
    private void SyncEditProperties(ComboBox comboBox)
    {
        BindUtils.RelayBind(this, ItemsSourceProperty, comboBox, ItemsControl.ItemsSourceProperty);

    }
}