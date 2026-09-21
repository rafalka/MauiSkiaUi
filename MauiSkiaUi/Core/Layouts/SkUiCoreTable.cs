using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core grid that paints per-row / per-column / per-cell backgrounds and span-aware separator lines
/// in the Background layer (before children). Layout matches <see cref="SkUiCoreGrid"/>.
/// </summary>
public class SkUiCoreTable : SkUiCoreGrid
{
    private readonly Dictionary<int, Color> _rowBackgrounds = new();
    private readonly Dictionary<int, Color> _columnBackgrounds = new();
    private readonly Dictionary<(int Row, int Column), CellBackground> _cellBackgrounds = new();
    private Color? _rowSeparatorColor;
    private Color? _columnSeparatorColor;
    private double _rowSeparatorThickness = 1;
    private double _columnSeparatorThickness = 1;
    private SkUiCoreTableTrackBackgroundOrder _trackBackgroundOrder =
        SkUiCoreTableTrackBackgroundOrder.ColumnsOverRows;

    /// <summary>Creates a table and registers the track-chrome Background painter.</summary>
    public SkUiCoreTable() => SetPaintBackground(PaintTableBackground);

    /// <inheritdoc cref="SkUiCoreGrid.SetPadding" />
    public new SkUiCoreTable SetPadding(Thickness value)
    {
        base.SetPadding(value);
        return this;
    }

    /// <inheritdoc cref="SkUiCoreGrid.SetRowSpacing" />
    public new SkUiCoreTable SetRowSpacing(double value)
    {
        base.SetRowSpacing(value);
        return this;
    }

    /// <inheritdoc cref="SkUiCoreGrid.SetColumnSpacing" />
    public new SkUiCoreTable SetColumnSpacing(double value)
    {
        base.SetColumnSpacing(value);
        return this;
    }

    /// <inheritdoc cref="SkUiCoreGrid.SetRowDefinitions" />
    public new SkUiCoreTable SetRowDefinitions(IEnumerable<SkUiCoreRowDefinition> definitions)
    {
        base.SetRowDefinitions(definitions);
        return this;
    }

    /// <inheritdoc cref="SkUiCoreGrid.SetColumnDefinitions" />
    public new SkUiCoreTable SetColumnDefinitions(IEnumerable<SkUiCoreColumnDefinition> definitions)
    {
        base.SetColumnDefinitions(definitions);
        return this;
    }

    /// <summary>
    /// Which track fill covers the other at row/column intersections.
    /// Default is <see cref="SkUiCoreTableTrackBackgroundOrder.ColumnsOverRows"/>.
    /// </summary>
    public SkUiCoreTableTrackBackgroundOrder TrackBackgroundOrder
    {
        get => _trackBackgroundOrder;
        set => SetTrackBackgroundOrder(value);
    }

    /// <summary>Color of horizontal separators between rows, or <c>null</c> to hide them.</summary>
    public Color? RowSeparatorColor
    {
        get => _rowSeparatorColor;
        set => SetRowSeparatorColor(value);
    }

    /// <summary>Color of vertical separators between columns, or <c>null</c> to hide them.</summary>
    public Color? ColumnSeparatorColor
    {
        get => _columnSeparatorColor;
        set => SetColumnSeparatorColor(value);
    }

    /// <summary>Thickness of horizontal separators in DIPs.</summary>
    public double RowSeparatorThickness
    {
        get => _rowSeparatorThickness;
        set => SetRowSeparatorThickness(value);
    }

    /// <summary>Thickness of vertical separators in DIPs.</summary>
    public double ColumnSeparatorThickness
    {
        get => _columnSeparatorThickness;
        set => SetColumnSeparatorThickness(value);
    }

    /// <summary>Sets which track fill covers the other at intersections.</summary>
    public SkUiCoreTable SetTrackBackgroundOrder(SkUiCoreTableTrackBackgroundOrder value)
    {
        if (!SetProperty(ref _trackBackgroundOrder, value, nameof(TrackBackgroundOrder))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the horizontal separator color (<c>null</c> hides lines).</summary>
    public SkUiCoreTable SetRowSeparatorColor(Color? value)
    {
        if (Equals(_rowSeparatorColor, value)) return this;
        _rowSeparatorColor = value;
        OnPropertyChanged(nameof(RowSeparatorColor));
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the vertical separator color (<c>null</c> hides lines).</summary>
    public SkUiCoreTable SetColumnSeparatorColor(Color? value)
    {
        if (Equals(_columnSeparatorColor, value)) return this;
        _columnSeparatorColor = value;
        OnPropertyChanged(nameof(ColumnSeparatorColor));
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets horizontal separator thickness in DIPs.</summary>
    public SkUiCoreTable SetRowSeparatorThickness(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _rowSeparatorThickness, value, nameof(RowSeparatorThickness))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets vertical separator thickness in DIPs.</summary>
    public SkUiCoreTable SetColumnSeparatorThickness(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _columnSeparatorThickness, value, nameof(ColumnSeparatorThickness))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets or clears the background fill for row <paramref name="index"/>.</summary>
    public SkUiCoreTable SetRowBackground(int index, Color? color)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (color is null)
            _rowBackgrounds.Remove(index);
        else
            _rowBackgrounds[index] = color;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets or clears the background fill for column <paramref name="index"/>.</summary>
    public SkUiCoreTable SetColumnBackground(int index, Color? color)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (color is null)
            _columnBackgrounds.Remove(index);
        else
            _columnBackgrounds[index] = color;
        InvalidatePaint();
        return this;
    }

    /// <summary>
    /// Sets or clears a background fill for the cell at <paramref name="row"/>/<paramref name="column"/>.
    /// The fill covers the full arranged cell (including area behind centered children with margin).
    /// Pass <paramref name="color"/> as <c>null</c> to clear.
    /// </summary>
    public SkUiCoreTable SetCellBackground(int row, int column, Color? color, int rowSpan = 1, int columnSpan = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowSpan, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columnSpan, 1);
        var key = (row, column);
        if (color is null)
            _cellBackgrounds.Remove(key);
        else
            _cellBackgrounds[key] = new CellBackground(color, rowSpan, columnSpan);
        InvalidatePaint();
        return this;
    }

    /// <summary>Gets the background color for the cell origin at <paramref name="row"/>/<paramref name="column"/>, or <c>null</c>.</summary>
    public Color? GetCellBackground(int row, int column) =>
        _cellBackgrounds.TryGetValue((row, column), out var entry) ? entry.Color : null;

    /// <summary>
    /// Arranged rect for a stored cell background (uses the span recorded with
    /// <see cref="SetCellBackground"/>). Throws if the table has not been laid out.
    /// </summary>
    public Rect GetCellBackgroundRect(int row, int column)
    {
        if (!_cellBackgrounds.TryGetValue((row, column), out var entry))
            return GetCellBounds(row, column);
        return GetCellBounds(row, column, entry.RowSpan, entry.ColumnSpan);
    }

    /// <summary>Gets the background for row <paramref name="index"/>, or <c>null</c>.</summary>
    public Color? GetRowBackground(int index) =>
        _rowBackgrounds.TryGetValue(index, out var color) ? color : null;

    /// <summary>Gets the background for column <paramref name="index"/>, or <c>null</c>.</summary>
    public Color? GetColumnBackground(int index) =>
        _columnBackgrounds.TryGetValue(index, out var color) ? color : null;

    /// <summary>
    /// Local rect covering the full content width of row <paramref name="index"/>
    /// (after the last arrange). Used for tests and custom chrome.
    /// </summary>
    public Rect GetRowBackgroundRect(int index)
    {
        var y = GetRowOffset(index);
        var height = GetRowHeight(index);
        var width = Math.Max(0, Frame.Width - Padding.HorizontalThickness);
        return new Rect(Padding.Left, y, width, height);
    }

    /// <summary>
    /// Local rect covering the full content height of column <paramref name="index"/>
    /// (after the last arrange).
    /// </summary>
    public Rect GetColumnBackgroundRect(int index)
    {
        var x = GetColumnOffset(index);
        var width = GetColumnWidth(index);
        var height = Math.Max(0, Frame.Height - Padding.VerticalThickness);
        return new Rect(x, Padding.Top, width, height);
    }

    /// <summary>
    /// Horizontal separator segments along the boundary after row <paramref name="boundaryAfterRow"/>.
    /// Omits ranges crossed by a visible row-spanning cell. Y is the inner bottom edge of that row.
    /// </summary>
    public IReadOnlyList<Rect> GetRowSeparatorSegments(int boundaryAfterRow)
    {
        if (boundaryAfterRow < 0 || boundaryAfterRow >= RowCount - 1)
            return Array.Empty<Rect>();
        if (IsRowBoundaryCrossedBySpan(boundaryAfterRow))
        {
            // Split around spanned cells: emit segments only for columns not covered by a crossing span.
            return BuildRowSeparatorSegments(boundaryAfterRow);
        }

        var y = GetRowOffset(boundaryAfterRow) + GetRowHeight(boundaryAfterRow);
        var thickness = Math.Max(0, _rowSeparatorThickness);
        var x = Padding.Left;
        var width = Math.Max(0, Frame.Width - Padding.HorizontalThickness);
        // line at inner track edge (thickness drawn centered on Y in paint)
        return [new Rect(x, y, width, thickness)];
    }

    /// <summary>
    /// Vertical separator segments along the boundary after column <paramref name="boundaryAfterColumn"/>.
    /// Omits ranges crossed by a visible column-spanning cell. X is the inner right edge of that column.
    /// </summary>
    public IReadOnlyList<Rect> GetColumnSeparatorSegments(int boundaryAfterColumn)
    {
        if (boundaryAfterColumn < 0 || boundaryAfterColumn >= ColumnCount - 1)
            return Array.Empty<Rect>();
        if (!IsColumnBoundaryCrossedBySpan(boundaryAfterColumn))
        {
            var x = GetColumnOffset(boundaryAfterColumn) + GetColumnWidth(boundaryAfterColumn);
            var thickness = Math.Max(0, _columnSeparatorThickness);
            var y = Padding.Top;
            var height = Math.Max(0, Frame.Height - Padding.VerticalThickness);
            return [new Rect(x, y, thickness, height)];
        }

        return BuildColumnSeparatorSegments(boundaryAfterColumn);
    }

    /// <summary>Draws track fills, then cell fills, then separators.</summary>
    protected void PaintTableBackground(SKCanvas canvas)
    {
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        if (_trackBackgroundOrder == SkUiCoreTableTrackBackgroundOrder.RowsOverColumns)
        {
            PaintColumnBackgrounds(canvas, paint);
            PaintRowBackgrounds(canvas, paint);
        }
        else
        {
            PaintRowBackgrounds(canvas, paint);
            PaintColumnBackgrounds(canvas, paint);
        }

        PaintCellBackgrounds(canvas);

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeCap = SKStrokeCap.Square;

        if (_rowSeparatorColor is not null && _rowSeparatorThickness > 0)
        {
            paint.Color = ToSkColor(_rowSeparatorColor);
            paint.StrokeWidth = (float)_rowSeparatorThickness;
            for (var r = 0; r < RowCount - 1; r++)
            {
                foreach (var segment in GetRowSeparatorSegments(r))
                {
                    if (segment.Width <= 0) continue;
                    var y = (float)(segment.Y + segment.Height * 0.5);
                    canvas.DrawLine((float)segment.X, y, (float)(segment.X + segment.Width), y, paint);
                }
            }
        }

        if (_columnSeparatorColor is not null && _columnSeparatorThickness > 0)
        {
            paint.Color = ToSkColor(_columnSeparatorColor);
            paint.StrokeWidth = (float)_columnSeparatorThickness;
            for (var c = 0; c < ColumnCount - 1; c++)
            {
                foreach (var segment in GetColumnSeparatorSegments(c))
                {
                    if (segment.Height <= 0) continue;
                    var x = (float)(segment.X + segment.Width * 0.5);
                    canvas.DrawLine(x, (float)segment.Y, x, (float)(segment.Y + segment.Height), paint);
                }
            }
        }
    }

    private void PaintRowBackgrounds(SKCanvas canvas, SKPaint paint)
    {
        for (var r = 0; r < RowCount; r++)
        {
            if (!_rowBackgrounds.TryGetValue(r, out var color)) continue;
            paint.Color = ToSkColor(color);
            canvas.DrawRect(ToSk(GetRowBackgroundRect(r)), paint);
        }
    }

    private void PaintColumnBackgrounds(SKCanvas canvas, SKPaint paint)
    {
        for (var c = 0; c < ColumnCount; c++)
        {
            if (!_columnBackgrounds.TryGetValue(c, out var color)) continue;
            paint.Color = ToSkColor(color);
            canvas.DrawRect(ToSk(GetColumnBackgroundRect(c)), paint);
        }
    }

    private void PaintCellBackgrounds(SKCanvas canvas)
    {
        if (_cellBackgrounds.Count == 0) return;
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        foreach (var ((row, column), entry) in _cellBackgrounds)
        {
            paint.Color = ToSkColor(entry.Color);
            canvas.DrawRect(ToSk(GetCellBounds(row, column, entry.RowSpan, entry.ColumnSpan)), paint);
        }
    }

    private IReadOnlyList<Rect> BuildRowSeparatorSegments(int boundaryAfterRow)
    {
        var y = GetRowOffset(boundaryAfterRow) + GetRowHeight(boundaryAfterRow);
        var thickness = Math.Max(0, _rowSeparatorThickness);
        var segments = new List<Rect>();
        var covered = new bool[ColumnCount];
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            var p = GetPlacement(child);
            if (p.RowSpan <= 1) continue;
            if (p.Row <= boundaryAfterRow && p.Row + p.RowSpan - 1 > boundaryAfterRow)
            {
                var col = Math.Clamp(p.Column, 0, ColumnCount - 1);
                var span = Math.Clamp(p.ColumnSpan, 1, ColumnCount - col);
                for (var c = col; c < col + span; c++)
                    covered[c] = true;
            }
        }

        var runStart = -1;
        for (var c = 0; c <= ColumnCount; c++)
        {
            var open = c < ColumnCount && !covered[c];
            if (open && runStart < 0) runStart = c;
            if ((!open || c == ColumnCount) && runStart >= 0)
            {
                var end = c;
                var x = GetColumnOffset(runStart);
                var right = GetColumnOffset(end - 1) + GetColumnWidth(end - 1);
                segments.Add(new Rect(x, y, Math.Max(0, right - x), thickness));
                runStart = -1;
            }
        }

        return segments;
    }

    private IReadOnlyList<Rect> BuildColumnSeparatorSegments(int boundaryAfterColumn)
    {
        var x = GetColumnOffset(boundaryAfterColumn) + GetColumnWidth(boundaryAfterColumn);
        var thickness = Math.Max(0, _columnSeparatorThickness);
        var segments = new List<Rect>();
        var covered = new bool[RowCount];
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            var p = GetPlacement(child);
            if (p.ColumnSpan <= 1) continue;
            if (p.Column <= boundaryAfterColumn && p.Column + p.ColumnSpan - 1 > boundaryAfterColumn)
            {
                var row = Math.Clamp(p.Row, 0, RowCount - 1);
                var span = Math.Clamp(p.RowSpan, 1, RowCount - row);
                for (var r = row; r < row + span; r++)
                    covered[r] = true;
            }
        }

        var runStart = -1;
        for (var r = 0; r <= RowCount; r++)
        {
            var open = r < RowCount && !covered[r];
            if (open && runStart < 0) runStart = r;
            if ((!open || r == RowCount) && runStart >= 0)
            {
                var end = r;
                var y = GetRowOffset(runStart);
                var bottom = GetRowOffset(end - 1) + GetRowHeight(end - 1);
                segments.Add(new Rect(x, y, thickness, Math.Max(0, bottom - y)));
                runStart = -1;
            }
        }

        return segments;
    }

    private static SKRect ToSk(Rect rect) =>
        new((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);

    private readonly record struct CellBackground(Color Color, int RowSpan, int ColumnSpan);
}
