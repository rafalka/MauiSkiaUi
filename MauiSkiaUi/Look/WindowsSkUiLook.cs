namespace MauiSkiaUi;

/// <summary>
/// A look that mimics MAUI's Windows controls (WinUI 3, Fluent): <see cref="DefaultSkUiLook"/> with Windows' choices where it
/// has them. It draws the same on every platform; <see cref="DefaultSkUiLook.ForCurrentPlatform"/> picks it on Windows.
/// </summary>
/// <remarks>
/// Measured against MAUI's native controls on Windows 11: Fluent's 40 × 20 outlined toggle in its 40 epx row, 20 epx check
/// boxes and radio buttons in WinUI's 120 × 32 (the radio button's ring fills around a white dot), 32 epx buttons, text boxes
/// and combo boxes with 4 epx corners, a slider whose white thumb holds an accent dot, a 3 epx progress bar on a 1 epx line,
/// a trackless progress ring, 32 epx combo box items, 14 epx Segoe UI Variable text, and OK before Cancel in popups.
/// </remarks>
public class WindowsSkUiLook : DefaultSkUiLook
{
    /// <summary>A shared instance (set it as <see cref="SkUiLook.Current"/>).</summary>
    public new static WindowsSkUiLook Instance { get; } = new();

    /// <inheritdoc />
    /// <remarks>Windows' dialogs put the primary button first.</remarks>
    public override bool PopupAcceptButtonFirst => true;

    /// <inheritdoc />
    /// <remarks>Fluent's quick, decelerating motion.</remarks>
    protected override SkUiTransition GetTransitionCore(SkUiTransitionKind kind) => kind switch
    {
        SkUiTransitionKind.Switch => SkUiTransition.FromMilliseconds(167, SkUiEasings.FluentDecelerate),
        SkUiTransitionKind.CheckBox or SkUiTransitionKind.RadioButton => SkUiTransition.FromMilliseconds(83, SkUiEasings.FluentDecelerate),
        SkUiTransitionKind.Press => SkUiTransition.FromMilliseconds(83, Easing.Linear),
        SkUiTransitionKind.Release => SkUiTransition.FromMilliseconds(167, SkUiEasings.FluentDecelerate),
        SkUiTransitionKind.PickerOpen => SkUiTransition.FromMilliseconds(167, SkUiEasings.FluentDecelerate),
        _ => base.GetTransitionCore(kind)
    };

    /// <inheritdoc />
    /// <remarks>MAUI's toggle switch: Fluent's 40 × 20 outlined track centered in 40 × 40, a 12 epx thumb 10 epx from its ends.</remarks>
    public override Size DefaultSwitchSize => new(40, 40);

    /// <inheritdoc />
    public override float SwitchTrackHeight => 20;

    /// <inheritdoc />
    public override SkUiSwitchStyle SwitchStyle => SkUiSwitchStyle.Outlined;

    /// <inheritdoc />
    /// <remarks>
    /// WinUI's check box: the 20 epx box 6 epx inside a 32 epx row at least 120 wide (its MinWidth, which a
    /// <c>MinimumWidthRequest</c> replaces).
    /// </remarks>
    public override Size DefaultCheckBoxSize => new(32, 32);

    /// <inheritdoc />
    public override double DefaultCheckBoxMinimumWidth => 120;

    /// <inheritdoc />
    public override float CheckBoxInset => 6;

    /// <inheritdoc />
    public override float GetCheckBoxCornerRadius(float size) => 4;

    /// <inheritdoc />
    public override float CheckBoxBorderWidth => 1;

    /// <inheritdoc />
    /// <remarks>Fluent's check glyph: thin and smaller than the box.</remarks>
    public override float CheckMarkStrokeFraction => 0.065f;

    /// <inheritdoc />
    public override float CheckMarkScale => 0.8f;

    /// <inheritdoc />
    /// <remarks>
    /// WinUI's radio button: a 20 epx ring at the start of a 32 epx row at least 120 wide (its MinWidth, which a
    /// <c>MinimumWidthRequest</c> replaces), 8 epx from the text; checked, the ring fills with the accent around a 12 epx white dot.
    /// </remarks>
    public override Size DefaultRadioButtonSize => new(20, 20);

    /// <inheritdoc />
    public override Thickness RadioButtonPadding => new(0, 6);

    /// <inheritdoc />
    public override double DefaultRadioButtonMinimumWidth => 120;

    /// <inheritdoc />
    public override SkUiRadioButtonStyle RadioButtonStyle => SkUiRadioButtonStyle.Filled;

    /// <inheritdoc />
    public override float RadioRingFraction => 0.05f;

    /// <inheritdoc />
    public override float RadioDotRadiusFraction => 0.3f;

    /// <inheritdoc />
    /// <remarks>A standard button: 32 epx, Fluent's 11, 5, 11, 6 padding inside its 1 epx border.</remarks>
    public override double DefaultButtonMinimumHeight => 32;

    /// <inheritdoc />
    public override Thickness DefaultButtonPadding => new(12, 6, 12, 7);

    /// <inheritdoc />
    public override double DefaultButtonCornerRadius => 4;

    /// <inheritdoc />
    /// <remarks>Fluent's elevation border: 1 epx, darker along the bottom.</remarks>
    public override double DefaultButtonBorderWidth => 1;

    /// <inheritdoc />
    public override bool ButtonHasElevationBorder => true;

    /// <inheritdoc />
    /// <remarks>Fluent's slider: a 4 epx track as long as the slider and a 20 epx white thumb around a 10 epx accent dot.</remarks>
    public override float SliderTrackInset => 0;

    /// <inheritdoc />
    public override float SliderThumbInnerRadius => 5;

    /// <inheritdoc />
    /// <remarks>Fluent's progress bar: a 3 epx fill over a 1 epx track line.</remarks>
    public override double DefaultProgressBarHeight => 3;

    /// <inheritdoc />
    public override float ProgressBarTrackLineHeight => 1;

    /// <inheritdoc />
    /// <remarks>Fluent's combo box: 32 epx, the text 12 epx in and a 38 epx column for the chevron.</remarks>
    public override SkUiPickerFieldStyle DefaultPickerFieldStyle => SkUiPickerFieldStyle.ComboBox;

    /// <inheritdoc />
    public override double DefaultPickerMinimumHeight => 32;

    /// <inheritdoc />
    public override Thickness PickerFieldPadding => new(12, 5, 0, 7);

    /// <inheritdoc />
    public override double PickerChevronWidth => 38;

    /// <inheritdoc />
    /// <remarks>Fluent's text box: 32 epx, 10, 5, 6, 6 padding inside its 1 epx border.</remarks>
    public override SkUiTextFieldStyle DefaultTextFieldStyle => SkUiTextFieldStyle.TextBox;

    /// <inheritdoc />
    public override double DefaultTextFieldMinimumHeight => 32;

    /// <inheritdoc />
    public override Thickness TextFieldPadding => new(11, 6, 7, 7);

    /// <inheritdoc />
    public override float FieldCornerRadius => 4;

    /// <inheritdoc />
    /// <remarks>
    /// MAUI's stepper on Windows is two 32 epx buttons; SkiaUi keeps its segmented stepper, the same size.
    /// </remarks>
    public override Size StepperButtonSize => new(32, 32);

    /// <inheritdoc />
    public override double DefaultStepperCornerRadius => 4;

    /// <inheritdoc />
    /// <remarks>Fluent's indeterminate progress ring: 32 epx, an arc without a track.</remarks>
    public override SkUiActivityIndicatorStyle ActivityIndicatorStyle => SkUiActivityIndicatorStyle.Ring;

    /// <inheritdoc />
    public override Size DefaultActivityIndicatorSize => new(32, 32);

    /// <inheritdoc />
    /// <remarks>Fluent's thin scroll bars grow to 6 when the pointer is over them.</remarks>
    public override double ScrollBarThickness => 2;

    /// <inheritdoc />
    public override double ScrollBarExpandedThickness => 6;

    /// <inheritdoc />
    /// <remarks>Fluent's combo box items are 32 high and the list sits on its field.</remarks>
    public override double PopupItemHeight => 32;

    /// <inheritdoc />
    public override Thickness PopupItemPadding => new(12, 5);

    /// <inheritdoc />
    public override double PopupAnchorGap => 0;

    /// <inheritdoc />
    /// <remarks>Fluent's flyouts and dialogs (a combo box's list, Windows 11): 8 epx; bottom panes square.</remarks>
    public override CornerRadius GetPopupCornerRadius(SkUiPopupPlacement placement) =>
        placement == SkUiPopupPlacement.BottomPane ? default : new CornerRadius(8);

    /// <inheritdoc />
    /// <remarks>Fluent's flyout shadow: light and short (about 10 epx around a combo box's list, a little more below).</remarks>
    public override Shadow? GetPopupShadow(SkUiPopupPlacement placement) =>
        new() { Brush = Brush.Black, Opacity = 0.06f, Radius = 8, Offset = new Point(0, 2) };

    /// <inheritdoc />
    /// <remarks>Fluent's body text, 14 epx.</remarks>
    public override double DefaultFontSize => 14;

    /// <inheritdoc />
    /// <remarks>
    /// Segoe UI Variable's text design, by the family name DirectWrite lists for Skia (the name "Segoe UI Variable" matches
    /// the static Segoe UI); Windows 10 and headless runs, without it, use the system font.
    /// </remarks>
    public override string? DefaultFontFamily => "Segoe UI Variable Text";

    /// <inheritdoc />
    /// <remarks>DirectWrite draws Segoe UI Variable at the optical size of its size in points (its axis: 5 to 36).</remarks>
    public override bool AppliesOpticalSize => true;

    /// <inheritdoc />
    /// <remarks>DirectWrite's optical size is in points: 0.75 of the size in epx (14 epx text is at 10.5).</remarks>
    public override double OpticalSizeScale => 0.75;

    /// <inheritdoc />
    public override SkUiColorScheme GetColorScheme(bool isDark) => isDark ? WindowsSkUiColorScheme.Dark : WindowsSkUiColorScheme.Light;
}
