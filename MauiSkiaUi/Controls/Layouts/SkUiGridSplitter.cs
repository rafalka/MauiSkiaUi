namespace MauiSkiaUi;

/// <summary>
/// A <see cref="SkUiDragger"/> that resizes the rows (or columns) of the grid it is in: dragging it gives one track the
/// pointer's travel and takes it from the other. Which tracks is <see cref="ResizeBehavior"/>: by default, the ones around a
/// splitter in a row (or column) of its own, or, for a splitter aligned to the end of a cell (a table header's column
/// boundary), its own track and the next one. It works in a <see cref="SkUiGrid"/> and, as a surface of its own, in MAUI's
/// <see cref="Grid"/>. Two star tracks keep sharing the grid in the new proportion; next to a star track the other one
/// becomes absolute; Auto and absolute tracks become absolute. Arrow keys move it by
/// <see cref="SkUiDragger.KeyboardIncrement"/>; assistive technologies see a slider over the size of the first track. Limits
/// per track: <see cref="MinimumLengthProperty"/> and <see cref="MaximumLengthProperty"/> on its row or column definition.
/// <see cref="SkUiDragger.DragAxis"/> does not apply: the splitter follows its resize direction.
/// </summary>
public class SkUiGridSplitter : SkUiDragger, ISkUiGridSplitterOwner
{
    // Field initializers, not constructor assignments: an implicit style applies before the constructor body runs.
    private SkUiGridResizeDirection _resizeDirection;
    private SkUiGridResizeBehavior _resizeBehavior;

    /// <summary>Bindable <see cref="ResizeDirection"/>.</summary>
    public static readonly BindableProperty ResizeDirectionProperty = BindableProperty.Create(nameof(ResizeDirection), typeof(SkUiGridResizeDirection),
        typeof(SkUiGridSplitter), SkUiGridResizeDirection.Auto, validateValue: SkUiValidate.ResizeDirection,
        propertyChanged: (view, _, value) => ((SkUiGridSplitter)view).OnResizeDirectionChanged((SkUiGridResizeDirection)value));

    /// <summary>Bindable <see cref="ResizeBehavior"/>.</summary>
    public static readonly BindableProperty ResizeBehaviorProperty = BindableProperty.Create(nameof(ResizeBehavior), typeof(SkUiGridResizeBehavior),
        typeof(SkUiGridSplitter), SkUiGridResizeBehavior.BasedOnAlignment, validateValue: SkUiValidate.ResizeBehavior,
        propertyChanged: (view, _, value) => ((SkUiGridSplitter)view)._resizeBehavior = (SkUiGridResizeBehavior)value);

    /// <summary>
    /// Attached to a <see cref="RowDefinition"/> or <see cref="ColumnDefinition"/>: the smallest size, in DIPs, a splitter
    /// drag leaves that track (default 0). MAUI's definitions have no minimum of their own; this one limits splitters only.
    /// </summary>
    public static readonly BindableProperty MinimumLengthProperty = BindableProperty.CreateAttached("MinimumLength", typeof(double), typeof(SkUiGridSplitter), 0d,
        validateValue: SkUiValidate.NonNegative);

    /// <summary>
    /// Attached to a <see cref="RowDefinition"/> or <see cref="ColumnDefinition"/>: the largest size, in DIPs, a splitter
    /// drag gives that track (default: no limit).
    /// </summary>
    public static readonly BindableProperty MaximumLengthProperty = BindableProperty.CreateAttached("MaximumLength", typeof(double), typeof(SkUiGridSplitter),
        double.PositiveInfinity, validateValue: SkUiValidate.SizeLimit);

    /// <summary>The smallest size a splitter drag leaves <paramref name="definition"/>'s track.</summary>
    public static double GetMinimumLength(BindableObject definition) => (double)definition.GetValue(MinimumLengthProperty);

    /// <summary>Sets the smallest size a splitter drag leaves <paramref name="definition"/>'s track.</summary>
    public static void SetMinimumLength(BindableObject definition, double value) => definition.SetValue(MinimumLengthProperty, value);

    /// <summary>The largest size a splitter drag gives <paramref name="definition"/>'s track.</summary>
    public static double GetMaximumLength(BindableObject definition) => (double)definition.GetValue(MaximumLengthProperty);

    /// <summary>Sets the largest size a splitter drag gives <paramref name="definition"/>'s track.</summary>
    public static void SetMaximumLength(BindableObject definition, double value) => definition.SetValue(MaximumLengthProperty, value);

    /// <summary>Rows, columns, or (default) chosen from the splitter's alignment and place in the grid.</summary>
    public SkUiGridResizeDirection ResizeDirection { get => (SkUiGridResizeDirection)GetValue(ResizeDirectionProperty); set => SetValue(ResizeDirectionProperty, value); }

    /// <summary>Which tracks the splitter resizes (default <see cref="SkUiGridResizeBehavior.BasedOnAlignment"/>).</summary>
    public SkUiGridResizeBehavior ResizeBehavior { get => (SkUiGridResizeBehavior)GetValue(ResizeBehaviorProperty); set => SetValue(ResizeBehaviorProperty, value); }

    /// <summary>Sets the resize direction (same as the property setter).</summary>
    public SkUiGridSplitter SetResizeDirection(SkUiGridResizeDirection value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), value, null);
        ResizeDirection = value;
        return this;
    }

    private void OnResizeDirectionChanged(SkUiGridResizeDirection value)
    {
        _resizeDirection = value;
        InvalidateMeasureOverride();
    }

    /// <summary>Sets the resize behavior (same as the property setter).</summary>
    public SkUiGridSplitter SetResizeBehavior(SkUiGridResizeBehavior value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), value, null);
        ResizeBehavior = value;
        return this;
    }

    /// <summary>The engine shared with <see cref="Core.SkUiCoreGridSplitter"/>, created on first use.</summary>
    private SkUiGridSplitterEngine Engine => field ??= new SkUiGridSplitterEngine(this);

    /// <summary>The grid the splitter is in, with its definitions: a <see cref="Grid"/> or a <see cref="SkUiGrid"/>.</summary>
    private (IGridLayout Grid, RowDefinitionCollection Rows, ColumnDefinitionCollection Columns)? Container => Parent switch
    {
        Grid grid => (grid, grid.RowDefinitions, grid.ColumnDefinitions),
        SkUiGrid grid => (grid, grid.RowDefinitions, grid.ColumnDefinitions),
        _ => null
    };

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();
        // An Auto direction depends on the grid's shape.
        InvalidateMeasureOverride();
    }

    /// <summary>Whether the splitter resizes columns: from <see cref="ResizeDirection"/>, its alignment and its place in the grid.</summary>
    internal bool ResizesColumns
    {
        get
        {
            var horizontal = HorizontalOptions.Alignment;
            var vertical = VerticalOptions.Alignment;
            if (Container is not { } container)
                return SkUiGridSplitterMath.ResizesColumns(_resizeDirection, horizontal, vertical, 1, 1, 1, 1, new Size(Width, Height));
            var grid = container.Grid;
            return SkUiGridSplitterMath.ResizesColumns(_resizeDirection, horizontal, vertical, Math.Max(1, container.Rows.Count),
                Math.Max(1, container.Columns.Count), grid.GetRowSpan(this), grid.GetColumnSpan(this), new Size(Width, Height));
        }
    }

    /// <inheritdoc />
    protected override SkUiPanAxis EffectiveDragAxis => ResizesColumns ? SkUiPanAxis.Horizontal : SkUiPanAxis.Vertical;

    /// <inheritdoc />
    protected override bool CanDrag => TrackIndices(out _) is not null;

    /// <inheritdoc />
    protected override void OnDragStarted(SkUiDragEventArgs args)
    {
        Engine.Started();
        base.OnDragStarted(args);
    }

    /// <inheritdoc />
    protected override void OnDragDelta(SkUiDragEventArgs args)
    {
        Engine.Delta(args);
        base.OnDragDelta(args);
    }

    /// <inheritdoc />
    protected override void OnDragCompleted(SkUiDragEventArgs args)
    {
        Engine.Completed();
        base.OnDragCompleted(args);
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        Engine.PopulateSemantics(info, ResizesColumns);
    }

    /// <inheritdoc />
    protected override bool OnSemanticsAction(SkUiSemanticsActions action) => Engine.OnSemanticsAction(action) ?? base.OnSemanticsAction(action);

    /// <inheritdoc />
    protected override bool OnSemanticsSetValue(double value) => Engine.SetSemanticsValue(value);

    // The indices of the tracks the splitter resizes, or null when there are none; no track sizes, as the router's hit test asks (CanDrag).
    private (int First, int Second)? TrackIndices(out bool columns)
    {
        columns = false;
        if (Container is not { } container)
            return null;
        columns = ResizesColumns;
        var grid = container.Grid;
        var indices = SkUiGridSplitterMath.Indices(_resizeBehavior, columns ? HorizontalOptions.Alignment : VerticalOptions.Alignment,
            columns ? grid.GetColumn(this) : grid.GetRow(this), columns ? grid.GetColumnSpan(this) : grid.GetRowSpan(this));
        return SkUiGridSplitterMath.AreTracks(indices, columns ? container.Columns.Count : container.Rows.Count) ? indices : null;
    }

    SkUiGridSplitterTracks? ISkUiGridSplitterOwner.GetTracks()
    {
        if (TrackIndices(out var columns) is not { } indices || Container is not { } container)
            return null;
        var grid = container.Grid;
        var sizes = SkUiGridSplitterMath.TrackSizes(grid, columns);
        return new SkUiGridSplitterTracks(columns, indices.First, Track(indices.First), indices.Second,
            indices.Second < 0 ? null : Track(indices.Second));

        SkUiGridTrack Track(int at)
        {
            BindableObject definition = columns ? container.Columns[at] : container.Rows[at];
            var length = columns ? container.Columns[at].Width : container.Rows[at].Height;
            return new SkUiGridTrack(SkUiGridSplitterMath.Kind(length), length.Value, sizes[at], GetMinimumLength(definition), GetMaximumLength(definition));
        }
    }

    void ISkUiGridSplitterOwner.SetLengths(SkUiGridSplitterTracks tracks, SkUiGridLengthChange? first, SkUiGridLengthChange? second)
    {
        if (Container is not { } container)
            return;
        Apply(tracks.FirstIndex, first);
        Apply(tracks.SecondIndex, second);

        void Apply(int at, SkUiGridLengthChange? change)
        {
            if (change is not { } value)
                return;
            var length = value.IsStar ? new GridLength(value.Value, GridUnitType.Star) : new GridLength(value.Value);
            if (tracks.ResizesColumns)
                container.Columns[at].Width = length;
            else
                container.Rows[at].Height = length;
        }
    }

    bool ISkUiGridSplitterOwner.IsRightToLeft => IsRightToLeft;

    double ISkUiGridSplitterOwner.KeyboardIncrement => KeyboardIncrement;
}
