namespace MauiSkiaUi;

/// <summary>
/// A look that mimics macOS: <see cref="DefaultSkUiLook"/> with the Mac's choices where it has them. It draws the same on every
/// platform; <see cref="DefaultSkUiLook.ForCurrentPlatform"/> picks it on Mac Catalyst.
/// </summary>
/// <remarks>
/// So far: bounce overscroll, an inline pull-to-refresh indicator and (as the default look) outlined picker fields whose lists open anchored to the field, as a
/// pop-up button's menu. The Mac's own control shapes and sizes come with the platform looks phase
/// (docs/design/ImplementationPlan.md).
/// </remarks>
public class MacSkUiLook : DefaultSkUiLook
{
    /// <summary>A shared instance (set it as <see cref="SkUiLook.Current"/>).</summary>
    public new static MacSkUiLook Instance { get; } = new();

    /// <inheritdoc />
    /// <remarks>Apple's rubber-band bounce.</remarks>
    public override SkUiOverscrollMode BaseOverscroll => SkUiOverscrollMode.Bounce;

    /// <inheritdoc />
    /// <remarks>An inline indicator above the content (UIRefreshControl).</remarks>
    public override SkUiRefreshStyle BaseRefreshStyle => SkUiRefreshStyle.Inline;

    /// <inheritdoc />
    /// <remarks>The Mac's spring-like curve.</remarks>
    protected override SkUiTransition GetTransitionCore(SkUiTransitionKind kind) => kind switch
    {
        SkUiTransitionKind.Switch => SkUiTransition.FromMilliseconds(300, SkUiEasings.IosStandard),
        SkUiTransitionKind.IndicatorPosition => SkUiTransition.FromMilliseconds(300, SkUiEasings.IosStandard),
        SkUiTransitionKind.PickerOpen => SkUiTransition.FromMilliseconds(350, SkUiEasings.IosStandard),
        SkUiTransitionKind.Release => SkUiTransition.FromMilliseconds(250, Easing.CubicOut),
        _ => base.GetTransitionCore(kind)
    };

    /// <inheritdoc />
    /// <remarks>The Mac's smaller switch.</remarks>
    public override Size DefaultSwitchSize => new(38, 22);

    /// <inheritdoc />
    public override Size DefaultCheckBoxSize => new(14, 14);

    /// <inheritdoc />
    public override float GetCheckBoxCornerRadius(float size) => 3;

    /// <inheritdoc />
    public override float CheckBoxBorderWidth => 1;

    /// <inheritdoc />
    public override Size DefaultRadioButtonSize => new(14, 14);

    /// <inheritdoc />
    public override double DefaultButtonMinimumHeight => 28;

    /// <inheritdoc />
    public override double DefaultSliderThickness => 24;

    /// <inheritdoc />
    public override float SliderThumbRadius => 8;

    /// <inheritdoc />
    public override bool SliderThumbHasShadow => true;

    /// <inheritdoc />
    /// <remarks>The Mac's focus halo is thicker.</remarks>
    public override float FocusRingThickness => 3;
}
