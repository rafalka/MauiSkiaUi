namespace MauiSkiaUi;

/// <summary>
/// A look that mimics MAUI's Android controls as they draw by default, Material 2 (Material Components): <see cref="DefaultSkUiLook"/>
/// with Android's choices where it has them. It draws the same on every platform; <see cref="DefaultSkUiLook.ForCurrentPlatform"/>
/// picks it on Android unless the app turns on MAUI's Material 3 (<c>&lt;UseMaterial3&gt;true&lt;/UseMaterial3&gt;</c>), which picks
/// <see cref="AndroidMaterial3SkUiLook"/>.
/// </summary>
/// <remarks>
/// Underlined picker and text fields (<see cref="SkUiPickerFieldStyle.Underline"/>, <see cref="SkUiTextFieldStyle.Underline"/>),
/// rippling presses, the stretch overscroll, Material 2's switch (a thin track under a round thumb), 18 dp check boxes,
/// 20 dp radio buttons, buttons with 4 dp corners that fill 36 of their 48 dp, a slider with a thin track and a small
/// thumb, 48 dp activity indicators, 48 dp menu rows and 14 sp text. Settable options
/// (<see cref="DefaultSkUiLook.PressEffect"/>, <see cref="DefaultSkUiLook.PickerFieldStyle"/>, <see cref="DefaultSkUiLook.TextFieldStyle"/>)
/// still override it.
/// </remarks>
public class AndroidSkUiLook : DefaultSkUiLook
{
    /// <summary>A shared instance (set it as <see cref="SkUiLook.Current"/>).</summary>
    public new static AndroidSkUiLook Instance { get; } = new();

    /// <summary>The MAUI feature switch <c>&lt;UseMaterial3&gt;</c> sets.</summary>
    private const string Material3Switch = "Microsoft.Maui.RuntimeFeature.IsMaterial3Enabled";

    /// <summary>Whether the app turned on MAUI's Material 3 Android controls (<c>&lt;UseMaterial3&gt;true&lt;/UseMaterial3&gt;</c>).</summary>
    public static bool IsMaterial3Enabled => AppContext.TryGetSwitch(Material3Switch, out var enabled) && enabled;

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
    /// <remarks>Material 2's standard curve.</remarks>
    protected override SkUiTransition GetTransitionCore(SkUiTransitionKind kind) => kind switch
    {
        SkUiTransitionKind.Switch => SkUiTransition.FromMilliseconds(250, SkUiEasings.Standard),
        SkUiTransitionKind.CheckBox or SkUiTransitionKind.RadioButton => SkUiTransition.FromMilliseconds(150, SkUiEasings.Standard),
        SkUiTransitionKind.IndicatorPosition => SkUiTransition.FromMilliseconds(250, SkUiEasings.Standard),
        SkUiTransitionKind.PickerOpen => SkUiTransition.FromMilliseconds(225, SkUiEasings.Standard),
        _ => base.GetTransitionCore(kind)
    };

    /// <inheritdoc />
    /// <remarks>Material 2's switch: a 23 × 14 dp track under a 20 dp thumb, centered in 48 × 48 dp.</remarks>
    public override Size DefaultSwitchSize => new(48, 48);

    /// <inheritdoc />
    public override SkUiSwitchStyle SwitchStyle => SkUiSwitchStyle.ThumbOverTrack;

    /// <inheritdoc />
    public override float SwitchTrackHeight => 14;

    /// <inheritdoc />
    /// <remarks>Material's 18 dp box, centered in 32 dp.</remarks>
    public override Size DefaultCheckBoxSize => new(32, 32);

    /// <inheritdoc />
    public override float CheckBoxInset => 7;

    /// <inheritdoc />
    public override float GetCheckBoxCornerRadius(float size) => 2;

    /// <inheritdoc />
    public override float CheckBoxBorderWidth => 2;

    /// <inheritdoc />
    /// <remarks>A 20 dp ring centered in 32 dp, the text right after it.</remarks>
    public override Size DefaultRadioButtonSize => new(32, 32);

    /// <inheritdoc />
    public override float RadioButtonInset => 6;

    /// <inheritdoc />
    public override double DefaultRadioButtonContentSpacing => 0;

    /// <inheritdoc />
    public override float RadioRingFraction => 0.1f;

    /// <inheritdoc />
    public override float RadioDotRadiusFraction => 0.25f;

    /// <inheritdoc />
    /// <remarks>Material's 36 dp button, 16 dp from the text on each side.</remarks>
    public override double DefaultButtonMinimumHeight => 36;

    /// <inheritdoc />
    public override Thickness DefaultButtonPadding => new(16, 8);

    /// <inheritdoc />
    public override double DefaultButtonCornerRadius => 4;

    /// <inheritdoc />
    /// <remarks>Android's seek bar: a 2 dp track and a 12 dp thumb, 16 dp in from each end.</remarks>
    public override float SliderThumbRadius => 6;

    /// <inheritdoc />
    public override float SliderThumbTravelInset => 16;

    /// <inheritdoc />
    public override float SliderTrackHeight => 2;

    /// <inheritdoc />
    public override float FieldCornerRadius => 4;

    /// <inheritdoc />
    /// <remarks>An EditText's padding; MAUI's picker has no arrow.</remarks>
    public override Thickness PickerFieldPadding => new(4, 10, 4, 11);

    /// <inheritdoc />
    public override double PickerChevronWidth => 0;

    /// <inheritdoc />
    public override double DefaultPickerMinimumHeight => 0;

    /// <inheritdoc />
    public override Thickness TextFieldPadding => new(4, 10, 4, 11);

    /// <inheritdoc />
    public override double DefaultTextFieldMinimumHeight => 0;

    /// <inheritdoc />
    /// <remarks>A horizontal progress bar: a 4 dp bar centered in 16 dp.</remarks>
    public override double DefaultProgressBarHeight => 16;

    /// <inheritdoc />
    public override float ProgressBarTrackHeight => 4;

    /// <inheritdoc />
    /// <remarks>Android's progress bar: 48 dp with the ring 4 dp in.</remarks>
    public override Size DefaultActivityIndicatorSize => new(48, 48);

    /// <inheritdoc />
    public override float ActivityIndicatorInset => 4;

    /// <inheritdoc />
    public override float GetActivityIndicatorStrokeWidth(float diameter) => 4;

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
    /// <remarks>A TextView's font padding: Roboto's lines are 18.7 high at 14 sp, not 16.4.</remarks>
    public override bool TextIncludesFontPadding => true;

    /// <inheritdoc />
    /// <remarks>Android's body text, 14 sp (the system font is Roboto).</remarks>
    public override double DefaultFontSize => 14;

    /// <inheritdoc />
    public override SkUiColorScheme GetColorScheme(bool isDark) => isDark ? AndroidSkUiColorScheme.Dark : AndroidSkUiColorScheme.Light;
}
