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
        Reveal = null;
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

    /// <summary>
    /// A channel driven by a scroll-direction <paramref name="reveal"/>: its progress, 0 (shown) to 1 (hidden), maps onto
    /// <paramref name="shown"/>…<paramref name="hidden"/> instead of an offset; with <paramref name="step"/>, it jumps from one
    /// to the other halfway (reduced motion).
    /// </summary>
    public SkUiRenderLink(SkUiRenderReveal reveal, SkUiRenderLinkTarget target, float shown, float hidden, float[]? curve = null,
        SkUiRenderLinkClipEdge clipEdge = SkUiRenderLinkClipEdge.Start, SkUiRenderLinkClipShape clipShape = SkUiRenderLinkClipShape.Rectangle,
        float clipCornerRadius = 0, bool step = false)
        : this(reveal.Source, reveal.Horizontal, target, step ? 0.5f : 0, step ? 0.5f : 1, shown, hidden, step ? null : curve, clipEdge, clipShape,
            clipCornerRadius) =>
        Reveal = reveal;

    /// <summary>
    /// A channel that is piecewise linear in the offset: at <paramref name="inputs"/>[i] (ascending) the output is
    /// <paramref name="outputs"/>[i], linear between neighbors and held outside the first and last. A tab view's indicator
    /// uses it to hit each tab's position and width exactly while the panes move (no easing table can).
    /// </summary>
    public SkUiRenderLink(SkUiRenderNode source, bool horizontal, SkUiRenderLinkTarget target, float[] inputs, float[] outputs,
        SkUiRenderLinkClipEdge clipEdge = SkUiRenderLinkClipEdge.Start, SkUiRenderLinkClipShape clipShape = SkUiRenderLinkClipShape.Rectangle)
        : this(source, horizontal, target, inputs.Length > 0 ? inputs[0] : 0, inputs.Length > 0 ? inputs[^1] : 0,
            outputs.Length > 0 ? outputs[0] : 0, outputs.Length > 0 ? outputs[^1] : 0, null, clipEdge, clipShape)
    {
        if (inputs.Length != outputs.Length || inputs.Length == 0)
            throw new ArgumentException("A piecewise channel needs as many outputs as inputs, at least one.", nameof(outputs));
        Inputs = inputs;
        Outputs = outputs;
    }

    public SkUiRenderNode Source { get; }

    /// <summary>A piecewise channel's break points (ascending offsets), or <c>null</c>.</summary>
    public float[]? Inputs { get; }

    /// <summary>A piecewise channel's outputs at <see cref="Inputs"/>.</summary>
    public float[]? Outputs { get; }

    /// <summary>The scroll-direction input that drives the channel instead of the offset; <c>null</c> = the offset.</summary>
    public SkUiRenderReveal? Reveal { get; }

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
        if (Inputs is { } inputs && Outputs is { } outputs)
            return Piecewise(inputs, outputs, input);
        float progress;
        if (_inverseSpan == 0) // the step sentinel set in the constructor
            progress = input >= InputStart ? 1 : 0;
        else
            progress = Math.Clamp((input - InputStart) * _inverseSpan, 0, 1);
        if (Curve is { } curve)
            progress = Sample(curve, progress);
        return From + (To - From) * progress;
    }

    private static float Piecewise(float[] inputs, float[] outputs, float input)
    {
        if (input <= inputs[0])
            return outputs[0];
        var last = inputs.Length - 1;
        if (input >= inputs[last])
            return outputs[last];
        var index = 0;
        while (index < last - 1 && input >= inputs[index + 1])
            index++;
        var span = inputs[index + 1] - inputs[index];
        var fraction = span > 0 ? (input - inputs[index]) / span : 1;
        return outputs[index] + (outputs[index + 1] - outputs[index]) * fraction;
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

    /// <summary>
    /// UI side: the reveals' request counts (<see cref="SkUiScrollReveal.RequestStamp"/>), so a request changes the committed
    /// props and a frame carries it to the render thread. Not drawn.
    /// </summary>
    public int RevealRequests;

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
    /// The contribution of <paramref name="links"/> on the render thread, at their sources' current offsets (reveals advanced
    /// to <paramref name="now"/>). A channel whose source is gone (detached) drops out; when every source is gone,
    /// <paramref name="committed"/> (what the UI side last computed) is returned whole. <paramref name="moving"/> is set when a reveal's timed move runs (the
    /// frames must keep coming); reveals whose move ended are added to <paramref name="ended"/>.
    /// </summary>
    public static SkUiRenderLinkValues Evaluate(SkUiRenderLink[] links, in SkUiRenderLinkValues committed, TimeSpan now,
        List<SkUiRenderReveal>? ended, ref bool moving)
    {
        var values = Identity;
        var live = false;
        foreach (var link in links)
        {
            // A channel whose source is gone (detached) drops out; the others still follow theirs.
            if (link.Source.Disposed)
                continue;
            live = true;
            float input;
            if (link.Reveal is { } reveal)
            {
                input = reveal.Advance(now, out var endedNow);
                if (endedNow && ended is not null && !ended.Contains(reveal))
                    ended.Add(reveal);
                moving |= reveal.IsMoving;
            }
            else
            {
                input = link.Input(link.Source.Props);
            }
            values.Add(link.Target, link.Evaluate(input));
        }
        // Every source gone: what the UI side last computed, so the node stays where it was drawn.
        return live || links.Length == 0 ? values : committed;
    }

    /// <inheritdoc cref="Evaluate(SkUiRenderLink[], in SkUiRenderLinkValues, TimeSpan, List{SkUiRenderReveal}?, ref bool)" />
    public static SkUiRenderLinkValues Evaluate(SkUiRenderLink[] links, in SkUiRenderLinkValues committed)
    {
        var moving = false;
        return Evaluate(links, committed, TimeSpan.Zero, null, ref moving);
    }

    /// <summary>
    /// The contribution of <paramref name="links"/> on the UI side, all driven by one scroller at the given offset (reveals:
    /// the progress last drawn).
    /// </summary>
    public static SkUiRenderLinkValues Evaluate(SkUiRenderLink[] links, float offsetX, float offsetY)
    {
        var values = Identity;
        foreach (var link in links)
            values.Add(link.Target, link.Evaluate(link.Reveal is { } reveal ? reveal.Progress : link.Horizontal ? offsetX : offsetY));
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
        // An axis no channel clips is not clipped (what the node draws outside its bounds stays), unless the clip has rounded
        // corners, which need its edges there: the node's own. On a clipped axis the clip may be larger than the node (a
        // header growing while pulled is not cut at its layout size).
        var square = shaped is null;
        var clipsX = edgeX is not null;
        var clipsY = edgeY is not null;
        var w = clipsX ? values.ClipWidth : square ? width + 2 * Unbounded : width;
        var h = clipsY ? values.ClipHeight : square ? height + 2 * Unbounded : height;
        if (w <= 0 || h <= 0)
            return true; // clipped away: an empty rectangle
        var x = clipsX ? Place(edgeX!.Value, width, w) : square ? -Unbounded : 0;
        var y = clipsY ? Place(edgeY!.Value, height, h) : square ? -Unbounded : 0;
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

    /// <summary>How far past the node an axis that no channel clips reaches (DIPs): in effect, not clipped.</summary>
    private const float Unbounded = 1_000_000;

    /// <summary>
    /// The round rectangle a shadow cast by an outline (its <paramref name="bounds"/> and corner radius,
    /// <see cref="SkUiRenderShadow.OutlineCornerRadius"/>) takes under this clip; <c>false</c> when the clip leaves the
    /// outline whole (draw the shadow as it is) or hides it. Allocates nothing.
    /// </summary>
    public bool TryGetOutlineShadow(SKRect bounds, float outlineRadius, out SKRect rect, out float radius, out bool hidden)
    {
        rect = Rect;
        radius = 0;
        hidden = !rect.IntersectsWith(bounds);
        if (hidden || rect.Contains(bounds))
            return false;
        rect.Intersect(bounds);
        // At least the outline's own corners (a rounded card keeps them where the clip does not cut).
        radius = Math.Min(Math.Max(Radius, outlineRadius), Math.Min(rect.Width, rect.Height) / 2);
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
