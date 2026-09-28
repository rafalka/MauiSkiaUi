using MauiSkiaUi.Rendering;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core scroller (analogue of <see cref="SkUiScrollView"/>, sharing its engine): clamped offsets applied as a
/// composite-time children translation (scrolling never re-records content), render-thread fling and animated
/// scrolls, wheel input, and the shared scroll gesture — content taps win unless the pointer moves past the touch
/// slop in a direction this scroller can move; nested scrollers (Core or SkUi*) get the drags of their own axis and
/// hand over at their edges. It clips its content to the viewport.
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
        _scroller.Orientation = value;
        OnPropertyChanged(nameof(Orientation));
        InvalidateMeasure();
        return this;
    }

    /// <summary>Clamps and sets the offset (no remeasure / rearrange).</summary>
    public SkUiCoreScrollView ScrollTo(double horizontalOffset, double verticalOffset)
    {
        _scroller.StopMotion();
        _scroller.SetOffset(horizontalOffset, verticalOffset);
        return this;
    }

    /// <summary>Starts a render-thread scroll animation; disposing the handle cancels it.</summary>
    public IDisposable AnimateScrollTo(double horizontalOffset, double verticalOffset, TimeSpan duration) =>
        _scroller.AnimateTo(horizontalOffset, verticalOffset, duration);

    /// <summary>Scrolls immediately or animates over 300 ms on the render thread; a newer scroll or gesture cancels the task.</summary>
    public Task ScrollToAsync(double horizontalOffset, double verticalOffset, bool animated = true) =>
        _scroller.ScrollToAsync(horizontalOffset, verticalOffset, animated);

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var extent = base.MeasureContent(
            _scroller.Horizontal ? double.PositiveInfinity : widthConstraint,
            _scroller.Vertical ? double.PositiveInfinity : heightConstraint);
        _scroller.Extent = extent;
        return new Size(Math.Min(widthConstraint, extent.Width), Math.Min(heightConstraint, extent.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        _scroller.Viewport = size;
        var extent = _scroller.Extent;
        // Content keeps its layout origin; the offset is a composite-time children translation.
        base.ArrangeContent(new Size(Math.Max(extent.Width, size.Width), Math.Max(extent.Height, size.Height)));
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
    }

    /// <inheritdoc />
    internal override double ChildrenSpaceWidth => Math.Max(_scroller.Extent.Width, _scroller.Viewport.Width);

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        props.ChildrenOffsetX = (float)_scroller.X;
        props.ChildrenOffsetY = (float)_scroller.Y;
        props.ChildrenClipRect = new SkiaSharp.SKRect(0, 0, (float)_scroller.Viewport.Width, (float)_scroller.Viewport.Height);
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
