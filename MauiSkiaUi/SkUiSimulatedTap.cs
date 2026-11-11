namespace MauiSkiaUi;

/// <summary>
/// What a synthetic tap from <see cref="SkUiDiagnostics.SimulateTapAt"/> or
/// <see cref="SkUiDiagnostics.SimulateTap(object, bool)"/> did, so automation can report it truthfully: a tap on a label
/// inside a scroller, on a disabled control or on a control without a tap is delivered but taken by nothing.
/// </summary>
/// <param name="Hit">The deepest drawn element at the tapped point; <c>null</c> when nothing was pressed (outside the surface, clipped away, not on a surface).</param>
/// <param name="Winner">
/// When the press was released, the recognizer that won it. When it was cancelled, the one that would have taken a release
/// without movement: the arena's winner, else its innermost member that stays for the sweep (pans, scrollers, swipes and long
/// presses that have not claimed give such a release up), else the one leading the press (a scroller around a label). From
/// <see cref="SkUiDiagnostics.SimulateTap(object, bool)"/> it can be another element's recognizer that would have taken the
/// press. <c>null</c> when no drawn recognizer took part (a disabled control, plain drawn content, a native view) or,
/// after a release, every one gave up.
/// </param>
/// <param name="IsTap">
/// <see cref="Winner"/> completes on release: a tap recognizer, a control's own tap, a slider or stepper tap, a text field.
/// A scroller, pan or swipe winning a press that does not move is not a tap.
/// </param>
/// <param name="PassedToNative">
/// A real touch would be left to the native views around the surface: a MAUI <c>TapGestureRecognizer</c> on a parent of
/// the surface, or a native view hosted on it (<see cref="SkUiMauiContentView"/>, which is not pressed then). On iOS and Mac
/// Catalyst that is when no drawn tap is pending and no drawn gesture claimed the touch; on Android and Windows, when the
/// surface did not take the press at all (a disabled control or a scroller around a label take it without a tap).
/// </param>
/// <param name="Released">The press was released (<c>false</c>: cancelled, or never pressed), so the winner's action ran.</param>
public readonly record struct SkUiSimulatedTap(
    IVisualTreeElement? Hit,
    SkUiGestureRecognizer? Winner,
    bool IsTap,
    bool PassedToNative,
    bool Released);
