using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn handle the user drags: it raises <see cref="DragStarted"/>, <see cref="DragDelta"/> and <see cref="DragCompleted"/>
/// with the travel since the drag started, and does nothing else itself. Handle the events (or override
/// <see cref="OnDragStarted"/>, <see cref="OnDragDelta"/>, <see cref="OnDragCompleted"/>) to resize or move something: take its
/// size on start, then set it to that size plus the total travel on each delta. The travel is measured in the parent's
/// coordinates, so it keeps following the pointer when the handler moves or resizes the dragger itself.
/// <see cref="DragAxis"/> picks the direction it follows (a drag across a one-axis dragger goes to scrollers around it); arrow
/// keys along it drag it by <see cref="KeyboardIncrement"/>. The look draws an optional line along it (<see cref="ShowsLine"/>)
/// and a grip (<see cref="ShowsGrip"/>, placed by <see cref="GripAlignment"/>). <see cref="SkUiGridSplitter"/> is a dragger
/// that resizes grid tracks.
/// </summary>
public class SkUiDragger : SkUiView, ISkUiDraggerOwner, ISkUiKeyHandler
{
    // Field initializers, not constructor assignments: an implicit style applies before the constructor body runs.
    private SkUiPanAxis _dragAxis;
    private bool _showsGrip = true;
    private Color _gripColor = SkUiColors.Muted;
    private LayoutAlignment _gripAlignment = LayoutAlignment.Center;
    private double _gripLength = -1;
    private bool _showsLine;
    private Color _lineColor = SkUiColors.Muted;
    private double _keyboardIncrement = DefaultKeyboardIncrement;
    private double _touchThickness = -1;

    /// <summary>Default <see cref="KeyboardIncrement"/>, in DIPs.</summary>
    public const double DefaultKeyboardIncrement = 10;

    /// <summary>Bindable <see cref="DragAxis"/>.</summary>
    public static readonly BindableProperty DragAxisProperty = BindableProperty.Create(nameof(DragAxis), typeof(SkUiPanAxis), typeof(SkUiDragger),
        SkUiPanAxis.Both, validateValue: SkUiValidate.PanAxis,
        propertyChanged: (view, _, value) => ((SkUiDragger)view).OnDragAxisChanged((SkUiPanAxis)value));

    /// <summary>Bindable <see cref="ShowsGrip"/>.</summary>
    public static readonly BindableProperty ShowsGripProperty = BindableProperty.Create(nameof(ShowsGrip), typeof(bool), typeof(SkUiDragger), true,
        propertyChanged: (view, _, value) => ((SkUiDragger)view).OnPaintChanged(ref ((SkUiDragger)view)._showsGrip, (bool)value));

    /// <summary>Bindable <see cref="GripColor"/>.</summary>
    public static readonly BindableProperty GripColorProperty = BindableProperty.Create(nameof(GripColor), typeof(Color), typeof(SkUiDragger), null,
        defaultValueCreator: _ => SkUiColors.Muted, validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiDragger)view).OnPaintChanged(ref ((SkUiDragger)view)._gripColor, (Color)value));

    /// <summary>Bindable <see cref="GripAlignment"/>.</summary>
    public static readonly BindableProperty GripAlignmentProperty = BindableProperty.Create(nameof(GripAlignment), typeof(LayoutAlignment), typeof(SkUiDragger),
        LayoutAlignment.Center, validateValue: SkUiValidate.Alignment,
        propertyChanged: (view, _, value) => ((SkUiDragger)view).OnPaintChanged(ref ((SkUiDragger)view)._gripAlignment, (LayoutAlignment)value));

    /// <summary>Bindable <see cref="GripLength"/>.</summary>
    public static readonly BindableProperty GripLengthProperty = BindableProperty.Create(nameof(GripLength), typeof(double), typeof(SkUiDragger), -1d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiDragger)view).OnPaintChanged(ref ((SkUiDragger)view)._gripLength, (double)value));

    /// <summary>Bindable <see cref="ShowsLine"/>.</summary>
    public static readonly BindableProperty ShowsLineProperty = BindableProperty.Create(nameof(ShowsLine), typeof(bool), typeof(SkUiDragger), false,
        propertyChanged: (view, _, value) => ((SkUiDragger)view).OnPaintChanged(ref ((SkUiDragger)view)._showsLine, (bool)value));

    /// <summary>Bindable <see cref="LineColor"/>.</summary>
    public static readonly BindableProperty LineColorProperty = BindableProperty.Create(nameof(LineColor), typeof(Color), typeof(SkUiDragger), null,
        defaultValueCreator: _ => SkUiColors.Muted, validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiDragger)view).OnPaintChanged(ref ((SkUiDragger)view)._lineColor, (Color)value));

    /// <summary>Bindable <see cref="KeyboardIncrement"/>.</summary>
    public static readonly BindableProperty KeyboardIncrementProperty = BindableProperty.Create(nameof(KeyboardIncrement), typeof(double),
        typeof(SkUiDragger), DefaultKeyboardIncrement, validateValue: SkUiValidate.FinitePositive,
        propertyChanged: (view, _, value) => ((SkUiDragger)view)._keyboardIncrement = (double)value);

    /// <summary>Bindable <see cref="TouchThickness"/>.</summary>
    public static readonly BindableProperty TouchThicknessProperty = BindableProperty.Create(nameof(TouchThickness), typeof(double), typeof(SkUiDragger),
        -1d, validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiDragger)view).OnTouchThicknessChanged((double)value));

    /// <summary>
    /// The direction the dragger follows (default <see cref="SkUiPanAxis.Both"/>). The line and grip run across a one-axis
    /// dragger's direction (a <see cref="SkUiPanAxis.Vertical"/> dragger is a horizontal bar), and along the longer side of a
    /// two-axis one.
    /// </summary>
    public SkUiPanAxis DragAxis { get => (SkUiPanAxis)GetValue(DragAxisProperty); set => SetValue(DragAxisProperty, value); }

    /// <summary>Whether the look draws the grip (default <c>true</c>).</summary>
    public bool ShowsGrip { get => (bool)GetValue(ShowsGripProperty); set => SetValue(ShowsGripProperty, value); }

    /// <summary>Color of the grip (default: the color scheme's muted color). <see cref="VisualElement.Background"/> fills the dragger.</summary>
    public Color GripColor { get => (Color)GetValue(GripColorProperty); set => SetValue(GripColorProperty, value); }

    /// <summary>
    /// Where the grip sits along the dragger (default <see cref="LayoutAlignment.Center"/>): at its start (top, or left in a
    /// left-to-right layout), center or end, or along all of it (<see cref="LayoutAlignment.Fill"/>). A column splitter that
    /// spans a table with its grip in the header uses <see cref="LayoutAlignment.Start"/>.
    /// </summary>
    public LayoutAlignment GripAlignment { get => (LayoutAlignment)GetValue(GripAlignmentProperty); set => SetValue(GripAlignmentProperty, value); }

    /// <summary>Length of the grip in DIPs; below 0 (default) the look's <see cref="SkUiLook.DraggerGripLength"/>.</summary>
    public double GripLength { get => (double)GetValue(GripLengthProperty); set => SetValue(GripLengthProperty, value); }

    /// <summary>Whether the look draws a line along the whole dragger, through its middle (default <c>false</c>): a visible divider.</summary>
    public bool ShowsLine { get => (bool)GetValue(ShowsLineProperty); set => SetValue(ShowsLineProperty, value); }

    /// <summary>Color of the line (default: the color scheme's muted color).</summary>
    public Color LineColor { get => (Color)GetValue(LineColorProperty); set => SetValue(LineColorProperty, value); }

    /// <summary>DIPs an arrow key moves the dragger (default <see cref="DefaultKeyboardIncrement"/>).</summary>
    public double KeyboardIncrement { get => (double)GetValue(KeyboardIncrementProperty); set => SetValue(KeyboardIncrementProperty, value); }

    /// <summary>
    /// Least thickness of the touch area across the dragger, in DIPs; below 0 (default) the look's
    /// <see cref="SkUiLook.DraggerTouchThickness"/>. A thinner dragger (<c>HeightRequest="1"</c> on a row splitter: a divider
    /// that takes no room) takes pointers that far across, centered on it, and its grip draws over its neighbors; put it after
    /// them (or give it a higher <c>ZIndex</c>) so it is on top.
    /// </summary>
    public double TouchThickness { get => (double)GetValue(TouchThicknessProperty); set => SetValue(TouchThicknessProperty, value); }

    /// <summary>Whether a drag is in progress.</summary>
    public bool IsDragging => Model.IsDragging;

    /// <summary>Raised when a drag starts (the totals are 0).</summary>
    public event EventHandler<SkUiDragEventArgs>? DragStarted;

    /// <summary>Raised as the drag moves, with the travel since it started and since the previous event.</summary>
    public event EventHandler<SkUiDragEventArgs>? DragDelta;

    /// <summary>Raised when the drag ends: released, or canceled (<see cref="SkUiDragEventArgs.IsCanceled"/>).</summary>
    public event EventHandler<SkUiDragEventArgs>? DragCompleted;

    /// <summary>Sets the drag axis (same as the property setter).</summary>
    public SkUiDragger SetDragAxis(SkUiPanAxis value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), value, null);
        DragAxis = value;
        return this;
    }

    private void OnDragAxisChanged(SkUiPanAxis value)
    {
        _dragAxis = value;
        InvalidateMeasureOverride();
    }

    /// <summary>Sets whether the grip shows (same as the property setter).</summary>
    public SkUiDragger SetShowsGrip(bool value) { ShowsGrip = value; return this; }

    /// <summary>Sets the grip color (same as the property setter).</summary>
    public SkUiDragger SetGripColor(Color value) { ArgumentNullException.ThrowIfNull(value); GripColor = value; return this; }

    /// <summary>Sets the grip alignment (same as the property setter).</summary>
    public SkUiDragger SetGripAlignment(LayoutAlignment value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), value, null);
        GripAlignment = value;
        return this;
    }

    /// <summary>Sets the grip length (same as the property setter; below 0: the look's).</summary>
    public SkUiDragger SetGripLength(double value) { SkUiValidate.ThrowIfNotFinite(value, nameof(value)); GripLength = value; return this; }

    /// <summary>Sets whether the line shows (same as the property setter).</summary>
    public SkUiDragger SetShowsLine(bool value) { ShowsLine = value; return this; }

    /// <summary>Sets the line color (same as the property setter).</summary>
    public SkUiDragger SetLineColor(Color value) { ArgumentNullException.ThrowIfNull(value); LineColor = value; return this; }

    /// <summary>Sets the keyboard increment (same as the property setter).</summary>
    public SkUiDragger SetKeyboardIncrement(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        KeyboardIncrement = value;
        return this;
    }

    /// <summary>Sets the touch thickness (same as the property setter; below 0: the look's).</summary>
    public SkUiDragger SetTouchThickness(double value) { SkUiValidate.ThrowIfNotFinite(value, nameof(value)); TouchThickness = value; return this; }

    private void OnTouchThicknessChanged(double value)
    {
        _touchThickness = value;
        InvalidateRender(SkUiRenderDirty.Props);
        InvalidatePaint();
    }

    private void OnPaintChanged<T>(ref T field, T value)
    {
        field = value;
        InvalidatePaint();
    }

    /// <summary>The input engine shared with <see cref="Core.SkUiCoreDragger"/>, created on first use.</summary>
    private SkUiDraggerModel Model => field ??= new SkUiDraggerModel(this);

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
        SkUiLook.Current.DrawDragger(canvas, SkUiDraggerMath.Paint(new Size(Width, Height), EffectiveDragAxis, _touchThickness, IsRightToLeft, _showsGrip,
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
        IsEnabled && SkUiDraggerMath.HitsTouchArea(new Size(Width, Height), EffectiveDragAxis, _touchThickness, SkUiLookScope.Resolve(this), local) && CanDrag;

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
