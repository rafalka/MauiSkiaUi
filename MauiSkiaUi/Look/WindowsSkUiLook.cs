namespace MauiSkiaUi;

/// <summary>
/// A look that mimics Windows (Fluent): <see cref="DefaultSkUiLook"/> with Windows' choices where it has them. It draws the same
/// on every platform; <see cref="DefaultSkUiLook.ForCurrentPlatform"/> picks it on Windows.
/// </summary>
/// <remarks>
/// So far the default look's choices match Windows' (outlined picker fields whose lists open anchored to the field, as a
/// combo box), except for the order of a popup's OK and Cancel: OK first, as Windows' dialogs. Fluent's own control shapes and
/// sizes come with the platform looks phase (docs/design/ImplementationPlan.md).
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
    /// <remarks>Fluent's 40 × 20 outlined toggle.</remarks>
    public override Size DefaultSwitchSize => new(40, 20);

    /// <inheritdoc />
    public override SkUiSwitchStyle SwitchStyle => SkUiSwitchStyle.Outlined;

    /// <inheritdoc />
    public override Size DefaultCheckBoxSize => new(20, 20);

    /// <inheritdoc />
    public override float GetCheckBoxCornerRadius(float size) => 4;

    /// <inheritdoc />
    public override float CheckBoxBorderWidth => 1;

    /// <inheritdoc />
    public override Size DefaultRadioButtonSize => new(20, 20);

    /// <inheritdoc />
    public override float RadioRingFraction => 0.05f;

    /// <inheritdoc />
    public override float RadioDotRadiusFraction => 0.25f;

    /// <inheritdoc />
    public override double DefaultButtonMinimumHeight => 32;

    /// <inheritdoc />
    public override double DefaultButtonCornerRadius => 4;
}
