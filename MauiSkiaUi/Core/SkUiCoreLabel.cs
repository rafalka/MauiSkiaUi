using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core text node with fluent <c>Set*</c> apply path and <see cref="System.ComponentModel.INotifyPropertyChanged"/>.
/// No MAUI bindable properties or styling — suitable inside complex controls hosted by <see cref="SkUiCoreHost"/>.
/// Defaults come from <see cref="SkUiColorScheme"/> / <see cref="SkUiLook"/>.
/// The text properties of <see cref="SkUiLabel"/> (one shared engine): <see cref="LineBreakMode"/>, a custom
/// <see cref="LineBreaker"/>, <see cref="MaxLines"/>, <see cref="LineHeight"/>, <see cref="CharacterSpacing"/>,
/// <see cref="TextDecorations"/>, <see cref="TextTransform"/> and <see cref="FontAttributes"/>; and spans
/// (<see cref="SetSpans(IEnumerable{SkUiCoreSpan}?)"/>, MAUI's <c>FormattedText</c>) with their own styles and taps.
/// Optional rounded chrome (a badge, a chip): <see cref="FillColor"/> (or a <see cref="SkUiCoreNode.Background"/>, solid or
/// gradient, which replaces it), <see cref="CornerRadii"/> (<see cref="SetCornerRadius"/>
/// sets all four), <see cref="BorderColor"/> and <see cref="BorderWidth"/>, drawn unless a <see cref="SkUiCoreNode.PaintBackground"/>
/// painter replaces it.
/// </summary>
public class SkUiCoreLabel : SkUiCoreNode
{
    private string _text = string.Empty;
    private string _displayText = string.Empty; // _text after _textTransform
    private Color _textColor = SkUiColors.DefaultForeground;
    private double _fontSize = 16;
    private bool _fontAutoScalingEnabled = true;
    private int _fontScaleVersion; // SkUiFontScaling.Version the rich text was built at
    private string? _fontFamily;
    private FontAttributes _fontAttributes;
    private Thickness _padding;
    private TextAlignment _horizontal = TextAlignment.Start;
    private TextAlignment _vertical = TextAlignment.Start;
    private Microsoft.Maui.LineBreakMode _lineBreakMode = Microsoft.Maui.LineBreakMode.WordWrap;
    private SkUiTextLineBreaker? _lineBreaker;
    private int _maxLines = -1;
    private double _lineHeight = -1;
    private double _characterSpacing;
    private TextDecorations _textDecorations;
    private TextTransform _textTransform = TextTransform.Default;
    private readonly SkUiTextLayout _layout;
    private SkUiTextDirection _textDirection;
    private SkUiTextRendering _textRendering;
    private SKPaint? _textPaint;
    private Color _fillColor = Colors.Transparent;
    private SkUiChromeState _chrome;
    private SkUiCoreSpan[] _spans = [];
    private SkUiRichTextLayout? _richLayout;
    private SkUiRichText? _richText; // _spans resolved against the label's defaults; null: rebuild
    private SkUiSpanTapGestureRecognizer? _spanTap;
    private TextType _textType;
    private IReadOnlyList<SkUiHtmlRun>? _htmlRuns; // _text parsed as HTML; null: parse again
    private EventHandler<SkUiLinkTappedEventArgs>? _linkTapped;

    /// <summary>Creates an empty word-wrapping label.</summary>
    public SkUiCoreLabel() => _layout = new SkUiTextLayout(this);

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

    /// <summary>
    /// Whether the text follows the system text size (<see cref="SkUiFontScaling"/>), as MAUI's <c>FontAutoScalingEnabled</c>:
    /// <see cref="FontSize"/> and span sizes are scaled when drawn. Default <c>true</c>.
    /// </summary>
    public bool FontAutoScalingEnabled
    {
        get => _fontAutoScalingEnabled;
        set => SetFontAutoScalingEnabled(value);
    }

    /// <summary>Font family (registry or system name).</summary>
    public string? FontFamily
    {
        get => _fontFamily;
        set => SetFontFamily(value);
    }

    /// <summary>Bold and italic flags.</summary>
    public FontAttributes FontAttributes
    {
        get => _fontAttributes;
        set => SetFontAttributes(value);
    }

    /// <summary>The most lines drawn; -1 (default), 0 or less: no limit (see <see cref="SkUiLabel.MaxLines"/>).</summary>
    public int MaxLines
    {
        get => _maxLines;
        set => SetMaxLines(value);
    }

    /// <summary>Multiplier of the font's line spacing; -1 (default), 0 or less: the font's (see <see cref="SkUiLabel.LineHeight"/>).</summary>
    public double LineHeight
    {
        get => _lineHeight;
        set => SetLineHeight(value);
    }

    /// <summary>DIPs added after each character (negative: tighter).</summary>
    public double CharacterSpacing
    {
        get => _characterSpacing;
        set => SetCharacterSpacing(value);
    }

    /// <summary>Underline and / or strikethrough, in the text color.</summary>
    public TextDecorations TextDecorations
    {
        get => _textDecorations;
        set => SetTextDecorations(value);
    }

    /// <summary>Displays <see cref="Text"/> in upper or lower case (invariant culture); <see cref="Text"/> keeps its value.</summary>
    public TextTransform TextTransform
    {
        get => _textTransform;
        set => SetTextTransform(value);
    }

    /// <summary>Background fill of the rounded chrome (transparent by default); a set <see cref="SkUiCoreNode.Background"/> replaces it.</summary>
    public Color FillColor
    {
        get => _fillColor;
        set => SetFillColor(value);
    }

    /// <summary>Border color (drawn inside the bounds; <see cref="Padding"/> is not adjusted).</summary>
    public Color BorderColor
    {
        get => _chrome.BorderColor;
        set => SetBorderColor(value);
    }

    /// <summary>Border width in DIPs.</summary>
    public double BorderWidth
    {
        get => _chrome.BorderWidth;
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

    /// <summary>Uniform corner radius used until the radii are set (0; buttons use the look's).</summary>
    protected virtual double DefaultCornerRadius => 0;

    /// <inheritdoc />
    internal override Microsoft.Maui.CornerRadius PressEffectCornerRadii => EffectiveCornerRadii;

    private Microsoft.Maui.CornerRadius EffectiveCornerRadii => _chrome.ResolveRadii(DefaultCornerRadius);

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

    /// <summary>Wrapping / truncation (default <see cref="Microsoft.Maui.LineBreakMode.WordWrap"/>); a custom <see cref="LineBreaker"/> replaces it and can still apply it.</summary>
    public Microsoft.Maui.LineBreakMode LineBreakMode
    {
        get => _lineBreakMode;
        set => SetLineBreakMode(value);
    }

    /// <summary>
    /// Custom line breaking that replaces <see cref="LineBreakMode"/> (see <see cref="SkUiLabel.LineBreaker"/>);
    /// <c>null</c> (default): <see cref="LineBreakMode"/>. Call <see cref="InvalidateTextLayout"/> when its own inputs change.
    /// </summary>
    public SkUiTextLineBreaker? LineBreaker
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

    /// <summary>Sets text and invalidates measure. Clears <see cref="Spans"/> (text replaces formatted text, as on MAUI's Label).</summary>
    public SkUiCoreLabel SetText(string? value)
    {
        value ??= string.Empty;
        ClearSpans();
        if (!SetProperty(ref _text, value, nameof(Text))) return this;
        _htmlRuns = null;
        if (IsHtml) InvalidateText();
        UpdateDisplayText();
        return this;
    }

    /// <summary>
    /// How <see cref="Text"/> is read: <see cref="Microsoft.Maui.TextType.Html"/> draws it as HTML (see
    /// <see cref="SkUiHtml"/> and <see cref="SkUiLabel.TextType"/>); links raise <see cref="LinkTapped"/>. Spans still win
    /// when set.
    /// </summary>
    public TextType TextType
    {
        get => _textType;
        set => SetTextType(value);
    }

    /// <summary>Sets <see cref="TextType"/>.</summary>
    public SkUiCoreLabel SetTextType(TextType value)
    {
        if (!SetProperty(ref _textType, value, nameof(TextType))) return this;
        _htmlRuns = null;
        _spanTap?.Cancel();
        InvalidateText();
        return this;
    }

    /// <summary>A link (<c>&lt;a href&gt;</c>) of HTML text was tapped; nothing opens by itself.</summary>
    public event EventHandler<SkUiLinkTappedEventArgs>? LinkTapped { add => _linkTapped += value; remove => _linkTapped -= value; }

    /// <summary>The <c>href</c> of the HTML link drawn at <paramref name="point"/> (label coordinates), or <c>null</c>.</summary>
    public string? LinkAt(Point point) =>
        IsHtml && SpanIndexAt(point) is var index and >= 0 && index < HtmlRuns.Count ? HtmlRuns[index].Style.Href : null;

    private bool IsHtml => _spans.Length == 0 && _textType == TextType.Html;

    /// <summary>Whether the text is drawn by the formatted-text engine (spans or HTML).</summary>
    private bool UsesRichText => _spans.Length > 0 || _textType == TextType.Html;

    private IReadOnlyList<SkUiHtmlRun> HtmlRuns => _htmlRuns ??= SkUiHtml.Parse(_text);

    /// <summary>
    /// The spans drawn instead of <see cref="Text"/> (MAUI's <c>FormattedText</c>), wrapped and aligned as one paragraph;
    /// empty: <see cref="Text"/>. Each span's unset values are the label's. The label's <see cref="LineBreakMode"/>,
    /// <see cref="MaxLines"/>, alignment and padding apply; <see cref="LineBreaker"/> and <see cref="TextRendering"/> do not
    /// (spans are always shaped).
    /// </summary>
    public IReadOnlyList<SkUiCoreSpan> Spans => _spans;

    /// <summary>
    /// Shows <paramref name="spans"/> instead of <see cref="Text"/>, which is cleared (as on MAUI's Label); <c>null</c> or
    /// none: back to <see cref="Text"/>. A span belongs to one label at a time.
    /// </summary>
    /// <exception cref="InvalidOperationException">A span is shown by another label, or appears twice.</exception>
    public SkUiCoreLabel SetSpans(params IEnumerable<SkUiCoreSpan>? spans)
    {
        var next = spans?.ToArray() ?? [];
        for (var index = 0; index < next.Length; index++)
        {
            var span = next[index] ?? throw new ArgumentNullException(nameof(spans), "Spans cannot be null.");
            if ((span.Owner is { } owner && !ReferenceEquals(owner, this)) || Array.IndexOf(next, span, 0, index) >= 0)
                throw new InvalidOperationException("A span can be shown by one label at a time, once.");
        }
        foreach (var span in _spans)
            span.Owner = null;
        foreach (var span in next)
            span.Owner = this;
        _spans = next;
        if (next.Length > 0 && SetProperty(ref _text, string.Empty, nameof(Text)))
            _displayText = string.Empty;
        _spanTap?.Cancel();
        OnPropertyChanged(nameof(Spans));
        InvalidateText();
        return this;
    }

    /// <summary>Appends <paramref name="span"/> to <see cref="Spans"/> (see <see cref="SetSpans(IEnumerable{SkUiCoreSpan}?)"/>).</summary>
    public SkUiCoreLabel AddSpan(SkUiCoreSpan span)
    {
        ArgumentNullException.ThrowIfNull(span);
        return SetSpans([.. _spans, span]);
    }

    private void ClearSpans()
    {
        if (_spans.Length > 0) SetSpans(null);
    }

    /// <summary>A span changed: lay out again, or only repaint for colors, backgrounds and decorations.</summary>
    internal void OnSpanChanged(bool layout)
    {
        _richText = null;
        if (layout) InvalidateText(); else InvalidatePaint();
    }

    /// <summary>The spans with the label's defaults applied, rebuilt when the system text size changed since.</summary>
    private SkUiRichText RichText
    {
        get
        {
            var version = SkUiFontScaling.Version;
            if (_richText is null || _fontScaleVersion != version)
            {
                _fontScaleVersion = version;
                _richText = BuildRichText();
            }
            return _richText;
        }
    }

    /// <summary>A font size as drawn: scaled by the system text size when <see cref="FontAutoScalingEnabled"/>.</summary>
    private double ScaledFontSize(double size) => SkUiFontScaling.ScaleFontSize(size, _fontAutoScalingEnabled);

    private SkUiRichTextLayout RichLayout => _richLayout ??= new SkUiRichTextLayout();

    private SkUiRichText BuildRichText()
    {
        if (IsHtml)
            return SkUiHtml.ToRichText(HtmlRuns, _fontFamily, _fontSize, _fontAttributes, _characterSpacing, _lineHeight, _textColor, _textDecorations,
                _fontAutoScalingEnabled);
        if (_spans.Length == 0) return SkUiRichText.Empty;
        var builder = new SkUiRichText.Builder();
        foreach (var span in _spans)
        {
            var attributes = span.FontAttributes ?? _fontAttributes;
            var transform = span.TextTransform != TextTransform.Default ? span.TextTransform : _textTransform;
            builder.Add(SkUiTextTransform.Apply(span.Text, transform),
                new SkUiTextSpanStyle(SkUiTypefaces.Resolve(span.FontFamily ?? _fontFamily, attributes), ScaledFontSize(span.FontSize ?? _fontSize), attributes,
                    span.CharacterSpacing ?? _characterSpacing, span.LineHeight ?? _lineHeight),
                new SkUiTextSpanPaint(ToSkColor(span.TextColor ?? _textColor), span.BackgroundColor is { } background ? ToSkColor(background) : default,
                    span.TextDecorations ?? _textDecorations));
        }
        return builder.Build();
    }

    /// <summary>The span drawn at <paramref name="point"/> (label coordinates), or <c>null</c> (beside the text, or no spans).</summary>
    public SkUiCoreSpan? SpanAt(Point point) =>
        _spans.Length > 0 && SpanIndexAt(point) is var index and >= 0 && index < _spans.Length ? _spans[index] : null;

    private int SpanIndexAt(Point point) =>
        RichLayout.HitTest(RichText, TextStyle, TextInset, Frame.Width, Frame.Height, _horizontal, _vertical, point);

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        if (_spans.Length > 0 ? Array.Exists(_spans, span => span.IsTappable) : IsHtml && SkUiHtml.HasLinks(HtmlRuns))
            recognizers.Add(_spanTap ??= new SkUiSpanTapGestureRecognizer
            {
                TappableSpanAt = TappableSpanAt,
                TapHandler = args =>
                {
                    if (_spanTap?.TappedSpan(args) is not (>= 0 and var index)) return;
                    if (_spans.Length > 0)
                    {
                        if (index < _spans.Length) _spans[index].RaiseTapped(args);
                    }
                    else if (index < HtmlRuns.Count && HtmlRuns[index].Style.Href is { } href)
                        _linkTapped?.Invoke(this, new SkUiLinkTappedEventArgs(href, args.Position));
                }
            });
        base.CollectGestureRecognizers(recognizers);
    }

    private int TappableSpanAt(Point point)
    {
        if (!UsesRichText || SpanIndexAt(point) is not (>= 0 and var index)) return -1;
        if (_spans.Length > 0)
            return index < _spans.Length && _spans[index].IsTappable ? index : -1;
        return index < HtmlRuns.Count && HtmlRuns[index].Style.Href is not null ? index : -1;
    }

    /// <summary>Sets bold and italic flags.</summary>
    public SkUiCoreLabel SetFontAttributes(FontAttributes value)
    {
        if (!SetProperty(ref _fontAttributes, value, nameof(FontAttributes))) return this;
        InvalidateText();
        return this;
    }

    /// <summary>Sets the maximum number of lines (-1, 0 or less: no limit).</summary>
    public SkUiCoreLabel SetMaxLines(int value)
    {
        if (!SetProperty(ref _maxLines, value, nameof(MaxLines))) return this;
        InvalidateText();
        return this;
    }

    /// <summary>Sets the line height multiplier (-1, 0 or less: the font's).</summary>
    public SkUiCoreLabel SetLineHeight(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        if (!SetProperty(ref _lineHeight, value, nameof(LineHeight))) return this;
        InvalidateText();
        return this;
    }

    /// <summary>Sets the spacing between characters in DIPs.</summary>
    public SkUiCoreLabel SetCharacterSpacing(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        if (!SetProperty(ref _characterSpacing, value, nameof(CharacterSpacing))) return this;
        InvalidateText();
        return this;
    }

    /// <summary>Sets underline / strikethrough.</summary>
    public SkUiCoreLabel SetTextDecorations(TextDecorations value)
    {
        if (!SetProperty(ref _textDecorations, value, nameof(TextDecorations))) return this;
        _richText = null;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the case transform of the displayed text.</summary>
    public SkUiCoreLabel SetTextTransform(TextTransform value)
    {
        if (!SetProperty(ref _textTransform, value, nameof(TextTransform))) return this;
        if (UsesRichText) InvalidateText(); // spans inherit it; the (empty) plain text would not change
        UpdateDisplayText();
        return this;
    }

    /// <summary>Sets foreground color.</summary>
    public SkUiCoreLabel SetTextColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _textColor, value, nameof(TextColor))) return this;
        _richText = null;
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

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/>.</summary>
    public SkUiCoreLabel SetFontAutoScalingEnabled(bool value)
    {
        if (!SetProperty(ref _fontAutoScalingEnabled, value, nameof(FontAutoScalingEnabled))) return this;
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
        if (!_chrome.SetBorderColor(value)) return this;
        OnPropertyChanged(nameof(BorderColor));
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets border width in DIPs.</summary>
    public SkUiCoreLabel SetBorderWidth(double value)
    {
        if (!_chrome.SetBorderWidth(value)) return this;
        OnPropertyChanged(nameof(BorderWidth));
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the per-corner radii in DIPs (marks them as app-explicit so look swaps do not replace them).</summary>
    public SkUiCoreLabel SetCornerRadii(Microsoft.Maui.CornerRadius value)
    {
        // Also a change from the default radii to the same stored value: they are explicit from now on.
        if (!_chrome.SetRadii(value)) return this;
        OnPropertyChanged(nameof(CornerRadii));
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets all four <see cref="CornerRadii"/> to <paramref name="value"/> DIPs (app-explicit, like <see cref="SetCornerRadii"/>).</summary>
    public SkUiCoreLabel SetCornerRadius(double value)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The corner radius must be finite and non-negative.");
        return SetCornerRadii(new Microsoft.Maui.CornerRadius(value));
    }

    /// <summary>Draws the rounded fill (<paramref name="fill"/>) and border through <see cref="SkUiLook.DrawRoundedBox(SKCanvas, SKRect, Microsoft.Maui.CornerRadius, SKColor, SKColor, float)"/>.</summary>
    protected void PaintChrome(SKCanvas canvas, Color fill) =>
        _chrome.Draw(canvas, (float)Frame.Width, (float)Frame.Height, EffectiveCornerRadii, ToSkColor(fill));

    /// <summary>
    /// Draws the rounded fill (a solid color or a gradient, <c>null</c>: none) and border through
    /// <see cref="SkUiLook.DrawRoundedBox(SKCanvas, SKRect, Microsoft.Maui.CornerRadius, Paint, SKColor, float)"/>.
    /// </summary>
    protected void PaintChrome(SKCanvas canvas, Paint? fill) =>
        _chrome.Draw(canvas, (float)Frame.Width, (float)Frame.Height, EffectiveCornerRadii, fill);

    /// <summary>The chrome's fill: <see cref="SkUiCoreNode.Background"/>, else <see cref="FillColor"/> (<c>null</c> when transparent).</summary>
    private SkUiFill ChromeFill => Background is { } background ? SkUiFill.From(background) : SkUiFill.From(_fillColor);

    /// <summary>The rounded chrome (fill and border), unless a <see cref="SkUiCoreNode.PaintBackground"/> painter replaces it.</summary>
    protected override void OnPaintBackground(SKCanvas canvas)
    {
        var fill = ChromeFill;
        if (fill.IsVisible || _chrome.HasBorder)
            _chrome.Draw(canvas, (float)Frame.Width, (float)Frame.Height, EffectiveCornerRadii, fill);
    }

    /// <inheritdoc />
    internal override SKPath? CreateShadowOutline(float width, float height) =>
        PaintBackground is null ? ChromeShadowOutline(width, height, EffectiveCornerRadii, ChromeFill.ToPaint()) : null;

    /// <summary>The shadow silhouette of the rounded chrome filled with <paramref name="fill"/> (<c>null</c> unless opaque).</summary>
    private protected SKPath? ChromeShadowOutline(float width, float height, Microsoft.Maui.CornerRadius radii, Paint? fill) =>
        _chrome.ShadowOutline(width, height, radii, fill);

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
        var relayout = value == TextAlignment.Justify || _horizontal == TextAlignment.Justify;
        if (!SetProperty(ref _horizontal, value, nameof(HorizontalTextAlignment))) return this;
        // Justified lines are stretched by the layout (and measure as wide as the label); other alignments only paint.
        if (relayout) InvalidateText(); else InvalidatePaint();
        return this;
    }

    /// <summary>Sets vertical text alignment within the arranged slot.</summary>
    public SkUiCoreLabel SetVerticalTextAlignment(TextAlignment value)
    {
        if (!SetProperty(ref _vertical, value, nameof(VerticalTextAlignment))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets wrapping / truncation (same semantics as <see cref="SkUiLabel.LineBreakMode"/>).</summary>
    public SkUiCoreLabel SetLineBreakMode(Microsoft.Maui.LineBreakMode mode)
    {
        if (!SetProperty(ref _lineBreakMode, mode, nameof(LineBreakMode))) return this;
        InvalidateText();
        return this;
    }

    /// <summary>Sets the custom line breaker (<c>null</c>: <see cref="LineBreakMode"/>).</summary>
    public SkUiCoreLabel SetLineBreaker(SkUiTextLineBreaker? value)
    {
        if (!SetProperty(ref _lineBreaker, value, nameof(LineBreaker))) return this;
        InvalidateText();
        return this;
    }

    /// <summary>Breaks the text again at the next measure, e.g. when what a custom <see cref="LineBreaker"/> reads has changed.</summary>
    public void InvalidateTextLayout() => InvalidateText();

    private SkUiTextStyle TextStyle => new(SkUiTypefaces.Resolve(_fontFamily, _fontAttributes), ScaledFontSize(_fontSize), _lineBreakMode, _lineBreaker,
        _maxLines, _lineHeight, _characterSpacing, EffectiveTextDirection, _textRendering, _fontAttributes, _horizontal == TextAlignment.Justify);

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => UsesRichText
        ? RichLayout.Measure(RichText, TextStyle, _padding, widthConstraint)
        : _layout.Measure(_displayText, TextStyle, _padding, widthConstraint);

    /// <summary>The text's size without padding when wrapped to <paramref name="widthConstraint"/> (buttons place an image beside it).</summary>
    private protected Size MeasureText(double widthConstraint) => UsesRichText
        ? RichLayout.Measure(RichText, TextStyle, default, widthConstraint)
        : _layout.Measure(_displayText, TextStyle, default, widthConstraint);

    /// <summary>Whether there is text to draw (plain, spans or HTML).</summary>
    private protected bool HasText => (UsesRichText ? RichText.Text : _displayText).Length > 0;

    /// <summary>The text's slot in the arranged label, as an inset from its edges: <see cref="Padding"/> (buttons: beside their image).</summary>
    private protected virtual Thickness TextInset => _padding;

    /// <summary>Whether glyphs are clipped to the text's slot (<see cref="TextInset"/>), not only to the bounds: buttons keep text that does not fit out of their padding and image.</summary>
    private protected virtual bool ClipsTextToInset => false;

    /// <summary>Whether <see cref="PaintIcon"/> draws something (a button's image).</summary>
    private protected virtual bool HasIcon => false;

    /// <summary>Draws content beside the text (a button's image), clipped with it to the rounded corners.</summary>
    private protected virtual void PaintIcon(SKCanvas canvas) { }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var hasText = HasText;
        if (!hasText && !HasIcon) return;
        // Glyphs never bleed past the rounded corners.
        var saveCount = _chrome.ClipToRadii(canvas, (float)Frame.Width, (float)Frame.Height, EffectiveCornerRadii);
        try
        {
            PaintIcon(canvas);
            if (hasText)
            {
                if (ClipsTextToInset)
                {
                    var inset = TextInset;
                    canvas.Save();
                    canvas.ClipRect(new SKRect((float)inset.Left, (float)inset.Top, (float)(Frame.Width - inset.Right), (float)(Frame.Height - inset.Bottom)));
                    PaintText(canvas);
                    canvas.Restore();
                }
                else
                    PaintText(canvas);
            }
        }
        finally { SkUiChromeState.EndClip(canvas, saveCount); }
    }

    private void PaintText(SKCanvas canvas)
    {
        var paint = _textPaint ??= new SKPaint { IsAntialias = true };
        var inset = TextInset;
        if (UsesRichText)
        {
            RichLayout.Draw(canvas, RichText, TextStyle, inset, Frame.Width, Frame.Height, _horizontal, _vertical, paint);
            return;
        }
        paint.Color = ToSkColor(_textColor);
        _layout.Draw(canvas, _displayText, TextStyle, inset, Frame.Width, Frame.Height,
            _horizontal, _vertical, paint, _textDecorations);
    }

    private void UpdateDisplayText()
    {
        var display = SkUiTextTransform.Apply(_text, _textTransform);
        if (display == _displayText) return;
        _displayText = display;
        InvalidateText();
    }

    private void InvalidateText()
    {
        _layout.Invalidate();
        _richText = null;
        _richLayout?.Invalidate();
        InvalidateMeasure();
    }
}
