using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AtomLineEdit = AtomUI.Desktop.Controls.LineEdit;
using AtomIconButton = AtomUI.Desktop.Controls.IconButton;
using AtomUI.Icons.AntDesign;
using cdisc_dataset.Controls.DataGrid.Searching;

namespace cdisc_dataset.Controls.DataGrid;

public enum DataGridGridLinesVisibility
{
    None,
    Horizontal,
    Vertical,
    Both
}

public class DataGrid : TemplatedControl
{
    private DataGridScrollPanel? _scrollPanel;
    private DataGridColumnHeadersPresenter? _headersPresenter;
    private Border? _headerClipper;
    private ScrollBar? _vScrollBar;
    private ScrollBar? _hScrollBar;
    private bool _updatingScrollBars;
    private readonly List<DataGridRow> _realizedRows = new();
    private DataGridRow? _currentRow;
    private DataGridCell? _currentCell;
    private bool _isEditing;
    private bool _templateApplied;
    private double _verticalOffset;
    private double _horizontalOffset;
    private double _lastArrangeHeight;
    private bool _itemsDirty = true;
    private List<object>? _cachedItems;
    private TextBlock? _statusBar;

    // Validation tracking
    private readonly HashSet<INotifyDataErrorInfo> _validationTrackedItems = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<INotifyDataErrorInfo> _validationItemsWithError = new(ReferenceEqualityComparer.Instance);
    private bool _isValid = true;

    // Search support
    private SearchModel? _searchModel;
    private DataGridSearchAdapter? _searchAdapter;
    private AtomLineEdit? _searchTextBox;
    private TextBlock? _searchCountText;
    private Border? _searchBarBorder;
    private bool _usesExternalSearchModel;

    public static readonly StyledProperty<bool> IsSearchBarVisibleProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(IsSearchBarVisible), true);

    public static readonly StyledProperty<SearchHighlightMode> SearchHighlightModeProperty =
        AvaloniaProperty.Register<DataGrid, SearchHighlightMode>(nameof(SearchHighlightMode), SearchHighlightMode.Cell);
    public static readonly StyledProperty<SearchModel?> SearchModelProperty =
        AvaloniaProperty.Register<DataGrid, SearchModel?>(nameof(SearchModel));
    public static readonly StyledProperty<object?> SearchBarRightContentProperty =
        AvaloniaProperty.Register<DataGrid, object?>(nameof(SearchBarRightContent));

    public bool IsSearchBarVisible { get => GetValue(IsSearchBarVisibleProperty); set => SetValue(IsSearchBarVisibleProperty, value); }
    public SearchHighlightMode SearchHighlightMode { get => GetValue(SearchHighlightModeProperty); set => SetValue(SearchHighlightModeProperty, value); }
    public SearchModel? SearchModel { get => GetValue(SearchModelProperty); set => SetValue(SearchModelProperty, value); }
    public object? SearchBarRightContent { get => GetValue(SearchBarRightContentProperty); set => SetValue(SearchBarRightContentProperty, value); }

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<DataGrid, IEnumerable?>(nameof(ItemsSource));
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<DataGrid, object?>(nameof(SelectedItem));
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<DataGrid, int>(nameof(SelectedIndex), -1);
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(RowHeight), 28);
    public static readonly StyledProperty<double> ColumnHeaderHeightProperty =
        AvaloniaProperty.Register<DataGrid, double>(nameof(ColumnHeaderHeight), 30);
    public static readonly StyledProperty<IBrush?> HeaderBackgroundProperty =
        AvaloniaProperty.Register<DataGrid, IBrush?>(nameof(HeaderBackground),
            new SolidColorBrush(Color.Parse("#F0F0F0")));
    public static readonly StyledProperty<IBrush?> RowBackgroundProperty =
        AvaloniaProperty.Register<DataGrid, IBrush?>(nameof(RowBackground),
            new SolidColorBrush(Colors.White));
    public static readonly StyledProperty<IBrush?> AlternatingRowBackgroundProperty =
        AvaloniaProperty.Register<DataGrid, IBrush?>(nameof(AlternatingRowBackground),
            new SolidColorBrush(Color.Parse("#F8F8F8")));
    public static readonly StyledProperty<IBrush?> RowHoverBackgroundProperty =
        AvaloniaProperty.Register<DataGrid, IBrush?>(nameof(RowHoverBackground),
            new SolidColorBrush(Color.Parse("#F5FAFF")));
    public static readonly StyledProperty<int> LeftFrozenColumnCountProperty =
        AvaloniaProperty.Register<DataGrid, int>(nameof(LeftFrozenColumnCount), 0);
    public static readonly StyledProperty<int> RightFrozenColumnCountProperty =
        AvaloniaProperty.Register<DataGrid, int>(nameof(RightFrozenColumnCount), 0);
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(IsReadOnly));
    public static readonly StyledProperty<DataGridGridLinesVisibility> GridLinesVisibilityProperty =
        AvaloniaProperty.Register<DataGrid, DataGridGridLinesVisibility>(nameof(GridLinesVisibility), DataGridGridLinesVisibility.Both);

    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    public double ColumnHeaderHeight { get => GetValue(ColumnHeaderHeightProperty); set => SetValue(ColumnHeaderHeightProperty, value); }
    public IBrush? HeaderBackground { get => GetValue(HeaderBackgroundProperty); set => SetValue(HeaderBackgroundProperty, value); }
    public IBrush? RowBackground { get => GetValue(RowBackgroundProperty); set => SetValue(RowBackgroundProperty, value); }
    public IBrush? AlternatingRowBackground { get => GetValue(AlternatingRowBackgroundProperty); set => SetValue(AlternatingRowBackgroundProperty, value); }
    public IBrush? RowHoverBackground { get => GetValue(RowHoverBackgroundProperty); set => SetValue(RowHoverBackgroundProperty, value); }
    public int LeftFrozenColumnCount { get => GetValue(LeftFrozenColumnCountProperty); set => SetValue(LeftFrozenColumnCountProperty, value); }
    public int RightFrozenColumnCount { get => GetValue(RightFrozenColumnCountProperty); set => SetValue(RightFrozenColumnCountProperty, value); }
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public DataGridGridLinesVisibility GridLinesVisibility { get => GetValue(GridLinesVisibilityProperty); set => SetValue(GridLinesVisibilityProperty, value); }

    public ObservableCollection<DataGridColumn> Columns { get; } = new();
    public DataGridRow? CurrentRow => _currentRow;
    public DataGridCell? CurrentCell => _currentCell;
    public bool IsEditing => _isEditing;
    internal double HorizontalOffset => _horizontalOffset;
    internal double VerticalOffset => _verticalOffset;
    internal DataGridColumnHeadersPresenter? HeadersPresenter => _headersPresenter;

    internal double GetLeftFrozenWidth()
    {
        double w = 0;
        int stop = Math.Min(LeftFrozenColumnCount, Columns.Count);
        for (int i = 0; i < stop; i++)
            w += Columns[i].GetEffectiveWidth();
        return w;
    }

    internal double GetRightFrozenWidth()
    {
        double w = 0;
        int start = Math.Max(0, Columns.Count - RightFrozenColumnCount);
        for (int i = start; i < Columns.Count; i++)
            w += Columns[i].GetEffectiveWidth();
        return w;
    }

    internal bool IsColumnFrozen(int columnIndex)
    {
        return columnIndex < LeftFrozenColumnCount
            || columnIndex >= Columns.Count - RightFrozenColumnCount;
    }

    public event EventHandler<DataGridColumnEventArgs>? ColumnHeaderClick;
    public event EventHandler? SelectionChanged;
    public event EventHandler<DataGridCellEditEndingEventArgs>? CellEditEnding;

    public bool IsValid
    {
        get => _isValid;
        internal set
        {
            _isValid = value;
            PseudoClasses.Set(":invalid", !value);
        }
    }

    protected override Type StyleKeyOverride => typeof(DataGrid);

    public DataGrid()
    {
        Columns.CollectionChanged += OnColumnsChanged;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        EnsureDefaultVisualTree();
    }

    private Border BuildSearchBar()
    {
        _usesExternalSearchModel = SearchModel != null;
        AttachSearchModel(SearchModel ?? new SearchModel
        {
            HighlightMode = SearchHighlightMode,
        });

        _searchTextBox = new AtomLineEdit
        {
            PlaceholderText = "搜索...",
            Width = 240,
            Margin = new Thickness(4),
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            FontSize = 13,
            IsAllowClear = true
        };

        if (_usesExternalSearchModel)
        {
            _searchTextBox.Bind(TextBox.TextProperty, new Binding("Query")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            });
        }
        _searchTextBox.TextChanged += OnSearchTextChanged;
        _searchTextBox.KeyDown += OnSearchKeyDown;

        var prevBtn = new AtomIconButton
        {
            Icon = new ArrowUpOutlined(),
            Width = 26,
            Height = 26,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            FontSize = 14,
            Margin = new Thickness(2, 0),
            Padding = new Thickness(0),
            MinWidth = 0,
        };
        prevBtn.Click += (_, _) => _searchModel?.MovePrevious();

        var nextBtn = new AtomIconButton
        {
            Icon = new ArrowDownOutlined(),
            Width = 26,
            Height = 26,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            FontSize = 14,
            Margin = new Thickness(2, 0),
            Padding = new Thickness(0),
            MinWidth = 0,
        };
        nextBtn.Click += (_, _) => _searchModel?.MoveNext();

        _searchCountText = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#666666")),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };

        var searchControls = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        searchControls.Children.Add(_searchTextBox);
        searchControls.Children.Add(prevBtn);
        searchControls.Children.Add(nextBtn);
        searchControls.Children.Add(_searchCountText);

        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        content.Children.Add(searchControls);

        var rightContent = new ContentControl
        {
            Content = SearchBarRightContent,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        Grid.SetColumn(rightContent, 1);
        content.Children.Add(rightContent);

        return new Border
        {
            //Background = new SolidColorBrush(Color.Parse("#F5F5F5")),
            Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#E0E0E0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 8, 8, 6),
            Padding = new Thickness(4, 2),
            Child = content,
        };
    }

    private void AttachSearchModel(SearchModel searchModel)
    {
        if (ReferenceEquals(_searchModel, searchModel)) return;

        if (_searchModel != null)
        {
            _searchModel.ResultsChanged -= OnSearchResultsChanged;
            _searchModel.CurrentChanged -= OnSearchCurrentChanged;
        }
        _searchAdapter?.Dispose();

        _searchModel = searchModel;
        _searchAdapter = new DataGridSearchAdapter(_searchModel, this);
        _searchModel.ResultsChanged += OnSearchResultsChanged;
        _searchModel.CurrentChanged += OnSearchCurrentChanged;
    }

    private void UpdateSearchBarVisibility()
    {
        if (_searchBarBorder == null) return;
        _searchBarBorder.IsVisible = IsSearchBarVisible;
        if (!IsSearchBarVisible)
        {
            _searchModel?.Clear();
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_searchTextBox == null || _searchModel == null) return;

        var query = _searchTextBox.Text ?? string.Empty;
        if (_searchTextBox.InnerRightContent is Control clearButton)
            clearButton.IsVisible = !string.IsNullOrWhiteSpace(query);

        if (_usesExternalSearchModel) return;
        if (string.IsNullOrWhiteSpace(query))
            _searchModel.Clear();
        else
            _searchModel.Apply(new[] { new SearchDescriptor(query) });
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (_searchModel == null) return;
        if (e.Key == Key.Enter)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                _searchModel.MovePrevious();
            else
                _searchModel.MoveNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _searchModel.Clear();
            if (_searchTextBox != null) _searchTextBox.Text = string.Empty;
            e.Handled = true;
        }
    }

    private void OnSearchResultsChanged(object? sender, SearchResultsChangedEventArgs e)
    {
        if (_searchCountText == null || _searchModel == null) return;
        var count = _searchModel.Results.Select(result => result.RowIndex).Distinct().Count();
        _searchCountText.Text = count > 0 ? $"{count} 行结果" : "无结果";

        ClearRows();
        InvalidateItemsCache();
        RefreshRows();
        _scrollPanel?.InvalidateMeasure();
        _scrollPanel?.InvalidateArrange();
    }

    private void OnSearchCurrentChanged(object? sender, SearchCurrentChangedEventArgs e)
    {
        if (_searchModel?.CurrentResult == null) return;
        var result = _searchModel.CurrentResult;
        var items = GetItemsList();
        int filteredIndex = items.IndexOf(result.Item);
        if (filteredIndex >= 0)
        {
            EnsureRowVisible(filteredIndex);
            UpdateScrollBars();
            UpdateViewport();

            var row = _realizedRows.FirstOrDefault(realizedRow => realizedRow.Index == filteredIndex);
            var cell = row?.Cells.FirstOrDefault(candidate => Columns.IndexOf(candidate.Column!) == result.ColumnIndex);
            if (row != null && cell != null)
            {
                if (_currentRow != null && _currentRow != row) _currentRow.IsSelected = false;
                row.IsSelected = true;
                _currentRow = row;
                SelectedIndex = filteredIndex;
                SelectedItem = row.DataContext;

                if (_currentCell != null && _currentCell != cell) _currentCell.IsSelected = false;
                cell.IsSelected = true;
                _currentCell = cell;
            }
        }

        foreach (var row in _realizedRows)
            row.RefreshSearchHighlights();
    }

    internal SearchHighlightMode EffectiveSearchHighlightMode => _searchModel?.HighlightMode ?? SearchHighlightMode.None;

    internal SearchResult? GetCellSearchResult(DataGridCell cell)
    {
        if (_searchModel == null || _searchModel.Results.Count == 0) return null;
        if (cell.Column == null || cell.DataItem == null) return null;

        var columnIndex = Columns.IndexOf(cell.Column);
        var result = _searchModel.CurrentResult;
        if (result != null && result.Item == cell.DataItem && result.ColumnIndex == columnIndex)
            return result;

        return null;
    }

    internal IReadOnlyList<SearchMatch>? GetCellSearchMatches(DataGridCell cell)
    {
        if (_searchModel == null || _searchModel.Results.Count == 0) return null;
        if (cell.Column == null || cell.DataItem == null) return null;

        var columnIndex = Columns.IndexOf(cell.Column);
        foreach (var result in _searchModel.Results)
        {
            if (result.Item == cell.DataItem && result.ColumnIndex == columnIndex && result.Matches.Count > 0)
                return result.Matches;
        }
        return null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureDefaultVisualTree();
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsureDefaultVisualTree();
        _lastArrangeHeight = finalSize.Height;
        var r = base.ArrangeOverride(finalSize);
        UpdateStarColumnWidths(GetViewportWidth());
        UpdateViewport();
        UpdateScrollBars();
        return r;
    }

    private void EnsureDefaultVisualTree()
    {
        if (_templateApplied) return;

        _headersPresenter = new DataGridColumnHeadersPresenter { OwningGrid = this };
        _headerClipper = new Border
        {
            ClipToBounds = true,
            BorderBrush = new SolidColorBrush(Color.Parse("#D0D0D0")),
            BorderThickness = new Thickness(0, 1, 0, 1),
            Background = HeaderBackground,
            ZIndex = 10,
        };
        _headerClipper.Child = _headersPresenter;

        _scrollPanel = new DataGridScrollPanel
        {
            OwningGrid = this,
            ClipToBounds = true,
        };

        _vScrollBar = new ScrollBar
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        };
        _vScrollBar.ValueChanged += OnVScrollBarChanged;

        _hScrollBar = new ScrollBar
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };
        _hScrollBar.ValueChanged += OnHScrollBarChanged;

        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"),
            ClipToBounds = true,
        };

        Grid.SetRow(_headerClipper, 1);
        Grid.SetColumn(_headerClipper, 0);
        root.Children.Add(_headerClipper);

        // Search bar
        _searchBarBorder = BuildSearchBar();
        Grid.SetRow(_searchBarBorder, 0);
        Grid.SetColumn(_searchBarBorder, 0);
        Grid.SetColumnSpan(_searchBarBorder, 2);
        root.Children.Add(_searchBarBorder);
        UpdateSearchBarVisibility();

        Grid.SetRow(_scrollPanel, 2);
        Grid.SetColumn(_scrollPanel, 0);
        root.Children.Add(_scrollPanel);

        Grid.SetRow(_vScrollBar, 2);
        Grid.SetColumn(_vScrollBar, 1);
        root.Children.Add(_vScrollBar);

        _statusBar = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#666666")),
            Margin = new Thickness(8, 2),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        var statusBarBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F5F5F5")),
            BorderBrush = new SolidColorBrush(Color.Parse("#E0E0E0")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = _statusBar,
        };
        Grid.SetRow(statusBarBorder, 3);
        Grid.SetColumn(statusBarBorder, 0);
        root.Children.Add(statusBarBorder);

        Grid.SetRow(_hScrollBar, 4);
        Grid.SetColumn(_hScrollBar, 0);
        root.Children.Add(_hScrollBar);

        ClipToBounds = true;
        LogicalChildren.Clear();
        VisualChildren.Clear();
        LogicalChildren.Add(root);
        VisualChildren.Add(root);

        _templateApplied = true;
        BuildHeaders();
        RefreshRows();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        double vpH = GetViewportHeight();
        double vpW = Bounds.Width;
        int itemCount = GetItemCount();
        double totalH = itemCount * RowHeight;
        double totalW = Columns.Sum(c => c.GetEffectiveWidth());
        double maxV = Math.Max(0, totalH - vpH);
        double maxH = Math.Max(0, totalW - vpW);

        if (e.Delta.Y != 0)
            _verticalOffset = Math.Clamp(_verticalOffset - e.Delta.Y * RowHeight * 3, 0, maxV);
        if (e.Delta.X != 0)
            _horizontalOffset = Math.Clamp(_horizontalOffset - e.Delta.X * 30, 0, maxH);

        SyncHorizontalOffset();
        UpdateScrollBars();
        UpdateViewport();
        e.Handled = true;
    }

    private void UpdateStatusBar()
    {
        if (_statusBar == null) return;
        int total = ItemsSource?.Cast<object>().Count() ?? 0;
        int filtered = GetItemCount();
        _statusBar.Text = total == filtered
            ? $"共 {total} 行"
            : $"共 {filtered} 行（筛选自 {total} 行）";
    }

    private double GetViewportHeight()
    {
        if (_scrollPanel != null && _scrollPanel.Bounds.Height > 1)
            return _scrollPanel.Bounds.Height;

        double vpH = _lastArrangeHeight > 0 ? _lastArrangeHeight : Bounds.Height;
        vpH -= ColumnHeaderHeight;
        if (_statusBar != null) vpH -= 26;
        if (_hScrollBar != null && _hScrollBar.IsVisible) vpH -= _hScrollBar.DesiredSize.Height;
        return Math.Max(0, vpH);
    }

    private double GetViewportWidth()
    {
        if (_scrollPanel != null && _scrollPanel.Bounds.Width > 1)
            return _scrollPanel.Bounds.Width;

        double vpW = Bounds.Width;
        if (_vScrollBar != null && _vScrollBar.IsVisible) vpW -= _vScrollBar.DesiredSize.Width;
        return Math.Max(0, vpW);
    }

    private void SyncHorizontalOffset()
    {
        if (_headersPresenter != null)
        {
            _headersPresenter.HorizontalOffset = _horizontalOffset;
            _headersPresenter.InvalidateArrange();
        }
        foreach (var row in _realizedRows)
            row.InvalidateArrange();
    }

    private void OnVScrollBarChanged(object? sender, RoutedEventArgs e)
    {
        if (_updatingScrollBars) return;
        _verticalOffset = _vScrollBar!.Value;
        SyncHorizontalOffset();
        UpdateViewport();
    }

    private void OnHScrollBarChanged(object? sender, RoutedEventArgs e)
    {
        if (_updatingScrollBars) return;
        _horizontalOffset = _hScrollBar!.Value;
        SyncHorizontalOffset();
        UpdateViewport();
    }

    private void UpdateScrollBars()
    {
        if (_vScrollBar == null || _hScrollBar == null) return;
        if (_lastArrangeHeight <= 0 && Bounds.Height <= 0) return;
        _updatingScrollBars = true;

        int itemCount = GetItemCount();
        double totalH = itemCount * RowHeight;
        double totalW = Columns.Sum(c => c.GetEffectiveWidth());

        // First pass: determine which scrollbars are needed
        double vpH = GetViewportHeight();
        double vpW = GetViewportWidth();
        bool vNeeded = totalH > vpH + 1;
        bool hNeeded = totalW > vpW + 1;

        // Show/hide scrollbars
        _vScrollBar.IsVisible = vNeeded;
        _hScrollBar.IsVisible = hNeeded;

        // Second pass: recalculate with actual scrollbar visibility
        if (vNeeded)
        {
            vpH = GetViewportHeight();
            _hScrollBar.IsVisible = totalW > vpW;
            hNeeded = _hScrollBar.IsVisible;
        }
        if (hNeeded)
        {
            vpH = GetViewportHeight();
        }

        if (vNeeded)
        {
            _vScrollBar.Minimum = 0;
            _vScrollBar.Maximum = Math.Max(0, totalH - vpH);
            _vScrollBar.ViewportSize = vpH;
            _vScrollBar.LargeChange = vpH;
            _vScrollBar.SmallChange = RowHeight;
            _verticalOffset = Math.Clamp(_verticalOffset, 0, _vScrollBar.Maximum);
            _vScrollBar.Value = _verticalOffset;
        }
        else
        {
            _verticalOffset = 0;
        }

        double finalVpW = vNeeded ? GetViewportWidth() : Bounds.Width;
        if (hNeeded)
        {
            _hScrollBar.Minimum = 0;
            _hScrollBar.Maximum = Math.Max(0, totalW - finalVpW);
            _hScrollBar.ViewportSize = finalVpW;
            _hScrollBar.LargeChange = finalVpW;
            _hScrollBar.SmallChange = 50;
            _horizontalOffset = Math.Clamp(_horizontalOffset, 0, _hScrollBar.Maximum);
            _hScrollBar.Value = _horizontalOffset;
        }
        else
        {
            _horizontalOffset = 0;
        }

        _updatingScrollBars = false;
    }

    private void UpdateStarColumnWidths(double viewportWidth)
    {
        var stars = Columns.Where(column => column.IsVisible && column.Width.IsStar).ToList();
        if (stars.Count == 0) return;

        double fixedWidth = Columns
            .Where(column => column.IsVisible && !column.Width.IsStar)
            .Sum(column => column.GetEffectiveWidth());
        double remainingWidth = Math.Max(0, viewportWidth - fixedWidth);
        var unresolved = new List<DataGridColumn>(stars);

        while (unresolved.Count > 0)
        {
            double totalWeight = unresolved.Sum(column => column.Width.Value);
            bool constrained = false;
            foreach (var column in unresolved.ToList())
            {
                double targetWidth = totalWeight > 0
                    ? remainingWidth * column.Width.Value / totalWeight
                    : 0;
                double constrainedWidth = Math.Clamp(targetWidth, column.MinWidth, column.MaxWidth);
                if (Math.Abs(constrainedWidth - targetWidth) < 0.01) continue;

                column.SetActualWidth(constrainedWidth);
                remainingWidth = Math.Max(0, remainingWidth - constrainedWidth);
                unresolved.Remove(column);
                constrained = true;
            }

            if (constrained) continue;
            foreach (var column in unresolved)
            {
                double width = totalWeight > 0
                    ? remainingWidth * column.Width.Value / totalWeight
                    : 0;
                column.SetActualWidth(width);
            }
            break;
        }

        _headersPresenter?.InvalidateArrange();
        foreach (var row in _realizedRows)
            row.InvalidateArrange();
    }

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        for (int i = 0; i < Columns.Count; i++) { Columns[i].Index = i; Columns[i].DataGridOwner = this; }
        if (_templateApplied) { BuildHeaders(); RefreshRows(); }
    }

    private void BuildHeaders()
    {
        if (_headersPresenter == null) return;
        _headersPresenter.Children.Clear();
        int leftFrozen = Math.Min(LeftFrozenColumnCount, Columns.Count);
        int rightFrozen = Math.Min(RightFrozenColumnCount, Columns.Count);
        int totalCount = Columns.Count;

        for (int ci = 0; ci < Columns.Count; ci++)
        {
            if (!Columns[ci].IsVisible) continue;
            if (ci >= leftFrozen && ci < totalCount - rightFrozen)
            {
                var header = new DataGridColumnHeader { OwningColumn = Columns[ci], Height = ColumnHeaderHeight };
                _headersPresenter.Children.Add(header);
            }
        }
        for (int ci = 0; ci < Columns.Count; ci++)
        {
            if (!Columns[ci].IsVisible) continue;
            if (ci < leftFrozen || ci >= totalCount - rightFrozen)
            {
                var header = new DataGridColumnHeader { OwningColumn = Columns[ci], Height = ColumnHeaderHeight };
                _headersPresenter.Children.Add(header);
            }
        }
        _headersPresenter.InvalidateMeasure();
        _headersPresenter.InvalidateArrange();
    }

    internal void OnColumnHeaderClick(DataGridColumn column)
    {
        ColumnHeaderClick?.Invoke(this, new DataGridColumnEventArgs(column));
    }

    internal void OnColumnWidthChanged(DataGridColumn column)
    {
        foreach (var row in _realizedRows)
        {
            foreach (var cell in row.Cells)
            {
                if (cell.Column != null)
                    cell.Width = cell.Column.GetEffectiveWidth();
            }
            row.InvalidateMeasure();
            row.InvalidateArrange();
        }

        _headersPresenter?.InvalidateMeasure();
        _headersPresenter?.InvalidateArrange();
        _scrollPanel?.InvalidateMeasure();
        _scrollPanel?.InvalidateArrange();
        UpdateScrollBars();
        UpdateViewport();
    }

    internal void OnColumnVisibilityChanged(DataGridColumn column)
    {
        if (!_templateApplied) return;
        BuildHeaders();
        foreach (var row in _realizedRows)
            row.UpdateCells();
        _headersPresenter?.InvalidateMeasure();
        _headersPresenter?.InvalidateArrange();
        _scrollPanel?.InvalidateMeasure();
        _scrollPanel?.InvalidateArrange();
        UpdateScrollBars();
        UpdateViewport();
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!_templateApplied) return;
            UpdateScrollBars();
            UpdateViewport();
            _scrollPanel?.InvalidateArrange();
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void UpdateViewport()
    {
        if (_scrollPanel == null || ItemsSource == null) { ClearRows(); return; }
        int itemCount = GetItemCount();
        double vpH = GetViewportHeight();
        if (vpH <= 0) vpH = 600;

        int first = Math.Max(0, (int)Math.Floor(_verticalOffset / RowHeight) - 2);
        int last = Math.Min(itemCount - 1, (int)Math.Ceiling((_verticalOffset + vpH) / RowHeight) + 2);
        var items = GetItemsList();
        var keep = new HashSet<int>();
        for (int i = first; i <= last; i++)
        {
            keep.Add(i);
            var existing = _realizedRows.FirstOrDefault(r => r.Index == i);
            if (existing != null)
            {
                var item = i < items.Count ? items[i] : null;
                if (!ReferenceEquals(existing.DataContext, item))
                {
                    Debug.WriteLine($"[DataGrid.UpdateViewport.Rebind] index={i}, old={existing.DataContext}#{(existing.DataContext == null ? 0 : RuntimeHelpers.GetHashCode(existing.DataContext))}, new={item}#{(item == null ? 0 : RuntimeHelpers.GetHashCode(item))}");
                    existing.DataContext = item;
                    existing.UpdateCells();
                }
                continue;
            }

            var row = CreateRow(i, items);
            _realizedRows.Add(row);
            if (!_scrollPanel.Children.Contains(row)) _scrollPanel.Children.Add(row);
        }
        foreach (var row in _realizedRows.Where(r => !keep.Contains(r.Index)).ToList())
        { _realizedRows.Remove(row); _scrollPanel.Children.Remove(row); }

        _scrollPanel.InvalidateArrange();
    }

    private DataGridRow CreateRow(int index, IList items)
    {
        object? item = index < items.Count ? items[index] : null;
        var row = new DataGridRow { Index = index, DataContext = item, Height = RowHeight, OwningGrid = this };
        row.UpdateCells();
        row.PointerPressed += OnRowPointerPressed;
        bool sel = index == SelectedIndex;
        row.IsSelected = sel;
        if (sel) _currentRow = row;
        SubscribeItemValidation(item);
        RestoreRowValidationState(row);
        return row;
    }

    private void ClearRows()
    {
        if (_scrollPanel != null) _scrollPanel.Children.Clear();
        _realizedRows.Clear();
    }

    private void InvalidateItemsCache()
    {
        _itemsDirty = true;
        _cachedItems = null;
    }

    private IList GetItemsList()
    {
        if (_itemsDirty)
        {
            if (ItemsSource == null) { _cachedItems = new List<object>(); }
            else
            {
                var source = ItemsSource.Cast<object>();
                foreach (var col in Columns.Where(c => c.IsFiltered))
                    source = source.Where(item => col.PassesFilter(item));
                if (_searchModel is { Results.Count: > 0 })
                {
                    var matched = new HashSet<object>(_searchModel.Results.Select(r => r.Item));
                    source = source.Where(item => matched.Contains(item));
                }
                _cachedItems = source.ToList();
                Debug.WriteLine($"[DataGrid.GetItemsList] count={_cachedItems.Count}, items={string.Join(" | ", _cachedItems.Select((item, index) => $"{index}:{item}#{RuntimeHelpers.GetHashCode(item)}"))}");
            }
            _itemsDirty = false;
        }
        return _cachedItems!;
    }

    internal IEnumerable<object> GetFilterContextItems(DataGridColumn currentColumn)
    {
        if (ItemsSource == null) return Array.Empty<object>();

        IEnumerable<object> source = ItemsSource.Cast<object>();
        foreach (var column in Columns.Where(column => column != currentColumn && column.IsFiltered))
            source = source.Where(column.PassesFilter);

        if (_searchModel is { Results.Count: > 0 })
        {
            var matched = new HashSet<object>(_searchModel.Results.Select(result => result.Item));
            source = source.Where(matched.Contains);
        }

        return source;
    }

    internal int GetItemCount() => GetItemsList().Count;

    internal void OnFilterChanged(DataGridColumn column)
    {
        _verticalOffset = 0;
        SelectedIndex = -1;
        _currentRow = null;
        _currentCell = null;
        _realizedRows.Clear();
        if (_scrollPanel != null)
        {
            for (int i = _scrollPanel.Children.Count - 1; i >= 0; i--)
            {
                if (_scrollPanel.Children[i] is DataGridRow row)
                    _scrollPanel.Children.RemoveAt(i);
            }
        }
        InvalidateItemsCache();
        UpdateStatusBar();
        UpdateScrollBars();
        UpdateViewport();
        BuildHeaders();
    }

    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not DataGridRow row) return;
        var pos = e.GetPosition(row);
        var pt = e.GetCurrentPoint(this);
        DataGridCell? cell = pt.Properties.IsLeftButtonPressed ? HitTestCell(row, pos.X) : null;
        if (_isEditing && cell != null && cell != _currentCell)
        {
            CommitEdit();
        }

        if (_currentRow != null && _currentRow != row) _currentRow.IsSelected = false;
        row.IsSelected = true;
        _currentRow = row;
        SelectedIndex = row.Index;
        SelectedItem = row.DataContext;

        if (cell != null)
        {
            if (_currentCell != null && _currentCell != cell) _currentCell.IsSelected = false;
            cell.IsSelected = true;
            _currentCell = cell;

            if (cell.Column is DataGridCheckBoxColumn && cell.DataItem != null)
            {
                ToggleCheckBoxValue(cell);
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void ToggleCheckBoxValue(DataGridCell cell)
    {
        if (cell.Column is not DataGridCheckBoxColumn cbCol || cbCol.Binding == null || cell.DataItem == null) return;
        var binding = cbCol.Binding;
        if (binding is not Binding b) return;
        var prop = cell.DataItem.GetType().GetProperty(b.Path);
        if (prop == null || !prop.CanWrite) return;
        var current = prop.GetValue(cell.DataItem);
        bool newVal = current is bool bv ? !bv : true;
        prop.SetValue(cell.DataItem, newVal);
        cell.ResetDisplay();
    }

    private DataGridCell? HitTestCell(DataGridRow row, double x)
    {
        foreach (var cell in row.Cells)
        {
            var bounds = cell.Bounds;
            if (x >= bounds.X && x < bounds.X + bounds.Width)
                return cell;
        }
        return null;
    }

    public void OnCellPressed(DataGridCell cell, PointerPressedEventArgs e) { }

    public void BeginEdit(DataGridCell cell)
    {
        if (cell.Column?.IsReadOnly == true || cell.DataItem == null || _isEditing) return;
        _isEditing = true;
        _currentCell = cell;
        cell.BeginEdit();
    }

    public void CommitEdit()
    {
        if (!_isEditing || _currentCell == null) return;
        var args = new DataGridCellEditEndingEventArgs(_currentCell, _currentCell.Column, DataGridEditAction.Commit);
        CellEditEnding?.Invoke(this, args);
        if (args.Cancel) return;
        var value = _currentCell.CommitEdit();
        CommitCellValue(_currentCell, value);
        _isEditing = false;
    }

    internal void OnComboBoxDropDownClosed()
    {
        if (_isEditing)
            CommitEdit();
    }

    private static void CommitCellValue(DataGridCell cell, object? value)
    {
        if (cell.DataItem == null || cell.Column is not DataGridBoundColumn boundColumn
            || string.IsNullOrWhiteSpace(boundColumn.BindingPath))
            return;

        var property = cell.DataItem.GetType().GetProperty(boundColumn.BindingPath);
        if (property?.CanWrite != true)
            return;

        property.SetValue(cell.DataItem, value);
    }

    public void CancelEdit()
    {
        if (!_isEditing || _currentCell == null) return;
        var args = new DataGridCellEditEndingEventArgs(_currentCell, _currentCell.Column, DataGridEditAction.Cancel);
        CellEditEnding?.Invoke(this, args);
        _currentCell.CancelEdit();
        _isEditing = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_isEditing)
        {
            if (_currentCell?.Column is DataGridPopupColumn)
                return;

            if (e.Key == Key.Escape) { CancelEdit(); e.Handled = true; }
            else if (e.Key == Key.Enter) { CommitEdit(); Navigate(0, 1); e.Handled = true; }
            else if (e.Key == Key.Tab) { CommitEdit(); Navigate(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1, 0); e.Handled = true; }
            return;
        }
        switch (e.Key)
        {
            case Key.F2:
            case Key.Enter:
                if (_currentCell?.Column is { IsReadOnly: false }) { BeginEdit(_currentCell); e.Handled = true; }
                break;
            case Key.F when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                IsSearchBarVisible = !IsSearchBarVisible;
                if (IsSearchBarVisible && _searchTextBox != null) _searchTextBox.Focus();
                e.Handled = true;
                break;
            case Key.Escape when IsSearchBarVisible && _searchTextBox?.IsFocused == true:
                IsSearchBarVisible = false;
                _searchTextBox.Text = string.Empty;
                e.Handled = true;
                break;
            case Key.Up: Navigate(0, -1); e.Handled = true; break;
            case Key.Down: Navigate(0, 1); e.Handled = true; break;
            case Key.Left: Navigate(-1, 0); e.Handled = true; break;
            case Key.Right: Navigate(1, 0); e.Handled = true; break;
            case Key.PageDown: Navigate(0, Math.Max(1, (int)(Bounds.Height / RowHeight))); e.Handled = true; break;
            case Key.PageUp: Navigate(0, -Math.Max(1, (int)(Bounds.Height / RowHeight))); e.Handled = true; break;
        }
    }

    private void Navigate(int colDelta, int rowDelta)
    {
        int count = GetItemCount();
        if (count == 0) return;
        int newIdx = Math.Clamp(SelectedIndex + rowDelta, 0, count - 1);
        SelectedIndex = newIdx;
        EnsureRowVisible(newIdx);
        UpdateScrollBars();
        UpdateViewport();
        int colIdx = _currentCell?.Column != null ? Columns.IndexOf(_currentCell.Column) : 0;
        colIdx = Math.Clamp(colIdx + colDelta, 0, Math.Max(0, Columns.Count - 1));
        var row = _realizedRows.FirstOrDefault(r => r.Index == newIdx);
        if (row != null && colIdx < row.Cells.Count)
        {
            if (_currentCell != null) _currentCell.IsSelected = false;
            var cell = row.Cells[colIdx];
            cell.IsSelected = true;
            _currentCell = cell;
        }
    }

    private void EnsureRowVisible(int rowIndex)
    {
        double vpH = GetViewportHeight();
        if (vpH <= 0) return;
        double rowTop = rowIndex * RowHeight;
        double rowBottom = rowTop + RowHeight;
        if (rowTop < _verticalOffset)
            _verticalOffset = rowTop;
        else if (rowBottom > _verticalOffset + vpH)
            _verticalOffset = rowBottom - vpH;
        double maxV = Math.Max(0, GetItemCount() * RowHeight - vpH);
        _verticalOffset = Math.Clamp(_verticalOffset, 0, maxV);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); Focus(); }

    private INotifyCollectionChanged? _subscribedSource;
    private bool _searchRefreshQueued;

    private void SubscribeCollectionChanged()
    {
        if (_subscribedSource != null)
        {
            _subscribedSource.CollectionChanged -= OnItemsSourceCollectionChanged;
            _subscribedSource = null;
        }
        if (ItemsSource is INotifyCollectionChanged incc)
        {
            _subscribedSource = incc;
            _subscribedSource.CollectionChanged += OnItemsSourceCollectionChanged;
        }
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            InvalidateItemsCache();
            UpdateStatusBar();
            UpdateScrollBars();
            UpdateViewport();

            if (_searchRefreshQueued || _searchAdapter == null) return;
            _searchRefreshQueued = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _searchRefreshQueued = false;
                _searchAdapter?.RefreshResults();
            }, Avalonia.Threading.DispatcherPriority.Background);
        });
    }

    public void RefreshRows()
    {
        SubscribeCollectionChanged();
        InvalidateItemsCache();
        UpdateStatusBar();
        UpdateViewport();
    }

    internal void OnColumnBindingChanged(DataGridColumn column) { }

    internal void SetVerticalOffset(double offset)
    {
        _verticalOffset = offset;
        UpdateScrollBars();
        SyncHorizontalOffset();
        UpdateViewport();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty) { InvalidateItemsCache(); UpdateStatusBar(); RefreshRows(); }
        else if (change.Property == SearchModelProperty)
        {
            _usesExternalSearchModel = SearchModel != null;
            AttachSearchModel(SearchModel ?? new SearchModel { HighlightMode = SearchHighlightMode });
        }
        else if (change.Property == SelectedIndexProperty) UpdateViewport();
        else if (change.Property == GridLinesVisibilityProperty)
        {
            foreach (var row in _realizedRows) row.RefreshVisuals();
        }
    }

    private static void ApplyScrollBarTheme(ScrollBar scrollbar)
    {
        try
        {
            var resourceKey = scrollbar.Orientation == Avalonia.Layout.Orientation.Vertical
                ? "SemiVerticalScrollBar"
                : "SemiScrollBar";
            if (Application.Current?.TryGetResource(resourceKey, null, out var value) == true
                && value is Avalonia.Styling.ControlTheme ct)
            {
                scrollbar.Theme = ct;
                scrollbar.InvalidateVisual();
            }
        }
        catch { }
    }

    #region Validation

    internal static string? GetColumnBindingPath(DataGridColumn column)
    {
        if (column is DataGridBoundColumn bound && !string.IsNullOrEmpty(bound.BindingPath))
            return bound.BindingPath;
        return null;
    }

    internal static void ClearCellValidation(DataGridCell cell)
    {
        cell.SetValidationMessage(null, DataGridValidationSeverity.None, Colors.Transparent);
        cell.IsValid = true;
    }

    internal void RestoreRowValidationState(DataGridRow row)
    {
        if (row.DataContext is not INotifyDataErrorInfo indei)
            return;

        foreach (var cell in row.Cells)
        {
            var col = cell.Column;
            if (col == null)
            {
                cell.SetValidationMessage(null, DataGridValidationSeverity.None, Colors.Transparent);
                continue;
            }
            var path = GetColumnBindingPath(col);
            if (string.IsNullOrWhiteSpace(path))
            {
                cell.SetValidationMessage(null, DataGridValidationSeverity.None, Colors.Transparent);
                continue;
            }

            var errors = indei.GetErrors(path);
            if (errors == null)
            {
                cell.SetValidationMessage(null, DataGridValidationSeverity.None, Colors.Transparent);
                continue;
            }

            var exceptions = ValidationUtil.CreateValidationExceptions(errors);
            if (exceptions.Count == 0)
            {
                cell.SetValidationMessage(null, DataGridValidationSeverity.None, Colors.Transparent);
                continue;
            }

            var severity = ValidationUtil.GetValidationSeverity(exceptions);
            string? message = null;
            if (exceptions.Count > 0)
            {
                message = exceptions.Count == 1
                    ? exceptions[0].Message
                    : string.Join(Environment.NewLine, exceptions.Select(ex => ex.Message));
            }
            cell.IsValid = severity != DataGridValidationSeverity.InValid;
            var iconColor = severity switch
            {
                DataGridValidationSeverity.InValid => Color.Parse("#E81123"),
                DataGridValidationSeverity.Warning => Color.Parse("#FFB900"),
                DataGridValidationSeverity.Info => Color.Parse("#0078D4"),
                _ => Colors.Transparent
            };
            cell.SetValidationMessage(message, severity, iconColor);
        }

        bool hasError = false;
        foreach (var cell in row.Cells)
        {
            if (cell.ValidationSeverity == DataGridValidationSeverity.InValid)
            { hasError = true; break; }
        }
        row.IsValid = !hasError;
        row.ValidationSeverity = hasError ? DataGridValidationSeverity.InValid : DataGridValidationSeverity.None;
        UpdateGridValidationState();
    }

    internal void UpdateGridValidationState()
    {
        bool hasError = false;
        foreach (var row in _realizedRows)
        {
            if (!row.IsValid || row.ValidationSeverity == DataGridValidationSeverity.InValid)
            { hasError = true; break; }
        }
        if (!hasError) hasError = _validationItemsWithError.Count > 0;
        IsValid = !hasError;
    }

    internal void SubscribeItemValidation(object? item)
    {
        if (item is INotifyDataErrorInfo indei)
        {
            if (_validationTrackedItems.Add(indei))
                indei.ErrorsChanged += OnItemErrorsChanged;
        }
    }

    internal void UnsubscribeItemValidation(object? item)
    {
        if (item is INotifyDataErrorInfo indei)
        {
            if (_validationTrackedItems.Remove(indei))
            {
                indei.ErrorsChanged -= OnItemErrorsChanged;
                _validationItemsWithError.Remove(indei);
            }
        }
    }

    private void OnItemErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
    {
        if (sender is not INotifyDataErrorInfo indei) return;

        bool hasItemError = false;
        foreach (var col in Columns)
        {
            var path = GetColumnBindingPath(col);
            if (string.IsNullOrWhiteSpace(path)) continue;
            var errors = indei.GetErrors(path);
            if (errors == null) continue;
            var exceptions = ValidationUtil.CreateValidationExceptions(errors);
            if (exceptions.Count > 0 && ValidationUtil.GetValidationSeverity(exceptions) == DataGridValidationSeverity.InValid)
            { hasItemError = true; break; }
        }

        if (hasItemError) _validationItemsWithError.Add(indei);
        else _validationItemsWithError.Remove(indei);

        foreach (var row in _realizedRows)
        {
            if (ReferenceEquals(row.DataContext, indei))
            {
                RestoreRowValidationState(row);
                row.RefreshVisuals();
                return;
            }
        }
        UpdateGridValidationState();
    }

    #endregion
}

internal class DataGridScrollPanel : Panel
{
    internal DataGrid? OwningGrid { get; set; }

    private Border? _leftFrozenShadow;
    private Border? _rightFrozenShadow;

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (OwningGrid == null) return base.ArrangeOverride(finalSize);

        double hOff = OwningGrid.HorizontalOffset;
        double rowH = OwningGrid.RowHeight;
        double vpW = finalSize.Width;
        var columns = OwningGrid.Columns;
        int leftFrozen = Math.Min(OwningGrid.LeftFrozenColumnCount, columns.Count);
        int rightFrozen = Math.Min(OwningGrid.RightFrozenColumnCount, columns.Count);
        int totalCount = columns.Count;

        double leftFrozenW = 0;
        for (int i = 0; i < leftFrozen; i++)
            leftFrozenW += columns[i].GetEffectiveWidth();

        double rightFrozenW = 0;
        for (int i = totalCount - rightFrozen; i < totalCount; i++)
            rightFrozenW += columns[i].GetEffectiveWidth();

        double totalW = columns.Sum(c => c.GetEffectiveWidth());

        double vpH = finalSize.Height;
        int itemCount = OwningGrid.GetItemCount();
        double totalH = itemCount * rowH;
        double vOff = OwningGrid.VerticalOffset;
        if (totalH > vpH)
        {
            double maxV = totalH - vpH;
            if (vOff > maxV)
            {
                vOff = maxV;
                OwningGrid.SetVerticalOffset(vOff);
            }
        }
        else
        {
            if (vOff != 0)
            {
                vOff = 0;
                OwningGrid.SetVerticalOffset(0);
            }
        }

        double scrollableW = totalW - leftFrozenW - rightFrozenW;
        double scrollableVisible = vpW - leftFrozenW - rightFrozenW;
        if (scrollableVisible < 0) scrollableVisible = 0;
        double maxHOff = Math.Max(0, scrollableW - scrollableVisible);
        hOff = Math.Clamp(hOff, 0, maxHOff);

        EnsureFrozenShadows();

        double scrollableEnd = leftFrozenW + scrollableW - hOff;
        double rightFrozenMaxX = vpW - rightFrozenW;
        if (rightFrozenMaxX < 0) rightFrozenMaxX = 0;
        scrollableEnd = Math.Min(scrollableEnd, rightFrozenMaxX);

        bool hasLeftShadow = leftFrozen > 0 && scrollableW > scrollableVisible && hOff > 0;
        bool hasRightShadow = rightFrozen > 0 && scrollableW > scrollableVisible && hOff < maxHOff - 0.5;

        if (_leftFrozenShadow != null)
        {
            _leftFrozenShadow.IsVisible = hasLeftShadow;
            if (hasLeftShadow)
                _leftFrozenShadow.Arrange(new Rect(leftFrozenW - 6, 0, 6, finalSize.Height));
        }
        if (_rightFrozenShadow != null)
        {
            _rightFrozenShadow.IsVisible = hasRightShadow;
            if (hasRightShadow)
                _rightFrozenShadow.Arrange(new Rect(scrollableEnd - 1, 0, 6, finalSize.Height));
        }

        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is DataGridRow row)
            {
                double y = row.Index * rowH - vOff;
                row.Arrange(new Rect(0, y, vpW, rowH));
            }
        }

        return finalSize;
    }

    private void EnsureFrozenShadows()
    {
        if (_leftFrozenShadow != null) return;

        _leftFrozenShadow = new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Colors.Transparent),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 3,
                Blur = 6,
                Color = Color.Parse("#18000000"),
            }),
            IsHitTestVisible = false,
            ZIndex = 5,
        };
        _rightFrozenShadow = new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Colors.Transparent),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = -3,
                Blur = 6,
                Color = Color.Parse("#18000000"),
            }),
            IsHitTestVisible = false,
            ZIndex = 5,
        };
        Children.Add(_leftFrozenShadow);
        Children.Add(_rightFrozenShadow);
    }
}

public class DataGridColumnEventArgs : EventArgs
{
    public DataGridColumn Column { get; }
    public DataGridColumnEventArgs(DataGridColumn c) => Column = c;
}

public enum DataGridEditAction { Commit, Cancel }

public class DataGridCellEditEndingEventArgs : EventArgs
{
    public DataGridCell Cell { get; }
    public DataGridColumn? Column { get; }
    public DataGridEditAction EditAction { get; }
    public bool Cancel { get; set; }
    public DataGridCellEditEndingEventArgs(DataGridCell cell, DataGridColumn? column, DataGridEditAction action)
    { Cell = cell; Column = column; EditAction = action; }
}