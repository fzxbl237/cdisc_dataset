using System;
using System.Collections.Generic;
using AtomUI.Controls.Utils;
using AtomUI.Data;
using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using DataGridBoundColumn = Avalonia.Controls.DataGridBoundColumn;
using DataGridCell = Avalonia.Controls.DataGridCell;
using TextBlock = Avalonia.Controls.TextBlock;

namespace cdisc_dataset.Controls;

public class DataGridAtomAutoCompleteColumn : DataGridBoundColumn
{
    
    public static readonly StyledProperty<ICompleteOptionsAsyncLoader?> OptionsAsyncLoaderProperty =
        AvaloniaProperty.Register<DataGridAtomAutoCompleteColumn, ICompleteOptionsAsyncLoader?>(nameof(OptionsAsyncLoader));
    
    public ICompleteOptionsAsyncLoader? OptionsAsyncLoader
    {
        get => GetValue(OptionsAsyncLoaderProperty);
        set => SetValue(OptionsAsyncLoaderProperty, value);
    }
    
    public static readonly StyledProperty<IEnumerable<IAutoCompleteOption>?> OptionsSourceProperty =
        AvaloniaProperty.Register<DataGridAtomAutoCompleteColumn, IEnumerable<IAutoCompleteOption>?>(nameof(OptionsSource));
    
    public IEnumerable<IAutoCompleteOption>? OptionsSource
    {
        get => GetValue(OptionsSourceProperty);
        set => SetValue(OptionsSourceProperty, value);
    }
    
    
    
    public static readonly StyledProperty<IDataTemplate?> OptionTemplateProperty =
        AvaloniaProperty.Register<DataGridAtomAutoCompleteColumn, IDataTemplate?>(nameof(OptionTemplate));
    
    public IDataTemplate? OptionTemplate
    {
        get => GetValue(OptionTemplateProperty);
        set => SetValue(OptionTemplateProperty, value);
    }
    
    public DataGridAtomAutoCompleteColumn()
    {
        BindingTarget = AbstractAutoComplete.ValueProperty;
    }
    
    protected override Control GenerateElement(DataGridCell cell, object dataItem)
    {
        var textBlock = new TextBlock
        {
            Name = "CellAutoCompleteTextBlock",
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(12,0,12,0)
        };

        if (Binding != null && dataItem != DataGridCollectionView.NewItemPlaceholder)
        {
            textBlock.Bind(TextBlock.TextProperty, Binding);
        }

        return textBlock;
    }

    protected override object PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs)
    {
        if (editingElement is AutoComplete autoComplete)
        {
            string uneditedText = autoComplete.Value ?? string.Empty;

            // Select all text for easy replacement
            autoComplete.Focus();
            return uneditedText;
        }

        return string.Empty;
    }
    

    protected override Control GenerateEditingElementDirect(DataGridCell cell, object dataItem)
    {
        var autoComplete = new AutoComplete()
        {
            Name = "CellAutoCompleteBox",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 2, 5, 2),
            IsPopupMatchSelectWidth = true,
            MinimumPrefixLength = 0,
            Filter = ValueFilterFactory.BuildFilter(ValueFilterMode.Contains)
        };
        SyncEditProperties(autoComplete);
        return autoComplete;
    }
    
    // protected override void RefreshCellContent(Control element, string propertyName)
    // {
    //     ArgumentNullException.ThrowIfNull(element);
    //
    //     if (element is AutoComplete autoComplete)
    //     {
    //         SyncEditProperties(autoComplete);
    //     }
    //     else
    //     {
    //         base.RefreshCellContent(element, propertyName);
    //     }
    // }
    //
    // protected override void CancelCellEdit(Control editingElement, object uneditedValue)
    // {
    //     if (editingElement is AutoComplete autoComplete)
    //     {
    //         autoComplete.Value = uneditedValue as string ?? string.Empty;
    //     }
    // }
    //
    // protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    // {
    //     base.OnPropertyChanged(change);
    //
    //     if (change.Property == OptionsAsyncLoaderProperty
    //         || change.Property == OptionTemplateProperty)
    //     {
    //         NotifyPropertyChanged(change.Property.Name);
    //     }
    // }
    
    private void SyncEditProperties(AutoComplete autoComplete)
    {
        // if (ItemsSource != null)
        // {
        //     autoComplete.ItemsSource = ItemsSource;
        // }

        BindUtils.RelayBind(this, OptionsAsyncLoaderProperty, autoComplete, AbstractAutoComplete.OptionsAsyncLoaderProperty);
        BindUtils.RelayBind(this, OptionsSourceProperty, autoComplete, AbstractAutoComplete.OptionsSourceProperty);      
        BindUtils.RelayBind(this, OptionTemplateProperty, autoComplete, AbstractAutoComplete.OptionTemplateProperty);

    }
}