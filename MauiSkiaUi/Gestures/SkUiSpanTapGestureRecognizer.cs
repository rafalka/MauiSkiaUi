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
