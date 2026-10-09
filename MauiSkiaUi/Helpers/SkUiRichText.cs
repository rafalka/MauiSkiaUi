using System.Text;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>What decides the lines of one span: its font, size, attributes, spacing and line height (label defaults applied).</summary>
/// <param name="Typeface">Primary typeface (family and attributes resolved).</param>
/// <param name="FontSize">Font size in DIPs.</param>
/// <param name="FontAttributes">Requested bold / italic: drawn synthetically when <paramref name="Typeface"/> lacks them.</param>
/// <param name="CharacterSpacing">DIPs added after each character.</param>
/// <param name="LineHeight">Multiplier of the font's line spacing; 0 / negative: the font's.</param>
internal readonly record struct SkUiTextSpanStyle(
    SKTypeface Typeface,
    double FontSize,
    FontAttributes FontAttributes = FontAttributes.None,
    double CharacterSpacing = 0,
    double LineHeight = -1);

/// <summary>How one span paints: colors and decorations (no re-layout when they change).</summary>
/// <param name="TextColor">Glyph and decoration color.</param>
/// <param name="Background">Fill behind the span's glyphs (transparent: none).</param>
/// <param name="Decorations">Underline / strikethrough.</param>
internal readonly record struct SkUiTextSpanPaint(SKColor TextColor, SKColor Background = default, TextDecorations Decorations = TextDecorations.None);

/// <summary>
/// Formatted text resolved for <see cref="SkUiRichTextLayout"/>: the spans' displayed text (after their text transform)
/// concatenated, the offset where each span starts, and each span's layout and paint style. Immutable; labels build a new
/// one when a span or a default changes, and the layout tells layout changes from paint-only ones (<see cref="SameLayout"/>).
/// </summary>
internal sealed class SkUiRichText
{
    private int[]? _spanOf;

    internal SkUiRichText(string text, int[] starts, SkUiTextSpanStyle[] styles, SkUiTextSpanPaint[] paints)
    {
        Text = text;
        Starts = starts;
        Styles = styles;
        Paints = paints;
    }

    /// <summary>No spans.</summary>
    internal static SkUiRichText Empty { get; } = new(string.Empty, [], [], []);

    /// <summary>All spans' text.</summary>
    internal string Text { get; }

    /// <summary>Where each span starts in <see cref="Text"/>.</summary>
    internal int[] Starts { get; }

    internal SkUiTextSpanStyle[] Styles { get; }

    internal SkUiTextSpanPaint[] Paints { get; }

    /// <summary>The span of each code unit of <see cref="Text"/>.</summary>
    internal int[] SpanOf
    {
        get
        {
            if (_spanOf is null)
            {
                var spanOf = new int[Text.Length];
                for (var span = 0; span < Starts.Length; span++)
                {
                    var end = span + 1 < Starts.Length ? Starts[span + 1] : Text.Length;
                    spanOf.AsSpan(Starts[span], end - Starts[span]).Fill(span);
                }
                _spanOf = spanOf;
            }
            return _spanOf;
        }
    }

    /// <summary>Whether <paramref name="other"/> breaks into the same lines (same text, span ranges and layout styles); paints may differ.</summary>
    internal bool SameLayout(SkUiRichText? other) =>
        other is not null && (ReferenceEquals(this, other)
            || (Text == other.Text && Starts.AsSpan().SequenceEqual(other.Starts) && Styles.AsSpan().SequenceEqual(other.Styles)));

    /// <summary>Builds a <see cref="SkUiRichText"/> span by span.</summary>
    internal sealed class Builder
    {
        private readonly StringBuilder _text = new();
        private readonly List<int> _starts = [];
        private readonly List<SkUiTextSpanStyle> _styles = [];
        private readonly List<SkUiTextSpanPaint> _paints = [];

        internal Builder Add(string? text, in SkUiTextSpanStyle style, in SkUiTextSpanPaint paint)
        {
            _starts.Add(_text.Length);
            _text.Append(text);
            _styles.Add(style);
            _paints.Add(paint);
            return this;
        }

        internal SkUiRichText Build() => _starts.Count == 0
            ? Empty
            : new SkUiRichText(_text.ToString(), [.. _starts], [.. _styles], [.. _paints]);
    }
}

/// <summary>
/// Formatted-text measure / paint / hit-test engine for labels with spans (<c>FormattedText</c> on <see cref="SkUiLabel"/>,
/// spans on <see cref="Core.SkUiCoreLabel"/>). It shares the plain engine's shaping (<see cref="SkUiShaping"/>: HarfBuzz,
/// bidi resolved across spans, per-character font fallback from each span's font), line breaking, truncation, line
/// placement and decorations, so spans wrap inside one paragraph as one text. Always shaped (no simple fast path), and
/// custom line breakers do not apply. Lines are cached per width; a change that only repaints (span colors, background,
/// decorations) reuses them. A fitted style shrinks, grows or tightens every span alike to fit
/// its slot (<see cref="SkUiTextFit"/>).
/// </summary>
internal sealed class SkUiRichTextLayout : ISkUiTextSpanFonts, ISkUiTextFitLayout
{
    private readonly Dictionary<int, float> _ellipsisWidths = [];
    private List<SkUiShaping.StyledLine> _lines = [];
    // What the lines were broken for: the text's layout parts, the paragraph style and the width.
    private SkUiRichText _text = SkUiRichText.Empty;
    private SkUiTextStyle _style;
    private double _brokenWidth = double.NaN;
    // As in SkUiTextLayout: nothing wrapped or truncated, so any width ≥ _maxLineWidth gives the same lines.
    private bool _naturalFit;
    private float _maxLineWidth;
    // As in SkUiTextLayout: the fit's view of the lines.
    private bool _fits;
    private float _widestParagraph;
    private SkUiTextFit? _fit;

    /// <summary>Line layouts computed by this instance (diagnostics / tests).</summary>
    internal int LayoutCount { get; private set; }

    /// <summary>
    /// Disposes the lines' text blobs and forgets them (the label's retained pictures are gone): nothing is left to the
    /// finalizer. The next measure or draw lays the text out again.
    /// </summary>
    internal void Release()
    {
        ReleaseBlobs(_lines);
        _lines = [];
        Invalidate();
    }

    private static void ReleaseBlobs(List<SkUiShaping.StyledLine> lines)
    {
        foreach (var line in lines)
        {
            var pieces = line.Pieces;
            for (var index = 0; index < pieces.Length; index++)
            {
                if (pieces[index].Blob is not { } blob)
                    continue;
                blob.Dispose();
                pieces[index] = pieces[index] with { Blob = null };
            }
        }
    }

    /// <summary>Forgets broken lines (padding or a default changed).</summary>
    internal void Invalidate()
    {
        _brokenWidth = double.NaN;
        _naturalFit = false;
        _fit?.Clear();
    }

    SKTypeface ISkUiTextSpanFonts.Typeface(int span) => _text.Styles[span].Typeface;

    SKFont ISkUiTextSpanFonts.Font(int span, SKTypeface face)
    {
        var style = _text.Styles[span];
        return SkUiTextResources.Font(face, (float)style.FontSize, style.FontAttributes);
    }

    float ISkUiTextSpanFonts.Spacing(int span) => _style.TightenedSpacing(_text.Styles[span].CharacterSpacing, _text.Styles[span].FontSize);

    float ISkUiTextSpanFonts.LineHeight(int span) => (float)_text.Styles[span].LineHeight;

    private void EnsureLines(SkUiRichText text, double width, SkUiTextStyle style)
    {
        width = double.IsNaN(width) ? 0 : Math.Max(0, width);
        // Span text is always shaped and custom breakers do not apply: neither decides these lines.
        style = style with { LineBreaker = null, Rendering = SkUiTextRendering.Shaped };
        if (style == _style && text.SameLayout(_text) && !double.IsNaN(_brokenWidth)
            && (width == _brokenWidth || (_naturalFit && width >= _maxLineWidth)))
        {
            _text = text; // newer paints, same lines
            return;
        }
        _brokenWidth = double.NaN;
        _style = style;
        _text = text;
        LayoutCount++;
        if (_fit?.Shaping is ShapedParagraphs { Running: true } running)
            running.Layouts++;
        _ellipsisWidths.Clear();
        _widestParagraph = 0;
        var lines = new List<SkUiShaping.StyledLine>();
        var fits = true;
        var natural = text.Text.Length == 0 || BreakStock(width, lines, out fits);
        // Pictures that drew the old lines hold their own references to the blobs.
        ReleaseBlobs(_lines);
        _lines = lines;
        _naturalFit = natural;
        _fits = fits;
        _brokenWidth = width;
        _maxLineWidth = 0;
        foreach (var line in lines)
            _maxLineWidth = Math.Max(_maxLineWidth, line.Width);
    }

    /// <summary>
    /// <paramref name="style"/> at the scale and tightening <see cref="SkUiTextFit"/> picks for a
    /// <paramref name="width"/>×<paramref name="height"/> content slot; unchanged when the style is not fitted.
    /// </summary>
    private SkUiTextStyle Fitted(SkUiRichText text, in SkUiTextStyle style, double width, double height) =>
        style.IsFitted ? (_fit ??= new SkUiTextFit()).Fit(this, text, style, width, height) : style;

    /// <inheritdoc />
    bool ISkUiTextFitLayout.Fits(object text, in SkUiTextStyle style, double width, double height, out double estimate)
    {
        EnsureLines((SkUiRichText)text, SkUiTextFit.UnscaledWidth(width, style), SkUiTextFit.Unscaled(style));
        return SkUiTextFit.Check(_fits, (float)LinesSize().Height, _widestParagraph, style, width, height, out estimate);
    }

    /// <inheritdoc />
    Size ISkUiTextFitLayout.Extent(object text, in SkUiTextStyle style, double width)
    {
        EnsureLines((SkUiRichText)text, SkUiTextFit.UnscaledWidth(width, style), SkUiTextFit.Unscaled(style));
        var size = LinesSize();
        return new Size(size.Width * style.Scale, size.Height * style.Scale);
    }

    /// <inheritdoc />
    void ISkUiTextFitLayout.ReuseShaping(bool reuse)
    {
        if (_fit is not { } fit)
            return;
        if (!reuse)
            (fit.Shaping as ShapedParagraphs)?.End();
        else if (fit.Shaping is ShapedParagraphs shaped)
            shaped.Begin();
        else
        {
            var created = new ShapedParagraphs();
            fit.Shaping = created;
            created.Begin();
        }
    }

    /// <summary>Shapes <c>full[start..end]</c>, or reuses it from an earlier layout of the running fit.</summary>
    private SkUiShaping.Paragraph ShapeParagraph(string full, int[] spanOf, int start, int end) =>
        _fit?.Shaping is ShapedParagraphs { Running: true, Layouts: > 1 } shaped
            ? shaped.Get(full, spanOf, start, end, this)
            : SkUiShaping.ShapeStyled(full[start..end], spanOf[start..end], _style.Direction, this);

    /// <summary>
    /// Paragraphs shaped by the layouts of one fit (<see cref="ISkUiTextFitLayout.ReuseShaping"/>), by start and tightening,
    /// for the text and direction they were shaped with: trials differ in width and tightening only. Kept from the fit's
    /// second layout on (text that fits as set needs one), cleared when the fit ends.
    /// </summary>
    private sealed class ShapedParagraphs
    {
        private readonly Dictionary<(int Start, float Tightening), SkUiShaping.Paragraph> _paragraphs = [];
        private (SkUiRichText? Text, SkUiTextDirection Direction) _with;

        /// <summary>Whether a fit runs.</summary>
        internal bool Running { get; private set; }

        /// <summary>Layouts of the running fit.</summary>
        internal int Layouts { get; set; }

        internal void Begin() => (Running, Layouts) = (true, 0);

        internal SkUiShaping.Paragraph Get(string full, int[] spanOf, int start, int end, SkUiRichTextLayout layout)
        {
            var style = layout._style;
            if (!ReferenceEquals(layout._text, _with.Text) || style.Direction != _with.Direction)
            {
                _paragraphs.Clear();
                _with = (layout._text, style.Direction);
            }
            var key = (start, style.Tightening);
            if (!_paragraphs.TryGetValue(key, out var paragraph))
                _paragraphs[key] = paragraph = SkUiShaping.ShapeStyled(full[start..end], spanOf[start..end], style.Direction, layout);
            return paragraph;
        }

        internal void End()
        {
            (Running, Layouts) = (false, 0);
            _paragraphs.Clear();
            _with = default;
        }
    }

    /// <summary>The widest line and the lines' height.</summary>
    private Size LinesSize()
    {
        var height = 0f;
        foreach (var line in _lines)
            height += line.Height;
        return new Size(_maxLineWidth, height);
    }

    /// <inheritdoc />
    bool ISkUiTextFitLayout.SameText(object text, object other) => ((SkUiRichText)text).SameLayout((SkUiRichText)other);

    /// <summary>
    /// Breaks the text into <paramref name="lines"/> as <see cref="SkUiTextLayout"/> does for a stock mode: paragraphs at
    /// newlines, wrapping, head / middle / tail truncation, and <c>MaxLines</c> (tail truncation wraps and ellipsizes the
    /// last kept line).
    /// <c>fits</c>: whether the lines show all the text, as <see cref="SkUiTextLayout"/>'s stock breaking tells it.
    /// </summary>
    /// <returns>Whether the lines are the same at any width at least as wide as the widest.</returns>
    private bool BreakStock(double width, List<SkUiShaping.StyledLine> lines, out bool fits)
    {
        var mode = _style.LineBreakMode;
        var maxLines = _style.MaxLines;
        var natural = true;
        fits = true;
        var limit = maxLines > 0 ? maxLines : int.MaxValue;
        var wrapTail = mode == LineBreakMode.TailTruncation && maxLines > 0;
        var justifyWidth = _style.Justify ? (float)width : (float?)null;
        var full = _text.Text;
        var spanOf = _text.SpanOf;
        var specs = new List<SkUiTextLayout.LineSpec>();
        // The paragraph and start of the last line, which a later MaxLines cut ellipsizes.
        (SkUiShaping.Paragraph Paragraph, int Start, int EmptySpan) last = default;
        var start = 0;
        while (start <= full.Length)
        {
            var end = full.AsSpan(start).IndexOfAny('\r', '\n') is var newline and >= 0 ? start + newline : full.Length;
            var next = end < full.Length && full[end] == '\r' && end + 1 < full.Length && full[end + 1] == '\n' ? end + 2 : end + 1;
            if (lines.Count == limit)
            {
                fits = false;
                if (wrapTail && last.Paragraph is not null)
                {
                    lines[^1] = Truncate(last.Paragraph, last.Start, width, LineBreakMode.TailTruncation, last.EmptySpan);
                    natural = false;
                }
                break;
            }
            // A blank paragraph is as tall as the span holding its line break.
            var emptySpan = spanOf[Math.Min(start, spanOf.Length - 1)];
            var paragraph = ShapeParagraph(full, spanOf, start, end);
            start = next;
            _widestParagraph = Math.Max(_widestParagraph, paragraph.Width);
            if (double.IsInfinity(width) || mode == LineBreakMode.NoWrap || paragraph.Width <= width)
            {
                fits &= !(paragraph.Width > width); // NoWrap: drawn past the width
                lines.Add(SkUiShaping.BuildStyledLine(paragraph, 0, paragraph.Text.Length, this, emptySpan));
                last = (paragraph, 0, emptySpan);
                continue;
            }
            natural = false;
            switch (mode)
            {
                case LineBreakMode.HeadTruncation:
                case LineBreakMode.MiddleTruncation:
                case LineBreakMode.TailTruncation when !wrapTail:
                    fits = false;
                    lines.Add(Truncate(paragraph, 0, width, mode, emptySpan));
                    last = default;
                    break;
                default:
                    specs.Clear();
                    var leftOver = SkUiTextLayout.Wrap(paragraph, width, mode != LineBreakMode.CharacterWrap, null, specs, limit - lines.Count, out var splitWord);
                    fits &= !leftOver && !splitWord;
                    foreach (var spec in specs)
                        lines.Add(SkUiShaping.BuildStyledLine(paragraph, spec.Start, spec.End, this, emptySpan, spec.Wrapped ? justifyWidth : null));
                    if (specs.Count > 0)
                        last = (paragraph, specs[^1].Start, emptySpan);
                    if (leftOver && wrapTail)
                    {
                        lines[^1] = Truncate(paragraph, specs[^1].Start, width, LineBreakMode.TailTruncation, emptySpan);
                        return false;
                    }
                    break;
            }
        }
        return natural;
    }

    /// <summary>
    /// Ellipsizes <c>paragraph.Text[start..]</c> so it fits (tail / head / middle) as a line of its own. The ellipsis
    /// takes the style of the text it replaces (the last kept character; for head truncation the first), and spans keep
    /// their styles. Tail truncation always ends with the ellipsis (the last line kept by <c>MaxLines</c>).
    /// </summary>
    private SkUiShaping.StyledLine Truncate(SkUiShaping.Paragraph paragraph, int start, double width, LineBreakMode mode, int emptySpan)
    {
        const string ellipsis = SkUiTextLayout.DefaultEllipsis;
        var text = paragraph.Text;
        var spans = paragraph.Spans!;
        var advances = paragraph.Advances;
        var boundary = SkUiTextLayout.GraphemeStarts(text);
        var head = mode == LineBreakMode.HeadTruncation;
        var middle = mode == LineBreakMode.MiddleTruncation;

        int PrefixEnd(double budget)
        {
            var used = 0.0;
            var fit = start;
            for (var index = start; index < text.Length; index++)
            {
                used += advances[index];
                if (used > budget) break;
                if (boundary[index + 1]) fit = index + 1;
            }
            while (fit > start && text[fit - 1] == ' ') fit--;
            return fit;
        }

        int SuffixStart(double budget)
        {
            var used = 0.0;
            var fit = text.Length;
            for (var index = text.Length - 1; index >= start; index--)
            {
                used += advances[index];
                if (used > budget) break;
                if (boundary[index]) fit = index;
            }
            while (fit < text.Length && text[fit] == ' ') fit++;
            return fit;
        }

        // The span next to the ellipsis decides its width, and the cut decides that span: guess from where the text
        // overflows, cut, and cut once more if the span found there draws a wider ellipsis (its width then fits the budget).
        var ellipsisSpan = head ? spans[^1] : spans[Math.Max(start, PrefixEnd(width) - 1)];
        var ellipsisWidth = EllipsisWidth(ellipsisSpan);
        var (prefixEnd, suffixStart) = (start, text.Length);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (ellipsisWidth > width)
                return SkUiShaping.BuildStyledLine(paragraph, 0, 0, this, emptySpan);
            var available = width - ellipsisWidth;
            prefixEnd = head ? start : PrefixEnd(middle ? available / 2 : available);
            suffixStart = head ? SuffixStart(available) : middle ? SuffixStart(available / 2) : text.Length;
            var adjacent = head
                ? spans[Math.Min(suffixStart, text.Length - 1)]
                : spans[Math.Max(start, prefixEnd - 1)];
            if (adjacent == ellipsisSpan)
                break;
            var adjacentWidth = EllipsisWidth(adjacent);
            if (adjacentWidth <= ellipsisWidth)
            {
                ellipsisSpan = adjacent;
                break;
            }
            if (attempt == 0)
                (ellipsisSpan, ellipsisWidth) = (adjacent, adjacentWidth);
        }

        var prefix = prefixEnd - start;
        var suffix = text.Length - suffixStart;
        var synthesized = new StringBuilder(prefix + ellipsis.Length + suffix)
            .Append(text, start, prefix).Append(ellipsis).Append(text, suffixStart, suffix).ToString();
        var synthesizedSpans = new int[synthesized.Length];
        spans.AsSpan(start, prefix).CopyTo(synthesizedSpans);
        synthesizedSpans.AsSpan(prefix, ellipsis.Length).Fill(ellipsisSpan);
        spans.AsSpan(suffixStart, suffix).CopyTo(synthesizedSpans.AsSpan(prefix + ellipsis.Length));
        var line = SkUiShaping.ShapeStyled(synthesized, synthesizedSpans, _style.Direction, this);
        return SkUiShaping.BuildStyledLine(line, 0, synthesized.Length, this, ellipsisSpan);
    }

    /// <summary>Width of the ellipsis drawn in <paramref name="span"/>'s style.</summary>
    private float EllipsisWidth(int span)
    {
        if (!_ellipsisWidths.TryGetValue(span, out var width))
        {
            var ellipsis = SkUiTextLayout.DefaultEllipsis;
            var spans = new int[ellipsis.Length];
            spans.AsSpan().Fill(span);
            _ellipsisWidths[span] = width = SkUiShaping.ShapeStyled(ellipsis, spans, SkUiTextDirection.LeftToRight, this).Width;
        }
        return width;
    }

    /// <summary>
    /// Content size in DIPs including <paramref name="padding"/>. <paramref name="heightConstraint"/> only matters to a fitted
    /// style.
    /// </summary>
    internal Size Measure(SkUiRichText text, in SkUiTextStyle style, Thickness padding, double widthConstraint, double heightConstraint = double.PositiveInfinity)
    {
        var contentWidth = double.IsInfinity(widthConstraint)
            ? double.PositiveInfinity
            : Math.Max(0, widthConstraint - padding.HorizontalThickness);
        var fitted = Fitted(text, style, contentWidth, SkUiTextFit.ContentHeight(heightConstraint, padding));
        EnsureLines(text, SkUiTextFit.UnscaledWidth(contentWidth, fitted), SkUiTextFit.Unscaled(fitted));
        var size = LinesSize();
        return new Size(size.Width * fitted.Scale + padding.HorizontalThickness, size.Height * fitted.Scale + padding.VerticalThickness);
    }

    /// <summary>
    /// Draws the text into a <paramref name="width"/>×<paramref name="height"/> slot, placed and aligned as
    /// <see cref="SkUiTextLayout.Draw"/> places plain text: span backgrounds first (behind every line), then each span's
    /// glyphs and decorations in its own color. <paramref name="paint"/> is reused for every span.
    /// </summary>
    internal void Draw(SKCanvas canvas, SkUiRichText text, in SkUiTextStyle style, Thickness padding,
        double width, double height, TextAlignment horizontal, TextAlignment vertical, SKPaint paint)
    {
        if (text.Text.Length == 0)
            return;
        var fitted = Fitted(text, style, Math.Max(0, width - padding.HorizontalThickness), SkUiTextFit.ContentHeight(height, padding));
        // A fitted scale draws the unscaled lines with a canvas scale.
        var restore = SkUiTextFit.BeginScaled(canvas, fitted.Scale, ref padding, ref width, ref height);
        var available = Math.Max(0, width - padding.HorizontalThickness);
        EnsureLines(text, available, SkUiTextFit.Unscaled(fitted));
        var paints = text.Paints;
        var backgrounds = false;
        foreach (var span in paints)
            backgrounds |= span.Background.Alpha > 0;
        if (backgrounds)
        {
            foreach (var (line, left, top) in Placed(padding, height, horizontal, vertical, available))
            {
                var baseline = top + line.Ascent;
                foreach (var piece in line.Pieces)
                {
                    if (paints[piece.Span].Background is var background && background.Alpha == 0)
                        continue;
                    paint.Color = background;
                    canvas.DrawRect(left + piece.X, baseline - piece.Ascent, piece.Width, piece.Ascent + piece.Descent, paint);
                }
            }
        }
        foreach (var (line, left, top) in Placed(padding, height, horizontal, vertical, available))
        {
            var baseline = top + line.Ascent;
            foreach (var piece in line.Pieces)
            {
                var span = paints[piece.Span];
                paint.Color = span.TextColor;
                if (piece.Blob is { } blob)
                    canvas.DrawText(blob, left + piece.X, baseline, paint);
                if (span.Decorations != TextDecorations.None)
                    SkUiTextLayout.DrawDecorations(canvas, span.Decorations, left + piece.X, baseline, piece.Width, piece.Size, piece.Metrics, paint);
            }
        }
        if (restore >= 0)
            canvas.RestoreToCount(restore);
    }

    /// <summary>
    /// The span drawn at <paramref name="point"/> (content coordinates of the same slot <see cref="Draw"/> uses), or -1
    /// when the point is beside the text. A line owns its full height (and a justified gap below it); a span owns its
    /// glyph advances on the line.
    /// </summary>
    internal int HitTest(SkUiRichText text, in SkUiTextStyle style, Thickness padding, double width, double height,
        TextAlignment horizontal, TextAlignment vertical, Point point)
    {
        if (text.Text.Length == 0)
            return -1;
        var fitted = Fitted(text, style, Math.Max(0, width - padding.HorizontalThickness), SkUiTextFit.ContentHeight(height, padding));
        // The unscaled lines of a fitted scale see the point unscaled.
        point = SkUiTextFit.UnscalePoint(fitted.Scale, padding, point);
        SkUiTextFit.UnscaleSlot(fitted.Scale, ref padding, ref width, ref height);
        var available = Math.Max(0, width - padding.HorizontalThickness);
        EnsureLines(text, available, SkUiTextFit.Unscaled(fitted));
        var (_, gap) = Vertical(padding, height, vertical);
        foreach (var (line, left, top) in Placed(padding, height, horizontal, vertical, available))
        {
            if (point.Y < top || point.Y >= top + line.Height + gap)
                continue;
            foreach (var piece in line.Pieces)
                if (point.X >= left + piece.X && point.X < left + piece.X + piece.Width)
                    return piece.Span;
            return -1;
        }
        return -1;
    }

    private (float Top, float Gap) Vertical(Thickness padding, double height, TextAlignment vertical)
    {
        var total = 0f;
        foreach (var line in _lines)
            total += line.Height;
        return SkUiTextLayout.PlaceVertically(total, _lines.Count, padding, height, vertical);
    }

    /// <summary>Each line with its left edge and top.</summary>
    private IEnumerable<(SkUiShaping.StyledLine Line, float Left, float Top)> Placed(Thickness padding, double height,
        TextAlignment horizontal, TextAlignment vertical, double available)
    {
        var (top, gap) = Vertical(padding, height, vertical);
        foreach (var line in _lines)
        {
            yield return (line, SkUiTextLayout.LineLeft(line.BaseLevel, line.Width, horizontal, padding, available), top);
            top += line.Height + gap;
        }
    }
}
