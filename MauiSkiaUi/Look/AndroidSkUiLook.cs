namespace MauiSkiaUi;

/// <summary>
/// A look that mimics Android (Material): <see cref="DefaultSkUiLook"/> with Android's choices where it has them. It draws the
/// same on every platform; <see cref="DefaultSkUiLook.ForCurrentPlatform"/> picks it on Android.
/// </summary>
/// <remarks>
/// So far: overscroll that stretches, picker and text fields underlined (<see cref="SkUiPickerFieldStyle.Underline"/>, <see cref="SkUiTextFieldStyle.Underline"/>:
/// Material's text field) and buttons that ripple when pressed (<see cref="SkUiPressEffect.Ripple"/>). Settable options
/// (<see cref="DefaultSkUiLook.PressEffect"/>, <see cref="DefaultSkUiLook.PickerFieldStyle"/>, <see cref="DefaultSkUiLook.TextFieldStyle"/>)
/// still override it.
/// </remarks>
public class AndroidSkUiLook : DefaultSkUiLook
{
    /// <summary>A shared instance (set it as <see cref="SkUiLook.Current"/>).</summary>
    public new static AndroidSkUiLook Instance { get; } = new();

    /// <inheritdoc />
    /// <remarks>Material's underlined field.</remarks>
    public override SkUiPickerFieldStyle DefaultPickerFieldStyle => SkUiPickerFieldStyle.Underline;

    /// <inheritdoc />
    /// <remarks>Material's underlined field.</remarks>
    public override SkUiTextFieldStyle DefaultTextFieldStyle => SkUiTextFieldStyle.Underline;

    /// <inheritdoc />
    /// <remarks>Android 12's stretch.</remarks>
    public override SkUiOverscrollMode BaseOverscroll => SkUiOverscrollMode.Stretch;

    /// <inheritdoc />
    /// <remarks>Material's ripple.</remarks>
    public override SkUiPressEffect BasePressEffect => SkUiPressEffect.Ripple;

    /// <inheritdoc />
    /// <remarks>Material's page indicator: the selected dot stretches into a pill.</remarks>
    public override SkUiIndicatorStyle BaseIndicatorStyle => SkUiIndicatorStyle.Pill;

    /// <inheritdoc />
    /// <remarks>Material 3's motion: the emphasized curves.</remarks>
    protected override SkUiTransition GetTransitionCore(SkUiTransitionKind kind) => kind switch
    {
        SkUiTransitionKind.Switch => SkUiTransition.FromMilliseconds(300, SkUiEasings.Emphasized),
        SkUiTransitionKind.CheckBox or SkUiTransitionKind.RadioButton => SkUiTransition.FromMilliseconds(100, SkUiEasings.Emphasized),
        SkUiTransitionKind.IndicatorPosition => SkUiTransition.FromMilliseconds(300, SkUiEasings.Emphasized),
        SkUiTransitionKind.PickerOpen => SkUiTransition.FromMilliseconds(250, SkUiEasings.EmphasizedDecelerate),
        _ => base.GetTransitionCore(kind)
    };

    /// <inheritdoc />
    /// <remarks>Material 3: 52 × 32, an outlined track and a thumb that grows when on.</remarks>
    public override Size DefaultSwitchSize => new(52, 32);

    /// <inheritdoc />
    public override SkUiSwitchStyle SwitchStyle => SkUiSwitchStyle.Outlined;

    /// <inheritdoc />
    public override float SwitchOffThumbRadiusFraction => 0.25f;

    /// <inheritdoc />
    public override float SwitchOnThumbRadiusFraction => 0.375f;

    /// <inheritdoc />
    public override float SwitchOutlineWidth => 2;

    /// <inheritdoc />
    /// <remarks>Material's 18 dp box.</remarks>
    public override Size DefaultCheckBoxSize => new(18, 18);

    /// <inheritdoc />
    public override float GetCheckBoxCornerRadius(float size) => 2;

    /// <inheritdoc />
    public override float CheckBoxBorderWidth => 2;

    /// <inheritdoc />
    public override Size DefaultRadioButtonSize => new(20, 20);

    /// <inheritdoc />
    public override float RadioRingFraction => 0.1f;

    /// <inheritdoc />
    public override float RadioDotRadiusFraction => 0.25f;

    /// <inheritdoc />
    /// <remarks>Material's 40 dp button.</remarks>
    public override double DefaultButtonMinimumHeight => 40;

    /// <inheritdoc />
    /// <remarks>Material's fully rounded button.</remarks>
    public override double DefaultButtonCornerRadius => 20;

    /// <inheritdoc />
    public override float FieldCornerRadius => 4;

    /// <inheritdoc />
    public override Size DefaultActivityIndicatorSize => new(40, 40);

    /// <inheritdoc />
    /// <remarks>Thin bars that do not grow.</remarks>
    public override double ScrollBarThickness => 3;

    /// <inheritdoc />
    public override double ScrollBarExpandedThickness => 3;

    /// <inheritdoc />
    /// <remarks>Material's menu rows are 48 dp.</remarks>
    public override double PopupItemHeight => 48;

    /// <inheritdoc />
    public override Thickness PopupItemPadding => new(16, 12);

    /// <inheritdoc />
    /// <remarks>Material's body medium, 14 sp (the system font is Roboto).</remarks>
    public override double DefaultFontSize => 14;

    /// <inheritdoc />
    public override SkUiColorScheme GetColorScheme(bool isDark) => isDark ? AndroidSkUiColorScheme.Dark : AndroidSkUiColorScheme.Light;
}
