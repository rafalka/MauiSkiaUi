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
/// hands the drag (and the fling) to the outer scroller on the same axis.
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
        _scroller.Orientation = value;
        InvalidateMeasureOverride();
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var extent = base.MeasureContent(_scroller.Horizontal ? double.PositiveInfinity : widthConstraint, _scroller.Vertical ? double.PositiveInfinity : heightConstraint);
        _scroller.Extent = extent;
        return new Size(Math.Min(widthConstraint, extent.Width), Math.Min(heightConstraint, extent.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        _scroller.Viewport = size;
        var extent = _scroller.Extent;
        // Content stays arranged at its layout origin; the offset is a composite-time children translation.
        Content?.Arrange(new Rect(
            Padding.Left, Padding.Top,
            Math.Max(0, Math.Max(extent.Width, size.Width) - Padding.HorizontalThickness),
            Math.Max(0, Math.Max(extent.Height, size.Height) - Padding.VerticalThickness)));
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

    /// <summary>RTL horizontal scrollers start at their logical start (the right end) once per content / direction.</summary>
    private bool _rtlStartApplied;

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        _rtlStartApplied = false;
        base.OnEffectiveFlowDirectionChanged();
    }

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
        _scroller?.Gesture.Cancel();
    }

    /// <summary>Clamps and sets an offset without remeasuring or rearranging content.</summary>
    public SkUiScrollView ScrollTo(double horizontalOffset, double verticalOffset)
    {
        _scroller.StopMotion();
        _scroller.SetOffset(horizontalOffset, verticalOffset);
        return this;
    }

    /// <summary>Starts a render-thread scroll animation; disposing the returned handle cancels it.</summary>
    public IDisposable AnimateScrollTo(double horizontalOffset, double verticalOffset, TimeSpan duration) =>
        _scroller.AnimateTo(horizontalOffset, verticalOffset, duration);

    /// <summary>Scrolls immediately or animates over 300 ms on the render thread. A superseding gesture, scroll, or unload cancels the task.</summary>
    public Task ScrollToAsync(double horizontalOffset, double verticalOffset, bool animated = true) =>
        _scroller.ScrollToAsync(horizontalOffset, verticalOffset, animated);

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
