using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>Which state change a <see cref="SkUiTransition"/> animates (<see cref="SkUiLook.GetTransition"/>).</summary>
public enum SkUiTransitionKind
{
    /// <summary>A switch moves to another <see cref="SkUiCheckState"/>.</summary>
    Switch,
    /// <summary>A check box moves to another <see cref="SkUiCheckState"/>.</summary>
    CheckBox,
    /// <summary>A radio button moves to another <see cref="SkUiCheckState"/>.</summary>
    RadioButton,
    /// <summary>A control is pressed: <see cref="SkUiPressVisual.Pressed"/> goes towards 1.</summary>
    Press,
    /// <summary>A press ends: <see cref="SkUiPressVisual.Pressed"/> goes back to 0 and <see cref="SkUiPressVisual.RippleFade"/> to 1.</summary>
    Release,
    /// <summary>A press ripple spreads from the press point: <see cref="SkUiPressVisual.Ripple"/> goes from 0 to 1.</summary>
    Ripple,
    /// <summary>A slider thumb jumps to a tapped value.</summary>
    SliderThumb,
    /// <summary>A determinate progress bar's fill follows a new <c>Progress</c> (<c>ProgressTo</c> animates on its own).</summary>
    Progress
}

/// <summary>
/// How a look animates one kind of state change: <see cref="Duration"/> and <see cref="Easing"/>. A zero duration
/// (<see cref="None"/>) means no transition: the control shows the new state at once.
/// </summary>
/// <param name="Duration">Length of the full transition; interrupted transitions reverse in proportion.</param>
/// <param name="Easing">Curve applied to the progress (default linear). Symmetric curves reverse exactly.</param>
public readonly record struct SkUiTransition(TimeSpan Duration, Easing? Easing = null)
{
    /// <summary>No transition.</summary>
    public static SkUiTransition None => default;

    /// <summary>Whether this is no transition.</summary>
    public bool IsNone => Duration <= TimeSpan.Zero;

    /// <summary>A transition of <paramref name="milliseconds"/> with <paramref name="easing"/>.</summary>
    public static SkUiTransition FromMilliseconds(double milliseconds, Easing? easing = null) =>
        new(TimeSpan.FromMilliseconds(milliseconds), easing);
}

/// <summary>
/// A toggle's visual state for its look: the state it shows (<see cref="State"/>), the state it comes from
/// (<see cref="From"/>) and how far the transition is (<see cref="Progress"/>, eased; 1 = settled). Use
/// <see cref="Weight"/> or the <c>Blend</c> helpers to draw any point of the transition.
/// </summary>
/// <param name="State">The control's state (the transition's target).</param>
/// <param name="From">The state the transition started from (<see cref="State"/> when settled).</param>
/// <param name="Progress">0 shows <see cref="From"/>, 1 shows <see cref="State"/>.</param>
/// <param name="Pressed">How pressed the control is, 0–1 (animated by the look's press / release transitions).</param>
public readonly record struct SkUiToggleVisual(SkUiCheckState State, SkUiCheckState From, float Progress, float Pressed = 0)
{
    /// <summary>A settled visual showing <paramref name="state"/>.</summary>
    public static SkUiToggleVisual Settled(SkUiCheckState state, float pressed = 0) => new(state, state, 1, pressed);

    /// <summary>Whether no transition is in progress.</summary>
    public bool IsSettled => From == State || Progress >= 1;

    /// <summary>How much of <paramref name="state"/> is shown, 0–1 (the weights of the three states add up to 1).</summary>
    public float Weight(SkUiCheckState state)
    {
        if (IsSettled)
            return state == State ? 1 : 0;
        var progress = Math.Clamp(Progress, 0, 1);
        return (state == State ? progress : 0) + (state == From ? 1 - progress : 0);
    }

    /// <summary>A number per state, blended by the transition (e.g. a thumb position).</summary>
    public float Blend(float whenUnchecked, float whenChecked, float whenIndeterminate) =>
        whenUnchecked * Weight(SkUiCheckState.Unchecked)
        + whenChecked * Weight(SkUiCheckState.Checked)
        + whenIndeterminate * Weight(SkUiCheckState.Indeterminate);

    /// <summary>A color per state, blended by the transition (e.g. a track color).</summary>
    public SKColor Blend(SKColor whenUnchecked, SKColor whenChecked, SKColor whenIndeterminate)
    {
        var u = Weight(SkUiCheckState.Unchecked);
        var c = Weight(SkUiCheckState.Checked);
        var i = Weight(SkUiCheckState.Indeterminate);
        static byte Channel(float a, float b, float c) => (byte)Math.Clamp(Math.Round(a + b + c), 0, 255);
        return new SKColor(
            Channel(whenUnchecked.Red * u, whenChecked.Red * c, whenIndeterminate.Red * i),
            Channel(whenUnchecked.Green * u, whenChecked.Green * c, whenIndeterminate.Green * i),
            Channel(whenUnchecked.Blue * u, whenChecked.Blue * c, whenIndeterminate.Blue * i),
            Channel(whenUnchecked.Alpha * u, whenChecked.Alpha * c, whenIndeterminate.Alpha * i));
    }
}

/// <summary>
/// A pressable control's press feedback for its look: how pressed it is and, for ripples, where the press started
/// and how far the ripple has spread and faded.
/// </summary>
/// <param name="Pressed">0 (released) to 1 (pressed), animated by the look's press / release transitions.</param>
/// <param name="Origin">Where the last press started, in the control's coordinates.</param>
/// <param name="Ripple">How far the last press's ripple has spread, 0–1 (it keeps spreading after a release).</param>
/// <param name="RippleFade">How far the ripple has faded after the release, 0 (visible) to 1 (gone).</param>
public readonly record struct SkUiPressVisual(float Pressed, SKPoint Origin, float Ripple, float RippleFade)
{
    /// <summary>No press feedback.</summary>
    public static SkUiPressVisual None => new(0, default, 0, 1);

    /// <summary>A settled press (<c>true</c>: fully pressed at <paramref name="origin"/>).</summary>
    public static SkUiPressVisual Settled(bool pressed, SKPoint origin = default) =>
        pressed ? new(1, origin, 1, 0) : None;

    /// <summary>Whether a ripple is visible.</summary>
    public bool HasRipple => Ripple > 0 && RippleFade < 1;
}

/// <summary>Press feedback style of <see cref="DefaultSkUiLook"/> buttons.</summary>
public enum SkUiPressEffect
{
    /// <summary>The fill fades while pressed (iOS-like).</summary>
    Dim,
    /// <summary>A ripple spreads from the press point and fades after the release (Material-like).</summary>
    Ripple
}
