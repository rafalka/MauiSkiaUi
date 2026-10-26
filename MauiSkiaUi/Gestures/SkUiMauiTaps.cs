using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MauiSkiaUi;

/// <summary>
/// The MAUI <see cref="TapGestureRecognizer"/> bridge: drawn taps run the tap recognizers in a drawn view's
/// <c>GestureRecognizers</c> and in its label spans' <c>Span.GestureRecognizers</c>, as MAUI does: a recognizer runs for a
/// sequence of its <c>NumberOfTapsRequired</c> taps made with a button in its <c>Buttons</c> (a touch is the primary
/// button). Swipes, pans, pinches and pointers run on <see cref="SkUiMauiGestures"/>, and other recognizers are reported
/// once per view (<see cref="ReportUnsupported"/>).
/// </summary>
internal static class SkUiMauiTaps
{
    /// <summary>Whether <paramref name="recognizers"/> has a tap recognizer for single (1) or double (2) taps.</summary>
    internal static bool Has(IList<IGestureRecognizer> recognizers, int taps)
    {
        for (var index = 0; index < recognizers.Count; index++)
            if (IsBridged(recognizers[index], taps))
                return true;
        return false;
    }

    private static bool IsBridged(IGestureRecognizer recognizer, int taps) => IsBridged(recognizer) && ((TapGestureRecognizer)recognizer).NumberOfTapsRequired == taps;

    private static bool IsBridged(IGestureRecognizer recognizer) =>
        recognizer is TapGestureRecognizer { NumberOfTapsRequired: > 0 } tap && (tap.Buttons & (ButtonsMask.Primary | ButtonsMask.Secondary)) != 0;

    /// <summary>The most taps a tap recognizer of <paramref name="recognizers"/> requires; 0 without one.</summary>
    internal static int MaxTaps(IList<IGestureRecognizer> recognizers)
    {
        var most = 0;
        for (var index = 0; index < recognizers.Count; index++)
            if (IsBridged(recognizers[index]))
                most = Math.Max(most, ((TapGestureRecognizer)recognizers[index]).NumberOfTapsRequired);
        return most;
    }

    /// <summary>Whether a tap recognizer of <paramref name="recognizers"/> takes presses with <paramref name="buttons"/>.</summary>
    internal static bool Takes(IList<IGestureRecognizer> recognizers, SkUiPointerButtons buttons)
    {
        var button = ButtonOf(buttons);
        for (var index = 0; index < recognizers.Count; index++)
            if (IsBridged(recognizers[index]) && (((TapGestureRecognizer)recognizers[index]).Buttons & button) != 0)
                return true;
        return false;
    }

    /// <summary>MAUI's button of a press: a touch, the primary or no button reported is <see cref="ButtonsMask.Primary"/>.</summary>
    private static ButtonsMask ButtonOf(SkUiPointerButtons buttons) =>
        buttons.HasFlag(SkUiPointerButtons.Secondary) ? ButtonsMask.Secondary
        : buttons is SkUiPointerButtons.None or SkUiPointerButtons.Primary ? ButtonsMask.Primary : 0;

    /// <summary>
    /// Raises the tap recognizers of <paramref name="recognizers"/> that want <paramref name="taps"/> taps of the pressed
    /// <paramref name="buttons"/>, as MAUI does: the command (when it can execute), then <see cref="TapGestureRecognizer.Tapped"/>
    /// with <paramref name="sender"/> as the sender and <paramref name="position"/> (in <paramref name="sender"/>'s
    /// coordinates) from <see cref="TappedEventArgs.GetPosition"/>.
    /// </summary>
    internal static void Raise(IList<IGestureRecognizer> recognizers, int taps, View sender, Point position, SkUiPointerButtons buttons)
    {
        if (!Has(recognizers, taps))
            return;
        var button = ButtonOf(buttons);
        Point? PositionRelativeTo(IElement? element) => ReferenceEquals(element, sender) ? position : null;
        foreach (var recognizer in recognizers.ToArray())
        {
            if (!IsBridged(recognizer, taps) || (((TapGestureRecognizer)recognizer).Buttons & button) == 0)
                continue;
            var tap = (TapGestureRecognizer)recognizer;
            if (CanSendTapped(sender))
                SendTapped(tap, sender, PositionRelativeTo);
            else if (tap.Command is { } command && command.CanExecute(tap.CommandParameter))
                command.Execute(tap.CommandParameter); // a MAUI version without the internal SendTapped: the command at least
        }
    }

    private static bool? _canSendTapped;

    /// <summary>
    /// Whether this MAUI has the internal <c>SendTapped</c>: probed once on a new recognizer (no command, no handlers, so no
    /// app code runs), so a <see cref="MissingMethodException"/> from an app's command or handler is never taken for it.
    /// </summary>
    private static bool CanSendTapped(View sender)
    {
        if (_canSendTapped is { } known)
            return known;
        try
        {
            SendTapped(new TapGestureRecognizer(), sender, null);
            _canSendTapped = true;
        }
        catch (MissingMethodException)
        {
            _canSendTapped = false;
        }
        return _canSendTapped.Value;
    }

    // MAUI raises Tapped only from its internal SendTapped (command, then the event); the accessor is trimming- and
    // Native-AOT-safe, unlike reflection.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SendTapped")]
    private static extern void SendTapped(TapGestureRecognizer recognizer, View sender, Func<IElement?, Point?>? getPosition);

    /// <summary>
    /// Writes a trace warning for MAUI gesture input <paramref name="view"/> has but does not run: recognizers other than the
    /// bridged taps, and platform behaviors (e.g. the toolkit's <c>TouchBehavior</c>), which attach to a native view a drawn
    /// child does not have. On a surface root (<paramref name="native"/>) MAUI runs the recognizers itself, on the native
    /// surface and outside the drawn gesture arena, so they are reported too. Returns whether anything was reported.
    /// </summary>
    internal static bool ReportUnsupported(View view, bool native)
    {
        var reported = false;
        var recognizers = view.GestureRecognizers;
        for (var index = 0; index < recognizers.Count; index++)
        {
            var recognizer = recognizers[index];
            if (native)
                reported |= Warn(view, recognizer, "runs natively on the surface, outside the drawn gesture arena: drawn controls under it do not stop it. Use the view's Tapped / TappedCommand instead");
            else if (!IsBridged(recognizer) && !SkUiMauiGestures.Runs(recognizer))
                reported |= Warn(view, recognizer, recognizer switch
                {
                    TapGestureRecognizer => "is not run: it requires no taps, or no primary or secondary button",
                    PanGestureRecognizer => "is not run: drawn pans follow one pointer (TouchPoints 1)",
                    _ => "is not run: drawn views run MAUI's tap, swipe, pan, pinch and pointer recognizers; drag and drop needs a native view (host it in an SkUiMauiContentView)",
                });
        }
        // Reading Behaviors creates the collection: only when it exists.
        if (!native && view.IsSet(VisualElement.BehaviorsProperty))
            foreach (var behavior in view.Behaviors)
                if (IsPlatformBehavior(behavior.GetType()))
                    reported |= Warn(view, behavior, "is not run: platform behaviors attach to a native view, which drawn views do not have. Use the view's Tapped / TappedCommand, LongPressedCommand and ShowsPressEffect or visual states instead");
        return reported;
    }

    private static bool IsPlatformBehavior(Type? type)
    {
        for (; type is not null; type = type.BaseType)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PlatformBehavior<,>))
                return true;
        return false;
    }

    private static bool Warn(View view, object input, string problem)
    {
        var name = string.IsNullOrEmpty(view.AutomationId) ? view.GetType().Name : $"{view.GetType().Name} '{view.AutomationId}'";
        Trace.WriteLine($"SkiaUi: {input.GetType().Name} on {name} {problem}.");
        return true;
    }
}
