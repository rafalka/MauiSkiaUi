using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// A drawn handle the user drags (Core analogue of <see cref="SkUiDragger"/>): it raises <see cref="DragStarted"/>,
/// <see cref="DragDelta"/> and <see cref="DragCompleted"/> with the travel since the drag started, in the parent's coordinates,
/// and does nothing else itself. Handle the events (or override <see cref="OnDragStarted"/>, <see cref="OnDragDelta"/>,
/// <see cref="OnDragCompleted"/>) to resize or move something. Arrow keys along <see cref="DragAxis"/> drag it by
/// <see cref="KeyboardIncrement"/>. The look draws an optional line and a grip. <see cref="SkUiCoreGridSplitter"/> is a
/// dragger that resizes grid tracks.
/// </summary>
public class SkUiCoreDragger : SkUiCoreNode, ISkUiDraggerOwner, ISkUiKeyHandler
{
    private SkUiPanAxis _dragAxis;
    private bool _showsGrip = true;
    private Color _gripColor = SkUiColors.Muted;
    private LayoutAlignment _gripAlignment = LayoutAlignment.Center;
    private double _gripLength = -1;
    private bool _showsLine;
    private Color _lineColor = SkUiColors.Muted;
    private double _keyboardIncrement = SkUiDragger.DefaultKeyboardIncrement;
    private double _touchThickness = -1;
    private SkUiDraggerModel? _model;

    /// <summary>The direction the dragger follows (default <see cref="SkUiPanAxis.Both"/>).</summary>
    public SkUiPanAxis DragAxis { get => _dragAxis; set => SetDragAxis(value); }

    /// <summary>Whether the look draws the grip (default <c>true</c>).</summary>
    public bool ShowsGrip { get => _showsGrip; set => SetShowsGrip(value); }

    /// <summary>Color of the grip (default: the color scheme's muted color); the background fills the dragger.</summary>
    public Color GripColor { get => _gripColor; set => SetGripColor(value); }

    /// <summary>Where the grip sits along the dragger: start, center (default), end, or along all of it (<see cref="LayoutAlignment.Fill"/>).</summary>
    public LayoutAlignment GripAlignment { get => _gripAlignment; set => SetGripAlignment(value); }

    /// <summary>Length of the grip in DIPs; below 0 (default) the look's <see cref="SkUiLook.DraggerGripLength"/>.</summary>
    public double GripLength { get => _gripLength; set => SetGripLength(value); }

    /// <summary>Whether the look draws a line along the whole dragger (default <c>false</c>).</summary>
    public bool ShowsLine { get => _showsLine; set => SetShowsLine(value); }

    /// <summary>Color of the line (default: the color scheme's muted color).</summary>
    public Color LineColor { get => _lineColor; set => SetLineColor(value); }

    /// <summary>DIPs an arrow key moves the dragger (default <see cref="SkUiDragger.DefaultKeyboardIncrement"/>).</summary>
    public double KeyboardIncrement { get => _keyboardIncrement; set => SetKeyboardIncrement(value); }

    /// <summary>
    /// Least thickness of the touch area across the dragger, in DIPs; below 0 (default) the look's
    /// <see cref="SkUiLook.DraggerTouchThickness"/>. A thinner dragger takes pointers that far across, centered on it, and its
    /// grip draws over its neighbors; put it after them so it is on top.
    /// </summary>
    public double TouchThickness { get => _touchThickness; set => SetTouchThickness(value); }

    /// <summary>Whether a drag is in progress.</summary>
    public bool IsDragging => _model?.IsDragging == true;

    /// <summary>Raised when a drag starts (the totals are 0).</summary>
    public event EventHandler<SkUiDragEventArgs>? DragStarted;

    /// <summary>Raised as the drag moves, with the travel since it started and since the previous event.</summary>
    public event EventHandler<SkUiDragEventArgs>? DragDelta;

    /// <summary>Raised when the drag ends: released, or canceled (<see cref="SkUiDragEventArgs.IsCanceled"/>).</summary>
    public event EventHandler<SkUiDragEventArgs>? DragCompleted;

    /// <summary>Sets the drag axis.</summary>
    public SkUiCoreDragger SetDragAxis(SkUiPanAxis value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), value, null);
        if (SetProperty(ref _dragAxis, value, nameof(DragAxis)))
            InvalidateMeasure();
        return this;
    }

    /// <summary>Sets whether the grip shows.</summary>
    public SkUiCoreDragger SetShowsGrip(bool value) => SetPaintProperty(ref _showsGrip, value, nameof(ShowsGrip));

    /// <summary>Sets the grip color.</summary>
    public SkUiCoreDragger SetGripColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return SetPaintProperty(ref _gripColor, value, nameof(GripColor));
    }

    /// <summary>Sets the grip alignment.</summary>
    public SkUiCoreDragger SetGripAlignment(LayoutAlignment value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), value, null);
        return SetPaintProperty(ref _gripAlignment, value, nameof(GripAlignment));
    }

    /// <summary>Sets the grip length (below 0: the look's).</summary>
    public SkUiCoreDragger SetGripLength(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        return SetPaintProperty(ref _gripLength, value, nameof(GripLength));
    }

    /// <summary>Sets whether the line shows.</summary>
    public SkUiCoreDragger SetShowsLine(bool value) => SetPaintProperty(ref _showsLine, value, nameof(ShowsLine));

    /// <summary>Sets the line color.</summary>
    public SkUiCoreDragger SetLineColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return SetPaintProperty(ref _lineColor, value, nameof(LineColor));
    }

    /// <summary>Sets the keyboard increment, in DIPs (positive).</summary>
    public SkUiCoreDragger SetKeyboardIncrement(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        SetProperty(ref _keyboardIncrement, value, nameof(KeyboardIncrement));
        return this;
    }

    /// <summary>Sets the touch thickness, in DIPs (below 0: the look's).</summary>
    public SkUiCoreDragger SetTouchThickness(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        if (SetProperty(ref _touchThickness, value, nameof(TouchThickness)))
        {
            InvalidateRender(SkUiRenderDirty.Props);
            InvalidatePaint();
        }
        return this;
    }

    private SkUiCoreDragger SetPaintProperty<T>(ref T field, T value, string name)
    {
        if (SetProperty(ref field, value, name))
            InvalidatePaint();
        return this;
    }

    private SkUiDraggerModel Model => _model ??= new SkUiDraggerModel(this);

    /// <summary>The direction the dragger follows now: <see cref="DragAxis"/>; a grid splitter's comes from its place in the grid.</summary>
    protected virtual SkUiPanAxis EffectiveDragAxis => _dragAxis;

    /// <summary>Whether a drag can start now (default: always); otherwise pointers and arrow keys go to others.</summary>
    protected virtual bool CanDrag => true;

    /// <summary>A drag started (also one arrow key press); raises <see cref="DragStarted"/>.</summary>
    protected virtual void OnDragStarted(SkUiDragEventArgs args) => DragStarted?.Invoke(this, args);

    /// <summary>The drag moved; raises <see cref="DragDelta"/>.</summary>
    protected virtual void OnDragDelta(SkUiDragEventArgs args) => DragDelta?.Invoke(this, args);

    /// <summary>The drag ended; raises <see cref="DragCompleted"/>.</summary>
    protected virtual void OnDragCompleted(SkUiDragEventArgs args) => DragCompleted?.Invoke(this, args);

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => SkUiDraggerMath.Measure(EffectiveDragAxis);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var enabled = IsEnabled;
        SkUiLook.Current.DrawDragger(canvas, SkUiDraggerMath.Paint(Frame.Size, EffectiveDragAxis, _touchThickness, IsRightToLeft, _showsGrip,
            ToSkColor(SkUiColors.EnabledTint(_gripColor, enabled)), _gripAlignment, _gripLength, _showsLine,
            ToSkColor(SkUiColors.EnabledTint(_lineColor, enabled)), IsDragging, enabled));
    }

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        base.OnGetRenderProps(ref props);
        // Outside the paint's look scope: the subtree's look, as painting uses.
        SkUiDraggerMath.ApplyOverflow(ref props, EffectiveDragAxis, _touchThickness, SkUiLookScope.Resolve(this));
    }

    // The touch area past a thin dragger, while it can be dragged (a disabled one would block what is under it).
    internal override bool HitsOutsideBounds(Point local) =>
        IsEnabled && SkUiDraggerMath.HitsTouchArea(Frame.Size, EffectiveDragAxis, _touchThickness, SkUiLookScope.Resolve(this), local) && CanDrag;

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        base.CollectGestureRecognizers(recognizers);
        recognizers.Add(Model.Gesture);
    }

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => IsEnabled && Model.OnKeyDown(key);

    SkUiPanAxis ISkUiDraggerOwner.DragAxis => EffectiveDragAxis;

    bool ISkUiDraggerOwner.CanDrag => CanDrag;

    Point ISkUiDraggerOwner.Offset => Frame.Location;

    double ISkUiDraggerOwner.KeyboardIncrement => _keyboardIncrement;

    void ISkUiDraggerOwner.Started(SkUiDragEventArgs args)
    {
        InvalidatePaint();
        OnDragStarted(args);
    }

    void ISkUiDraggerOwner.Moved(SkUiDragEventArgs args) => OnDragDelta(args);

    void ISkUiDraggerOwner.Ended(SkUiDragEventArgs args)
    {
        InvalidatePaint();
        OnDragCompleted(args);
    }
}
