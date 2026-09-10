using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>Drawn, left-to-right text with wrapping, alignment, and MAUI-style bindable properties.</summary>
public class SkUiLabel : SkUiView
{
    private string text = string.Empty;
    private Color textColor = Colors.Black;
    private double fontSize = 16;
    private string? fontFamily;
    private FontAttributes fontAttributes;
    private LineBreakMode lineBreakMode = LineBreakMode.WordWrap;
    private TextAlignment horizontalTextAlignment;
    private TextAlignment verticalTextAlignment;
    private Thickness padding;
    private string[] lines = [];
    private double lineWidth = double.NaN;

    /// <summary>Bindable text.</summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(SkUiLabel), string.Empty, propertyChanged: (view, _, value) => ((SkUiLabel)view).SetText((string?)value));
    /// <summary>Bindable foreground color.</summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SkUiLabel), Colors.Black, defaultValueCreator: view => ((SkUiLabel)view).DefaultTextColor, propertyChanged: (view, _, value) => ((SkUiLabel)view).SetTextColor((Color)value));
    /// <summary>Bindable font size in DIPs.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkUiLabel), 16d, propertyChanged: (view, _, value) => ((SkUiLabel)view).SetFontSize((double)value));
    /// <summary>Bindable system font family name.</summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(SkUiLabel), null, propertyChanged: (view, _, value) => ((SkUiLabel)view).SetFontFamily((string?)value));
    /// <summary>Bindable bold and italic attributes.</summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(SkUiLabel), FontAttributes.None, propertyChanged: (view, _, value) => ((SkUiLabel)view).SetFontAttributes((FontAttributes)value));
    /// <summary>Bindable wrapping or truncation mode.</summary>
    public static readonly BindableProperty LineBreakModeProperty = BindableProperty.Create(nameof(LineBreakMode), typeof(LineBreakMode), typeof(SkUiLabel), LineBreakMode.WordWrap, propertyChanged: (view, _, value) => ((SkUiLabel)view).SetLineBreakMode((LineBreakMode)value));
    /// <summary>Bindable horizontal text alignment.</summary>
    public static readonly BindableProperty HorizontalTextAlignmentProperty = BindableProperty.Create(nameof(HorizontalTextAlignment), typeof(TextAlignment), typeof(SkUiLabel), TextAlignment.Start, defaultValueCreator: view => ((SkUiLabel)view).DefaultTextAlignment, propertyChanged: (view, _, value) => ((SkUiLabel)view).SetHorizontalTextAlignment((TextAlignment)value));
    /// <summary>Bindable vertical text alignment.</summary>
    public static readonly BindableProperty VerticalTextAlignmentProperty = BindableProperty.Create(nameof(VerticalTextAlignment), typeof(TextAlignment), typeof(SkUiLabel), TextAlignment.Start, defaultValueCreator: view => ((SkUiLabel)view).DefaultTextAlignment, propertyChanged: (view, _, value) => ((SkUiLabel)view).SetVerticalTextAlignment((TextAlignment)value));
    /// <summary>Bindable text inset.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(SkUiLabel), default(Thickness), defaultValueCreator: view => ((SkUiLabel)view).DefaultPadding, propertyChanged: (view, _, value) => ((SkUiLabel)view).SetPadding((Thickness)value));

    /// <summary>Default foreground used by derived controls and bindable value clearing.</summary>
    protected virtual Color DefaultTextColor => Colors.Black;
    /// <summary>Default alignment used by derived controls and bindable value clearing.</summary>
    protected virtual TextAlignment DefaultTextAlignment => TextAlignment.Start;
    /// <summary>Default inset used by derived controls and bindable value clearing.</summary>
    protected virtual Thickness DefaultPadding => default;

    /// <summary>Text displayed by the control.</summary>
    public string Text { get => text; set => SetValue(TextProperty, value); }
    /// <summary>Foreground color.</summary>
    public Color TextColor { get => textColor; set => SetValue(TextColorProperty, value); }
    /// <summary>Font size in DIPs.</summary>
    public double FontSize { get => fontSize; set => SetValue(FontSizeProperty, value); }
    /// <summary>System font family, or a name registered via <see cref="SkUiFonts.Register"/> for app-embedded MAUI fonts. Complex-script shaping is not supported.</summary>
    public string? FontFamily { get => fontFamily; set => SetValue(FontFamilyProperty, value); }
    /// <summary>Bold and italic flags.</summary>
    public FontAttributes FontAttributes { get => fontAttributes; set => SetValue(FontAttributesProperty, value); }
    /// <summary>Wrapping/truncation behavior.</summary>
    public LineBreakMode LineBreakMode { get => lineBreakMode; set => SetValue(LineBreakModeProperty, value); }
    /// <summary>Horizontal text placement.</summary>
    public TextAlignment HorizontalTextAlignment { get => horizontalTextAlignment; set => SetValue(HorizontalTextAlignmentProperty, value); }
    /// <summary>Vertical text placement.</summary>
    public TextAlignment VerticalTextAlignment { get => verticalTextAlignment; set => SetValue(VerticalTextAlignmentProperty, value); }
    /// <summary>Text inset in DIPs.</summary>
    public Thickness Padding { get => padding; set => SetValue(PaddingProperty, value); }

    /// <summary>Sets text without bindable write-back.</summary>
    public SkUiLabel SetText(string? value) { value ??= string.Empty; if (text == value) return this; text = value; InvalidateText(); return this; }
    /// <summary>Sets text color without bindable write-back.</summary>
    public SkUiLabel SetTextColor(Color value) { ArgumentNullException.ThrowIfNull(value); if (textColor == value) return this; textColor = value; InvalidatePaint(); return this; }
    /// <summary>Sets font size without bindable write-back.</summary>
    public SkUiLabel SetFontSize(double value) { if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); if (fontSize == value) return this; fontSize = value; InvalidateText(); return this; }
    /// <summary>Sets font family without bindable write-back.</summary>
    public SkUiLabel SetFontFamily(string? value) { if (fontFamily == value) return this; fontFamily = value; InvalidateText(); return this; }
    /// <summary>Sets font attributes without bindable write-back.</summary>
    public SkUiLabel SetFontAttributes(FontAttributes value) { if (fontAttributes == value) return this; fontAttributes = value; InvalidateText(); return this; }
    /// <summary>Sets line mode without bindable write-back.</summary>
    public SkUiLabel SetLineBreakMode(LineBreakMode value) { lineBreakMode = value; InvalidateText(); return this; }
    /// <summary>Sets horizontal alignment without bindable write-back.</summary>
    public SkUiLabel SetHorizontalTextAlignment(TextAlignment value) { horizontalTextAlignment = value; InvalidatePaint(); return this; }
    /// <summary>Sets vertical alignment without bindable write-back.</summary>
    public SkUiLabel SetVerticalTextAlignment(TextAlignment value) { verticalTextAlignment = value; InvalidatePaint(); return this; }
    /// <summary>Sets padding without bindable write-back.</summary>
    public SkUiLabel SetPadding(Thickness value) { padding = value; InvalidateText(); return this; }

    private void InvalidateText() { lineWidth = double.NaN; InvalidateMeasureOverride(); }

    // Registry-resolved typefaces (e.g. app-embedded MAUI fonts via SkUiFonts.Register) are cached/owned by the
    // registry and must not be disposed here; only the ad-hoc system-lookup fallback is owned by the caller.
    private (SKTypeface Typeface, bool Owned) ResolveTypeface()
    {
        if (fontFamily is not null && SkUiFonts.TryResolve(fontFamily) is { } registered)
            return (registered, false);
        var typeface = SKTypeface.FromFamilyName(fontFamily,
            fontAttributes.HasFlag(FontAttributes.Bold) ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal, fontAttributes.HasFlag(FontAttributes.Italic) ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        return (typeface, true);
    }

    private void EnsureLines(double width, SKFont font)
    {
        width = Math.Max(0, width);
        if (width == lineWidth) return;
        lineWidth = width;
        if (text.Length == 0) { lines = []; return; }
        var result = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (lineBreakMode is LineBreakMode.NoWrap or LineBreakMode.HeadTruncation or LineBreakMode.MiddleTruncation or LineBreakMode.TailTruncation)
            {
                result.Add(Truncate(paragraph, width, font));
                continue;
            }
            var remaining = paragraph;
            while (remaining.Length > 0 && font.MeasureText(remaining) > width)
            {
                var boundaries = StringInfo.ParseCombiningCharacters(remaining);
                var count = 1;
                while (count < boundaries.Length && font.MeasureText(remaining[..boundaries[count]]) <= width) count++;
                var end = count == 1 ? (boundaries.Length > 1 ? boundaries[1] : remaining.Length) : boundaries[count - 1];
                if (lineBreakMode == LineBreakMode.WordWrap)
                {
                    var space = remaining.LastIndexOf(' ', Math.Max(0, end - 1), end);
                    if (space > 0) end = space;
                }
                result.Add(remaining[..end]);
                remaining = remaining[end..].TrimStart(' ');
            }
            if (remaining.Length > 0 || paragraph.Length == 0) result.Add(remaining);
        }
        lines = result.ToArray();
    }

    private string Truncate(string value, double width, SKFont font)
    {
        if (lineBreakMode == LineBreakMode.NoWrap || font.MeasureText(value) <= width) return value;
        const string ellipsis = "...";
        if (font.MeasureText(ellipsis) > width) return string.Empty;
        var boundaries = StringInfo.ParseCombiningCharacters(value);
        for (var count = boundaries.Length - 1; count >= 0; count--)
        {
            var prefixCount = lineBreakMode == LineBreakMode.HeadTruncation ? 0 : lineBreakMode == LineBreakMode.MiddleTruncation ? (count + 1) / 2 : count;
            var suffixCount = count - prefixCount;
            var candidate = value[..(prefixCount == 0 ? 0 : boundaries[prefixCount])] + ellipsis
                + (suffixCount == 0 ? string.Empty : value[boundaries[boundaries.Length - suffixCount]..]);
            if (font.MeasureText(candidate) <= width) return candidate;
        }
        return ellipsis;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var (typeface, owned) = ResolveTypeface();
        try
        {
            using var font = new SKFont(typeface, (float)fontSize);
            EnsureLines(widthConstraint - padding.HorizontalThickness, font);
            var width = 0f;
            foreach (var line in lines) width = Math.Max(width, font.MeasureText(line));
            return new Size(width + padding.HorizontalThickness, lines.Length * font.Spacing + padding.VerticalThickness);
        }
        finally { if (owned) typeface.Dispose(); }
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var (typeface, owned) = ResolveTypeface();
        try
        {
            using var font = new SKFont(typeface, (float)fontSize);
            using var paint = new SKPaint { Color = ToSkColor(textColor), IsAntialias = true };
            var availableWidth = Math.Max(0, Width - padding.HorizontalThickness);
            EnsureLines(availableWidth, font);
            var height = lines.Length * font.Spacing;
            var offset = verticalTextAlignment == TextAlignment.Center ? (Height - padding.VerticalThickness - height) / 2
                : verticalTextAlignment == TextAlignment.End ? Height - padding.VerticalThickness - height : 0;
            var baseline = (float)(padding.Top + Math.Max(0, offset)) - font.Metrics.Ascent;
            foreach (var line in lines)
            {
                var lineSize = font.MeasureText(line);
                var left = padding.Left + (horizontalTextAlignment == TextAlignment.Center ? (availableWidth - lineSize) / 2
                    : horizontalTextAlignment == TextAlignment.End ? availableWidth - lineSize : 0);
                canvas.DrawText(line, (float)left, baseline, SKTextAlign.Left, font, paint);
                baseline += font.Spacing;
            }
        }
        finally { if (owned) typeface.Dispose(); }
    }
}