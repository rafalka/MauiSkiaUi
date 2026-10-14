using System.Globalization;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// What <see cref="SkUiAvatarView"/> and <see cref="Core.SkUiCoreAvatar"/> draw inside their border, shared by both
/// layers: the image once it has loaded (through the shared loader and cache, decoded just large enough to cover the
/// content area), else the text (<c>Text</c> as it is, or its initials) through the shared text engine, both
/// clipped to the inside of the stroke; the colors from the look by color mode; the badge over the border. Each layer
/// copies its property values in here and invalidates its node; a new text or image re-records only the node.
/// </summary>
internal sealed class SkUiAvatarContent
{
    /// <summary>The Community Toolkit's default text.</summary>
    internal const string DefaultText = "?";

    /// <summary>The smallest scale text that sizes itself shrinks to before it is clipped.</summary>
    private const float AutoMinimumScale = 0.25f;

    private readonly ISkUiTransitionHost _host;
    private readonly Action _imageChanged;
    private readonly SkUiTextLayout _layout;
    private SkUiImageSlot? _image;
    private SkUiImageSource? _source;
    private SkUiImageOptions _options = new(DownsampleToFill: true);

    private string _text = DefaultText;
    private SkUiAvatarInitials _initials;
    private TextTransform _textTransform = TextTransform.Default;
    private string _displayText = DefaultText;

    private string? _fontFamily;
    private FontAttributes _fontAttributes;
    private SKTypeface _typeface = SkUiTypefaces.Resolve(null);
    private double _fontSize = -1;
    private bool _fontAutoScalingEnabled = true;
    private double _characterSpacing;

    /// <param name="host">The node: its clock plays animated images, its paint invalidation shows them.</param>
    /// <param name="imageChanged">Called when the image (or its load state) changed: the node repaints.</param>
    public SkUiAvatarContent(ISkUiTransitionHost host, Action imageChanged)
    {
        _host = host;
        _imageChanged = imageChanged;
        _layout = new SkUiTextLayout(host);
    }

    /// <summary>Fit, fill (default), stretch or center the image in the content area.</summary>
    public Aspect Aspect { get; set; } = Aspect.AspectFill;

    /// <summary>Where the colors come from when the background or text color is not set.</summary>
    public SkUiAvatarColorMode ColorMode { get; set; }

    /// <summary>The text shown without an image: the text's initials, else the transformed text.</summary>
    public string DisplayText => _displayText;

    /// <summary>The intrinsic size depends on the text (the font size is set); otherwise it is the look's avatar size.</summary>
    public bool MeasuresText => _fontSize >= 0;

    /// <summary>Whether a loaded image is drawn instead of the text.</summary>
    public bool ShowsImage => _image?.Image is not null;

    /// <summary>The current load (completed when idle; tests).</summary>
    public Task LoadingTask => _image?.LoadingTask ?? Task.CompletedTask;

    /// <summary>The shown image's cache entry (tests: avatars of one source share it).</summary>
    internal SkUiCachedImage? CachedImage => _image?.Entry;

    /// <summary>The decode bounds in DIPs (tests).</summary>
    internal Size DecodeSize => new(_options.DownsampleWidth, _options.DownsampleHeight);

    /// <summary>What screen readers read: the whole text (a person's name rather than its initials).</summary>
    public string SemanticsText => _text;

    /// <summary>The look's colors for this avatar, picked by its whole text.</summary>
    public SkUiAvatarColors ResolveColors() => SkUiLook.Current.GetAvatarColors(ColorMode, _text);

    /// <summary>Sets the text; returns whether what is shown changed.</summary>
    public bool SetText(string? value)
    {
        value ??= string.Empty;
        if (_text == value) return false;
        _text = value;
        return UpdateDisplayText();
    }

    /// <summary>Sets whether initials are made from the text; returns whether what is shown changed.</summary>
    public bool SetInitials(SkUiAvatarInitials value)
    {
        if (_initials == value) return false;
        _initials = value;
        return UpdateDisplayText();
    }

    /// <summary>Sets the text's case transform; returns whether what is shown changed.</summary>
    public bool SetTextTransform(TextTransform value)
    {
        if (_textTransform == value) return false;
        _textTransform = value;
        return UpdateDisplayText();
    }

    private bool UpdateDisplayText()
    {
        var shown = Initials(_text, _initials) is { Length: > 0 } initials ? initials : SkUiTextTransform.Apply(_text, _textTransform);
        if (_displayText == shown) return false;
        _displayText = shown;
        _layout.Invalidate(); // the lines are cached by style and width, not by text
        return true;
    }

    /// <summary>Sets the font; returns whether it changed.</summary>
    public bool SetFont(string? family, FontAttributes attributes)
    {
        if (_fontFamily == family && _fontAttributes == attributes) return false;
        _fontFamily = family;
        _fontAttributes = attributes;
        _typeface = SkUiTypefaces.Resolve(family, attributes);
        _layout.Invalidate();
        return true;
    }

    /// <summary>Sets the font size (negative: sized by the look to the avatar); returns whether it changed.</summary>
    public bool SetFontSize(double value)
    {
        if (_fontSize == value) return false; // a setter's change check
        _fontSize = value;
        return true;
    }

    /// <summary>Sets whether a set font size follows the system text size; returns whether it changed.</summary>
    public bool SetFontAutoScalingEnabled(bool value)
    {
        if (_fontAutoScalingEnabled == value) return false;
        _fontAutoScalingEnabled = value;
        return true;
    }

    /// <summary>Sets the character spacing; returns whether it changed.</summary>
    public bool SetCharacterSpacing(double value)
    {
        if (_characterSpacing == value) return false; // a setter's change check
        _characterSpacing = value;
        return true;
    }

    /// <summary>
    /// Loads <paramref name="source"/> (<c>null</c>: none), decoded to cover <paramref name="expectedContent"/> DIPs (the
    /// content area the avatar has, or will most likely get).
    /// </summary>
    public void SetSource(SkUiImageSource? source, Size expectedContent)
    {
        if (source is not null && Equals(_source, source) && (_image?.Entry is not null || _image?.IsLoading == true))
            return;
        _source = source;
        if (source is null && _image is null)
            return;
        UpdateDecodeSize(expectedContent);
        Load();
    }

    /// <summary>Loads the current source again (its properties changed).</summary>
    public void Reload()
    {
        if (_source is not null || _image is not null)
            Load();
    }

    /// <summary>
    /// The avatar was arranged with a content area of <paramref name="content"/> DIPs: a larger area than the image was
    /// decoded for reloads it (a smaller one keeps the image, drawn smaller).
    /// </summary>
    public void OnContentSizeChanged(Size content)
    {
        if (_source is null || !UpdateDecodeSize(content)) return;
        Load(keepShown: true); // the smaller image until the larger one has loaded, not the text
    }

    private bool UpdateDecodeSize(Size content)
    {
        // Whole DIPs, so avatars of one size share their cache entries.
        var width = Math.Ceiling(content.Width);
        var height = Math.Ceiling(content.Height);
        if (width <= 0 || height <= 0 || (width <= _options.DownsampleWidth && height <= _options.DownsampleHeight))
            return false;
        _options = _options with { DownsampleWidth = Math.Max(width, _options.DownsampleWidth), DownsampleHeight = Math.Max(height, _options.DownsampleHeight) };
        return true;
    }

    private void Load(bool keepShown = false) => (_image ??= new SkUiImageSlot(_host, _imageChanged)).Load(_source, _options, keepShown);

    /// <summary>
    /// The intrinsic size: the text in its set font size plus <paramref name="inset"/> when <see cref="MeasuresText"/>,
    /// otherwise the look's avatar size. The image never changes it (a loaded image does not lay the list out again).
    /// </summary>
    public Size Measure(Thickness inset)
    {
        if (!MeasuresText)
            return SkUiLook.Current.DefaultAvatarSize;
        var text = _displayText.Length == 0 ? Size.Zero : _layout.Measure(_displayText, Style(Size.Zero), default, double.PositiveInfinity);
        return new Size(text.Width + inset.HorizontalThickness, text.Height + inset.VerticalThickness);
    }

    /// <summary>
    /// Draws the image (once loaded) or the text, centered, in <paramref name="content"/> (the area inside the padding and
    /// the stroke), clipped to <paramref name="clip"/> (the inside of the stroke).
    /// </summary>
    public void Paint(SKCanvas canvas, SKRect content, SKPath clip, Color? textColor)
    {
        if (content.Width <= 0 || content.Height <= 0)
            return;
        var save = canvas.Save();
        canvas.ClipPath(clip, antialias: true);
        if (_image?.Image is not null)
            _image.Paint(canvas, content, Aspect);
        else if (_displayText.Length > 0)
            DrawText(canvas, content, textColor ?? ResolveColors().Text);
        canvas.RestoreToCount(save);
    }

    private void DrawText(SKCanvas canvas, SKRect content, Color color)
    {
        var style = Style(new Size(content.Width, content.Height));
        if (style.FontSize <= 0)
            return;
        var paint = SkUiTextResources.TextPaint;
        paint.Color = SkUiShapePainter.ToSkColor(color);
        canvas.Translate(content.Left, content.Top);
        _layout.Draw(canvas, _displayText, style, default, content.Width, content.Height, TextAlignment.Center, TextAlignment.Center, paint);
    }

    /// <summary>The text's style: the set font size, or (negative) the look's size for a <paramref name="content"/> area, shrinking to fit.</summary>
    private SkUiTextStyle Style(Size content)
    {
        var auto = !MeasuresText;
        var size = auto ? SkUiLook.Current.GetAvatarFontSize(content) : SkUiFontScaling.ScaleFontSize(_fontSize, _fontAutoScalingEnabled);
        return new SkUiTextStyle(_typeface, size, LineBreakMode.NoWrap, null, -1, -1, _characterSpacing, SkUiTextDirection.Auto,
            SkUiTextRendering.Default, _fontAttributes, MinimumScale: auto ? AutoMinimumScale : 1);
    }

    /// <summary>
    /// What the badge is drawn with at <paramref name="position"/> of a <paramref name="width"/> × <paramref name="height"/>
    /// avatar whose outline's corner there has <paramref name="cornerRadius"/>: <paramref name="color"/> (<c>null</c>:
    /// transparent) and a ring of <paramref name="ringColor"/> (<c>null</c>: the scheme's background); <c>null</c> for an
    /// empty avatar.
    /// </summary>
    public static SkUiAvatarBadgePaint? BadgePaint(double width, double height, Color? color, SkUiAvatarBadgePosition position, double cornerRadius, Color? ringColor)
    {
        var look = SkUiLook.Current;
        var avatar = new SKRect(0, 0, (float)width, (float)height);
        var bounds = look.GetAvatarBadgeBounds(avatar, position, cornerRadius);
        if (bounds.IsEmpty)
            return null;
        return new SkUiAvatarBadgePaint(avatar, position, bounds, color is null ? SKColors.Transparent : SkUiShapePainter.ToSkColor(color),
            SkUiShapePainter.ToSkColor(ringColor ?? SkUiColors.DefaultBackground), look.GetAvatarBadgeRingWidth(bounds.Width));
    }

    /// <summary>The radius of <paramref name="radii"/> at the badge's corner.</summary>
    public static double CornerAt(CornerRadius radii, SkUiAvatarBadgePosition position) => position switch
    {
        SkUiAvatarBadgePosition.TopLeft => radii.TopLeft,
        SkUiAvatarBadgePosition.TopRight => radii.TopRight,
        SkUiAvatarBadgePosition.BottomLeft => radii.BottomLeft,
        _ => radii.BottomRight
    };

    /// <summary>Disposes the text's blobs (the node's retained pictures are gone).</summary>
    public void Release() => _layout.Release();

    /// <summary>
    /// The initials of <paramref name="name"/>: the first letter (or digit) of the first word, and with
    /// <see cref="SkUiAvatarInitials.FirstAndLastWord"/> of the last word too, in upper case for the current culture.
    /// Letters are whole graphemes (accents, surrogate pairs); words without a letter or digit (emoji, punctuation) are
    /// skipped. Empty with <see cref="SkUiAvatarInitials.None"/> or when there is no such word.
    /// </summary>
    internal static string Initials(string? name, SkUiAvatarInitials type)
    {
        if (type == SkUiAvatarInitials.None || string.IsNullOrWhiteSpace(name))
            return string.Empty;
        string? first = null;
        string? last = null;
        var words = 0;
        var index = 0;
        while (index < name.Length)
        {
            while (index < name.Length && char.IsWhiteSpace(name[index]))
                index++;
            var start = index;
            while (index < name.Length && !char.IsWhiteSpace(name[index]))
                index++;
            if (index > start && WordInitial(name, start, index) is { } initial)
            {
                first ??= initial;
                last = initial;
                words++;
            }
        }
        if (first is null)
            return string.Empty;
        var initials = type == SkUiAvatarInitials.FirstAndLastWord && words > 1 ? first + last : first;
        return initials.ToUpper(CultureInfo.CurrentCulture);
    }

    private static string? WordInitial(string name, int start, int end)
    {
        var index = start;
        while (index < end)
        {
            var length = StringInfo.GetNextTextElementLength(name.AsSpan(index, end - index));
            if (char.IsLetterOrDigit(name, index))
                return name.Substring(index, length);
            index += Math.Max(1, length);
        }
        return null;
    }
}
