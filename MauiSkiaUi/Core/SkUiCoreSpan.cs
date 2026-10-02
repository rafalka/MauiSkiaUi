using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MauiSkiaUi.Core;

/// <summary>
/// A run of text with its own style in a <see cref="SkUiCoreLabel"/> (MAUI's <c>Span</c>, see
/// <see cref="SkUiCoreLabel.SetSpans(IEnumerable{SkUiCoreSpan}?)"/>). Unset values (<c>null</c>) are the label's; set ones
/// override them for this span. Changes redraw the label (colors, background, decorations) or lay it out again. A span
/// belongs to one label at a time.
/// </summary>
public sealed class SkUiCoreSpan : INotifyPropertyChanged
{
    private string _text;
    private Color? _textColor;
    private Color? _backgroundColor;
    private string? _fontFamily;
    private double? _fontSize;
    private FontAttributes? _fontAttributes;
    private double? _characterSpacing;
    private double? _lineHeight;
    private TextDecorations? _textDecorations;
    private TextTransform _textTransform = TextTransform.Default;
    private EventHandler<SkUiTappedEventArgs>? _tapped;

    /// <summary>Creates a span with <paramref name="text"/>.</summary>
    public SkUiCoreSpan(string? text = null) => _text = text ?? string.Empty;

    /// <summary>The label showing this span.</summary>
    public SkUiCoreLabel? Owner { get; internal set; }

    /// <summary>The span's text (it may contain newlines).</summary>
    public string Text { get => _text; set => SetText(value); }

    /// <summary>Glyph and decoration color; <c>null</c>: the label's <see cref="SkUiCoreLabel.TextColor"/>.</summary>
    public Color? TextColor { get => _textColor; set => SetTextColor(value); }

    /// <summary>Fill behind the span's glyphs (the font's ascent to descent); <c>null</c>: none.</summary>
    public Color? BackgroundColor { get => _backgroundColor; set => SetBackgroundColor(value); }

    /// <summary>Font family; <c>null</c>: the label's.</summary>
    public string? FontFamily { get => _fontFamily; set => SetFontFamily(value); }

    /// <summary>Font size in DIPs; <c>null</c>: the label's.</summary>
    public double? FontSize { get => _fontSize; set => SetFontSize(value); }

    /// <summary>Bold and italic; <c>null</c>: the label's.</summary>
    public FontAttributes? FontAttributes { get => _fontAttributes; set => SetFontAttributes(value); }

    /// <summary>DIPs added after each character; <c>null</c>: the label's.</summary>
    public double? CharacterSpacing { get => _characterSpacing; set => SetCharacterSpacing(value); }

    /// <summary>
    /// Multiplier of the span's line spacing (a line is as tall as its tallest span); <c>null</c>: the label's
    /// <see cref="SkUiCoreLabel.LineHeight"/>.
    /// </summary>
    public double? LineHeight { get => _lineHeight; set => SetLineHeight(value); }

    /// <summary>Underline / strikethrough; <c>null</c>: the label's.</summary>
    public TextDecorations? TextDecorations { get => _textDecorations; set => SetTextDecorations(value); }

    /// <summary>Case transform of the span's text; <see cref="TextTransform.Default"/>: the label's.</summary>
    public TextTransform TextTransform { get => _textTransform; set => SetTextTransform(value); }

    /// <summary>
    /// Tap on the span's glyphs (the sender is the span; the position is in the label's coordinates). Handlers make
    /// the span tappable: a press on it takes the tap from the label and its ancestors, a press beside it does not.
    /// </summary>
    public event EventHandler<SkUiTappedEventArgs>? Tapped { add => _tapped += value; remove => _tapped -= value; }

    /// <summary>Whether the span has <see cref="Tapped"/> handlers.</summary>
    internal bool IsTappable => _tapped is not null;

    internal void RaiseTapped(SkUiTappedEventArgs args) => _tapped?.Invoke(this, args);

    /// <summary>Sets <see cref="Text"/>.</summary>
    public SkUiCoreSpan SetText(string? value) => Set(ref _text, value ?? string.Empty, layout: true);

    /// <summary>Sets <see cref="TextColor"/>.</summary>
    public SkUiCoreSpan SetTextColor(Color? value) => Set(ref _textColor, value, layout: false);

    /// <summary>Sets <see cref="BackgroundColor"/>.</summary>
    public SkUiCoreSpan SetBackgroundColor(Color? value) => Set(ref _backgroundColor, value, layout: false);

    /// <summary>Sets <see cref="FontFamily"/>.</summary>
    public SkUiCoreSpan SetFontFamily(string? value) => Set(ref _fontFamily, value, layout: true);

    /// <summary>Sets <see cref="FontSize"/> (finite and positive, or <c>null</c>).</summary>
    public SkUiCoreSpan SetFontSize(double? value)
    {
        if (value is { } size && (!double.IsFinite(size) || size <= 0)) throw new ArgumentOutOfRangeException(nameof(value));
        return Set(ref _fontSize, value, layout: true);
    }

    /// <summary>Sets <see cref="FontAttributes"/>.</summary>
    public SkUiCoreSpan SetFontAttributes(FontAttributes? value) => Set(ref _fontAttributes, value, layout: true);

    /// <summary>Sets <see cref="CharacterSpacing"/> (finite, or <c>null</c>).</summary>
    public SkUiCoreSpan SetCharacterSpacing(double? value)
    {
        if (value is { } spacing) SkUiValidate.ThrowIfNotFinite(spacing, nameof(value));
        return Set(ref _characterSpacing, value, layout: true);
    }

    /// <summary>Sets <see cref="LineHeight"/> (finite, or <c>null</c>; 0 or less: the font's).</summary>
    public SkUiCoreSpan SetLineHeight(double? value)
    {
        if (value is { } height) SkUiValidate.ThrowIfNotFinite(height, nameof(value));
        return Set(ref _lineHeight, value, layout: true);
    }

    /// <summary>Sets <see cref="TextDecorations"/>.</summary>
    public SkUiCoreSpan SetTextDecorations(TextDecorations? value) => Set(ref _textDecorations, value, layout: false);

    /// <summary>Sets <see cref="TextTransform"/>.</summary>
    public SkUiCoreSpan SetTextTransform(TextTransform value) => Set(ref _textTransform, value, layout: true);

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private SkUiCoreSpan Set<T>(ref T field, T value, bool layout, [CallerMemberName] string? setter = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return this;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(setter![3..])); // SetText → Text
        Owner?.OnSpanChanged(layout);
        return this;
    }
}
