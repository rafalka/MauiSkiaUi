using MauiSkiaUi.Rendering;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core scroller (analogue of <see cref="SkUiScrollView"/>, sharing its engine): clamped offsets applied as a
/// composite-time children translation (scrolling never re-records content), render-thread fling and animated
/// scrolls, wheel input, and the shared scroll gesture — content taps win unless the pointer moves past the touch
/// slop in a direction this scroller can move; nested scrollers (Core or SkUi*) get the drags of their own axis and
/// hand over at their edges. It clips its content to the viewport. Past the outermost edge the content overscrolls
/// (<see cref="Overscroll"/>); look-drawn scroll bars show while scrolling (<see cref="VerticalScrollBarVisibility"/>,
/// <see cref="HorizontalScrollBarVisibility"/>).
/// </summary>
public class SkUiCoreScrollView : SkUiCoreContentView, ISkUiScrollHost
{
    private readonly SkUiScrollController _scroller;
    private bool _rtlStartApplied;

    /// <summary>Creates a vertical scroller.</summary>
    public SkUiCoreScrollView()
    {
        InitClipToBounds(true);
        _scroller = new SkUiScrollController(this, InvalidateRender, OnOffsetChanged);
    }

    /// <summary>The horizontal scroll bar: shown while scrolling (<see cref="ScrollBarVisibility.Default"/>), always, or never.</summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => _scroller.HorizontalScrollBarVisibility;
        set => SetHorizontalScrollBarVisibility(value);
    }

    /// <summary>The vertical scroll bar (on the left in right-to-left layouts); see <see cref="HorizontalScrollBarVisibility"/>.</summary>
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => _scroller.VerticalScrollBarVisibility;
        set => SetVerticalScrollBarVisibility(value);
    }

    /// <summary>What a drag or fling past the content's edge does (<see cref="SkUiOverscrollMode.Default"/>: the look's).</summary>
    public SkUiOverscrollMode Overscroll
    {
        get => _scroller.Overscroll;
        set => SetOverscroll(value);
    }

    /// <summary>The vertical scroll bar (a Core node drawn by the look along the viewport edge); shown by <see cref="VerticalScrollBarVisibility"/>.</summary>
    public SkUiCoreScrollBar VerticalScrollBar => _scroller.ScrollBar(ScrollOrientation.Vertical);

    /// <summary>The horizontal scroll bar; see <see cref="VerticalScrollBar"/>.</summary>
    public SkUiCoreScrollBar HorizontalScrollBar => _scroller.ScrollBar(ScrollOrientation.Horizontal);

    /// <summary>
    /// Whether drags, flings and wheel scrolling end with a child of the content lined up with the viewport (see
    /// <see cref="SkUiScrollView.SnapPointsType"/>).
    /// </summary>
    public SnapPointsType SnapPointsType
    {
        get => _scroller.SnapPointsType;
        set => SetSnapPointsType(value);
    }

    /// <summary>Which edge (or the center) of a content child lines up with the viewport's at a snap point.</summary>
    public SnapPointsAlignment SnapPointsAlignment
    {
        get => _scroller.SnapPointsAlignment;
        set => SetSnapPointsAlignment(value);
    }

    /// <summary>Sets <see cref="SnapPointsType"/>.</summary>
    public SkUiCoreScrollView SetSnapPointsType(SnapPointsType value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (_scroller.SnapPointsType == value) return this;
        _scroller.SnapPointsType = value;
        OnPropertyChanged(nameof(SnapPointsType));
        return this;
    }

    /// <summary>Sets <see cref="SnapPointsAlignment"/>.</summary>
    public SkUiCoreScrollView SetSnapPointsAlignment(SnapPointsAlignment value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (_scroller.SnapPointsAlignment == value) return this;
        _scroller.SnapPointsAlignment = value;
        OnPropertyChanged(nameof(SnapPointsAlignment));
        return this;
    }

    /// <summary>Sets <see cref="HorizontalScrollBarVisibility"/>.</summary>
    public SkUiCoreScrollView SetHorizontalScrollBarVisibility(ScrollBarVisibility value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (_scroller.HorizontalScrollBarVisibility == value) return this;
        _scroller.HorizontalScrollBarVisibility = value;
        // Always reserves a gutter: the content is laid out again.
        InvalidateMeasure();
        _scroller.ArrangeScrollBars(IsRightToLeft);
        OnPropertyChanged(nameof(HorizontalScrollBarVisibility));
        return this;
    }

    /// <summary>Sets <see cref="VerticalScrollBarVisibility"/>.</summary>
    public SkUiCoreScrollView SetVerticalScrollBarVisibility(ScrollBarVisibility value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (_scroller.VerticalScrollBarVisibility == value) return this;
        _scroller.VerticalScrollBarVisibility = value;
        InvalidateMeasure();
        _scroller.ArrangeScrollBars(IsRightToLeft);
        OnPropertyChanged(nameof(VerticalScrollBarVisibility));
        return this;
    }

    /// <summary>Sets <see cref="Overscroll"/>.</summary>
    public SkUiCoreScrollView SetOverscroll(SkUiOverscrollMode value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (_scroller.Overscroll == value) return this;
        _scroller.ClearOverscroll();
        _scroller.Overscroll = value;
        InvalidateRender(SkUiRenderDirty.Props);
        OnPropertyChanged(nameof(Overscroll));
        return this;
    }

    SkUiScrollController ISkUiScrollHost.Scroller => _scroller;

    /// <summary>Enabled axes; Neither disables scrolling.</summary>
    public ScrollOrientation Orientation
    {
        get => _scroller.Orientation;
        set => SetOrientation(value);
    }

    /// <summary>Current horizontal offset (DIPs).</summary>
    public double ScrollX => _scroller.X;

    /// <summary>Current vertical offset (DIPs).</summary>
    public double ScrollY => _scroller.Y;

    /// <summary>Measured content extent including padding.</summary>
    public Size ContentSize => _scroller.Extent;

    /// <summary>Viewport size (DIPs).</summary>
    public Size ViewportSize => _scroller.Viewport;

    /// <summary>True while a render-thread fling or animated scroll runs.</summary>
    public bool IsScrollAnimating => _scroller.IsMotionRunning;

    /// <summary>Raised after the offset changes (also during render-thread motion).</summary>
    public event EventHandler<ScrolledEventArgs>? Scrolled;

    /// <summary>Sets the enabled axes.</summary>
    public SkUiCoreScrollView SetOrientation(ScrollOrientation value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (_scroller.Orientation == value) return this;
        _scroller.StopMotion();
        _scroller.Gesture.Cancel();
        _scroller.ClearOverscroll();
        _scroller.Orientation = value;
        OnPropertyChanged(nameof(Orientation));
        InvalidateMeasure();
        return this;
    }

    /// <summary>Clamps and sets the offset (no remeasure / rearrange).</summary>
    public SkUiCoreScrollView ScrollTo(double horizontalOffset, double verticalOffset)
    {
        _scroller.StopMotion();
        _scroller.ClearOverscroll();
        _scroller.SetOffset(horizontalOffset, verticalOffset);
        return this;
    }

    /// <summary>Starts a render-thread scroll animation; disposing the handle cancels it.</summary>
    public IDisposable AnimateScrollTo(double horizontalOffset, double verticalOffset, TimeSpan duration) =>
        _scroller.AnimateTo(horizontalOffset, verticalOffset, duration);

    /// <summary>Scrolls immediately or animates over 300 ms on the render thread; a newer scroll or gesture cancels the task.</summary>
    public Task ScrollToAsync(double horizontalOffset, double verticalOffset, bool animated = true) =>
        Orientation == ScrollOrientation.Neither ? Task.CompletedTask : _scroller.ScrollToAsync(horizontalOffset, verticalOffset, animated);

    /// <summary>
    /// Scrolls so that <paramref name="node"/>, a descendant, is at <paramref name="position"/> of the viewport (as
    /// <see cref="SkUiScrollView.ScrollToAsync(Element, ScrollToPosition, bool)"/>); before the first layout the request
    /// waits for it.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="node"/> is not inside this scroll view, or <paramref name="position"/> is not defined.</exception>
    public Task ScrollToAsync(SkUiCoreNode node, ScrollToPosition position, bool animated = true)
    {
        if (Orientation == ScrollOrientation.Neither)
            return Task.CompletedTask;
        ArgumentNullException.ThrowIfNull(node);
        if (!Enum.IsDefined(position))
            throw new ArgumentException("position is not a valid ScrollToPosition", nameof(position));
        if (SkUiScrollController.GetContentBounds(this, node) is null)
            throw new ArgumentException("The node does not belong to this scroll view.", nameof(node));
        return _scroller.ScrollToTargetAsync(() => GetScrollPositionForNode(node, position), animated);
    }

    /// <summary>The offset that puts <paramref name="node"/> at <paramref name="position"/> of the viewport (not clamped).</summary>
    public Point GetScrollPositionForNode(SkUiCoreNode node, ScrollToPosition position) =>
        SkUiScrollController.GetContentBounds(this, node) is { } bounds
            ? _scroller.GetOffsetFor(bounds, position)
            : new Point(ScrollX, ScrollY);

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        // Scroll bars that always show reserve gutters beside the content.
        var (left, right, bottom) = _scroller.Gutters(IsRightToLeft);
        var width = Math.Max(0, widthConstraint - left - right);
        var height = Math.Max(0, heightConstraint - bottom);
        // Across the scroll axis an explicitly sized content keeps its size (clipped), as SkUiScrollView.
        if (Content is { } content)
        {
            width = SkUiScrollController.CrossConstraint(width, content.Width, content.MaximumWidth, content.Margin.HorizontalThickness + Padding.HorizontalThickness);
            height = SkUiScrollController.CrossConstraint(height, content.Height, content.MaximumHeight, content.Margin.VerticalThickness + Padding.VerticalThickness);
        }
        var extent = base.MeasureContent(_scroller.Horizontal ? double.PositiveInfinity : width, _scroller.Vertical ? double.PositiveInfinity : height);
        _scroller.Extent = extent;
        return new Size(Math.Min(widthConstraint, extent.Width + left + right), Math.Min(heightConstraint, extent.Height + bottom));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        _scroller.Layout(size, IsRightToLeft);
        var viewport = _scroller.Viewport;
        var extent = _scroller.Extent;
        // Content keeps its layout origin; the offset is a composite-time children translation.
        base.ArrangeContent(new Size(Math.Max(extent.Width, viewport.Width), Math.Max(extent.Height, viewport.Height)));
        InvalidateRender(SkUiRenderDirty.Props);
        if (!_rtlStartApplied && IsRightToLeft && _scroller.Horizontal && _scroller.MaxX > 0)
        {
            _rtlStartApplied = true;
            _scroller.SetOffset(_scroller.MaxX, _scroller.Y);
        }
        else
        {
            _scroller.Clamp();
        }
        _scroller.ArrangeScrollBars(IsRightToLeft);
        _scroller.OnArranged();
    }

    /// <inheritdoc />
    /// <remarks>Includes a gutter on the left (right-to-left), so mirrored content lands right of it.</remarks>
    internal override double ChildrenSpaceWidth => Math.Max(_scroller.Extent.Width, _scroller.Viewport.Width) + _scroller.ScrollportLeft;

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props) => _scroller.FillRenderProps(ref props);

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        base.AddRenderChildren(children);
        _scroller?.AddScrollBars(children);
    }

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        base.CollectGestureRecognizers(recognizers);
        if (_scroller.Orientation != ScrollOrientation.Neither)
            recognizers.Add(_scroller.Gesture);
    }

    /// <inheritdoc />
    internal override void CancelGestures()
    {
        base.CancelGestures();
        _scroller?.StopMotion();
        _scroller?.Gesture.Cancel();
        _scroller?.ClearOverscroll();
    }

    /// <inheritdoc />
    protected override void OnContentChanged()
    {
        _scroller.Reset();
        _rtlStartApplied = false;
        base.OnContentChanged();
        InvalidateRender(SkUiRenderDirty.Props);
    }

    private void OnOffsetChanged()
    {
        OnPropertyChanged(nameof(ScrollX));
        OnPropertyChanged(nameof(ScrollY));
        Scrolled?.Invoke(this, new ScrolledEventArgs(ScrollX, ScrollY));
    }
}
