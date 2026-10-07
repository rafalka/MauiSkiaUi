using System.Collections.Concurrent;
using MauiSkiaUi.Core;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Process-wide typeface cache. System lookups (<see cref="SKFontManager.MatchFamily(string, SKFontStyle)"/>) are
/// expensive, so each (family, attributes) pair is resolved once. Registered fonts (<see cref="SkUiFonts"/>) win over
/// system fonts. A face without the requested weight or slant (a registered font with no bold file) is drawn with
/// synthetic bold / italic by the text engine (<see cref="SkUiTextLayout"/>), as Android does.
/// Cached typefaces are never disposed: retained pictures may reference them.
/// </summary>
internal static class SkUiTypefaces
{
    private static readonly ConcurrentDictionary<(string Family, FontAttributes Attributes), SKTypeface> SystemCache = new();
    private static readonly ConcurrentDictionary<(SKTypeface Registered, FontAttributes Attributes), SKTypeface> RegisteredCache = new();

    /// <summary>
    /// The platform's default family by name: an empty family name matches the default face but ignores the
    /// requested style (CoreText, Android), while the same family by name gives its bold and italic faces.
    /// </summary>
    private static readonly Lazy<string> DefaultFamily = new(() => SKTypeface.Default.FamilyName ?? string.Empty);

    internal static SKTypeface Resolve(string? family, FontAttributes attributes = FontAttributes.None)
    {
        if (!string.IsNullOrEmpty(family) && SkUiFonts.TryResolve(family) is { } registered)
            return attributes == FontAttributes.None
                ? registered
                : RegisteredCache.GetOrAdd((registered, attributes), static key => StyledFace(key.Registered.FamilyName, key.Attributes) ?? key.Registered);
        return SystemCache.GetOrAdd((family ?? string.Empty, attributes), static key =>
            Match(key.Family, key.Attributes) ?? Match(DefaultFamily.Value, key.Attributes) ?? SKTypeface.Default);
    }

    /// <summary>Whether <paramref name="typeface"/> is bold (semibold or heavier); else the engine synthesizes bold.</summary>
    internal static bool HasBold(SKTypeface typeface) => typeface.FontWeight >= (int)SKFontStyleWeight.SemiBold;

    /// <summary>Whether <paramref name="typeface"/> is italic or oblique; else the engine synthesizes the slant.</summary>
    internal static bool HasItalic(SKTypeface typeface) => typeface.FontSlant != SKFontStyleSlant.Upright;

    private static SKTypeface? Match(string family, FontAttributes attributes)
    {
        if (family.Length == 0)
            return null;
        using var style = Style(attributes);
        return SKFontManager.Default.MatchFamily(family, style);
    }

    /// <summary>
    /// The closest face of <paramref name="family"/> to the requested style, when the font manager knows the family
    /// (fonts MAUI registered with CoreText; a registered font that is also installed); the engine synthesizes what it
    /// still lacks (bold-italic from a bold face). Android asset fonts are not in the font manager: <c>null</c>.
    /// </summary>
    private static SKTypeface? StyledFace(string? family, FontAttributes attributes) =>
        !string.IsNullOrEmpty(family) && Match(family, attributes) is { } face && face.FamilyName == family ? face : null;

    private static SKFontStyle Style(FontAttributes attributes) => new(
        attributes.HasFlag(FontAttributes.Bold) ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
        SKFontStyleWidth.Normal,
        attributes.HasFlag(FontAttributes.Italic) ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
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
    private static readonly SkUiWeakEvent _changed = new();

    /// <summary>
    /// Rendering used by labels whose <c>TextRendering</c> is <see cref="SkUiTextRendering.Default"/> (initially
    /// <see cref="SkUiTextRendering.Auto"/>). Best set once at startup; a change re-measures and redraws live surfaces (the
    /// modes lay text out slightly differently, so text measured in one mode must not be drawn in the other).
    /// </summary>
    public static SkUiTextRendering DefaultRendering
    {
        get => _defaultRendering;
        set
        {
            var rendering = value == SkUiTextRendering.Default ? SkUiTextRendering.Auto : value;
            if (rendering == _defaultRendering)
                return;
            _defaultRendering = rendering;
            _changed.Raise(null, EventArgs.Empty);
        }
    }

    /// <summary>Raised when <see cref="DefaultRendering"/> changes (live surfaces re-measure and redraw); subscribers are not kept alive.</summary>
    internal static event EventHandler? Changed
    {
        add => _changed.Add(value);
        remove => _changed.Remove(value);
    }
}

/// <summary>MAUI's <see cref="TextTransform"/> as its controls apply it (invariant culture).</summary>
internal static class SkUiTextTransform
{
    internal static string Apply(string text, TextTransform transform) => transform switch
    {
        TextTransform.Lowercase => text.ToLowerInvariant(),
        TextTransform.Uppercase => text.ToUpperInvariant(),
        _ => text
    };
}

/// <summary>
/// What decides the lines of drawn text: font, line breaking, line height, spacing, direction and rendering. Colors,
/// alignment and decorations are paint-only and passed to <see cref="SkUiTextLayout.Draw"/>.
/// </summary>
/// <param name="Typeface">Primary typeface (family and attributes resolved).</param>
/// <param name="FontSize">Font size in DIPs.</param>
/// <param name="LineBreakMode">Stock wrapping / truncation, also what a custom breaker's <see cref="SkUiTextLineBreakContext.Break()"/> applies.</param>
/// <param name="LineBreaker">A custom breaker, or <c>null</c> for <paramref name="LineBreakMode"/>; a stock breaker
/// (<see cref="SkUiTextLineBreakers"/>) is its mode.</param>
/// <param name="MaxLines">The most lines painted; 0 / negative: no limit.</param>
/// <param name="LineHeight">Multiplier of the font's line spacing; 0 / negative: the font's.</param>
/// <param name="CharacterSpacing">DIPs added after each character.</param>
/// <param name="Direction">Paragraph direction.</param>
/// <param name="Rendering">Shaping mode.</param>
/// <param name="FontAttributes">Requested bold / italic: drawn synthetically when <paramref name="Typeface"/> lacks them.</param>
/// <param name="Justify">Horizontal <see cref="TextAlignment.Justify"/>: wrapped lines but a paragraph's last are stretched to the width.</param>
internal readonly record struct SkUiTextStyle(
    SKTypeface Typeface,
    double FontSize,
    LineBreakMode LineBreakMode = LineBreakMode.WordWrap,
    SkUiTextLineBreaker? LineBreaker = null,
    int MaxLines = -1,
    double LineHeight = -1,
    double CharacterSpacing = 0,
    SkUiTextDirection Direction = SkUiTextDirection.Auto,
    SkUiTextRendering Rendering = SkUiTextRendering.Default,
    FontAttributes FontAttributes = FontAttributes.None,
    bool Justify = false);

/// <summary>
/// Shared text measure / paint engine for <see cref="SkUiLabel"/> and <see cref="SkUiCoreLabel"/> (one
/// implementation for both layers). Text is shaped with HarfBuzz (<see cref="SkUiShaping"/>): complex scripts,
/// ligatures, kerning, bidirectional (RTL / mixed) text and per-character font fallback. Broken, shaped lines
/// are cached as text blobs keyed by wrap width and <see cref="SkUiTextStyle"/>, so paint-only changes (color,
/// alignment, decorations) and re-paints never re-shape or re-wrap.
/// </summary>
internal sealed class SkUiTextLayout
{
    internal const string DefaultEllipsis = "...";
    private readonly Dictionary<SKTypeface, SKFont> _fonts = [];
    private readonly Func<SKTypeface, SKFont> _fontFor;
    // Stock breaking scratch, shared per thread (layout runs on the UI thread and never nests a stock break).
    [ThreadStatic] private static List<LineSpec>? _specs;
    private SKTypeface? _typeface;
    private float _fontSize;
    private FontAttributes _fontAttributes;
    private List<SkUiShaping.Line> _lines = [];
    private double _brokenWidth = double.NaN;
    // Style of the current lines (rendering resolved); shaping reads its direction, rendering and spacing.
    private SkUiTextStyle _style;
    // True when every paragraph fit on its lines without wrapping / truncation at _brokenWidth: any width ≥
    // _maxLineWidth then yields the same lines, so measure-at-constraint and draw-at-arranged-width share one layout.
    private bool _naturalFit;
    private float _maxLineWidth;

    /// <summary>
    /// A line before shaping: a range of a shaped paragraph, or a synthesized string (a truncation).
    /// <paramref name="Wrapped"/>: the paragraph continues on the next line (what justification stretches).
    /// </summary>
    internal readonly record struct LineSpec(SkUiShaping.Paragraph? Paragraph, int Start, int End, string? Text, bool Wrapped = false);

    /// <param name="owner">The label, handed to custom breakers as <see cref="SkUiTextLineBreakContext.Owner"/>.</param>
    internal SkUiTextLayout(object? owner = null)
    {
        Owner = owner;
        _fontFor = FontFor;
    }

    /// <summary>The label this layout belongs to.</summary>
    internal object? Owner { get; }

    /// <summary>Line layouts computed by this instance (diagnostics / tests).</summary>
    internal int LayoutCount { get; private set; }

    /// <summary>Whether the last layout used the simple (non-HarfBuzz) path for every paragraph (tests).</summary>
    internal bool LastLayoutSimple { get; private set; }

    /// <summary>Forgets broken lines (text or padding changed, or a custom breaker's inputs).</summary>
    internal void Invalidate()
    {
        _brokenWidth = double.NaN;
        _naturalFit = false;
    }

    private static SkUiTextRendering Resolve(SkUiTextRendering rendering) =>
        rendering == SkUiTextRendering.Default ? SkUiTextOptions.DefaultRendering : rendering;

    private SkUiShaping.Paragraph ShapeParagraph(string text, SKFont primary)
    {
        var paragraph = ShapeParagraphCore(text, primary);
        LastLayoutSimple &= paragraph.IsSimple;
        return paragraph;
    }

    private SkUiShaping.Paragraph ShapeParagraphCore(string text, SKFont primary)
    {
        var direction = _style.Direction;
        var spacing = (float)_style.CharacterSpacing;
        switch (_style.Rendering)
        {
            case SkUiTextRendering.Simple:
                return SkUiShaping.ShapeSimple(text, primary, direction, spacing);
            case SkUiTextRendering.Auto when SkUiShaping.TryShapeSimple(text, primary, direction, spacing) is { } simple:
                return simple;
            default:
                return SkUiShaping.Shape(text, primary.Typeface, _fontSize, direction, _fontFor, spacing);
        }
    }

    private SKFont FontFor(SKTypeface typeface)
    {
        if (!_fonts.TryGetValue(typeface, out var font))
            _fonts[typeface] = font = CreateFont(typeface, _fontSize, _fontAttributes);
        return font;
    }

    /// <summary>A font for drawn text: <paramref name="typeface"/> at <paramref name="size"/>, synthesizing the requested attributes it lacks.</summary>
    internal static SKFont CreateFont(SKTypeface typeface, float size, FontAttributes attributes)
    {
        // Linear (unhinted) metrics: Skia measures like HarfBuzz shapes, so simple and shaped text agree on FreeType
        // hosts (Android, Linux) too, where hinted advances would otherwise differ by a fraction of a pixel per glyph.
        var font = new SKFont(typeface, size) { LinearMetrics = true };
        // Synthetic styles for a face that lacks them (a registered font without a bold / italic file), as
        // Android's Typeface.create does: emboldened outlines and a 14° slant. Per face, so fallback fonts too.
        if (attributes.HasFlag(FontAttributes.Bold) && !SkUiTypefaces.HasBold(typeface))
            font.Embolden = true;
        if (attributes.HasFlag(FontAttributes.Italic) && !SkUiTypefaces.HasItalic(typeface))
            font.SkewX = -0.25f;
        return font;
    }

    private SKFont Primary(in SkUiTextStyle style)
    {
        var (typeface, size) = (style.Typeface, style.FontSize);
        if (!ReferenceEquals(_typeface, typeface) || _fontSize != (float)size || _fontAttributes != style.FontAttributes)
        {
            // Text blobs in recorded pictures hold their own typeface references, so old fonts can go.
            foreach (var font in _fonts.Values)
                font.Dispose();
            _fonts.Clear();
            _typeface = typeface;
            _fontSize = (float)size;
            _fontAttributes = style.FontAttributes;
            _brokenWidth = double.NaN;
        }
        return FontFor(typeface);
    }

    private void EnsureLines(string text, double width, SkUiTextStyle style, SKFont primary)
    {
        width = double.IsNaN(width) ? 0 : Math.Max(0, width);
        style = style with { Rendering = Resolve(style.Rendering) };
        if (style == _style && !double.IsNaN(_brokenWidth)
            && (width == _brokenWidth || (_naturalFit && width >= _maxLineWidth)))
            return;
        _brokenWidth = double.NaN; // until the lines are complete (a custom breaker may throw)
        _style = style;
        LayoutCount++;
        LastLayoutSimple = true;
        var lines = new List<SkUiShaping.Line>();
        var natural = true;
        if (text.Length > 0)
        {
            var mode = style.LineBreaker is { } breaker ? StockMode(breaker) : style.LineBreakMode;
            if (mode is null)
            {
                // Custom breaker: it decides the logical lines; each line is shaped as its own paragraph.
                natural = false;
                var logical = style.LineBreaker!(new SkUiTextLineBreakContext(this, text, width, style.MaxLines, style.LineBreakMode, primary));
                foreach (var line in logical ?? [])
                {
                    if (style.MaxLines > 0 && lines.Count == style.MaxLines)
                        break;
                    lines.Add(ShapeLine(line ?? string.Empty, primary));
                }
            }
            else
            {
                var specs = _specs ??= [];
                specs.Clear();
                try
                {
                    natural = BreakStock(text, width, mode.Value, style.MaxLines, DefaultEllipsis, primary, specs);
                    var justifyWidth = style.Justify ? (float)width : (float?)null;
                    foreach (var spec in specs)
                        lines.Add(spec.Text is { } synthesized
                            ? ShapeLine(synthesized, primary)
                            : SkUiShaping.BuildLine(spec.Paragraph!, spec.Start, spec.End, primary, _fontFor, spec.Wrapped ? justifyWidth : null));
                }
                finally
                {
                    specs.Clear(); // releases the shaped paragraphs
                }
            }
        }
        ApplyLineHeight(lines, style.LineHeight);
        _lines = lines;
        _naturalFit = natural;
        _brokenWidth = width;
        _maxLineWidth = 0;
        foreach (var line in lines)
            _maxLineWidth = Math.Max(_maxLineWidth, line.Width);
    }

    private static LineBreakMode? StockMode(SkUiTextLineBreaker breaker)
    {
        if (ReferenceEquals(breaker, SkUiTextLineBreakers.WordWrap)) return LineBreakMode.WordWrap;
        if (ReferenceEquals(breaker, SkUiTextLineBreakers.CharacterWrap)) return LineBreakMode.CharacterWrap;
        if (ReferenceEquals(breaker, SkUiTextLineBreakers.NoWrap)) return LineBreakMode.NoWrap;
        if (ReferenceEquals(breaker, SkUiTextLineBreakers.TailTruncation)) return LineBreakMode.TailTruncation;
        if (ReferenceEquals(breaker, SkUiTextLineBreakers.HeadTruncation)) return LineBreakMode.HeadTruncation;
        if (ReferenceEquals(breaker, SkUiTextLineBreakers.MiddleTruncation)) return LineBreakMode.MiddleTruncation;
        return null;
    }

    /// <summary>
    /// MAUI's <c>LineHeight</c>: each line is <paramref name="multiplier"/> × its spacing high, the extra (or missing)
    /// space split above and below the glyphs (half-leading, as CSS <c>line-height</c>).
    /// </summary>
    private static void ApplyLineHeight(List<SkUiShaping.Line> lines, double multiplier)
    {
        if (!(multiplier > 0) || multiplier == 1)
            return;
        foreach (var line in lines)
        {
            var height = (float)(line.Height * multiplier);
            line.Ascent += (height - line.Height) / 2;
            line.Height = height;
        }
    }

    private static string[] SplitParagraphs(string text) =>
        text.AsSpan().IndexOfAny('\r', '\n') < 0 ? [text] : text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private SkUiShaping.Line ShapeLine(string text, SKFont primary)
    {
        var paragraph = ShapeParagraph(text, primary);
        return SkUiShaping.BuildLine(paragraph, 0, text.Length, primary, _fontFor);
    }

    /// <summary>Width of <paramref name="text"/> on one line per paragraph, as drawn with the current style (custom breakers).</summary>
    internal double MeasureUnbroken(string text, SKFont primary)
    {
        var widest = 0f;
        foreach (var paragraph in SplitParagraphs(text))
            widest = Math.Max(widest, ShapeParagraphCore(paragraph, primary).Width);
        return widest;
    }

    /// <summary>The stock breaking of <paramref name="text"/> as strings, with the current style (custom breakers).</summary>
    internal IReadOnlyList<string> BreakToStrings(string text, double width, LineBreakMode mode, int maxLines, string ellipsis, SKFont primary)
    {
        if (text.Length == 0)
            return [];
        var specs = new List<LineSpec>();
        BreakStock(text, double.IsNaN(width) ? 0 : Math.Max(0, width), mode, maxLines, ellipsis, primary, specs);
        var lines = new string[specs.Count];
        for (var index = 0; index < specs.Count; index++)
        {
            var spec = specs[index];
            var source = spec.Paragraph?.Text ?? string.Empty;
            lines[index] = spec.Text ?? (spec.Start == 0 && spec.End == source.Length ? source : source[spec.Start..spec.End]);
        }
        return lines;
    }

    /// <summary>
    /// Breaks <paramref name="text"/> into <paramref name="specs"/> for a stock <paramref name="mode"/>, keeping at most
    /// <paramref name="maxLines"/> lines (MAUI's <c>MaxLines</c>): wrapped lines past it are dropped; with tail truncation
    /// the text wraps and its last kept line ends with <paramref name="ellipsis"/> when more text follows.
    /// </summary>
    /// <returns>Whether the lines are the same at any width at least as wide as the widest (nothing wrapped or truncated).</returns>
    private bool BreakStock(string text, double width, LineBreakMode mode, int maxLines, string ellipsis, SKFont primary, List<LineSpec> specs)
    {
        var natural = true;
        var limit = maxLines > 0 ? maxLines : int.MaxValue;
        var wrapTail = mode == LineBreakMode.TailTruncation && maxLines > 0;
        foreach (var source in SplitParagraphs(text))
        {
            if (specs.Count == limit)
            {
                if (wrapTail)
                {
                    EllipsizeLast(specs, width, ellipsis, primary);
                    natural = false;
                }
                break;
            }
            var paragraph = ShapeParagraph(source, primary);
            if (double.IsInfinity(width) || mode == LineBreakMode.NoWrap || paragraph.Width <= width)
            {
                specs.Add(new LineSpec(paragraph, 0, source.Length, null));
                continue;
            }
            natural = false;
            switch (mode)
            {
                case LineBreakMode.HeadTruncation:
                case LineBreakMode.MiddleTruncation:
                case LineBreakMode.TailTruncation when !wrapTail:
                    specs.Add(new LineSpec(null, 0, 0, Truncate(paragraph, 0, width, mode, ellipsis, primary)));
                    break;
                default:
                    if (Wrap(paragraph, width, mode != LineBreakMode.CharacterWrap, primary, specs, limit) && wrapTail)
                    {
                        EllipsizeLast(specs, width, ellipsis, primary);
                        return false;
                    }
                    break;
            }
        }
        return natural;
    }

    /// <summary>Replaces the last line by the rest of its paragraph, tail-truncated so it ends with <paramref name="ellipsis"/>.</summary>
    private void EllipsizeLast(List<LineSpec> specs, double width, string ellipsis, SKFont primary)
    {
        if (specs[^1] is { Paragraph: { } paragraph, Start: var start })
            specs[^1] = new LineSpec(null, 0, 0, Truncate(paragraph, start, width, LineBreakMode.TailTruncation, ellipsis, primary));
    }

    /// <summary>
    /// Greedy line breaking over shaped advances (grapheme-safe; words, hyphens and CJK boundaries), adding lines until
    /// <paramref name="specs"/> holds <paramref name="limit"/>. <paramref name="primary"/> fills a simple paragraph's advances
    /// on demand (<c>null</c> for shaped paragraphs).
    /// </summary>
    /// <returns>Whether text was left over at the limit.</returns>
    internal static bool Wrap(SkUiShaping.Paragraph paragraph, double width, bool words, SKFont? primary, List<LineSpec> specs, int limit)
    {
        if (primary is not null)
            SkUiShaping.EnsureAdvances(paragraph, primary);
        var text = paragraph.Text;
        var advances = paragraph.Advances;
        var boundary = GraphemeStarts(text);
        var n = text.Length;
        var start = 0;
        while (start < n)
        {
            if (specs.Count == limit)
                return true;
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
                specs.Add(new LineSpec(paragraph, start, n, null));
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
            specs.Add(new LineSpec(paragraph, start, lineEnd, null, Wrapped: true));
            start = end;
            while (start < n && text[start] == ' ') start++;
        }
        return false;
    }

    internal static bool[] GraphemeStarts(string text)
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

    /// <summary>
    /// Ellipsizes <c>paragraph.Text[start..]</c> so its shaped width fits (tail / head / middle). Tail truncation always
    /// ends with the ellipsis, also when the text fits (the last line kept by <c>MaxLines</c>).
    /// </summary>
    private string Truncate(SkUiShaping.Paragraph paragraph, int start, double width, LineBreakMode mode, string ellipsis, SKFont primary)
    {
        SkUiShaping.EnsureAdvances(paragraph, primary);
        var ellipsisWidth = MeasureUnbroken(ellipsis, primary);
        if (ellipsisWidth > width)
            return string.Empty;
        var text = paragraph.Text;
        var advances = paragraph.Advances;
        var boundary = GraphemeStarts(text);
        var available = width - ellipsisWidth;

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
            return fit;
        }

        return mode switch
        {
            LineBreakMode.HeadTruncation => ellipsis + text[SuffixStart(available)..].TrimStart(' '),
            LineBreakMode.MiddleTruncation => text[start..PrefixEnd(available / 2)].TrimEnd(' ') + ellipsis + text[SuffixStart(available / 2)..].TrimStart(' '),
            _ => text[start..PrefixEnd(available)].TrimEnd(' ') + ellipsis
        };
    }

    /// <summary>Content size in DIPs including <paramref name="padding"/>.</summary>
    internal Size Measure(string text, in SkUiTextStyle style, Thickness padding, double widthConstraint)
    {
        var primary = Primary(style);
        var contentWidth = double.IsInfinity(widthConstraint)
            ? double.PositiveInfinity
            : Math.Max(0, widthConstraint - padding.HorizontalThickness);
        EnsureLines(text, contentWidth, style, primary);
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
    /// Horizontal <see cref="TextAlignment.Justify"/> lines were stretched by the layout (<see cref="SkUiTextStyle.Justify"/>),
    /// a paragraph's last line is at Start; vertical Justify spreads the lines over the height (one line: Start).
    /// <paramref name="decorations"/> are drawn under / through each line's glyphs in the text paint.
    /// </summary>
    internal void Draw(SKCanvas canvas, string text, in SkUiTextStyle style, Thickness padding,
        double width, double height, TextAlignment horizontal, TextAlignment vertical, SKPaint paint,
        TextDecorations decorations = TextDecorations.None)
    {
        if (text.Length == 0)
            return;
        var primary = Primary(style);
        var available = Math.Max(0, width - padding.HorizontalThickness);
        EnsureLines(text, available, style, primary);
        var total = 0f;
        foreach (var line in _lines)
            total += line.Height;
        var (top, gap) = PlaceVertically(total, _lines.Count, padding, height, vertical);
        var metrics = decorations == TextDecorations.None ? default : primary.Metrics;
        foreach (var line in _lines)
        {
            if (line.Blob is not null || line.Text is not null)
            {
                var left = LineLeft(line.BaseLevel, line.Width, horizontal, padding, available);
                var baseline = top + line.Ascent;
                if (line.Blob is { } blob)
                    canvas.DrawText(blob, left, baseline, paint);
                else
                    canvas.DrawText(line.Text!, left, baseline, SKTextAlign.Left, primary, paint);
                if (decorations != TextDecorations.None)
                    DrawDecorations(canvas, decorations, left, baseline, line.Width, primary.Size, metrics, paint);
            }
            top += line.Height + gap;
        }
    }

    /// <summary>
    /// Where the first line starts (<paramref name="vertical"/> alignment of <paramref name="total"/> DIPs of lines in the
    /// padded <paramref name="height"/>) and the gap added between lines (vertical Justify; one line: Start).
    /// </summary>
    internal static (float Top, float Gap) PlaceVertically(float total, int lineCount, Thickness padding, double height, TextAlignment vertical)
    {
        var free = height - padding.VerticalThickness - total;
        var offset = vertical == TextAlignment.Center ? free / 2 : vertical == TextAlignment.End ? free : 0;
        var gap = vertical == TextAlignment.Justify && lineCount > 1 && free > 0 ? (float)(free / (lineCount - 1)) : 0;
        return ((float)(padding.Top + Math.Max(0, offset)), gap);
    }

    /// <summary>
    /// Left edge of a line <paramref name="lineWidth"/> wide in the <paramref name="available"/> content width:
    /// <see cref="TextAlignment.Start"/> and <see cref="TextAlignment.End"/> follow the line's paragraph direction (Start
    /// is the right edge for RTL); justified lines were stretched by the layout and sit at Start. A line wider than the
    /// content (<see cref="LineBreakMode.NoWrap"/>) starts at Start whatever the alignment, as native labels and buttons
    /// show it: its beginning stays visible.
    /// </summary>
    internal static float LineLeft(byte baseLevel, float lineWidth, TextAlignment horizontal, Thickness padding, double available)
    {
        var rtl = baseLevel % 2 == 1;
        if (lineWidth > available)
            horizontal = TextAlignment.Start;
        var alignment = horizontal switch
        {
            TextAlignment.Start or TextAlignment.Justify => rtl ? TextAlignment.End : TextAlignment.Start,
            TextAlignment.End => rtl ? TextAlignment.Start : TextAlignment.End,
            _ => horizontal
        };
        return (float)(padding.Left + (alignment == TextAlignment.Center ? (available - lineWidth) / 2
            : alignment == TextAlignment.End ? available - lineWidth : 0));
    }

    /// <summary>
    /// Underline / strikethrough across a line (all its bidi runs, in visual order) at the primary font's positions,
    /// or at proportional ones for fonts that report none.
    /// </summary>
    internal static void DrawDecorations(SKCanvas canvas, TextDecorations decorations, float left, float baseline, float width, float size, in SKFontMetrics metrics, SKPaint paint)
    {
        if (decorations.HasFlag(TextDecorations.Underline))
        {
            var thickness = metrics.UnderlineThickness is { } underline && underline > 0 ? underline : size / 16;
            // Skia: distance from the baseline to the top of the stroke.
            var position = metrics.UnderlinePosition ?? size / 10;
            canvas.DrawRect(left, baseline + position, width, thickness, paint);
        }
        if (decorations.HasFlag(TextDecorations.Strikethrough))
        {
            var thickness = metrics.StrikeoutThickness is { } strikeout && strikeout > 0 ? strikeout : size / 16;
            // Skia: distance from the baseline to the bottom of the stroke (negative: above the baseline).
            var position = metrics.StrikeoutPosition ?? (metrics.XHeight > 0 ? -metrics.XHeight / 2 + thickness / 2 : -size * 0.3f);
            canvas.DrawRect(left, baseline + position - thickness, width, thickness, paint);
        }
    }
}
