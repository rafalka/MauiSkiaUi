namespace MauiSkiaUi;

/// <summary>
/// A drawn horizontal stack that places its children by their <c>HorizontalOptions</c>, not by their place in the list:
/// Start children stacked from the start side, End children from the end side, and Center (and Fill) children in a row
/// placed by <see cref="CenterStrategy"/>, by default centered in the layout whatever is at the sides (a bar's title next to
/// a close button). With Fill children, the row takes the whole room between the sides and they share what is left of it (a
/// search field between a back and a menu button). The side children are measured first and the centered ones get the width
/// left, so a long title truncates instead of overlapping them. Mirrored right to left. Same engine as
/// <see cref="Core.SkUiCoreHorizontalAlignStackLayout"/>.
/// </summary>
public class SkUiHorizontalAlignStackLayout : SkUiLayout
{
    private double _spacing;
    private SkUiCenterStrategy _centerStrategy;
    private SkUiAlignStackState _state;

    /// <summary>Bindable gap between children in DIPs (also the least gap between the sides and the centered children).</summary>
    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(nameof(Spacing), typeof(double), typeof(SkUiHorizontalAlignStackLayout), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiHorizontalAlignStackLayout)view).OnSpacingChanged((double)value));

    /// <summary>Bindable placement of the centered children; default <see cref="SkUiCenterStrategy.Parent"/>.</summary>
    public static readonly BindableProperty CenterStrategyProperty = BindableProperty.Create(nameof(CenterStrategy), typeof(SkUiCenterStrategy),
        typeof(SkUiHorizontalAlignStackLayout), SkUiCenterStrategy.Parent,
        validateValue: SkUiValidate.CenterStrategy,
        propertyChanged: (view, _, value) => ((SkUiHorizontalAlignStackLayout)view).OnCenterStrategyChanged((SkUiCenterStrategy)value));

    /// <summary>Gap between children in DIPs.</summary>
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>Where the Center children go (a row with Fill children spans the room between the sides); default <see cref="SkUiCenterStrategy.Parent"/>.</summary>
    public SkUiCenterStrategy CenterStrategy { get => (SkUiCenterStrategy)GetValue(CenterStrategyProperty); set => SetValue(CenterStrategyProperty, value); }

    /// <summary>Sets <see cref="Spacing"/> (same as the property setter).</summary>
    public SkUiHorizontalAlignStackLayout SetSpacing(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); Spacing = value; return this; }

    /// <summary>Sets <see cref="CenterStrategy"/> (same as the property setter).</summary>
    public SkUiHorizontalAlignStackLayout SetCenterStrategy(SkUiCenterStrategy value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown center strategy.");
        CenterStrategy = value;
        return this;
    }

    private void OnSpacingChanged(double value) { _spacing = value; InvalidateMeasureOverride(); }
    private void OnCenterStrategyChanged(SkUiCenterStrategy value) { _centerStrategy = value; InvalidateMeasureOverride(); }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var children = new SkUiViewChildren(Children);
        return SkUiAlignStackEngine.Measure(ref children, ref _state, _centerStrategy, widthConstraint, heightConstraint, EffectivePadding, _spacing);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var children = new SkUiViewChildren(Children);
        SkUiAlignStackEngine.Arrange(ref children, ref _state, _centerStrategy, size, EffectivePadding, _spacing);
    }
}
