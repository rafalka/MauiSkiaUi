using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core text node with fluent <c>Set*</c> apply path and <see cref="System.ComponentModel.INotifyPropertyChanged"/>.
/// No MAUI bindable properties or styling — suitable inside complex controls hosted by <see cref="SkUiCoreHost"/>.
/// Defaults come from <see cref="SkUiColorScheme"/> / <see cref="SkUiLook"/>.
/// Line wrapping/truncation is driven by <see cref="LineBreaker"/>; <see cref="SetLineBreakMode"/> installs a stock breaker.
/// Optional rounded chrome (a badge, a chip): <see cref="FillColor"/>, <see cref="CornerRadii"/> (or the uniform
/// <see cref="CornerRadius"/>),
/// <see cref="BorderColor"/> and <see cref="BorderWidth"/>, drawn unless a <see cref="SkUiCoreNode.PaintBackground"/>
/// painter replaces it.
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
    private Microsoft.Maui.LineBreakMode? _lineBreakMode = Microsoft.Maui.LineBreakMode.WordWrap;
    private SkUiCoreTextLineBreaker _lineBreaker = SkUiCoreTextLineBreakers.WordWrap;
    private readonly SkUiTextLayout _layout = new();
    private SkUiTextDirection _textDirection;
    private SkUiTextRendering _textRendering;
    private SKPaint? _textPaint;
    private Color _fillColor = Colors.Transparent;
    private Color _borderColor = Colors.Transparent;
    private double _borderWidth;
    private Microsoft.Maui.CornerRadius _cornerRadii;
    private bool _cornerRadiusExplicit;
    private SkUiRoundedClip _textClip;

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

    /// <summary>Background fill of the rounded chrome (transparent by default).</summary>
    public Color FillColor
    {
        get => _fillColor;
        set => SetFillColor(value);
    }

    /// <summary>Border color (drawn inside the bounds; <see cref="Padding"/> is not adjusted).</summary>
    public Color BorderColor
    {
        get => _borderColor;
        set => SetBorderColor(value);
    }

    /// <summary>Border width in DIPs.</summary>
    public double BorderWidth
    {
        get => _borderWidth;
        set => SetBorderWidth(value);
    }

    /// <summary>
    /// Per-corner radii in DIPs of the fill and border; text is clipped to them. Radii larger than the label allows are
    /// scaled down, so a large uniform radius gives a pill. When unset, <see cref="DefaultCornerRadius"/> on every
    /// corner (resolved at paint time, so buttons follow look swaps).
    /// </summary>
    public Microsoft.Maui.CornerRadius CornerRadii
    {
        get => EffectiveCornerRadii;
        set => SetCornerRadii(value);
    }

    /// <summary>
    /// Uniform view of <see cref="CornerRadii"/>, an <c>int</c> as MAUI Button's <c>CornerRadius</c> (and
    /// <see cref="SkUiLabel.CornerRadius"/>): setting it sets all four corners; it reads the top-left radius, rounded.
    /// Use <see cref="CornerRadii"/> for fractional radii.
    /// </summary>
    public int CornerRadius
    {
        get => (int)Math.Round(EffectiveCornerRadii.TopLeft);
        set => SetCornerRadius(value);
    }

    /// <summary>Uniform corner radius used until the radii are set (0; buttons use the look's).</summary>
    protected virtual double DefaultCornerRadius => 0;

    private Microsoft.Maui.CornerRadius EffectiveCornerRadii =>
        _cornerRadiusExplicit ? _cornerRadii : new Microsoft.Maui.CornerRadius(DefaultCornerRadius);

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

    /// <summary>
    /// Stock <see cref="Microsoft.Maui.LineBreakMode"/> last applied via <see cref="SetLineBreakMode"/>,
    /// or <c>null</c> when a custom <see cref="LineBreaker"/> is installed.
    /// </summary>
    public Microsoft.Maui.LineBreakMode? LineBreakMode => _lineBreakMode;

    /// <summary>
    /// Active line-breaking policy. Defaults to <see cref="SkUiCoreTextLineBreakers.WordWrap"/>.
    /// Assigning a custom breaker clears <see cref="LineBreakMode"/>.
    /// </summary>
    public SkUiCoreTextLineBreaker LineBreaker
    {
        get => _lineBreaker;
        set => SetLineBreaker(value);
    }

    /// <summary>
    /// Paragraph direction. <see cref="SkUiTextDirection.Auto"/> (default) uses the first strong character;
    /// Start / End alignment follows the resolved direction.
    /// </summary>
    public SkUiTextDirection TextDirection
    {
        get => _textDirection;
        set => SetTextDirection(value);
    }

    /// <summary>Explicit <see cref="TextDirection"/>, else the inherited layout direction (<see cref="SkUiCoreNode.FlowDirection"/>).</summary>
    private SkUiTextDirection EffectiveTextDirection =>
        _textDirection != SkUiTextDirection.Auto ? _textDirection : InheritedDirection();

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged() => InvalidateText();

    /// <summary>
    /// Text rendering mode (<see cref="SkUiTextRendering.Default"/> → <see cref="SkUiTextOptions.DefaultRendering"/>).
    /// Use <see cref="SkUiTextRendering.Simple"/> for dense plain text / numbers where shaping is never needed.
    /// </summary>
    public SkUiTextRendering TextRendering
    {
        get => _textRendering;
        set => SetTextRendering(value);
    }

    /// <summary>Sets <see cref="TextRendering"/>.</summary>
    public SkUiCoreLabel SetTextRendering(SkUiTextRendering value)
    {
        if (!SetProperty(ref _textRendering, value, nameof(TextRendering))) return this;
        InvalidateText();
        return this;
    }

    /// <summary>Sets <see cref="TextDirection"/>.</summary>
    public SkUiCoreLabel SetTextDirection(SkUiTextDirection value)
    {
        if (!SetProperty(ref _textDirection, value, nameof(TextDirection))) return this;
        InvalidateText();
        return this;
    }

    /// <summary>Sets text and invalidates measure.</summary>
    public SkUiCoreLabel SetText(string? value)
    {
        value ??= string.Empty;
        if (!SetProperty(ref _text, value, nameof(Text))) return this;
        InvalidateText();
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
        InvalidateText();
        return this;
    }

    /// <summary>Sets font family (registry or system name).</summary>
    public SkUiCoreLabel SetFontFamily(string? value)
    {
        if (!SetProperty(ref _fontFamily, value, nameof(FontFamily))) return this;
        InvalidateText();
        return this;
    }

    /// <summary>Sets the chrome fill color.</summary>
    public SkUiCoreLabel SetFillColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _fillColor, value, nameof(FillColor))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets border color.</summary>
    public SkUiCoreLabel SetBorderColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _borderColor, value, nameof(BorderColor))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets border width in DIPs.</summary>
    public SkUiCoreLabel SetBorderWidth(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _borderWidth, value, nameof(BorderWidth))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the per-corner radii in DIPs (marks them as app-explicit so look swaps do not replace them).</summary>
    public SkUiCoreLabel SetCornerRadii(Microsoft.Maui.CornerRadius value)
    {
        SkUiCornerRadii.Validate(value, nameof(value));
        var wasExplicit = _cornerRadiusExplicit;
        _cornerRadiusExplicit = true;
        if (!SetProperty(ref _cornerRadii, value, nameof(CornerRadii)) && wasExplicit) return this;
        OnPropertyChanged(nameof(CornerRadius));
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets all four corner radii to <paramref name="value"/> DIPs (app-explicit, like <see cref="SetCornerRadii"/>).</summary>
    public SkUiCoreLabel SetCornerRadius(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        return SetCornerRadii(new Microsoft.Maui.CornerRadius(value));
    }

    /// <summary>Draws the rounded fill (<paramref name="fill"/>) and border through <see cref="SkUiLook.DrawRoundedBox(SKCanvas, SKRect, Microsoft.Maui.CornerRadius, SKColor, SKColor, float)"/>.</summary>
    protected void PaintChrome(SKCanvas canvas, Color fill) =>
        SkUiLook.Current.DrawRoundedBox(canvas, new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height), EffectiveCornerRadii,
            ToSkColor(fill), ToSkColor(_borderColor), (float)_borderWidth);

    /// <summary>Sets text inset in DIPs.</summary>
    public SkUiCoreLabel SetPadding(Thickness value)
    {
        if (!SetProperty(ref _padding, value, nameof(Padding))) return this;
        InvalidateText();
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

    /// <summary>
    /// Installs the stock breaker for <paramref name="mode"/> (same semantics as <see cref="SkUiLabel"/>).
    /// </summary>
    public SkUiCoreLabel SetLineBreakMode(Microsoft.Maui.LineBreakMode mode)
    {
        if (_lineBreakMode == mode)
            return this;
        _lineBreakMode = mode;
        _lineBreaker = SkUiCoreTextLineBreakers.For(mode);
        OnPropertyChanged(nameof(LineBreakMode));
        OnPropertyChanged(nameof(LineBreaker));
        InvalidateText();
        return this;
    }

    /// <summary>
    /// Installs a custom line breaker. Sets <see cref="LineBreakMode"/> to <c>null</c>.
    /// </summary>
    public SkUiCoreLabel SetLineBreaker(SkUiCoreTextLineBreaker value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (ReferenceEquals(_lineBreaker, value) && _lineBreakMode is null)
            return this;
        _lineBreaker = value;
        _lineBreakMode = null;
        OnPropertyChanged(nameof(LineBreaker));
        OnPropertyChanged(nameof(LineBreakMode));
        InvalidateText();
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        _layout.Measure(_text, SkUiTypefaces.Resolve(_fontFamily), _fontSize, _padding, widthConstraint, _lineBreaker, EffectiveTextDirection, _textRendering);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var radii = EffectiveCornerRadii;
        if (PaintBackground is null && (_fillColor.Alpha > 0 || (_borderWidth > 0 && _borderColor.Alpha > 0)))
            PaintChrome(canvas, _fillColor);
        if (_text.Length == 0) return;
        if (!SkUiCornerRadii.HasAny(radii))
        {
            PaintText(canvas);
            return;
        }
        // Glyphs never bleed past the rounded corners.
        var saveCount = canvas.Save();
        try
        {
            canvas.ClipPath(_textClip.Get((float)Frame.Width, (float)Frame.Height, radii), antialias: true);
            PaintText(canvas);
        }
        finally
        {
            canvas.RestoreToCount(saveCount);
        }
    }

    private void PaintText(SKCanvas canvas)
    {
        var paint = _textPaint ??= new SKPaint { IsAntialias = true };
        paint.Color = ToSkColor(_textColor);
        _layout.Draw(canvas, _text, SkUiTypefaces.Resolve(_fontFamily), _fontSize, _padding, Frame.Width, Frame.Height,
            _horizontal, _vertical, paint, _lineBreaker, EffectiveTextDirection, _textRendering);
    }

    private void InvalidateText()
    {
        _layout.Invalidate();
        InvalidateMeasure();
    }
}
