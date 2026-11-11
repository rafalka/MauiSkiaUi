namespace MauiSkiaUi;

/// <summary>
/// What a synthetic tap from <see cref="SkUiDiagnostics.SimulateTapAt"/> or
/// <see cref="SkUiDiagnostics.SimulateTap(object, bool)"/> did, so automation can report it truthfully: a tap on a label
/// inside a scroller, on a disabled control or on a control without a tap is delivered but taken by nothing.
/// </summary>
/// <param name="Hit">The deepest drawn element at the tapped point; <c>null</c> when nothing was pressed (outside the surface, clipped away, not on a surface).</param>
/// <param name="Winner">
/// When the press was released, the recognizer that won it; when it was cancelled, the one leading the press then (the
/// arena's winner, else its innermost member: a scroller around a label). <c>null</c> when no drawn recognizer took part (a disabled control,
/// plain drawn content) or, after a release, every one gave up (a scroller resigns a release that did not move).
/// </param>
/// <param name="IsTap">
/// <see cref="Winner"/> completes on release: a tap recognizer, a control's own tap, a slider or stepper tap, a text field.
/// A scroller, pan or swipe winning a press that does not move is not a tap.
/// </param>
/// <param name="PassedToNative">
/// Nothing drawn took the tap: a real touch would be left to native ancestors of the surface (a MAUI
/// <c>TapGestureRecognizer</c> on a parent of the surface recognizes it).
/// </param>
/// <param name="Released">The press was released (<c>false</c>: cancelled, or never pressed), so the winner's action ran.</param>
public readonly record struct SkUiSimulatedTap(
    IVisualTreeElement? Hit,
    SkUiGestureRecognizer? Winner,
    bool IsTap,
    bool PassedToNative,
    bool Released);
