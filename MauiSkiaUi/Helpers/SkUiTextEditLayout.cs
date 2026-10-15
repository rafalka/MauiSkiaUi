using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>One laid-out line of editable text (<see cref="SkUiTextEditLayout"/>).</summary>
internal sealed class SkUiTextEditLine
{
    /// <summary>Text index of the line's first code unit.</summary>
    public int Start;
    /// <summary>Text index after the line's last code unit: a wrapped line's trailing spaces are included, a paragraph's <c>\n</c> is not.</summary>
    public int End;
    /// <summary>Whether the line ends its paragraph (the next line, if any, starts after a <c>\n</c>).</summary>
    public bool EndsParagraph;
    /// <summary>Top of the line from the top of the text.</summary>
    public float Top;
    /// <summary>Baseline below <see cref="Top"/>.</summary>
    public float Ascent;
    public float Height;
    /// <summary>Width of the drawn glyphs (a wrapped line's trailing spaces hang past it).</summary>
    public float Width;
    /// <summary>Left edge of the line in the text's box (alignment, <see cref="SkUiTextEditLayout.Arrange"/>).</summary>
    public float Offset;
    public byte BaseLevel;
    internal SKTextBlob? Blob;
    internal string? Text;
    /// <summary>Per code unit of [<see cref="Start"/>, <see cref="End"/>): left and right x from the line's left edge, and bidi level.</summary>
    internal float[] Left = [];
    internal float[] Right = [];
    internal byte[] Levels = [];

    public int Length => End - Start;
    public float Bottom => Top + Height;
    public bool IsRightToLeft => BaseLevel % 2 == 1;
}

/// <summary>
/// Layout of editable text for <c>SkUiEntry</c> / <c>SkUiEditor</c> (both layers): the shared shaping engine
/// (<see cref="SkUiShaping"/>; simple Latin fast path, HarfBuzz, bidi, font fallback), with what editing needs that a
/// label's layout drops: each line's text range and where each of its code units sits, so carets, hit-testing and
/// selection follow clusters and bidi runs. Paragraphs (<c>\n</c>) are laid out separately and kept by content: an edit
/// re-shapes only the paragraphs it changed. Single-line text is one unwrapped line; multiline text wraps at the given
/// width (words, then characters). Indices are UTF-16 code units of the text; caret positions are grapheme boundaries.
/// UI thread; the blobs are immutable and replayed by recorded pictures.
/// </summary>
internal sealed class SkUiTextEditLayout
{
    // Width of the newline box a selection shows at the end of a selected paragraph, in ems.
    private const float NewlineEms = 0.3f;
    // A line height multiplier this close to 1 is the font's own.
    private const double LineHeightTolerance = 1e-6;
    private readonly Func<SKTypeface, SKFont> _fontFor;
    private readonly List<ParagraphLayout> _paragraphs = [];
    private readonly List<SkUiTextEditLine> _lines = [];
    private readonly List<(int Start, int Length)> _ranges = [];
    private readonly List<ParagraphLayout> _rebuilt = [];
    private readonly SkUiShaping.CaretEdges _edges = new();
    [ThreadStatic] private static List<SkUiTextLayout.LineSpec>? _specs;
    private SkUiTextStyle _style;
    private SKFont? _primary;
    private double _wrapWidth = double.NaN;
    private bool _hasStyle;
    private string _text = string.Empty;

    private sealed class ParagraphLayout
    {
        public string Text = string.Empty;
        public readonly List<SkUiTextEditLine> Lines = [];
    }

    internal SkUiTextEditLayout() => _fontFor = typeface => SkUiTextResources.Font(typeface, _fontSize, _style.FontAttributes);

    private float _fontSize;

    /// <summary>The laid-out lines, top to bottom (at least one).</summary>
    internal IReadOnlyList<SkUiTextEditLine> Lines => _lines;

    /// <summary>The text laid out.</summary>
    internal string Text => _text;

    /// <summary>The widest line's drawn width.</summary>
    internal float Width { get; private set; }

    /// <summary>The lines' total height.</summary>
    internal float Height { get; private set; }

    /// <summary>Paragraphs shaped by the last <see cref="Update"/> (tests: an edit re-shapes only what it changed).</summary>
    internal int LastShapedParagraphs { get; private set; }

    /// <summary>The primary font of the current style (after <see cref="Update"/>).</summary>
    internal SKFont? PrimaryFont => _primary;

    /// <summary>
    /// Lays <paramref name="text"/> out with <paramref name="style"/> (font, spacing, direction, rendering, line height);
    /// <paramref name="wrapWidth"/>: the width multiline text wraps at (infinite or NaN: no wrapping). Paragraphs whose text,
    /// style and wrap width are unchanged keep their lines.
    /// </summary>
    internal void Update(string text, in SkUiTextStyle style, double wrapWidth)
    {
        var rendering = style.Rendering == SkUiTextRendering.Default ? SkUiTextOptions.DefaultRendering : style.Rendering;
        var resolved = style with { Rendering = rendering, LineBreaker = null, MaxLines = -1 };
        if (double.IsNaN(wrapWidth) || wrapWidth <= 0)
            wrapWidth = double.PositiveInfinity;
        // Exact: a different width or style lays every paragraph out again.
        if (!_hasStyle || resolved != _style || !wrapWidth.Equals(_wrapWidth))
        {
            ReleaseParagraphs(_paragraphs);
            _paragraphs.Clear();
            _style = resolved;
            _wrapWidth = wrapWidth;
            _hasStyle = true;
            _fontSize = (float)resolved.FontSize;
            _primary = SkUiTextResources.Font(resolved.Typeface, _fontSize, resolved.FontAttributes);
        }
        else if (ReferenceEquals(text, _text) && _lines.Count > 0)
        {
            LastShapedParagraphs = 0;
            return;
        }
        _text = text;
        SplitParagraphs(text);
        // Unchanged paragraphs at both ends keep their lines; the ones between are laid out again.
        var head = 0;
        while (head < _ranges.Count && head < _paragraphs.Count && SameText(text, _ranges[head], _paragraphs[head].Text))
            head++;
        var tail = 0;
        while (tail < _ranges.Count - head && tail < _paragraphs.Count - head
               && SameText(text, _ranges[_ranges.Count - 1 - tail], _paragraphs[_paragraphs.Count - 1 - tail].Text))
            tail++;
        _rebuilt.Clear();
        for (var index = head; index < _ranges.Count - tail; index++)
        {
            var (start, length) = _ranges[index];
            _rebuilt.Add(LayOut(start == 0 && length == text.Length ? text : text.Substring(start, length)));
        }
        var removed = _paragraphs.Count - head - tail;
        for (var index = head; index < head + removed; index++)
            ReleaseParagraph(_paragraphs[index]);
        _paragraphs.RemoveRange(head, removed);
        _paragraphs.InsertRange(head, _rebuilt);
        LastShapedParagraphs = _rebuilt.Count;
        _rebuilt.Clear();
        Reposition();
    }

    private static bool SameText(string text, (int Start, int Length) range, string paragraph) =>
        range.Length == paragraph.Length && text.AsSpan(range.Start, range.Length).SequenceEqual(paragraph);

    private void SplitParagraphs(string text)
    {
        _ranges.Clear();
        var start = 0;
        while (true)
        {
            var newline = text.IndexOf('\n', start);
            if (newline < 0)
            {
                _ranges.Add((start, text.Length - start));
                return;
            }
            _ranges.Add((start, newline - start));
            start = newline + 1;
        }
    }

    /// <summary>Gives every line its text indices and top, and the layout its size.</summary>
    private void Reposition()
    {
        _lines.Clear();
        var offset = 0;
        var top = 0f;
        var width = 0f;
        foreach (var paragraph in _paragraphs)
        {
            var first = paragraph.Lines.Count == 0 ? 0 : paragraph.Lines[0].Start;
            foreach (var line in paragraph.Lines)
            {
                var length = line.Length;
                line.Start = offset + (line.Start - first);
                line.End = line.Start + length;
                line.Top = top;
                top += line.Height;
                width = Math.Max(width, line.Width);
                _lines.Add(line);
            }
            // Lines were kept relative to the paragraph's first line; the next paragraph starts after the '\n'.
            offset += paragraph.Text.Length + 1;
        }
        Width = width;
        Height = top;
    }

    private ParagraphLayout LayOut(string text)
    {
        var layout = new ParagraphLayout { Text = text };
        var primary = _primary!;
        var spacing = (float)_style.CharacterSpacing;
        var paragraph = _style.Rendering switch
        {
            SkUiTextRendering.Simple => SkUiShaping.ShapeSimple(text, primary, _style.Direction, spacing),
            SkUiTextRendering.Auto when SkUiShaping.TryShapeSimple(text, primary, _style.Direction, spacing) is { } simple => simple,
            _ => SkUiShaping.Shape(text, primary.Typeface, _fontSize, _style.Direction, _fontFor, spacing)
        };
        if (text.Length == 0 || double.IsPositiveInfinity(_wrapWidth) || paragraph.Width <= _wrapWidth)
        {
            layout.Lines.Add(BuildLine(paragraph, 0, text.Length, text.Length, endsParagraph: true));
            return layout;
        }
        var specs = _specs ??= [];
        specs.Clear();
        try
        {
            SkUiTextLayout.Wrap(paragraph, _wrapWidth, _style.LineBreakMode != LineBreakMode.CharacterWrap, primary, specs, int.MaxValue, out _);
            for (var index = 0; index < specs.Count; index++)
            {
                var spec = specs[index];
                var last = index == specs.Count - 1;
                layout.Lines.Add(BuildLine(paragraph, spec.Start, spec.End, last ? text.Length : specs[index + 1].Start, last));
            }
        }
        finally
        {
            specs.Clear();
        }
        return layout;
    }

    /// <summary>A line drawing <c>[start, drawEnd)</c> of <paramref name="paragraph"/> and owning <c>[start, end)</c> (trailing spaces hang).</summary>
    private SkUiTextEditLine BuildLine(SkUiShaping.Paragraph paragraph, int start, int drawEnd, int end, bool endsParagraph)
    {
        var shaped = SkUiShaping.BuildLine(paragraph, start, drawEnd, _primary!, _fontFor, edges: _edges);
        var line = new SkUiTextEditLine
        {
            Start = start,
            End = end,
            EndsParagraph = endsParagraph,
            Ascent = shaped.Ascent,
            Height = shaped.Height,
            Width = shaped.Width,
            BaseLevel = shaped.BaseLevel,
            Blob = shaped.Blob,
            Text = shaped.Text
        };
        var count = end - start;
        line.Left = new float[count];
        line.Right = new float[count];
        line.Levels = new byte[count];
        var drawn = drawEnd - start;
        Array.Copy(_edges.Left, line.Left, drawn);
        Array.Copy(_edges.Right, line.Right, drawn);
        Array.Copy(_edges.Levels, line.Levels, drawn);
        if (drawn < count)
        {
            // Hanging spaces: past the right end of a left-to-right line, before the left end of a right-to-left one.
            SkUiShaping.EnsureAdvances(paragraph, _primary!);
            var pen = 0f;
            for (var index = drawn; index < count; index++)
            {
                var advance = start + index < paragraph.Advances.Length ? paragraph.Advances[start + index] : 0;
                (line.Left[index], line.Right[index]) = line.IsRightToLeft ? (-pen - advance, -pen) : (line.Width + pen, line.Width + pen + advance);
                line.Levels[index] = line.BaseLevel;
                pen += advance;
            }
        }
        if (_style.LineHeight > 0 && Math.Abs(_style.LineHeight - 1) > LineHeightTolerance)
        {
            // MAUI's LineHeight: half-leading above and below the glyphs, as labels.
            var height = (float)(line.Height * _style.LineHeight);
            line.Ascent += (height - line.Height) / 2;
            line.Height = height;
        }
        return line;
    }

    /// <summary>
    /// Places the lines horizontally in a box <paramref name="available"/> wide: <paramref name="alignment"/> by each line's
    /// paragraph direction (Start is the right edge for right-to-left text); a line wider than the box starts at Start.
    /// </summary>
    internal void Arrange(TextAlignment alignment, double available)
    {
        foreach (var line in _lines)
            line.Offset = SkUiTextLayout.LineLeft(line.BaseLevel, line.Width, alignment, default, available);
    }

    /// <summary>The line holding the caret at <paramref name="index"/>; <paramref name="upstream"/>: at a wrap, the end of the earlier line.</summary>
    internal int LineIndexOf(int index, bool upstream = false)
    {
        int low = 0, high = _lines.Count - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (_lines[middle].Start <= index)
                low = middle;
            else
                high = middle - 1;
        }
        if (upstream && low > 0 && _lines[low].Start == index && !_lines[low - 1].EndsParagraph)
            low--;
        return low;
    }

    /// <summary>X of the caret at <paramref name="index"/> on line <paramref name="lineIndex"/>, in the text's box.</summary>
    internal float CaretX(int lineIndex, int index)
    {
        var line = _lines[lineIndex];
        var unit = Math.Clamp(index - line.Start, 0, line.Length);
        if (line.Length == 0)
            return line.Offset + (line.IsRightToLeft ? line.Width : 0);
        if (unit < line.Length)
            // The leading edge of the character after the caret.
            return line.Offset + (line.Levels[unit] % 2 == 1 ? line.Right[unit] : line.Left[unit]);
        // At the line's end: the trailing edge of its last character.
        var last = line.Length - 1;
        return line.Offset + (line.Levels[last] % 2 == 1 ? line.Left[last] : line.Right[last]);
    }

    /// <summary>The caret at <paramref name="index"/>: x, and the top and bottom of its line, in the text's box.</summary>
    internal SKRect CaretRect(int index, bool upstream = false, float width = 0)
    {
        if (_lines.Count == 0)
            return SKRect.Empty;
        var lineIndex = LineIndexOf(index, upstream);
        var line = _lines[lineIndex];
        var x = CaretX(lineIndex, index);
        return new SKRect(x, line.Top, x + width, line.Bottom);
    }

    /// <summary>The caret position nearest <paramref name="point"/> (in the text's box): the index and whether it ends a wrapped line.</summary>
    internal (int Index, bool Upstream) HitTest(SKPoint point)
    {
        if (_lines.Count == 0)
            return (0, false);
        return HitTestLine(LineAtY(point.Y), point.X);
    }

    /// <summary>The line at <paramref name="y"/> (the first above, the last below).</summary>
    internal int LineAtY(float y)
    {
        int low = 0, high = _lines.Count - 1;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (y < _lines[middle].Bottom)
                high = middle;
            else
                low = middle + 1;
        }
        return low;
    }

    /// <summary>The caret position on line <paramref name="lineIndex"/> nearest <paramref name="x"/> (in the text's box).</summary>
    internal (int Index, bool Upstream) HitTestLine(int lineIndex, float x)
    {
        var line = _lines[lineIndex];
        if (line.Length == 0)
            return (line.Start, false);
        x -= line.Offset;
        var text = _text;
        int? best = null;
        var bestDistance = float.MaxValue;
        var bestAfter = false;
        for (var unit = 0; unit < line.Length; unit = NextBoundary(text, line, unit))
        {
            var (left, right) = (line.Left[unit], line.Right[unit]);
            var rtl = line.Levels[unit] % 2 == 1;
            // Inside the character: the nearer edge; outside: its nearest edge.
            var distance = x < left ? left - x : x > right ? x - right : 0;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = unit;
                var pastMiddle = x >= (left + right) / 2;
                bestAfter = rtl ? !pastMiddle : pastMiddle;
                if (distance == 0)
                    break;
            }
        }
        var index = line.Start + (bestAfter ? NextBoundary(text, line, best!.Value) : best!.Value);
        // A caret after a wrapped line's last character stays on that line (rather than starting the next one).
        return (index, index == line.End && !line.EndsParagraph);
    }

    /// <summary>
    /// The text index of the character under <paramref name="point"/> (in the text's box), or nearest it on its line: what a
    /// double click selects the word of. An empty line gives its start.
    /// </summary>
    internal int CharacterAt(SKPoint point)
    {
        if (_lines.Count == 0)
            return 0;
        var line = _lines[LineAtY(point.Y)];
        if (line.Length == 0)
            return line.Start;
        var x = point.X - line.Offset;
        var best = 0;
        var bestDistance = float.MaxValue;
        for (var unit = 0; unit < line.Length; unit = NextBoundary(_text, line, unit))
        {
            var distance = x < line.Left[unit] ? line.Left[unit] - x : x > line.Right[unit] ? x - line.Right[unit] : 0;
            if (distance < bestDistance)
            {
                (best, bestDistance) = (unit, distance);
                if (distance == 0)
                    break;
            }
        }
        return line.Start + best;
    }

    private static int NextBoundary(string text, SkUiTextEditLine line, int unit)
    {
        var length = System.Globalization.StringInfo.GetNextTextElementLength(text.AsSpan(line.Start + unit, line.Length - unit));
        return unit + Math.Max(1, length);
    }

    /// <summary>
    /// Adds the boxes covering text <c>[start, end)</c> (in the text's box) to <paramref name="rects"/>: per line, one box per
    /// visual stretch (bidi text can split a range), plus a newline box where the range continues past a paragraph's end.
    /// </summary>
    internal void AddRangeRects(int start, int end, List<SKRect> rects)
    {
        if (end <= start || _lines.Count == 0)
            return;
        var newline = (float)(_fontSize * NewlineEms);
        for (var lineIndex = LineIndexOf(start); lineIndex < _lines.Count; lineIndex++)
        {
            var line = _lines[lineIndex];
            if (line.Start >= end && !(line.Length == 0 && line.Start == start))
                break;
            var from = Math.Max(start, line.Start) - line.Start;
            var to = Math.Min(end, line.End) - line.Start;
            var first = rects.Count;
            for (var unit = from; unit < to; unit++)
            {
                var (left, right) = (line.Offset + line.Left[unit], line.Offset + line.Right[unit]);
                if (right - left <= 0)
                    continue;
                // Joins stretches that touch (a left-to-right run's characters are side by side).
                var joined = false;
                for (var index = first; index < rects.Count && !joined; index++)
                {
                    var rect = rects[index];
                    if (left <= rect.Right + 0.5f && right >= rect.Left - 0.5f)
                    {
                        rects[index] = new SKRect(Math.Min(rect.Left, left), rect.Top, Math.Max(rect.Right, right), rect.Bottom);
                        joined = true;
                    }
                }
                if (!joined)
                    rects.Add(new SKRect(left, line.Top, right, line.Bottom));
            }
            if (line.EndsParagraph && end > line.End && lineIndex + 1 < _lines.Count)
            {
                // The selected '\n': a narrow box at the line's end, past its glyphs.
                var x = line.Offset + (line.IsRightToLeft ? Math.Min(0, MinLeft(line)) - newline : Math.Max(line.Width, MaxRight(line)));
                rects.Add(new SKRect(x, line.Top, x + newline, line.Bottom));
            }
        }
    }

    private static float MinLeft(SkUiTextEditLine line)
    {
        var min = 0f;
        for (var unit = 0; unit < line.Length; unit++)
            min = Math.Min(min, line.Left[unit]);
        return min;
    }

    private static float MaxRight(SkUiTextEditLine line)
    {
        var max = 0f;
        for (var unit = 0; unit < line.Length; unit++)
            max = Math.Max(max, line.Right[unit]);
        return max;
    }

    /// <summary>
    /// Draws the lines whose boxes meet <c>[visibleTop, visibleBottom)</c> (text coordinates) with the text's box at
    /// (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    internal void Draw(SKCanvas canvas, float x, float y, SKPaint paint, float visibleTop = float.NegativeInfinity, float visibleBottom = float.PositiveInfinity)
    {
        if (_primary is not { } primary)
            return;
        foreach (var line in _lines)
        {
            if (line.Bottom < visibleTop)
                continue;
            if (line.Top > visibleBottom)
                break;
            var baseline = y + line.Top + line.Ascent;
            if (line.Blob is { } blob)
                canvas.DrawText(blob, x + line.Offset, baseline, paint);
            else if (!string.IsNullOrEmpty(line.Text))
                canvas.DrawText(line.Text, x + line.Offset, baseline, SKTextAlign.Left, primary, paint);
        }
    }

    /// <summary>Disposes the lines' blobs and forgets the layout (the owner's pictures are gone); the next update lays everything out.</summary>
    internal void Release()
    {
        ReleaseParagraphs(_paragraphs);
        _paragraphs.Clear();
        _lines.Clear();
        _hasStyle = false;
        _text = string.Empty;
        Width = Height = 0;
    }

    private static void ReleaseParagraphs(List<ParagraphLayout> paragraphs)
    {
        foreach (var paragraph in paragraphs)
            ReleaseParagraph(paragraph);
    }

    // Pictures that drew the lines hold their own references to the blobs.
    private static void ReleaseParagraph(ParagraphLayout paragraph)
    {
        foreach (var line in paragraph.Lines)
        {
            line.Blob?.Dispose();
            line.Blob = null;
        }
    }
}
