using System.Runtime.CompilerServices;

namespace MauiSkiaUi;

/// <summary>
/// Taps on the tappable spans of a label (MAUI <c>Span.GestureRecognizers</c>, Core <see cref="Core.SkUiCoreSpan.Tapped"/>).
/// It joins a pointer's arena only when pressed on such a span, so a press beside one leaves the label's own tap and its
/// ancestors' gestures as they are; the label collects it first, so a span wins the tap over the label. A tap is reported
/// when it is released on the span it was pressed on.
/// </summary>
internal sealed class SkUiSpanTapGestureRecognizer : SkUiTapGestureRecognizer
{
    /// <summary>The tappable span at a point in the label's coordinates, or -1.</summary>
    internal Func<Point, int>? TappableSpanAt { get; init; }

    /// <summary>The span the last press started on.</summary>
    internal int PressedSpan { get; private set; } = -1;

    /// <summary>The span under <paramref name="args"/>' release when it is the pressed one, or -1.</summary>
    internal int TappedSpan(SkUiTappedEventArgs args) =>
        SpanAt(args.Position) is var span && span == PressedSpan ? span : -1;

    private int SpanAt(Point point) => TappableSpanAt?.Invoke(point) ?? -1;

    /// <inheritdoc />
    protected internal override bool OnPointerPressed(SkUiPointer pointer)
    {
        if (Owner is not { } owner || SpanAt(pointer.GetPosition(owner)) is not (var span and >= 0))
            return false;
        if (!base.OnPointerPressed(pointer))
            return false;
        PressedSpan = span;
        return true;
    }
}

/// <summary>Raises MAUI <see cref="TapGestureRecognizer"/>s from drawn taps (span taps of <see cref="SkUiLabel.FormattedText"/>).</summary>
internal static class SkUiMauiTaps
{
    /// <summary>Whether <paramref name="span"/> has a tap recognizer for single (1) or double (2) taps.</summary>
    internal static bool Has(Span span, int taps)
    {
        foreach (var recognizer in span.GestureRecognizers)
            if (recognizer is TapGestureRecognizer tap && tap.NumberOfTapsRequired == taps && tap.Buttons.HasFlag(ButtonsMask.Primary))
                return true;
        return false;
    }

    /// <summary>
    /// Raises <paramref name="span"/>'s tap recognizers that want <paramref name="taps"/> taps, as MAUI does: the command
    /// (when it can execute), then <see cref="TapGestureRecognizer.Tapped"/> with <paramref name="sender"/> as the sender and
    /// <paramref name="position"/> (in <paramref name="sender"/>'s coordinates) from <see cref="TappedEventArgs.GetPosition"/>.
    /// Drawn taps are primary-button taps.
    /// </summary>
    internal static void Raise(Span span, int taps, View sender, Point position)
    {
        Point? PositionRelativeTo(IElement? element) => ReferenceEquals(element, sender) ? position : null;
        foreach (var recognizer in span.GestureRecognizers.ToArray())
        {
            if (recognizer is not TapGestureRecognizer tap || tap.NumberOfTapsRequired != taps || !tap.Buttons.HasFlag(ButtonsMask.Primary))
                continue;
            try
            {
                SendTapped(tap, sender, PositionRelativeTo);
            }
            catch (MissingMethodException)
            {
                // A MAUI version without the internal SendTapped: the command at least.
                if (tap.Command is { } command && command.CanExecute(tap.CommandParameter))
                    command.Execute(tap.CommandParameter);
            }
        }
    }

    // MAUI raises Tapped only from its internal SendTapped (command, then the event); the accessor is trimming- and
    // Native-AOT-safe, unlike reflection.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SendTapped")]
    private static extern void SendTapped(TapGestureRecognizer recognizer, View sender, Func<IElement?, Point?>? getPosition);
}
