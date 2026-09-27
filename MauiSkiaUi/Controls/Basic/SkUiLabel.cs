using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>Drawn, left-to-right text with wrapping, alignment, and MAUI-style bindable properties.</summary>
public class SkUiLabel : SkUiView
{
    private string _text = string.Empty;
    private Color _textColor = SkUiColors.DefaultForeground;
    private double _fontSize = 16;
    private string? _fontFamily;
    private FontAttributes _fontAttributes;
    private LineBreakMode _lineBreakMode = LineBreakMode.WordWrap;
    private TextAlignment _horizontalTextAlignment;
    private TextAlignment _verticalTextAlignment;
    private Thickness _padding;
    private readonly SkUiTextLayout _layout = new();
    private SKPaint? _textPaint;

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
    protected virtual Color DefaultTextColor => SkUiColors.DefaultForeground;
    /// <summary>Default alignment used by derived controls and bindable value clearing.</summary>
    protected virtual TextAlignment DefaultTextAlignment => TextAlignment.Start;
    /// <summary>Default inset used by derived controls and bindable value clearing.</summary>
    protected virtual Thickness DefaultPadding => default;

    /// <summary>Text displayed by the control.</summary>
    public string Text { get => _text; set => SetValue(TextProperty, value); }
    /// <summary>Foreground color.</summary>
    public Color TextColor { get => _textColor; set => SetValue(TextColorProperty, value); }
    /// <summary>Font size in DIPs.</summary>
    public double FontSize { get => _fontSize; set => SetValue(FontSizeProperty, value); }
    /// <summary>System font family, or a name registered via <see cref="SkUiFonts.Register"/> for app-embedded MAUI fonts. Complex-script shaping is not supported.</summary>
    public string? FontFamily { get => _fontFamily; set => SetValue(FontFamilyProperty, value); }
    /// <summary>Bold and italic flags.</summary>
    public FontAttributes FontAttributes { get => _fontAttributes; set => SetValue(FontAttributesProperty, value); }
    /// <summary>Wrapping/truncation behavior.</summary>
    public LineBreakMode LineBreakMode { get => _lineBreakMode; set => SetValue(LineBreakModeProperty, value); }
    /// <summary>Horizontal text placement.</summary>
    public TextAlignment HorizontalTextAlignment { get => _horizontalTextAlignment; set => SetValue(HorizontalTextAlignmentProperty, value); }
    /// <summary>Vertical text placement.</summary>
    public TextAlignment VerticalTextAlignment { get => _verticalTextAlignment; set => SetValue(VerticalTextAlignmentProperty, value); }
    /// <summary>Text inset in DIPs.</summary>
    public Thickness Padding { get => _padding; set => SetValue(PaddingProperty, value); }

    /// <summary>Sets text without bindable write-back.</summary>
    public SkUiLabel SetText(string? value) { value ??= string.Empty; if (_text == value) return this; _text = value; InvalidateText(); return this; }
    /// <summary>Sets text color without bindable write-back.</summary>
    public SkUiLabel SetTextColor(Color value) { ArgumentNullException.ThrowIfNull(value); if (_textColor == value) return this; _textColor = value; InvalidatePaint(); return this; }
    /// <summary>Sets font size without bindable write-back.</summary>
    public SkUiLabel SetFontSize(double value) { if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); if (_fontSize == value) return this; _fontSize = value; InvalidateText(); return this; }
    /// <summary>Sets font family without bindable write-back.</summary>
    public SkUiLabel SetFontFamily(string? value) { if (_fontFamily == value) return this; _fontFamily = value; InvalidateText(); return this; }
    /// <summary>Sets font attributes without bindable write-back.</summary>
    public SkUiLabel SetFontAttributes(FontAttributes value) { if (_fontAttributes == value) return this; _fontAttributes = value; InvalidateText(); return this; }
    /// <summary>Sets line mode without bindable write-back.</summary>
    public SkUiLabel SetLineBreakMode(LineBreakMode value) { _lineBreakMode = value; InvalidateText(); return this; }
    /// <summary>Sets horizontal alignment without bindable write-back.</summary>
    public SkUiLabel SetHorizontalTextAlignment(TextAlignment value) { _horizontalTextAlignment = value; InvalidatePaint(); return this; }
    /// <summary>Sets vertical alignment without bindable write-back.</summary>
    public SkUiLabel SetVerticalTextAlignment(TextAlignment value) { _verticalTextAlignment = value; InvalidatePaint(); return this; }
    /// <summary>Sets padding without bindable write-back.</summary>
    public SkUiLabel SetPadding(Thickness value) { _padding = value; InvalidateText(); return this; }

    private void InvalidateText() { _layout.Invalidate(); InvalidateMeasureOverride(); }

    /// <summary>
    /// Paragraph direction from MAUI <see cref="VisualElement.FlowDirection"/>: an explicit or inherited right-to-left
    /// flow gives RTL paragraphs, an explicit left-to-right flow gives LTR, and the default (<c>MatchParent</c> under a
    /// left-to-right parent) lets the first strong character decide, like Android's <c>firstStrong</c> text direction.
    /// </summary>
    private SkUiTextDirection TextDirection =>
        FlowDirection == FlowDirection.LeftToRight ? SkUiTextDirection.LeftToRight
        : ((IView)this).FlowDirection == FlowDirection.RightToLeft ? SkUiTextDirection.RightToLeft
        : SkUiTextDirection.Auto;

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(FlowDirection))
            InvalidateText();
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        _layout.Measure(_text, SkUiTypefaces.Resolve(_fontFamily, _fontAttributes), _fontSize, _padding, widthConstraint,
            MauiSkiaUi.Core.SkUiCoreTextLineBreakers.For(_lineBreakMode), TextDirection);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (_text.Length == 0) return;
        var paint = _textPaint ??= new SKPaint { IsAntialias = true };
        paint.Color = ToSkColor(_textColor);
        _layout.Draw(canvas, _text, SkUiTypefaces.Resolve(_fontFamily, _fontAttributes), _fontSize, _padding, Width, Height,
            _horizontalTextAlignment, _verticalTextAlignment, paint, MauiSkiaUi.Core.SkUiCoreTextLineBreakers.For(_lineBreakMode), TextDirection);
    }
}