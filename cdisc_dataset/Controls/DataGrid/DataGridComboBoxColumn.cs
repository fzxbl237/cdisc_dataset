using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ComboBox = AtomUI.Desktop.Controls.ComboBox;

namespace cdisc_dataset.Controls.DataGrid;

public class DataGridComboBoxColumn : DataGridBoundColumn
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<DataGridComboBoxColumn, IEnumerable?>(nameof(ItemsSource));
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<DataGridComboBoxColumn, double>(nameof(FontSize), 13);
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<DataGridComboBoxColumn, IBrush?>(nameof(Foreground));
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<DataGridComboBoxColumn, IDataTemplate?>(nameof(ItemTemplate));
    public static readonly StyledProperty<double> MaxDropDownHeightProperty =
        AvaloniaProperty.Register<DataGridComboBoxColumn, double>(nameof(MaxDropDownHeight), 300);

    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
    public double MaxDropDownHeight { get => GetValue(MaxDropDownHeightProperty); set => SetValue(MaxDropDownHeightProperty, value); }

    public DataGridComboBoxColumn() { BindingTarget = ComboBox.SelectedItemProperty; }

    public override Control GenerateElement(DataGridCell cell, object? dataItem)
    {
        var comboBox = new ComboBox
        {
            ItemsSource = ItemsSource,
            MaxDropDownHeight = MaxDropDownHeight,
            FontSize = FontSize,
            MinHeight = 22,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            FocusAdorner = null,
            StyleVariant = AtomUI.Controls.InputControlStyleVariant.Borderless,
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            IsHitTestVisible = false,
        };

        if (Foreground != null)
            comboBox.Foreground = Foreground;
        if (ItemTemplate != null)
            comboBox.ItemTemplate = ItemTemplate;
        if (Binding != null && dataItem != null)
        {
            var binding = CloneBinding(Binding);
            if (binding is Binding dataBinding)
                dataBinding.Mode = BindingMode.OneWay;
            comboBox.Bind(ComboBox.SelectedItemProperty, binding);
        }
        else if (dataItem != null)
        {
            comboBox.SelectedItem = dataItem;
        }

        return comboBox;
    }

    protected override Control? GenerateEditingElementDirect(DataGridCell cell, object? dataItem)
    {
        var cb = new ComboBox
        {
            ItemsSource = ItemsSource,
            MaxDropDownHeight = MaxDropDownHeight,
            FontSize = FontSize,
            MinHeight = 22,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            FocusAdorner = null,
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
        };

        cb.DropDownClosed += (_, _) => DataGridOwner?.OnComboBoxDropDownClosed();

        if (ItemTemplate != null) cb.ItemTemplate = ItemTemplate;

        if (dataItem != null && Binding is Binding srcBinding)
        {
            var b = (Binding)CloneBinding(srcBinding);
            b.Mode = BindingMode.TwoWay;
            cb.Bind(ComboBox.SelectedItemProperty, b);
        }

        return cb;
    }

    public override object? PrepareCellForEdit(Control editingElement, RoutedEventArgs? editingEventArgs)
    {
        if (editingElement is ComboBox cb)
        {
            var originalValue = cb.SelectedItem;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => cb.IsDropDownOpen = true,
                Avalonia.Threading.DispatcherPriority.Loaded);
            return originalValue;
        }
        return null;
    }

    public override void CancelCellEdit(Control editingElement, object? uneditedValue)
    {
        if (editingElement is ComboBox cb) cb.SelectedItem = uneditedValue;
    }

    public override object? CommitCellEdit(Control editingElement)
    {
        if (editingElement is ComboBox cb) return cb.SelectedItem;
        return null;
    }
}