using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Drawn text with wrapping, alignment, and MAUI-style bindable properties. Optional rounded chrome (a badge, a chip,
/// a tag): <see cref="CornerRadii"/> (or the uniform <see cref="CornerRadius"/>), <see cref="BorderColor"/> and
/// <see cref="BorderWidth"/> shape the <see cref="VisualElement.Background"/> fill without wrapping the label in a border.
/// </summary>
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
    private SkUiTextRendering _textRendering;
    private SKPaint? _textPaint;
    private Microsoft.Maui.CornerRadius _cornerRadii;
    private Color _borderColor = Colors.Transparent;
    private double _borderWidth;
    private SkUiRoundedClip _textClip;

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
    /// <summary>
    /// Bindable text rendering mode: <see cref="SkUiTextRendering.Auto"/> (via <see cref="SkUiTextRendering.Default"/>) takes a
    /// fast path for plain Latin text and shapes everything else with HarfBuzz; <see cref="SkUiTextRendering.Simple"/> never
    /// shapes (fastest, for dense plain text / numbers); <see cref="SkUiTextRendering.Shaped"/> always shapes (kerning, ligatures).
    /// </summary>
    public static readonly BindableProperty TextRenderingProperty = BindableProperty.Create(nameof(TextRendering), typeof(SkUiTextRendering), typeof(SkUiLabel), SkUiTextRendering.Default,
        propertyChanged: (view, _, value) => ((SkUiLabel)view).SetTextRendering((SkUiTextRendering)value));
    /// <summary>Bindable text inset.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(SkUiLabel), default(Thickness), defaultValueCreator: view => ((SkUiLabel)view).DefaultPadding, propertyChanged: (view, _, value) => ((SkUiLabel)view).SetPadding((Thickness)value));

    /// <summary>Bindable per-corner radii of the background and border (0: square).</summary>
    public static readonly BindableProperty CornerRadiiProperty = BindableProperty.Create(nameof(CornerRadii), typeof(Microsoft.Maui.CornerRadius), typeof(SkUiLabel), default(Microsoft.Maui.CornerRadius),
        defaultValueCreator: view => new Microsoft.Maui.CornerRadius(((SkUiLabel)view).DefaultCornerRadius),
        propertyChanged: (view, _, value) => ((SkUiLabel)view).SetCornerRadii((Microsoft.Maui.CornerRadius)value));
    /// <summary>Bindable uniform corner radius (MAUI Button's <c>int</c> <c>CornerRadius</c>): sets all four <see cref="CornerRadii"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(int), typeof(SkUiLabel), 0,
        defaultValueCreator: view => (int)Math.Round(((SkUiLabel)view).DefaultCornerRadius),
        propertyChanged: (view, _, value) => ((SkUiLabel)view).SetCornerRadius((int)value));
    /// <summary>Bindable border color.</summary>
    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(SkUiLabel), Colors.Transparent, propertyChanged: (view, _, value) => ((SkUiLabel)view).SetBorderColor((Color)value));
    /// <summary>Bindable border width.</summary>
    public static readonly BindableProperty BorderWidthProperty = BindableProperty.Create(nameof(BorderWidth), typeof(double), typeof(SkUiLabel), 0d, propertyChanged: (view, _, value) => ((SkUiLabel)view).SetBorderWidth((double)value));

    /// <summary>
    /// Starts the applied state from the same defaults the bindable store reports (the <c>Default*</c> members, which
    /// derived controls such as buttons override), without touching the store.
    /// </summary>
    public SkUiLabel()
    {
        _textColor = DefaultTextColor;
        _horizontalTextAlignment = _verticalTextAlignment = DefaultTextAlignment;
        _padding = DefaultPadding;
        _cornerRadii = new Microsoft.Maui.CornerRadius(DefaultCornerRadius);
    }

    /// <summary>Default (uniform) corner radius used by derived controls and bindable value clearing.</summary>
    protected virtual double DefaultCornerRadius => 0;
    /// <summary>Default foreground used by derived controls and bindable value clearing.</summary>
    protected virtual Color DefaultTextColor => SkUiColors.DefaultForeground;
    /// <summary>Default alignment used by derived controls and bindable value clearing.</summary>
    protected virtual TextAlignment DefaultTextAlignment => TextAlignment.Start;
    /// <summary>Default inset used by derived controls and bindable value clearing.</summary>
    protected virtual Thickness DefaultPadding => default;

    /// <summary>Text displayed by the control.</summary>
    public string Text { get => (string?)GetValue(TextProperty) ?? string.Empty; set => SetValue(TextProperty, value); }
    /// <summary>Foreground color.</summary>
    public Color TextColor { get => (Color)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
    /// <summary>Font size in DIPs.</summary>
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    /// <summary>System font family, or a name registered via <see cref="SkUiFonts.Register"/> for app-embedded MAUI fonts. Complex-script shaping is not supported.</summary>
    public string? FontFamily { get => (string?)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    /// <summary>Bold and italic flags.</summary>
    public FontAttributes FontAttributes { get => (FontAttributes)GetValue(FontAttributesProperty); set => SetValue(FontAttributesProperty, value); }
    /// <summary>Wrapping/truncation behavior.</summary>
    public LineBreakMode LineBreakMode { get => (LineBreakMode)GetValue(LineBreakModeProperty); set => SetValue(LineBreakModeProperty, value); }
    /// <summary>Horizontal text placement.</summary>
    public TextAlignment HorizontalTextAlignment { get => (TextAlignment)GetValue(HorizontalTextAlignmentProperty); set => SetValue(HorizontalTextAlignmentProperty, value); }
    /// <summary>Vertical text placement.</summary>
    public TextAlignment VerticalTextAlignment { get => (TextAlignment)GetValue(VerticalTextAlignmentProperty); set => SetValue(VerticalTextAlignmentProperty, value); }
    /// <summary>Text inset in DIPs.</summary>
    public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    /// <summary>
    /// Per-corner radii in DIPs of the background and border (like <see cref="SkUiBorder.CornerRadius"/>); text is
    /// clipped to them. Radii larger than the label allows are scaled down, so a large uniform radius gives a pill.
    /// Hit bounds remain rectangular.
    /// </summary>
    public Microsoft.Maui.CornerRadius CornerRadii { get => (Microsoft.Maui.CornerRadius)GetValue(CornerRadiiProperty); set => SetValue(CornerRadiiProperty, value); }
    /// <summary>
    /// Uniform view of <see cref="CornerRadii"/>, an <c>int</c> as MAUI Button's <c>CornerRadius</c>: setting it sets all
    /// four corners; it reads the top-left radius, rounded. Setting either property replaces the corners (the last one
    /// set wins); use <see cref="CornerRadii"/> for fractional radii.
    /// </summary>
    public int CornerRadius { get => (int)Math.Round(_cornerRadii.TopLeft); set => SetValue(CornerRadiusProperty, value); }
    /// <summary>Border color (drawn inside the bounds; <see cref="Padding"/> is not adjusted).</summary>
    public Color BorderColor { get => (Color)GetValue(BorderColorProperty); set => SetValue(BorderColorProperty, value); }
    /// <summary>Border width in DIPs.</summary>
    public double BorderWidth { get => (double)GetValue(BorderWidthProperty); set => SetValue(BorderWidthProperty, value); }
    /// <inheritdoc cref="TextRenderingProperty" />
    public SkUiTextRendering TextRendering { get => (SkUiTextRendering)GetValue(TextRenderingProperty); set => SetValue(TextRenderingProperty, value); }
    /// <summary>Sets <see cref="TextRendering"/> (same as the property setter).</summary>
    public SkUiLabel SetTextRendering(SkUiTextRendering value) { if (WriteBindable(TextRenderingProperty, value)) return this; if (_textRendering == value) return this; _textRendering = value; InvalidateText(); return this; }

    /// <summary>Sets text (same as the property setter).</summary>
    public SkUiLabel SetText(string? value) { if (WriteBindable(TextProperty, value)) return this; value ??= string.Empty; if (_text == value) return this; _text = value; InvalidateText(); return this; }
    /// <summary>Sets text color (same as the property setter).</summary>
    public SkUiLabel SetTextColor(Color value) { ArgumentNullException.ThrowIfNull(value); if (WriteBindable(TextColorProperty, value)) return this; if (_textColor == value) return this; _textColor = value; InvalidatePaint(); return this; }
    /// <summary>Sets font size (same as the property setter).</summary>
    public SkUiLabel SetFontSize(double value) { if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); if (WriteBindable(FontSizeProperty, value)) return this; if (_fontSize == value) return this; _fontSize = value; InvalidateText(); return this; }
    /// <summary>Sets font family (same as the property setter).</summary>
    public SkUiLabel SetFontFamily(string? value) { if (WriteBindable(FontFamilyProperty, value)) return this; if (_fontFamily == value) return this; _fontFamily = value; InvalidateText(); return this; }
    /// <summary>Sets font attributes (same as the property setter).</summary>
    public SkUiLabel SetFontAttributes(FontAttributes value) { if (WriteBindable(FontAttributesProperty, value)) return this; if (_fontAttributes == value) return this; _fontAttributes = value; InvalidateText(); return this; }
    /// <summary>Sets line mode (same as the property setter).</summary>
    public SkUiLabel SetLineBreakMode(LineBreakMode value) { if (WriteBindable(LineBreakModeProperty, value)) return this; _lineBreakMode = value; InvalidateText(); return this; }
    /// <summary>Sets horizontal alignment (same as the property setter).</summary>
    public SkUiLabel SetHorizontalTextAlignment(TextAlignment value) { if (WriteBindable(HorizontalTextAlignmentProperty, value)) return this; _horizontalTextAlignment = value; InvalidatePaint(); return this; }
    /// <summary>Sets vertical alignment (same as the property setter).</summary>
    public SkUiLabel SetVerticalTextAlignment(TextAlignment value) { if (WriteBindable(VerticalTextAlignmentProperty, value)) return this; _verticalTextAlignment = value; InvalidatePaint(); return this; }
    /// <summary>Sets padding (same as the property setter).</summary>
    public SkUiLabel SetPadding(Thickness value) { if (WriteBindable(PaddingProperty, value)) return this; _padding = value; InvalidateText(); return this; }

    /// <summary>Sets the per-corner radii (same as the property setter).</summary>
    public SkUiLabel SetCornerRadii(Microsoft.Maui.CornerRadius value) { SkUiCornerRadii.Validate(value, nameof(value)); if (WriteBindable(CornerRadiiProperty, value)) return this; if (_cornerRadii == value) return this; _cornerRadii = value; InvalidatePaint(); return this; }
    /// <summary>Sets all four corner radii to <paramref name="value"/> (same as the property setter).</summary>
    public SkUiLabel SetCornerRadius(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); if (WriteBindable(CornerRadiusProperty, value)) return this; return SetCornerRadii(new Microsoft.Maui.CornerRadius(value)); }
    /// <summary>Sets border color (same as the property setter).</summary>
    public SkUiLabel SetBorderColor(Color value) { ArgumentNullException.ThrowIfNull(value); if (WriteBindable(BorderColorProperty, value)) return this; if (_borderColor == value) return this; _borderColor = value; InvalidatePaint(); return this; }
    /// <summary>Sets border width (same as the property setter).</summary>
    public SkUiLabel SetBorderWidth(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); if (WriteBindable(BorderWidthProperty, value)) return this; if (_borderWidth == value) return this; _borderWidth = value; InvalidatePaint(); return this; }

    /// <inheritdoc />
    internal override Microsoft.Maui.CornerRadius PressEffectCornerRadii => _cornerRadii;

    /// <summary>Whether the label draws rounded chrome instead of the plain rectangular background.</summary>
    private bool HasChrome => SkUiCornerRadii.HasAny(_cornerRadii) || (_borderWidth > 0 && _borderColor.Alpha > 0);

    /// <summary>Draws the rounded fill (<paramref name="fill"/>) and border through <see cref="SkUiLook.DrawRoundedBox(SKCanvas, SKRect, Microsoft.Maui.CornerRadius, SKColor, SKColor, float)"/>.</summary>
    protected void PaintChrome(SKCanvas canvas, Color fill) =>
        SkUiLook.Current.DrawRoundedBox(canvas, new SKRect(0, 0, (float)Width, (float)Height), _cornerRadii,
            ToSkColor(fill), ToSkColor(_borderColor), (float)_borderWidth);

    /// <inheritdoc />
    protected override void OnPaintBackground(SKCanvas canvas)
    {
        if (HasChrome)
            PaintChrome(canvas, ResolveSolidBackgroundColor() ?? Colors.Transparent);
        else
            base.OnPaintBackground(canvas);
    }

    private void InvalidateText() { _layout.Invalidate(); InvalidateMeasureOverride(); }

    /// <summary>
    /// Paragraph direction from MAUI <see cref="VisualElement.FlowDirection"/>: an explicit or inherited right-to-left
    /// flow gives RTL paragraphs, an explicit left-to-right flow gives LTR, and the default (<c>MatchParent</c> under a
    /// left-to-right parent) lets the first strong character decide, like Android's <c>firstStrong</c> text direction.
    /// </summary>
    private SkUiTextDirection TextDirection
    {
        get
        {
            var effective = ((IVisualElementController)this).EffectiveFlowDirection;
            if (effective.HasFlag(EffectiveFlowDirection.RightToLeft)) return SkUiTextDirection.RightToLeft;
            return effective.HasFlag(EffectiveFlowDirection.Explicit) ? SkUiTextDirection.LeftToRight : SkUiTextDirection.Auto;
        }
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged() => InvalidateText();

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        _layout.Measure(_text, SkUiTypefaces.Resolve(_fontFamily, _fontAttributes), _fontSize, _padding, widthConstraint,
            MauiSkiaUi.Core.SkUiCoreTextLineBreakers.For(_lineBreakMode), TextDirection, _textRendering);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (_text.Length == 0) return;
        if (!SkUiCornerRadii.HasAny(_cornerRadii))
        {
            PaintText(canvas);
            return;
        }
        // Glyphs never bleed past the rounded corners.
        var saveCount = canvas.Save();
        try
        {
            canvas.ClipPath(_textClip.Get((float)Width, (float)Height, _cornerRadii), antialias: true);
            PaintText(canvas);
        }
        finally { canvas.RestoreToCount(saveCount); }
    }

    private void PaintText(SKCanvas canvas)
    {
        var paint = _textPaint ??= new SKPaint { IsAntialias = true };
        paint.Color = ToSkColor(_textColor);
        _layout.Draw(canvas, _text, SkUiTypefaces.Resolve(_fontFamily, _fontAttributes), _fontSize, _padding, Width, Height,
            _horizontalTextAlignment, _verticalTextAlignment, paint, MauiSkiaUi.Core.SkUiCoreTextLineBreakers.For(_lineBreakMode), TextDirection, _textRendering);
    }
}