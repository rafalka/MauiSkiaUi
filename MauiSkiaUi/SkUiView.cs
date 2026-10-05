using Microsoft.Maui.Layouts;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>Base for Skia-drawn views, with layout that does not require a handler.</summary>
public partial class SkUiView : View, ISkUiView, ISkUiRenderable, ISkUiGestureElement, ISkUiTransitionHost, ISkUiShadowCaster
{
    private bool _measureDirty = true;
    private bool _arrangeDirty = true;
    private Rect _lastArrangeBounds;
    private Size _lastConstraint;
    private Size _measuredSize;
    private bool _hwAccelerated;
    private int _updateDepth;
    private bool _paintPending;
    private bool _layoutPending;
    private bool _ownLayoutPending; // this node's own measure changed (not only a descendant's)
    private bool _laidOut;          // measured and arranged at least once
    private bool _relayoutPending;  // surface roots: a descendant's measure changed, relaid out before the next frame
    private SkUiRenderDirty _renderPending;
    private SkUiRenderState? _renderState;
#if SKUI_DIAGNOSTICS
    private int _diagnosticRecordFrameCount;
    private double _diagnosticRecordFrameTotalMs;
#endif
    private SkUiGestureSet? _gestures;
    private SkUiPointerRouter? _router;
    private SkUiTapGestureRecognizer? _tap;
    private bool _mauiGesturesReported;
    private SkUiAnimationClock? _animationClock;
    private ICommand? _tappedCommand;
    private object? _tappedCommandParameter;

    /// <summary>Bindable opt-in tap command.</summary>
    public static readonly BindableProperty TappedCommandProperty = BindableProperty.Create(
        nameof(TappedCommand), typeof(ICommand), typeof(SkUiView), null,
        propertyChanged: (view, _, value) => ((SkUiView)view).OnTappedCommandChanged((ICommand?)value));
    /// <summary>Bindable tap command parameter.</summary>
    public static readonly BindableProperty TappedCommandParameterProperty = BindableProperty.Create(
        nameof(TappedCommandParameter), typeof(object), typeof(SkUiView), null,
        propertyChanged: (view, _, value) => ((SkUiView)view).OnTappedCommandParameterChanged(value));
    /// <summary>Optional command; passive nodes participate when it can execute.</summary>
    public ICommand? TappedCommand { get => (ICommand?)GetValue(TappedCommandProperty); set => SetValue(TappedCommandProperty, value); }
    /// <summary>Parameter supplied to the tap command.</summary>
    public object? TappedCommandParameter { get => GetValue(TappedCommandParameterProperty); set => SetValue(TappedCommandParameterProperty, value); }
    /// <summary>Sets the tap command (same as the property setter).</summary>
    public SkUiView SetTappedCommand(ICommand? value) { TappedCommand = value; return this; }
    private void OnTappedCommandChanged(ICommand? value) { _tappedCommand = value; }
    /// <summary>Sets the tap parameter (same as the property setter).</summary>
    public SkUiView SetTappedCommandParameter(object? value) { TappedCommandParameter = value; return this; }
    private void OnTappedCommandParameterChanged(object? value) { _tappedCommandParameter = value; }
    /// <summary>
    /// Any object the app wants to keep with this view (an id, a model, a cache). SkiaUi never reads or changes it. Not a
    /// bindable property: it raises no change notification and takes no part in layout, drawing or input.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>Sets <see cref="Tag"/> (same as the property setter).</summary>
    public SkUiView SetTag(object? value)
    {
        Tag = value;
        return this;
    }

    /// <summary>Whether an eligible captured pointer is currently pressed inside this node.</summary>
    public bool IsPressed { get; private set; }

    /// <summary>
    /// Whether a hovering pointer (mouse, trackpad, pen or iPad pointer) is over this view or one of its descendants.
    /// Drives the <c>PointerOver</c> visual state; touch-only devices never set it. MAUI's own flag is internal and set
    /// by native views, which drawn views do not have.
    /// </summary>
    public bool IsPointerOver { get; private set; }

    /// <summary>Where the last press started, in this view's coordinates (ripple origin).</summary>
    internal Point PressPosition { get; private set; }

    private bool _showsPressEffect;
    private SkUiPressAnimator? _pressEffect;

    /// <summary>Bindable <see cref="ShowsPressEffect"/>.</summary>
    public static readonly BindableProperty ShowsPressEffectProperty = BindableProperty.Create(nameof(ShowsPressEffect), typeof(bool), typeof(SkUiView), false,
        propertyChanged: (view, _, value) => ((SkUiView)view).OnShowsPressEffectChanged((bool)value));

    /// <summary>
    /// Draws the look's press feedback (<see cref="SkUiLook.DrawPressOverlay"/>: a dim or a ripple from the press point)
    /// over this view and its children while it is pressed, clipped to its rounded shape (a label's or border's corner
    /// radii). For containers that act as one button (a card, an icon with text): the view needs a tap handler
    /// (<see cref="Tapped"/> or <see cref="TappedCommand"/>) to be pressed. Buttons draw their own feedback.
    /// </summary>
    public bool ShowsPressEffect { get => (bool)GetValue(ShowsPressEffectProperty); set => SetValue(ShowsPressEffectProperty, value); }

    /// <summary>Sets <see cref="ShowsPressEffect"/> (same as the property setter).</summary>
    public SkUiView SetShowsPressEffect(bool value)
    {
        ShowsPressEffect = value;
        return this;
    }

    private void OnShowsPressEffectChanged(bool value)
    {
        if (_showsPressEffect == value) return;
        _showsPressEffect = value;
        InvalidatePaint();
    }

    /// <summary>Corner radii the press effect is clipped to (default square; labels and borders use their own).</summary>
    internal virtual CornerRadius PressEffectCornerRadii => default;

    /// <summary>The look or color scheme changed: forget the cached measure and re-record (no per-node propagation).</summary>
    internal void MarkLookChanged()
    {
        _measureDirty = true;
        _arrangeDirty = true;
        if (_effects is { } effects)
            effects.PaintVersion++;
        SkUiRenderInvalidation.Mark(this, SkUiRenderDirty.Content);
    }

    SkUiAnimationClock? ISkUiTransitionHost.TransitionClock => _renderState is { HasCommitted: true } ? AnimationClock : null;

    void ISkUiTransitionHost.InvalidateTransition() => InvalidatePaint();

    private void SetPressed(bool value)
    {
        if (IsPressed == value) return;
        IsPressed = value;
        if (_showsPressEffect || _pressEffect is not null)
            (_pressEffect ??= new SkUiPressAnimator(this)).SetPressed(value, PressPosition);
        OnPropertyChanged(nameof(IsPressed));
        OnPressedChanged();
        ChangeVisualState();
        InvalidatePaint();
    }

    void ISkUiInputNode.SetPointerOver(bool isOver)
    {
        if (IsPointerOver == isOver) return;
        IsPointerOver = isOver;
        SkUiHover.Changed(isOver);
        OnPropertyChanged(nameof(IsPointerOver));
        ChangeVisualState();
    }

    /// <summary>
    /// Moves to MAUI's visual states from SkiaUi's input state, as MAUI's <c>VisualElement</c> does from the native view's:
    /// <c>CommonStates</c> is <c>Disabled</c> (also while <see cref="CanReceiveTap"/> is false, e.g. a command that
    /// cannot execute), else <c>PointerOver</c> (<see cref="IsPointerOver"/>), else <c>Normal</c>; while enabled, the
    /// focus group gets <c>Focused</c> / <c>Unfocused</c>. Controls add theirs (buttons <c>Pressed</c>, toggles their
    /// checked states). Runs on press, hover, enabled and control state changes, and when visual state groups are set.
    /// </summary>
    protected override void ChangeVisualState()
    {
        var enabled = IsVisualStateEnabled;
        VisualStateManager.GoToState(this, !enabled ? VisualStateManager.CommonStates.Disabled
            : IsPointerOver ? VisualStateManager.CommonStates.PointerOver : VisualStateManager.CommonStates.Normal);
        if (enabled)
            VisualStateManager.GoToState(this, IsFocused ? VisualStateManager.CommonStates.Focused : UnfocusedState);
    }

    /// <summary>MAUI's (internal) name of the focus group's unfocused state.</summary>
    private const string UnfocusedState = "Unfocused";

    /// <summary>Enabled for visual states: <see cref="VisualElement.IsEnabled"/> and <see cref="CanReceiveTap"/>.</summary>
    private protected bool IsVisualStateEnabled => IsEnabled && CanReceiveTap;

    /// <summary>Updates intrinsic control feedback when the shared pointer state changes.</summary>
    protected virtual void OnPressedChanged() { }

    /// <summary>Whether an intrinsic tap action is currently enabled.</summary>
    protected virtual bool CanReceiveTap => true;

    /// <summary>
    /// Raised when this node's own content must be re-recorded (<see cref="InvalidatePaint"/>), and on the
    /// surface root once per frame when anything below it changed.
    /// </summary>
    public event EventHandler? PaintInvalidated;

    /// <summary>
    /// Raised when this node's measure is invalidated, by its own change or a descendant's, once per update batch
    /// (tests, diagnostics). Ancestors are not re-recorded for it (<see cref="PaintInvalidated"/> may not follow).
    /// </summary>
    internal event EventHandler? LayoutInvalidated;

    /// <summary>Raised on a render root when its subtree needs a new frame (first change per frame only).</summary>
    internal event EventHandler? RenderRootDirty;

    /// <summary>
    /// Clips this node's content, children and overlay to its arranged rectangle. Defaults to <c>true</c> for
    /// leaf controls and <c>false</c> for layouts / content hosts (MAUI <c>Layout.IsClippedToBounds</c> parity),
    /// so children can overflow (e.g. shadows, press scale). Scroll viewports always clip their children.
    /// </summary>
    public static readonly BindableProperty ClipToBoundsProperty = BindableProperty.Create(
        nameof(ClipToBounds), typeof(bool), typeof(SkUiView), true,
        defaultValueCreator: view => view is not (SkUiLayout or SkUiContentView or SkUiExpander or Core.SkUiCoreHost));

    /// <inheritdoc cref="ClipToBoundsProperty" />
    public bool ClipToBounds { get => (bool)GetValue(ClipToBoundsProperty); set => SetValue(ClipToBoundsProperty, value); }

    /// <summary>
    /// Whether hosted native views below are clipped to this node's arranged rectangle: <see cref="ClipToBounds"/>, or
    /// while drawn content is revealed in a band native views cannot be scaled into (an animating expander).
    /// </summary>
    internal virtual bool ClipsHostedViews => ClipToBounds;

    /// <summary>
    /// Opts this node into single taps. MAUI <c>GestureRecognizers</c> also work for taps: a <see cref="TapGestureRecognizer"/>
    /// (1 or 2 taps, primary button) runs on drawn taps, after this event.
    /// </summary>
    public event EventHandler<SkUiTappedEventArgs>? Tapped;

    /// <summary>The clock shared by this node and its surface-owning ancestor.</summary>
    /// <remarks>
    /// Resolves to the topmost SkiaUi ancestor's clock. Starting animations while this node is
    /// disconnected (or only mid-tree) creates a local clock; when the subtree is later parented
    /// under another SkiaUi host, <see cref="OnAnimationRootChanged"/> runs so clients can rebind.
    /// </remarks>
    public SkUiAnimationClock AnimationClock => SkiaParent?.AnimationClock ?? (_animationClock ??= new());

    internal SkUiView? SkiaParent => Parent as SkUiView;

    /// <summary>Effective (inherited or explicit) right-to-left flow direction.</summary>
    internal bool IsRightToLeft =>
        ((IVisualElementController)this).EffectiveFlowDirection.HasFlag(EffectiveFlowDirection.RightToLeft);

    /// <summary>Width of the coordinate space children are arranged in (mirroring axis for RTL).</summary>
    internal virtual double ChildrenSpaceWidth => Frame.Width;

    /// <summary>Called when the effective flow direction changes (own or inherited); layout is already invalidated.</summary>
    internal virtual void OnEffectiveFlowDirectionChanged() { }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();
        // A local clock is only valid while this node is the top of its SkiaUi subtree. Once a
        // Skia parent appears, abandon it so descendants re-register on the shared ancestor clock:
        // clients rebind first (OnAnimationRootChanged), then whatever is left on the local clock stops.
        var abandoned = SkiaParent is not null ? _animationClock : null;
        if (abandoned is not null)
            _animationClock = null;
        // When this node is detached, descendants still have it as Parent — pass subtreeDetached so
        // they stop clocks instead of rebinding onto an orphan mid-tree clock.
        NotifyAnimationRootChanged(subtreeDetached: Parent is null);
        abandoned?.StopAll();
        // Detached from a drawn parent: its retained pictures are released by the compositor, so a later
        // attach must record this subtree from scratch.
        if (SkiaParent is null && _renderState is not null)
            SkUiRenderInvalidation.ResetSubtree(this);
        // Pointers captured by a detached subtree must not complete (taps, presses) later, and it is no longer under
        // the pointer: no PointerOver state sticks to it until the next hover move.
        SkUiGestureSet.CancelSubtree(this);
        SkUiHover.ClearSubtree(this);
        // A focused node that left its surface is no longer focused.
        SkUiFocusManager.ValidateAll();
        SkUiShownTracker.OnParentSet(this);
    }

    /// <summary>
    /// Called when this node or an ancestor changes parenting such that <see cref="AnimationClock"/>
    /// may resolve to a different instance. Override to rebind repeating animations.
    /// </summary>
    /// <param name="subtreeDetached">
    /// True when an ancestor (or this node) was removed from its parent; descendants should stop
    /// rather than rebind, even though their own <see cref="Element.Parent"/> may still be set.
    /// </param>
    protected virtual void OnAnimationRootChanged(bool subtreeDetached = false) { }

    /// <summary>Notifies this node and every SkiaUi descendant that the shared clock may have moved.</summary>
    private void NotifyAnimationRootChanged(bool subtreeDetached)
    {
        OnAnimationRootChanged(subtreeDetached);
        foreach (var child in SkiaChildren)
        {
            if (child is SkUiView view)
                view.NotifyAnimationRootChanged(subtreeDetached);
        }
    }

    /// <summary>Called on this node and every SkiaUi descendant when this node moved within the drawn tree.</summary>
    internal virtual void NotifyMoved()
    {
        foreach (var child in SkiaChildren)
        {
            if (child is SkUiView view)
                view.NotifyMoved();
        }
    }

    /// <summary>
    /// Hosted <see cref="ISkUiView"/> children, if any. Used to walk the tree when the standalone root's
    /// handler (dis)connects, e.g. to attach/detach <see cref="SkUiMauiContentView"/> native overlays.
    /// </summary>
    internal virtual IEnumerable<ISkUiView> SkiaChildren => [];

    /// <summary>
    /// Render-thread compositing statistics of this view's surface (standalone roots with a handler; default otherwise).
    /// Intended for diagnostics and benchmarks.
    /// </summary>
    public SkUiRenderStatistics GetRenderStatistics()
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        return Handler is SkUiViewHandler handler ? handler.RenderStatistics : default;
#else
        return default;
#endif
    }

    /// <summary>Recording and shadow counters of this view's surface since it was created (diagnostics, stress pages).</summary>
    internal SkUiRenderCounters GetRenderCounters()
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        return Handler is SkUiViewHandler handler ? handler.RenderCounters : default;
#else
        return default;
#endif
    }

#if SKUI_DIAGNOSTICS
    /// <summary>
    /// Starts recording per-frame timings of this view's surface (diagnostics, stress pages); returns the trace, or
    /// <c>null</c> without a surface. Replaces a running trace. Stop with <see cref="StopFrameTrace"/>.
    /// </summary>
    internal SkUiFrameTrace? StartFrameTrace()
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        if (Handler is not SkUiViewHandler { Compositor: { } compositor })
            return null;
        var trace = new SkUiFrameTrace();
        compositor.FrameTrace = trace;
        return trace;
#else
        return null;
#endif
    }

    /// <summary>Stops recording frame timings (see <see cref="StartFrameTrace"/>).</summary>
    internal void StopFrameTrace()
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        if (Handler is SkUiViewHandler { Compositor: { } compositor })
            compositor.FrameTrace = null;
#endif
    }
#endif

    /// <summary>Resets <see cref="GetRenderStatistics"/> for this view's surface.</summary>
    public void ResetRenderStatistics()
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        (Handler as SkUiViewHandler)?.ResetRenderStatistics();
#endif
    }

    /// <summary>Selects GPU rendering for a standalone node. Set before attaching a handler.</summary>
    public bool HwAccelerated
    {
        get => _hwAccelerated;
        set
        {
            if (Handler is not null && value != _hwAccelerated)
                throw new InvalidOperationException("HwAccelerated must be set before handler creation.");
            _hwAccelerated = value;
        }
    }

#if SKUI_DIAGNOSTICS
    /// <summary>
    /// Number of UI-thread <c>SKPicture</c> recordings since the last
    /// <see cref="ResetDiagnosticRecordStats"/> call. Used by the stress harness.
    /// </summary>
    internal int DiagnosticRecordFrameCount => _diagnosticRecordFrameCount;

    /// <summary>
    /// Cumulative milliseconds spent in UI-thread picture recording since the last
    /// <see cref="ResetDiagnosticRecordStats"/> call.
    /// </summary>
    internal double DiagnosticRecordFrameTotalMs => _diagnosticRecordFrameTotalMs;

    /// <summary>Clears cumulative UI-thread record-frame diagnostics.</summary>
    internal void ResetDiagnosticRecordStats()
    {
        _diagnosticRecordFrameCount = 0;
        _diagnosticRecordFrameTotalMs = 0;
    }

    /// <summary>Records one UI-thread picture-recording sample for diagnostics.</summary>
    internal void NoteDiagnosticRecordFrame(double milliseconds)
    {
        if (milliseconds < 0 || double.IsNaN(milliseconds) || double.IsInfinity(milliseconds))
            return;
        _diagnosticRecordFrameCount++;
        _diagnosticRecordFrameTotalMs += milliseconds;
    }
#endif

    /// <inheritdoc />
    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        var constraint = new Size(widthConstraint, heightConstraint);
        if (!_measureDirty && constraint == _lastConstraint)
            return _measuredSize;

        IView view = this;
        var width = Math.Max(0, widthConstraint - Margin.HorizontalThickness);
        var height = Math.Max(0, heightConstraint - Margin.VerticalThickness);
        var maximumWidth = double.IsNaN(view.MaximumWidth) ? double.PositiveInfinity : view.MaximumWidth;
        var maximumHeight = double.IsNaN(view.MaximumHeight) ? double.PositiveInfinity : view.MaximumHeight;
        var content = MeasureContent(
            Math.Min(width, double.IsNaN(view.Width) ? maximumWidth : view.Width),
            Math.Min(height, double.IsNaN(view.Height) ? maximumHeight : view.Height));
        _measuredSize = IsVisible
            ? new Size(
                LayoutManager.ResolveConstraints(width, view.Width, content.Width, view.MinimumWidth, maximumWidth) + Margin.HorizontalThickness,
                LayoutManager.ResolveConstraints(height, view.Height, content.Height, view.MinimumHeight, maximumHeight) + Margin.VerticalThickness)
            : Size.Zero;
        _lastConstraint = constraint;
        _measureDirty = false;
        _arrangeDirty = true;
        return _measuredSize;
    }

    /// <summary>Measures intrinsic content without margins, in DIPs.</summary>
    protected virtual Size MeasureContent(double widthConstraint, double heightConstraint) => Size.Zero;

    /// <inheritdoc />
    protected override Size ArrangeOverride(Rect bounds)
    {
        if (!_arrangeDirty && bounds == _lastArrangeBounds)
        {
#if WINDOWS
            // WinUI arranges per layout pass: an Arrange made before the native view was in the tree is lost, so
            // re-apply the cached frame (Android / iOS keep the frame they were given).
            Handler?.PlatformArrange(Frame);
#endif
            return Frame.Size;
        }
        var previousFrame = Frame;
        var previousSize = Frame.Size;
        var frame = this.ComputeFrame(bounds);
        // RTL: MAUI mirrors native views; drawn children have none, so mirror the final frame (after margins and
        // alignment, like a native mirrored layout) inside the parent's children space. Hit-testing, overlays and
        // rendering all use Frame, so they follow automatically.
        if (SkiaParent is { IsRightToLeft: true } parent)
            frame = new Rect(parent.ChildrenSpaceWidth - frame.Right, frame.Y, frame.Width, frame.Height);
        Frame = frame;
        if (_arrangeDirty || previousSize != Frame.Size)
            ArrangeContent(Frame.Size);
        // Descendants keep their cached frames, but their root-relative position changed (native overlays follow).
        if (previousFrame.Location != Frame.Location && Handler is null && SkUiMauiContentView.HostsNativeViews)
            NotifyMoved();
        var frameChanged = _lastArrangeBounds != bounds || previousFrame != Frame;
        _lastArrangeBounds = bounds;
        _arrangeDirty = false;
        _laidOut = true;
        Handler?.PlatformArrange(Frame);
        // Offset-only changes are composite-time; the recorder re-records content only when the size changed.
        if (frameChanged)
            InvalidateRender(SkUiRenderDirty.Props);
        return Frame.Size;
    }

    /// <summary>Arranges hosted content in this node's local coordinate system.</summary>
    protected virtual void ArrangeContent(Size size) { }

    /// <inheritdoc />
    protected override void InvalidateMeasureOverride()
    {
        _measureDirty = true;
        _arrangeDirty = true;
        _layoutPending = true;
        _ownLayoutPending = true;
        InvalidatePaint();
    }

    /// <summary>
    /// A child's measure changed: this node is measured and arranged again, but its own content is not re-recorded
    /// (a size change re-records it when arranged; moved children are composite-time). So a relayout every frame (an
    /// expanding expander) records nothing above the changed node and leaves ancestor shadows alone.
    /// </summary>
    private void InvalidateMeasureFromChild()
    {
        _measureDirty = true;
        _arrangeDirty = true;
        _layoutPending = true;
        if (_updateDepth == 0)
            FlushInvalidation();
    }

    /// <summary>
    /// Surface roots, before recording a frame: lays the tree out again after a descendant's measure changed, with the
    /// constraint and bounds the native layout last gave the root (they have not changed, or the native layout would
    /// have measured the root itself). When the root's own size changes, the native layout is asked as well, and the
    /// tree is arranged in the old bounds until it runs.
    /// </summary>
    internal void RelayoutIfNeeded()
    {
        if (!_relayoutPending)
            return;
        _relayoutPending = false;
        IView view = this;
        var previous = view.DesiredSize;
        var size = view.Measure(_lastConstraint.Width, _lastConstraint.Height);
        if (size != previous)
            base.InvalidateMeasureOverride();
        view.Arrange(_lastArrangeBounds);
    }

    /// <summary>Coalesces layout and paint notifications until the matching EndUpdating call.</summary>
    public void StartUpdating() => _updateDepth++;

    /// <summary>Ends an update batch and propagates at most one invalidation.</summary>
    public void EndUpdating()
    {
        if (_updateDepth == 0)
            throw new InvalidOperationException("No update batch is active.");
        if (--_updateDepth == 0)
            FlushInvalidation();
    }

    /// <summary>Requests a re-record of this node's own content (not its children) without invalidating measure.</summary>
    public void InvalidatePaint()
    {
        _paintPending = true;
        if (_updateDepth == 0)
            FlushInvalidation();
    }

    /// <summary>Marks composite-time state (props / children) dirty, honoring update batches.</summary>
    internal void InvalidateRender(SkUiRenderDirty flags)
    {
        _renderPending |= flags;
        if (_updateDepth == 0)
            FlushInvalidation();
    }

    private void FlushInvalidation()
    {
        var invalidateLayout = _layoutPending;
        var ownLayout = _ownLayoutPending;
        var invalidatePaint = _paintPending;
        var render = _renderPending;
        _layoutPending = _ownLayoutPending = _paintPending = false;
        _renderPending = SkUiRenderDirty.None;
        if (invalidateLayout)
        {
            if (SkiaParent is { } parent)
                parent.InvalidateMeasureFromChild();
            else if (!ownLayout && _laidOut && HasLiveSurface)
            {
                // Relayout boundary: a descendant changed, so this surface root is laid out again in place right
                // before its next frame (RelayoutIfNeeded), as one frame with the change. The native layout is
                // only asked when the root's own size changes. The frame is requested by the change's origin, which
                // always re-records itself (its mark reaches this root after this layout walk).
                _relayoutPending = true;
            }
            else
                base.InvalidateMeasureOverride();
            LayoutInvalidated?.Invoke(this, EventArgs.Empty);
        }
        if (invalidatePaint)
        {
            render |= SkUiRenderDirty.Content;
            if (_effects is { } effects)
                effects.PaintVersion++; // the fill, and so the shadow's silhouette, may have changed
        }
        if (render != SkUiRenderDirty.None)
            SkUiRenderInvalidation.Mark(this, render);
        if (invalidatePaint)
            PaintInvalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if ((propertyName == nameof(IsEnabled) && !IsEnabled)
            || (propertyName == nameof(IsVisible) && !IsVisible)
            || (propertyName == nameof(InputTransparent) && InputTransparent))
        {
            CancelGestures();
            SkUiFocusManager.ValidateAll();
        }
        if (IsSemanticProperty(propertyName))
            SkUiSemantics.Invalidate(this);
        if (propertyName == nameof(IsVisible))
        {
            InvalidateMeasureOverride();
            SkUiShownTracker.OnVisibilityChanged(this);
        }
        // MAUI raises FlowDirection on every descendant whose effective direction changes.
        if (propertyName == nameof(FlowDirection))
        {
            InvalidateMeasureOverride();
            OnEffectiveFlowDirectionChanged();
        }
        switch (propertyName)
        {
            case nameof(WidthRequest):
            case nameof(HeightRequest):
            case nameof(MinimumWidthRequest):
            case nameof(MinimumHeightRequest):
            case nameof(MaximumWidthRequest):
            case nameof(MaximumHeightRequest):
            case nameof(Margin):
            case nameof(HorizontalOptions):
            case nameof(VerticalOptions):
                InvalidateMeasureOverride();
                break;
            case nameof(Background):
            case nameof(BackgroundColor):
                InvalidatePaint();
                break;
            case nameof(ZIndex):
                (SkiaParent)?.InvalidateRender(SkUiRenderDirty.Children);
                break;
            case nameof(Clip):
                // Also raised by MAUI when the geometry's properties change.
                OnClipChanged(Clip);
                break;
            case nameof(Shadow):
                // Also raised by MAUI when the shadow's properties change.
                OnShadowChanged(Shadow);
                break;
            case nameof(IsVisible):
            case nameof(ClipToBounds):
            case nameof(Opacity):
            case nameof(TranslationX):
            case nameof(TranslationY):
            case nameof(Rotation):
            case nameof(Scale):
            case nameof(ScaleX):
            case nameof(ScaleY):
            case nameof(AnchorX):
            case nameof(AnchorY):
                InvalidateRender(SkUiRenderDirty.Props);
                break;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Immediate-mode paint of this subtree in local coordinates (offscreen snapshots, tests). Surfaces use the
    /// retained compositor instead, which composes the same phases from recorded pictures on the render thread.
    /// </remarks>
    public void Paint(SKCanvas canvas) => SkUiImmediatePainter.Paint(this, canvas, applyOffset: false);

    SkUiRenderState ISkUiRenderable.RenderState => RenderState;

    internal SkUiRenderState RenderState => _renderState ??= new SkUiRenderState();

    ISkUiRenderable? ISkUiRenderable.RenderParent => SkiaParent;

    void ISkUiRenderable.GetRenderProps(ref SkUiRenderProps props)
    {
        var frame = Frame;
        props.X = (float)frame.X;
        props.Y = (float)frame.Y;
        props.Width = (float)frame.Width;
        props.Height = (float)frame.Height;
        props.TranslationX = (float)TranslationX;
        props.TranslationY = (float)TranslationY;
        props.Rotation = (float)Rotation;
        var scale = Scale;
        props.ScaleX = (float)(scale * ScaleX);
        props.ScaleY = (float)(scale * ScaleY);
        props.AnchorX = (float)AnchorX;
        props.AnchorY = (float)AnchorY;
        props.Opacity = (float)Opacity;
        props.IsVisible = IsVisible;
        props.ClipToBounds = ClipToBounds;
        OnGetRenderProps(ref props);
        if (_effects is { } effects)
        {
            props.ClipPath = effects.GetClipPath(_clip, props.Width, props.Height);
            props.Shadow = effects.GetShadow(_shadow, this, props);
        }
    }

    private SkUiVisualEffects? _effects;
    private Microsoft.Maui.Controls.Shapes.Geometry? _clip;
    private Shadow? _shadow;
    private SkUiWeakListener<SkUiView>? _shadowBrushListener;

    private void OnClipChanged(Microsoft.Maui.Controls.Shapes.Geometry? value)
    {
        _clip = value;
        if (value is null && _effects is null) return;
        (_effects ??= new()).ClipVersion++;
        InvalidateRender(SkUiRenderDirty.Props);
    }

    private void OnShadowChanged(Shadow? value)
    {
        _shadow = value;
        if (value is null && _effects is null) return;
        (_effects ??= new()).ShadowVersion++;
        // MAUI forwards the shadow's own changes, not its gradient's stops.
        (_shadowBrushListener ??= new(this, static (view, _) =>
        {
            if (view._effects is { } effects) effects.ShadowVersion++;
            view.InvalidateRender(SkUiRenderDirty.Props);
        })).Listen(value?.Brush);
        InvalidateRender(SkUiRenderDirty.Props);
    }

    SKPath? ISkUiShadowCaster.CreateShadowOutline(float width, float height) => CreateShadowOutline(width, height);

    /// <summary>
    /// The silhouette of the view's own opaque fill, which its shadow is cast from (MAUI on Android draws the shadow of a view
    /// with an opaque background from the background's shape), or <c>null</c> to cast it from everything the view and its
    /// children draw. By default the rectangle of an opaque <see cref="VisualElement.Background"/> drawn by the default
    /// painter; controls with shaped fills return their shape.
    /// </summary>
    internal virtual SKPath? CreateShadowOutline(float width, float height) =>
        _paintBackground is null && ResolveBackgroundFill() is { IsOpaque: true } ? RectangleOutline(width, height) : null;

    /// <summary>A rectangle of the given size as a shadow outline.</summary>
    private protected static SKPath RectangleOutline(float width, float height)
    {
        using var builder = new SKPathBuilder();
        builder.AddRect(new SKRect(0, 0, width, height));
        return builder.Detach();
    }

    /// <summary>Lets containers add children offset / clip, spinning content, or ink overflow.</summary>
    internal virtual void OnGetRenderProps(ref SkUiRenderProps props) { }

    void ISkUiRenderable.RecordContent(SKCanvas canvas)
    {
        if (_paintBackground is { } paintBackground)
            paintBackground(canvas);
        else
            OnPaintBackground(canvas);
        OnPaintContent(canvas);
    }

    bool ISkUiRenderable.HasOverlay => _paintOverlay is not null || _showsPressEffect || _focusRingVisible;

    void ISkUiRenderable.RecordOverlay(SKCanvas canvas)
    {
        _paintOverlay?.Invoke(canvas);
        if (_showsPressEffect)
            SkUiLook.Current.DrawPressOverlay(canvas, new SkUiPressOverlayPaint(new SKRect(0, 0, (float)Width, (float)Height),
                PressEffectCornerRadii, _pressEffect?.Visual ?? SkUiPressVisual.None, IsEnabled: true)); // a disabled view is not pressed; no veil
        if (_focusRingVisible)
            DrawFocusRing(canvas);
    }

    void ISkUiRenderable.GetRenderChildren(List<ISkUiRenderable> children) => AddRenderChildren(children);

    /// <summary>Appends drawn children in paint order; defaults to <see cref="SkiaChildren"/>.</summary>
    internal virtual void AddRenderChildren(List<ISkUiRenderable> children)
    {
        foreach (var child in SkiaChildren)
            if (child is ISkUiRenderable renderable)
                children.Add(renderable);
    }

    void ISkUiRenderable.OnRenderRootDirty(bool fromDescendant)
    {
        RenderRootDirty?.Invoke(this, EventArgs.Empty);
        // A root's own content invalidation already raised PaintInvalidated in FlushInvalidation.
        if (fromDescendant)
            PaintInvalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Animates a composite-time property on the render thread, so the motion stays smooth while the UI thread is
    /// busy. The bindable property is updated to the final (or last shown, if cancelled) value when it ends.
    /// Setting the property during the animation cancels it. Returns <c>true</c> when it ran to completion.
    /// </summary>
    public Task<bool> AnimateAsync(SkUiAnimatableProperty property, double to, uint length = 250, Easing? easing = null)
    {
        if (!double.IsFinite(to)) throw new ArgumentOutOfRangeException(nameof(to));
        var (properties, targets) = property switch
        {
            SkUiAnimatableProperty.Opacity => (new[] { SkUiRenderProperty.Opacity }, new[] { (float)Math.Clamp(to, 0, 1) }),
            SkUiAnimatableProperty.TranslationX => ([SkUiRenderProperty.TranslationX], [(float)to]),
            SkUiAnimatableProperty.TranslationY => ([SkUiRenderProperty.TranslationY], [(float)to]),
            SkUiAnimatableProperty.Rotation => ([SkUiRenderProperty.Rotation], [(float)to]),
            SkUiAnimatableProperty.ScaleX => ([SkUiRenderProperty.ScaleX], [(float)(Scale * to)]),
            SkUiAnimatableProperty.ScaleY => ([SkUiRenderProperty.ScaleY], [(float)(Scale * to)]),
            SkUiAnimatableProperty.Scale => (new[] { SkUiRenderProperty.ScaleX, SkUiRenderProperty.ScaleY },
                new[] { (float)(to * ScaleX), (float)(to * ScaleY) }),
            _ => throw new ArgumentOutOfRangeException(nameof(property))
        };
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tween = new SkUiRenderTween(properties, targets, TimeSpan.FromMilliseconds(Math.Max(1, length)), easing)
        {
            Finished = (animation, completed) =>
            {
                RenderState.ActiveAnimations?.Remove(animation);
                if (!animation.SupersededByUi && ((SkUiRenderTween)animation).LastValues is { } values)
                    ApplyAnimatedValues(property, (SkUiRenderTween)animation, values);
                completion.TrySetResult(completed);
            }
        };
        SkUiRenderInvalidation.Enqueue(this, tween);
        return completion.Task;
    }

    private void ApplyAnimatedValues(SkUiAnimatableProperty property, SkUiRenderTween tween, float[] values)
    {
        for (var index = 0; index < values.Length; index++)
            RenderState.Acknowledge(tween.Properties[index], values[index]);
        switch (property)
        {
            case SkUiAnimatableProperty.Opacity: Opacity = values[0]; break;
            case SkUiAnimatableProperty.TranslationX: TranslationX = values[0]; break;
            case SkUiAnimatableProperty.TranslationY: TranslationY = values[0]; break;
            case SkUiAnimatableProperty.Rotation: Rotation = values[0]; break;
            case SkUiAnimatableProperty.ScaleX: ScaleX = Scale == 0 ? 0 : values[0] / Scale; break;
            case SkUiAnimatableProperty.ScaleY: ScaleY = Scale == 0 ? 0 : values[0] / Scale; break;
            case SkUiAnimatableProperty.Scale: Scale = ScaleX == 0 ? 0 : values[0] / ScaleX; break;
        }
    }

    private Action<SKCanvas>? _paintBackground;
    private Action<SKCanvas>? _paintOverlay;

    /// <summary>
    /// Background-layer painter. When set, replaces the default <see cref="VisualElement.Background"/> /
    /// <see cref="VisualElement.BackgroundColor"/> fill. Clear to restore that default.
    /// Prefer this over subclassing for chrome customization.
    /// </summary>
    public Action<SKCanvas>? PaintBackground
    {
        get => _paintBackground;
        set => SetPaintBackground(value);
    }

    /// <summary>
    /// Overlay-layer painter drawn after <see cref="OnPaintContent"/> and the children (badges, press tints, borders
    /// over content); <c>null</c> (default) means no overlay layer. Subclasses set it too, e.g. in their constructor.
    /// </summary>
    public Action<SKCanvas>? PaintOverlay
    {
        get => _paintOverlay;
        set => SetPaintOverlay(value);
    }

    /// <summary>Sets the Background painter; <c>null</c> restores the default fill via <see cref="OnPaintBackground"/>.</summary>
    public SkUiView SetPaintBackground(Action<SKCanvas>? value)
    {
        if (ReferenceEquals(_paintBackground, value)) return this;
        _paintBackground = value;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the Overlay painter; <c>null</c> removes the overlay layer.</summary>
    public SkUiView SetPaintOverlay(Action<SKCanvas>? value)
    {
        if (ReferenceEquals(_paintOverlay, value)) return this;
        _paintOverlay = value;
        InvalidatePaint();
        return this;
    }

    /// <summary>
    /// Resolves a solid fill from <see cref="VisualElement.Background"/> or <see cref="VisualElement.BackgroundColor"/>.
    /// MAUI's default <see cref="Brush"/> is an empty <see cref="SolidColorBrush"/> with a null color; that empty
    /// brush must not hide a set <see cref="VisualElement.BackgroundColor"/> (same rule as <see cref="IView.Background"/>).
    /// Other brushes fall through to <see cref="VisualElement.BackgroundColor"/>; <see cref="ResolveBackgroundPaint"/> also
    /// returns gradients.
    /// </summary>
    protected Color? ResolveSolidBackgroundColor()
    {
        if (Background is SolidColorBrush brush && brush.Color is not null)
            return brush.Color;
        return BackgroundColor;
    }

    /// <summary>
    /// The fill of the background: a solid or gradient <see cref="VisualElement.Background"/> (linear and radial gradients,
    /// mapped onto the view's bounds), else <see cref="VisualElement.BackgroundColor"/>, else <c>null</c>. An empty brush (MAUI's
    /// default, a gradient without stops) does not hide <see cref="VisualElement.BackgroundColor"/>; image brushes are not drawn.
    /// </summary>
    protected Paint? ResolveBackgroundPaint()
    {
        switch (Background)
        {
            case SolidColorBrush { Color: { } color }:
                return new SolidPaint(color);
            case GradientBrush { GradientStops.Count: > 0 } gradient:
                return gradient;
        }
        return BackgroundColor is { } background ? new SolidPaint(background) : null;
    }

    /// <summary>
    /// <see cref="ResolveBackgroundPaint"/> as a resolved fill, without allocating for solid colors (controls resolve their
    /// background on every recording); <c>null</c> without a background.
    /// </summary>
    internal SkUiFill? ResolveBackgroundFill()
    {
        switch (Background)
        {
            case SolidColorBrush { Color: { } color }:
                return SkUiFill.From(color);
            case GradientBrush { GradientStops.Count: > 0 } gradient:
                return SkUiFill.From((Paint)gradient);
        }
        return BackgroundColor is { } background ? SkUiFill.From(background) : null;
    }

    /// <summary>
    /// Color used to clear the platform/backing surface before painting a frame: the root's resolved background when it is
    /// an opaque color; otherwise transparent (gradients, translucent colors, none) so rounded or translucent roots show
    /// host content underneath (FR-8 / FR-11), and a gradient never shows a <see cref="VisualElement.BackgroundColor"/> it
    /// replaces. The root draws its own background over the clear in every case.
    /// </summary>
    internal SKColor SurfaceClearColor =>
        ResolveBackgroundFill() is { Gradient: null, Color.Alpha: 255 } fill ? fill.Color : SKColors.Transparent;

    /// <summary>
    /// Default Background when <see cref="PaintBackground"/> is unset: the view's rectangle filled with
    /// <see cref="ResolveBackgroundPaint"/> (solid colors and gradients). Subclasses may call this from a custom
    /// <see cref="PaintBackground"/> painter.
    /// </summary>
    protected void PaintDefaultBackground(SKCanvas canvas)
    {
        if (ResolveBackgroundFill() is { } fill)
            SkUiShapePainter.FillRect(canvas, new SKRect(0, 0, (float)Width, (float)Height), fill);
    }

    /// <summary>
    /// Background paint phase when <see cref="PaintBackground"/> is unset.
    /// Prefer <see cref="PaintBackground"/> for new code; override this for subclass chrome that must stay virtual.
    /// </summary>
    protected virtual void OnPaintBackground(SKCanvas canvas) => PaintDefaultBackground(canvas);

    /// <summary>Content paint phase (structural: glyphs, children, hosted content). Always virtual — not replaceable by a delegate.</summary>
    protected virtual void OnPaintContent(SKCanvas canvas) { }

    /// <summary>Converts a MAUI color to its Skia RGBA representation.</summary>
    protected static SKColor ToSkColor(Color color) => new(
        (byte)(color.Red * 255), (byte)(color.Green * 255),
        (byte)(color.Blue * 255), (byte)(color.Alpha * 255));

    private SKMatrix RenderMatrix
    {
        get
        {
            var anchorX = (float)(Width * AnchorX);
            var anchorY = (float)(Height * AnchorY);
            return SKMatrix.CreateTranslation((float)TranslationX + anchorX, (float)TranslationY + anchorY)
                .PreConcat(SKMatrix.CreateRotationDegrees((float)Rotation))
                .PreConcat(SKMatrix.CreateScale((float)(Scale * ScaleX), (float)(Scale * ScaleY)))
                .PreConcat(SKMatrix.CreateTranslation(-anchorX, -anchorY));
        }
    }

    internal static bool MapPoint(ISkUiView child, Point position, out Point local)
    {
        var point = new SKPoint((float)(position.X - child.Frame.X), (float)(position.Y - child.Frame.Y));
        if (child is SkUiView node)
        {
            if (!node.RenderMatrix.TryInvert(out var inverse))
            {
                local = default;
                return false;
            }
            point = inverse.MapPoint(point);
        }
        local = new Point(point.X, point.Y);
        return true;
    }

    internal void ValidateChild(ISkUiView child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent is not null || child.Handler is not null)
            throw new InvalidOperationException("A hosted view must be unparented and have no handler.");
        for (IElement? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
            if (ReferenceEquals(ancestor, child))
                throw new InvalidOperationException("A SkiaUi tree cannot contain a cycle.");
        if (child is not Element)
            throw new ArgumentException("Hosted views must also be MAUI Elements for XAML ownership.", nameof(child));
    }

    internal void AttachChild(ISkUiView child)
    {
        AddLogicalChild((Element)child);
        InvalidateRender(SkUiRenderDirty.Children);
        InvalidateMeasureOverride();
    }

    internal void DetachChild(ISkUiView child)
    {
        RemoveLogicalChild((Element)child);
        InvalidateRender(SkUiRenderDirty.Children);
        InvalidateMeasureOverride();
    }

    /// <summary>
    /// Makes an image source that this view shows bind against it, as MAUI's image views do: the source becomes this
    /// view's element child (a weak link, so a shared source keeps no view alive), so it inherits the binding context
    /// and resources; the old source is released. Call <see cref="InheritBindingContext"/> for it on context changes.
    /// </summary>
    private protected void AdoptImageSource(ImageSource? oldValue, ImageSource? newValue)
    {
        if (oldValue is not null && !ReferenceEquals(oldValue, newValue) && ReferenceEquals(oldValue.Parent, this))
            oldValue.Parent = null;
        if (newValue is not null)
            newValue.Parent = this;
    }

    /// <summary>Gives a bindable value that is not a child (a source, brush, geometry, definition) this view's binding context.</summary>
    private protected void InheritBindingContext(BindableObject? value)
    {
        if (value is not null)
            SetInheritedBindingContext(value, BindingContext);
    }

    /// <summary>Takes this view's binding context back from a value it no longer uses (a context set on the value itself stays).</summary>
    private protected static void ReleaseBindingContext(BindableObject? value)
    {
        if (value is not null)
            SetInheritedBindingContext(value, null);
    }

    /// <summary>
    /// Delivers a pointer sample in this view's local DIPs, with this view as the surface root: the press is hit-tested
    /// once through the drawn tree (SkUi* and Core), then the gesture arena decides between the recognizers of the
    /// element under the pointer and its ancestors (taps, scrolling, pans…). Returns whether a drawn element takes the
    /// pointer; <c>false</c> lets native parents handle it. Custom input: add a recognizer to <see cref="Gestures"/>
    /// (e.g. <see cref="SkUiPointerGestureRecognizer"/>) instead of overriding this method.
    /// </summary>
    public bool Touch(SkUiTouchEvent touch) => Router.Dispatch(touch);

    internal SkUiPointerRouter Router => _router ??= new SkUiPointerRouter(this);

    bool ISkUiInputNode.IsHitTestVisible => IsVisible && !InputTransparent;

    bool ISkUiInputNode.IsInputEnabled => IsEnabled && CanReceiveTap;

    void ISkUiInputNode.CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers) => CollectGestureRecognizers(recognizers);

    /// <summary>Appends this view's recognizers: built-in tap, long press, swipe, pan, pinch, custom, then intrinsic ones.</summary>
    internal virtual void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        // Called for the views under a press: MAUI gesture input a drawn view does not run is reported then, once.
        if (!_mauiGesturesReported && (GestureRecognizers.Count > 0 || IsSet(BehaviorsProperty)))
            _mauiGesturesReported = SkUiMauiTaps.ReportUnsupported(this, native: !RunsMauiTaps);
        if (WantsSingleTap || WantsDoubleTap)
            recognizers.Add(_tap ??= new SkUiTapGestureRecognizer
            {
                TapHandler = args => { if (WantsSingleTap) OnTapped(args); },
                WantsDoubleTap = () => WantsDoubleTap,
                DoubleTapHandler = args =>
                {
                    _gestures?.RaiseDoubleTapped(args);
                    RaiseMauiTaps(args, 2);
                },
                PressedHandler = (pressed, position) =>
                {
                    PressPosition = position;
                    SetPressed(pressed);
                }
            });
        _gestures?.Collect(recognizers);
    }

    private bool WantsSingleTap => Tapped is not null || HandlesTap || (_tappedCommand?.CanExecute(_tappedCommandParameter) ?? false) || HasMauiTaps(1);

    private bool WantsDoubleTap => _gestures?.WantsDoubleTap == true || HasMauiTaps(2);

    /// <summary>
    /// Whether drawn taps run this view's MAUI tap recognizers: views inside a surface, which have no native view. A surface
    /// root has one, and MAUI runs its recognizers on it.
    /// </summary>
    private bool RunsMauiTaps => Handler is null;

    /// <summary>
    /// Whether a MAUI tap recognizer wants <paramref name="taps"/> taps. As on MAUI, it takes part whether or not its command
    /// can execute (an empty recognizer keeps taps from what is underneath).
    /// </summary>
    private bool HasMauiTaps(int taps) => GestureRecognizers.Count > 0 && RunsMauiTaps && SkUiMauiTaps.Has(GestureRecognizers, taps);

    private void RaiseMauiTaps(SkUiTappedEventArgs args, int taps)
    {
        if (GestureRecognizers.Count > 0 && RunsMauiTaps)
            SkUiMauiTaps.Raise(GestureRecognizers, taps, this, args.Position);
    }

    void ISkUiGestureElement.CancelGestures() => CancelGestures();

    /// <summary>Stops this view's gestures (press state, pans, scrolling).</summary>
    internal virtual void CancelGestures()
    {
        _tap?.Cancel();
        _gestures?.CancelAll();
        _router?.CancelAll();
        SetPressed(false);
    }

    private SkUiGestureSet GestureSet => _gestures ??= new SkUiGestureSet(this);

    /// <summary>Custom recognizers (e.g. <see cref="SkUiPointerGestureRecognizer"/>, <see cref="SkUiPanGestureRecognizer"/>), after the built-in ones.</summary>
    public IList<SkUiGestureRecognizer> Gestures => GestureSet.Custom ??= [];

    /// <summary>Double tap (single taps then wait <see cref="SkUiGestureSettings.DoubleTapTimeout"/>).</summary>
    public event EventHandler<SkUiTappedEventArgs>? DoubleTapped { add => GestureSet.DoubleTapped += value; remove => GestureSet.DoubleTapped -= value; }

    /// <summary>Long press (<see cref="SkUiGestureSettings.LongPressDuration"/> within the touch slop).</summary>
    public event EventHandler<SkUiLongPressedEventArgs>? LongPressed { add => GestureSet.LongPressed += value; remove => GestureSet.LongPressed -= value; }

    /// <summary>Swipe in one of <see cref="SwipeDirections"/>.</summary>
    public event EventHandler<SkUiSwipedEventArgs>? Swiped { add => GestureSet.Swiped += value; remove => GestureSet.Swiped -= value; }

    /// <summary>Pan along <see cref="PanAxis"/>.</summary>
    public event EventHandler<SkUiPanUpdatedEventArgs>? PanUpdated { add => GestureSet.PanUpdated += value; remove => GestureSet.PanUpdated -= value; }

    /// <summary>Two-pointer pinch / rotate.</summary>
    public event EventHandler<SkUiPinchUpdatedEventArgs>? PinchUpdated { add => GestureSet.PinchUpdated += value; remove => GestureSet.PinchUpdated -= value; }

    /// <summary>Bindable double-tap command.</summary>
    public static readonly BindableProperty DoubleTappedCommandProperty = BindableProperty.Create(nameof(DoubleTappedCommand), typeof(ICommand), typeof(SkUiView), null,
        propertyChanged: (view, _, value) => ((SkUiView)view).GestureSet.DoubleTappedCommand = (ICommand?)value);
    /// <summary>Bindable double-tap command parameter.</summary>
    public static readonly BindableProperty DoubleTappedCommandParameterProperty = BindableProperty.Create(nameof(DoubleTappedCommandParameter), typeof(object), typeof(SkUiView), null,
        propertyChanged: (view, _, value) => ((SkUiView)view).GestureSet.DoubleTappedCommandParameter = value);
    /// <summary>Bindable long-press command.</summary>
    public static readonly BindableProperty LongPressedCommandProperty = BindableProperty.Create(nameof(LongPressedCommand), typeof(ICommand), typeof(SkUiView), null,
        propertyChanged: (view, _, value) => ((SkUiView)view).GestureSet.LongPressedCommand = (ICommand?)value);
    /// <summary>Bindable long-press command parameter.</summary>
    public static readonly BindableProperty LongPressedCommandParameterProperty = BindableProperty.Create(nameof(LongPressedCommandParameter), typeof(object), typeof(SkUiView), null,
        propertyChanged: (view, _, value) => ((SkUiView)view).GestureSet.LongPressedCommandParameter = value);
    /// <summary>Bindable swipe command (parameter defaults to the <see cref="SwipeDirection"/>).</summary>
    public static readonly BindableProperty SwipedCommandProperty = BindableProperty.Create(nameof(SwipedCommand), typeof(ICommand), typeof(SkUiView), null,
        propertyChanged: (view, _, value) => ((SkUiView)view).GestureSet.SwipedCommand = (ICommand?)value);
    /// <summary>Bindable swipe command parameter.</summary>
    public static readonly BindableProperty SwipedCommandParameterProperty = BindableProperty.Create(nameof(SwipedCommandParameter), typeof(object), typeof(SkUiView), null,
        propertyChanged: (view, _, value) => ((SkUiView)view).GestureSet.SwipedCommandParameter = value);
    /// <summary>Bindable swipe directions.</summary>
    public static readonly BindableProperty SwipeDirectionsProperty = BindableProperty.Create(nameof(SwipeDirections), typeof(SwipeDirection), typeof(SkUiView),
        SwipeDirection.Left | SwipeDirection.Right | SwipeDirection.Up | SwipeDirection.Down,
        propertyChanged: (view, _, value) => ((SkUiView)view).GestureSet.SwipeDirections = (SwipeDirection)value);
    /// <summary>Bindable pan axis.</summary>
    public static readonly BindableProperty PanAxisProperty = BindableProperty.Create(nameof(PanAxis), typeof(SkUiPanAxis), typeof(SkUiView), SkUiPanAxis.Both,
        propertyChanged: (view, _, value) => ((SkUiView)view).GestureSet.PanAxis = (SkUiPanAxis)value);

    /// <inheritdoc cref="DoubleTappedCommandProperty" />
    public ICommand? DoubleTappedCommand { get => (ICommand?)GetValue(DoubleTappedCommandProperty); set => SetValue(DoubleTappedCommandProperty, value); }
    /// <inheritdoc cref="DoubleTappedCommandParameterProperty" />
    public object? DoubleTappedCommandParameter { get => GetValue(DoubleTappedCommandParameterProperty); set => SetValue(DoubleTappedCommandParameterProperty, value); }
    /// <inheritdoc cref="LongPressedCommandProperty" />
    public ICommand? LongPressedCommand { get => (ICommand?)GetValue(LongPressedCommandProperty); set => SetValue(LongPressedCommandProperty, value); }
    /// <inheritdoc cref="LongPressedCommandParameterProperty" />
    public object? LongPressedCommandParameter { get => GetValue(LongPressedCommandParameterProperty); set => SetValue(LongPressedCommandParameterProperty, value); }
    /// <inheritdoc cref="SwipedCommandProperty" />
    public ICommand? SwipedCommand { get => (ICommand?)GetValue(SwipedCommandProperty); set => SetValue(SwipedCommandProperty, value); }
    /// <inheritdoc cref="SwipedCommandParameterProperty" />
    public object? SwipedCommandParameter { get => GetValue(SwipedCommandParameterProperty); set => SetValue(SwipedCommandParameterProperty, value); }
    /// <inheritdoc cref="SwipeDirectionsProperty" />
    public SwipeDirection SwipeDirections { get => (SwipeDirection)GetValue(SwipeDirectionsProperty); set => SetValue(SwipeDirectionsProperty, value); }
    /// <inheritdoc cref="PanAxisProperty" />
    public SkUiPanAxis PanAxis { get => (SkUiPanAxis)GetValue(PanAxisProperty); set => SetValue(PanAxisProperty, value); }

    /// <summary>Allows a control to participate in taps without an event subscriber.</summary>
    protected virtual bool HandlesTap => false;

    /// <summary>Raises a classified single tap: the shared <see cref="Tapped"/> event, then <see cref="TappedCommand"/> if eligible.</summary>
    protected virtual void OnTapped(SkUiTappedEventArgs args)
    {
        RaiseTapped(args);
        ExecuteTappedCommand();
    }

    /// <summary>
    /// Raises the shared <see cref="Tapped"/> event, then the single-tap MAUI <see cref="TapGestureRecognizer"/>s in
    /// <see cref="View.GestureRecognizers"/>, without executing <see cref="TappedCommand"/>.
    /// </summary>
    protected void RaiseTapped(SkUiTappedEventArgs args)
    {
        Tapped?.Invoke(this, args);
        RaiseMauiTaps(args, 1);
    }

    /// <summary>
    /// Executes <see cref="TappedCommand"/> if eligible. Separated from <see cref="RaiseTapped"/> so controls with their
    /// own command (e.g. <see cref="SkUiButton"/>) can raise the shared event without also firing this generic command,
    /// which would otherwise run alongside their dedicated command on the same tap.
    /// </summary>
    protected void ExecuteTappedCommand()
    {
        if (_tappedCommand?.CanExecute(_tappedCommandParameter) == true)
            _tappedCommand.Execute(_tappedCommandParameter);
    }
}

/// <summary>Composite-time properties that <see cref="SkUiView.AnimateAsync"/> animates on the render thread.</summary>
public enum SkUiAnimatableProperty
{
    /// <summary><see cref="VisualElement.Opacity"/>.</summary>
    Opacity,
    /// <summary><see cref="VisualElement.TranslationX"/>.</summary>
    TranslationX,
    /// <summary><see cref="VisualElement.TranslationY"/>.</summary>
    TranslationY,
    /// <summary><see cref="VisualElement.Rotation"/> in degrees.</summary>
    Rotation,
    /// <summary><see cref="VisualElement.Scale"/> (both axes).</summary>
    Scale,
    /// <summary><see cref="VisualElement.ScaleX"/>.</summary>
    ScaleX,
    /// <summary><see cref="VisualElement.ScaleY"/>.</summary>
    ScaleY
}
