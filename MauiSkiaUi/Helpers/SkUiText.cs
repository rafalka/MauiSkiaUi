using System.Collections.Concurrent;
using MauiSkiaUi.Core;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Process-wide typeface cache. System lookups (<see cref="SKTypeface.FromFamilyName(string, SKFontStyleWeight, SKFontStyleWidth, SKFontStyleSlant)"/>)
/// are expensive, so each (family, attributes) pair is resolved once. Registered fonts (<see cref="SkUiFonts"/>)
/// win over system fonts. Cached typefaces are never disposed: retained pictures may reference them.
/// </summary>
internal static class SkUiTypefaces
{
    private static readonly ConcurrentDictionary<(string Family, FontAttributes Attributes), SKTypeface> SystemCache = new();

    internal static SKTypeface Resolve(string? family, FontAttributes attributes = FontAttributes.None)
    {
        if (!string.IsNullOrEmpty(family) && SkUiFonts.TryResolve(family) is { } registered)
            return registered;
        return SystemCache.GetOrAdd((family ?? string.Empty, attributes), static key =>
            SKTypeface.FromFamilyName(key.Family,
                key.Attributes.HasFlag(FontAttributes.Bold) ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                key.Attributes.HasFlag(FontAttributes.Italic) ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright)
            ?? SKTypeface.Default);
    }
}

/// <summary>How drawn text is laid out.</summary>
public enum SkUiTextRendering
{
    /// <summary>Use <see cref="SkUiTextOptions.DefaultRendering"/>.</summary>
    Default,
    /// <summary>
    /// Simple Latin / digit / punctuation text the font fully covers takes the fast path (Skia glyphs and advances, no
    /// HarfBuzz); anything else (complex scripts, RTL, emoji, missing glyphs) is shaped with HarfBuzz.
    /// </summary>
    Auto,
    /// <summary>Always HarfBuzz: kerning and ligatures even for Latin text; bidi and font fallback.</summary>
    Shaped,
    /// <summary>
    /// Always the fast pre-HarfBuzz renderer: no shaping, bidi or font fallback (complex scripts render incorrectly,
    /// missing glyphs show as the font's .notdef). For dense UIs of plain text or numbers.
    /// </summary>
    Simple
}

/// <summary>Process-wide text options.</summary>
public static class SkUiTextOptions
{
    private static SkUiTextRendering _defaultRendering = SkUiTextRendering.Auto;

    /// <summary>Rendering used by labels whose <c>TextRendering</c> is <see cref="SkUiTextRendering.Default"/> (initially <see cref="SkUiTextRendering.Auto"/>).</summary>
    public static SkUiTextRendering DefaultRendering
    {
        get => _defaultRendering;
        set => _defaultRendering = value == SkUiTextRendering.Default ? SkUiTextRendering.Auto : value;
    }
}

/// <summary>
/// Shared text measure / paint engine for <see cref="SkUiLabel"/> and <see cref="SkUiCoreLabel"/> (one
/// implementation for both layers). Text is shaped with HarfBuzz (<see cref="SkUiShaping"/>): complex scripts,
/// ligatures, kerning, bidirectional (RTL / mixed) text and per-character font fallback. Broken, shaped lines
/// are cached as text blobs keyed by wrap width and direction, so paint-only changes (color, alignment) and
/// re-paints never re-shape or re-wrap.
/// </summary>
internal sealed class SkUiTextLayout
{
    private const string Ellipsis = "...";
    private readonly Dictionary<SKTypeface, SKFont> _fonts = [];
    private readonly Func<SKTypeface, SKFont> _fontFor;
    private SKTypeface? _typeface;
    private float _fontSize;
    private List<SkUiShaping.Line> _lines = [];
    private double _brokenWidth = double.NaN;
    private SkUiTextDirection _direction;
    private SkUiTextRendering _rendering;
    // True when every paragraph fit on its lines without wrapping / truncation at _brokenWidth: any width ≥
    // _maxLineWidth then yields the same lines, so measure-at-constraint and draw-at-arranged-width share one layout.
    private bool _naturalFit;
    private float _maxLineWidth;

    internal SkUiTextLayout() => _fontFor = FontFor;

    /// <summary>Line layouts computed by this instance (diagnostics / tests).</summary>
    internal int LayoutCount { get; private set; }

    /// <summary>Whether the last layout used the simple (non-HarfBuzz) path for every paragraph (tests).</summary>
    internal bool LastLayoutSimple { get; private set; }

    /// <summary>Forgets broken lines (text, font, padding, direction or break policy changed).</summary>
    internal void Invalidate()
    {
        _brokenWidth = double.NaN;
        _naturalFit = false;
    }

    private static SkUiTextRendering Resolve(SkUiTextRendering rendering) =>
        rendering == SkUiTextRendering.Default ? SkUiTextOptions.DefaultRendering : rendering;

    private SkUiShaping.Paragraph ShapeParagraph(string text, SKFont primary, SkUiTextDirection direction)
    {
        var paragraph = ShapeParagraphCore(text, primary, direction);
        LastLayoutSimple &= paragraph.IsSimple;
        return paragraph;
    }

    private SkUiShaping.Paragraph ShapeParagraphCore(string text, SKFont primary, SkUiTextDirection direction)
    {
        switch (_rendering)
        {
            case SkUiTextRendering.Simple:
                return SkUiShaping.ShapeSimple(text, primary, direction);
            case SkUiTextRendering.Auto when SkUiShaping.TryShapeSimple(text, primary, direction) is { } simple:
                return simple;
            default:
                return SkUiShaping.Shape(text, primary.Typeface, _fontSize, direction, _fontFor);
        }
    }

    private SKFont FontFor(SKTypeface typeface)
    {
        if (!_fonts.TryGetValue(typeface, out var font))
            // Linear (unhinted) metrics: Skia measures like HarfBuzz shapes, so simple and shaped text agree on FreeType
            // hosts (Android, Linux) too, where hinted advances would otherwise differ by a fraction of a pixel per glyph.
            _fonts[typeface] = font = new SKFont(typeface, _fontSize) { LinearMetrics = true };
        return font;
    }

    private SKFont Primary(SKTypeface typeface, double size)
    {
        if (!ReferenceEquals(_typeface, typeface) || _fontSize != (float)size)
        {
            // Text blobs in recorded pictures hold their own typeface references, so old fonts can go.
            foreach (var font in _fonts.Values)
                font.Dispose();
            _fonts.Clear();
            _typeface = typeface;
            _fontSize = (float)size;
            _brokenWidth = double.NaN;
        }
        return FontFor(typeface);
    }

    private void EnsureLines(string text, double width, SKFont primary, SkUiCoreTextLineBreaker breaker, SkUiTextDirection direction, SkUiTextRendering rendering)
    {
        width = double.IsNaN(width) ? 0 : Math.Max(0, width);
        rendering = Resolve(rendering);
        if (direction == _direction && rendering == _rendering && !double.IsNaN(_brokenWidth)
            && (width == _brokenWidth || (_naturalFit && width >= _maxLineWidth)))
            return;
        _brokenWidth = width;
        _direction = direction;
        _rendering = rendering;
        _naturalFit = true;
        LayoutCount++;
        LastLayoutSimple = true;
        var lines = new List<SkUiShaping.Line>();
        if (text.Length > 0)
        {
            var mode = StockMode(breaker);
            if (mode is null)
            {
                // Custom breaker: it decides the logical lines; each line is shaped as its own paragraph.
                _naturalFit = false;
                foreach (var logical in breaker(text, width, primary))
                    lines.Add(ShapeLine(logical, primary, direction));
            }
            else
            {
                foreach (var paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                    LayoutParagraph(paragraph, width, mode.Value, primary, direction, lines);
            }
        }
        _lines = lines;
        _maxLineWidth = 0;
        foreach (var line in lines)
            _maxLineWidth = Math.Max(_maxLineWidth, line.Width);
    }

    private static LineBreakMode? StockMode(SkUiCoreTextLineBreaker breaker)
    {
        if (ReferenceEquals(breaker, SkUiCoreTextLineBreakers.WordWrap)) return LineBreakMode.WordWrap;
        if (ReferenceEquals(breaker, SkUiCoreTextLineBreakers.CharacterWrap)) return LineBreakMode.CharacterWrap;
        if (ReferenceEquals(breaker, SkUiCoreTextLineBreakers.NoWrap)) return LineBreakMode.NoWrap;
        if (ReferenceEquals(breaker, SkUiCoreTextLineBreakers.TailTruncation)) return LineBreakMode.TailTruncation;
        if (ReferenceEquals(breaker, SkUiCoreTextLineBreakers.HeadTruncation)) return LineBreakMode.HeadTruncation;
        if (ReferenceEquals(breaker, SkUiCoreTextLineBreakers.MiddleTruncation)) return LineBreakMode.MiddleTruncation;
        return null;
    }

    private SkUiShaping.Line ShapeLine(string text, SKFont primary, SkUiTextDirection direction)
    {
        var paragraph = ShapeParagraph(text, primary, direction);
        return SkUiShaping.BuildLine(paragraph, 0, text.Length, primary, _fontFor);
    }

    private void LayoutParagraph(string text, double width, LineBreakMode mode, SKFont primary, SkUiTextDirection direction, List<SkUiShaping.Line> lines)
    {
        var paragraph = ShapeParagraph(text, primary, direction);
        if (double.IsInfinity(width) || mode == LineBreakMode.NoWrap || paragraph.Width <= width)
        {
            lines.Add(SkUiShaping.BuildLine(paragraph, 0, text.Length, primary, _fontFor));
            return;
        }
        switch (mode)
        {
            case LineBreakMode.TailTruncation:
            case LineBreakMode.HeadTruncation:
            case LineBreakMode.MiddleTruncation:
                _naturalFit = false;
                lines.Add(ShapeLine(Truncate(paragraph, width, mode, primary), primary, direction));
                return;
            default:
                _naturalFit = false;
                Wrap(paragraph, width, mode == LineBreakMode.WordWrap, primary, lines);
                return;
        }
    }

    /// <summary>Greedy line breaking over shaped advances (grapheme-safe; words, hyphens and CJK boundaries).</summary>
    private void Wrap(SkUiShaping.Paragraph paragraph, double width, bool words, SKFont primary, List<SkUiShaping.Line> lines)
    {
        SkUiShaping.EnsureAdvances(paragraph, primary);
        var text = paragraph.Text;
        var advances = paragraph.Advances;
        var boundary = GraphemeStarts(text);
        var n = text.Length;
        var start = 0;
        while (start < n)
        {
            var used = 0.0;
            var index = start;
            var lastBreak = -1;
            while (index < n)
            {
                if (words && index > start && boundary[index] && CanBreakBefore(text, index))
                    lastBreak = index;
                var advance = advances[index];
                if (used + advance > width && index > start && text[index] != ' ')
                    break;
                used += advance;
                index++;
            }
            if (index >= n)
            {
                lines.Add(SkUiShaping.BuildLine(paragraph, start, n, primary, _fontFor));
                break;
            }
            int end;
            if (words && lastBreak > start)
                end = lastBreak;
            else
            {
                end = index;
                while (end > start && !boundary[end]) end--;
                if (end <= start)
                {
                    end = start + 1;
                    while (end < n && !boundary[end]) end++;
                }
            }
            var lineEnd = end;
            while (lineEnd > start && text[lineEnd - 1] == ' ') lineEnd--;
            lines.Add(SkUiShaping.BuildLine(paragraph, start, lineEnd, primary, _fontFor));
            start = end;
            while (start < n && text[start] == ' ') start++;
        }
    }

    private static bool[] GraphemeStarts(string text)
    {
        var starts = new bool[text.Length + 1];
        foreach (var index in System.Globalization.StringInfo.ParseCombiningCharacters(text))
            starts[index] = true;
        starts[text.Length] = true;
        return starts;
    }

    /// <summary>Break opportunity before <paramref name="index"/>: after a space or hyphen, or next to CJK ideographs / kana.</summary>
    private static bool CanBreakBefore(string text, int index)
    {
        var previous = text[index - 1];
        if (previous == ' ' || (previous == '-' && text[index] != ' '))
            return true;
        return IsCjk(text.ConvertToUtf32OrReplacement(index)) || IsCjk(previous);
    }

    private static bool IsCjk(int cp) =>
        cp is >= 0x2E80 and <= 0x9FFF or >= 0xF900 and <= 0xFAFF or >= 0x20000 and <= 0x3FFFF or >= 0xFF66 and <= 0xFF9D;

    /// <summary>Ellipsizes a logical string so its shaped width fits (tail / head / middle).</summary>
    private static string Truncate(SkUiShaping.Paragraph paragraph, double width, LineBreakMode mode, SKFont primary)
    {
        SkUiShaping.EnsureAdvances(paragraph, primary);
        var ellipsis = primary.MeasureText(Ellipsis);
        if (ellipsis > width)
            return string.Empty;
        var text = paragraph.Text;
        var advances = paragraph.Advances;
        var boundary = GraphemeStarts(text);
        var available = width - ellipsis;

        int Prefix(double budget)
        {
            var used = 0.0;
            var fit = 0;
            for (var index = 0; index < text.Length; index++)
            {
                used += advances[index];
                if (used > budget) break;
                if (boundary[index + 1]) fit = index + 1;
            }
            return fit;
        }

        int SuffixStart(double budget)
        {
            var used = 0.0;
            var fit = text.Length;
            for (var index = text.Length - 1; index >= 0; index--)
            {
                used += advances[index];
                if (used > budget) break;
                if (boundary[index]) fit = index;
            }
            return fit;
        }

        return mode switch
        {
            LineBreakMode.HeadTruncation => Ellipsis + text[SuffixStart(available)..],
            LineBreakMode.MiddleTruncation => text[..Prefix(available / 2)] + Ellipsis + text[SuffixStart(available / 2)..],
            _ => text[..Prefix(available)] + Ellipsis
        };
    }

    /// <summary>Content size in DIPs including <paramref name="padding"/>.</summary>
    internal Size Measure(string text, SKTypeface typeface, double fontSize, Thickness padding, double widthConstraint,
        SkUiCoreTextLineBreaker breaker, SkUiTextDirection direction = SkUiTextDirection.Auto,
        SkUiTextRendering rendering = SkUiTextRendering.Default)
    {
        var primary = Primary(typeface, fontSize);
        var contentWidth = double.IsInfinity(widthConstraint)
            ? double.PositiveInfinity
            : Math.Max(0, widthConstraint - padding.HorizontalThickness);
        EnsureLines(text, contentWidth, primary, breaker, direction, rendering);
        var width = 0f;
        var height = 0f;
        foreach (var line in _lines)
        {
            width = Math.Max(width, line.Width);
            height += line.Height;
        }
        return new Size(width + padding.HorizontalThickness, height + padding.VerticalThickness);
    }

    /// <summary>
    /// Draws the text into a <paramref name="width"/>×<paramref name="height"/> slot. <see cref="TextAlignment.Start"/>
    /// and <see cref="TextAlignment.End"/> follow each paragraph's direction (Start is the right edge for RTL).
    /// </summary>
    internal void Draw(SKCanvas canvas, string text, SKTypeface typeface, double fontSize, Thickness padding,
        double width, double height, TextAlignment horizontal, TextAlignment vertical, SKPaint paint,
        SkUiCoreTextLineBreaker breaker, SkUiTextDirection direction = SkUiTextDirection.Auto,
        SkUiTextRendering rendering = SkUiTextRendering.Default)
    {
        if (text.Length == 0)
            return;
        var primary = Primary(typeface, fontSize);
        var available = Math.Max(0, width - padding.HorizontalThickness);
        EnsureLines(text, available, primary, breaker, direction, rendering);
        var total = 0f;
        foreach (var line in _lines)
            total += line.Height;
        var offset = vertical == TextAlignment.Center ? (height - padding.VerticalThickness - total) / 2
            : vertical == TextAlignment.End ? height - padding.VerticalThickness - total : 0;
        var top = (float)(padding.Top + Math.Max(0, offset));
        foreach (var line in _lines)
        {
            if (line.Blob is not null || line.Text is not null)
            {
                var rtl = line.BaseLevel % 2 == 1;
                var alignment = horizontal switch
                {
                    TextAlignment.Start => rtl ? TextAlignment.End : TextAlignment.Start,
                    TextAlignment.End => rtl ? TextAlignment.Start : TextAlignment.End,
                    _ => horizontal
                };
                var left = padding.Left + (alignment == TextAlignment.Center ? (available - line.Width) / 2
                    : alignment == TextAlignment.End ? available - line.Width : 0);
                if (line.Blob is { } blob)
                    canvas.DrawText(blob, (float)left, top + line.Ascent, paint);
                else
                    canvas.DrawText(line.Text!, (float)left, top + line.Ascent, SKTextAlign.Left, primary, paint);
            }
            top += line.Height;
        }
    }
}
