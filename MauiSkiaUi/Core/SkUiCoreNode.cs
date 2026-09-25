using System.ComponentModel;
using System.Runtime.CompilerServices;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Base Core node: DIP layout, paint, and touch with no MAUI control overhead.
/// Implements <see cref="INotifyPropertyChanged"/>; fluent <c>Set*</c> methods are the single apply path
/// and raise notifications via <see cref="SetProperty{T}"/>.
/// </summary>
public class SkUiCoreNode : ISkUiCoreNode, INotifyPropertyChanged, ISkUiRenderable
{
    private bool _measureDirty = true;
    private bool _arrangeDirty = true;
    private Size _lastConstraint;
    private Size _desiredSize;
    private Rect _frame;
    private Rect _lastArrangeBounds;
    private double _opacity = 1;
    private double _translationX;
    private double _translationY;
    private double _rotation;
    private double _scale = 1;
    private bool _clipToBounds = true;
    private SkUiRenderDirty _renderPending;
    private SkUiRenderState? _renderState;
    private ISkUiCoreNode? _parent;
    private bool _isVisible = true;
    private double _width = double.NaN;
    private double _height = double.NaN;
    private double _minimumWidth;
    private double _minimumHeight;
    private double _maximumWidth = double.PositiveInfinity;
    private double _maximumHeight = double.PositiveInfinity;
    private Thickness _margin;
    private LayoutAlignment _horizontalAlignment = LayoutAlignment.Fill;
    private LayoutAlignment _verticalAlignment = LayoutAlignment.Fill;
    private int _updateDepth;
    private bool _paintPending;
    private bool _layoutPending;
    private SkUiAnimationClock? _rootClock;
    private SkUiAnimationClock? _ownedClock;

    /// <inheritdoc />
    public ISkUiCoreNode? Parent => _parent;

    /// <inheritdoc />
    public Rect Frame => _frame;

    /// <inheritdoc />
    public Size DesiredSize => _desiredSize;

    /// <inheritdoc cref="ISkUiCoreNode.IsVisible" />
    public bool IsVisible
    {
        get => _isVisible;
        set => SetIsVisible(value);
    }

    /// <summary>Explicit width in DIPs, or <see cref="double.NaN"/> for unconstrained.</summary>
    public double Width
    {
        get => _width;
        set => SetWidth(value);
    }

    /// <summary>Explicit height in DIPs, or <see cref="double.NaN"/> for unconstrained.</summary>
    public double Height
    {
        get => _height;
        set => SetHeight(value);
    }

    /// <summary>Minimum width in DIPs.</summary>
    public double MinimumWidth
    {
        get => _minimumWidth;
        set => SetMinimumWidth(value);
    }

    /// <summary>Minimum height in DIPs.</summary>
    public double MinimumHeight
    {
        get => _minimumHeight;
        set => SetMinimumHeight(value);
    }

    /// <summary>Maximum width in DIPs.</summary>
    public double MaximumWidth
    {
        get => _maximumWidth;
        set => SetMaximumWidth(value);
    }

    /// <summary>Maximum height in DIPs.</summary>
    public double MaximumHeight
    {
        get => _maximumHeight;
        set => SetMaximumHeight(value);
    }

    /// <summary>Outer margin in DIPs applied during measure/arrange.</summary>
    public Thickness Margin
    {
        get => _margin;
        set => SetMargin(value);
    }

    /// <summary>
    /// Horizontal alignment within the arrange slot given by a parent layout (same idea as MAUI
    /// <c>HorizontalOptions</c> / <c>ComputeFrame</c>). Default is <see cref="LayoutAlignment.Fill"/>.
    /// </summary>
    public LayoutAlignment HorizontalAlignment
    {
        get => _horizontalAlignment;
        set => SetHorizontalAlignment(value);
    }

    /// <summary>
    /// Vertical alignment within the arrange slot given by a parent layout (same idea as MAUI
    /// <c>VerticalOptions</c> / <c>ComputeFrame</c>). Default is <see cref="LayoutAlignment.Fill"/>.
    /// </summary>
    public LayoutAlignment VerticalAlignment
    {
        get => _verticalAlignment;
        set => SetVerticalAlignment(value);
    }

    /// <summary>Raised when this node's own content must be re-recorded, and on a render root once per frame.</summary>
    public event EventHandler? PaintInvalidated;

    /// <summary>Opacity in [0, 1], applied at composite time (no re-record).</summary>
    public double Opacity { get => _opacity; set => SetOpacity(value); }

    /// <summary>Render-time horizontal translation in DIPs (no layout).</summary>
    public double TranslationX { get => _translationX; set => SetTranslationX(value); }

    /// <summary>Render-time vertical translation in DIPs (no layout).</summary>
    public double TranslationY { get => _translationY; set => SetTranslationY(value); }

    /// <summary>Render-time rotation in degrees about the center.</summary>
    public double Rotation { get => _rotation; set => SetRotation(value); }

    /// <summary>Render-time uniform scale about the center.</summary>
    public double Scale { get => _scale; set => SetScale(value); }

    /// <summary>
    /// Clips own content and children to the arranged rectangle. Defaults to <c>true</c> for leaves and
    /// <c>false</c> for panels / content views, so children may overflow (shadows, press scale).
    /// </summary>
    public bool ClipToBounds { get => _clipToBounds; set => SetClipToBounds(value); }

    /// <summary>Sets <see cref="Opacity"/>.</summary>
    public SkUiCoreNode SetOpacity(double value)
    {
        value = double.IsNaN(value) ? 1 : Math.Clamp(value, 0, 1);
        if (SetProperty(ref _opacity, value, nameof(Opacity))) InvalidateRender(SkUiRenderDirty.Props);
        return this;
    }

    /// <summary>Sets <see cref="TranslationX"/>.</summary>
    public SkUiCoreNode SetTranslationX(double value)
    {
        if (SetProperty(ref _translationX, value, nameof(TranslationX))) InvalidateRender(SkUiRenderDirty.Props);
        return this;
    }

    /// <summary>Sets <see cref="TranslationY"/>.</summary>
    public SkUiCoreNode SetTranslationY(double value)
    {
        if (SetProperty(ref _translationY, value, nameof(TranslationY))) InvalidateRender(SkUiRenderDirty.Props);
        return this;
    }

    /// <summary>Sets <see cref="Rotation"/>.</summary>
    public SkUiCoreNode SetRotation(double value)
    {
        if (SetProperty(ref _rotation, value, nameof(Rotation))) InvalidateRender(SkUiRenderDirty.Props);
        return this;
    }

    /// <summary>Sets <see cref="Scale"/>.</summary>
    public SkUiCoreNode SetScale(double value)
    {
        if (SetProperty(ref _scale, value, nameof(Scale))) InvalidateRender(SkUiRenderDirty.Props);
        return this;
    }

    /// <summary>Sets <see cref="ClipToBounds"/>.</summary>
    public SkUiCoreNode SetClipToBounds(bool value)
    {
        if (SetProperty(ref _clipToBounds, value, nameof(ClipToBounds))) InvalidateRender(SkUiRenderDirty.Props);
        return this;
    }

    /// <summary>
    /// Animates <see cref="Opacity"/>, translation, <see cref="Rotation"/> or <see cref="Scale"/> on the render thread;
    /// the property is updated to the final (or last shown, if cancelled) value when it ends.
    /// </summary>
    public Task<bool> AnimateAsync(SkUiAnimatableProperty property, double to, uint length = 250, Easing? easing = null)
    {
        if (!double.IsFinite(to)) throw new ArgumentOutOfRangeException(nameof(to));
        SkUiRenderProperty[] properties = property switch
        {
            SkUiAnimatableProperty.Opacity => [SkUiRenderProperty.Opacity],
            SkUiAnimatableProperty.TranslationX => [SkUiRenderProperty.TranslationX],
            SkUiAnimatableProperty.TranslationY => [SkUiRenderProperty.TranslationY],
            SkUiAnimatableProperty.Rotation => [SkUiRenderProperty.Rotation],
            SkUiAnimatableProperty.Scale => [SkUiRenderProperty.ScaleX, SkUiRenderProperty.ScaleY],
            _ => throw new ArgumentOutOfRangeException(nameof(property), "Core nodes support uniform Scale only.")
        };
        var target = (float)(property == SkUiAnimatableProperty.Opacity ? Math.Clamp(to, 0, 1) : to);
        var targets = properties.Length == 2 ? new[] { target, target } : new[] { target };
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tween = new SkUiRenderTween(properties, targets, TimeSpan.FromMilliseconds(Math.Max(1, length)), easing)
        {
            Finished = (animation, completed) =>
            {
                RenderState.ActiveAnimations?.Remove(animation);
                if (!animation.SupersededByUi && ((SkUiRenderTween)animation).LastValues is { } values)
                {
                    for (var index = 0; index < values.Length; index++)
                        RenderState.Acknowledge(properties[index], values[index]);
                    switch (property)
                    {
                        case SkUiAnimatableProperty.Opacity: SetOpacity(values[0]); break;
                        case SkUiAnimatableProperty.TranslationX: SetTranslationX(values[0]); break;
                        case SkUiAnimatableProperty.TranslationY: SetTranslationY(values[0]); break;
                        case SkUiAnimatableProperty.Rotation: SetRotation(values[0]); break;
                        case SkUiAnimatableProperty.Scale: SetScale(values[0]); break;
                    }
                }
                completion.TrySetResult(completed);
            }
        };
        SkUiRenderInvalidation.Enqueue(this, tween);
        return completion.Task;
    }

    /// <summary>Raised when measure/arrange must run again for this subtree.</summary>
    public event EventHandler? MeasureInvalidated;

    /// <summary>
    /// Shared animation clock: walks to a host-bound root clock when present; otherwise a locally owned clock.
    /// <see cref="SkUiCoreHost"/> binds its <see cref="SkUiView.AnimationClock"/> onto the Core root.
    /// </summary>
    public SkUiAnimationClock AnimationClock
    {
        get
        {
            for (SkUiCoreNode? node = this; node is not null; node = node._parent as SkUiCoreNode)
            {
                if (node._rootClock is { } root)
                    return root;
            }
            return _ownedClock ??= new SkUiAnimationClock();
        }
    }

    /// <summary>Called when the inherited animation root changes (host reparent / clock rebind).</summary>
    /// <param name="subtreeDetached">
    /// <c>true</c> when this node (or an ancestor) left a host tree and no longer inherits a host clock.
    /// </param>
    protected virtual void OnAnimationRootChanged(bool subtreeDetached = false) { }

    /// <summary>Binds the host surface clock onto a Core tree root (cleared when the host detaches content).</summary>
    internal void BindAnimationClock(SkUiAnimationClock? clock)
    {
        if (ReferenceEquals(_rootClock, clock)) return;
        _rootClock = clock;
        var detached = clock is null && !HasInheritedHostClock;
        OnAnimationRootChanged(detached);
        PropagateAnimationRootChanged(detached);
    }

    /// <summary>Whether this node or an ancestor currently holds a host-bound animation clock.</summary>
    internal bool HasInheritedHostClock
    {
        get
        {
            for (SkUiCoreNode? node = this; node is not null; node = node._parent as SkUiCoreNode)
            {
                if (node._rootClock is not null)
                    return true;
            }
            return false;
        }
    }

    /// <summary>Host that exclusively owns this node as a Core tree root, if any.</summary>
    internal SkUiCoreHost? HostOwner { get; set; }

    /// <summary>Notifies descendants that <see cref="AnimationClock"/> may have changed.</summary>
    private void PropagateAnimationRootChanged(bool subtreeDetached)
    {
        if (this is SkUiCorePanel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is SkUiCoreNode coreChild)
                {
                    coreChild.OnAnimationRootChanged(subtreeDetached);
                    coreChild.PropagateAnimationRootChanged(subtreeDetached);
                }
            }
        }
        else if (this is SkUiCoreContentView { Content: SkUiCoreNode content })
        {
            content.OnAnimationRootChanged(subtreeDetached);
            content.PropagateAnimationRootChanged(subtreeDetached);
        }
    }

    /// <summary>
    /// Returns <c>true</c> when attaching <paramref name="child"/> under this node would create a cycle
    /// (child is this node or an ancestor of this node).
    /// </summary>
    internal bool WouldCreateParentCycle(ISkUiCoreNode child)
    {
        if (ReferenceEquals(child, this)) return true;
        for (ISkUiCoreNode? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, child))
                return true;
        }
        return false;
    }

    /// <summary>Attaches this node under <paramref name="parent"/>; the previous parent must be cleared first.</summary>
    internal void AttachTo(ISkUiCoreNode? parent)
    {
        if (_parent is not null && parent is not null && !ReferenceEquals(_parent, parent))
            throw new InvalidOperationException("A Core node already has a parent.");
        SetProperty(ref _parent, parent, nameof(Parent));
        if (parent is null && _renderState is not null)
            SkUiRenderInvalidation.ResetSubtree(this);
        var detached = parent is null && !HasInheritedHostClock;
        OnAnimationRootChanged(detached);
        PropagateAnimationRootChanged(detached);
    }

    /// <summary>Sets visibility; raises <see cref="System.ComponentModel.INotifyPropertyChanged"/> when changed.</summary>
    public SkUiCoreNode SetIsVisible(bool value)
    {
        if (!SetProperty(ref _isVisible, value, nameof(IsVisible))) return this;
        OnIsVisibleChanged();
        InvalidateMeasure();
        return this;
    }

    /// <summary>Called after <see cref="IsVisible"/> changes.</summary>
    protected virtual void OnIsVisibleChanged() { }

    /// <summary>Sets an explicit width (<see cref="double.NaN"/> clears it).</summary>
    public SkUiCoreNode SetWidth(double value)
    {
        if (!SetProperty(ref _width, value, nameof(Width))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets an explicit height (<see cref="double.NaN"/> clears it).</summary>
    public SkUiCoreNode SetHeight(double value)
    {
        if (!SetProperty(ref _height, value, nameof(Height))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets minimum width in DIPs.</summary>
    public SkUiCoreNode SetMinimumWidth(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _minimumWidth, value, nameof(MinimumWidth))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets minimum height in DIPs.</summary>
    public virtual SkUiCoreNode SetMinimumHeight(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _minimumHeight, value, nameof(MinimumHeight))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets maximum width in DIPs.</summary>
    public SkUiCoreNode SetMaximumWidth(double value)
    {
        if (value < 0 || double.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (!SetProperty(ref _maximumWidth, value, nameof(MaximumWidth))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets maximum height in DIPs.</summary>
    public SkUiCoreNode SetMaximumHeight(double value)
    {
        if (value < 0 || double.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (!SetProperty(ref _maximumHeight, value, nameof(MaximumHeight))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets outer margin in DIPs.</summary>
    public SkUiCoreNode SetMargin(Thickness value)
    {
        if (!SetProperty(ref _margin, value, nameof(Margin))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets horizontal alignment within the parent's arrange slot.</summary>
    public SkUiCoreNode SetHorizontalAlignment(LayoutAlignment value)
    {
        if (!SetProperty(ref _horizontalAlignment, value, nameof(HorizontalAlignment))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets vertical alignment within the parent's arrange slot.</summary>
    public SkUiCoreNode SetVerticalAlignment(LayoutAlignment value)
    {
        if (!SetProperty(ref _verticalAlignment, value, nameof(VerticalAlignment))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Coalesces layout and paint notifications until the matching <see cref="EndUpdating"/>.</summary>
    public void StartUpdating() => _updateDepth++;

    /// <summary>Ends an update batch and propagates at most one invalidation.</summary>
    public void EndUpdating()
    {
        if (_updateDepth == 0)
            throw new InvalidOperationException("No update batch is active.");
        if (--_updateDepth == 0)
            FlushInvalidation();
    }

    /// <inheritdoc />
    public void InvalidateMeasure()
    {
        _measureDirty = true;
        _arrangeDirty = true;
        _layoutPending = true;
        InvalidatePaint();
    }

    /// <inheritdoc />
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
        var layout = _layoutPending;
        var paint = _paintPending;
        var render = _renderPending;
        _layoutPending = _paintPending = false;
        _renderPending = SkUiRenderDirty.None;
        if (layout)
        {
            if (_parent is SkUiCoreNode coreParent)
                coreParent.InvalidateMeasure();
            else
                MeasureInvalidated?.Invoke(this, EventArgs.Empty);
        }
        if (paint)
            render |= SkUiRenderDirty.Content;
        if (render != SkUiRenderDirty.None)
            SkUiRenderInvalidation.Mark(this, render);
        if (paint)
            PaintInvalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Size Measure(double widthConstraint, double heightConstraint)
    {
        var constraint = new Size(widthConstraint, heightConstraint);
        if (!_measureDirty && constraint == _lastConstraint)
            return _desiredSize;

        Size desired;
        if (!_isVisible)
        {
            desired = Size.Zero;
            _lastConstraint = constraint;
            _measureDirty = false;
            _arrangeDirty = true;
        }
        else
        {
            var width = Math.Max(0, widthConstraint - _margin.HorizontalThickness);
            var height = Math.Max(0, heightConstraint - _margin.VerticalThickness);
            if (!double.IsNaN(_width)) width = Math.Min(width, _width);
            if (!double.IsNaN(_height)) height = Math.Min(height, _height);
            width = Math.Min(width, _maximumWidth);
            height = Math.Min(height, _maximumHeight);

            var content = MeasureContent(width, height);
            var desiredWidth = ResolveDimension(widthConstraint, _width, content.Width, _minimumWidth, _maximumWidth) + _margin.HorizontalThickness;
            var desiredHeight = ResolveDimension(heightConstraint, _height, content.Height, _minimumHeight, _maximumHeight) + _margin.VerticalThickness;
            desired = new Size(desiredWidth, desiredHeight);
            _lastConstraint = constraint;
            _measureDirty = false;
            _arrangeDirty = true;
        }

        if (_desiredSize != desired)
        {
            _desiredSize = desired;
            OnPropertyChanged(nameof(DesiredSize));
        }

        return _desiredSize;
    }

    /// <summary>Measures intrinsic content without margin, in DIPs.</summary>
    protected virtual Size MeasureContent(double widthConstraint, double heightConstraint) => Size.Zero;

    /// <inheritdoc />
    public void Arrange(Rect bounds)
    {
        // Compare against the incoming slot (not the margin-deflated frame) so clean nodes skip re-arrange.
        if (!_arrangeDirty && bounds == _lastArrangeBounds)
            return;
        _lastArrangeBounds = bounds;

        var x = bounds.X + _margin.Left;
        var y = bounds.Y + _margin.Top;
        var width = Math.Max(0, bounds.Width - _margin.HorizontalThickness);
        var height = Math.Max(0, bounds.Height - _margin.VerticalThickness);
        if (!double.IsNaN(_width)) width = Math.Min(width, _width);
        if (!double.IsNaN(_height)) height = Math.Min(height, _height);
        var frame = new Rect(x, y, width, height);
        if (_frame != frame)
        {
            _frame = frame;
            OnPropertyChanged(nameof(Frame));
            // Offset-only changes are composite-time; the recorder re-records content only on size change.
            InvalidateRender(SkUiRenderDirty.Props);
        }
        ArrangeContent(_frame.Size);
        _arrangeDirty = false;
    }

    /// <summary>Arranges hosted content in this node's local coordinate system.</summary>
    protected virtual void ArrangeContent(Size size) { }

    /// <inheritdoc />
    /// <remarks>Immediate-mode paint of this subtree in local coordinates; surfaces use the retained compositor.</remarks>
    public void Paint(SKCanvas canvas) => SkUiImmediatePainter.Paint(this, canvas, applyOffset: false);

    internal SkUiRenderState RenderState => _renderState ??= new SkUiRenderState();

    SkUiRenderState ISkUiRenderable.RenderState => RenderState;

    ISkUiRenderable? ISkUiRenderable.RenderParent => (ISkUiRenderable?)(_parent as SkUiCoreNode) ?? HostOwner;

    void ISkUiRenderable.GetRenderProps(ref SkUiRenderProps props)
    {
        props.X = (float)_frame.X;
        props.Y = (float)_frame.Y;
        props.Width = (float)_frame.Width;
        props.Height = (float)_frame.Height;
        props.TranslationX = (float)_translationX;
        props.TranslationY = (float)_translationY;
        props.Rotation = (float)_rotation;
        props.ScaleX = props.ScaleY = (float)_scale;
        props.Opacity = (float)_opacity;
        props.IsVisible = _isVisible;
        props.ClipToBounds = _clipToBounds;
        OnGetRenderProps(ref props);
    }

    /// <summary>Sets the <see cref="ClipToBounds"/> default without notifications (constructors only).</summary>
    internal void InitClipToBounds(bool value) => _clipToBounds = value;

    /// <summary>Lets containers add children clip / offset, spinning content, or ink overflow.</summary>
    internal virtual void OnGetRenderProps(ref SkUiRenderProps props) { }

    void ISkUiRenderable.RecordContent(SKCanvas canvas)
    {
        _paintBackground?.Invoke(canvas);
        OnPaintContent(canvas);
    }

    bool ISkUiRenderable.HasOverlay => _paintOverlay is not null;

    void ISkUiRenderable.RecordOverlay(SKCanvas canvas) => _paintOverlay?.Invoke(canvas);

    void ISkUiRenderable.GetRenderChildren(List<ISkUiRenderable> children) => AddRenderChildren(children);

    /// <summary>Appends drawn children in paint order.</summary>
    internal virtual void AddRenderChildren(List<ISkUiRenderable> children) { }

    void ISkUiRenderable.OnRenderRootDirty(bool fromDescendant)
    {
        if (fromDescendant)
            PaintInvalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Render transform (translation, rotation, scale about the center) excluding the frame offset.</summary>
    internal SKMatrix RenderMatrix
    {
        get
        {
            if (_translationX == 0 && _translationY == 0 && _rotation == 0 && _scale == 1)
                return SKMatrix.Identity;
            var anchorX = (float)(_frame.Width / 2);
            var anchorY = (float)(_frame.Height / 2);
            return SKMatrix.CreateTranslation((float)_translationX + anchorX, (float)_translationY + anchorY)
                .PreConcat(SKMatrix.CreateRotationDegrees((float)_rotation))
                .PreConcat(SKMatrix.CreateScale((float)_scale, (float)_scale))
                .PreConcat(SKMatrix.CreateTranslation(-anchorX, -anchorY));
        }
    }

    /// <summary>Maps a point in the parent's children space into this node's local space (inverse transform).</summary>
    internal static bool TryMapFromParent(ISkUiCoreNode node, Point parentPoint, out Point local)
    {
        var point = new SKPoint((float)(parentPoint.X - node.Frame.X), (float)(parentPoint.Y - node.Frame.Y));
        if (node is SkUiCoreNode core)
        {
            var matrix = core.RenderMatrix;
            if (!matrix.IsIdentity)
            {
                if (!matrix.TryInvert(out var inverse))
                {
                    local = default;
                    return false;
                }
                point = inverse.MapPoint(point);
            }
        }
        local = new Point(point.X, point.Y);
        return true;
    }

    private Action<SKCanvas>? _paintBackground;
    private Action<SKCanvas>? _paintOverlay;

    /// <summary>
    /// Background-layer painter. When unset, the Background phase is a no-op.
    /// Prefer this over subclassing for chrome customization.
    /// </summary>
    public Action<SKCanvas>? PaintBackground
    {
        get => _paintBackground;
        set => SetPaintBackground(value);
    }

    /// <summary>
    /// Overlay-layer painter drawn after <see cref="OnPaintContent"/>. <c>null</c> means no overlay.
    /// </summary>
    public Action<SKCanvas>? PaintOverlay
    {
        get => _paintOverlay;
        set => SetPaintOverlay(value);
    }

    /// <summary>Sets the Background painter; <c>null</c> draws no background.</summary>
    public SkUiCoreNode SetPaintBackground(Action<SKCanvas>? value)
    {
        if (!SetProperty(ref _paintBackground, value, nameof(PaintBackground))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the Overlay painter; <c>null</c> draws no overlay.</summary>
    public SkUiCoreNode SetPaintOverlay(Action<SKCanvas>? value)
    {
        if (!SetProperty(ref _paintOverlay, value, nameof(PaintOverlay))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Content paint phase (structural). Always virtual — not replaceable by a delegate.</summary>
    protected virtual void OnPaintContent(SKCanvas canvas) { }

    /// <inheritdoc />
    public virtual bool Touch(SkUiTouchEvent touch) => false;

    /// <summary>Converts a MAUI graphics color to Skia.</summary>
    protected static SKColor ToSkColor(Color color) => new(
        (byte)(color.Red * 255), (byte)(color.Green * 255),
        (byte)(color.Blue * 255), (byte)(color.Alpha * 255));

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Updates <paramref name="field"/> when the value changes and raises <see cref="PropertyChanged"/>.
    /// Returns <c>false</c> when the value is unchanged (no notification).
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Raises <see cref="PropertyChanged"/> for <paramref name="propertyName"/>.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static double ResolveDimension(double constraint, double explicitSize, double content, double minimum, double maximum)
    {
        var value = !double.IsNaN(explicitSize) ? explicitSize : content;
        if (!double.IsInfinity(constraint))
            value = Math.Min(value, constraint);
        value = Math.Max(value, minimum);
        value = Math.Min(value, maximum);
        return Math.Max(0, value);
    }
}
