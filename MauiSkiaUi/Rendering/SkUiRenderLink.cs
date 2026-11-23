using SkiaSharp;

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
    Opacity,

    /// <summary>Clips the node to this many DIPs across (<see cref="SkUiRenderLink.ClipEdge"/> says which part stays).</summary>
    ClipWidth,

    /// <summary>Clips the node to this many DIPs down.</summary>
    ClipHeight
}

/// <summary>Which part of a node a <see cref="SkUiRenderLinkTarget.ClipWidth"/> / <see cref="SkUiRenderLinkTarget.ClipHeight"/> keeps, physically (the UI side resolves leading and trailing).</summary>
internal enum SkUiRenderLinkClipEdge : byte
{
    /// <summary>The left or top part.</summary>
    Start,

    /// <summary>The right or bottom part.</summary>
    End,
    Center
}

/// <summary>The corners of a scroll-linked clip.</summary>
internal enum SkUiRenderLinkClipShape : byte
{
    Rectangle,

    /// <summary>Corners of <see cref="SkUiRenderLink.ClipCornerRadius"/> (at most half the clip's shorter side).</summary>
    RoundRectangle,

    /// <summary>Corners of half the clip's shorter side at every size: a pill stays a pill.</summary>
    Capsule
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
    /// <param name="clipEdge">For a clip: which part of the node stays.</param>
    /// <param name="clipShape">For a clip: its corners.</param>
    /// <param name="clipCornerRadius">For a <see cref="SkUiRenderLinkClipShape.RoundRectangle"/> clip: the corner radius.</param>
    public SkUiRenderLink(SkUiRenderNode source, bool horizontal, SkUiRenderLinkTarget target, float inputStart, float inputEnd,
        float from, float to, float[]? curve = null, SkUiRenderLinkClipEdge clipEdge = SkUiRenderLinkClipEdge.Start,
        SkUiRenderLinkClipShape clipShape = SkUiRenderLinkClipShape.Rectangle, float clipCornerRadius = 0)
    {
        ClipEdge = clipEdge;
        ClipShape = clipShape;
        ClipCornerRadius = Math.Max(0, clipCornerRadius);
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

    public SkUiRenderLinkClipEdge ClipEdge { get; }

    public SkUiRenderLinkClipShape ClipShape { get; }

    public float ClipCornerRadius { get; }

    /// <summary>Whether the channel clips the node.</summary>
    public bool IsClip => Target is SkUiRenderLinkTarget.ClipWidth or SkUiRenderLinkTarget.ClipHeight;

    /// <summary>The channel's input from its source's props: the scroll offset with the overscroll shown (<see cref="SkUiRenderProps.ScrollLinkInputX"/>).</summary>
    public float Input(in SkUiRenderProps source) => Horizontal ? source.ScrollLinkInputX : source.ScrollLinkInputY;

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
/// opacity multiply, clips take the smaller (the node's own values and render-thread animations of them stay as they are,
/// and combine with these).
/// The compositor computes it each frame at the render thread's offsets; the UI side keeps it in
/// <see cref="SkUiRenderProps.LinkValues"/> at its own offsets.
/// </summary>
internal record struct SkUiRenderLinkValues
{
    public float TranslationX, TranslationY, ScaleX, ScaleY, Opacity;

    /// <summary>The clip's size in DIPs on each axis; <see cref="float.PositiveInfinity"/> = not clipped on it.</summary>
    public float ClipWidth, ClipHeight;

    /// <summary>No contribution.</summary>
    public static SkUiRenderLinkValues Identity => new()
    {
        ScaleX = 1, ScaleY = 1, Opacity = 1, ClipWidth = float.PositiveInfinity, ClipHeight = float.PositiveInfinity
    };

    /// <summary>Whether a channel clips (on either axis).</summary>
    public readonly bool IsClipped => !float.IsPositiveInfinity(ClipWidth) || !float.IsPositiveInfinity(ClipHeight);

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
            case SkUiRenderLinkTarget.ClipWidth: ClipWidth = Math.Min(ClipWidth, Math.Max(0, value)); break;
            case SkUiRenderLinkTarget.ClipHeight: ClipHeight = Math.Min(ClipHeight, Math.Max(0, value)); break;
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

/// <summary>
/// A scroll-linked clip in a node's local coordinates (<see cref="SkUiRenderLinkTarget.ClipWidth"/> /
/// <see cref="SkUiRenderLinkTarget.ClipHeight"/>): it clips the node's content, children, overlay and shadow, never its layout.
/// </summary>
/// <param name="Rect">The part of the node that shows; empty when clipped away.</param>
/// <param name="Radius">The corner radius (0: square corners).</param>
internal readonly record struct SkUiRenderLinkClip(SKRect Rect, float Radius)
{
    /// <summary>
    /// The clip of <paramref name="links"/> at <paramref name="values"/> for a node <paramref name="width"/> ×
    /// <paramref name="height"/>; <c>false</c> when no channel clips. Each axis keeps the edge of the first channel that clips
    /// it; the corners come from the first clip channel with a shape other than a rectangle.
    /// </summary>
    public static bool TryGet(SkUiRenderLink[] links, in SkUiRenderLinkValues values, float width, float height, out SkUiRenderLinkClip clip)
    {
        clip = default;
        if (!values.IsClipped)
            return false;
        SkUiRenderLinkClipEdge? edgeX = null, edgeY = null;
        SkUiRenderLink? shaped = null;
        foreach (var link in links)
        {
            if (link.Target == SkUiRenderLinkTarget.ClipWidth)
                edgeX ??= link.ClipEdge;
            else if (link.Target == SkUiRenderLinkTarget.ClipHeight)
                edgeY ??= link.ClipEdge;
            else
                continue;
            if (shaped is null && link.ClipShape != SkUiRenderLinkClipShape.Rectangle)
                shaped = link;
        }
        var w = Math.Min(width, values.ClipWidth);
        var h = Math.Min(height, values.ClipHeight);
        if (w <= 0 || h <= 0)
            return true; // clipped away: an empty rectangle
        var x = Place(edgeX ?? SkUiRenderLinkClipEdge.Start, width, w);
        var y = Place(edgeY ?? SkUiRenderLinkClipEdge.Start, height, h);
        var half = Math.Min(w, h) / 2;
        var radius = shaped?.ClipShape switch
        {
            SkUiRenderLinkClipShape.Capsule => half,
            SkUiRenderLinkClipShape.RoundRectangle => Math.Min(half, shaped.ClipCornerRadius),
            _ => 0
        };
        clip = new SkUiRenderLinkClip(new SKRect(x, y, x + w, y + h), radius);
        return true;
    }

    private static float Place(SkUiRenderLinkClipEdge edge, float length, float kept) => edge switch
    {
        SkUiRenderLinkClipEdge.End => length - kept,
        SkUiRenderLinkClipEdge.Center => (length - kept) / 2,
        _ => 0
    };

    /// <summary>Whether nothing shows.</summary>
    public bool IsEmpty => Rect.IsEmpty;

    /// <summary>Whether <paramref name="point"/> (local) is inside it, its rounded corners included.</summary>
    public bool Contains(SKPoint point)
    {
        if (!Rect.Contains(point.X, point.Y))
            return false;
        if (Radius <= 0)
            return true;
        // In a corner square, inside the quarter circle only.
        var cx = Math.Clamp(point.X, Rect.Left + Radius, Rect.Right - Radius);
        var cy = Math.Clamp(point.Y, Rect.Top + Radius, Rect.Bottom - Radius);
        var dx = point.X - cx;
        var dy = point.Y - cy;
        return dx * dx + dy * dy <= Radius * Radius;
    }

    /// <summary>Clips <paramref name="canvas"/> to it; <paramref name="roundRect"/> is reused for rounded corners (no allocation per frame).</summary>
    public void Apply(SKCanvas canvas, SKRoundRect roundRect)
    {
        if (Radius <= 0)
        {
            canvas.ClipRect(Rect);
            return;
        }
        roundRect.SetRect(Rect, Radius, Radius);
        canvas.ClipRoundRect(roundRect, antialias: true);
    }
}
