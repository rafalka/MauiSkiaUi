namespace MauiSkiaUi.Core;

/// <summary>
/// A <see cref="SkUiCoreDragger"/> that resizes the rows (or columns) of the <see cref="SkUiCoreGrid"/> it is in (Core
/// analogue of <see cref="SkUiGridSplitter"/>), also a <see cref="SkUiCoreTable"/>: dragging it gives one track the pointer's
/// travel and takes it from the other. Which tracks is <see cref="ResizeBehavior"/>: by default, the ones around a splitter in
/// a row (or column) of its own, or, for a splitter aligned to the end of a cell (a table header's column boundary), its own
/// track and the next one. Two star tracks keep sharing the grid in the new proportion; next to a star track the other one
/// becomes absolute; Auto and absolute tracks become absolute. Drags respect the definitions' <c>MinHeight</c> /
/// <c>MaxHeight</c> (<c>MinWidth</c> / <c>MaxWidth</c>). <see cref="SkUiCoreDragger.DragAxis"/> does not apply: the splitter
/// follows its resize direction.
/// </summary>
public class SkUiCoreGridSplitter : SkUiCoreDragger, ISkUiGridSplitterOwner
{
    private SkUiGridResizeDirection _resizeDirection;
    private SkUiGridResizeBehavior _resizeBehavior;
    private SkUiGridSplitterEngine? _engine;

    /// <summary>Rows, columns, or (default) chosen from the splitter's alignment and place in the grid.</summary>
    public SkUiGridResizeDirection ResizeDirection { get => _resizeDirection; set => SetResizeDirection(value); }

    /// <summary>Which tracks the splitter resizes (default <see cref="SkUiGridResizeBehavior.BasedOnAlignment"/>).</summary>
    public SkUiGridResizeBehavior ResizeBehavior { get => _resizeBehavior; set => SetResizeBehavior(value); }

    /// <summary>Sets the resize direction.</summary>
    public SkUiCoreGridSplitter SetResizeDirection(SkUiGridResizeDirection value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), value, null);
        if (SetProperty(ref _resizeDirection, value, nameof(ResizeDirection)))
            InvalidateMeasure();
        return this;
    }

    /// <summary>Sets the resize behavior.</summary>
    public SkUiCoreGridSplitter SetResizeBehavior(SkUiGridResizeBehavior value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), value, null);
        SetProperty(ref _resizeBehavior, value, nameof(ResizeBehavior));
        return this;
    }

    private SkUiGridSplitterEngine Engine => _engine ??= new SkUiGridSplitterEngine(this);

    internal bool ResizesColumns
    {
        get
        {
            if (Parent is not SkUiCoreGrid grid)
                return SkUiGridSplitterMath.ResizesColumns(_resizeDirection, HorizontalAlignment, VerticalAlignment, 1, 1, 1, 1, Frame.Size);
            var placement = grid.GetPlacement(this);
            return SkUiGridSplitterMath.ResizesColumns(_resizeDirection, HorizontalAlignment, VerticalAlignment, Math.Max(1, grid.RowDefinitions.Count),
                Math.Max(1, grid.ColumnDefinitions.Count), placement.RowSpan, placement.ColumnSpan, Frame.Size);
        }
    }

    /// <inheritdoc />
    protected override SkUiPanAxis EffectiveDragAxis => ResizesColumns ? SkUiPanAxis.Horizontal : SkUiPanAxis.Vertical;

    /// <inheritdoc />
    protected override bool CanDrag => ((ISkUiGridSplitterOwner)this).GetTracks() is not null;

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

    SkUiGridSplitterTracks? ISkUiGridSplitterOwner.GetTracks()
    {
        if (Parent is not SkUiCoreGrid grid)
            return null;
        var columns = ResizesColumns;
        var placement = grid.GetPlacement(this);
        var indices = SkUiGridSplitterMath.Indices(_resizeBehavior, columns ? HorizontalAlignment : VerticalAlignment,
            columns ? placement.Column : placement.Row, columns ? placement.ColumnSpan : placement.RowSpan);
        if (!SkUiGridSplitterMath.AreTracks(indices, columns ? grid.ColumnDefinitions.Count : grid.RowDefinitions.Count))
            return null;
        return new SkUiGridSplitterTracks(columns, indices.First, Track(indices.First), indices.Second, indices.Second < 0 ? null : Track(indices.Second));

        SkUiGridTrack Track(int at)
        {
            if (columns)
            {
                var column = grid.ColumnDefinitions[at];
                return new SkUiGridTrack(Kind(column.Width), column.Width.Value, grid.GetColumnWidth(at), column.MinWidth, column.MaxWidth);
            }
            var row = grid.RowDefinitions[at];
            return new SkUiGridTrack(Kind(row.Height), row.Height.Value, grid.GetRowHeight(at), row.MinHeight, row.MaxHeight);
        }
    }

    private static SkUiGridTrackKind Kind(SkUiCoreGridLength length) =>
        length.IsStar ? SkUiGridTrackKind.Star : length.IsAuto ? SkUiGridTrackKind.Auto : SkUiGridTrackKind.Absolute;

    void ISkUiGridSplitterOwner.SetLengths(SkUiGridSplitterTracks tracks, SkUiGridLengthChange? first, SkUiGridLengthChange? second)
    {
        if (Parent is not SkUiCoreGrid grid)
            return;
        Apply(tracks.FirstIndex, first);
        Apply(tracks.SecondIndex, second);

        void Apply(int at, SkUiGridLengthChange? change)
        {
            if (change is not { } value)
                return;
            var length = value.IsStar ? new SkUiCoreGridLength(value.Value, SkUiCoreGridUnitType.Star) : new SkUiCoreGridLength(value.Value);
            if (tracks.ResizesColumns)
                grid.ColumnDefinitions[at].SetWidth(length);
            else
                grid.RowDefinitions[at].SetHeight(length);
        }
    }

    bool ISkUiGridSplitterOwner.IsRightToLeft => IsRightToLeft;

    double ISkUiGridSplitterOwner.KeyboardIncrement => KeyboardIncrement;
}
