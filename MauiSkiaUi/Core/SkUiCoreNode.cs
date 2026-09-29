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
public class SkUiCoreNode : ISkUiCoreNode, INotifyPropertyChanged, ISkUiRenderable, ISkUiGestureElement, ISkUiTransitionHost
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
    private FlowDirection _flowDirection = FlowDirection.MatchParent;
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

    /// <summary>
    /// Layout direction. <see cref="Microsoft.Maui.FlowDirection.MatchParent"/> (default) inherits from the Core parent, and at
    /// the Core root from the hosting <see cref="SkUiCoreHost"/> (MAUI <c>FlowDirection</c>). Right-to-left mirrors child
    /// frames (after margins and alignment), like a native mirrored layout.
    /// </summary>
    public FlowDirection FlowDirection { get => _flowDirection; set => SetFlowDirection(value); }

    /// <summary>Sets <see cref="FlowDirection"/>.</summary>
    public SkUiCoreNode SetFlowDirection(FlowDirection value)
    {
        if (SetProperty(ref _flowDirection, value, nameof(FlowDirection)))
            NotifyFlowDirectionChanged();
        return this;
    }

    /// <summary>Effective right-to-left layout direction (own, inherited, or from the host).</summary>
    internal bool IsRightToLeft => InheritedDirection() == SkUiTextDirection.RightToLeft;

    /// <summary>Effective direction: explicit LTR / RTL on this node or an ancestor (or the host), else <c>Auto</c>.</summary>
    internal SkUiTextDirection InheritedDirection()
    {
        SkUiCoreNode node = this;
        while (true)
        {
            if (node._flowDirection == FlowDirection.RightToLeft) return SkUiTextDirection.RightToLeft;
            if (node._flowDirection == FlowDirection.LeftToRight) return SkUiTextDirection.LeftToRight;
            if (node._parent is SkUiCoreNode parent)
            {
                node = parent;
                continue;
            }
            if (node.HostOwner is not { } host) return SkUiTextDirection.Auto;
            var effective = ((IVisualElementController)host).EffectiveFlowDirection;
            if (effective.HasFlag(EffectiveFlowDirection.RightToLeft)) return SkUiTextDirection.RightToLeft;
            return effective.HasFlag(EffectiveFlowDirection.Explicit) ? SkUiTextDirection.LeftToRight : SkUiTextDirection.Auto;
        }
    }

    /// <summary>Re-lays out this subtree after its effective direction changed (children that inherit follow).</summary>
    internal void NotifyFlowDirectionChanged()
    {
        InvalidateMeasure();
        OnEffectiveFlowDirectionChanged();
        var children = new List<ISkUiRenderable>();
        AddRenderChildren(children);
        foreach (var child in children)
            if (child is SkUiCoreNode { _flowDirection: FlowDirection.MatchParent } core)
                core.NotifyFlowDirectionChanged();
    }

    /// <summary>Called when the effective direction changes; layout is already invalidated.</summary>
    internal virtual void OnEffectiveFlowDirectionChanged() { }

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

    /// <summary>Width of the coordinate space children are arranged in (RTL mirroring axis); scrollers use their extent.</summary>
    internal virtual double ChildrenSpaceWidth => _frame.Width;

    /// <summary>Host that exclusively owns this node as a Core tree root, if any.</summary>
    internal SkUiCoreHost? HostOwner { get; set; }

    /// <summary>Visual tree children for diagnostics tools (called by tools only; may allocate).</summary>
    internal virtual IReadOnlyList<IVisualTreeElement> VisualChildren => [];

    IReadOnlyList<IVisualTreeElement> IVisualTreeElement.GetVisualChildren() => VisualChildren;

    IVisualTreeElement? IVisualTreeElement.GetVisualParent() => (IVisualTreeElement?)_parent ?? HostOwner;

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
        if (parent is null)
            SkUiGestureSet.CancelSubtree(this);
        var detached = parent is null && !HasInheritedHostClock;
        OnAnimationRootChanged(detached);
        PropagateAnimationRootChanged(detached);
    }

    private string? _automationId;

    /// <summary>Identifier for UI automation and diagnostics tools (like MAUI's <c>AutomationId</c>); no layout effect.</summary>
    public string? AutomationId => _automationId;

    /// <summary>Sets <see cref="AutomationId"/>.</summary>
    public SkUiCoreNode SetAutomationId(string? value)
    {
        SetProperty(ref _automationId, value, nameof(AutomationId));
        return this;
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
        // RTL: mirror the final frame inside the parent's (or host's) coordinate space.
        var mirrorSpace = _parent is SkUiCoreNode parentNode ? parentNode.ChildrenSpaceWidth : HostOwner?.Width ?? 0;
        if (mirrorSpace > 0 && (_parent is SkUiCoreNode rtlParent ? rtlParent.IsRightToLeft : HostOwner?.IsRightToLeft == true))
            frame = new Rect(mirrorSpace - frame.Right, frame.Y, frame.Width, frame.Height);
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

    bool ISkUiRenderable.HasOverlay => _paintOverlay is not null || _showsPressEffect;

    void ISkUiRenderable.RecordOverlay(SKCanvas canvas)
    {
        _paintOverlay?.Invoke(canvas);
        if (_showsPressEffect)
            SkUiLook.Current.DrawPressOverlay(canvas, new SkUiPressOverlayPaint(new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height),
                PressEffectCornerRadii, _pressEffect?.Visual ?? SkUiPressVisual.None, IsEnabled: true));
    }

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

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SkUiCoreNode, SkUiPointerRouter> Routers = new();
    private SkUiGestureSet? _gestures;

    /// <summary>
    /// Delivers a pointer sample in this node's local DIPs with this node as the dispatch root (normally the
    /// <see cref="SkUiCoreHost"/> surface does this). Taps, scrolling and other gestures run through the shared gesture
    /// arena; custom input uses <see cref="AddGestureRecognizer"/> instead of overriding touch handling.
    /// </summary>
    public bool Touch(SkUiTouchEvent touch) => Routers.GetValue(this, node => new SkUiPointerRouter(node)).Dispatch(touch);

    bool ISkUiInputNode.IsHitTestVisible => _isVisible;

    bool ISkUiInputNode.IsInputEnabled => true;

    void ISkUiInputNode.CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers) => CollectGestureRecognizers(recognizers);

    /// <summary>Appends this node's recognizers (built-in tap, long press, swipe, pan, pinch, custom, then intrinsic ones).</summary>
    internal virtual void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        var set = _gestures;
        if (HasIntrinsicTap || set?.Tapped is not null || set?.WantsDoubleTap == true)
        {
            set = GestureSet;
            recognizers.Add(set.Tap ??= new SkUiTapGestureRecognizer
            {
                TapHandler = args =>
                {
                    _gestures?.Tapped?.Invoke(this, args);
                    if (HasIntrinsicTap)
                        OnIntrinsicTap(args);
                },
                WantsDoubleTap = () => _gestures?.WantsDoubleTap == true,
                DoubleTapHandler = args => _gestures?.RaiseDoubleTapped(args),
                PressedHandler = (pressed, position) =>
                {
                    PressPosition = position;
                    UpdatePressEffect(pressed);
                    OnGesturePressedChanged(pressed);
                }
            });
        }
        set?.Collect(recognizers);
    }

    /// <summary>Whether the control reacts to taps without app handlers (buttons, toggles).</summary>
    internal virtual bool HasIntrinsicTap => false;

    /// <summary>Intrinsic tap action (click, toggle).</summary>
    internal virtual void OnIntrinsicTap(SkUiTappedEventArgs args) { }

    /// <summary>Press feedback from the tap recognizer.</summary>
    internal virtual void OnGesturePressedChanged(bool pressed) { }

    /// <summary>Where the last press started, in this node's coordinates (ripple origin).</summary>
    internal Point PressPosition { get; private set; }

    private bool _showsPressEffect;
    private SkUiPressAnimator? _pressEffect;

    /// <summary>
    /// Draws the look's press feedback (<see cref="SkUiLook.DrawPressOverlay"/>: a dim or a ripple from the press point)
    /// over this node and its children while it is pressed, clipped to its rounded shape (a label's or border's corner
    /// radii). For composite buttons built from several nodes (e.g. a <see cref="SkUiCoreBorder"/> holding an icon and
    /// labels): the node needs a <see cref="Tapped"/> handler to be pressed. Buttons draw their own feedback.
    /// </summary>
    public bool ShowsPressEffect
    {
        get => _showsPressEffect;
        set => SetShowsPressEffect(value);
    }

    /// <summary>Sets <see cref="ShowsPressEffect"/>.</summary>
    public SkUiCoreNode SetShowsPressEffect(bool value)
    {
        if (!SetProperty(ref _showsPressEffect, value, nameof(ShowsPressEffect))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Corner radii the press effect is clipped to (default square; labels and borders use their own).</summary>
    internal virtual CornerRadius PressEffectCornerRadii => default;

    private void UpdatePressEffect(bool pressed)
    {
        if (_showsPressEffect || _pressEffect is not null)
            (_pressEffect ??= new SkUiPressAnimator(this)).SetPressed(pressed, PressPosition);
    }

    SkUiAnimationClock? ISkUiTransitionHost.TransitionClock => _renderState is { HasCommitted: true } ? AnimationClock : null;

    void ISkUiTransitionHost.InvalidateTransition() => InvalidatePaint();

    void ISkUiGestureElement.CancelGestures() => CancelGestures();

    /// <summary>Stops this node's gestures.</summary>
    internal virtual void CancelGestures()
    {
        _gestures?.CancelAll();
        if (Routers.TryGetValue(this, out var router))
            router.CancelAll();
        UpdatePressEffect(false);
        OnGesturePressedChanged(false);
    }

    private SkUiGestureSet GestureSet => _gestures ??= new SkUiGestureSet(this);

    /// <summary>Tap (and click for buttons). Handlers make the node participate in taps.</summary>
    public event EventHandler<SkUiTappedEventArgs>? Tapped { add => GestureSet.Tapped += value; remove => GestureSet.Tapped -= value; }

    /// <summary>Double tap (single taps then wait <see cref="SkUiGestureSettings.DoubleTapTimeout"/>).</summary>
    public event EventHandler<SkUiTappedEventArgs>? DoubleTapped { add => GestureSet.DoubleTapped += value; remove => GestureSet.DoubleTapped -= value; }

    /// <summary>Long press.</summary>
    public event EventHandler<SkUiLongPressedEventArgs>? LongPressed { add => GestureSet.LongPressed += value; remove => GestureSet.LongPressed -= value; }

    /// <summary>Swipe in one of <see cref="SwipeDirections"/>.</summary>
    public event EventHandler<SkUiSwipedEventArgs>? Swiped { add => GestureSet.Swiped += value; remove => GestureSet.Swiped -= value; }

    /// <summary>Pan along <see cref="PanAxis"/>.</summary>
    public event EventHandler<SkUiPanUpdatedEventArgs>? PanUpdated { add => GestureSet.PanUpdated += value; remove => GestureSet.PanUpdated -= value; }

    /// <summary>Two-pointer pinch / rotate.</summary>
    public event EventHandler<SkUiPinchUpdatedEventArgs>? PinchUpdated { add => GestureSet.PinchUpdated += value; remove => GestureSet.PinchUpdated -= value; }

    /// <summary>Directions <see cref="Swiped"/> reacts to (default all).</summary>
    public SwipeDirection SwipeDirections => _gestures?.SwipeDirections ?? (SwipeDirection.Left | SwipeDirection.Right | SwipeDirection.Up | SwipeDirection.Down);

    /// <summary>Sets <see cref="SwipeDirections"/>.</summary>
    public SkUiCoreNode SetSwipeDirections(SwipeDirection value) { GestureSet.SwipeDirections = value; return this; }

    /// <summary>Axes <see cref="PanUpdated"/> reacts to (default both).</summary>
    public SkUiPanAxis PanAxis => _gestures?.PanAxis ?? SkUiPanAxis.Both;

    /// <summary>Sets <see cref="PanAxis"/>.</summary>
    public SkUiCoreNode SetPanAxis(SkUiPanAxis value) { GestureSet.PanAxis = value; return this; }

    /// <summary>Adds a custom recognizer (after the built-in ones).</summary>
    public SkUiCoreNode AddGestureRecognizer(SkUiGestureRecognizer recognizer)
    {
        ArgumentNullException.ThrowIfNull(recognizer);
        (GestureSet.Custom ??= []).Add(recognizer);
        return this;
    }

    /// <summary>Removes a custom recognizer.</summary>
    public bool RemoveGestureRecognizer(SkUiGestureRecognizer recognizer)
    {
        recognizer.Cancel();
        return _gestures?.Custom?.Remove(recognizer) == true;
    }

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
