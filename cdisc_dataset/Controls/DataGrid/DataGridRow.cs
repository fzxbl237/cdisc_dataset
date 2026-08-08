using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace cdisc_dataset.Controls.DataGrid;

public class DataGridRow : Control
{
    public static readonly new StyledProperty<double> HeightProperty =
        AvaloniaProperty.Register<DataGridRow, double>(nameof(Height), 28);
    public static readonly StyledProperty<int> IndexProperty =
        AvaloniaProperty.Register<DataGridRow, int>(nameof(Index));
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<DataGridRow, bool>(nameof(IsSelected));
    public static readonly StyledProperty<bool> IsValidProperty =
        AvaloniaProperty.Register<DataGridRow, bool>(nameof(IsValid), true);
    public static readonly StyledProperty<DataGridValidationSeverity> ValidationSeverityProperty =
        AvaloniaProperty.Register<DataGridRow, DataGridValidationSeverity>(nameof(ValidationSeverity));

    private readonly List<DataGridCell> _cells = new();

    public int Index { get => GetValue(IndexProperty); set => SetValue(IndexProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set { SetValue(IsSelectedProperty, value); RefreshVisuals(); } }
    public bool IsValid { get => GetValue(IsValidProperty); set => SetValue(IsValidProperty, value); }
    public DataGridValidationSeverity ValidationSeverity { get => GetValue(ValidationSeverityProperty); set => SetValue(ValidationSeverityProperty, value); }
    public new double Height { get => GetValue(HeightProperty); set => SetValue(HeightProperty, value); }
    public IReadOnlyList<DataGridCell> Cells => _cells;
    public DataGrid? OwningGrid { get; internal set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var cell in _cells) cell.EnsureBuilt();
        double w = OwningGrid?.Columns.Sum(c => c.GetEffectiveWidth()) ?? 0;
        return new Size(w, availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var grid = OwningGrid;
        if (grid == null) return base.ArrangeOverride(finalSize);

        var columns = grid.Columns;
        int leftFrozen = Math.Min(grid.LeftFrozenColumnCount, columns.Count);
        int rightFrozen = Math.Min(grid.RightFrozenColumnCount, columns.Count);
        int totalCount = columns.Count;
        double hOff = grid.HorizontalOffset;
        double vpW = finalSize.Width;

        double leftFrozenW = 0;
        for (int i = 0; i < leftFrozen; i++)
            leftFrozenW += columns[i].GetEffectiveWidth();

        double scrollableW = 0;
        for (int i = leftFrozen; i < totalCount - rightFrozen; i++)
            scrollableW += columns[i].GetEffectiveWidth();

        double rightFrozenW = 0;
        for (int i = totalCount - rightFrozen; i < totalCount; i++)
            rightFrozenW += columns[i].GetEffectiveWidth();

        double scrollableEnd = leftFrozenW + scrollableW - hOff;
        double rightFrozenMaxX = vpW - rightFrozenW;
        if (rightFrozenMaxX < 0) rightFrozenMaxX = 0;
        scrollableEnd = Math.Min(scrollableEnd, rightFrozenMaxX);

        foreach (var cell in _cells)
        {
            var col = cell.Column;
            if (col == null) continue;
            int colIdx = columns.IndexOf(col);
            double w = col.GetEffectiveWidth();

            double cellX;
            if (colIdx < leftFrozen)
            {
                double pos = 0;
                for (int i = 0; i < colIdx; i++) pos += columns[i].GetEffectiveWidth();
                cellX = pos;
            }
            else if (colIdx >= totalCount - rightFrozen)
            {
                double pos = scrollableEnd;
                for (int i = totalCount - rightFrozen; i < colIdx; i++) pos += columns[i].GetEffectiveWidth();
                cellX = pos;
            }
            else
            {
                double pos = leftFrozenW;
                for (int i = leftFrozen; i < colIdx; i++) pos += columns[i].GetEffectiveWidth();
                cellX = pos - hOff;
            }

            cell.Arrange(new Rect(cellX, 0, w, finalSize.Height));
        }

        ClipToBounds = (leftFrozen > 0 || rightFrozen > 0);
        return finalSize;
    }

    public void UpdateCells()
    {
        // Remove only previously-built cells, keep the persistent horizontal line
        foreach (var cell in _cells)
        {
            LogicalChildren.Remove(cell);
            VisualChildren.Remove(cell);
        }
        _cells.Clear();
        if (OwningGrid == null || DataContext == null) return;

        var columns = OwningGrid.Columns;
        int leftFrozen = Math.Min(OwningGrid.LeftFrozenColumnCount, columns.Count);
        int rightFrozen = Math.Min(OwningGrid.RightFrozenColumnCount, columns.Count);
        int totalCount = columns.Count;

        for (int ci = 0; ci < columns.Count; ci++)
        {
            if (!columns[ci].IsVisible) continue;
            if (ci >= leftFrozen && ci < totalCount - rightFrozen)
            {
                var column = columns[ci];
                var cell = new DataGridCell { Column = column, DataItem = DataContext, OwningRow = this, Width = column.GetEffectiveWidth() };
                cell.ContentControl = column.GenerateElement(cell, DataContext);
                cell.EnsureBuilt();
                _cells.Add(cell); LogicalChildren.Add(cell); VisualChildren.Add(cell);
            }
        }
        for (int ci = 0; ci < columns.Count; ci++)
        {
            if (!columns[ci].IsVisible) continue;
            if (ci < leftFrozen || ci >= totalCount - rightFrozen)
            {
                var column = columns[ci];
                var cell = new DataGridCell { Column = column, DataItem = DataContext, OwningRow = this, Width = column.GetEffectiveWidth() };
                cell.ContentControl = column.GenerateElement(cell, DataContext);
                cell.EnsureBuilt();
                _cells.Add(cell); LogicalChildren.Add(cell); VisualChildren.Add(cell);
            }
        }

        RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        foreach (var cell in _cells)
        {
            if (cell == null) continue;
            cell.UpdateBg();
        }
    }

    public void RefreshSearchHighlights()
    {
        foreach (var cell in _cells)
            cell.UpdateSearchHighlight();
    }
}

