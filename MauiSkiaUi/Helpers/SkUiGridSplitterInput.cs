namespace MauiSkiaUi;

/// <summary>How a grid track is sized, as a splitter sees it (MAUI's and Core's grid lengths map to it).</summary>
internal enum SkUiGridTrackKind
{
    Absolute,
    Auto,
    Star
}

/// <summary>A track a splitter resizes.</summary>
/// <param name="Kind">How its definition sizes it.</param>
/// <param name="Value">Its definition's length: DIPs for an absolute track, the weight of a star track.</param>
/// <param name="Actual">Its arranged size.</param>
/// <param name="Minimum">The smallest size a drag leaves it.</param>
/// <param name="Maximum">The largest size a drag gives it.</param>
internal readonly record struct SkUiGridTrack(SkUiGridTrackKind Kind, double Value, double Actual, double Minimum, double Maximum);

/// <summary>A track's new definition: <paramref name="Value"/> DIPs, or a star weight when <paramref name="IsStar"/>.</summary>
internal readonly record struct SkUiGridLengthChange(double Value, bool IsStar);

/// <summary>
/// The tracks a splitter resizes: <paramref name="First"/> takes the drag's travel, <paramref name="Second"/> (none for
/// <see cref="SkUiGridResizeBehavior.CurrentOnly"/>) gives it up.
/// </summary>
internal readonly record struct SkUiGridSplitterTracks(bool ResizesColumns, int FirstIndex, SkUiGridTrack First, int SecondIndex, SkUiGridTrack? Second);

/// <summary>Track math shared by <see cref="SkUiGridSplitter"/> and <see cref="Core.SkUiCoreGridSplitter"/>.</summary>
internal static class SkUiGridSplitterMath
{
    // Two star tracks keep a positive weight each (a Core star length must be positive), whatever the drag.
    private const double MinimumStarShare = 1e-4;

    /// <summary>
    /// The direction of a splitter: with <see cref="SkUiGridResizeDirection.Auto"/>, columns when it is aligned horizontally
    /// and fills vertically (rows the other way round), then rows when it spans every column of a grid with several rows,
    /// columns when it spans every row of a grid with several columns, otherwise by its shape (taller than wide: columns).
    /// </summary>
    public static bool ResizesColumns(SkUiGridResizeDirection direction, LayoutAlignment horizontal, LayoutAlignment vertical, int rows, int columns,
        int rowSpan, int columnSpan, Size size) => direction switch
    {
        SkUiGridResizeDirection.Columns => true,
        SkUiGridResizeDirection.Rows => false,
        _ when horizontal != LayoutAlignment.Fill && vertical == LayoutAlignment.Fill => true,
        _ when vertical != LayoutAlignment.Fill && horizontal == LayoutAlignment.Fill => false,
        _ when columns > 1 && (rows == 1 || rowSpan >= rows) => true,
        _ when rows > 1 && (columns == 1 || columnSpan >= columns) => false,
        _ => size.Width < size.Height
    };

    /// <summary>
    /// The tracks a splitter in tracks <paramref name="index"/> … <paramref name="index"/> + <paramref name="span"/> - 1
    /// resizes (second -1: none); <paramref name="alignment"/> is its alignment along the resize direction.
    /// </summary>
    public static (int First, int Second) Indices(SkUiGridResizeBehavior behavior, LayoutAlignment alignment, int index, int span)
    {
        if (behavior == SkUiGridResizeBehavior.BasedOnAlignment)
            behavior = alignment switch
            {
                LayoutAlignment.End => SkUiGridResizeBehavior.CurrentAndNext,
                LayoutAlignment.Start => SkUiGridResizeBehavior.PreviousAndCurrent,
                _ => SkUiGridResizeBehavior.PreviousAndNext
            };
        var last = index + span - 1;
        return behavior switch
        {
            SkUiGridResizeBehavior.CurrentAndNext => (last, last + 1),
            SkUiGridResizeBehavior.PreviousAndCurrent => (index - 1, index),
            SkUiGridResizeBehavior.CurrentOnly => (last, -1),
            _ => (index - 1, last + 1)
        };
    }

    /// <summary>Whether both indices name explicit definitions (an index past the last one is placed in the last one).</summary>
    public static bool AreTracks((int First, int Second) indices, int count) =>
        indices.First >= 0 && indices.First < count && (indices.Second == -1 || indices.Second < count) && indices.First != indices.Second;

    /// <summary>
    /// The size the first track can take for <paramref name="size"/>: within both tracks' limits, the two together keeping
    /// their size (the first one alone within its own limits). Conflicting limits leave the first track's minimum.
    /// </summary>
    public static double ClampFirst(in SkUiGridTrack first, in SkUiGridTrack? second, double size)
    {
        var (low, high) = FirstRange(first, second);
        return Math.Clamp(size, low, high);
    }

    /// <summary>The smallest and largest size the first track can take (the largest may be infinite with no second track).</summary>
    public static (double Low, double High) FirstRange(in SkUiGridTrack first, in SkUiGridTrack? second)
    {
        var low = Math.Max(0, first.Minimum);
        var high = first.Maximum;
        if (second is { } next)
        {
            var total = first.Actual + next.Actual;
            low = Math.Max(low, total - next.Maximum);
            high = Math.Min(Math.Min(total, high), total - next.Minimum);
        }
        return (low, Math.Max(low, high));
    }

    /// <summary>
    /// New definitions that give the first track <paramref name="size"/> (clamped) and the second one the rest of their space.
    /// Two star tracks share their weights in the new proportion (they keep sharing a resized grid); next to a star track only
    /// the other one changes, to absolute DIPs (the star track takes what is left); Auto and absolute tracks become absolute;
    /// a track resized alone becomes absolute. <c>null</c>: that track's definition stays.
    /// </summary>
    public static (SkUiGridLengthChange? First, SkUiGridLengthChange? Second) Resize(in SkUiGridTrack first, in SkUiGridTrack? second, double size)
    {
        size = ClampFirst(first, second, size);
        if (second is not { } next)
            return (new SkUiGridLengthChange(size, false), null);
        var total = first.Actual + next.Actual;
        if (first.Kind == SkUiGridTrackKind.Star && next.Kind == SkUiGridTrackKind.Star)
        {
            if (total <= 0)
                return default;
            var weight = first.Value + next.Value;
            var share = Math.Clamp(weight * size / total, weight * MinimumStarShare, weight * (1 - MinimumStarShare));
            return (new(share, true), new(weight - share, true));
        }
        return (first.Kind == SkUiGridTrackKind.Star ? null : new SkUiGridLengthChange(size, false),
            next.Kind == SkUiGridTrackKind.Star ? null : new SkUiGridLengthChange(total - size, false));
    }

    /// <summary>
    /// Arranged sizes of a MAUI grid's rows or columns (<see cref="Grid"/>, <see cref="SkUiGrid"/>) as its layout manager
    /// sizes them in a grid of fixed size: absolute tracks at their length, Auto tracks at their largest child in that track
    /// alone, star tracks sharing the rest by weight. MAUI does not expose what it arranged; a track sized by children
    /// spanning several tracks, or star tracks in a grid that sizes to its content, may come out a little different (a drag
    /// then starts with a small jump).
    /// </summary>
    public static double[] TrackSizes(IGridLayout grid, bool columns)
    {
        var count = Math.Max(1, columns ? grid.ColumnDefinitions.Count : grid.RowDefinitions.Count);
        var sizes = new double[count];
        var stars = 0.0;
        for (var index = 0; index < count; index++)
        {
            var length = Length(grid, columns, index);
            if (length.IsAbsolute)
                sizes[index] = length.Value;
            else if (length.IsStar)
                stars += length.Value;
        }
        foreach (var view in grid)
        {
            if (view.Visibility == Visibility.Collapsed || (columns ? grid.GetColumnSpan(view) : grid.GetRowSpan(view)) != 1)
                continue;
            var index = Math.Min(columns ? grid.GetColumn(view) : grid.GetRow(view), count - 1);
            if (Length(grid, columns, index).IsAuto)
                sizes[index] = Math.Max(sizes[index], columns ? view.DesiredSize.Width : view.DesiredSize.Height);
        }
        var padding = grid.Padding;
        var rest = columns ? grid.Frame.Width - padding.HorizontalThickness : grid.Frame.Height - padding.VerticalThickness;
        rest -= (count - 1) * (columns ? grid.ColumnSpacing : grid.RowSpacing);
        for (var index = 0; index < count; index++)
            if (!Length(grid, columns, index).IsStar)
                rest -= sizes[index];
        for (var index = 0; index < count; index++)
            if (Length(grid, columns, index) is { IsStar: true } star)
                sizes[index] = stars > 0 ? Math.Max(0, rest) * star.Value / stars : 0;
        return sizes;
    }

    private static GridLength Length(IGridLayout grid, bool columns, int index)
    {
        if (columns)
            return index < grid.ColumnDefinitions.Count ? grid.ColumnDefinitions[index].Width : GridLength.Star;
        return index < grid.RowDefinitions.Count ? grid.RowDefinitions[index].Height : GridLength.Star;
    }

    /// <summary>The kind of a MAUI grid length.</summary>
    public static SkUiGridTrackKind Kind(GridLength length) =>
        length.IsStar ? SkUiGridTrackKind.Star : length.IsAuto ? SkUiGridTrackKind.Auto : SkUiGridTrackKind.Absolute;
}

/// <summary>What a splitter's layer provides to <see cref="SkUiGridSplitterEngine"/>.</summary>
internal interface ISkUiGridSplitterOwner
{
    /// <summary>The tracks the splitter resizes; <c>null</c> when it is not in a grid or they have no definitions.</summary>
    SkUiGridSplitterTracks? GetTracks();

    /// <summary>Writes the tracks' new definitions (<c>null</c>: unchanged).</summary>
    void SetLengths(SkUiGridSplitterTracks tracks, SkUiGridLengthChange? first, SkUiGridLengthChange? second);

    /// <summary>The splitter is laid out right to left (columns run from the right).</summary>
    bool IsRightToLeft { get; }

    /// <summary>DIPs an assistive increment moves the splitter.</summary>
    double KeyboardIncrement { get; }
}

/// <summary>
/// What a grid splitter does with its dragger's drags, shared by both layers: on start it takes the tracks' sizes, on each
/// delta it gives the first track its start size plus the travel along the resize direction (mirrored for right-to-left
/// columns). Assistive technologies see a slider over the first track's size.
/// </summary>
internal sealed class SkUiGridSplitterEngine(ISkUiGridSplitterOwner owner)
{
    // A track with no maximum reads, for assistive technologies, as a range up to this many times its size.
    private const double UnboundedRangeFactor = 2;

    private SkUiGridSplitterTracks? _dragged;

    public void Started() => _dragged = owner.GetTracks();

    public void Delta(SkUiDragEventArgs args)
    {
        if (_dragged is not { } tracks)
            return;
        var travel = tracks.ResizesColumns ? args.TotalX : args.TotalY;
        // Right to left, the first column is on the right of the boundary.
        if (tracks.ResizesColumns && owner.IsRightToLeft)
            travel = -travel;
        ResizeTo(tracks, tracks.First.Actual + travel);
    }

    public void Completed() => _dragged = null;

    private bool ResizeTo(SkUiGridSplitterTracks tracks, double size)
    {
        var (first, second) = SkUiGridSplitterMath.Resize(tracks.First, tracks.Second, size);
        if (first is null && second is null)
            return false;
        owner.SetLengths(tracks, first, second);
        return true;
    }

    /// <summary>A slider over the size of the first track.</summary>
    public void PopulateSemantics(SkUiSemanticsInfo info, bool resizesColumns)
    {
        info.Role = SkUiSemanticsRole.Slider;
        info.IsHorizontal = resizesColumns;
        if (owner.GetTracks() is not { } tracks)
            return;
        var (low, high) = Range(tracks);
        info.Range = new SkUiSemanticsRange(low, high, Math.Clamp(tracks.First.Actual, low, high));
        if (high > low)
            info.Actions |= SkUiSemanticsActions.Increment | SkUiSemanticsActions.Decrement;
    }

    private static (double Low, double High) Range(SkUiGridSplitterTracks tracks)
    {
        var (low, high) = SkUiGridSplitterMath.FirstRange(tracks.First, tracks.Second);
        return (low, double.IsInfinity(high) ? Math.Max(low, tracks.First.Actual * UnboundedRangeFactor) : high);
    }

    /// <summary>Increment / decrement: the first track grows / shrinks by one keyboard increment.</summary>
    public bool? OnSemanticsAction(SkUiSemanticsActions action) => action switch
    {
        SkUiSemanticsActions.Increment => SetSemanticsValue(Current() + owner.KeyboardIncrement),
        SkUiSemanticsActions.Decrement => SetSemanticsValue(Current() - owner.KeyboardIncrement),
        _ => null
    };

    private double Current() => owner.GetTracks()?.First.Actual ?? 0;

    /// <summary>An assistive technology sets the size of the first track.</summary>
    public bool SetSemanticsValue(double value)
    {
        if (owner.GetTracks() is not { } tracks)
            return false;
        var (low, high) = Range(tracks);
        var size = Math.Clamp(value, low, high);
        // Exact: no step left in that direction.
        return size != tracks.First.Actual && ResizeTo(tracks, size);
    }
}
