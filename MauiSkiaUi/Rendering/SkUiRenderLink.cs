namespace MauiSkiaUi.Rendering;

/// <summary>The property a <see cref="SkUiRenderLink"/> drives.</summary>
internal enum SkUiRenderLinkTarget : byte
{
    TranslationX,
    TranslationY,
    ScaleX,
    ScaleY,

    /// <summary>Both scales.</summary>
    Scale,
    Opacity
}

/// <summary>
/// One scroll-linked channel (<see cref="SkUiRenderProps.Links"/>): <see cref="Source"/>'s children offset on one axis, mapped
/// from <see cref="InputStart"/>…<see cref="InputEnd"/> onto <see cref="From"/>…<see cref="To"/> (held outside the range,
/// optionally through a sampled curve) and combined into one property of the node. The compositor evaluates it every frame
/// against the source's current (possibly render-thread animated) offset, so a node placed, scaled or faded by a scroll
/// offset follows flings without UI-thread work; the UI side evaluates the same channels at its own offset into
/// <see cref="SkUiRenderProps.LinkValues"/> for hit-testing and the immediate painter. The source need not be an ancestor:
/// the compositor reads its props directly, and render-thread animations are ticked before any node is drawn.
/// </summary>
/// <remarks>Immutable; the node builds new channels only when what drives it changes, so unchanged commits reuse them.</remarks>
internal sealed class SkUiRenderLink
{
    private readonly float _inverseSpan;

    /// <param name="source">The scroller whose children offset drives the channel.</param>
    /// <param name="horizontal">Whether the horizontal offset drives it (else the vertical one).</param>
    /// <param name="target">The property it drives.</param>
    /// <param name="inputStart">The offset at which the output is <paramref name="from"/>.</param>
    /// <param name="inputEnd">The offset at which the output is <paramref name="to"/>; equal to the start: a step there.</param>
    /// <param name="from">The output at and before <paramref name="inputStart"/>.</param>
    /// <param name="to">The output at and after <paramref name="inputEnd"/>.</param>
    /// <param name="curve">
    /// The easing sampled at equal steps from 0 to 1 (at least two points, first 0 and last 1 for a curve that starts and
    /// ends in place); <c>null</c> = linear. Shared between channels with the same easing, never changed.
    /// </param>
    public SkUiRenderLink(SkUiRenderNode source, bool horizontal, SkUiRenderLinkTarget target, float inputStart, float inputEnd,
        float from, float to, float[]? curve = null)
    {
        Source = source;
        Horizontal = horizontal;
        Target = target;
        InputStart = inputStart;
        InputEnd = inputEnd;
        From = from;
        To = to;
        Curve = curve is { Length: >= 2 } ? curve : null;
        // Exact comparison: equal ends are the step sentinel, not a computed value.
        _inverseSpan = inputEnd == inputStart ? 0 : 1 / (inputEnd - inputStart);
    }

    public SkUiRenderNode Source { get; }

    public bool Horizontal { get; }

    public SkUiRenderLinkTarget Target { get; }

    public float InputStart { get; }

    public float InputEnd { get; }

    public float From { get; }

    public float To { get; }

    public float[]? Curve { get; }

    /// <summary>The channel's input from its source's children offset.</summary>
    public float Input(in SkUiRenderProps source) => Horizontal ? source.ChildrenOffsetX : source.ChildrenOffsetY;

    /// <summary>The output for an input offset.</summary>
    public float Evaluate(float input)
    {
        float progress;
        if (_inverseSpan == 0) // the step sentinel set in the constructor
            progress = input >= InputStart ? 1 : 0;
        else
            progress = Math.Clamp((input - InputStart) * _inverseSpan, 0, 1);
        if (Curve is { } curve)
            progress = Sample(curve, progress);
        return From + (To - From) * progress;
    }

    /// <summary>A curve sampled at equal steps, read at <paramref name="progress"/> (0…1) with linear interpolation.</summary>
    internal static float Sample(float[] curve, float progress)
    {
        var position = progress * (curve.Length - 1);
        var index = Math.Clamp((int)position, 0, curve.Length - 2);
        var fraction = position - index;
        return curve[index] + (curve[index + 1] - curve[index]) * fraction;
    }
}

/// <summary>
/// What a node's <see cref="SkUiRenderProps.Links"/> add to its own values at one set of offsets: translations add, scales and
/// opacity multiply (the node's own values and render-thread animations of them stay as they are, and combine with these).
/// The compositor computes it each frame at the render thread's offsets; the UI side keeps it in
/// <see cref="SkUiRenderProps.LinkValues"/> at its own offsets.
/// </summary>
internal record struct SkUiRenderLinkValues
{
    public float TranslationX, TranslationY, ScaleX, ScaleY, Opacity;

    /// <summary>No contribution.</summary>
    public static SkUiRenderLinkValues Identity => new() { ScaleX = 1, ScaleY = 1, Opacity = 1 };

    /// <summary>Combines one channel's output.</summary>
    public void Add(SkUiRenderLinkTarget target, float value)
    {
        switch (target)
        {
            case SkUiRenderLinkTarget.TranslationX: TranslationX += value; break;
            case SkUiRenderLinkTarget.TranslationY: TranslationY += value; break;
            case SkUiRenderLinkTarget.ScaleX: ScaleX *= value; break;
            case SkUiRenderLinkTarget.ScaleY: ScaleY *= value; break;
            case SkUiRenderLinkTarget.Scale: ScaleX *= value; ScaleY *= value; break;
            case SkUiRenderLinkTarget.Opacity: Opacity *= value; break;
            default: throw new ArgumentOutOfRangeException(nameof(target));
        }
    }

    /// <summary>
    /// The contribution of <paramref name="links"/> on the render thread, at their sources' current offsets. A channel whose
    /// source is gone (detached) contributes what the UI side last computed: <paramref name="committed"/> is used whole then.
    /// </summary>
    public static SkUiRenderLinkValues Evaluate(SkUiRenderLink[] links, in SkUiRenderLinkValues committed)
    {
        var values = Identity;
        foreach (var link in links)
        {
            if (link.Source.Disposed)
                return committed;
            values.Add(link.Target, link.Evaluate(link.Input(link.Source.Props)));
        }
        return values;
    }

    /// <summary>The contribution of <paramref name="links"/> on the UI side, all driven by one scroller at the given offset.</summary>
    public static SkUiRenderLinkValues Evaluate(SkUiRenderLink[] links, float offsetX, float offsetY)
    {
        var values = Identity;
        foreach (var link in links)
            values.Add(link.Target, link.Evaluate(link.Horizontal ? offsetX : offsetY));
        return values;
    }
}
