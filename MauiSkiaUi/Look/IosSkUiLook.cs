namespace MauiSkiaUi;

/// <summary>
/// A look that mimics iOS: <see cref="DefaultSkUiLook"/> with iOS's choices where it has them. It draws the same on every
/// platform; <see cref="DefaultSkUiLook.ForCurrentPlatform"/> picks it on iPhones and iPads.
/// </summary>
/// <remarks>
/// So far: bounce overscroll, an inline pull-to-refresh indicator, picker fields outlined (a text field's rounded border, as the default look) and, on phones, the picker's list in a
/// sheet at the bottom of the window, where iOS shows its picker wheel (<see cref="SkUiPopupPlacement.Window"/> popups fill the
/// width and align to the bottom of the safe area).
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
    /// <remarks>iOS has no check box: a circle with a check mark.</remarks>
    public override Size DefaultCheckBoxSize => new(22, 22);

    /// <inheritdoc />
    public override float GetCheckBoxCornerRadius(float size) => size / 2;

    /// <inheritdoc />
    public override float CheckBoxBorderWidth => 1;

    /// <inheritdoc />
    public override Size DefaultRadioButtonSize => new(22, 22);

    /// <inheritdoc />
    public override double DefaultButtonCornerRadius => 10;

    /// <inheritdoc />
    /// <remarks>A large white thumb with a soft shadow on a thin track.</remarks>
    public override float SliderThumbRadius => 14;

    /// <inheritdoc />
    public override double DefaultSliderThickness => 36;

    /// <inheritdoc />
    public override bool SliderThumbHasShadow => true;

    /// <inheritdoc />
    public override float FieldCornerRadius => 8;

    /// <inheritdoc />
    public override SkUiActivityIndicatorStyle ActivityIndicatorStyle => SkUiActivityIndicatorStyle.Spokes;

    /// <inheritdoc />
    public override Size DefaultActivityIndicatorSize => new(37, 37);

    /// <inheritdoc />
    /// <remarks>Thin indicators that do not grow.</remarks>
    public override double ScrollBarThickness => 3;

    /// <inheritdoc />
    public override double ScrollBarExpandedThickness => 3;
}
