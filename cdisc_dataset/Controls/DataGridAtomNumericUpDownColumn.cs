using System;
using System.Collections.Generic;
using AtomUI.Data;
using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using DataGridBoundColumn = Avalonia.Controls.DataGridBoundColumn;
using DataGridCell = Avalonia.Controls.DataGridCell;
using NumericUpDown = AtomUI.Desktop.Controls.NumericUpDown;
using TextBlock = Avalonia.Controls.TextBlock;
using TextBox = Avalonia.Controls.TextBox;

namespace cdisc_dataset.Controls;

public class DataGridAtomNumericUpDownColumn:DataGridBoundColumn
{
    public static readonly StyledProperty<Decimal?> MinimumProperty =
        AvaloniaProperty.Register<DataGridAtomNumericUpDownColumn, Decimal?>(nameof(Minimum));
    
    public Decimal? Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }
    
    public static readonly StyledProperty<Decimal?> MaximumProperty =
        AvaloniaProperty.Register<DataGridAtomNumericUpDownColumn, Decimal?>(nameof(Maximum));
    
    public Decimal? Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }
    
    public static readonly StyledProperty<Decimal?> IncrementProperty =
        AvaloniaProperty.Register<DataGridAtomNumericUpDownColumn, Decimal?>(nameof(Increment));
    
    public Decimal? Increment
    {
        get => GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }
    
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
    
    public DataGridAtomNumericUpDownColumn()
    {
        BindingTarget = Avalonia.Controls.NumericUpDown.ValueProperty;
    }

    protected override object PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs)
    {
        if (editingElement is NumericUpDown numericUpDown)
        {
            numericUpDown.Focus();
            string uneditedText = numericUpDown.Value.ToString() ?? string.Empty;
            return uneditedText;
        }

        return string.Empty;
    }

    protected override Control GenerateEditingElementDirect(DataGridCell cell, object dataItem)
    {
        var numericUpDown = new NumericUpDown()
        {
            Name = "CellNumericUpDown",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 2, 5, 2),
        };
        
        SyncEditProperties(numericUpDown);
        
        return numericUpDown;
    }
    
    private void SyncEditProperties(NumericUpDown numericUpDown)
    {
        // if (ItemsSource != null)
        // {
        //     autoComplete.ItemsSource = ItemsSource;
        // }

        BindUtils.RelayBind(this, MinimumProperty, numericUpDown, Avalonia.Controls.NumericUpDown.MinimumProperty);
        BindUtils.RelayBind(this, MaximumProperty, numericUpDown, Avalonia.Controls.NumericUpDown.MaximumProperty);      
        BindUtils.RelayBind(this, IncrementProperty, numericUpDown, Avalonia.Controls.NumericUpDown.IncrementProperty);

    }
}