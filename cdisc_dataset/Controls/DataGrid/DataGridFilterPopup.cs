using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AtomButton = AtomUI.Desktop.Controls.Button;
using AtomLineEdit = AtomUI.Desktop.Controls.LineEdit;
using AtomListBox = AtomUI.Desktop.Controls.ListBox;
using AtomListBoxItem = AtomUI.Desktop.Controls.ListBoxItem;

namespace cdisc_dataset.Controls.DataGrid;

public class DataGridFilterPopup : TemplatedControl
{
    private AtomLineEdit? _searchBox;
    private AtomListBox? _filterList;
    private Border? _selectAllBox;
    private TextBlock? _selectAllCheckMark;
    private TextBlock? _selectAllIndeterminate;
    private Border? _selectAllCheckBorder;
    private readonly List<FilterItem> _items = new();
    private static readonly IBrush CheckBg = new SolidColorBrush(Color.Parse("#3B82F6"));
    private static readonly IBrush UncheckBdr = new SolidColorBrush(Color.Parse("#CBD5E1"));
    private static readonly IBrush TextPrimary = new SolidColorBrush(Color.Parse("#1E293B"));

    public DataGridColumn? Column { get; set; }
    public DataGrid? OwningGrid { get; set; }
    public event EventHandler? FilterApplied;
    private List<string>? _pendingUniqueValues;
    private HashSet<string>? _pendingSelectedValues;
    protected override Type StyleKeyOverride => typeof(DataGridFilterPopup);

    public void FocusSearchBox() => _searchBox?.Focus();

    public void BuildContent(List<string> uniqueValues, HashSet<string>? selectedValues)
    {
        _pendingUniqueValues = uniqueValues;
        _pendingSelectedValues = selectedValues;
        if (_filterList != null) PopulateItems(uniqueValues, selectedValues);
    }

    private void PopulateItems(List<string> uniqueValues, HashSet<string>? selectedValues)
    {
        _items.Clear();
        _filterList!.Items.Clear();
        var sorted = uniqueValues.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var val in sorted)
        {
            bool isChecked = selectedValues == null || selectedValues.Count == 0 || selectedValues.Contains(val);
            var displayText = string.IsNullOrEmpty(val) ? "(\u7A7A\u767D)" : val;
            var item = new FilterItem { Value = val, IsChecked = isChecked };
            item.ListItem = CreateListBoxItem(item, displayText);
            _items.Add(item);
            _filterList!.Items.Add(item.ListItem);
        }
        SyncSelectAll();
    }

    private AtomListBoxItem CreateListBoxItem(FilterItem item, string displayText)
    {
        var checkBorder = new Border
        {
            Width = 18, Height = 18,
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1.5),
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
            Background = item.IsChecked ? CheckBg : Brushes.White,
            BorderBrush = item.IsChecked ? CheckBg : UncheckBdr,
        };
        var checkMark = new TextBlock
        {
            Text = "\u2713", FontSize = 12, FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = item.IsChecked,
        };
        checkBorder.Child = checkMark;
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(6, 2) };
        panel.Children.Add(checkBorder);
        panel.Children.Add(new TextBlock
        {
            Text = displayText, FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = TextPrimary,
        });
        var container = new AtomListBoxItem
        {
            Content = panel,
            Padding = new Thickness(8, 7),
            Margin = new Thickness(0, 1),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        item.CheckBorder = checkBorder;
        item.CheckMark = checkMark;
        container.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(container).Properties.IsLeftButtonPressed) return;
            item.IsChecked = !item.IsChecked;
            UpdateItemVisual(item);
            SyncSelectAll();
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        return container;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _searchBox = e.NameScope.Find<AtomLineEdit>("PART_SearchBox");
        _filterList = e.NameScope.Find<AtomListBox>("PART_FilterList");
        _selectAllBox = e.NameScope.Find<Border>("PART_SelectAllBox");
        _selectAllCheckMark = e.NameScope.Find<TextBlock>("PART_SelectAllCheckMark");
        _selectAllIndeterminate = e.NameScope.Find<TextBlock>("PART_SelectAllIndeterminate");
        _selectAllCheckBorder = e.NameScope.Find<Border>("PART_SelectAllCheckBorder");
        if (_searchBox != null) _searchBox.TextChanged += (_, _) => DoSearch();
        if (_selectAllBox != null) _selectAllBox.PointerPressed += (_, _) =>
        {
            bool allChecked = _items.Count > 0 && _items.All(i => i.IsChecked);
            SetSelectAll(!allChecked);
        };
        var sortAsc = e.NameScope.Find<AtomButton>("PART_SortAsc");
        var sortDesc = e.NameScope.Find<AtomButton>("PART_SortDesc");
        if (sortAsc != null) sortAsc.Click += (_, _) => SortItems(true);
        if (sortDesc != null) sortDesc.Click += (_, _) => SortItems(false);
        var clearBtn = e.NameScope.Find<AtomButton>("PART_ClearBtn");
        var okBtn = e.NameScope.Find<AtomButton>("PART_OkBtn");
        if (clearBtn != null) clearBtn.Click += (_, _) =>
        {
            SetSelectAll(true);
            Column?.ClearFilter();
            FilterApplied?.Invoke(this, EventArgs.Empty);
        };
        if (okBtn != null) okBtn.Click += (_, _) =>
        {
            if (Column == null) return;
            var selected = _items.Where(i => i.IsChecked).Select(i => i.Value).ToHashSet();
            if (selected.Count == _items.Count || selected.Count == 0) Column.ClearFilter();
            else Column.SetFilter(selected);
            FilterApplied?.Invoke(this, EventArgs.Empty);
        };
        if (_pendingUniqueValues != null)
        {
            PopulateItems(_pendingUniqueValues, _pendingSelectedValues);
            _pendingUniqueValues = null;
        }
    }

    private void SetSelectAll(bool val)
    {
        foreach (var item in _items)
        {
            item.IsChecked = val;
            UpdateItemVisual(item);
        }
        SyncSelectAll();
    }

    private static void UpdateItemVisual(FilterItem item)
    {
        if (item.CheckBorder != null)
        {
            item.CheckBorder.Background = item.IsChecked ? CheckBg : Brushes.White;
            item.CheckBorder.BorderBrush = item.IsChecked ? CheckBg : UncheckBdr;
        }
        if (item.CheckMark != null) item.CheckMark.IsVisible = item.IsChecked;
    }

    private void SyncSelectAll()
    {
        bool allChecked = _items.Count > 0 && _items.All(i => i.IsChecked);
        bool noneChecked = _items.Count == 0 || _items.All(i => !i.IsChecked);
        bool isIndeterminate = !allChecked && !noneChecked;

        if (_selectAllCheckBorder != null)
        {
            _selectAllCheckBorder.Background = allChecked || isIndeterminate ? CheckBg : Brushes.White;
            _selectAllCheckBorder.BorderBrush = allChecked || isIndeterminate ? CheckBg : UncheckBdr;
        }
        if (_selectAllCheckMark != null) _selectAllCheckMark.IsVisible = allChecked;
        if (_selectAllIndeterminate != null) _selectAllIndeterminate.IsVisible = isIndeterminate;
    }

    private void DoSearch()
    {
        if (_filterList == null) return;
        RenderMatchingItems(_searchBox?.Text?.ToLowerInvariant() ?? "");
        SyncSelectAll();
    }

    private void RenderMatchingItems(string text)
    {
        if (_filterList == null) return;
        _filterList.Items.Clear();
        foreach (var item in _items)
        {
            if (!string.IsNullOrEmpty(text) && !item.Value.ToLowerInvariant().Contains(text)) continue;
            var displayText = string.IsNullOrEmpty(item.Value) ? "(空白)" : item.Value;
            item.ListItem = CreateListBoxItem(item, displayText);
            _filterList.Items.Add(item.ListItem);
        }
    }

    private void SortItems(bool ascending)
    {
        if (_filterList == null || _items.Count == 0) return;
        var text = _searchBox?.Text?.ToLowerInvariant() ?? "";
        var sorted = ascending
            ? _items.OrderBy(i => i.Value, StringComparer.OrdinalIgnoreCase).ToList()
            : _items.OrderByDescending(i => i.Value, StringComparer.OrdinalIgnoreCase).ToList();
        _items.Clear();
        _items.AddRange(sorted);
        RenderMatchingItems(text);
    }

    private class FilterItem
    {
        public string Value { get; set; } = "";
        public bool IsChecked { get; set; } = true;
        public AtomListBoxItem ListItem { get; set; } = null!;
        public Border? CheckBorder { get; set; }
        public TextBlock? CheckMark { get; set; }
    }
}
