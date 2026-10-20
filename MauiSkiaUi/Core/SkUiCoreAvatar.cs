using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core avatar (the twin of <see cref="SkUiAvatarView"/>, the Community Toolkit's <c>AvatarView</c>): the
/// <see cref="ImageSource"/> (aspect fill) once it has loaded, else the <see cref="Text"/> (<c>"?"</c> by default) or its
/// initials (<see cref="Initials"/>), inside the border of <see cref="SkUiCoreBorder"/> and clipped to it, with an
/// optional status badge. One drawn node with the same pixels as <see cref="SkUiAvatarView"/>: the shared image loader
/// and cache (decoded just large enough), the shared text engine, colors from the look.
/// </summary>
/// <remarks>
/// The toolkit's defaults: 48 × 48 (the look's <see cref="SkUiLook.DefaultAvatarSize"/>, as the intrinsic size),
/// centered, a white 1-wide stroke fully rounded (a circle at any size until <see cref="SkUiCoreBorder.CornerRadius"/> is
/// set), padding 1, and the look's background for <see cref="AvatarColorMode"/> until a background is set.
/// </remarks>
public class SkUiCoreAvatar : SkUiCoreBorder, ISkUiFontElement
{
    private readonly SkUiAvatarContent _avatar;
    private SkUiImageSource? _imageSource;
    private string _text = SkUiAvatarContent.DefaultText;
    private SkUiAvatarInitials _initials;
    private TextTransform _textTransform = TextTransform.Default;
    private Color? _textColor;
    private string? _fontFamily;
    private FontAttributes _fontAttributes;
    private double _fontSize = -1;
    private bool _fontAutoScalingEnabled = true;
    private double _characterSpacing;
    private SkUiAvatarColorMode _avatarColorMode;
    private Aspect _aspect = Aspect.AspectFill;
    private Color? _badgeColor;
    private SkUiAvatarBadgePosition _badgePosition;
    private Color? _badgeBorderColor;
    private Action<SKCanvas, SkUiAvatarBadgePaint>? _badgePainter;

    /// <summary>Creates an avatar showing <c>"?"</c>, centered, with the toolkit's white 1-wide border and padding 1.</summary>
    public SkUiCoreAvatar()
    {
        _avatar = new SkUiAvatarContent(this, InvalidatePaint);
        SetStroke(Colors.White);
        base.SetPadding(new Thickness(1));
        SetHorizontalAlignment(LayoutAlignment.Center);
        SetVerticalAlignment(LayoutAlignment.Center);
    }

    /// <summary>The text shown without an image (default <c>"?"</c>): as it is, or its initials (see <see cref="SkUiAvatarView.Text"/>).</summary>
    public string Text { get => _text; set => SetText(value); }

    /// <summary>The image; shown once it has loaded, the text until then and when it fails.</summary>
    public SkUiImageSource? ImageSource { get => _imageSource; set => SetImageSource(value); }

    /// <summary>The text's color (<c>null</c>: the look's for <see cref="AvatarColorMode"/>).</summary>
    public Color? TextColor { get => _textColor; set => SetTextColor(value); }

    /// <summary>Upper or lower case for <see cref="Text"/> (initials are always upper case).</summary>
    public TextTransform TextTransform { get => _textTransform; set => SetTextTransform(value); }

    /// <summary>The text's font family (<c>null</c>: the default font).</summary>
    public string? FontFamily { get => _fontFamily; set => SetFont(value, _fontSize, _fontAttributes); }

    /// <summary>Bold and italic text.</summary>
    public FontAttributes FontAttributes { get => _fontAttributes; set => SetFont(_fontFamily, _fontSize, value); }

    /// <summary>The text size in DIPs; negative (default -1): the look sizes it to the avatar and text too wide shrinks.</summary>
    public double FontSize { get => _fontSize; set => SetFont(_fontFamily, value, _fontAttributes); }

    /// <summary>Whether a set <see cref="FontSize"/> follows the system text size (<see cref="SkUiFontScaling"/>). Default <c>true</c>.</summary>
    public bool FontAutoScalingEnabled { get => _fontAutoScalingEnabled; set => SetFontAutoScalingEnabled(value); }

    /// <summary>Space added after each character, in DIPs.</summary>
    public double CharacterSpacing { get => _characterSpacing; set => SetCharacterSpacing(value); }

    /// <summary>Whether <see cref="Text"/> shows as it is (default) or as its initials (see <see cref="SkUiAvatarView.Initials"/>).</summary>
    public SkUiAvatarInitials Initials { get => _initials; set => SetInitials(value); }

    /// <summary>Where the background and text colors come from while they are not set (see <see cref="SkUiAvatarView.AvatarColorMode"/>).</summary>
    public SkUiAvatarColorMode AvatarColorMode { get => _avatarColorMode; set => SetAvatarColorMode(value); }

    /// <summary>How the image fills the content area (default <see cref="Aspect.AspectFill"/>).</summary>
    public Aspect Aspect { get => _aspect; set => SetAspect(value); }

    /// <summary>A status dot over the border in this color (<c>null</c>, default: none).</summary>
    public Color? BadgeColor { get => _badgeColor; set => SetBadge(value, _badgePosition, _badgeBorderColor); }

    /// <summary>The corner the badge sits on (default <see cref="SkUiAvatarBadgePosition.BottomRight"/>).</summary>
    public SkUiAvatarBadgePosition BadgePosition { get => _badgePosition; set => SetBadge(_badgeColor, value, _badgeBorderColor); }

    /// <summary>The ring around the badge (<c>null</c>: the color scheme's background).</summary>
    public Color? BadgeBorderColor { get => _badgeBorderColor; set => SetBadge(_badgeColor, _badgePosition, value); }

    /// <summary>Draws this avatar's badge instead of the look (see <see cref="SkUiAvatarView.BadgePainter"/>).</summary>
    public Action<SKCanvas, SkUiAvatarBadgePaint>? BadgePainter { get => _badgePainter; set => SetBadgePainter(value); }

    /// <summary>Whether a loaded image is drawn instead of the text.</summary>
    public bool IsShowingImage => _avatar.ShowsImage;

    /// <summary>The load of the current <see cref="ImageSource"/> (completed when idle; tests).</summary>
    internal Task ImageLoadingTask => _avatar.LoadingTask;

    /// <summary>The shown image's cache entry (tests).</summary>
    internal SkUiCachedImage? CachedImage => _avatar.CachedImage;

    /// <summary>The text drawn without an image (tests).</summary>
    internal string DisplayText => _avatar.DisplayText;

    /// <summary>Sets the text shown without an image.</summary>
    public SkUiCoreAvatar SetText(string? value)
    {
        value ??= string.Empty;
        if (!SetProperty(ref _text, value, nameof(Text))) return this;
        if (_avatar.SetText(value))
            InvalidateText();
        else
            InvalidatePaint(); // the same initials of another name: the colors follow the whole text
        InvalidateSemantics();
        return this;
    }

    /// <summary>Loads <paramref name="value"/> (<c>null</c>: no image; an equal source already shown does not reload).</summary>
    public SkUiCoreAvatar SetImageSource(SkUiImageSource? value)
    {
        var changed = SetProperty(ref _imageSource, value, nameof(ImageSource));
        _avatar.SetSource(value, ExpectedContentSize());
        if (changed) InvalidatePaint();
        return this;
    }

    /// <summary>Sets the text's color (<c>null</c>: the look's).</summary>
    public SkUiCoreAvatar SetTextColor(Color? value)
    {
        if (SetProperty(ref _textColor, value, nameof(TextColor))) InvalidatePaint();
        return this;
    }

    /// <summary>Sets the case transform of <see cref="Text"/>.</summary>
    public SkUiCoreAvatar SetTextTransform(TextTransform value)
    {
        if (SetProperty(ref _textTransform, value, nameof(TextTransform)) && _avatar.SetTextTransform(value)) InvalidateText();
        return this;
    }

    /// <summary>Sets the font family, size (negative: sized to the avatar) and attributes.</summary>
    public SkUiCoreAvatar SetFont(string? family = null, double size = -1, FontAttributes attributes = FontAttributes.None)
    {
        if (!double.IsFinite(size) || size == 0)
            throw new ArgumentOutOfRangeException(nameof(size), size, "The font size must be positive, or negative for a size from the avatar.");
        SetProperty(ref _fontFamily, family, nameof(FontFamily));
        SetProperty(ref _fontAttributes, attributes, nameof(FontAttributes));
        SetProperty(ref _fontSize, size, nameof(FontSize));
        var fontChanged = _avatar.SetFont(family, attributes);
        if (_avatar.SetFontSize(size))
            InvalidateMeasure(); // between a set size and one from the avatar
        else if (fontChanged)
            InvalidateText();
        return this;
    }

    /// <summary>Sets whether a set font size follows the system text size.</summary>
    public SkUiCoreAvatar SetFontAutoScalingEnabled(bool value)
    {
        if (SetProperty(ref _fontAutoScalingEnabled, value, nameof(FontAutoScalingEnabled)) && _avatar.SetFontAutoScalingEnabled(value)) InvalidateText();
        return this;
    }

    /// <summary>Sets the space added after each character, in DIPs.</summary>
    public SkUiCoreAvatar SetCharacterSpacing(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        if (SetProperty(ref _characterSpacing, value, nameof(CharacterSpacing)) && _avatar.SetCharacterSpacing(value)) InvalidateText();
        return this;
    }

    /// <summary>Sets whether <see cref="Text"/> shows as it is or as its initials.</summary>
    public SkUiCoreAvatar SetInitials(SkUiAvatarInitials value)
    {
        if (SetProperty(ref _initials, value, nameof(Initials)) && _avatar.SetInitials(value)) InvalidateText();
        return this;
    }

    /// <summary>Sets where the colors come from while they are not set.</summary>
    public SkUiCoreAvatar SetAvatarColorMode(SkUiAvatarColorMode value)
    {
        if (!SetProperty(ref _avatarColorMode, value, nameof(AvatarColorMode))) return this;
        _avatar.ColorMode = value;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets how the image fills the content area.</summary>
    public SkUiCoreAvatar SetAspect(Aspect value)
    {
        if (!SetProperty(ref _aspect, value, nameof(Aspect))) return this;
        _avatar.Aspect = value;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="BadgePainter"/> (<c>null</c>: the look draws the badge) and repaints.</summary>
    public SkUiCoreAvatar SetBadgePainter(Action<SKCanvas, SkUiAvatarBadgePaint>? value)
    {
        if (SetProperty(ref _badgePainter, value, nameof(BadgePainter))) InvalidatePaint();
        return this;
    }

    /// <summary>Sets the badge (a <c>null</c> color hides it).</summary>
    public SkUiCoreAvatar SetBadge(Color? color, SkUiAvatarBadgePosition position = SkUiAvatarBadgePosition.BottomRight, Color? borderColor = null)
    {
        var changed = SetProperty(ref _badgeColor, color, nameof(BadgeColor));
        changed |= SetProperty(ref _badgePosition, position, nameof(BadgePosition));
        changed |= SetProperty(ref _badgeBorderColor, borderColor, nameof(BadgeBorderColor));
        if (changed) InvalidatePaint();
        return this;
    }

    /// <inheritdoc cref="SkUiCoreBorder.SetStroke(Color)" />
    public new SkUiCoreAvatar SetStroke(Color value)
    {
        base.SetStroke(value);
        return this;
    }

    /// <inheritdoc cref="SkUiCoreBorder.SetStrokeThickness" />
    public new SkUiCoreAvatar SetStrokeThickness(double value)
    {
        base.SetStrokeThickness(value);
        return this;
    }

    /// <inheritdoc cref="SkUiCoreBorder.SetCornerRadius(double)" />
    public new SkUiCoreAvatar SetCornerRadius(double uniformRadius)
    {
        base.SetCornerRadius(uniformRadius);
        return this;
    }

    /// <inheritdoc cref="SkUiCoreBorder.SetCornerRadius(CornerRadius)" />
    public new SkUiCoreAvatar SetCornerRadius(CornerRadius value)
    {
        base.SetCornerRadius(value);
        return this;
    }

    /// <inheritdoc cref="SkUiCoreBorder.SetPadding" />
    public new SkUiCoreAvatar SetPadding(Thickness value)
    {
        base.SetPadding(value);
        return this;
    }

    /// <inheritdoc cref="SkUiCoreBorder.SetBackgroundColor" />
    public new SkUiCoreAvatar SetBackgroundColor(Color value)
    {
        base.SetBackgroundColor(value);
        return this;
    }

    /// <summary>The shown text changed: only the intrinsic size of text in a set font size depends on it.</summary>
    private void InvalidateText()
    {
        if (_avatar.MeasuresText)
            InvalidateMeasure();
        else
            InvalidatePaint();
    }

    /// <inheritdoc />
    /// <remarks>Fully rounded (a circle for a square avatar) until <see cref="SkUiCoreBorder.CornerRadius"/> is set.</remarks>
    private protected override CornerRadius OutlineCornerRadius(double width, double height) =>
        IsCornerRadiusSet ? base.OutlineCornerRadius(width, height) : new CornerRadius(Math.Max(0, Math.Min(width, height) / 2));

    /// <inheritdoc />
    /// <remarks>Without a background, the look's color for <see cref="AvatarColorMode"/>.</remarks>
    private protected override SkUiFill BorderFill =>
        Background is not null || IsBackgroundColorSet ? base.BorderFill : SkUiFill.From(_avatar.ResolveColors().Background);

    private Size ContentSize(Size size)
    {
        var inset = ContentInset;
        return new Size(Math.Max(0, size.Width - inset.HorizontalThickness), Math.Max(0, size.Height - inset.VerticalThickness));
    }

    /// <summary>The content area the image is decoded for before the avatar is arranged: its size, else its requests, else the look's.</summary>
    private Size ExpectedContentSize()
    {
        if (Frame.Width > 0 && Frame.Height > 0)
            return ContentSize(Frame.Size);
        var size = SkUiLook.Current.DefaultAvatarSize;
        return ContentSize(new Size(Width >= 0 ? Width : size.Width, Height >= 0 ? Height : size.Height));
    }

    /// <inheritdoc />
    /// <remarks>The text in a set <see cref="FontSize"/> plus the padding and stroke, else the look's avatar size; content can make it larger.</remarks>
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var own = _avatar.Measure(ContentInset);
        var content = base.MeasureContent(widthConstraint, heightConstraint);
        return new Size(Math.Max(own.Width, content.Width), Math.Max(own.Height, content.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        base.ArrangeContent(size);
        _avatar.OnContentSizeChanged(ContentSize(size));
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var inset = ContentInset;
        var area = new SKRect((float)inset.Left, (float)inset.Top, (float)(Frame.Width - inset.Right), (float)(Frame.Height - inset.Bottom));
        _avatar.Paint(canvas, area, ContentClip(), _textColor);
    }

    /// <inheritdoc />
    private protected override void PaintOverOutline(SKCanvas canvas)
    {
        if (HasBadge && SkUiAvatarContent.BadgePaint(Frame.Width, Frame.Height, _badgeColor, _badgePosition,
                SkUiAvatarContent.CornerAt(BadgeCornerRadii(), _badgePosition), _badgeBorderColor) is { } paint)
            OnPaintBadge(canvas, paint);
    }

    /// <summary>Whether a badge is drawn: <see cref="BadgeColor"/> or <see cref="BadgePainter"/> is set. Subclasses that draw one of their own return <c>true</c>.</summary>
    protected virtual bool HasBadge => _badgeColor is not null || _badgePainter is not null;

    /// <summary>
    /// Draws the badge over the border (when <see cref="HasBadge"/>): <see cref="BadgePainter"/>, else the look's
    /// (<see cref="SkUiLook.DrawAvatarBadge"/>). Override to draw a badge of your own; leave the canvas as it was.
    /// </summary>
    protected virtual void OnPaintBadge(SKCanvas canvas, SkUiAvatarBadgePaint paint)
    {
        if (_badgePainter is { } painter)
            painter(canvas, paint);
        else
            SkUiLook.Current.DrawAvatarBadge(canvas, paint);
    }

    /// <summary>The outline's radii the badge sits on: the rounded rectangle's, a circle's for an ellipse, else square corners.</summary>
    private CornerRadius BadgeCornerRadii() => StrokeShape switch
    {
        SkUiCoreEllipse => new CornerRadius(Math.Min(Frame.Width, Frame.Height) / 2),
        _ => PressEffectCornerRadii
    };

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        _avatar.Release();
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.Image;
        info.Text = _avatar.SemanticsText;
    }
}
