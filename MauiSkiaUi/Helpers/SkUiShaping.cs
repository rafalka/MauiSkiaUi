using System.Collections.Concurrent;
using System.Globalization;
using HarfBuzzSharp;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using Buffer = HarfBuzzSharp.Buffer;

namespace MauiSkiaUi;

/// <summary>
/// HarfBuzz-based paragraph shaping shared by all drawn text: bidi levels (<see cref="SkUiBidi"/>), itemization by
/// level / script / font (with per-character system font fallback for scripts and emoji the primary typeface
/// lacks), HarfBuzz shaping with paragraph context (ligatures, kerning, Arabic joining, Indic reordering, mark
/// placement), and visual line assembly (UAX #9 L2) into <see cref="SKTextBlob"/>s.
/// Runs on the UI thread during measure / record; the resulting blobs are immutable and replayed on the render thread.
/// </summary>
internal static class SkUiShaping
{
    private static readonly ConcurrentDictionary<(SKTypeface Primary, int CodePoint), SKTypeface> Fallbacks = new();
    [ThreadStatic] private static Dictionary<SKTypeface, SKShaper>? t_shapers;
    [ThreadStatic] private static SKTextBlobBuilder? t_builder;
    [ThreadStatic] private static Buffer? t_buffer;
    private static readonly ConcurrentDictionary<SKTypeface, byte[]> Coverage = new();

    /// <summary>One shaped run of a paragraph in logical order.</summary>
    internal sealed class Run
    {
        public int Start;
        public int Length;
        public byte Level;
        public Script Script;
        public SKTypeface Typeface = null!;
        public ushort[] Glyphs = [];
        public SKPoint[] Positions = [];
        public uint[] Clusters = [];
        public float Width;
    }

    /// <summary>A shaped paragraph: text, levels, logical runs and per-code-unit advances (for line breaking).</summary>
    internal sealed class Paragraph
    {
        public string Text = string.Empty;
        public byte BaseLevel;
        public byte[] Levels = [];
        public List<Run> Runs = [];
        public float[] Advances = [];
        public float Width;
        /// <summary>
        /// Built by the simple path: no runs; lines are plain substrings measured / drawn by Skia directly (the
        /// pre-HarfBuzz renderer). <see cref="Advances"/> are only filled when line breaking needs them.
        /// </summary>
        public bool IsSimple;
    }

    /// <summary>A laid-out visual line ready to draw at (x, baseline).</summary>
    internal sealed class Line
    {
        public SKTextBlob? Blob;
        public float Width;
        public float Ascent;   // positive distance above the baseline
        public float Height;   // line spacing
        public byte BaseLevel;
        /// <summary>Simple-path line: drawn with <c>DrawText(string)</c> in the primary font (no blob).</summary>
        public string? Text;
    }

    /// <summary>
    /// Fast path: text made only of Latin / digits / common punctuation that the primary font fully covers, in a
    /// left-to-right paragraph, needs no bidi, no fallback and (for UI text) no HarfBuzz — glyphs and advances come
    /// straight from Skia, like the pre-HarfBuzz renderer. Returns <c>null</c> when the text is not simple.
    /// </summary>
    internal static Paragraph? TryShapeSimple(string text, SKFont font, SkUiTextDirection direction)
    {
        if (direction == SkUiTextDirection.RightToLeft)
            return null;
        var typeface = font.Typeface;
        foreach (var c in text)
            if (!IsSimpleChar(c) || !HasGlyph(typeface, c))
                return null;
        return ShapeSimple(text, font);
    }

    /// <summary>Simple (pre-HarfBuzz) paragraph: one Skia measure; missing glyphs draw as the font's .notdef.</summary>
    internal static Paragraph ShapeSimple(string text, SKFont font) => new()
    {
        Text = text,
        IsSimple = true,
        Width = text.Length == 0 ? 0 : font.MeasureText(text)
    };

    /// <summary>Fills per-code-unit advances of a simple paragraph (only needed to wrap or truncate).</summary>
    internal static void EnsureAdvances(Paragraph paragraph, SKFont font)
    {
        if (!paragraph.IsSimple || paragraph.Advances.Length == paragraph.Text.Length)
            return;
        var widths = font.GetGlyphWidths(paragraph.Text.AsSpan());
        if (widths.Length == paragraph.Text.Length)
        {
            paragraph.Advances = widths;
            return;
        }
        // Surrogate pairs yield one width per code point: spread them back onto code units.
        var advances = new float[paragraph.Text.Length];
        var glyph = 0;
        for (var index = 0; index < paragraph.Text.Length && glyph < widths.Length; index++, glyph++)
        {
            advances[index] = widths[glyph];
            if (char.IsHighSurrogate(paragraph.Text[index])) index++;
        }
        paragraph.Advances = advances;
    }

    /// <summary>Characters the simple path handles: printable ASCII, Latin-1 / Latin Extended-A, common punctuation and currency.</summary>
    private static bool IsSimpleChar(char c) =>
        c is >= ' ' and <= '~' or >= '\u00A0' and <= '\u017F' or >= '\u2010' and <= '\u2027' or >= '\u2030' and <= '\u205E' or >= '\u20A0' and <= '\u20C0';

    /// <summary>Shapes <paramref name="text"/> (no line breaks inside) as one paragraph.</summary>
    internal static Paragraph Shape(string text, SKTypeface primary, float size, SkUiTextDirection direction, Func<SKTypeface, SKFont> fonts)
    {
        var paragraph = new Paragraph { Text = text, BaseLevel = SkUiBidi.BaseLevel(text, direction) };
        paragraph.Levels = SkUiBidi.ResolveLevels(text, paragraph.BaseLevel);
        paragraph.Advances = new float[text.Length];
        if (text.Length == 0)
            return paragraph;
        Itemize(paragraph, primary);
        foreach (var run in paragraph.Runs)
        {
            ShapeRun(text, run.Start, run.Length, run, fonts(run.Typeface));
            paragraph.Width += run.Width;
            // Per-code-unit advances for line breaking: each glyph's advance (next visual x − its x) goes to the
            // first code unit of its cluster; the other code units of a cluster (ligatures, marks) get 0.
            for (var glyph = 0; glyph < run.Glyphs.Length; glyph++)
            {
                var next = glyph + 1 < run.Glyphs.Length ? run.Positions[glyph + 1].X : run.Width;
                var cluster = Math.Clamp((int)run.Clusters[glyph], run.Start, run.Start + run.Length - 1);
                paragraph.Advances[cluster] += next - run.Positions[glyph].X;
            }
        }
        return paragraph;
    }

    private static void Itemize(Paragraph paragraph, SKTypeface primary)
    {
        var text = paragraph.Text;
        var unicode = UnicodeFunctions.Default;
        Run? current = null;
        for (var index = 0; index < text.Length;)
        {
            var cp = text.ConvertToUtf32OrReplacement(index);
            var units = cp > 0xFFFF ? 2 : 1;
            var level = paragraph.Levels[index];
            // ASCII needs no native script lookup: letters are Latin, everything else Common.
            var script = cp < 0x80
                ? (cp is >= 'A' and <= 'Z' or >= 'a' and <= 'z' ? Script.Latin : Script.Common)
                : unicode.GetScript(cp);
            var neutralScript = script == Script.Common || script == Script.Inherited || script == Script.Unknown;
            var face = FaceFor(cp, primary, current?.Typeface);
            var sameScript = current is not null && (neutralScript || current.Script == script || current.Script == Script.Common);
            if (current is null || current.Level != level || !ReferenceEquals(current.Typeface, face) || !sameScript)
            {
                current = new Run { Start = index, Level = level, Script = neutralScript ? Script.Common : script, Typeface = face };
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

    /// <summary>Typeface for a code point: primary when it has a glyph, else a cached system fallback.</summary>
    private static SKTypeface FaceFor(int cp, SKTypeface primary, SKTypeface? current)
    {
        // Marks, joiners and variation selectors stay with the preceding character's font (never split a cluster).
        if (current is not null && IsClusterExtender(cp))
            return current;
        // The primary font wins whenever it covers the character — including spaces and punctuation next to
        // fallback text (an emoji font's space or an Arabic font's dash would look wrong in Latin text).
        if (HasGlyph(primary, cp) || cp is ' ' or '\t' or 0xFFFD)
            return primary;
        if (current is not null && !ReferenceEquals(current, primary) && HasGlyph(current, cp))
            return current;
        return Fallbacks.GetOrAdd((primary, cp), static key =>
            SKFontManager.Default.MatchCharacter(key.Primary.FamilyName, key.Primary.FontStyle, null, key.CodePoint) ?? key.Primary);
    }

#pragma warning disable CS0618 // Typeface-level glyph lookup is the cheapest coverage check (no SKFont allocation).
    private static bool HasGlyph(SKTypeface typeface, int cp)
    {
        if (cp > 0xFFFF)
            return typeface.GetGlyph(cp) != 0;
        // BMP coverage cached per typeface (0 = unknown, 1 = has glyph, 2 = missing): avoids a native call per character.
        var cache = Coverage.GetOrAdd(typeface, static _ => new byte[0x10000]);
        var known = Volatile.Read(ref cache[cp]);
        if (known == 0)
        {
            known = typeface.GetGlyph(cp) != 0 ? (byte)1 : (byte)2;
            Volatile.Write(ref cache[cp], known);
        }
        return known == 1;
    }
#pragma warning restore CS0618

    private static bool IsClusterExtender(int cp) =>
        cp is 0x200C or 0x200D or >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF or >= 0x1F3FB and <= 0x1F3FF or >= 0xE0020 and <= 0xE007F
        || CharUnicodeInfo.GetUnicodeCategory(cp) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.SpacingCombiningMark;

    private static SKShaper Shaper(SKTypeface typeface)
    {
        var shapers = t_shapers ??= [];
        if (!shapers.TryGetValue(typeface, out var shaper))
            shapers[typeface] = shaper = new SKShaper(typeface);
        return shaper;
    }

    /// <summary>Shapes <c>text[start..start+length)</c> with the whole string as context into <paramref name="run"/>.</summary>
    private static void ShapeRun(string text, int start, int length, Run run, SKFont font)
    {
        var buffer = t_buffer ??= new Buffer();
        buffer.ClearContents();
        buffer.AddUtf16(text.AsSpan(), start, length);
        buffer.Direction = run.Level % 2 == 1 ? Direction.RightToLeft : Direction.LeftToRight;
        if (run.Script != Script.Common)
            buffer.Script = run.Script;
        buffer.GuessSegmentProperties();
        var result = Shaper(run.Typeface).Shape(buffer, font);
        var count = result.Codepoints.Length;
        run.Glyphs = new ushort[count];
        run.Positions = result.Points;
        run.Clusters = result.Clusters;
        run.Width = result.Width;
        for (var index = 0; index < count; index++)
            run.Glyphs[index] = (ushort)result.Codepoints[index];
    }

    /// <summary>Builds the visual line for logical range <c>[start, end)</c> of <paramref name="paragraph"/>.</summary>
    internal static Line BuildLine(Paragraph paragraph, int start, int end, SKFont primary, Func<SKTypeface, SKFont> fonts)
    {
        var line = new Line { BaseLevel = paragraph.BaseLevel };
        var metrics = primary.Metrics;
        line.Ascent = -metrics.Ascent;
        line.Height = primary.Spacing;
        if (end <= start)
            return line;
        if (paragraph.IsSimple)
        {
            line.Text = start == 0 && end == paragraph.Text.Length ? paragraph.Text : paragraph.Text[start..end];
            line.Width = ReferenceEquals(line.Text, paragraph.Text) ? paragraph.Width : primary.MeasureText(line.Text);
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
            var piece = new Run { Start = clipStart, Length = clipEnd - clipStart, Level = run.Level, Script = run.Script, Typeface = run.Typeface };
            ShapeRun(paragraph.Text, piece.Start, piece.Length, piece, fonts(piece.Typeface));
            pieces.Add(piece);
        }

        var order = SkUiBidi.VisualOrder(pieces.ConvertAll(piece => piece.Level));
        // Build() resets the builder, so one per thread is reused.
        var builder = t_builder ??= new SKTextBlobBuilder();
        var pen = 0f;
        foreach (var index in order)
        {
            var piece = pieces[index];
            var font = fonts(piece.Typeface);
            if (!ReferenceEquals(piece.Typeface, primary.Typeface))
            {
                var fallback = font.Metrics;
                line.Ascent = Math.Max(line.Ascent, -fallback.Ascent);
                line.Height = Math.Max(line.Height, font.Spacing);
            }
            if (piece.Glyphs.Length > 0)
            {
                var buffer = builder.AllocatePositionedRun(font, piece.Glyphs.Length);
                buffer.SetGlyphs(piece.Glyphs);
                var positions = buffer.Positions;
                for (var glyph = 0; glyph < piece.Glyphs.Length; glyph++)
                    positions[glyph] = new SKPoint(pen + piece.Positions[glyph].X, piece.Positions[glyph].Y);
            }
            pen += piece.Width;
        }
        line.Width = pen;
        line.Blob = builder.Build();
        return line;
    }
}
