using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Drawn row of indicators, one per item, with the current one selected (Core analogue of <c>SkUiIndicatorView</c>), drawn
/// by the look (<see cref="SkUiLook.DrawIndicators"/>). A tap on an indicator selects it; the selection moves with the look's
/// <see cref="SkUiTransitionKind.IndicatorPosition"/> transition. Drive it from a pager of your own with
/// <see cref="Position"/> and <see cref="Count"/>.
/// </summary>
public class SkUiCoreIndicatorView : SkUiCoreNode
{
    private readonly SkUiIndicatorModel _model;

    /// <summary>Creates an indicator view without items.</summary>
    public SkUiCoreIndicatorView() =>
        _model = new SkUiIndicatorModel(this) { IndicatorColor = SkUiColors.TrackOff, SelectedColor = SkUiColors.Accent };

    /// <summary>
    /// The selected item (0 = the first; default 0). A tap on an indicator sets it. Within the items: beyond the last it becomes
    /// the last (also when <see cref="Count"/> drops); without items it is kept.
    /// </summary>
    public int Position { get => _model.Position; set => SetPosition(value); }

    /// <summary>How many indicators there are (default 0).</summary>
    public int Count { get => _model.Count; set => SetCount(value); }

    /// <summary>Unselected indicators' color (default: the color scheme's <see cref="SkUiColorScheme.TrackOff"/> when created).</summary>
    public Color IndicatorColor { get => _model.IndicatorColor; set => SetIndicatorColor(value); }

    /// <summary>The selected indicator's color (default: the color scheme's accent when created).</summary>
    public Color SelectedIndicatorColor { get => _model.SelectedColor; set => SetSelectedIndicatorColor(value); }

    /// <summary>An indicator's size in DIPs (default 6).</summary>
    public double IndicatorSize { get => _model.IndicatorSize; set => SetIndicatorSize(value); }

    /// <summary>The indicators' shape: circles (default) or squares.</summary>
    public IndicatorShape IndicatorsShape { get => _model.Shape; set => SetIndicatorsShape(value); }

    /// <summary>The most indicators shown (default all): with more items, a window around the selected one shows.</summary>
    public int MaximumVisible { get => _model.MaximumVisible; set => SetMaximumVisible(value); }

    /// <summary>Whether a single indicator is hidden (default <c>true</c>; the node keeps its size).</summary>
    public bool HideSingle { get => _model.HideSingle; set => SetHideSingle(value); }

    /// <summary>The gap between indicators in DIPs; negative (default -1): the look's (<see cref="SkUiLook.DefaultIndicatorSpacing"/>).</summary>
    public double IndicatorSpacing { get => _model.Spacing; set => SetIndicatorSpacing(value); }

    /// <summary>The row's direction (default horizontal).</summary>
    public StackOrientation Orientation { get => _model.Orientation; set => SetOrientation(value); }

    /// <summary>Sets <see cref="Position"/>.</summary>
    public SkUiCoreIndicatorView SetPosition(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (_model.Count > 0 && value >= _model.Count)
            value = _model.Count - 1;
        var previous = _model.Position;
        if (!SetProperty(ref _model.Position, value, nameof(Position)))
            return this;
        _model.PositionChanged(previous);
        InvalidatePaint();
        InvalidateSemantics();
        return this;
    }

    /// <summary>Sets <see cref="Count"/>.</summary>
    public SkUiCoreIndicatorView SetCount(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _model.Count, value, nameof(Count)))
            return this;
        if (value > 0 && _model.Position >= value)
            SetPosition(value - 1);
        InvalidateMeasure();
        InvalidateSemantics();
        return this;
    }

    /// <summary>Sets <see cref="IndicatorColor"/>.</summary>
    public SkUiCoreIndicatorView SetIndicatorColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref _model.IndicatorColor, value, nameof(IndicatorColor)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="SelectedIndicatorColor"/>.</summary>
    public SkUiCoreIndicatorView SetSelectedIndicatorColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref _model.SelectedColor, value, nameof(SelectedIndicatorColor)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="IndicatorSize"/>.</summary>
    public SkUiCoreIndicatorView SetIndicatorSize(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        if (SetProperty(ref _model.IndicatorSize, value, nameof(IndicatorSize)))
            InvalidateMeasure();
        return this;
    }

    /// <summary>Sets <see cref="IndicatorsShape"/>.</summary>
    public SkUiCoreIndicatorView SetIndicatorsShape(IndicatorShape value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (SetProperty(ref _model.Shape, value, nameof(IndicatorsShape)))
            InvalidateMeasure();
        return this;
    }

    /// <summary>Sets <see cref="MaximumVisible"/>.</summary>
    public SkUiCoreIndicatorView SetMaximumVisible(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (SetProperty(ref _model.MaximumVisible, value, nameof(MaximumVisible)))
            InvalidateMeasure();
        return this;
    }

    /// <summary>Sets <see cref="HideSingle"/>.</summary>
    public SkUiCoreIndicatorView SetHideSingle(bool value)
    {
        if (SetProperty(ref _model.HideSingle, value, nameof(HideSingle)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="IndicatorSpacing"/>.</summary>
    public SkUiCoreIndicatorView SetIndicatorSpacing(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        if (SetProperty(ref _model.Spacing, value, nameof(IndicatorSpacing)))
            InvalidateMeasure();
        return this;
    }

    /// <summary>Sets <see cref="Orientation"/>.</summary>
    public SkUiCoreIndicatorView SetOrientation(StackOrientation value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (SetProperty(ref _model.Orientation, value, nameof(Orientation)))
            InvalidateMeasure();
        return this;
    }

    /// <summary>The selection drawn now (tests).</summary>
    internal double DisplayedPosition => _model.Displayed;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => _model.Measure(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        _model.Draw(canvas, (float)Frame.Width, (float)Frame.Height, IsRightToLeft, enabled: true);

    /// <inheritdoc />
    internal override bool HasIntrinsicTap => !_model.IsHidden && _model.Count > 1;

    /// <inheritdoc />
    internal override void OnIntrinsicTap(SkUiTappedEventArgs args)
    {
        var index = _model.HitTest(args.Position, (float)Frame.Width, (float)Frame.Height, IsRightToLeft);
        if (index >= 0)
            SetPosition(index);
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        InvalidatePaint();
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        _model.PopulateSemantics(info);
    }

    /// <inheritdoc />
    protected override bool OnSemanticsAction(SkUiSemanticsActions action)
    {
        var target = _model.SemanticsTarget(action);
        if (target < 0)
            return base.OnSemanticsAction(action);
        SetPosition(target);
        return true;
    }

    /// <inheritdoc />
    protected override bool OnSemanticsSetValue(double value)
    {
        var target = _model.SemanticsValue(value);
        if (target < 0 || target == _model.ClampedPosition)
            return false;
        SetPosition(target);
        return true;
    }

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;
}
