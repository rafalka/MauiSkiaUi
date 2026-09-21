namespace MauiSkiaUi.Core;

/// <summary>
/// Core grid layout: Auto, absolute, and star tracks with optional per-track min/max,
/// row/column spans, and spacing. Owned algorithm (no MAUI <c>GridLayoutManager</c>).
/// Layout-only — track/cell chrome belongs on <see cref="SkUiCoreTable"/>.
/// </summary>
public class SkUiCoreGrid : SkUiCorePanel
{
    private readonly List<SkUiCoreRowDefinition> _rows = [];
    private readonly List<SkUiCoreColumnDefinition> _columns = [];
    private readonly Dictionary<ISkUiCoreNode, SkUiCoreGridPlacement> _placements = new();
    private double _rowSpacing;
    private double _columnSpacing;
    private SkUiCoreGridStructure? _structure;

    /// <summary>
    /// Row definitions (read-only). Empty implies one star row.
    /// Mutate via <see cref="SetRowDefinitions"/> or <see cref="AddRowDefinition"/>.
    /// </summary>
    public IReadOnlyList<SkUiCoreRowDefinition> RowDefinitions => _rows;

    /// <summary>
    /// Column definitions (read-only). Empty implies one star column.
    /// Mutate via <see cref="SetColumnDefinitions"/> or <see cref="AddColumnDefinition"/>.
    /// </summary>
    public IReadOnlyList<SkUiCoreColumnDefinition> ColumnDefinitions => _columns;

    /// <summary>Gap between rows in DIPs.</summary>
    public double RowSpacing
    {
        get => _rowSpacing;
        set => SetRowSpacing(value);
    }

    /// <summary>Gap between columns in DIPs.</summary>
    public double ColumnSpacing
    {
        get => _columnSpacing;
        set => SetColumnSpacing(value);
    }

    /// <summary>Number of rows used by the last measure/arrange (at least 1).</summary>
    public int RowCount => _structure?.RowCount ?? Math.Max(1, _rows.Count);

    /// <summary>Number of columns used by the last measure/arrange (at least 1).</summary>
    public int ColumnCount => _structure?.ColumnCount ?? Math.Max(1, _columns.Count);

    /// <inheritdoc cref="SkUiCorePanel.SetPadding" />
    public new SkUiCoreGrid SetPadding(Thickness value)
    {
        base.SetPadding(value);
        return this;
    }

    /// <summary>Sets the gap between rows in DIPs.</summary>
    public SkUiCoreGrid SetRowSpacing(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _rowSpacing, value, nameof(RowSpacing))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets the gap between columns in DIPs.</summary>
    public SkUiCoreGrid SetColumnSpacing(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _columnSpacing, value, nameof(ColumnSpacing))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Replaces row definitions and subscribes to size changes.</summary>
    public SkUiCoreGrid SetRowDefinitions(IEnumerable<SkUiCoreRowDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        UnsubscribeRows();
        _rows.Clear();
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            _rows.Add(definition);
            definition.SizeChanged += OnDefinitionSizeChanged;
        }
        InvalidateMeasure();
        return this;
    }

    /// <summary>Replaces column definitions and subscribes to size changes.</summary>
    public SkUiCoreGrid SetColumnDefinitions(IEnumerable<SkUiCoreColumnDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        UnsubscribeColumns();
        _columns.Clear();
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            _columns.Add(definition);
            definition.SizeChanged += OnDefinitionSizeChanged;
        }
        InvalidateMeasure();
        return this;
    }

    /// <summary>Appends a child in cell (0,0).</summary>
    public new SkUiCoreGrid Add(ISkUiCoreNode child) => Add(child, 0, 0);

    /// <summary>Appends a child at the given row and column.</summary>
    public SkUiCoreGrid Add(ISkUiCoreNode child, int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        ArgumentNullException.ThrowIfNull(child);
        InsertChild(Children.Count, child);
        _placements[child] = new SkUiCoreGridPlacement(row, column, rowSpan, columnSpan);
        return this;
    }

    /// <summary>Sets the grid cell for an existing child.</summary>
    public SkUiCoreGrid SetPlacement(ISkUiCoreNode child, int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!_placements.ContainsKey(child) && !Children.Contains(child))
            throw new ArgumentException("Child is not in this grid.", nameof(child));
        _placements[child] = new SkUiCoreGridPlacement(row, column, rowSpan, columnSpan);
        InvalidateMeasure();
        return this;
    }

    /// <summary>Gets the placement for <paramref name="child"/> (defaults to 0,0,1,1).</summary>
    public SkUiCoreGridPlacement GetPlacement(ISkUiCoreNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return _placements.TryGetValue(child, out var placement) ? placement : SkUiCoreGridPlacement.Default;
    }

    /// <summary>Sets the row index for <paramref name="child"/>.</summary>
    public SkUiCoreGrid SetRow(ISkUiCoreNode child, int row)
    {
        var p = GetPlacement(child);
        return SetPlacement(child, row, p.Column, p.RowSpan, p.ColumnSpan);
    }

    /// <summary>Sets the column index for <paramref name="child"/>.</summary>
    public SkUiCoreGrid SetColumn(ISkUiCoreNode child, int column)
    {
        var p = GetPlacement(child);
        return SetPlacement(child, p.Row, column, p.RowSpan, p.ColumnSpan);
    }

    /// <summary>Sets the row span for <paramref name="child"/>.</summary>
    public SkUiCoreGrid SetRowSpan(ISkUiCoreNode child, int rowSpan)
    {
        var p = GetPlacement(child);
        return SetPlacement(child, p.Row, p.Column, rowSpan, p.ColumnSpan);
    }

    /// <summary>Sets the column span for <paramref name="child"/>.</summary>
    public SkUiCoreGrid SetColumnSpan(ISkUiCoreNode child, int columnSpan)
    {
        var p = GetPlacement(child);
        return SetPlacement(child, p.Row, p.Column, p.RowSpan, columnSpan);
    }

    /// <summary>Removes a child and its placement.</summary>
    public new bool Remove(ISkUiCoreNode child)
    {
        _placements.Remove(child);
        return base.Remove(child);
    }

    /// <summary>Removes all children and placements.</summary>
    public new void Clear()
    {
        _placements.Clear();
        base.Clear();
    }

    /// <inheritdoc />
    protected override void OnChildRemoved(ISkUiCoreNode child) => _placements.Remove(child);

    /// <summary>Y origin of row <paramref name="index"/> within this grid's local content (includes padding).</summary>
    public double GetRowOffset(int index) =>
        EnsureStructureForQuery().GetRowOffset(index);

    /// <summary>X origin of column <paramref name="index"/> within this grid's local content (includes padding).</summary>
    public double GetColumnOffset(int index) =>
        EnsureStructureForQuery().GetColumnOffset(index);

    /// <summary>Arranged height of row <paramref name="index"/>.</summary>
    public double GetRowHeight(int index) =>
        EnsureStructureForQuery().GetRowHeight(index);

    /// <summary>Arranged width of column <paramref name="index"/>.</summary>
    public double GetColumnWidth(int index) =>
        EnsureStructureForQuery().GetColumnWidth(index);

    /// <summary>Union rect of the given cell range in local coordinates.</summary>
    public Rect GetCellBounds(int row, int column, int rowSpan = 1, int columnSpan = 1) =>
        EnsureStructureForQuery().GetCellBounds(row, column, rowSpan, columnSpan);

    /// <summary>
    /// True when a visible spanned cell crosses the horizontal boundary after
    /// <paramref name="boundaryAfterRow"/> (used by <see cref="SkUiCoreTable"/> separators).
    /// </summary>
    protected bool IsRowBoundaryCrossedBySpan(int boundaryAfterRow) =>
        _structure?.RowBoundaryCrossedBySpan(boundaryAfterRow) ?? false;

    /// <summary>
    /// True when a visible spanned cell crosses the vertical boundary after
    /// <paramref name="boundaryAfterColumn"/>.
    /// </summary>
    protected bool IsColumnBoundaryCrossedBySpan(int boundaryAfterColumn) =>
        _structure?.ColumnBoundaryCrossedBySpan(boundaryAfterColumn) ?? false;

    /// <summary>Last measured/arranged structure, or throws if none yet.</summary>
    private protected SkUiCoreGridStructure RequireStructure() => EnsureStructureForQuery();

    /// <summary>Adds a row definition and invalidates layout.</summary>
    public SkUiCoreGrid AddRowDefinition(SkUiCoreRowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.SizeChanged += OnDefinitionSizeChanged;
        _rows.Add(definition);
        InvalidateMeasure();
        return this;
    }

    /// <summary>Adds a column definition and invalidates layout.</summary>
    public SkUiCoreGrid AddColumnDefinition(SkUiCoreColumnDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.SizeChanged += OnDefinitionSizeChanged;
        _columns.Add(definition);
        InvalidateMeasure();
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        EnsureDefinitionSubscriptions();
        _structure = new SkUiCoreGridStructure(
            _rows, _columns, Children, GetPlacement,
            Padding, _rowSpacing, _columnSpacing,
            widthConstraint, heightConstraint);
        return new Size(_structure.MeasuredWidth, _structure.MeasuredHeight);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        if (_structure is null)
            MeasureContent(size.Width, size.Height);

        _structure!.PrepareForArrange(size);

        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                child.Arrange(Rect.Zero);
                continue;
            }

            var cell = _structure.GetChildBounds(child);
            if (child is SkUiCoreNode node)
                cell = SkUiCoreAbsoluteLayout.AlignInSlot(
                    cell, child.DesiredSize, node.HorizontalAlignment, node.VerticalAlignment);
            child.Arrange(cell);
        }
    }

    private SkUiCoreGridStructure EnsureStructureForQuery()
    {
        if (_structure is null)
            throw new InvalidOperationException("Grid has not been measured/arranged yet.");
        return _structure;
    }

    private void OnDefinitionSizeChanged(object? sender, EventArgs e) => InvalidateMeasure();

    private void EnsureDefinitionSubscriptions()
    {
        foreach (var row in _rows)
        {
            row.SizeChanged -= OnDefinitionSizeChanged;
            row.SizeChanged += OnDefinitionSizeChanged;
        }
        foreach (var column in _columns)
        {
            column.SizeChanged -= OnDefinitionSizeChanged;
            column.SizeChanged += OnDefinitionSizeChanged;
        }
    }

    private void UnsubscribeRows()
    {
        foreach (var row in _rows)
            row.SizeChanged -= OnDefinitionSizeChanged;
    }

    private void UnsubscribeColumns()
    {
        foreach (var column in _columns)
            column.SizeChanged -= OnDefinitionSizeChanged;
    }
}
