using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Maui.Layouts;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>Base for Skia-drawn views, with layout that does not require a handler.</summary>
public class SkUiView : View, ISkUiView, ISkUiRenderable, ISkUiGestureElement
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
    private SkUiRenderDirty _renderPending;
    private SkUiRenderState? _renderState;
#if SKUI_DIAGNOSTICS
    private int _diagnosticRecordFrameCount;
    private double _diagnosticRecordFrameTotalMs;
#endif
    private SkUiGestureSet? _gestures;
    private SkUiPointerRouter? _router;
    private SkUiTapGestureRecognizer? _tap;
    private SkUiAnimationClock? _animationClock;
    private ICommand? _tappedCommand;
    private object? _tappedCommandParameter;

    /// <summary>Bindable opt-in tap command.</summary>
    public static readonly BindableProperty TappedCommandProperty = BindableProperty.Create(
        nameof(TappedCommand), typeof(ICommand), typeof(SkUiView), null,
        propertyChanged: (view, _, value) => ((SkUiView)view).SetTappedCommand((ICommand?)value));
    /// <summary>Bindable tap command parameter.</summary>
    public static readonly BindableProperty TappedCommandParameterProperty = BindableProperty.Create(
        nameof(TappedCommandParameter), typeof(object), typeof(SkUiView), null,
        propertyChanged: (view, _, value) => ((SkUiView)view).SetTappedCommandParameter(value));
    /// <summary>Optional command; passive nodes participate when it can execute.</summary>
    public ICommand? TappedCommand { get => _tappedCommand; set => SetValue(TappedCommandProperty, value); }
    /// <summary>Parameter supplied to the tap command.</summary>
    public object? TappedCommandParameter { get => _tappedCommandParameter; set => SetValue(TappedCommandParameterProperty, value); }
    /// <summary>Sets the tap command without bindable write-back.</summary>
    public SkUiView SetTappedCommand(ICommand? value) { _tappedCommand = value; return this; }
    /// <summary>Sets the tap parameter without bindable write-back.</summary>
    public SkUiView SetTappedCommandParameter(object? value) { _tappedCommandParameter = value; return this; }
    /// <summary>Whether an eligible captured pointer is currently pressed inside this node.</summary>
    public bool IsPressed { get; private set; }

    private void SetPressed(bool value)
    {
        if (IsPressed == value) return;
        IsPressed = value;
        OnPropertyChanged(nameof(IsPressed));
        OnPressedChanged();
        InvalidatePaint();
    }

    /// <summary>Updates intrinsic control feedback when the shared pointer state changes.</summary>
    protected virtual void OnPressedChanged() { }

    /// <summary>Whether an intrinsic tap action is currently enabled.</summary>
    protected virtual bool CanReceiveTap => true;

    /// <summary>
    /// Raised when this node's own content must be re-recorded (<see cref="InvalidatePaint"/>), and on the
    /// surface root once per frame when anything below it changed.
    /// </summary>
    public event EventHandler? PaintInvalidated;

    /// <summary>Raised on a render root when its subtree needs a new frame (first change per frame only).</summary>
    internal event EventHandler? RenderRootDirty;

    /// <summary>
    /// Clips this node's content, children and overlay to its arranged rectangle. Defaults to <c>true</c> for
    /// leaf controls and <c>false</c> for layouts / content hosts (MAUI <c>Layout.IsClippedToBounds</c> parity),
    /// so children can overflow (e.g. shadows, press scale). Scroll viewports always clip their children.
    /// </summary>
    public static readonly BindableProperty ClipToBoundsProperty = BindableProperty.Create(
        nameof(ClipToBounds), typeof(bool), typeof(SkUiView), true,
        defaultValueCreator: view => view is not (SkUiLayout or SkUiContentView or Core.SkUiCoreHost));

    /// <inheritdoc cref="ClipToBoundsProperty" />
    public bool ClipToBounds { get => (bool)GetValue(ClipToBoundsProperty); set => SetValue(ClipToBoundsProperty, value); }

    /// <summary>Opts this node into single taps. MAUI GestureRecognizers are not used.</summary>
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
        // Skia parent appears, abandon it so descendants re-register on the shared ancestor clock.
        if (SkiaParent is not null && _animationClock is not null)
        {
            _animationClock.StopAll();
            _animationClock = null;
        }
        // When this node is detached, descendants still have it as Parent — pass subtreeDetached so
        // they stop clocks instead of rebinding onto an orphan mid-tree clock.
        NotifyAnimationRootChanged(subtreeDetached: Parent is null);
        // Detached from a drawn parent: its retained pictures are released by the compositor, so a later
        // attach must record this subtree from scratch.
        if (SkiaParent is null && _renderState is not null)
            SkUiRenderInvalidation.ResetSubtree(this);
        // Pointers captured by a detached subtree must not complete (taps, presses) later.
        SkUiGestureSet.CancelSubtree(this);
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
            return Frame.Size;
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
        var frameChanged = _lastArrangeBounds != bounds || previousFrame != Frame;
        _lastArrangeBounds = bounds;
        _arrangeDirty = false;
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
        InvalidatePaint();
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
        var invalidatePaint = _paintPending;
        var render = _renderPending;
        _layoutPending = _paintPending = false;
        _renderPending = SkUiRenderDirty.None;
        if (invalidateLayout)
        {
            if (SkiaParent is { } parent)
                ((IView)parent).InvalidateMeasure();
            else
                base.InvalidateMeasureOverride();
        }
        if (invalidatePaint)
            render |= SkUiRenderDirty.Content;
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
        }
        if (propertyName == nameof(IsVisible))
            InvalidateMeasureOverride();
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

    bool ISkUiRenderable.HasOverlay => _paintOverlay is not null || PaintOverrides.For(GetType()).Overlay;

    void ISkUiRenderable.RecordOverlay(SKCanvas canvas)
    {
        if (_paintOverlay is { } paintOverlay)
            paintOverlay(canvas);
        else
            OnPaintOverlay(canvas);
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

    [ThreadStatic] private static SKPaint? t_fillPaint;
    private Action<SKCanvas>? _paintBackground;
    private Action<SKCanvas>? _paintOverlay;

    /// <summary>
    /// Background-layer painter. When set, replaces the default solid <see cref="VisualElement.Background"/> /
    /// <see cref="VisualElement.BackgroundColor"/> fill. Clear to restore that default.
    /// Prefer this over subclassing for chrome customization.
    /// </summary>
    public Action<SKCanvas>? PaintBackground
    {
        get => _paintBackground;
        set => SetPaintBackground(value);
    }

    /// <summary>
    /// Overlay-layer painter drawn after <see cref="OnPaintContent"/>. <c>null</c> means no overlay
    /// unless a subclass overrides <see cref="OnPaintOverlay"/>.
    /// Prefer this over subclassing for badges, press tints, and similar chrome.
    /// </summary>
    public Action<SKCanvas>? PaintOverlay
    {
        get => _paintOverlay;
        set => SetPaintOverlay(value);
    }

    /// <summary>Sets the Background painter; <c>null</c> restores the default solid fill via <see cref="OnPaintBackground"/>.</summary>
    public SkUiView SetPaintBackground(Action<SKCanvas>? value)
    {
        if (ReferenceEquals(_paintBackground, value)) return this;
        _paintBackground = value;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the Overlay painter; <c>null</c> falls back to <see cref="OnPaintOverlay"/>.</summary>
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
    /// Non-solid brushes are ignored in v1 and fall through to <see cref="VisualElement.BackgroundColor"/>.
    /// </summary>
    protected Color? ResolveSolidBackgroundColor()
    {
        if (Background is SolidColorBrush brush && brush.Color is not null)
            return brush.Color;
        return BackgroundColor;
    }

    /// <summary>
    /// Color used to clear the platform/backing surface before painting a frame.
    /// Uses the root solid background when set; otherwise transparent so rounded or
    /// translucent roots can show host content underneath (FR-8 / FR-11).
    /// iOS HW replaces the drawable via a full-frame <c>Src</c> blit, so transparent
    /// clear still erases prior-frame ghosts without forcing an opaque white backing.
    /// </summary>
    internal SKColor SurfaceClearColor =>
        ResolveSolidBackgroundColor() is { } color ? ToSkColor(color) : SKColors.Transparent;

    /// <summary>
    /// Default Background when <see cref="PaintBackground"/> is unset. Solid MAUI fill only; other brush types are deferred.
    /// Subclasses may call this from a custom <see cref="PaintBackground"/> painter.
    /// </summary>
    protected void PaintDefaultBackground(SKCanvas canvas)
    {
        var color = ResolveSolidBackgroundColor();
        if (color is null)
            return;
        var paint = t_fillPaint ??= new SKPaint();
        paint.Color = ToSkColor(color);
        canvas.DrawRect(0, 0, (float)Width, (float)Height, paint);
    }

    /// <summary>
    /// Background paint phase when <see cref="PaintBackground"/> is unset.
    /// Prefer <see cref="PaintBackground"/> for new code; override this for subclass chrome that must stay virtual.
    /// </summary>
    protected virtual void OnPaintBackground(SKCanvas canvas) => PaintDefaultBackground(canvas);

    /// <summary>Content paint phase (structural: glyphs, children, hosted content). Always virtual — not replaceable by a delegate.</summary>
    protected virtual void OnPaintContent(SKCanvas canvas) { }

    /// <summary>
    /// Overlay paint phase when <see cref="PaintOverlay"/> is unset (no-op by default).
    /// Prefer <see cref="PaintOverlay"/> for new code; override this for subclass chrome that must stay virtual.
    /// </summary>
    protected virtual void OnPaintOverlay(SKCanvas canvas) { }

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
        if (WantsSingleTap || _gestures?.WantsDoubleTap == true)
            recognizers.Add(_tap ??= new SkUiTapGestureRecognizer
            {
                TapHandler = args => { if (WantsSingleTap) OnTapped(args); },
                WantsDoubleTap = () => _gestures?.WantsDoubleTap == true,
                DoubleTapHandler = args => _gestures?.RaiseDoubleTapped(args),
                PressedHandler = SetPressed
            });
        _gestures?.Collect(recognizers);
    }

    private bool WantsSingleTap => Tapped is not null || HandlesTap || (_tappedCommand?.CanExecute(_tappedCommandParameter) ?? false);

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

    /// <summary>Raises the shared <see cref="Tapped"/> event only, without executing <see cref="TappedCommand"/>.</summary>
    protected void RaiseTapped(SkUiTappedEventArgs args) => Tapped?.Invoke(this, args);

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

/// <summary>Caches which paint phases a type overrides, so empty phases skip recording.</summary>
internal static class PaintOverrides
{
    private static readonly ConcurrentDictionary<Type, (bool Overlay, bool Content)> Cache = new();

    internal static (bool Overlay, bool Content) For(Type type) => Cache.GetOrAdd(type, static t =>
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        bool Overrides(string name, Type baseType) =>
            t.GetMethod(name, flags, [typeof(SKCanvas)])?.DeclaringType is { } declaring && declaring != baseType;
        var baseType = typeof(SkUiView).IsAssignableFrom(t) ? typeof(SkUiView) : typeof(Core.SkUiCoreNode);
        return (Overrides("OnPaintOverlay", baseType), Overrides("OnPaintContent", baseType));
    });
}
