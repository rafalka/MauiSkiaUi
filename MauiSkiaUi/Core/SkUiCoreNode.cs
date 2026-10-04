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
public partial class SkUiCoreNode : ISkUiCoreNode, INotifyPropertyChanged, ISkUiRenderable, ISkUiGestureElement, ISkUiTransitionHost, ISkUiShadowCaster
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
    /// Horizontal alignment within the arrange slot given by a parent layout, as MAUI's <c>HorizontalOptions</c> /
    /// <c>ComputeFrame</c>, in every container (a stack aligns on its cross axis). Default is <see cref="LayoutAlignment.Fill"/>;
    /// with an explicit <see cref="Width"/> or a finite <see cref="MaximumWidth"/>, Fill centers, as in MAUI.
    /// </summary>
    public LayoutAlignment HorizontalAlignment
    {
        get => _horizontalAlignment;
        set => SetHorizontalAlignment(value);
    }

    /// <summary>
    /// Vertical alignment within the arrange slot given by a parent layout, as MAUI's <c>VerticalOptions</c> /
    /// <c>ComputeFrame</c>, in every container (a stack aligns on its cross axis). Default is <see cref="LayoutAlignment.Fill"/>;
    /// with an explicit <see cref="Height"/> or a finite <see cref="MaximumHeight"/>, Fill centers, as in MAUI.
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

    /// <summary>
    /// The SkUi* view that owns this node as a Core tree root, if any: a <see cref="SkUiCoreHost"/>, or a control drawing
    /// internal Core parts (a scroll view's scroll bars).
    /// </summary>
    internal SkUiView? HostOwner { get; set; }

    /// <summary>
    /// The node is pinned to its parent's viewport (<see cref="Rendering.SkUiRenderProps.Pinned"/>): its frame is in the parent's
    /// local coordinates, physical (not mirrored in right-to-left layouts), and the parent's scroll offset does not move it.
    /// </summary>
    internal virtual bool IsPinned => false;

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
        {
            SkUiGestureSet.CancelSubtree(this);
            SkUiFocusManager.ValidateAll();
        }
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

    /// <summary>
    /// Any object the app wants to keep with this node (an id, a model, a cache). SkiaUi never reads or changes it. It
    /// raises no <see cref="PropertyChanged"/> and takes no part in layout, drawing or input.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>Sets <see cref="Tag"/>.</summary>
    public SkUiCoreNode SetTag(object? value)
    {
        Tag = value;
        return this;
    }

    /// <summary>Sets visibility; raises <see cref="System.ComponentModel.INotifyPropertyChanged"/> when changed.</summary>
    public SkUiCoreNode SetIsVisible(bool value)
    {
        if (!SetProperty(ref _isVisible, value, nameof(IsVisible))) return this;
        if (!value)
            SkUiFocusManager.ValidateAll();
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
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        if (!SetProperty(ref _minimumWidth, value, nameof(MinimumWidth))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets minimum height in DIPs.</summary>
    public virtual SkUiCoreNode SetMinimumHeight(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
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
        {
            render |= SkUiRenderDirty.Content;
            if (_effects is { } effects)
                effects.PaintVersion++; // the fill, and so the shadow's silhouette, may have changed
        }
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

        var (x, width) = AlignInSlot(bounds.X, bounds.Width, _margin.Left, _margin.HorizontalThickness, _horizontalAlignment,
            _width, _maximumWidth, _desiredSize.Width);
        var (y, height) = AlignInSlot(bounds.Y, bounds.Height, _margin.Top, _margin.VerticalThickness, _verticalAlignment,
            _height, _maximumHeight, _desiredSize.Height);
        var frame = new Rect(x, y, width, height);
        // RTL: mirror the final frame inside the parent's (or host's) coordinate space.
        var mirrorSpace = _parent is SkUiCoreNode parentNode ? parentNode.ChildrenSpaceWidth : HostOwner?.Width ?? 0;
        if (!IsPinned && mirrorSpace > 0 && (_parent is SkUiCoreNode rtlParent ? rtlParent.IsRightToLeft : HostOwner?.IsRightToLeft == true))
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

    /// <summary>
    /// Places the node in its slot along one axis as MAUI's <c>ComputeFrame</c> does, so every Core container honors the
    /// alignment (stacks on the cross axis, content views and overlays on both): <see cref="LayoutAlignment.Fill"/> takes the
    /// slot (up to the maximum); an explicit size or a finite maximum makes Fill center; Start / Center / End place the
    /// desired size. Unlike MAUI, the frame never grows past the slot.
    /// </summary>
    private static (double Position, double Size) AlignInSlot(double start, double slot, double startMargin, double margins,
        LayoutAlignment alignment, double explicitSize, double maximum, double desired)
    {
        var available = Math.Max(0, slot - margins);
        double size;
        if (!double.IsNaN(explicitSize))
            size = Math.Min(available, explicitSize);
        else if (alignment == LayoutAlignment.Fill)
            size = Math.Min(available, maximum);
        else
            size = Math.Min(available, Math.Max(0, desired - margins));
        if (alignment == LayoutAlignment.Fill && (!double.IsNaN(explicitSize) || !double.IsInfinity(maximum)))
            alignment = LayoutAlignment.Center;
        var offset = alignment switch
        {
            LayoutAlignment.Center => (available - size) / 2,
            LayoutAlignment.End => available - size,
            _ => 0
        };
        return (start + startMargin + offset, size);
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
        props.Pinned = IsPinned;
        OnGetRenderProps(ref props);
        if (_effects is { } effects)
        {
            props.ClipPath = effects.GetClipPath(_clip, props.Width, props.Height);
            props.Shadow = effects.GetShadow(_shadow, this, props);
        }
    }

    /// <summary>Sets the <see cref="ClipToBounds"/> default without notifications (constructors only).</summary>
    internal void InitClipToBounds(bool value) => _clipToBounds = value;

    bool ISkUiRenderable.RecordsWhenTransparent => RecordsWhenTransparent;

    /// <inheritdoc cref="ISkUiRenderable.RecordsWhenTransparent" />
    internal virtual bool RecordsWhenTransparent => false;

    /// <summary>Lets containers add children clip / offset, spinning content, or ink overflow.</summary>
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
            SkUiLook.Current.DrawPressOverlay(canvas, new SkUiPressOverlayPaint(new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height),
                PressEffectCornerRadii, _pressEffect?.Visual ?? SkUiPressVisual.None, IsEnabled: true));
        if (_focusRingVisible)
            DrawFocusRing(canvas);
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
    private Paint? _background;
    private SkUiVisualEffects? _effects;
    private IShape? _clip;
    private IShadow? _shadow;
    private SkUiWeakListener<SkUiCoreNode>? _clipListener;
    private SkUiWeakListener<SkUiCoreNode>? _shadowListener;

    /// <summary>
    /// The fill behind the node (MAUI's <c>Background</c>): a solid color or a linear or radial gradient, mapped onto the
    /// node's bounds; <c>null</c> (default) draws none. Drawn by <see cref="OnPaintBackground"/> unless a
    /// <see cref="PaintBackground"/> painter is set; filled controls (labels, buttons, borders, boxes) fill their own shape
    /// with it instead of their color.
    /// </summary>
    public Paint? Background { get => _background; set => SetBackground(value); }

    /// <summary>Sets <see cref="Background"/>.</summary>
    public SkUiCoreNode SetBackground(Paint? value)
    {
        if (!SetProperty(ref _background, value, nameof(Background))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets a solid <see cref="Background"/>.</summary>
    public SkUiCoreNode SetBackground(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _background is SolidPaint { Color: var color } && color == value ? this : SetBackground(new SolidPaint(value));
    }

    /// <summary>
    /// Clips the node's content, children and overlay to a shape (MAUI's <c>Clip</c>): a Core shape (placed in the node's
    /// bounds, as when it shapes a border), a MAUI geometry (in the node's coordinates) or any MAUI Graphics
    /// <see cref="IShape"/>; <c>null</c> (default) for none. Applied when compositing, so it never re-records the node;
    /// input still uses the rectangular bounds. Changes of the shape's properties re-clip.
    /// </summary>
    public IShape? Clip { get => _clip; set => SetClip(value); }

    /// <summary>Sets <see cref="Clip"/>.</summary>
    public SkUiCoreNode SetClip(IShape? value)
    {
        if (ReferenceEquals(_clip, value)) return this;
        _clip = value;
        // A shared shape must not keep the node alive.
        (_clipListener ??= new(this, static (node, _) =>
        {
            if (node._effects is { } effects) effects.ClipVersion++;
            node.InvalidateRender(SkUiRenderDirty.Props);
        })).Listen(value);
        (_effects ??= new()).ClipVersion++;
        OnPropertyChanged(nameof(Clip));
        InvalidateRender(SkUiRenderDirty.Props);
        return this;
    }

    /// <summary>
    /// The drop shadow (MAUI's <c>Shadow</c>, FR-20): paint (solid or gradient), offset, blur radius and opacity, e.g. a
    /// <see cref="SkUiCoreShadow"/>; <c>null</c> (default) for none. A node with an opaque fill casts it from the fill's
    /// shape, any other from everything it and its children draw (text, images). Drawn outside the node's own clip, before
    /// the node, when compositing: it changes neither layout nor input, and moving, fading or transforming the node does
    /// not redraw it. A parent that clips its children clips their shadows too.
    /// </summary>
    public IShadow? Shadow { get => _shadow; set => SetShadow(value); }

    /// <summary>Sets <see cref="Shadow"/>.</summary>
    public SkUiCoreNode SetShadow(IShadow? value)
    {
        if (ReferenceEquals(_shadow, value)) return this;
        _shadow = value;
        // A mutable shadow (MAUI's Shadow) redraws when it changes; a shared one must not keep the node alive.
        (_shadowListener ??= new(this, static (node, _) =>
        {
            if (node._effects is { } effects) effects.ShadowVersion++;
            node.InvalidateRender(SkUiRenderDirty.Props);
        })).Listen(value);
        (_effects ??= new()).ShadowVersion++;
        OnPropertyChanged(nameof(Shadow));
        InvalidateRender(SkUiRenderDirty.Props);
        return this;
    }

    SKPath? ISkUiShadowCaster.CreateShadowOutline(float width, float height) => CreateShadowOutline(width, height);

    /// <summary>
    /// The silhouette of the node's own opaque fill, which its shadow is cast from, or <c>null</c> to cast it from everything
    /// the node and its children draw. By default the rectangle of an opaque <see cref="Background"/> drawn by the default
    /// painter; controls with shaped fills return their shape.
    /// </summary>
    internal virtual SKPath? CreateShadowOutline(float width, float height) =>
        _paintBackground is null && SkUiShapePainter.IsOpaque(_background) ? RectangleOutline(width, height) : null;

    /// <summary>A rectangle of the given size as a shadow outline.</summary>
    private protected static SKPath RectangleOutline(float width, float height)
    {
        using var builder = new SKPathBuilder();
        builder.AddRect(new SKRect(0, 0, width, height));
        return builder.Detach();
    }

    /// <summary>
    /// Background paint phase when <see cref="PaintBackground"/> is unset: by default <see cref="PaintDefaultBackground"/>.
    /// Override for chrome that must stay virtual.
    /// </summary>
    protected virtual void OnPaintBackground(SKCanvas canvas) => PaintDefaultBackground(canvas);

    /// <summary>Fills the node's rectangle with <see cref="Background"/>; painters may call it before their own chrome.</summary>
    protected void PaintDefaultBackground(SKCanvas canvas) =>
        SkUiShapePainter.FillRect(canvas, new SKRect(0, 0, (float)_frame.Width, (float)_frame.Height), _background);

    /// <summary>
    /// Background-layer painter. When unset, <see cref="OnPaintBackground"/> runs (by default it fills
    /// <see cref="Background"/>). Prefer this over subclassing for chrome customization.
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

    /// <summary>Sets the Background painter; <c>null</c> restores <see cref="OnPaintBackground"/>.</summary>
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

    void ISkUiInputNode.SetPointerOver(bool isOver) => OnPointerOverChanged(isOver);

    /// <summary>A hovering pointer entered (<c>true</c>) or left this node or one of its descendants.</summary>
    internal virtual void OnPointerOverChanged(bool isOver) { }

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

    /// <summary>The look or color scheme changed: forget the cached measure and re-record (no per-node propagation).</summary>
    internal void MarkLookChanged()
    {
        _measureDirty = true;
        _arrangeDirty = true;
        if (_effects is { } effects)
            effects.PaintVersion++;
        SkUiRenderInvalidation.Mark(this, SkUiRenderDirty.Content);
    }

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

    // Attached values: a small array created on the first SetValue (most nodes have none, laid-out children one to
    // four). Each value sits in its own typed box, kept (and reused) even when the value returns to its default, so
    // only the first write of a property allocates and reads never do; a linear scan beats a dictionary at these sizes.
    private AttachedEntry[]? _attached;
    private int _attachedCount;

    private struct AttachedEntry
    {
        public object Property;
        public object Box;
        /// <summary>Whether the value differs from the default (<see cref="IsSet{T}"/>).</summary>
        public bool IsSet;
    }

    /// <summary>Gets an attached value (<see cref="SkUiCoreAttachedProperty{T}.DefaultValue"/> when never set).</summary>
    public T GetValue<T>(SkUiCoreAttachedProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        var index = IndexOfAttached(property);
        return index < 0 ? property.DefaultValue : ((StrongBox<T>)_attached![index].Box).Value!;
    }

    /// <summary>
    /// Sets an attached value, raises <see cref="PropertyChanged"/> with the property's name when it changes and, for
    /// layout properties, re-measures the parent. Writing the default value is the same as <see cref="ClearValue{T}"/>.
    /// </summary>
    public SkUiCoreNode SetValue<T>(SkUiCoreAttachedProperty<T> property, T value)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.Validate(value);
        var isDefault = EqualityComparer<T>.Default.Equals(property.DefaultValue, value);
        var index = IndexOfAttached(property);
        if (index >= 0)
        {
            ref var entry = ref _attached![index];
            var box = (StrongBox<T>)entry.Box;
            if (EqualityComparer<T>.Default.Equals(box.Value, value))
                return this;
            box.Value = value;
            entry.IsSet = !isDefault;
        }
        else
        {
            if (isDefault)
                return this;
            if (_attached is null || _attachedCount == _attached.Length)
                Array.Resize(ref _attached, _attachedCount == 0 ? 2 : _attachedCount * 2);
            _attached[_attachedCount++] = new AttachedEntry { Property = property, Box = new StrongBox<T>(value), IsSet = true };
        }
        OnAttachedValueChanged(property);
        return this;
    }

    /// <summary>Restores an attached value to its default.</summary>
    public SkUiCoreNode ClearValue<T>(SkUiCoreAttachedProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return SetValue(property, property.DefaultValue);
    }

    /// <summary>
    /// Whether the node holds a value other than the default. Writing the default (or <see cref="ClearValue{T}"/>)
    /// resets it; unlike MAUI, where writing the default still counts as set.
    /// </summary>
    public bool IsSet<T>(SkUiCoreAttachedProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        var index = IndexOfAttached(property);
        return index >= 0 && _attached![index].IsSet;
    }

    private int IndexOfAttached(object property)
    {
        for (var index = 0; index < _attachedCount; index++)
            if (ReferenceEquals(_attached![index].Property, property))
                return index;
        return -1;
    }

    private void OnAttachedValueChanged<T>(SkUiCoreAttachedProperty<T> property)
    {
        OnPropertyChanged(property.Name);
        if (property.AffectsParentMeasure)
            _parent?.InvalidateMeasure();
    }

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
