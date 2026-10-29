namespace MauiSkiaUi;

/// <summary>
/// A look that mimics MAUI's iOS controls on iOS 26: <see cref="DefaultSkUiLook"/> with iOS's choices where it has them. It
/// draws the same on every platform; <see cref="DefaultSkUiLook.ForCurrentPlatform"/> picks it on iPhones and iPads, and
/// <see cref="MacSkUiLook"/> derives from it.
/// </summary>
/// <remarks>
/// iOS 26's switch (63 × 28 with a capsule thumb) and slider (a 6 pt track under a 37 × 24 capsule thumb), MAUI's round
/// 18 pt check box and 21 pt radio button, 20 pt spoke activity indicators, square button corners (a native button's
/// corners are square unless it sets a radius), outlined fields with 5 pt corners (a rounded text field's), bounce
/// overscroll, an inline pull-to-refresh indicator, 17 pt text and, on phones, the picker's list in a sheet at the bottom of
/// the window (<see cref="SkUiPopupPlacement.Window"/> popups fill the width and align to the bottom of the safe area).
/// </remarks>
public class IosSkUiLook : DefaultSkUiLook
{
    /// <summary>A shared instance (set it as <see cref="SkUiLook.Current"/>).</summary>
    public new static IosSkUiLook Instance { get; } = new();

    /// <inheritdoc />
    /// <remarks>Full width on phones (a sheet), centered elsewhere.</remarks>
    public override LayoutOptions PopupHorizontalOptions => IsPhone ? LayoutOptions.Fill : base.PopupHorizontalOptions;

    /// <inheritdoc />
    /// <remarks>At the bottom on phones (a sheet), centered elsewhere.</remarks>
    public override LayoutOptions PopupVerticalOptions => IsPhone ? LayoutOptions.End : base.PopupVerticalOptions;

    /// <inheritdoc />
    /// <remarks>A wheel, as iOS's picker (<c>UIPickerView</c>).</remarks>
    public override bool PickerPaneShowsWheel => true;

    /// <inheritdoc />
    /// <remarks>Apple's rubber-band bounce.</remarks>
    public override SkUiOverscrollMode BaseOverscroll => SkUiOverscrollMode.Bounce;

    /// <inheritdoc />
    /// <remarks>An inline indicator above the content (UIRefreshControl).</remarks>
    public override SkUiRefreshStyle BaseRefreshStyle => SkUiRefreshStyle.Inline;

    /// <inheritdoc />
    /// <remarks>iOS's spring-like curve.</remarks>
    protected override SkUiTransition GetTransitionCore(SkUiTransitionKind kind) => kind switch
    {
        SkUiTransitionKind.Switch => SkUiTransition.FromMilliseconds(300, SkUiEasings.IosStandard),
        SkUiTransitionKind.IndicatorPosition => SkUiTransition.FromMilliseconds(300, SkUiEasings.IosStandard),
        SkUiTransitionKind.PickerOpen => SkUiTransition.FromMilliseconds(350, SkUiEasings.IosStandard),
        SkUiTransitionKind.Release => SkUiTransition.FromMilliseconds(250, Easing.CubicOut),
        _ => base.GetTransitionCore(kind)
    };

    /// <inheritdoc />
    /// <remarks>iOS 26's switch: 63 × 28 with a 37 × 24 capsule thumb.</remarks>
    public override Size DefaultSwitchSize => new(63, 28);

    /// <inheritdoc />
    public override float SwitchThumbWidth => 37;

    /// <inheritdoc />
    /// <remarks>iOS has no check box; MAUI draws an 18 pt circle with a check mark.</remarks>
    public override Size DefaultCheckBoxSize => new(18, 18);

    /// <inheritdoc />
    public override float GetCheckBoxCornerRadius(float size) => size / 2;

    /// <inheritdoc />
    public override float CheckBoxBorderWidth => 1.5f;

    /// <inheritdoc />
    /// <remarks>MAUI's radio button template: a 21 pt ring 2 pt wide around an 11 pt dot, 6 pt from the content.</remarks>
    public override Size DefaultRadioButtonSize => new(21, 21);

    /// <inheritdoc />
    public override float RadioRingFraction => 2f / 21;

    /// <inheritdoc />
    public override float RadioDotRadiusFraction => 5.5f / 21;

    /// <inheritdoc />
    public override double DefaultRadioButtonContentSpacing => 6;

    /// <inheritdoc />
    /// <remarks>
    /// 7 pt on every side: MAUI's template lays out 41 pt around its text (the 21 pt circle, 6 pt to the text, 7 pt on
    /// each side), measured on iOS 26 against a label of the same text.
    /// </remarks>
    public override Thickness RadioButtonPadding => new(7);

    /// <inheritdoc />
    /// <remarks>A native button's corners are square unless it sets <c>CornerRadius</c>.</remarks>
    public override double DefaultButtonCornerRadius => 0;

    /// <inheritdoc />
    /// <remarks>A system button: 36 pt, 13.5 pt from the text on each side.</remarks>
    public override double DefaultButtonMinimumHeight => 36;

    /// <inheritdoc />
    public override Thickness DefaultButtonPadding => new(13.5, 7);

    /// <inheritdoc />
    /// <remarks>iOS 26's slider: a white 37 × 24 capsule thumb with a soft shadow over a 6 pt track as long as the slider.</remarks>
    public override float SliderThumbRadius => 12;

    /// <inheritdoc />
    public override float SliderThumbWidth => 37;

    /// <inheritdoc />
    public override float SliderThumbTravelInset => 18.5f;

    /// <inheritdoc />
    public override float SliderTrackInset => 0;

    /// <inheritdoc />
    public override float SliderTrackHeight => 6;

    /// <inheritdoc />
    public override bool SliderThumbHasShadow => true;

    /// <inheritdoc />
    public override double DefaultSliderThickness => 34;

    /// <inheritdoc />
    /// <remarks>A rounded text field's 5 pt corners.</remarks>
    public override float FieldCornerRadius => 5;

    /// <inheritdoc />
    /// <remarks>A rounded text field, 34 pt high; MAUI's picker has no arrow.</remarks>
    public override double DefaultTextFieldMinimumHeight => 34;

    /// <inheritdoc />
    public override Thickness TextFieldPadding => new(7, 6);

    /// <inheritdoc />
    public override double DefaultPickerMinimumHeight => 34;

    /// <inheritdoc />
    public override Thickness PickerFieldPadding => new(14, 6);

    /// <inheritdoc />
    public override double PickerChevronWidth => 0;

    /// <inheritdoc />
    public override SkUiActivityIndicatorStyle ActivityIndicatorStyle => SkUiActivityIndicatorStyle.Spokes;

    /// <inheritdoc />
    /// <remarks>MAUI's activity indicator is UIKit's medium one: 20 pt, 8 spokes.</remarks>
    public override Size DefaultActivityIndicatorSize => new(20, 20);

    /// <inheritdoc />
    public override int ActivityIndicatorSpokeCount => 8;

    /// <inheritdoc />
    /// <remarks>Thin indicators that do not grow.</remarks>
    public override double ScrollBarThickness => 3;

    /// <inheritdoc />
    public override double ScrollBarExpandedThickness => 3;

    /// <inheritdoc />
    /// <remarks>CoreText tracks text by the font's <c>trak</c> table.</remarks>
    public override bool AppliesFontTracking => true;

    /// <inheritdoc />
    /// <remarks>CoreText draws the system font at the optical size of its point size.</remarks>
    public override bool AppliesOpticalSize => true;

    /// <inheritdoc />
    /// <remarks>iOS's body text style, 17 pt, in the system font (SF).</remarks>
    public override double DefaultFontSize => 17;

    /// <inheritdoc />
    public override SkUiColorScheme GetColorScheme(bool isDark) => isDark ? IosSkUiColorScheme.Dark : IosSkUiColorScheme.Light;
}
