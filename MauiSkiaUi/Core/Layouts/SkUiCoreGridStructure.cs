namespace MauiSkiaUi.Core;

/// <summary>
/// Placement of one child inside a Core grid (row/column indices and spans).
/// </summary>
public readonly struct SkUiCoreGridPlacement
{
    /// <summary>Default cell (0,0) with span 1×1.</summary>
    public static SkUiCoreGridPlacement Default { get; } = new(0, 0, 1, 1);

    /// <summary>Creates a placement.</summary>
    public SkUiCoreGridPlacement(int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowSpan, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columnSpan, 1);
        Row = row;
        Column = column;
        RowSpan = rowSpan;
        ColumnSpan = columnSpan;
    }

    /// <summary>Zero-based row index.</summary>
    public int Row { get; }

    /// <summary>Zero-based column index.</summary>
    public int Column { get; }

    /// <summary>Number of rows spanned (at least 1).</summary>
    public int RowSpan { get; }

    /// <summary>Number of columns spanned (at least 1).</summary>
    public int ColumnSpan { get; }
}

/// <summary>
/// Pure grid measure/arrange math for Core (MAUI <c>GridStructure</c> semantics plus per-track min/max).
/// Does not reference MAUI layout managers.
/// </summary>
internal sealed class SkUiCoreGridStructure
{
    private readonly Definition[] _rows;
    private readonly Definition[] _columns;
    private readonly Cell[] _cells;
    private readonly ISkUiCoreNode[] _children;
    private readonly Thickness _padding;
    private readonly double _rowSpacing;
    private readonly double _columnSpacing;
    private readonly double _widthConstraint;
    private readonly double _heightConstraint;
    private readonly double _starRowWeight;
    private readonly double _starColumnWeight;

    public SkUiCoreGridStructure(
        IReadOnlyList<SkUiCoreRowDefinition> rowDefinitions,
        IReadOnlyList<SkUiCoreColumnDefinition> columnDefinitions,
        IReadOnlyList<ISkUiCoreNode> children,
        Func<ISkUiCoreNode, SkUiCoreGridPlacement> getPlacement,
        Thickness padding,
        double rowSpacing,
        double columnSpacing,
        double widthConstraint,
        double heightConstraint)
    {
        _padding = padding;
        _rowSpacing = rowSpacing;
        _columnSpacing = columnSpacing;
        _widthConstraint = widthConstraint;
        _heightConstraint = heightConstraint;

        _rows = CreateRows(rowDefinitions);
        _columns = CreateColumns(columnDefinitions);
        _starRowWeight = CountStars(_rows);
        _starColumnWeight = CountStars(_columns);

        var visible = new List<ISkUiCoreNode>(children.Count);
        foreach (var child in children)
        {
            if (child.IsVisible)
                visible.Add(child);
        }

        _children = visible.ToArray();
        _cells = new Cell[_children.Length];
        for (var i = 0; i < _children.Length; i++)
        {
            var placement = getPlacement(_children[i]);
            var column = Math.Clamp(placement.Column, 0, _columns.Length - 1);
            var row = Math.Clamp(placement.Row, 0, _rows.Length - 1);
            var columnSpan = Math.Clamp(placement.ColumnSpan, 1, _columns.Length - column);
            var rowSpan = Math.Clamp(placement.RowSpan, 1, _rows.Length - row);
            _cells[i] = new Cell(i, row, column, rowSpan, columnSpan,
                LengthFlagsFor(_columns, column, columnSpan),
                LengthFlagsFor(_rows, row, rowSpan));
        }

        Measure();
    }

    public int RowCount => _rows.Length;
    public int ColumnCount => _columns.Length;

    public double MeasuredWidth =>
        SumSizes(_columns, _columnSpacing) + _padding.HorizontalThickness;

    public double MeasuredHeight =>
        SumSizes(_rows, _rowSpacing) + _padding.VerticalThickness;

    public double GetRowHeight(int index) => _rows[index].Size;
    public double GetColumnWidth(int index) => _columns[index].Size;

    public double GetRowOffset(int index)
    {
        var y = _padding.Top;
        for (var i = 0; i < index; i++)
            y += _rows[i].Size + _rowSpacing;
        return y;
    }

    public double GetColumnOffset(int index)
    {
        var x = _padding.Left;
        for (var i = 0; i < index; i++)
            x += _columns[i].Size + _columnSpacing;
        return x;
    }

    public Rect GetCellBounds(int row, int column, int rowSpan, int columnSpan)
    {
        row = Math.Clamp(row, 0, _rows.Length - 1);
        column = Math.Clamp(column, 0, _columns.Length - 1);
        rowSpan = Math.Clamp(rowSpan, 1, _rows.Length - row);
        columnSpan = Math.Clamp(columnSpan, 1, _columns.Length - column);

        var x = GetColumnOffset(column);
        var y = GetRowOffset(row);
        var width = 0.0;
        var height = 0.0;
        for (var c = column; c < column + columnSpan; c++)
            width += _columns[c].Size;
        for (var r = row; r < row + rowSpan; r++)
            height += _rows[r].Size;
        width += (columnSpan - 1) * _columnSpacing;
        height += (rowSpan - 1) * _rowSpacing;
        return new Rect(x, y, width, height);
    }

    public Rect GetChildBounds(ISkUiCoreNode child)
    {
        for (var i = 0; i < _children.Length; i++)
        {
            if (!ReferenceEquals(_children[i], child)) continue;
            var cell = _cells[i];
            return GetCellBounds(cell.Row, cell.Column, cell.RowSpan, cell.ColumnSpan);
        }

        return Rect.Zero;
    }

    /// <summary>
    /// True when any visible cell with a span greater than 1 crosses the horizontal boundary
    /// after row <paramref name="boundaryAfterRow"/> (between that row and the next).
    /// </summary>
    public bool RowBoundaryCrossedBySpan(int boundaryAfterRow)
    {
        foreach (var cell in _cells)
        {
            if (cell.RowSpan <= 1) continue;
            if (cell.Row <= boundaryAfterRow && cell.Row + cell.RowSpan - 1 > boundaryAfterRow)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when any visible cell with a span greater than 1 crosses the vertical boundary
    /// after column <paramref name="boundaryAfterColumn"/>.
    /// </summary>
    public bool ColumnBoundaryCrossedBySpan(int boundaryAfterColumn)
    {
        foreach (var cell in _cells)
        {
            if (cell.ColumnSpan <= 1) continue;
            if (cell.Column <= boundaryAfterColumn && cell.Column + cell.ColumnSpan - 1 > boundaryAfterColumn)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Expands star tracks into <paramref name="arrangeSize"/> (content size including padding).
    /// Call after measure before reading final track sizes for arrange.
    /// </summary>
    public void PrepareForArrange(Size arrangeSize)
    {
        var contentWidth = Math.Max(0, arrangeSize.Width - _padding.HorizontalThickness);
        var contentHeight = Math.Max(0, arrangeSize.Height - _padding.VerticalThickness);
        ExpandStars(_columns, contentWidth, _columnSpacing, _starColumnWeight);
        ExpandStars(_rows, contentHeight, _rowSpacing, _starRowWeight);
    }

    private void Measure()
    {
        // Absolute tracks: fixed (clamped). Auto/star start at Min.
        foreach (var def in _rows)
            def.ResetForMeasure();
        foreach (var def in _columns)
            def.ResetForMeasure();

        var contentWidthConstraint = double.IsInfinity(_widthConstraint)
            ? double.PositiveInfinity
            : Math.Max(0, _widthConstraint - _padding.HorizontalThickness);
        var contentHeightConstraint = double.IsInfinity(_heightConstraint)
            ? double.PositiveInfinity
            : Math.Max(0, _heightConstraint - _padding.VerticalThickness);

        // Pass 1: measure cells that do not depend on unresolved stars (or treat stars as auto when unconstrained).
        MeasureAutoAndAbsoluteCells(contentWidthConstraint, contentHeightConstraint, secondPass: false);

        // Resolve stars when we have a finite remaining space.
        ResolveStars(_columns, contentWidthConstraint, _columnSpacing, _starColumnWeight, isColumn: true);
        ResolveStars(_rows, contentHeightConstraint, _rowSpacing, _starRowWeight, isColumn: false);

        // Pass 2: measure cells that needed star sizes.
        MeasureAutoAndAbsoluteCells(contentWidthConstraint, contentHeightConstraint, secondPass: true);

        // Distribute span extras into Auto (then Star) tracks under Max.
        ResolveSpans(isColumn: true);
        ResolveSpans(isColumn: false);

        // Ensure empty Auto tracks still honor Min; stars keep at least MinimumSize for measure.
        foreach (var def in _rows)
            def.EnsureMinimum();
        foreach (var def in _columns)
            def.EnsureMinimum();
    }

    private void MeasureAutoAndAbsoluteCells(double contentWidthConstraint, double contentHeightConstraint, bool secondPass)
    {
        for (var i = 0; i < _cells.Length; i++)
        {
            var cell = _cells[i];
            var widthKnown = IsSpanWidthKnown(cell, contentWidthConstraint);
            var heightKnown = IsSpanHeightKnown(cell, contentHeightConstraint);

            if (!secondPass)
            {
                // First pass: skip cells that still need star resolution under finite constraints.
                if ((!widthKnown && NeedsStarWidth(cell) && !double.IsInfinity(contentWidthConstraint))
                    || (!heightKnown && NeedsStarHeight(cell) && !double.IsInfinity(contentHeightConstraint)))
                    continue;
            }
            else
            {
                // Second pass: only cells that were deferred or need remeasure with stars.
                if (widthKnown && heightKnown && !NeedsStarWidth(cell) && !NeedsStarHeight(cell))
                {
                    // Already contributed in first pass for pure auto/absolute.
                }
            }

            var measureWidth = widthKnown
                ? SpanSize(_columns, cell.Column, cell.ColumnSpan, _columnSpacing)
                : (NeedsStarWidth(cell) && !double.IsInfinity(contentWidthConstraint)
                    ? SpanSize(_columns, cell.Column, cell.ColumnSpan, _columnSpacing)
                    : double.PositiveInfinity);

            var measureHeight = heightKnown
                ? SpanSize(_rows, cell.Row, cell.RowSpan, _rowSpacing)
                : (NeedsStarHeight(cell) && !double.IsInfinity(contentHeightConstraint)
                    ? SpanSize(_rows, cell.Row, cell.RowSpan, _rowSpacing)
                    : double.PositiveInfinity);

            if (secondPass && !NeedsStarWidth(cell) && !NeedsStarHeight(cell) && widthKnown && heightKnown)
            {
                // Pure auto already measured; still ok to remeasure for consistency when stars changed siblings.
            }

            var desired = _children[i].Measure(measureWidth, measureHeight);

            if (TreatWidthAsAuto(cell, contentWidthConstraint))
                ApplyDesiredToSpan(_columns, cell.Column, cell.ColumnSpan, _columnSpacing, desired.Width);
            if (TreatHeightAsAuto(cell, contentHeightConstraint))
                ApplyDesiredToSpan(_rows, cell.Row, cell.RowSpan, _rowSpacing, desired.Height);
        }
    }

    private bool TreatWidthAsAuto(Cell cell, double contentWidthConstraint) =>
        cell.HasAutoColumn || (cell.HasStarColumn && double.IsInfinity(contentWidthConstraint));

    private bool TreatHeightAsAuto(Cell cell, double contentHeightConstraint) =>
        cell.HasAutoRow || (cell.HasStarRow && double.IsInfinity(contentHeightConstraint));

    private bool NeedsStarWidth(Cell cell) => cell.HasStarColumn && !cell.HasAutoColumn;
    private bool NeedsStarHeight(Cell cell) => cell.HasStarRow && !cell.HasAutoRow;

    private bool IsSpanWidthKnown(Cell cell, double contentWidthConstraint)
    {
        if (cell.HasAutoColumn) return false;
        if (cell.HasStarColumn && double.IsInfinity(contentWidthConstraint)) return false;
        // Absolute-only, or stars already resolved under finite constraint.
        if (cell.HasStarColumn)
            return _columns[cell.Column].Size > 0 || _starColumnWeight == 0 || !double.IsInfinity(contentWidthConstraint);
        return true;
    }

    private bool IsSpanHeightKnown(Cell cell, double contentHeightConstraint)
    {
        if (cell.HasAutoRow) return false;
        if (cell.HasStarRow && double.IsInfinity(contentHeightConstraint)) return false;
        if (cell.HasStarRow)
            return _rows[cell.Row].Size > 0 || _starRowWeight == 0 || !double.IsInfinity(contentHeightConstraint);
        return true;
    }

    private void ResolveStars(Definition[] defs, double contentConstraint, double spacing, double starWeight, bool isColumn)
    {
        if (starWeight <= 0) return;
        if (double.IsInfinity(contentConstraint))
        {
            // Unconstrained: star tracks already grew like Auto via ApplyDesiredToSpan / MinimumSize.
            foreach (var def in defs)
            {
                if (def.IsStar)
                    def.SetSize(Math.Max(def.Size, def.MinimumSize));
            }
            return;
        }

        var used = 0.0;
        for (var i = 0; i < defs.Length; i++)
        {
            if (!defs[i].IsStar)
                used += defs[i].Size;
            if (i > 0) used += spacing;
        }

        var remaining = Math.Max(0, contentConstraint - used);
        DistributeStarSpace(defs, remaining, starWeight);
    }

    /// <summary>
    /// Assigns <paramref name="remaining"/> across star tracks by weight.
    /// Min (user min and content minimum) is a floor on that share; max is a ceiling.
    /// A track that hits either clamp is frozen and the difference is redistributed.
    /// </summary>
    private static void DistributeStarSpace(Definition[] defs, double remaining, double starWeight)
    {
        if (starWeight <= 0) return;

        var floor = new double[defs.Length];
        var floorSum = 0.0;
        for (var i = 0; i < defs.Length; i++)
        {
            if (!defs[i].IsStar) continue;
            var min = Math.Max(defs[i].UserMin, defs[i].MinimumSize);
            if (min > defs[i].UserMax) min = defs[i].UserMax;
            floor[i] = min;
            floorSum += min;
        }

        if (remaining <= floorSum + 0.001)
        {
            for (var i = 0; i < defs.Length; i++)
            {
                if (defs[i].IsStar)
                    defs[i].SetSize(floor[i]);
            }
            return;
        }

        var frozen = new bool[defs.Length];
        var budget = remaining;
        var weight = starWeight;
        for (var pass = 0; pass < defs.Length + 1 && weight > 0.0001; pass++)
        {
            var unit = budget / weight;
            var worst = -1;
            var worstGap = 0.0;
            for (var i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                if (!def.IsStar || frozen[i]) continue;
                var raw = unit * def.Length.Value;
                var gap = raw < floor[i] - 0.001 ? floor[i] - raw
                    : raw > def.UserMax + 0.001 ? raw - def.UserMax
                    : 0;
                if (gap > worstGap)
                {
                    worstGap = gap;
                    worst = i;
                }
            }

            if (worst < 0)
            {
                for (var i = 0; i < defs.Length; i++)
                {
                    var def = defs[i];
                    if (!def.IsStar || frozen[i]) continue;
                    def.SetSize(def.Clamp(unit * def.Length.Value));
                }
                return;
            }

            var clamped = defs[worst];
            var size = Math.Clamp(unit * clamped.Length.Value, floor[worst], clamped.UserMax);
            clamped.SetSize(size);
            frozen[worst] = true;
            budget -= size;
            weight -= clamped.Length.Value;
            if (budget < -0.001)
                break;
        }

        for (var i = 0; i < defs.Length; i++)
        {
            if (defs[i].IsStar && !frozen[i])
                defs[i].SetSize(floor[i]);
        }
    }

    private void ExpandStars(Definition[] defs, double contentSize, double spacing, double starWeight)
    {
        if (starWeight <= 0) return;

        // Non-star tracks keep their measured size. Stars are solved again for this slot
        // so a min is a floor on the proportional share, not a base the share is added to.
        var used = 0.0;
        for (var i = 0; i < defs.Length; i++)
        {
            if (!defs[i].IsStar)
                used += defs[i].Size;
            if (i > 0) used += spacing;
        }

        DistributeStarSpace(defs, Math.Max(0, contentSize - used), starWeight);
    }

    private void ResolveSpans(bool isColumn)
    {
        var defs = isColumn ? _columns : _rows;
        var spacing = isColumn ? _columnSpacing : _rowSpacing;

        foreach (var cell in _cells)
        {
            var span = isColumn ? cell.ColumnSpan : cell.RowSpan;
            if (span <= 1) continue;
            var start = isColumn ? cell.Column : cell.Row;
            var child = _children[cell.ViewIndex];
            var needed = isColumn ? child.DesiredSize.Width : child.DesiredSize.Height;
            GrowSpan(defs, start, span, spacing, needed);
        }
    }

    private static void GrowSpan(Definition[] defs, int start, int length, double spacing, double needed)
    {
        var current = SpanSize(defs, start, length, spacing);
        if (needed <= current) return;
        var deficit = needed - current;

        // Prefer Auto tracks with room under Max, then Star.
        deficit = DistributeDeficit(defs, start, length, deficit, preferAuto: true);
        if (deficit > 0.001)
            DistributeDeficit(defs, start, length, deficit, preferAuto: false);
    }

    private static double DistributeDeficit(Definition[] defs, int start, int endExclusiveSpan, double deficit, bool preferAuto)
    {
        var end = start + endExclusiveSpan;
        var candidates = 0;
        for (var i = start; i < end; i++)
        {
            var def = defs[i];
            var match = preferAuto ? def.IsAuto : def.IsStar;
            if (match && def.Size < def.UserMax - 0.0001)
                candidates++;
        }

        if (candidates == 0) return deficit;
        var share = deficit / candidates;
        var remaining = deficit;
        for (var i = start; i < end; i++)
        {
            var def = defs[i];
            var match = preferAuto ? def.IsAuto : def.IsStar;
            if (!match || def.Size >= def.UserMax - 0.0001) continue;
            var room = def.UserMax - def.Size;
            var add = Math.Min(share, room);
            def.Update(def.Size + add);
            remaining -= add;
        }

        return Math.Max(0, remaining);
    }

    private static void ApplyDesiredToSpan(Definition[] defs, int start, int span, double spacing, double desired)
    {
        if (span == 1)
        {
            defs[start].Update(desired);
            return;
        }

        var current = SpanSize(defs, start, span, spacing);
        if (desired <= current) return;
        GrowSpan(defs, start, span, spacing, desired);
    }

    private static double SpanSize(Definition[] defs, int start, int span, double spacing)
    {
        var sum = 0.0;
        for (var i = 0; i < span; i++)
        {
            sum += defs[start + i].Size;
            if (i > 0) sum += spacing;
        }
        return sum;
    }

    private static double SumSizes(Definition[] defs, double spacing)
    {
        var sum = 0.0;
        for (var i = 0; i < defs.Length; i++)
        {
            sum += defs[i].Size;
            if (i > 0) sum += spacing;
        }
        return sum;
    }

    private static double CountStars(Definition[] defs)
    {
        var weight = 0.0;
        foreach (var def in defs)
        {
            if (def.IsStar)
                weight += def.Length.Value;
        }
        return weight;
    }

    private static Definition[] CreateRows(IReadOnlyList<SkUiCoreRowDefinition> definitions)
    {
        if (definitions.Count == 0)
            return [new Definition(SkUiCoreGridLength.Star, 0, double.PositiveInfinity)];

        var rows = new Definition[definitions.Count];
        for (var i = 0; i < definitions.Count; i++)
        {
            var d = definitions[i];
            rows[i] = new Definition(d.Height, d.MinHeight, d.MaxHeight);
        }
        return rows;
    }

    private static Definition[] CreateColumns(IReadOnlyList<SkUiCoreColumnDefinition> definitions)
    {
        if (definitions.Count == 0)
            return [new Definition(SkUiCoreGridLength.Star, 0, double.PositiveInfinity)];

        var columns = new Definition[definitions.Count];
        for (var i = 0; i < definitions.Count; i++)
        {
            var d = definitions[i];
            columns[i] = new Definition(d.Width, d.MinWidth, d.MaxWidth);
        }
        return columns;
    }

    private static GridLengthFlags LengthFlagsFor(Definition[] defs, int start, int span)
    {
        var flags = GridLengthFlags.None;
        for (var i = start; i < start + span; i++)
        {
            if (defs[i].IsAuto) flags |= GridLengthFlags.Auto;
            if (defs[i].IsStar) flags |= GridLengthFlags.Star;
            if (defs[i].IsAbsolute) flags |= GridLengthFlags.Absolute;
        }
        return flags;
    }

    [Flags]
    private enum GridLengthFlags
    {
        None = 0,
        Absolute = 1,
        Auto = 2,
        Star = 4
    }

    private readonly struct Cell
    {
        public Cell(int viewIndex, int row, int column, int rowSpan, int columnSpan,
            GridLengthFlags columnFlags, GridLengthFlags rowFlags)
        {
            ViewIndex = viewIndex;
            Row = row;
            Column = column;
            RowSpan = rowSpan;
            ColumnSpan = columnSpan;
            ColumnFlags = columnFlags;
            RowFlags = rowFlags;
        }

        public int ViewIndex { get; }
        public int Row { get; }
        public int Column { get; }
        public int RowSpan { get; }
        public int ColumnSpan { get; }
        public GridLengthFlags ColumnFlags { get; }
        public GridLengthFlags RowFlags { get; }
        public bool HasAutoColumn => (ColumnFlags & GridLengthFlags.Auto) != 0;
        public bool HasStarColumn => (ColumnFlags & GridLengthFlags.Star) != 0;
        public bool HasAutoRow => (RowFlags & GridLengthFlags.Auto) != 0;
        public bool HasStarRow => (RowFlags & GridLengthFlags.Star) != 0;
    }

    private sealed class Definition
    {
        public Definition(SkUiCoreGridLength length, double userMin, double userMax)
        {
            Length = length;
            UserMin = Math.Max(0, userMin);
            UserMax = userMax < UserMin ? UserMin : userMax;
        }

        public SkUiCoreGridLength Length { get; }
        public double UserMin { get; }
        public double UserMax { get; }
        public double Size { get; private set; }
        public double MinimumSize { get; private set; }
        public bool IsAuto => Length.IsAuto;
        public bool IsStar => Length.IsStar;
        public bool IsAbsolute => Length.IsAbsolute;

        public void ResetForMeasure()
        {
            MinimumSize = UserMin;
            if (IsAbsolute)
                SetSize(Clamp(Length.Value));
            else
                SetSize(UserMin);
        }

        public void EnsureMinimum() => SetSize(Math.Max(Size, Math.Max(MinimumSize, UserMin)));

        public double Clamp(double value) => Math.Min(UserMax, Math.Max(UserMin, value));

        public void SetSize(double value)
        {
            Size = Clamp(value);
            if (!IsStar)
                MinimumSize = Size;
        }

        public void Update(double value)
        {
            var next = Clamp(Math.Max(Size, value));
            if (next > Size)
                SetSize(next);
            if (IsStar)
                MinimumSize = Math.Max(MinimumSize, Clamp(value));
        }
    }
}
