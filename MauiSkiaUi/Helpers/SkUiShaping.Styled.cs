using HarfBuzzSharp;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>The span styles a styled paragraph is shaped with (the formatted-text layout, <see cref="SkUiRichTextLayout"/>).</summary>
internal interface ISkUiTextSpanFonts
{
    /// <summary>Primary typeface of span <paramref name="span"/>.</summary>
    SKTypeface Typeface(int span);

    /// <summary>The font that draws <paramref name="face"/> (the span's typeface or a fallback) at the span's size and attributes.</summary>
    SKFont Font(int span, SKTypeface face);

    /// <summary>Character spacing of the span in DIPs.</summary>
    float Spacing(int span);

    /// <summary>Line height multiplier of the span; 0 or less: the font's.</summary>
    float LineHeight(int span);
}

/// <summary>
/// Styled (multi-span) shaping: a paragraph whose code units each belong to a span with its own font, size,
/// attributes and spacing. Bidi levels are resolved over the whole paragraph, so direction runs cross span boundaries;
/// itemization also splits at span boundaries, and each run is shaped with its span's font and the paragraph as context.
/// </summary>
internal static partial class SkUiShaping
{
    /// <summary>A run of one span on a visual line: its glyphs (relative to <see cref="X"/>) and the font box.</summary>
    /// <param name="Blob">Glyphs positioned from the piece's origin; <c>null</c> when it has none.</param>
    /// <param name="X">Left edge from the start of the line.</param>
    /// <param name="Width">Advance width, with character spacing and justification.</param>
    /// <param name="Span">Span index.</param>
    /// <param name="Ascent">The font's ascent (positive, above the baseline).</param>
    /// <param name="Descent">The font's descent (positive, below the baseline).</param>
    /// <param name="Size">The font size (decoration fallbacks).</param>
    /// <param name="Metrics">The font's metrics (decoration positions).</param>
    internal readonly record struct StyledPiece(SKTextBlob? Blob, float X, float Width, int Span, float Ascent, float Descent, float Size, SKFontMetrics Metrics);

    /// <summary>A laid-out visual line of styled text: one piece per run, in visual order.</summary>
    internal sealed class StyledLine
    {
        public StyledPiece[] Pieces = [];
        public float Width;
        public float Ascent;   // positive distance above the baseline (tallest span, with its line height)
        public float Height;   // line box
        public byte BaseLevel;
    }

    /// <summary>
    /// Shapes <paramref name="text"/> (no line breaks inside) as one paragraph; <paramref name="spans"/> gives each code
    /// unit's span.
    /// </summary>
    internal static Paragraph ShapeStyled(string text, int[] spans, SkUiTextDirection direction, ISkUiTextSpanFonts fonts)
    {
        var paragraph = new Paragraph { Text = text, Spans = spans, BaseLevel = SkUiBidi.BaseLevel(text, direction) };
        paragraph.Levels = SkUiBidi.ResolveLevels(text, paragraph.BaseLevel);
        paragraph.Advances = new float[text.Length];
        if (text.Length == 0)
            return paragraph;
        ItemizeStyled(paragraph, fonts);
        foreach (var run in paragraph.Runs)
        {
            ShapeRun(text, run.Start, run.Length, run, run.Font!, fonts.Spacing(run.Span));
            paragraph.Width += run.Width;
            AddAdvances(paragraph, run);
        }
        return paragraph;
    }

    /// <summary>As <see cref="Itemize"/>, also splitting at span boundaries; each span falls back from its own typeface.</summary>
    private static void ItemizeStyled(Paragraph paragraph, ISkUiTextSpanFonts fonts)
    {
        var text = paragraph.Text;
        var spans = paragraph.Spans!;
        var unicode = UnicodeFunctions.Default;
        Run? current = null;
        for (var index = 0; index < text.Length;)
        {
            var cp = text.ConvertToUtf32OrReplacement(index);
            var units = cp > 0xFFFF ? 2 : 1;
            var level = paragraph.Levels[index];
            var span = spans[index];
            var script = cp < 0x80
                ? (cp is >= 'A' and <= 'Z' or >= 'a' and <= 'z' ? Script.Latin : Script.Common)
                : unicode.GetScript(cp);
            var neutralScript = script == Script.Common || script == Script.Inherited || script == Script.Unknown;
            var sameSpan = current is not null && current.Span == span;
            var face = FaceFor(cp, fonts.Typeface(span), sameSpan ? current!.Typeface : null);
            var sameScript = current is not null && (neutralScript || current.Script == script || current.Script == Script.Common);
            if (!sameSpan || current!.Level != level || !ReferenceEquals(current.Typeface, face) || !sameScript)
            {
                current = new Run
                {
                    Start = index, Level = level, Script = neutralScript ? Script.Common : script, Typeface = face,
                    Span = span, Font = fonts.Font(span, face)
                };
                paragraph.Runs.Add(current);
            }
            else if (current.Script == Script.Common && !neutralScript)
            {
                current.Script = script;
            }
            current.Length += units;
            index += units;
        }
    }

    /// <summary>
    /// Builds the visual line for logical range <c>[start, end)</c> of a styled <paramref name="paragraph"/>: one
    /// piece per run, the line as tall as its tallest span (each span's line height split above and below its glyphs).
    /// An empty range is a blank line in <paramref name="emptySpan"/>'s font. With <paramref name="justifyWidth"/>, the
    /// spaces widen so the line is that wide.
    /// </summary>
    internal static StyledLine BuildStyledLine(Paragraph paragraph, int start, int end, ISkUiTextSpanFonts fonts, int emptySpan, float? justifyWidth = null)
    {
        var line = new StyledLine { BaseLevel = paragraph.BaseLevel };
        if (end <= start)
        {
            var font = fonts.Font(emptySpan, fonts.Typeface(emptySpan));
            var ascent = -font.Metrics.Ascent;
            var extra = LineBoxExtra(font.Spacing, fonts.LineHeight(emptySpan));
            line.Ascent = ascent + extra;
            line.Height = font.Spacing + 2 * extra;
            return line;
        }

        var pieces = new List<Run>();
        foreach (var run in paragraph.Runs)
        {
            var clipStart = Math.Max(start, run.Start);
            var clipEnd = Math.Min(end, run.Start + run.Length);
            if (clipEnd <= clipStart)
                continue;
            if (clipStart == run.Start && clipEnd == run.Start + run.Length)
            {
                pieces.Add(run);
                continue;
            }
            var piece = new Run
            {
                Start = clipStart, Length = clipEnd - clipStart, Level = run.Level, Script = run.Script, Typeface = run.Typeface,
                Span = run.Span, Font = run.Font
            };
            ShapeRun(paragraph.Text, piece.Start, piece.Length, piece, piece.Font!, fonts.Spacing(piece.Span));
            pieces.Add(piece);
        }

        var order = SkUiBidi.VisualOrder(pieces.ConvertAll(piece => piece.Level));
        var text = paragraph.Text;
        var extraSpace = 0f; // justification: added after each space glyph
        if (justifyWidth is { } target)
        {
            var natural = 0f;
            foreach (var piece in pieces)
                natural += piece.Width;
            extraSpace = JustifySpace(text, start, end, natural, target);
        }
        var builder = _builder ??= new SKTextBlobBuilder();
        var result = new StyledPiece[pieces.Count];
        var (lineAscent, lineDescent) = (0f, 0f);
        var pen = 0f;
        for (var slot = 0; slot < order.Length; slot++)
        {
            var piece = pieces[order[slot]];
            var font = piece.Font!;
            var shift = 0f;
            SKTextBlob? blob = null;
            if (piece.Glyphs.Length > 0)
            {
                var buffer = builder.AllocatePositionedRun(font, piece.Glyphs.Length);
                buffer.SetGlyphs(piece.Glyphs);
                var positions = buffer.Positions;
                for (var glyph = 0; glyph < piece.Glyphs.Length; glyph++)
                {
                    positions[glyph] = new SKPoint(shift + piece.Positions[glyph].X, piece.Positions[glyph].Y);
                    if (extraSpace != 0 && text[Math.Clamp((int)piece.Clusters[glyph], 0, text.Length - 1)] == ' ')
                        shift += extraSpace;
                }
                blob = builder.Build();
            }
            var metrics = font.Metrics;
            var ascent = -metrics.Ascent;
            var extra = LineBoxExtra(font.Spacing, fonts.LineHeight(piece.Span));
            lineAscent = Math.Max(lineAscent, ascent + extra);
            lineDescent = Math.Max(lineDescent, font.Spacing - ascent + extra);
            result[slot] = new StyledPiece(blob, pen, piece.Width + shift, piece.Span, ascent, metrics.Descent, font.Size, metrics);
            pen += piece.Width + shift;
        }
        line.Pieces = result;
        line.Width = pen;
        line.Ascent = lineAscent;
        line.Height = lineAscent + lineDescent;
        return line;
    }

    /// <summary>Space added above and below a font's glyphs by a line height <paramref name="multiplier"/> (half-leading; negative shrinks).</summary>
    private static float LineBoxExtra(float spacing, float multiplier) =>
        multiplier > 0 && multiplier != 1 ? spacing * (multiplier - 1) / 2 : 0;
}
