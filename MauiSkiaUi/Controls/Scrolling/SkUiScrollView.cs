using System.Runtime.CompilerServices;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// A single-surface scroller with clamped offsets, wheel input and inertial fling.
/// The scroll offset is a composite-time children translation: scrolling never re-records content, and
/// fling / animated scrolls run on the render thread, so they stay smooth while the UI thread is busy.
/// The render thread reports offsets back each frame to keep <see cref="ScrollX"/> / <see cref="ScrollY"/>,
/// hit-testing, <see cref="Scrolled"/> and native overlays in sync.
/// Drags take part in the gesture arena: content taps win unless the pointer moves past the touch slop along a
/// direction this scroller can move; nested scrollers get the drags of their own axis, and a scroller at its edge
/// hands the drag (and the fling) to the outer scroller on the same axis. Past the outermost edge the content
/// overscrolls (<see cref="Overscroll"/>: bounce or stretch, per look by default) and springs back.
/// Scroll bars (<see cref="VerticalScrollBarVisibility"/>, <see cref="HorizontalScrollBarVisibility"/>) are drawn by the
/// look, placed from the offset on the render thread, and fade out after scrolling stops.
/// </summary>
public class SkUiScrollView : SkUiContentView, ISkUiScrollHost
{
    // Assigned in the constructor; base-constructor property callbacks can run before that.
    private readonly SkUiScrollController _scroller = null!;
    /// <summary>Registered <see cref="SkUiMauiContentView"/> descendants that need offset sync (avoids O(tree) walks).</summary>
    private List<SkUiMauiContentView>? _overlayDescendants;

    /// <summary>Creates a scroller whose motion stops when its surface unloads.</summary>
    public SkUiScrollView()
    {
        _scroller = new SkUiScrollController(this, InvalidateRender, OnOffsetChanged);
        _scroller.MovingChanged += OnMovingChanged;
        _scroller.OverscrollChanged += SyncRegisteredOverlays;
        Unloaded += (_, _) => CancelInteraction();
    }

    SkUiScrollController ISkUiScrollHost.Scroller => _scroller;

    /// <summary>Bindable enabled scroll axes.</summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(ScrollOrientation), typeof(SkUiScrollView), ScrollOrientation.Vertical,
        validateValue: (_, value) => Enum.IsDefined((ScrollOrientation)value),
        propertyChanged: (view, _, value) => ((SkUiScrollView)view).OnOrientationChanged((ScrollOrientation)value));
    /// <summary>Enabled axes; Neither disables scrolling.</summary>
    public ScrollOrientation Orientation { get => (ScrollOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>Current horizontal offset in DIPs (updated from the render thread during fling).</summary>
    public double ScrollX => _scroller.X;
    /// <summary>Current vertical offset in DIPs (updated from the render thread during fling).</summary>
    public double ScrollY => _scroller.Y;
    /// <summary>Measured scrollable content extent, including padding.</summary>
    public Size ContentSize => _scroller.Extent;
    /// <summary>Raised after a clamped offset changes.</summary>
    public event EventHandler<ScrolledEventArgs>? Scrolled;

    /// <summary>
    /// Raised when <see cref="ScrollToAsync(double, double, bool)"/> or <see cref="ScrollToAsync(Element, ScrollToPosition, bool)"/>
    /// requests a scroll, before it starts (MAUI's event, with MAUI's arguments).
    /// </summary>
    public event EventHandler<ScrollToRequestedEventArgs>? ScrollToRequested;

    /// <summary>Bindable property for <see cref="HorizontalScrollBarVisibility"/>.</summary>
    public static readonly BindableProperty HorizontalScrollBarVisibilityProperty = BindableProperty.Create(nameof(HorizontalScrollBarVisibility),
        typeof(ScrollBarVisibility), typeof(SkUiScrollView), ScrollBarVisibility.Default,
        validateValue: (_, value) => Enum.IsDefined((ScrollBarVisibility)value),
        propertyChanged: (view, _, value) => ((SkUiScrollView)view).OnScrollBarVisibilityChanged(horizontal: true, (ScrollBarVisibility)value));

    /// <summary>Bindable property for <see cref="VerticalScrollBarVisibility"/>.</summary>
    public static readonly BindableProperty VerticalScrollBarVisibilityProperty = BindableProperty.Create(nameof(VerticalScrollBarVisibility),
        typeof(ScrollBarVisibility), typeof(SkUiScrollView), ScrollBarVisibility.Default,
        validateValue: (_, value) => Enum.IsDefined((ScrollBarVisibility)value),
        propertyChanged: (view, _, value) => ((SkUiScrollView)view).OnScrollBarVisibilityChanged(horizontal: false, (ScrollBarVisibility)value));

    /// <summary>
    /// The horizontal scroll bar (MAUI's): <see cref="ScrollBarVisibility.Default"/> shows it while scrolling and fades it out
    /// after, <see cref="ScrollBarVisibility.Always"/> keeps it while the content is wider than the viewport,
    /// <see cref="ScrollBarVisibility.Never"/> hides it.
    /// </summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    /// <summary>The vertical scroll bar (MAUI's), on the left in right-to-left layouts; see <see cref="HorizontalScrollBarVisibility"/>.</summary>
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    /// <summary>Bindable property for <see cref="Overscroll"/>.</summary>
    public static readonly BindableProperty OverscrollProperty = BindableProperty.Create(nameof(Overscroll), typeof(SkUiOverscrollMode),
        typeof(SkUiScrollView), SkUiOverscrollMode.Default,
        validateValue: (_, value) => Enum.IsDefined((SkUiOverscrollMode)value),
        propertyChanged: (view, _, value) => ((SkUiScrollView)view).OnOverscrollChanged((SkUiOverscrollMode)value));

    /// <summary>
    /// What a drag or fling past the content's edge does: <see cref="SkUiOverscrollMode.Default"/> follows the look
    /// (<see cref="SkUiLook.DefaultOverscroll"/>; the default look bounces on iOS and Mac Catalyst and stretches on
    /// Android). SkiaUi extension.
    /// </summary>
    public SkUiOverscrollMode Overscroll
    {
        get => (SkUiOverscrollMode)GetValue(OverscrollProperty);
        set => SetValue(OverscrollProperty, value);
    }

    /// <summary>
    /// The vertical scroll bar: a Core node drawn by the look along the viewport edge (style it with
    /// <see cref="Core.SkUiCoreScrollBar.ThumbColor"/> or <see cref="Core.SkUiCoreScrollBar.IsInteractive"/>); shown by
    /// <see cref="VerticalScrollBarVisibility"/>.
    /// </summary>
    public Core.SkUiCoreScrollBar VerticalScrollBar => _scroller.ScrollBar(ScrollOrientation.Vertical);

    /// <summary>The horizontal scroll bar; see <see cref="VerticalScrollBar"/>.</summary>
    public Core.SkUiCoreScrollBar HorizontalScrollBar => _scroller.ScrollBar(ScrollOrientation.Horizontal);

    /// <summary>Bindable property for <see cref="SnapPointsType"/>.</summary>
    public static readonly BindableProperty SnapPointsTypeProperty = BindableProperty.Create(nameof(SnapPointsType), typeof(SnapPointsType),
        typeof(SkUiScrollView), SnapPointsType.None,
        validateValue: (_, value) => Enum.IsDefined((SnapPointsType)value),
        propertyChanged: (view, _, value) => ((SkUiScrollView)view)._scroller.SnapPointsType = (SnapPointsType)value);

    /// <summary>Bindable property for <see cref="SnapPointsAlignment"/>.</summary>
    public static readonly BindableProperty SnapPointsAlignmentProperty = BindableProperty.Create(nameof(SnapPointsAlignment), typeof(SnapPointsAlignment),
        typeof(SkUiScrollView), SnapPointsAlignment.Start,
        validateValue: (_, value) => Enum.IsDefined((SnapPointsAlignment)value),
        propertyChanged: (view, _, value) => ((SkUiScrollView)view)._scroller.SnapPointsAlignment = (SnapPointsAlignment)value);

    /// <summary>
    /// Whether drags, flings and wheel scrolling end with a child of the content lined up with the viewport (MAUI's
    /// CollectionView enum): <see cref="SnapPointsType.Mandatory"/> on the snap point nearest to where the motion would stop,
    /// <see cref="SnapPointsType.MandatorySingle"/> one child per swipe (carousels). SkiaUi extension (MAUI's ScrollView has no
    /// snap points). Programmatic scrolls do not snap.
    /// </summary>
    public SnapPointsType SnapPointsType
    {
        get => (SnapPointsType)GetValue(SnapPointsTypeProperty);
        set => SetValue(SnapPointsTypeProperty, value);
    }

    /// <summary>Which edge (or the center) of a content child lines up with the viewport's at a snap point.</summary>
    public SnapPointsAlignment SnapPointsAlignment
    {
        get => (SnapPointsAlignment)GetValue(SnapPointsAlignmentProperty);
        set => SetValue(SnapPointsAlignmentProperty, value);
    }

    private void OnScrollBarVisibilityChanged(bool horizontal, ScrollBarVisibility value)
    {
        if (horizontal)
            _scroller.HorizontalScrollBarVisibility = value;
        else
            _scroller.VerticalScrollBarVisibility = value;
        // Always reserves a gutter: the content is laid out again.
        InvalidateMeasureOverride();
        _scroller.ArrangeScrollBars(IsRightToLeft);
    }

    private void OnOverscrollChanged(SkUiOverscrollMode value)
    {
        _scroller.ClearOverscroll();
        _scroller.Overscroll = value;
        InvalidateRender(SkUiRenderDirty.Props);
    }

    /// <summary>Where the content shows, in local coordinates: the arranged rectangle minus reserved scroll bar gutters.</summary>
    internal Rect Scrollport => _scroller.Scrollport;

    /// <summary>Offset at which the content is drawn: the scroll offset plus a bounce past the edges (native overlays follow it).</summary>
    internal Point VisualScrollOffset => new(_scroller.ChildrenOffset.X, _scroller.ChildrenOffset.Y);

    /// <summary>True while a render-thread fling or animated scroll is running.</summary>
    internal bool IsMotionRunning => _scroller.IsMotionRunning;

    /// <summary>True while the user drags this scroller or a fling / animated scroll runs.</summary>
    public bool IsScrolling => _scroller.IsMoving;

    private void OnMovingChanged(bool moving)
    {
        if (SkUiDiagnostics.TraceOn)
            SkUiDiagnostics.Write($"scroll {AutomationId ?? GetHashCode().ToString()} moving={moving} dragging={_scroller.Dragging} motion={_scroller.IsMotionRunning} offset={_scroller.X:F0},{_scroller.Y:F0} overlays={_overlayDescendants?.Count ?? 0}");
        if (_overlayDescendants is { Count: > 0 } overlays)
            foreach (var overlay in overlays.ToArray())
                overlay.NotifyAncestorScrollMotion(moving);
        OnPropertyChanged(nameof(IsScrolling));
    }

    /// <summary>Sets orientation (same as the property setter).</summary>
    public SkUiScrollView SetOrientation(ScrollOrientation value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Orientation = value;
        return this;
    }

    private void OnOrientationChanged(ScrollOrientation value)
    {
        CancelInteraction();
        _scroller.ClearOverscroll();
        _scroller.Orientation = value;
        InvalidateMeasureOverride();
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        // Scroll bars that always show reserve gutters beside the content.
        var (left, right, bottom) = _scroller.Gutters(IsRightToLeft);
        var width = Math.Max(0, widthConstraint - left - right);
        var height = Math.Max(0, heightConstraint - bottom);
        // Across the scroll axis an explicitly sized content keeps its size (clipped), as in MAUI.
        if (Content is { } content)
        {
            var inset = ContentInset;
            width = SkUiScrollController.CrossConstraint(width, content.Width, content.MaximumWidth, content.Margin.HorizontalThickness + inset.HorizontalThickness);
            height = SkUiScrollController.CrossConstraint(height, content.Height, content.MaximumHeight, content.Margin.VerticalThickness + inset.VerticalThickness);
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
        // Content stays arranged at its layout origin; the offset is a composite-time children translation.
        Content?.Arrange(new Rect(
            Padding.Left, Padding.Top,
            Math.Max(0, Math.Max(extent.Width, viewport.Width) - Padding.HorizontalThickness),
            Math.Max(0, Math.Max(extent.Height, viewport.Height) - Padding.VerticalThickness)));
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

    /// <summary>RTL horizontal scrollers start at their logical start (the right end) once per content / direction.</summary>
    private bool _rtlStartApplied;

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        _rtlStartApplied = false;
        base.OnEffectiveFlowDirectionChanged();
    }

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
        _scroller?.Gesture.Cancel();
    }

    /// <summary>Clamps and sets an offset without remeasuring or rearranging content.</summary>
    public SkUiScrollView ScrollTo(double horizontalOffset, double verticalOffset)
    {
        _scroller.StopMotion();
        _scroller.ClearOverscroll();
        _scroller.SetOffset(horizontalOffset, verticalOffset);
        return this;
    }

    /// <summary>Starts a render-thread scroll animation; disposing the returned handle cancels it.</summary>
    public IDisposable AnimateScrollTo(double horizontalOffset, double verticalOffset, TimeSpan duration) =>
        _scroller.AnimateTo(horizontalOffset, verticalOffset, duration);

    /// <summary>
    /// Scrolls immediately or animates over 300 ms on the render thread (MAUI's <c>ScrollToAsync</c>; nothing happens when
    /// <see cref="Orientation"/> is <see cref="ScrollOrientation.Neither"/>). Raises <see cref="ScrollToRequested"/>. A
    /// superseding gesture, scroll, or unload cancels the task.
    /// </summary>
    public Task ScrollToAsync(double horizontalOffset, double verticalOffset, bool animated = true)
    {
        if (Orientation == ScrollOrientation.Neither)
            return Task.CompletedTask;
        if (!double.IsFinite(horizontalOffset))
            throw new ArgumentOutOfRangeException(nameof(horizontalOffset));
        if (!double.IsFinite(verticalOffset))
            throw new ArgumentOutOfRangeException(nameof(verticalOffset));
        RaiseScrollToRequested(() => CreatePositionRequest(horizontalOffset, verticalOffset, animated));
        return _scroller.ScrollToAsync(horizontalOffset, verticalOffset, animated);
    }

    /// <summary>
    /// Scrolls so that <paramref name="element"/> (a drawn descendant, a Core node under a <see cref="Core.SkUiCoreHost"/>, or a
    /// MAUI view inside a <see cref="SkUiMauiContentView"/>) is at <paramref name="position"/> of the viewport, as MAUI's
    /// <c>ScrollToAsync(Element, ScrollToPosition, bool)</c>: <see cref="ScrollToPosition.MakeVisible"/> scrolls only when it is
    /// not fully visible. Before the first layout the request waits for it. Raises <see cref="ScrollToRequested"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="element"/> is not inside this scroll view, or <paramref name="position"/> is not defined.</exception>
    public Task ScrollToAsync(Element element, ScrollToPosition position, bool animated) => ScrollToTargetAsync(element, position, animated);

    /// <summary>Scrolls a Core node under a <see cref="Core.SkUiCoreHost"/> inside this view into view; see <see cref="ScrollToAsync(Element, ScrollToPosition, bool)"/>.</summary>
    public Task ScrollToAsync(Core.SkUiCoreNode node, ScrollToPosition position, bool animated) => ScrollToTargetAsync(node, position, animated);

    private Task ScrollToTargetAsync(object target, ScrollToPosition position, bool animated)
    {
        // MAUI's order: a scroller that cannot scroll ignores the request before validating it.
        if (Orientation == ScrollOrientation.Neither)
            return Task.CompletedTask;
        ArgumentNullException.ThrowIfNull(target);
        if (!Enum.IsDefined(position))
            throw new ArgumentException("position is not a valid ScrollToPosition", nameof(position));
        if (SkUiScrollController.GetContentBounds(this, target) is null)
            throw new ArgumentException("The element does not belong to this scroll view.", nameof(target));
        if (target is Element element)
            RaiseScrollToRequested(() => CreateElementRequest(element, position, animated));
        return _scroller.ScrollToTargetAsync(() => GetScrollPosition(target, position), animated);
    }

    /// <summary>
    /// The offset that puts <paramref name="item"/> at <paramref name="pos"/> of the viewport (MAUI's
    /// <c>GetScrollPositionForElement</c>; not clamped, the scroll clamps it). <see cref="ScrollToPosition.MakeVisible"/> returns
    /// the current offset when the item is fully visible.
    /// </summary>
    public Point GetScrollPositionForElement(VisualElement item, ScrollToPosition pos) => GetScrollPosition(item, pos);

    private Point GetScrollPosition(object target, ScrollToPosition position) =>
        SkUiScrollController.GetContentBounds(this, target) is { } bounds
            ? _scroller.GetOffsetFor(bounds, position)
            : new Point(ScrollX, ScrollY);

    /// <summary>
    /// Raises <see cref="ScrollToRequested"/> when someone listens. A MAUI version without the internal constructors
    /// (<see cref="MissingMethodException"/>) skips the event; the scroll still runs.
    /// </summary>
    private void RaiseScrollToRequested(Func<ScrollToRequestedEventArgs> create)
    {
        if (ScrollToRequested is not { } handler)
            return;
        ScrollToRequestedEventArgs args;
        try
        {
            args = create();
        }
        catch (MissingMethodException)
        {
            return;
        }
        handler(this, args);
    }

    // MAUI's ScrollToRequestedEventArgs has internal constructors only; the accessors are trimming- and Native-AOT-safe.
    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern ScrollToRequestedEventArgs CreatePositionRequest(double scrollX, double scrollY, bool shouldAnimate);

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern ScrollToRequestedEventArgs CreateElementRequest(Element element, ScrollToPosition position, bool shouldAnimate);

    private void OnOffsetChanged()
    {
        OnPropertyChanged(nameof(ScrollX));
        OnPropertyChanged(nameof(ScrollY));
        SyncRegisteredOverlays();
        Scrolled?.Invoke(this, new ScrolledEventArgs(ScrollX, ScrollY));
    }

    /// <summary>Registers a hosted overlay so offset updates can sync it without walking the full tree.</summary>
    internal void RegisterOverlayDescendant(SkUiMauiContentView overlay)
    {
        _overlayDescendants ??= [];
        if (_overlayDescendants.Contains(overlay))
            return;
        _overlayDescendants.Add(overlay);
        if (_scroller.IsMoving)
            overlay.NotifyAncestorScrollMotion(true);
    }

    /// <summary>Removes a previously registered overlay descendant.</summary>
    internal void UnregisterOverlayDescendant(SkUiMauiContentView overlay)
    {
        if (_overlayDescendants?.Remove(overlay) == true && _scroller.IsMoving)
            overlay.NotifyAncestorScrollMotion(false);
    }

    /// <summary>Pushes updated root-relative bounds to registered <see cref="SkUiMauiContentView"/> descendants.</summary>
    private void SyncRegisteredOverlays()
    {
        if (_overlayDescendants is null || _overlayDescendants.Count == 0)
            return;
        foreach (var overlay in _overlayDescendants)
            overlay.NotifyAncestorScrollOffsetChanged();
    }

    private void CancelInteraction()
    {
        if (_scroller is null)
            return;
        _scroller.StopMotion();
        _scroller.Gesture.Cancel();
        _scroller.ClearOverscroll();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if ((propertyName == nameof(IsEnabled) && !IsEnabled)
            || (propertyName == nameof(IsVisible) && !IsVisible)
            || (propertyName == nameof(InputTransparent) && InputTransparent)) CancelInteraction();
    }

    /// <inheritdoc />
    protected override void OnContentChanged()
    {
        _scroller.Reset();
        _rtlStartApplied = false;
        base.OnContentChanged();
        InvalidateRender(SkUiRenderDirty.Props);
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        if (Parent is null)
            CancelInteraction();
        base.OnParentSet();
    }
}
