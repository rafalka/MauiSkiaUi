using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Splits a label's text into the lines it paints, for <see cref="SkUiLabel.LineBreaker"/> and
/// <see cref="Core.SkUiCoreLabel.LineBreaker"/>. A custom breaker can shorten text its own way instead of the stock
/// ellipsis: a context-aware ellipsis ("Alice, Bob +3"), or a number shown with fewer decimals when it does not fit.
/// </summary>
/// <param name="context">The text, the available width and the label's settings, with measuring and the stock breaking
/// (<see cref="SkUiTextLineBreakContext.Break(string, LineBreakMode, string)"/>) to fall back to. Valid only during the call.</param>
/// <returns>One string per painted line, in order (an empty string paints a blank line). Each line is shaped as its own
/// paragraph; lines beyond the label's <c>MaxLines</c> are dropped.</returns>
public delegate IReadOnlyList<string> SkUiTextLineBreaker(SkUiTextLineBreakContext context);

/// <summary>
/// What a <see cref="SkUiTextLineBreaker"/> breaks, and the text engine's own measuring and breaking. Widths come
/// from the engine (shaping, font fallback, <c>CharacterSpacing</c>), so a line that <see cref="Fits"/> is drawn
/// without clipping. Use it only during the breaker call, on the UI thread.
/// </summary>
public sealed class SkUiTextLineBreakContext
{
    private readonly SkUiTextLayout _layout;

    internal SkUiTextLineBreakContext(SkUiTextLayout layout, string text, double availableWidth, int maxLines, LineBreakMode lineBreakMode, SKFont font)
    {
        _layout = layout;
        Text = text;
        AvailableWidth = availableWidth;
        MaxLines = maxLines;
        LineBreakMode = lineBreakMode;
        Font = font;
    }

    /// <summary>The label's text after its <c>TextTransform</c> (may contain newlines).</summary>
    public string Text { get; }

    /// <summary>
    /// Content width in DIPs (the label's width minus its padding); infinite when the label is measured unconstrained. A
    /// label that shrinks or grows its text (<c>ShrinkToFit</c>, <c>GrowToFill</c>) gives the width at the text's own size,
    /// the content width divided by the scale, as <see cref="Font"/> is at that size: the breaker sees the text unscaled.
    /// </summary>
    public double AvailableWidth { get; }

    /// <summary>The label's <c>MaxLines</c>: the most lines it paints, or 0 / negative for no limit.</summary>
    public int MaxLines { get; }

    /// <summary>The label's <c>LineBreakMode</c>, which <see cref="Break()"/> applies.</summary>
    public LineBreakMode LineBreakMode { get; }

    /// <summary>The label's primary font (typeface and size). Prefer <see cref="Measure"/>, which also counts shaping, fallback fonts and character spacing.</summary>
    public SKFont Font { get; }

    /// <summary>The label being laid out (<see cref="SkUiLabel"/> or <see cref="Core.SkUiCoreLabel"/>), e.g. to read its binding context.</summary>
    public object? Owner => _layout.Owner;

    /// <summary>Width in DIPs of <paramref name="text"/> drawn on one line (of its widest paragraph when it contains newlines).</summary>
    public double Measure(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _layout.MeasureUnbroken(text, Font);
    }

    /// <summary>Whether <paramref name="text"/> fits <see cref="AvailableWidth"/> on one line per paragraph.</summary>
    public bool Fits(string text) => double.IsInfinity(AvailableWidth) || Measure(text) <= AvailableWidth;

    /// <summary>The lines the label paints without a custom breaker: <see cref="Text"/> broken by <see cref="LineBreakMode"/> and <see cref="MaxLines"/>.</summary>
    public IReadOnlyList<string> Break() => Break(Text, LineBreakMode);

    /// <summary>
    /// Breaks <paramref name="text"/> as the stock engine does for <paramref name="mode"/> at <see cref="AvailableWidth"/>
    /// and <see cref="MaxLines"/>, truncating with <paramref name="ellipsis"/> instead of <c>"..."</c>.
    /// </summary>
    public IReadOnlyList<string> Break(string text, LineBreakMode mode, string ellipsis = SkUiTextLayout.DefaultEllipsis)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(ellipsis);
        return _layout.BreakToStrings(text, AvailableWidth, mode, MaxLines, ellipsis, Font);
    }
}

/// <summary>
/// Stock and composable <see cref="SkUiTextLineBreaker"/>s. The stock ones are stable instances (one per
/// <see cref="LineBreakMode"/>), and a label given one takes the engine's cached path, exactly as with that <c>LineBreakMode</c>.
/// </summary>
public static class SkUiTextLineBreakers
{
    /// <summary>One line per paragraph; no wrap or ellipsis.</summary>
    public static SkUiTextLineBreaker NoWrap { get; } = context => context.Break(context.Text, LineBreakMode.NoWrap);

    /// <summary>Wraps at word boundaries (after spaces and hyphens, between CJK ideographs) when possible.</summary>
    public static SkUiTextLineBreaker WordWrap { get; } = context => context.Break(context.Text, LineBreakMode.WordWrap);

    /// <summary>Wraps at grapheme boundaries.</summary>
    public static SkUiTextLineBreaker CharacterWrap { get; } = context => context.Break(context.Text, LineBreakMode.CharacterWrap);

    /// <summary>One line per paragraph with a leading ellipsis.</summary>
    public static SkUiTextLineBreaker HeadTruncation { get; } = context => context.Break(context.Text, LineBreakMode.HeadTruncation);

    /// <summary>One line per paragraph with a middle ellipsis.</summary>
    public static SkUiTextLineBreaker MiddleTruncation { get; } = context => context.Break(context.Text, LineBreakMode.MiddleTruncation);

    /// <summary>One line per paragraph with a trailing ellipsis; with <c>MaxLines</c>, wraps and ends the last line with it.</summary>
    public static SkUiTextLineBreaker TailTruncation { get; } = context => context.Break(context.Text, LineBreakMode.TailTruncation);

    /// <summary>Returns the stock breaker for <paramref name="mode"/>.</summary>
    public static SkUiTextLineBreaker For(LineBreakMode mode) => mode switch
    {
        LineBreakMode.NoWrap => NoWrap,
        LineBreakMode.CharacterWrap => CharacterWrap,
        LineBreakMode.HeadTruncation => HeadTruncation,
        LineBreakMode.MiddleTruncation => MiddleTruncation,
        LineBreakMode.TailTruncation => TailTruncation,
        _ => WordWrap
    };

    /// <summary>The label's <c>LineBreakMode</c> with <paramref name="ellipsis"/> (e.g. <c>"…"</c> or <c>" (more)"</c>) instead of <c>"..."</c>.</summary>
    public static SkUiTextLineBreaker WithEllipsis(string ellipsis)
    {
        ArgumentNullException.ThrowIfNull(ellipsis);
        return context => context.Break(context.Text, context.LineBreakMode, ellipsis);
    }

    /// <summary>
    /// Shorter forms instead of an ellipsis: the text as it is when it fits, otherwise the first of
    /// <paramref name="candidates"/> (longest first) that fits on one line, otherwise the last candidate broken by the
    /// label's <c>LineBreakMode</c>. With no candidates, the text is broken by the <c>LineBreakMode</c>.
    /// Candidates are read lazily, so later ones are only built when needed.
    /// </summary>
    /// <example>A number that drops decimals before it is truncated:
    /// <code>
    /// label.LineBreaker = SkUiTextLineBreakers.FirstFit(_ =&gt;
    ///     Enumerable.Range(0, 4).Reverse().Select(decimals =&gt; value.ToString($"N{decimals}")));
    /// </code></example>
    public static SkUiTextLineBreaker FirstFit(Func<SkUiTextLineBreakContext, IEnumerable<string>> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return context =>
        {
            if (context.Fits(context.Text))
                return context.Break();
            string? last = null;
            foreach (var candidate in candidates(context))
            {
                if (candidate is null)
                    continue;
                if (context.Fits(candidate))
                    return context.Break(candidate, LineBreakMode.NoWrap);
                last = candidate;
            }
            return context.Break(last ?? context.Text, context.LineBreakMode);
        };
    }
}
