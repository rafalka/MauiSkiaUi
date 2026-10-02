using SkiaSharp;
using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>
/// Drawn text with wrapping, alignment, and MAUI-style bindable properties: MAUI Label's text properties
/// (<see cref="MaxLines"/>, <see cref="LineHeight"/>, <see cref="CharacterSpacing"/>, <see cref="TextDecorations"/>,
/// <see cref="TextTransform"/>), spans (<see cref="FormattedText"/>) with their own styles and tap recognizers, plus a
/// custom <see cref="LineBreaker"/> that can shorten text its own way. Optional
/// rounded chrome (a badge, a chip, a tag): <see cref="CornerRadii"/> (or the uniform <see cref="CornerRadius"/>),
/// <see cref="BorderColor"/> and <see cref="BorderWidth"/> shape the <see cref="VisualElement.Background"/> fill without
/// wrapping the label in a border.
/// </summary>
[ContentProperty(nameof(Text))]
public class SkUiLabel : SkUiView
{
    private string _text = string.Empty;
    private string _displayText = string.Empty; // _text after _textTransform
    private Color _textColor = SkUiColors.DefaultForeground;
    private double _fontSize = 16;
    private string? _fontFamily;
    private FontAttributes _fontAttributes;
    private LineBreakMode _lineBreakMode = LineBreakMode.WordWrap;
    private SkUiTextLineBreaker? _lineBreaker;
    private int _maxLines = -1;
    private double _lineHeight = -1;
    private double _characterSpacing;
    private TextDecorations _textDecorations;
    private TextTransform _textTransform = TextTransform.Default;
    private TextAlignment _horizontalTextAlignment;
    private TextAlignment _verticalTextAlignment;
    private Thickness _padding;
    private readonly SkUiTextLayout _layout;
    private SkUiTextRendering _textRendering;
    private SKPaint? _textPaint;
    private Microsoft.Maui.CornerRadius _cornerRadii;
    private bool _syncingCornerRadius;
    private Color _borderColor = Colors.Transparent;
    private double _borderWidth;
    private SkUiRoundedClip _textClip;
    private FormattedString? _formattedText;
    private SkUiRichTextLayout? _richLayout;
    private SkUiRichText? _richText; // _formattedText resolved against the label's defaults; null: rebuild
    private SkUiSpanTapGestureRecognizer? _spanTap;
    private TextType _textType;
    private IReadOnlyList<SkUiHtmlRun>? _htmlRuns; // _text parsed as HTML; null: parse again
    private ICommand? _linkTappedCommand;

    /// <summary>Bindable text.</summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(SkUiLabel), string.Empty, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnTextChanged((string?)value));
    /// <summary>Bindable foreground color.</summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SkUiLabel), Colors.Black, defaultValueCreator: view => ((SkUiLabel)view).DefaultTextColor, validateValue: SkUiValidate.NotNull, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnTextColorChanged((Color)value));
    /// <summary>Bindable font size in DIPs.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkUiLabel), 16d, validateValue: SkUiValidate.FinitePositive, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnFontSizeChanged((double)value));
    /// <summary>Bindable system font family name.</summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(SkUiLabel), null, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnFontFamilyChanged((string?)value));
    /// <summary>Bindable bold and italic attributes.</summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(SkUiLabel), FontAttributes.None, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnFontAttributesChanged((FontAttributes)value));
    /// <summary>Bindable wrapping or truncation mode.</summary>
    public static readonly BindableProperty LineBreakModeProperty = BindableProperty.Create(nameof(LineBreakMode), typeof(LineBreakMode), typeof(SkUiLabel), LineBreakMode.WordWrap, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnLineBreakModeChanged((LineBreakMode)value));
    /// <summary>Bindable horizontal text alignment.</summary>
    public static readonly BindableProperty HorizontalTextAlignmentProperty = BindableProperty.Create(nameof(HorizontalTextAlignment), typeof(TextAlignment), typeof(SkUiLabel), TextAlignment.Start, defaultValueCreator: view => ((SkUiLabel)view).DefaultTextAlignment, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnHorizontalTextAlignmentChanged((TextAlignment)value));
    /// <summary>Bindable vertical text alignment.</summary>
    public static readonly BindableProperty VerticalTextAlignmentProperty = BindableProperty.Create(nameof(VerticalTextAlignment), typeof(TextAlignment), typeof(SkUiLabel), TextAlignment.Start, defaultValueCreator: view => ((SkUiLabel)view).DefaultTextAlignment, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnVerticalTextAlignmentChanged((TextAlignment)value));
    /// <summary>
    /// Bindable text rendering mode: <see cref="SkUiTextRendering.Auto"/> (via <see cref="SkUiTextRendering.Default"/>) takes a
    /// fast path for plain Latin text and shapes everything else with HarfBuzz; <see cref="SkUiTextRendering.Simple"/> never
    /// shapes (fastest, for dense plain text / numbers); <see cref="SkUiTextRendering.Shaped"/> always shapes (kerning, ligatures).
    /// </summary>
    public static readonly BindableProperty TextRenderingProperty = BindableProperty.Create(nameof(TextRendering), typeof(SkUiTextRendering), typeof(SkUiLabel), SkUiTextRendering.Default,
        propertyChanged: (view, _, value) => ((SkUiLabel)view).OnTextRenderingChanged((SkUiTextRendering)value));
    /// <summary>Bindable custom line breaker (<c>null</c>: <see cref="LineBreakMode"/>).</summary>
    public static readonly BindableProperty LineBreakerProperty = BindableProperty.Create(nameof(LineBreaker), typeof(SkUiTextLineBreaker), typeof(SkUiLabel), null, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnLineBreakerChanged((SkUiTextLineBreaker?)value));
    /// <summary>Bindable maximum number of lines (-1: no limit).</summary>
    public static readonly BindableProperty MaxLinesProperty = BindableProperty.Create(nameof(MaxLines), typeof(int), typeof(SkUiLabel), -1, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnMaxLinesChanged((int)value));
    /// <summary>Bindable line height multiplier (-1: the font's).</summary>
    public static readonly BindableProperty LineHeightProperty = BindableProperty.Create(nameof(LineHeight), typeof(double), typeof(SkUiLabel), -1d, validateValue: SkUiValidate.Finite, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnLineHeightChanged((double)value));
    /// <summary>Bindable spacing between characters in DIPs.</summary>
    public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create(nameof(CharacterSpacing), typeof(double), typeof(SkUiLabel), 0d, validateValue: SkUiValidate.Finite, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnCharacterSpacingChanged((double)value));
    /// <summary>Bindable underline / strikethrough.</summary>
    public static readonly BindableProperty TextDecorationsProperty = BindableProperty.Create(nameof(TextDecorations), typeof(TextDecorations), typeof(SkUiLabel), TextDecorations.None, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnTextDecorationsChanged((TextDecorations)value));
    /// <summary>Bindable case transform of the displayed text.</summary>
    public static readonly BindableProperty TextTransformProperty = BindableProperty.Create(nameof(TextTransform), typeof(TextTransform), typeof(SkUiLabel), TextTransform.Default, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnTextTransformChanged((TextTransform)value));
    /// <summary>Bindable formatted text (MAUI's <see cref="Microsoft.Maui.Controls.FormattedString"/> of <see cref="Span"/>s).</summary>
    public static readonly BindableProperty FormattedTextProperty = BindableProperty.Create(nameof(FormattedText), typeof(FormattedString), typeof(SkUiLabel), null,
        propertyChanging: (view, value, _) => ((SkUiLabel)view).DetachFormattedText((FormattedString?)value),
        propertyChanged: (view, _, value) => ((SkUiLabel)view).OnFormattedTextChanged((FormattedString?)value));
    /// <summary>Bindable text type: <see cref="TextType.Html"/> draws <see cref="Text"/> as HTML.</summary>
    public static readonly BindableProperty TextTypeProperty = BindableProperty.Create(nameof(TextType), typeof(TextType), typeof(SkUiLabel), TextType.Text,
        propertyChanged: (view, _, value) => ((SkUiLabel)view).OnTextTypeChanged((TextType)value));
    /// <summary>Bindable command run with the <c>href</c> of a tapped HTML link.</summary>
    public static readonly BindableProperty LinkTappedCommandProperty = BindableProperty.Create(nameof(LinkTappedCommand), typeof(ICommand), typeof(SkUiLabel), null,
        propertyChanged: (view, _, value) => ((SkUiLabel)view)._linkTappedCommand = (ICommand?)value);
    /// <summary>Bindable text inset.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(SkUiLabel), default(Thickness), defaultValueCreator: view => ((SkUiLabel)view).DefaultPadding, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnPaddingChanged((Thickness)value));

    /// <summary>Bindable per-corner radii of the background and border (0: square).</summary>
    public static readonly BindableProperty CornerRadiiProperty = BindableProperty.Create(nameof(CornerRadii), typeof(Microsoft.Maui.CornerRadius), typeof(SkUiLabel), default(Microsoft.Maui.CornerRadius),
        defaultValueCreator: view => new Microsoft.Maui.CornerRadius(((SkUiLabel)view).DefaultCornerRadius),
        validateValue: SkUiValidate.CornerRadii,
        propertyChanged: (view, _, value) => ((SkUiLabel)view).OnCornerRadiiChanged((Microsoft.Maui.CornerRadius)value));
    /// <summary>Bindable uniform corner radius (MAUI Button's <c>int</c> <c>CornerRadius</c>): sets all four <see cref="CornerRadii"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(int), typeof(SkUiLabel), 0,
        defaultValueCreator: view => (int)Math.Round(((SkUiLabel)view).DefaultCornerRadius),
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiLabel)view).OnCornerRadiusChanged((int)value));
    /// <summary>Bindable border color.</summary>
    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(SkUiLabel), Colors.Transparent, validateValue: SkUiValidate.NotNull, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnBorderColorChanged((Color)value));
    /// <summary>Bindable border width.</summary>
    public static readonly BindableProperty BorderWidthProperty = BindableProperty.Create(nameof(BorderWidth), typeof(double), typeof(SkUiLabel), 0d, validateValue: SkUiValidate.NonNegative, propertyChanged: (view, _, value) => ((SkUiLabel)view).OnBorderWidthChanged((double)value));

    /// <summary>
    /// Starts the applied state from the same defaults the bindable store reports (the <c>Default*</c> members, which
    /// derived controls such as buttons override), without touching the store.
    /// </summary>
    public SkUiLabel()
    {
        _layout = new SkUiTextLayout(this);
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

    /// <summary>Text displayed by the control. Setting it (to any string) clears <see cref="FormattedText"/>, as on MAUI's Label.</summary>
    public string Text { get => (string?)GetValue(TextProperty) ?? string.Empty; set => SetValue(TextProperty, value); }
    /// <summary>
    /// Text made of <see cref="Span"/>s, each with its own font, size, attributes, colors, decorations, character spacing,
    /// line height and text transform (unset ones are the label's), wrapped and aligned as one paragraph; a span's
    /// <see cref="TapGestureRecognizer"/>s run when it is tapped. Setting it clears <see cref="Text"/>, as on MAUI's Label.
    /// The label's <see cref="LineBreakMode"/>, <see cref="MaxLines"/>, alignment and padding apply; <see cref="LineBreaker"/>
    /// and <see cref="TextRendering"/> do not (spans are always shaped).
    /// </summary>
    public FormattedString? FormattedText { get => (FormattedString?)GetValue(FormattedTextProperty); set => SetValue(FormattedTextProperty, value); }
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
    /// <summary>
    /// Custom line breaking that replaces <see cref="LineBreakMode"/>, which it can still apply
    /// (<see cref="SkUiTextLineBreakContext.Break()"/>): a custom ellipsis (<see cref="SkUiTextLineBreakers.WithEllipsis"/>),
    /// shorter forms of the text such as a number with fewer decimals (<see cref="SkUiTextLineBreakers.FirstFit"/>), or
    /// any <see cref="SkUiTextLineBreaker"/>. <c>null</c> (default): <see cref="LineBreakMode"/>. It runs when the text,
    /// a text property or the width changes; call <see cref="InvalidateTextLayout"/> when its own inputs change.
    /// </summary>
    public SkUiTextLineBreaker? LineBreaker { get => (SkUiTextLineBreaker?)GetValue(LineBreakerProperty); set => SetValue(LineBreakerProperty, value); }
    /// <summary>
    /// The most lines drawn; -1 (default), 0 or less: no limit. Wrapped lines past it are dropped; with
    /// <see cref="LineBreakMode.TailTruncation"/> the text wraps and the last line ends with the ellipsis, as on MAUI's Label.
    /// </summary>
    public int MaxLines { get => (int)GetValue(MaxLinesProperty); set => SetValue(MaxLinesProperty, value); }
    /// <summary>Multiplier of the font's line spacing (1.5: half as much again); -1 (default), 0 or less: the font's. The extra space is split above and below each line.</summary>
    public double LineHeight { get => (double)GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    /// <summary>DIPs added after each character (negative: tighter). Wrapping and truncation account for it.</summary>
    public double CharacterSpacing { get => (double)GetValue(CharacterSpacingProperty); set => SetValue(CharacterSpacingProperty, value); }
    /// <summary>Underline and / or strikethrough, in the text color, under / through each line.</summary>
    public TextDecorations TextDecorations { get => (TextDecorations)GetValue(TextDecorationsProperty); set => SetValue(TextDecorationsProperty, value); }
    /// <summary>Displays <see cref="Text"/> in upper or lower case (invariant culture, as MAUI); <see cref="Text"/> keeps its value.</summary>
    public TextTransform TextTransform { get => (TextTransform)GetValue(TextTransformProperty); set => SetValue(TextTransformProperty, value); }
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
    public int CornerRadius { get => (int)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    /// <summary>Border color (drawn inside the bounds; <see cref="Padding"/> is not adjusted).</summary>
    public Color BorderColor { get => (Color)GetValue(BorderColorProperty); set => SetValue(BorderColorProperty, value); }
    /// <summary>Border width in DIPs.</summary>
    public double BorderWidth { get => (double)GetValue(BorderWidthProperty); set => SetValue(BorderWidthProperty, value); }
    /// <inheritdoc cref="TextRenderingProperty" />
    public SkUiTextRendering TextRendering { get => (SkUiTextRendering)GetValue(TextRenderingProperty); set => SetValue(TextRenderingProperty, value); }
    /// <summary>Sets <see cref="TextRendering"/> (same as the property setter).</summary>
    public SkUiLabel SetTextRendering(SkUiTextRendering value) { TextRendering = value; return this; }
    private void OnTextRenderingChanged(SkUiTextRendering value) { if (_textRendering == value) return; _textRendering = value; InvalidateText(); }

    /// <summary>Sets text (same as the property setter).</summary>
    public SkUiLabel SetText(string? value) { Text = value ?? string.Empty; return this; }
    private void OnTextChanged(string? value)
    {
        if (value is not null) FormattedText = null; // as MAUI: text replaces formatted text
        value ??= string.Empty;
        if (_text == value) return;
        _text = value;
        _htmlRuns = null;
        if (IsHtml) InvalidateText();
        UpdateDisplayText();
    }
    /// <summary>Sets formatted text (same as the property setter).</summary>
    public SkUiLabel SetFormattedText(FormattedString? value) { FormattedText = value; return this; }

    /// <summary>
    /// How <see cref="Text"/> is read (MAUI's): <see cref="TextType.Html"/> draws it as HTML (<see cref="SkUiHtml"/> lists
    /// the tags) with the label's values as the defaults the markup overrides, through the formatted-text engine; links
    /// raise <see cref="LinkTapped"/>. <see cref="FormattedText"/> still wins when set; <see cref="TextTransform"/>,
    /// <see cref="LineBreaker"/> and <see cref="TextRendering"/> do not apply to HTML.
    /// </summary>
    public TextType TextType { get => (TextType)GetValue(TextTypeProperty); set => SetValue(TextTypeProperty, value); }
    /// <summary>Sets the text type (same as the property setter).</summary>
    public SkUiLabel SetTextType(TextType value) { TextType = value; return this; }
    private void OnTextTypeChanged(TextType value) { if (_textType == value) return; _textType = value; _htmlRuns = null; _spanTap?.Cancel(); InvalidateText(); }

    /// <summary>Command run with the <c>href</c> of a tapped HTML link (<see cref="TextType.Html"/>), after <see cref="LinkTapped"/>.</summary>
    public ICommand? LinkTappedCommand { get => (ICommand?)GetValue(LinkTappedCommandProperty); set => SetValue(LinkTappedCommandProperty, value); }
    /// <summary>Sets the link command (same as the property setter).</summary>
    public SkUiLabel SetLinkTappedCommand(ICommand? value) { LinkTappedCommand = value; return this; }

    /// <summary>
    /// A link (<c>&lt;a href&gt;</c>) of HTML text was tapped. Nothing opens by itself: open the <c>Href</c> here or in
    /// <see cref="LinkTappedCommand"/>. A press on a link takes the tap from the label and its ancestors.
    /// </summary>
    public event EventHandler<SkUiLinkTappedEventArgs>? LinkTapped;

    private bool IsHtml => _formattedText is null && _textType == TextType.Html;

    /// <summary>Whether the text is drawn by the formatted-text engine (spans or HTML).</summary>
    private bool UsesRichText => _formattedText is not null || _textType == TextType.Html;

    private IReadOnlyList<SkUiHtmlRun> HtmlRuns => _htmlRuns ??= SkUiHtml.Parse(_text);

    private void DetachFormattedText(FormattedString? old)
    {
        if (old is null) return;
        old.PropertyChanged -= OnFormattedTextPropertyChanged;
        if (ReferenceEquals(old.Parent, this)) old.Parent = null;
    }

    private void OnFormattedTextChanged(FormattedString? value)
    {
        _formattedText = value;
        if (value is not null)
        {
            // As MAUI's Label: the label parents the formatted string (binding context, styles); spans follow it.
            value.Parent = this;
            value.PropertyChanged += OnFormattedTextPropertyChanged;
            Text = null!; // as MAUI: formatted text replaces text
        }
        _spanTap?.Cancel();
        InvalidateText();
    }

    /// <summary>A span was added, removed or changed (<see cref="FormattedString"/> reports all of them as <c>Spans</c>).</summary>
    private void OnFormattedTextPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FormattedString.Spans)) return;
        if (_richText is not { } previous) { InvalidateText(); return; }
        // A color, background or decoration change repaints the lines it already has.
        _richText = null;
        if (RichText.SameLayout(previous)) InvalidatePaint(); else InvalidateText();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (_formattedText is { } formatted) SetInheritedBindingContext(formatted, BindingContext);
    }
    /// <summary>Sets text color (same as the property setter).</summary>
    public SkUiLabel SetTextColor(Color value) { ArgumentNullException.ThrowIfNull(value); TextColor = value; return this; }
    private void OnTextColorChanged(Color value) { if (_textColor == value) return; _textColor = value; _richText = null; InvalidatePaint(); }
    /// <summary>Sets font size (same as the property setter).</summary>
    public SkUiLabel SetFontSize(double value) { if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); FontSize = value; return this; }
    private void OnFontSizeChanged(double value) { if (_fontSize == value) return; _fontSize = value; InvalidateText(); }
    /// <summary>Sets font family (same as the property setter).</summary>
    public SkUiLabel SetFontFamily(string? value) { FontFamily = value; return this; }
    private void OnFontFamilyChanged(string? value) { if (_fontFamily == value) return; _fontFamily = value; InvalidateText(); }
    /// <summary>Sets font attributes (same as the property setter).</summary>
    public SkUiLabel SetFontAttributes(FontAttributes value) { FontAttributes = value; return this; }
    private void OnFontAttributesChanged(FontAttributes value) { if (_fontAttributes == value) return; _fontAttributes = value; InvalidateText(); }
    /// <summary>Sets line mode (same as the property setter).</summary>
    public SkUiLabel SetLineBreakMode(LineBreakMode value) { LineBreakMode = value; return this; }
    private void OnLineBreakModeChanged(LineBreakMode value) { _lineBreakMode = value; InvalidateText(); }
    /// <summary>Sets the custom line breaker (same as the property setter).</summary>
    public SkUiLabel SetLineBreaker(SkUiTextLineBreaker? value) { LineBreaker = value; return this; }
    private void OnLineBreakerChanged(SkUiTextLineBreaker? value) { _lineBreaker = value; InvalidateText(); }
    /// <summary>Sets the maximum number of lines (same as the property setter).</summary>
    public SkUiLabel SetMaxLines(int value) { MaxLines = value; return this; }
    private void OnMaxLinesChanged(int value) { if (_maxLines == value) return; _maxLines = value; InvalidateText(); }
    /// <summary>Sets the line height multiplier (same as the property setter).</summary>
    public SkUiLabel SetLineHeight(double value) { SkUiValidate.ThrowIfNotFinite(value, nameof(value)); LineHeight = value; return this; }
    private void OnLineHeightChanged(double value) { if (_lineHeight == value) return; _lineHeight = value; InvalidateText(); }
    /// <summary>Sets the character spacing (same as the property setter).</summary>
    public SkUiLabel SetCharacterSpacing(double value) { SkUiValidate.ThrowIfNotFinite(value, nameof(value)); CharacterSpacing = value; return this; }
    private void OnCharacterSpacingChanged(double value) { if (_characterSpacing == value) return; _characterSpacing = value; InvalidateText(); }
    /// <summary>Sets the text decorations (same as the property setter).</summary>
    public SkUiLabel SetTextDecorations(TextDecorations value) { TextDecorations = value; return this; }
    private void OnTextDecorationsChanged(TextDecorations value) { if (_textDecorations == value) return; _textDecorations = value; _richText = null; InvalidatePaint(); }
    /// <summary>Sets the text transform (same as the property setter).</summary>
    public SkUiLabel SetTextTransform(TextTransform value) { TextTransform = value; return this; }
    private void OnTextTransformChanged(TextTransform value)
    {
        if (_textTransform == value) return;
        _textTransform = value;
        if (UsesRichText) InvalidateText(); // spans inherit it; the (empty) plain text would not change
        UpdateDisplayText();
    }
    /// <summary>Sets horizontal alignment (same as the property setter).</summary>
    public SkUiLabel SetHorizontalTextAlignment(TextAlignment value) { HorizontalTextAlignment = value; return this; }
    private void OnHorizontalTextAlignmentChanged(TextAlignment value)
    {
        // Justified lines are stretched by the layout (and measure as wide as the label); other alignments only paint.
        var relayout = value == TextAlignment.Justify || _horizontalTextAlignment == TextAlignment.Justify;
        _horizontalTextAlignment = value;
        if (relayout) InvalidateText(); else InvalidatePaint();
    }
    /// <summary>Sets vertical alignment (same as the property setter).</summary>
    public SkUiLabel SetVerticalTextAlignment(TextAlignment value) { VerticalTextAlignment = value; return this; }
    private void OnVerticalTextAlignmentChanged(TextAlignment value) { _verticalTextAlignment = value; InvalidatePaint(); }
    /// <summary>Sets padding (same as the property setter).</summary>
    public SkUiLabel SetPadding(Thickness value) { Padding = value; return this; }
    private void OnPaddingChanged(Thickness value) { _padding = value; InvalidateText(); }

    /// <summary>Sets the per-corner radii (same as the property setter).</summary>
    public SkUiLabel SetCornerRadii(Microsoft.Maui.CornerRadius value) { SkUiCornerRadii.Validate(value, nameof(value)); CornerRadii = value; return this; }
    private void OnCornerRadiiChanged(Microsoft.Maui.CornerRadius value)
    {
        if (_cornerRadii == value) return;
        _cornerRadii = value;
        SyncCornerRadiusProperty();
        InvalidatePaint();
    }
    /// <summary>Sets all four corner radii to <paramref name="value"/> (same as the property setter).</summary>
    public SkUiLabel SetCornerRadius(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); CornerRadius = value; return this; }
    private void OnCornerRadiusChanged(int value) { if (!_syncingCornerRadius) CornerRadii = new Microsoft.Maui.CornerRadius(value); }

    /// <summary>Keeps the <see cref="CornerRadius"/> store at the top-left radius, rounded, so its bindings see <see cref="CornerRadii"/> changes.</summary>
    private void SyncCornerRadiusProperty()
    {
        var uniform = (int)Math.Round(_cornerRadii.TopLeft);
        if ((int)GetValue(CornerRadiusProperty) == uniform) return;
        _syncingCornerRadius = true; // the store follows the radii; it must not set all four corners back
        try { SetValue(CornerRadiusProperty, uniform); }
        finally { _syncingCornerRadius = false; }
    }
    /// <summary>Sets border color (same as the property setter).</summary>
    public SkUiLabel SetBorderColor(Color value) { ArgumentNullException.ThrowIfNull(value); BorderColor = value; return this; }
    private void OnBorderColorChanged(Color value) { if (_borderColor == value) return; _borderColor = value; InvalidatePaint(); }
    /// <summary>Sets border width (same as the property setter).</summary>
    public SkUiLabel SetBorderWidth(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); BorderWidth = value; return this; }
    private void OnBorderWidthChanged(double value) { if (_borderWidth == value) return; _borderWidth = value; InvalidatePaint(); }

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

    private void InvalidateText()
    {
        _layout.Invalidate();
        _richText = null;
        _richLayout?.Invalidate();
        InvalidateMeasureOverride();
    }

    /// <summary>Breaks the text again at the next measure, e.g. when what a custom <see cref="LineBreaker"/> reads has changed.</summary>
    public void InvalidateTextLayout() => InvalidateText();

    private void UpdateDisplayText()
    {
        var display = SkUiTextTransform.Apply(_text, _textTransform);
        if (display == _displayText) return;
        _displayText = display;
        InvalidateText();
    }

    private SkUiTextStyle TextStyle => new(SkUiTypefaces.Resolve(_fontFamily, _fontAttributes), _fontSize, _lineBreakMode, _lineBreaker,
        _maxLines, _lineHeight, _characterSpacing, TextDirection, _textRendering, _fontAttributes, _horizontalTextAlignment == TextAlignment.Justify);

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

    /// <summary>The spans with the label's defaults applied (MAUI's rule: a span's own value when set, else the label's).</summary>
    private SkUiRichText RichText => _richText ??= BuildRichText();

    private SkUiRichTextLayout RichLayout => _richLayout ??= new SkUiRichTextLayout();

    private SkUiRichText BuildRichText()
    {
        if (IsHtml)
            return SkUiHtml.ToRichText(HtmlRuns, _fontFamily, _fontSize, _fontAttributes, _characterSpacing, _lineHeight, _textColor, _textDecorations);
        if (_formattedText is not { Spans.Count: > 0 } formatted) return SkUiRichText.Empty;
        var builder = new SkUiRichText.Builder();
        foreach (var span in formatted.Spans)
        {
            var family = span.IsSet(Span.FontFamilyProperty) ? span.FontFamily : _fontFamily;
            var size = span.IsSet(Span.FontSizeProperty) && double.IsFinite(span.FontSize) && span.FontSize > 0 ? span.FontSize : _fontSize;
            var attributes = span.IsSet(Span.FontAttributesProperty) ? span.FontAttributes : _fontAttributes;
            var spacing = span.IsSet(Span.CharacterSpacingProperty) && double.IsFinite(span.CharacterSpacing) ? span.CharacterSpacing : _characterSpacing;
            var lineHeight = span.LineHeight >= 0 ? span.LineHeight : _lineHeight;
            var decorations = span.IsSet(Span.TextDecorationsProperty) ? span.TextDecorations : _textDecorations;
            var transform = span.TextTransform != TextTransform.Default ? span.TextTransform : _textTransform;
            builder.Add(SkUiTextTransform.Apply(span.Text ?? string.Empty, transform),
                new SkUiTextSpanStyle(SkUiTypefaces.Resolve(family, attributes), size, attributes, spacing, lineHeight),
                new SkUiTextSpanPaint(ToSkColor(span.TextColor ?? _textColor), span.BackgroundColor is { } background ? ToSkColor(background) : default, decorations));
        }
        return builder.Build();
    }

    /// <summary>The <see cref="FormattedText"/> span drawn at <paramref name="point"/> (label coordinates), or <c>null</c>.</summary>
    public Span? SpanAt(Point point) =>
        _formattedText is { } formatted && SpanIndexAt(point) is var index and >= 0 && index < formatted.Spans.Count ? formatted.Spans[index] : null;

    /// <summary>The <c>href</c> of the HTML link drawn at <paramref name="point"/> (label coordinates), or <c>null</c>.</summary>
    public string? LinkAt(Point point) =>
        IsHtml && SpanIndexAt(point) is var index and >= 0 && index < HtmlRuns.Count ? HtmlRuns[index].Style.Href : null;

    private int SpanIndexAt(Point point) =>
        RichLayout.HitTest(RichText, TextStyle, _padding, Width, Height, _horizontalTextAlignment, _verticalTextAlignment, point);

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        if (_formattedText is { } formatted ? HasSpanTaps(formatted) : IsHtml && SkUiHtml.HasLinks(HtmlRuns))
            recognizers.Add(_spanTap ??= new SkUiSpanTapGestureRecognizer
            {
                TappableSpanAt = TappableSpanAt,
                WantsDoubleTap = () => _formattedText?.Spans.Any(span => SkUiMauiTaps.Has(span, 2)) == true,
                TapHandler = args => RaiseSpanTap(args, 1),
                DoubleTapHandler = args => RaiseSpanTap(args, 2)
            });
        base.CollectGestureRecognizers(recognizers);
    }

    private static bool HasSpanTaps(FormattedString formatted)
    {
        foreach (var span in formatted.Spans)
            if (IsTappable(span))
                return true;
        return false;
    }

    private static bool IsTappable(Span span) => SkUiMauiTaps.Has(span, 1) || SkUiMauiTaps.Has(span, 2);

    private int TappableSpanAt(Point point)
    {
        if (!UsesRichText || SpanIndexAt(point) is not (>= 0 and var index)) return -1;
        if (_formattedText is { } formatted)
            return index < formatted.Spans.Count && IsTappable(formatted.Spans[index]) ? index : -1;
        return index < HtmlRuns.Count && HtmlRuns[index].Style.Href is not null ? index : -1;
    }

    private void RaiseSpanTap(SkUiTappedEventArgs args, int taps)
    {
        if (_spanTap?.TappedSpan(args) is not (>= 0 and var index)) return;
        if (_formattedText is { } formatted)
        {
            if (index < formatted.Spans.Count)
                SkUiMauiTaps.Raise(formatted.Spans[index], taps, this, args.Position);
            return;
        }
        if (taps == 1 && index < HtmlRuns.Count && HtmlRuns[index].Style.Href is { } href)
        {
            LinkTapped?.Invoke(this, new SkUiLinkTappedEventArgs(href, args.Position));
            if (_linkTappedCommand?.CanExecute(href) == true)
                _linkTappedCommand.Execute(href);
        }
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => UsesRichText
        ? RichLayout.Measure(RichText, TextStyle, _padding, widthConstraint)
        : _layout.Measure(_displayText, TextStyle, _padding, widthConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if ((UsesRichText ? RichText.Text : _displayText).Length == 0) return;
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
        if (UsesRichText)
        {
            RichLayout.Draw(canvas, RichText, TextStyle, _padding, Width, Height, _horizontalTextAlignment, _verticalTextAlignment, paint);
            return;
        }
        paint.Color = ToSkColor(_textColor);
        _layout.Draw(canvas, _displayText, TextStyle, _padding, Width, Height,
            _horizontalTextAlignment, _verticalTextAlignment, paint, _textDecorations);
    }
}