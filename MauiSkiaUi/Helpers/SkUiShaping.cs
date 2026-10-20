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
internal static partial class SkUiShaping
{
    private static readonly ConcurrentDictionary<(SKTypeface Primary, int CodePoint), SKTypeface> Fallbacks = new();
    [ThreadStatic] private static Dictionary<SKTypeface, SKShaper?>? _shapers;
    [ThreadStatic] private static SKTextBlobBuilder? _builder;
    [ThreadStatic] private static Buffer? _buffer;
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
        /// <summary>Styled text: the span (style) index of the run; -1 for single-style text.</summary>
        public int Span = -1;
        /// <summary>Styled text: the font the run was shaped with (its span's size and attributes).</summary>
        public SKFont? Font;
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
        /// <summary>Character spacing in DIPs added after each cluster (simple path: after each code point).</summary>
        public float Spacing;
        /// <summary>
        /// Built by the simple path: no runs; lines are plain substrings measured / drawn by Skia directly (the
        /// pre-HarfBuzz renderer). <see cref="Advances"/> are only filled when line breaking needs them.
        /// </summary>
        public bool IsSimple;
        /// <summary>Styled text: the span index of each code unit (<c>null</c> for single-style text).</summary>
        public int[]? Spans;
    }

    /// <summary>A laid-out visual line ready to draw at (x, baseline).</summary>
    internal sealed class Line
    {
        public SKTextBlob? Blob;
        public float Width;
        public float Ascent;   // positive distance above the baseline
        public float Height;   // line spacing
        /// <summary>Android's font padding over the line's fonts (the primary and its fallbacks): how far the tallest font top rises above the line's ascent.</summary>
        public float FontPaddingTop;
        /// <summary>The most any of the line's fonts' bottom falls below its own descent.</summary>
        public float FontPaddingBottom;
        public byte BaseLevel;
        /// <summary>Simple-path line: drawn with <c>DrawText(string)</c> in the primary font (no blob).</summary>
        public string? Text;
    }

    /// <summary>
    /// Where each code unit of a line sits (editing: carets, hit-testing, selection): its left and right x on the line as
    /// drawn (visual order, from the line's left edge) and its bidi level. A cluster drawn as one glyph (a ligature) is
    /// split evenly between its graphemes; the later code units of a grapheme share its edges. Reused between lines.
    /// </summary>
    internal sealed class CaretEdges
    {
        /// <summary>The first code unit of the line in its paragraph.</summary>
        public int Start;
        /// <summary>Code units of the line.</summary>
        public int Count;
        public float[] Left = [];
        public float[] Right = [];
        public byte[] Levels = [];

        internal void Reset(int start, int count)
        {
            Start = start;
            Count = count;
            if (Left.Length < count)
            {
                var capacity = Math.Max(count, Left.Length * 2);
                Left = new float[capacity];
                Right = new float[capacity];
                Levels = new byte[capacity];
            }
            Left.AsSpan(0, count).Fill(float.NaN);
            Right.AsSpan(0, count).Fill(float.NaN);
            Levels.AsSpan(0, count).Clear();
        }

        /// <summary>Widens the edges of the code unit <paramref name="unit"/> (paragraph index) by a glyph spanning <paramref name="x0"/>–<paramref name="x1"/>.</summary>
        internal void Add(int unit, float x0, float x1)
        {
            var index = unit - Start;
            if ((uint)index >= (uint)Count)
                return;
            var (low, high) = x0 <= x1 ? (x0, x1) : (x1, x0);
            Left[index] = float.IsNaN(Left[index]) ? low : Math.Min(Left[index], low);
            Right[index] = float.IsNaN(Right[index]) ? high : Math.Max(Right[index], high);
        }

        /// <summary>
        /// Gives the code units no glyph started at (the rest of a cluster) their share: the cluster's graphemes split its
        /// width evenly (in the run's direction); units inside a grapheme share its edges.
        /// </summary>
        internal void FillClusters(string text)
        {
            var unit = 0;
            while (unit < Count)
            {
                var head = unit;
                var end = unit + 1;
                while (end < Count && float.IsNaN(Left[end])) end++;
                if (float.IsNaN(Left[head]))
                    (Left[head], Right[head]) = head > 0 ? (Right[head - 1], Right[head - 1]) : (0f, 0f);
                var graphemes = 0;
                for (var index = head; index < end; index += Math.Max(1, System.Globalization.StringInfo.GetNextTextElementLength(text.AsSpan(Start + index, end - index))))
                    graphemes++;
                var (left, right) = (Left[head], Right[head]);
                var width = graphemes > 1 ? (right - left) / graphemes : right - left;
                var rtl = Levels[head] % 2 == 1;
                var grapheme = 0;
                for (var index = head; index < end; grapheme++)
                {
                    var length = Math.Max(1, System.Globalization.StringInfo.GetNextTextElementLength(text.AsSpan(Start + index, end - index)));
                    var (x0, x1) = rtl ? (right - (grapheme + 1) * width, right - grapheme * width) : (left + grapheme * width, left + (grapheme + 1) * width);
                    for (var part = index; part < index + length && part < end; part++)
                        (Left[part], Right[part], Levels[part]) = (x0, x1, Levels[head]);
                    index += length;
                }
                unit = end;
            }
        }
    }

    /// <summary>
    /// Fast path: text made only of Latin / digits / common punctuation that the primary font fully covers, in a
    /// left-to-right paragraph, needs no bidi, no fallback and (for UI text) no HarfBuzz — glyphs and advances come
    /// straight from Skia, like the pre-HarfBuzz renderer. Returns <c>null</c> when the text is not simple.
    /// </summary>
    internal static Paragraph? TryShapeSimple(string text, SKFont font, SkUiTextDirection direction, float spacing = 0)
    {
        if (direction == SkUiTextDirection.RightToLeft)
            return null;
        var typeface = font.Typeface;
        foreach (var c in text)
            if (!IsSimpleChar(c) || !HasGlyph(typeface, c))
                return null;
        return ShapeSimple(text, font, direction, spacing);
    }

    /// <summary>
    /// Simple (pre-HarfBuzz) paragraph: one Skia measure; missing glyphs draw as the font's .notdef. Glyphs stay in
    /// logical order (no bidi); the base direction still decides what Start / End alignment means.
    /// </summary>
    internal static Paragraph ShapeSimple(string text, SKFont font, SkUiTextDirection direction, float spacing = 0) => new()
    {
        Text = text,
        IsSimple = true,
        Spacing = spacing,
        BaseLevel = SkUiBidi.BaseLevel(text, direction),
        Width = text.Length == 0 ? 0 : font.MeasureText(text) + spacing * CodePoints(text, 0, text.Length)
    };

    /// <summary>Code points in <c>text[start..end)</c> (the glyphs of a simple line).</summary>
    private static int CodePoints(string text, int start, int end)
    {
        var count = 0;
        for (var index = start; index < end; index++)
            if (!char.IsLowSurrogate(text[index])) count++;
        return count;
    }

    /// <summary>Fills per-code-unit advances of a simple paragraph (only needed to wrap or truncate).</summary>
    internal static void EnsureAdvances(Paragraph paragraph, SKFont font)
    {
        if (!paragraph.IsSimple || paragraph.Advances.Length == paragraph.Text.Length)
            return;
        var widths = font.GetGlyphWidths(paragraph.Text.AsSpan());
        if (widths.Length == paragraph.Text.Length)
        {
            if (paragraph.Spacing != 0)
                for (var index = 0; index < widths.Length; index++)
                    widths[index] += paragraph.Spacing;
            paragraph.Advances = widths;
            return;
        }
        // Surrogate pairs yield one width per code point: spread them back onto code units.
        var advances = new float[paragraph.Text.Length];
        var glyph = 0;
        for (var index = 0; index < paragraph.Text.Length && glyph < widths.Length; index++, glyph++)
        {
            advances[index] = widths[glyph] + paragraph.Spacing;
            if (char.IsHighSurrogate(paragraph.Text[index])) index++;
        }
        paragraph.Advances = advances;
    }

    /// <summary>Characters the simple path handles: printable ASCII, Latin-1 / Latin Extended-A, common punctuation and currency.</summary>
    private static bool IsSimpleChar(char c) =>
        c is >= ' ' and <= '~' or >= '\u00A0' and <= '\u017F' or >= '\u2010' and <= '\u2027' or >= '\u2030' and <= '\u205E' or >= '\u20A0' and <= '\u20C0';

    /// <summary>Shapes <paramref name="text"/> (no line breaks inside) as one paragraph, <paramref name="spacing"/> DIPs added after each cluster.</summary>
    internal static Paragraph Shape(string text, SKTypeface primary, float size, SkUiTextDirection direction, Func<SKTypeface, SKFont> fonts, float spacing = 0)
    {
        var paragraph = new Paragraph { Text = text, BaseLevel = SkUiBidi.BaseLevel(text, direction), Spacing = spacing };
        paragraph.Levels = SkUiBidi.ResolveLevels(text, paragraph.BaseLevel);
        paragraph.Advances = new float[text.Length];
        if (text.Length == 0)
            return paragraph;
        Itemize(paragraph, primary);
        foreach (var run in paragraph.Runs)
        {
            ShapeRun(text, run.Start, run.Length, run, fonts(run.Typeface), spacing);
            paragraph.Width += run.Width;
            AddAdvances(paragraph, run);
        }
        return paragraph;
    }

    /// <summary>
    /// Per-code-unit advances for line breaking: each glyph's advance (next visual x − its x) goes to the first code
    /// unit of its cluster; the other code units of a cluster (ligatures, marks) get 0.
    /// </summary>
    private static void AddAdvances(Paragraph paragraph, Run run)
    {
        for (var glyph = 0; glyph < run.Glyphs.Length; glyph++)
        {
            var next = glyph + 1 < run.Glyphs.Length ? run.Positions[glyph + 1].X : run.Width;
            var cluster = Math.Clamp((int)run.Clusters[glyph], run.Start, run.Start + run.Length - 1);
            paragraph.Advances[cluster] += next - run.Positions[glyph].X;
        }
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

    /// <summary>
    /// Line spacing of a fallback font: the extent of its glyphs, without its leading. Some fallback fonts carry a large
    /// leading (Hiragino Sans, macOS's fallback for ☆ and CJK: half the font size), which would make a line with one such
    /// symbol taller than its neighbours and put its text above the middle of a button.
    /// </summary>
    internal static float FallbackSpacing(SKFont font) => font.Metrics.Descent - font.Metrics.Ascent;

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

    /// <summary>HarfBuzz shaper for a typeface, or <c>null</c> when the typeface exposes no font data (e.g. Skia's empty
    /// default typeface on a Linux host without fonts).</summary>
    private static SKShaper? Shaper(SKTypeface typeface)
    {
        var shapers = _shapers ??= [];
        if (!shapers.TryGetValue(typeface, out var shaper))
        {
            using (var data = typeface.OpenStream())
                shaper = data is null ? null : new SKShaper(typeface);
            shapers[typeface] = shaper;
        }
        return shaper;
    }

    /// <summary>Shapes <c>text[start..start+length)</c> with the whole string as context into <paramref name="run"/>.</summary>
    private static void ShapeRun(string text, int start, int length, Run run, SKFont font, float spacing)
    {
        var buffer = _buffer ??= new Buffer();
        buffer.ClearContents();
        buffer.AddUtf16(text.AsSpan(), start, length);
        buffer.Direction = run.Level % 2 == 1 ? Direction.RightToLeft : Direction.LeftToRight;
        if (run.Script != Script.Common)
            buffer.Script = run.Script;
        buffer.GuessSegmentProperties();
        if (Shaper(run.Typeface) is not { } shaper)
        {
            ShapeRunUnshaped(text, start, length, run, font);
            AddSpacing(run, spacing);
            return;
        }
        var result = shaper.Shape(buffer, font);
        var count = result.Codepoints.Length;
        run.Glyphs = new ushort[count];
        run.Positions = result.Points;
        run.Clusters = result.Clusters;
        run.Width = result.Width;
        for (var index = 0; index < count; index++)
            run.Glyphs[index] = (ushort)result.Codepoints[index];
        AddSpacing(run, spacing);
    }

    /// <summary>
    /// Character spacing: <paramref name="spacing"/> DIPs after each cluster (a grapheme, or a ligature), in visual
    /// order, so it widens the cluster's advance and every later glyph moves by it (as iOS kerning and CSS letter-spacing).
    /// </summary>
    private static void AddSpacing(Run run, float spacing)
    {
        if (spacing == 0 || run.Glyphs.Length == 0)
            return;
        var offset = 0f;
        for (var glyph = 0; glyph < run.Glyphs.Length; glyph++)
        {
            run.Positions[glyph].X += offset;
            if (glyph + 1 == run.Glyphs.Length || run.Clusters[glyph + 1] != run.Clusters[glyph])
                offset += spacing;
        }
        run.Width += offset;
    }

    /// <summary>Run of a typeface HarfBuzz cannot read: one Skia glyph per code point with Skia advances, in visual order.</summary>
    private static void ShapeRunUnshaped(string text, int start, int length, Run run, SKFont font)
    {
        var span = text.AsSpan(start, length);
        var glyphs = font.GetGlyphs(span);
        var widths = font.GetGlyphWidths(span);
        var count = Math.Min(glyphs.Length, widths.Length);
        var clusters = new uint[count];
        for (int index = 0, unit = start; index < count && unit < start + length; index++)
        {
            clusters[index] = (uint)unit;
            unit += char.IsHighSurrogate(text[unit]) ? 2 : 1;
        }
        var order = new int[count];
        for (var index = 0; index < count; index++)
            order[index] = run.Level % 2 == 1 ? count - 1 - index : index;
        run.Glyphs = new ushort[count];
        run.Positions = new SKPoint[count];
        run.Clusters = new uint[count];
        var x = 0f;
        for (var index = 0; index < count; index++)
        {
            var logical = order[index];
            run.Glyphs[index] = glyphs[logical];
            run.Clusters[index] = clusters[logical];
            run.Positions[index] = new SKPoint(x, 0);
            x += widths[logical];
        }
        run.Width = x;
    }

    /// <summary>
    /// Builds the visual line for logical range <c>[start, end)</c> of <paramref name="paragraph"/>; with
    /// <paramref name="justifyWidth"/>, the spaces in it widen so the line is that wide (justified text). With
    /// <paramref name="edges"/>, also fills in where each code unit of the line sits (editing).
    /// </summary>
    internal static Line BuildLine(Paragraph paragraph, int start, int end, SKFont primary, Func<SKTypeface, SKFont> fonts, float? justifyWidth = null, CaretEdges? edges = null)
    {
        var line = new Line { BaseLevel = paragraph.BaseLevel };
        var metrics = primary.Metrics;
        line.Ascent = -metrics.Ascent;
        line.Height = primary.Spacing;
        var fontTop = -metrics.Top; // the tallest font's top above the baseline
        line.FontPaddingTop = Math.Max(0, fontTop - line.Ascent);
        line.FontPaddingBottom = Math.Max(0, metrics.Bottom - metrics.Descent);
        edges?.Reset(start, Math.Max(0, end - start));
        if (end <= start)
            return line;
        if (paragraph.IsSimple)
        {
            if (edges is not null)
                SimpleEdges(paragraph, start, end, primary, edges);
            if (paragraph.Spacing != 0 || justifyWidth is not null)
                return BuildSpacedSimpleLine(line, paragraph, start, end, primary, justifyWidth);
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
            ShapeRun(paragraph.Text, piece.Start, piece.Length, piece, fonts(piece.Typeface), paragraph.Spacing);
            pieces.Add(piece);
        }

        var order = SkUiBidi.VisualOrder(pieces.ConvertAll(piece => piece.Level));
        var text = paragraph.Text;
        var extra = 0f; // justification: added after each space glyph
        if (justifyWidth is { } target)
        {
            var natural = 0f;
            foreach (var piece in pieces)
                natural += piece.Width;
            extra = JustifySpace(text, start, end, natural, target);
        }
        // Build() resets the builder, so one per thread is reused.
        var builder = _builder ??= new SKTextBlobBuilder();
        var pen = 0f;
        var shift = 0f;
        foreach (var index in order)
        {
            var piece = pieces[index];
            var font = fonts(piece.Typeface);
            if (!ReferenceEquals(piece.Typeface, primary.Typeface))
            {
                // A fallback font's glyphs extend the line above and below the baseline, without its leading.
                var fallback = font.Metrics;
                var ascent = -fallback.Ascent;
                var below = Math.Max(line.Height - line.Ascent, FallbackSpacing(font) - ascent);
                line.Ascent = Math.Max(line.Ascent, ascent);
                line.Height = line.Ascent + below;
                fontTop = Math.Max(fontTop, -fallback.Top);
                line.FontPaddingBottom = Math.Max(line.FontPaddingBottom, fallback.Bottom - fallback.Descent);
            }
            if (piece.Glyphs.Length > 0)
            {
                var buffer = builder.AllocatePositionedRun(font, piece.Glyphs.Length);
                buffer.SetGlyphs(piece.Glyphs);
                var positions = buffer.Positions;
                for (var glyph = 0; glyph < piece.Glyphs.Length; glyph++)
                {
                    var x = pen + shift + piece.Positions[glyph].X;
                    positions[glyph] = new SKPoint(x, piece.Positions[glyph].Y);
                    if (extra != 0 && text[Math.Clamp((int)piece.Clusters[glyph], 0, text.Length - 1)] == ' ')
                        shift += extra;
                    if (edges is not null)
                    {
                        var next = glyph + 1 < piece.Glyphs.Length ? pen + shift + piece.Positions[glyph + 1].X : pen + piece.Width + shift;
                        edges.Add(Math.Clamp((int)piece.Clusters[glyph], piece.Start, piece.Start + piece.Length - 1), x, next);
                    }
                }
            }
            pen += piece.Width;
        }
        line.Width = pen + shift;
        line.Blob = builder.Build();
        if (edges is not null)
        {
            for (var unit = 0; unit < edges.Count; unit++)
                edges.Levels[unit] = paragraph.Levels[start + unit];
            edges.FillClusters(text);
        }
        line.FontPaddingTop = Math.Max(0, fontTop - line.Ascent); // from the ascent the tallest font set
        return line;
    }

    /// <summary>Edges of a simple line: its code units' advances side by side, left to right (no bidi on the simple path).</summary>
    private static void SimpleEdges(Paragraph paragraph, int start, int end, SKFont primary, CaretEdges edges)
    {
        EnsureAdvances(paragraph, primary);
        var x = 0f;
        for (var unit = start; unit < end; unit++)
        {
            var advance = unit < paragraph.Advances.Length ? paragraph.Advances[unit] : 0;
            edges.Left[unit - start] = x;
            edges.Right[unit - start] = x + advance;
            x += advance;
        }
        // A surrogate pair's width sits on its high surrogate: the low one shares the pair's edges.
        for (var unit = start + 1; unit < end; unit++)
            if (char.IsLowSurrogate(paragraph.Text[unit]))
                (edges.Left[unit - start], edges.Right[unit - start]) = (edges.Left[unit - start - 1], edges.Right[unit - start - 1]);
    }

    /// <summary>
    /// Width added after each space of <c>text[start..end)</c> so a line <paramref name="natural"/> wide becomes
    /// <paramref name="target"/> wide; 0 when it has no spaces or is not narrower.
    /// </summary>
    private static float JustifySpace(string text, int start, int end, float natural, float target)
    {
        if (natural >= target)
            return 0;
        var spaces = 0;
        for (var index = start; index < end; index++)
            if (text[index] == ' ') spaces++;
        return spaces == 0 ? 0 : (target - natural) / spaces;
    }

    /// <summary>
    /// Simple-path line with character spacing or justification: Skia glyphs and advances placed in a blob (still no
    /// HarfBuzz). One glyph per code point.
    /// </summary>
    private static Line BuildSpacedSimpleLine(Line line, Paragraph paragraph, int start, int end, SKFont primary, float? justifyWidth)
    {
        var text = paragraph.Text;
        var span = text.AsSpan(start, end - start);
        var glyphs = primary.GetGlyphs(span);
        var widths = primary.GetGlyphWidths(span);
        var count = Math.Min(glyphs.Length, widths.Length);
        var extra = 0f;
        if (justifyWidth is { } target)
        {
            var natural = paragraph.Spacing * count;
            for (var glyph = 0; glyph < count; glyph++)
                natural += widths[glyph];
            extra = JustifySpace(text, start, end, natural, target);
        }
        var builder = _builder ??= new SKTextBlobBuilder();
        var buffer = builder.AllocatePositionedRun(primary, count);
        buffer.SetGlyphs(glyphs.AsSpan(0, count));
        var positions = buffer.Positions;
        var pen = 0f;
        for (int glyph = 0, unit = start; glyph < count && unit < end; glyph++)
        {
            positions[glyph] = new SKPoint(pen, 0);
            pen += widths[glyph] + paragraph.Spacing + (text[unit] == ' ' ? extra : 0);
            unit += char.IsHighSurrogate(text[unit]) ? 2 : 1;
        }
        line.Width = pen;
        line.Blob = builder.Build();
        return line;
    }
}
