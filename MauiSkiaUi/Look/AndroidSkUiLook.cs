namespace MauiSkiaUi;

/// <summary>
/// A look that mimics Android (Material): <see cref="DefaultSkUiLook"/> with Android's choices where it has them. It draws the
/// same on every platform; <see cref="DefaultSkUiLook.ForCurrentPlatform"/> picks it on Android.
/// </summary>
/// <remarks>
/// So far: picker fields underlined (<see cref="SkUiPickerFieldStyle.Underline"/>, Material's text field) and buttons that ripple
/// when pressed (<see cref="SkUiPressEffect.Ripple"/>). Settable options (<see cref="DefaultSkUiLook.PressEffect"/>,
/// <see cref="DefaultSkUiLook.PickerFieldStyle"/>) still override it.
/// </remarks>
public class AndroidSkUiLook : DefaultSkUiLook
{
    /// <summary>A shared instance (set it as <see cref="SkUiLook.Current"/>).</summary>
    public new static AndroidSkUiLook Instance { get; } = new();

    /// <summary>Creates the look: buttons ripple when pressed.</summary>
    public AndroidSkUiLook() => PressEffect = SkUiPressEffect.Ripple;

    /// <inheritdoc />
    /// <remarks>Material's underlined field.</remarks>
    public override SkUiPickerFieldStyle DefaultPickerFieldStyle => SkUiPickerFieldStyle.Underline;
}
