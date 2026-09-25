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

/// <summary>
/// Shared text measure / paint engine for <see cref="SkUiLabel"/> and <see cref="SkUiCoreLabel"/> (one
/// implementation for both layers). Caches the font and the broken lines with their widths, keyed by the
/// wrap width, so paint-only changes (color, alignment) and re-paints never re-run line breaking.
/// </summary>
internal sealed class SkUiTextLayout
{
    private SKFont? _font;
    private SKTypeface? _fontTypeface;
    private float _fontSize;
    private string[] _lines = [];
    private float[] _widths = [];
    private double _brokenWidth = double.NaN;

    /// <summary>Forgets broken lines (text, font, padding or break policy changed).</summary>
    internal void Invalidate() => _brokenWidth = double.NaN;

    private SKFont Font(SKTypeface typeface, double size)
    {
        if (_font is null || !ReferenceEquals(_fontTypeface, typeface) || _fontSize != (float)size)
        {
            // Text blobs in recorded pictures hold their own typeface reference, so the old font can go.
            _font?.Dispose();
            _font = new SKFont(typeface, (float)size);
            _fontTypeface = typeface;
            _fontSize = (float)size;
            _brokenWidth = double.NaN;
        }
        return _font;
    }

    private void EnsureLines(string text, double width, SKFont font, SkUiCoreTextLineBreaker breaker)
    {
        width = double.IsNaN(width) ? 0 : Math.Max(0, width);
        if (width == _brokenWidth)
            return;
        _brokenWidth = width;
        if (text.Length == 0)
        {
            _lines = [];
            _widths = [];
            return;
        }
        var broken = breaker(text, width, font);
        _lines = broken as string[] ?? [.. broken];
        _widths = new float[_lines.Length];
        for (var index = 0; index < _lines.Length; index++)
            _widths[index] = font.MeasureText(_lines[index]);
    }

    /// <summary>Content size in DIPs including <paramref name="padding"/>.</summary>
    internal Size Measure(string text, SKTypeface typeface, double fontSize, Thickness padding, double widthConstraint, SkUiCoreTextLineBreaker breaker)
    {
        var font = Font(typeface, fontSize);
        var contentWidth = double.IsInfinity(widthConstraint)
            ? double.PositiveInfinity
            : Math.Max(0, widthConstraint - padding.HorizontalThickness);
        EnsureLines(text, contentWidth, font, breaker);
        var width = 0f;
        foreach (var lineWidth in _widths)
            width = Math.Max(width, lineWidth);
        return new Size(width + padding.HorizontalThickness, _lines.Length * font.Spacing + padding.VerticalThickness);
    }

    /// <summary>Draws the text into a <paramref name="width"/>×<paramref name="height"/> slot.</summary>
    internal void Draw(SKCanvas canvas, string text, SKTypeface typeface, double fontSize, Thickness padding,
        double width, double height, TextAlignment horizontal, TextAlignment vertical, SKPaint paint, SkUiCoreTextLineBreaker breaker)
    {
        if (text.Length == 0)
            return;
        var font = Font(typeface, fontSize);
        var available = Math.Max(0, width - padding.HorizontalThickness);
        EnsureLines(text, available, font, breaker);
        var total = _lines.Length * font.Spacing;
        var offset = vertical == TextAlignment.Center ? (height - padding.VerticalThickness - total) / 2
            : vertical == TextAlignment.End ? height - padding.VerticalThickness - total : 0;
        var baseline = (float)(padding.Top + Math.Max(0, offset)) - font.Metrics.Ascent;
        for (var index = 0; index < _lines.Length; index++)
        {
            var lineWidth = _widths[index];
            var left = padding.Left + (horizontal == TextAlignment.Center ? (available - lineWidth) / 2
                : horizontal == TextAlignment.End ? available - lineWidth : 0);
            canvas.DrawText(_lines[index], (float)left, baseline, SKTextAlign.Left, font, paint);
            baseline += font.Spacing;
        }
    }
}
