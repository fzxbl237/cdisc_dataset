using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ComboBox = AtomUI.Desktop.Controls.ComboBox;
using DataGridBoundColumn = Avalonia.Controls.DataGridBoundColumn;
using DataGridCell = Avalonia.Controls.DataGridCell;
using TextBlock = Avalonia.Controls.TextBlock;
using TextBox = Avalonia.Controls.TextBox;

namespace cdisc_dataset.Controls;

public class DataGridAtomLineEditColumn:DataGridBoundColumn
{
    protected override Control GenerateElement(DataGridCell cell, object dataItem)
    {
        var textBlock = new AtomUI.Desktop.Controls.TextBlock()
        {
            Name = "CellAutoCompleteTextBlock",
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
    
    public DataGridAtomLineEditColumn()
    {
        BindingTarget = TextBox.TextProperty;
    }

    protected override object PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs)
    {
        if (editingElement is LineEdit lineEdit)
        {
            string uneditedText = lineEdit.Text ?? string.Empty;

            // Select all text for easy replacement
            lineEdit.Focus();
            lineEdit.CaretIndex = uneditedText.Length;
            return uneditedText;
        }

        return string.Empty;
    }

    protected override Control GenerateEditingElementDirect(DataGridCell cell, object dataItem)
    {
        var lineEdit = new LineEdit()
        {
            Name = "CellAutoCompleteBox",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 2, 5, 2),
        };
        
        return lineEdit;
    }
}