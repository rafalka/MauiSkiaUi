namespace MauiSkiaUi;

/// <summary>
/// A look that mimics MAUI's Android controls with Material 3 turned on (<c>&lt;UseMaterial3&gt;true&lt;/UseMaterial3&gt;</c>):
/// <see cref="AndroidSkUiLook"/> with Material 3's shapes, sizes and motion. It draws the same on every platform;
/// <see cref="DefaultSkUiLook.ForCurrentPlatform"/> picks it on Android when the app turns Material 3 on.
/// </summary>
/// <remarks>
/// Material 3's switch (52 × 32, an outlined track and a thumb that grows when on), fully rounded buttons, a slider with a
/// 16 dp track and a bar handle, progress bars with a gap and a stop dot, the pill page indicator and the emphasized curves.
/// </remarks>
public class AndroidMaterial3SkUiLook : AndroidSkUiLook
{
    /// <summary>A shared instance (set it as <see cref="SkUiLook.Current"/>).</summary>
    public new static AndroidMaterial3SkUiLook Instance { get; } = new();

    /// <inheritdoc />
    /// <remarks>Material 3's page indicator: the selected dot stretches into a pill.</remarks>
    public override SkUiIndicatorStyle BaseIndicatorStyle => SkUiIndicatorStyle.Pill;

    /// <inheritdoc />
    /// <remarks>Material 3's motion: the emphasized curves.</remarks>
    protected override SkUiTransition GetTransitionCore(SkUiTransitionKind kind) => kind switch
    {
        SkUiTransitionKind.Switch => SkUiTransition.FromMilliseconds(300, SkUiEasings.Emphasized),
        SkUiTransitionKind.CheckBox or SkUiTransitionKind.RadioButton => SkUiTransition.FromMilliseconds(100, SkUiEasings.Emphasized),
        SkUiTransitionKind.IndicatorPosition => SkUiTransition.FromMilliseconds(300, SkUiEasings.Emphasized),
        SkUiTransitionKind.PickerOpen => SkUiTransition.FromMilliseconds(250, SkUiEasings.EmphasizedDecelerate),
        SkUiTransitionKind.SegmentSelection => SkUiTransition.FromMilliseconds(200, SkUiEasings.Standard),
        _ => base.GetTransitionCore(kind)
    };

    /// <inheritdoc />
    /// <remarks>Material 3: a 52 × 32 track centered in 52 × 48, outlined while off, with a thumb that grows when on.</remarks>
    public override Size DefaultSwitchSize => new(52, 48);

    /// <inheritdoc />
    public override float SwitchTrackHeight => 32;

    /// <inheritdoc />
    public override SkUiSwitchStyle SwitchStyle => SkUiSwitchStyle.Outlined;

    /// <inheritdoc />
    public override float SwitchOffThumbRadiusFraction => 0.25f;

    /// <inheritdoc />
    public override float SwitchOnThumbRadiusFraction => 0.375f;

    /// <inheritdoc />
    public override float SwitchOutlineWidth => 2;

    /// <inheritdoc />
    /// <remarks>Material 3's check box: the 18 dp box centered in 48 × 32.</remarks>
    public override Size DefaultCheckBoxSize => new(48, 32);

    /// <inheritdoc />
    /// <remarks>Material 3's fully rounded button.</remarks>
    public override double DefaultButtonCornerRadius => 18;

    /// <inheritdoc />
    /// <remarks>Material 3's slider: a 16 dp track with a 4 × 44 dp bar handle.</remarks>
    public override SkUiSliderStyle SliderStyle => SkUiSliderStyle.Handle;

    /// <inheritdoc />
    public override float SliderTrackHeight => 16;

    /// <inheritdoc />
    public override Size SliderHandleSize => new(4, 44);

    /// <inheritdoc />
    public override float SliderHandleGap => 4;

    /// <inheritdoc />
    public override double DefaultSliderThickness => 48;

    /// <inheritdoc />
    /// <remarks>The track starts 14 dp in; the handle stops half the track's height inside its ends.</remarks>
    public override float SliderTrackInset => 14;

    /// <inheritdoc />
    public override float SliderThumbTravelInset => 22;

    /// <inheritdoc />
    /// <remarks>Material 3's linear indicator: a gap before the track and a stop dot at its end.</remarks>
    public override float ProgressBarGap => 4;

    /// <inheritdoc />
    public override bool ProgressBarHasStopIndicator => true;

    /// <inheritdoc />
    /// <remarks>A 4 dp linear indicator.</remarks>
    public override double DefaultProgressBarHeight => 4;

    /// <inheritdoc />
    public override float ProgressBarTrackHeight => 0;

    /// <inheritdoc />
    /// <remarks>Rounded ends, as Material 3's linear indicator (Material 2's are square).</remarks>
    public override float GetProgressBarCornerRadius(float height) => height / 2;

    /// <inheritdoc />
    /// <remarks>
    /// 28 dp for window popups, as the platform's picker dialog; 4 dp for popups at a field, as Material 3's menus; bottom
    /// panes square.
    /// </remarks>
    public override CornerRadius GetPopupCornerRadius(SkUiPopupPlacement placement) => placement switch
    {
        SkUiPopupPlacement.Window => new CornerRadius(28),
        SkUiPopupPlacement.BottomPane => default,
        _ => new CornerRadius(4)
    };

    /// <inheritdoc />
    /// <remarks>Material 3's circular indicator: 40 dp, the 4 dp track 4 dp in.</remarks>
    public override Size DefaultActivityIndicatorSize => new(40, 40);

    /// <inheritdoc />
    /// <remarks>MAUI's Material 3 entry is an outlined text field 58 dp high with the text 16 dp in.</remarks>
    public override SkUiTextFieldStyle DefaultTextFieldStyle => SkUiTextFieldStyle.Outline;

    /// <inheritdoc />
    public override Thickness TextFieldPadding => new(16, 19.8);

    /// <inheritdoc />
    public override SkUiColorScheme GetColorScheme(bool isDark) => isDark ? AndroidMaterial3SkUiColorScheme.Dark : AndroidMaterial3SkUiColorScheme.Light;

    // Calendars (1.15): Material 3's date picker grid. Days in 48 dp rows under 40 dp weekday labels, their 40 dp circles from
    // the default drawing; today an outlined 1 dp circle; a 56 dp header with 40 dp icon buttons and the 14 dp title.

    /// <inheritdoc />
    public override double CalendarRowHeight => 48;

    /// <inheritdoc />
    public override double CalendarDayNamesHeight => 40;

    /// <inheritdoc />
    public override double CalendarHeaderHeight => 56;

    /// <inheritdoc />
    public override double CalendarNavigationButtonSize => 40;

    /// <inheritdoc />
    public override double CalendarHeaderFontSize => 14;

    /// <inheritdoc />
    public override double CalendarDayNameFontSize => 14;

    /// <inheritdoc />
    protected override float CalendarTodayRingWidth => 1;

    /// <inheritdoc />
    /// <remarks>Material 3's segmented buttons: outlined, the selected one filled with a check before its label, fading.</remarks>
    public override SkUiSegmentedStyle SegmentedStyle => SkUiSegmentedStyle.Outlined;

    /// <inheritdoc />
    /// <remarks>40 dp high, fully rounded ends, 18 dp icons and checks 8 dp before the label.</remarks>
    public override SkUiSegmentedMetrics SegmentedMetrics => new(40, 0, 0, 12, 20, 18, 8, 18);

    /// <inheritdoc />
    /// <remarks>Material 3's primary tabs: an underline as wide as the label, rounded, and icons above the text.</remarks>
    public override SkUiTabStripStyle TabStripStyle => SkUiTabStripStyle.LabelUnderline;

    /// <inheritdoc />
    /// <remarks>48 dp with text headers, 64 with icons; a 3 dp underline.</remarks>
    public override SkUiTabStripMetrics TabStripMetrics => new(48, 64, 16, 3, 24, 2, true);
}
