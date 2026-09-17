using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core text node with fluent <c>Set*</c> apply path and <see cref="System.ComponentModel.INotifyPropertyChanged"/>.
/// No MAUI bindable properties or styling — suitable inside complex controls hosted by <see cref="SkUiCoreHost"/>.
/// Defaults come from <see cref="SkUiColorScheme"/> / <see cref="SkUiLook"/>.
/// </summary>
public class SkUiCoreLabel : SkUiCoreNode
{
    private string _text = string.Empty;
    private Color _textColor = SkUiColors.DefaultForeground;
    private double _fontSize = 16;
    private string? _fontFamily;
    private Thickness _padding;
    private TextAlignment _horizontal = TextAlignment.Start;
    private TextAlignment _vertical = TextAlignment.Start;

    /// <summary>Displayed text.</summary>
    public string Text
    {
        get => _text;
        set => SetText(value);
    }

    /// <summary>Foreground color.</summary>
    public Color TextColor
    {
        get => _textColor;
        set => SetTextColor(value);
    }

    /// <summary>Font size in DIPs.</summary>
    public double FontSize
    {
        get => _fontSize;
        set => SetFontSize(value);
    }

    /// <summary>Font family (registry or system name).</summary>
    public string? FontFamily
    {
        get => _fontFamily;
        set => SetFontFamily(value);
    }

    /// <summary>Text inset in DIPs.</summary>
    public Thickness Padding
    {
        get => _padding;
        set => SetPadding(value);
    }

    /// <summary>Horizontal text alignment within the arranged slot.</summary>
    public TextAlignment HorizontalTextAlignment
    {
        get => _horizontal;
        set => SetHorizontalTextAlignment(value);
    }

    /// <summary>Vertical text alignment within the arranged slot.</summary>
    public TextAlignment VerticalTextAlignment
    {
        get => _vertical;
        set => SetVerticalTextAlignment(value);
    }

    /// <summary>Sets text and invalidates measure.</summary>
    public SkUiCoreLabel SetText(string? value)
    {
        value ??= string.Empty;
        if (!SetProperty(ref _text, value, nameof(Text))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets foreground color.</summary>
    public SkUiCoreLabel SetTextColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _textColor, value, nameof(TextColor))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets font size in DIPs.</summary>
    public SkUiCoreLabel SetFontSize(double value)
    {
        if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        if (!SetProperty(ref _fontSize, value, nameof(FontSize))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets font family (registry or system name).</summary>
    public SkUiCoreLabel SetFontFamily(string? value)
    {
        if (!SetProperty(ref _fontFamily, value, nameof(FontFamily))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets text inset in DIPs.</summary>
    public SkUiCoreLabel SetPadding(Thickness value)
    {
        if (!SetProperty(ref _padding, value, nameof(Padding))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets horizontal text alignment within the arranged slot.</summary>
    public SkUiCoreLabel SetHorizontalTextAlignment(TextAlignment value)
    {
        if (!SetProperty(ref _horizontal, value, nameof(HorizontalTextAlignment))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets vertical text alignment within the arranged slot.</summary>
    public SkUiCoreLabel SetVerticalTextAlignment(TextAlignment value)
    {
        if (!SetProperty(ref _vertical, value, nameof(VerticalTextAlignment))) return this;
        InvalidatePaint();
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var (typeface, owned) = ResolveTypeface();
        try
        {
            using var font = new SKFont(typeface, (float)_fontSize);
            var width = string.IsNullOrEmpty(_text) ? 0 : font.MeasureText(_text);
            return new Size(width + _padding.HorizontalThickness, font.Spacing + _padding.VerticalThickness);
        }
        finally
        {
            if (owned) typeface.Dispose();
        }
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (_text.Length == 0) return;
        var (typeface, owned) = ResolveTypeface();
        try
        {
            using var font = new SKFont(typeface, (float)_fontSize);
            using var paint = new SKPaint { Color = ToSkColor(_textColor), IsAntialias = true };
            var availableWidth = Math.Max(0, Frame.Width - _padding.HorizontalThickness);
            var lineWidth = font.MeasureText(_text);
            var left = _padding.Left + (_horizontal == TextAlignment.Center ? (availableWidth - lineWidth) / 2
                : _horizontal == TextAlignment.End ? availableWidth - lineWidth : 0);
            var height = font.Spacing;
            var offset = _vertical == TextAlignment.Center ? (Frame.Height - _padding.VerticalThickness - height) / 2
                : _vertical == TextAlignment.End ? Frame.Height - _padding.VerticalThickness - height : 0;
            var baseline = (float)(_padding.Top + Math.Max(0, offset)) - font.Metrics.Ascent;
            canvas.DrawText(_text, (float)left, baseline, SKTextAlign.Left, font, paint);
        }
        finally
        {
            if (owned) typeface.Dispose();
        }
    }

    private (SKTypeface Typeface, bool Owned) ResolveTypeface()
    {
        if (_fontFamily is not null && SkUiFonts.TryResolve(_fontFamily) is { } registered)
            return (registered, false);
        var typeface = SKTypeface.FromFamilyName(_fontFamily ?? string.Empty);
        return (typeface, true);
    }
}
