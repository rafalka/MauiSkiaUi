using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn avatar with the Community Toolkit's <c>AvatarView</c> API, so its XAML ports by changing the prefix: the
/// <see cref="ImageSource"/> (aspect fill) once it has loaded, else the <see cref="Text"/> (<c>"?"</c> by default), inside a
/// border (<see cref="BorderColor"/> / <see cref="BorderWidth"/> / <see cref="SkUiBorder.CornerRadius"/>, or the
/// <c>Border</c> members the toolkit's view inherits: <see cref="SkUiBorder.StrokeShape"/>, <see cref="SkUiBorder.Stroke"/>,
/// <see cref="SkUiBorder.StrokeThickness"/>, <c>Background</c>, <see cref="SkUiContentView.Padding"/>). One drawn node, not
/// a border around an image and a label: the image comes from the shared loader and cache, decoded just large enough
/// for the avatar (a list of repeated faces decodes once), the text from the shared text engine, both clipped to the
/// inside of the stroke; a new <see cref="Text"/> or <see cref="ImageSource"/> in a recycled list row re-records only
/// this node.
/// </summary>
/// <remarks>
/// <para>
/// The toolkit's defaults: 48 × 48 (the look's <see cref="SkUiLook.DefaultAvatarSize"/>, as the intrinsic size, so
/// <c>WidthRequest</c> / <c>HeightRequest</c> from styles apply), centered, a white 1-wide border fully rounded (a circle
/// at any size until <see cref="SkUiBorder.CornerRadius"/> is set), padding 1. The <c>Border</c> members win over the
/// toolkit's names once set: <see cref="SkUiBorder.Stroke"/> over <see cref="BorderColor"/>,
/// <see cref="SkUiBorder.StrokeThickness"/> over <see cref="BorderWidth"/>.
/// </para>
/// <para>
/// Beyond the toolkit: initials made from <see cref="Text"/> (a person's name; <see cref="Initials"/>), colors from the
/// look picked by the text (<see cref="AvatarColorMode"/>; also the default background, which the toolkit leaves
/// transparent), text that sizes itself to the avatar while <see cref="FontSize"/> is not set, <see cref="Aspect"/>, and a
/// status badge (<see cref="BadgeColor"/>). Screen readers read an image described by the whole <see cref="Text"/>, unless
/// semantic properties are set.
/// </para>
/// </remarks>
public class SkUiAvatarView : SkUiBorder
{
    private static readonly Thickness DefaultPadding = new(1);
    // Shared by every avatar with the default border color; never changed.
    private static readonly SolidPaint DefaultBorderPaint = new(Colors.White);

    private readonly SkUiAvatarContent _avatar;
    private ImageSource? _imageSource;
    private SkUiWeakListener<SkUiAvatarView>? _imageSourceListener;
    private Color? _textColor;
    private SolidPaint _borderPaint = DefaultBorderPaint;
    private double _borderWidth = 1;
    private Color? _badgeColor;
    private SkUiAvatarBadgePosition _badgePosition;
    private Color? _badgeBorderColor;
    private Action<SKCanvas, SkUiAvatarBadgePaint>? _badgePainter;

    /// <summary>Bindable <see cref="Text"/>.</summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(SkUiAvatarView), SkUiAvatarContent.DefaultText,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnTextChanged((string?)value));
    /// <summary>Bindable <see cref="ImageSource"/>.</summary>
    public static readonly BindableProperty ImageSourceProperty = BindableProperty.Create(nameof(ImageSource), typeof(ImageSource), typeof(SkUiAvatarView), null,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnImageSourceChanged((ImageSource?)value));
    /// <summary>Bindable <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SkUiAvatarView), null,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnTextColorChanged((Color?)value));
    /// <summary>Bindable <see cref="TextTransform"/>.</summary>
    public static readonly BindableProperty TextTransformProperty = BindableProperty.Create(nameof(TextTransform), typeof(TextTransform), typeof(SkUiAvatarView), TextTransform.Default,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnTextTransformChanged((TextTransform)value));
    /// <summary>Bindable <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(SkUiAvatarView), null,
        propertyChanged: (view, _, _) => ((SkUiAvatarView)view).OnFontChanged());
    /// <summary>Bindable <see cref="FontAttributes"/>.</summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(SkUiAvatarView), FontAttributes.None,
        propertyChanged: (view, _, _) => ((SkUiAvatarView)view).OnFontChanged());
    /// <summary>Bindable <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkUiAvatarView), -1d,
        validateValue: (_, value) => value is double size && double.IsFinite(size) && size is < 0 or > 0,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnFontSizeChanged((double)value));
    /// <summary>Bindable <see cref="FontAutoScalingEnabled"/>.</summary>
    public static readonly BindableProperty FontAutoScalingEnabledProperty = BindableProperty.Create(nameof(FontAutoScalingEnabled), typeof(bool), typeof(SkUiAvatarView), true,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnFontAutoScalingEnabledChanged((bool)value));
    /// <summary>Bindable <see cref="CharacterSpacing"/>.</summary>
    public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create(nameof(CharacterSpacing), typeof(double), typeof(SkUiAvatarView), 0d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnCharacterSpacingChanged((double)value));
    /// <summary>Bindable <see cref="BorderColor"/>.</summary>
    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(SkUiAvatarView), Colors.White,
        validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnBorderColorChanged((Color)value));
    /// <summary>Bindable <see cref="BorderWidth"/>.</summary>
    public static readonly BindableProperty BorderWidthProperty = BindableProperty.Create(nameof(BorderWidth), typeof(double), typeof(SkUiAvatarView), 1d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnBorderWidthChanged((double)value));
    /// <summary>Bindable <see cref="Initials"/>.</summary>
    public static readonly BindableProperty InitialsProperty = BindableProperty.Create(nameof(Initials), typeof(SkUiAvatarInitials), typeof(SkUiAvatarView), SkUiAvatarInitials.None,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnInitialsChanged((SkUiAvatarInitials)value));
    /// <summary>Bindable <see cref="AvatarColorMode"/>.</summary>
    public static readonly BindableProperty AvatarColorModeProperty = BindableProperty.Create(nameof(AvatarColorMode), typeof(SkUiAvatarColorMode), typeof(SkUiAvatarView), SkUiAvatarColorMode.Default,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnAvatarColorModeChanged((SkUiAvatarColorMode)value));
    /// <summary>Bindable <see cref="Aspect"/>.</summary>
    public static readonly BindableProperty AspectProperty = BindableProperty.Create(nameof(Aspect), typeof(Aspect), typeof(SkUiAvatarView), Aspect.AspectFill,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnAspectChanged((Aspect)value));
    /// <summary>Bindable <see cref="BadgeColor"/>.</summary>
    public static readonly BindableProperty BadgeColorProperty = BindableProperty.Create(nameof(BadgeColor), typeof(Color), typeof(SkUiAvatarView), null,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnBadgeColorChanged((Color?)value));
    /// <summary>Bindable <see cref="BadgePosition"/>.</summary>
    public static readonly BindableProperty BadgePositionProperty = BindableProperty.Create(nameof(BadgePosition), typeof(SkUiAvatarBadgePosition), typeof(SkUiAvatarView), SkUiAvatarBadgePosition.BottomRight,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnBadgePositionChanged((SkUiAvatarBadgePosition)value));
    /// <summary>Bindable <see cref="BadgeBorderColor"/>.</summary>
    public static readonly BindableProperty BadgeBorderColorProperty = BindableProperty.Create(nameof(BadgeBorderColor), typeof(Color), typeof(SkUiAvatarView), null,
        propertyChanged: (view, _, value) => ((SkUiAvatarView)view).OnBadgeBorderColorChanged((Color?)value));

    /// <summary>Creates an avatar showing <c>"?"</c>, centered, as the toolkit's.</summary>
    public SkUiAvatarView()
    {
        _avatar = new SkUiAvatarContent(this, InvalidatePaint);
        // As the toolkit's AvatarView (set by its constructor too): an avatar keeps its size inside stretching layouts.
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;
    }

    /// <summary>
    /// The text shown without an image (default <c>"?"</c>): as it is, or its initials (<see cref="Initials"/>) when it is a
    /// person's name. Screen readers read it whole, and <see cref="AvatarColorMode"/> picks the colors by it.
    /// </summary>
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    /// <summary>
    /// The image (files, <c>MauiImage</c> resources, <see cref="FontImageSource"/> glyphs, URIs, streams); shown once it has
    /// loaded, the text until then and when it fails.
    /// </summary>
    public ImageSource? ImageSource { get => (ImageSource?)GetValue(ImageSourceProperty); set => SetValue(ImageSourceProperty, value); }
    /// <summary>The text's color (<c>null</c>: the look's for <see cref="AvatarColorMode"/>).</summary>
    public Color? TextColor { get => (Color?)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
    /// <summary>Upper or lower case for <see cref="Text"/> (initials are always upper case).</summary>
    public TextTransform TextTransform { get => (TextTransform)GetValue(TextTransformProperty); set => SetValue(TextTransformProperty, value); }
    /// <summary>The text's font family (<c>null</c>: the default font).</summary>
    public string? FontFamily { get => (string?)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    /// <summary>Bold and italic text.</summary>
    public FontAttributes FontAttributes { get => (FontAttributes)GetValue(FontAttributesProperty); set => SetValue(FontAttributesProperty, value); }
    /// <summary>
    /// The text size in DIPs; negative (default -1, as the toolkit's): the look sizes it to the avatar
    /// (<see cref="SkUiLook.GetAvatarFontSize"/>) and text too wide for the avatar shrinks.
    /// </summary>
    [System.ComponentModel.TypeConverter(typeof(FontSizeConverter))]
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    /// <summary>Whether a set <see cref="FontSize"/> follows the system text size (<see cref="SkUiFontScaling"/>). Default <c>true</c>.</summary>
    public bool FontAutoScalingEnabled { get => (bool)GetValue(FontAutoScalingEnabledProperty); set => SetValue(FontAutoScalingEnabledProperty, value); }
    /// <summary>Space added after each character, in DIPs.</summary>
    public double CharacterSpacing { get => (double)GetValue(CharacterSpacingProperty); set => SetValue(CharacterSpacingProperty, value); }
    /// <summary>The border's color (default white) while <see cref="SkUiBorder.Stroke"/> is not set.</summary>
    public Color BorderColor { get => (Color)GetValue(BorderColorProperty); set => SetValue(BorderColorProperty, value); }
    /// <summary>The border's width in DIPs (default 1) while <see cref="SkUiBorder.StrokeThickness"/> is not set.</summary>
    public double BorderWidth { get => (double)GetValue(BorderWidthProperty); set => SetValue(BorderWidthProperty, value); }
    /// <summary>
    /// Whether <see cref="Text"/> shows as it is (<see cref="SkUiAvatarInitials.None"/>, default, the toolkit's) or as the
    /// initials of its first word or of its first and last words ("Ada Lovelace": "A", "AL"). A text without a letter or
    /// digit shows as it is.
    /// </summary>
    public SkUiAvatarInitials Initials { get => (SkUiAvatarInitials)GetValue(InitialsProperty); set => SetValue(InitialsProperty, value); }
    /// <summary>
    /// Where the background (while <c>Background</c> / <c>BackgroundColor</c> is not set) and the text color (while
    /// <see cref="TextColor"/> is not set) come from: the look's defaults, or a light or dark color picked by <see cref="Text"/>.
    /// </summary>
    public SkUiAvatarColorMode AvatarColorMode { get => (SkUiAvatarColorMode)GetValue(AvatarColorModeProperty); set => SetValue(AvatarColorModeProperty, value); }
    /// <summary>How the image fills the content area (default <see cref="Aspect.AspectFill"/>, the toolkit's).</summary>
    public Aspect Aspect { get => (Aspect)GetValue(AspectProperty); set => SetValue(AspectProperty, value); }
    /// <summary>
    /// A status dot over the border in this color (<c>null</c>, default: none), drawn by the look
    /// (<see cref="SkUiLook.DrawAvatarBadge"/>), or by <see cref="BadgePainter"/> / <see cref="OnPaintBadge"/>.
    /// </summary>
    public Color? BadgeColor { get => (Color?)GetValue(BadgeColorProperty); set => SetValue(BadgeColorProperty, value); }
    /// <summary>The corner the badge sits on (default <see cref="SkUiAvatarBadgePosition.BottomRight"/>).</summary>
    public SkUiAvatarBadgePosition BadgePosition { get => (SkUiAvatarBadgePosition)GetValue(BadgePositionProperty); set => SetValue(BadgePositionProperty, value); }
    /// <summary>The ring around the badge (<c>null</c>: the color scheme's background, so the badge stands out from the avatar).</summary>
    public Color? BadgeBorderColor { get => (Color?)GetValue(BadgeBorderColorProperty); set => SetValue(BadgeBorderColorProperty, value); }

    /// <summary>
    /// Draws this avatar's badge instead of the look (a count, an icon, a shape of its own), whenever it is set or
    /// <see cref="BadgeColor"/> is: over the border, in the avatar's local DIPs, with <see cref="SkUiAvatarBadgePaint"/>
    /// (the look's dot bounds at <see cref="BadgePosition"/>, the colors). It runs while the avatar records, on the UI
    /// thread: leave the canvas as it was, and call <see cref="SkUiView.InvalidatePaint"/> when what it draws changes.
    /// Subclasses can override <see cref="OnPaintBadge"/> instead.
    /// </summary>
    public Action<SKCanvas, SkUiAvatarBadgePaint>? BadgePainter { get => _badgePainter; set => SetBadgePainter(value); }

    /// <summary>Whether a loaded image is drawn instead of the text.</summary>
    public bool IsShowingImage => _avatar.ShowsImage;

    /// <summary>The load of the current <see cref="ImageSource"/> (completed when idle; tests).</summary>
    internal Task ImageLoadingTask => _avatar.LoadingTask;

    /// <summary>The shown image's cache entry (tests).</summary>
    internal SkUiCachedImage? CachedImage => _avatar.CachedImage;

    /// <summary>The image's decode bounds in DIPs (tests).</summary>
    internal Size ImageDecodeSize => _avatar.DecodeSize;

    /// <summary>The text drawn without an image: the initials of <see cref="Text"/>, else the text transformed (tests).</summary>
    internal string DisplayText => _avatar.DisplayText;

    /// <summary>Sets <see cref="Text"/> (same as the property setter).</summary>
    public SkUiAvatarView SetText(string value) { Text = value; return this; }
    /// <summary>Sets <see cref="ImageSource"/> (same as the property setter).</summary>
    public SkUiAvatarView SetImageSource(ImageSource? value) { ImageSource = value; return this; }
    /// <summary>Sets <see cref="TextColor"/> (same as the property setter).</summary>
    public SkUiAvatarView SetTextColor(Color? value) { TextColor = value; return this; }
    /// <summary>Sets <see cref="TextTransform"/> (same as the property setter).</summary>
    public SkUiAvatarView SetTextTransform(TextTransform value) { TextTransform = value; return this; }
    /// <summary>Sets the font family, size and attributes (same as the property setters; a negative size sizes the text to the avatar).</summary>
    public SkUiAvatarView SetFont(string? family = null, double size = -1, FontAttributes attributes = FontAttributes.None)
    {
        if (!double.IsFinite(size) || size == 0)
            throw new ArgumentOutOfRangeException(nameof(size), size, "The font size must be positive, or negative for a size from the avatar.");
        FontFamily = family;
        FontSize = size;
        FontAttributes = attributes;
        return this;
    }
    /// <summary>Sets <see cref="FontAutoScalingEnabled"/> (same as the property setter).</summary>
    public SkUiAvatarView SetFontAutoScalingEnabled(bool value) { FontAutoScalingEnabled = value; return this; }
    /// <summary>Sets <see cref="CharacterSpacing"/> (same as the property setter).</summary>
    public SkUiAvatarView SetCharacterSpacing(double value) { SkUiValidate.ThrowIfNotFinite(value, nameof(value)); CharacterSpacing = value; return this; }
    /// <summary>Sets <see cref="BorderColor"/> (same as the property setter).</summary>
    public SkUiAvatarView SetBorderColor(Color value) { ArgumentNullException.ThrowIfNull(value); BorderColor = value; return this; }
    /// <summary>Sets <see cref="BorderWidth"/> (same as the property setter).</summary>
    public SkUiAvatarView SetBorderWidth(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); BorderWidth = value; return this; }
    /// <summary>Sets <see cref="Initials"/> (same as the property setter).</summary>
    public SkUiAvatarView SetInitials(SkUiAvatarInitials value) { Initials = value; return this; }
    /// <summary>Sets <see cref="AvatarColorMode"/> (same as the property setter).</summary>
    public SkUiAvatarView SetAvatarColorMode(SkUiAvatarColorMode value) { AvatarColorMode = value; return this; }
    /// <summary>Sets <see cref="Aspect"/> (same as the property setter).</summary>
    public SkUiAvatarView SetAspect(Aspect value) { Aspect = value; return this; }
    /// <summary>Sets <see cref="BadgePainter"/> (<c>null</c>: the look draws the badge) and repaints.</summary>
    public SkUiAvatarView SetBadgePainter(Action<SKCanvas, SkUiAvatarBadgePaint>? value)
    {
        if (ReferenceEquals(_badgePainter, value)) return this;
        _badgePainter = value;
        InvalidatePaint();
        return this;
    }
    /// <summary>Sets the badge (same as the property setters; a <c>null</c> color hides it).</summary>
    public SkUiAvatarView SetBadge(Color? color, SkUiAvatarBadgePosition position = SkUiAvatarBadgePosition.BottomRight, Color? borderColor = null)
    {
        BadgeColor = color;
        BadgePosition = position;
        BadgeBorderColor = borderColor;
        return this;
    }

    private void OnTextChanged(string? value)
    {
        if (_avatar.SetText(value))
            InvalidateText();
        else
            InvalidatePaint(); // the same initials of another name: the colors follow the whole text
        InvalidateSemantics();
    }

    private void OnInitialsChanged(SkUiAvatarInitials value) { if (_avatar.SetInitials(value)) InvalidateText(); }
    private void OnTextTransformChanged(TextTransform value) { if (_avatar.SetTextTransform(value)) InvalidateText(); }
    private void OnFontChanged() { if (_avatar.SetFont(FontFamily, FontAttributes)) InvalidateText(); }
    private void OnFontSizeChanged(double value) { if (_avatar.SetFontSize(value)) InvalidateMeasureOverride(); }
    private void OnFontAutoScalingEnabledChanged(bool value) { if (_avatar.SetFontAutoScalingEnabled(value)) InvalidateText(); }
    private void OnCharacterSpacingChanged(double value) { if (_avatar.SetCharacterSpacing(value)) InvalidateText(); }
    private void OnTextColorChanged(Color? value) { _textColor = value; InvalidatePaint(); }
    private void OnAvatarColorModeChanged(SkUiAvatarColorMode value) { _avatar.ColorMode = value; InvalidatePaint(); }
    private void OnAspectChanged(Aspect value) { _avatar.Aspect = value; InvalidatePaint(); }
    private void OnBadgeColorChanged(Color? value) { _badgeColor = value; InvalidatePaint(); }
    private void OnBadgePositionChanged(SkUiAvatarBadgePosition value) { _badgePosition = value; InvalidatePaint(); }
    private void OnBadgeBorderColorChanged(Color? value) { _badgeBorderColor = value; InvalidatePaint(); }

    /// <summary>The shown text changed: only the intrinsic size of text in a set font size depends on it.</summary>
    private void InvalidateText()
    {
        if (_avatar.MeasuresText)
            InvalidateMeasureOverride();
        else
            InvalidatePaint();
    }

    private void OnBorderColorChanged(Color value)
    {
        _borderPaint = new SolidPaint(value);
        InvalidatePaint();
    }

    private void OnBorderWidthChanged(double value)
    {
        _borderWidth = value;
        if (IsSet(StrokeThicknessProperty)) return;
        InvalidateMeasureOverride(); // the content is inset by the border
        InvalidateShape();
    }

    private void OnImageSourceChanged(ImageSource? value)
    {
        if (ReferenceEquals(_imageSource, value)) return;
        // Bindings inside the source (a font glyph, a URI) resolve against the avatar, as in MAUI.
        AdoptImageSource(_imageSource, value);
        _imageSource = value;
        (_imageSourceListener ??= new(this, static (avatar, change) =>
        {
            if (SkUiMauiImageSources.AffectsImage(change.PropertyName))
                avatar._avatar.Reload();
        })).Listen(value);
        _avatar.SetSource(SkUiMauiImageSources.Convert(value), ExpectedContentSize());
        InvalidatePaint();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        InheritBindingContext(_imageSource);
    }

    // The Border members replace the toolkit's defaults once set (also by a style), and the defaults return when cleared.

    /// <inheritdoc />
    /// <remarks>
    /// Set to a value equal to the old one (<c>CornerRadius = 0</c> for a square avatar), MAUI raises no change, but the
    /// default gives way all the same; cleared, it comes back: redraw either way.
    /// </remarks>
    private protected override void OnSetOrCleared(BindableProperty property)
    {
        if (ReferenceEquals(property, CornerRadiusProperty))
        {
            InvalidateShape();
        }
        else if (ReferenceEquals(property, PaddingProperty) || ReferenceEquals(property, StrokeThicknessProperty))
        {
            InvalidateMeasureOverride(); // the content is inset by the padding and the border
            InvalidateShape();
        }
    }

    /// <inheritdoc />
    private protected override Paint? OutlinePaint => base.OutlinePaint ?? _borderPaint;

    /// <inheritdoc />
    private protected override double OutlineThickness => IsSet(StrokeThicknessProperty) ? base.OutlineThickness : _borderWidth;

    /// <inheritdoc />
    /// <remarks>Fully rounded (a circle for a square avatar) until <see cref="SkUiBorder.CornerRadius"/> is set.</remarks>
    private protected override CornerRadius OutlineCornerRadius(double width, double height) =>
        IsSet(CornerRadiusProperty) ? base.OutlineCornerRadius(width, height) : new CornerRadius(Math.Max(0, Math.Min(width, height) / 2));

    /// <inheritdoc />
    private protected override Thickness ContentInset => (IsSet(PaddingProperty) ? Padding : DefaultPadding) + OutlineThickness;

    /// <inheritdoc />
    /// <remarks>Without a background, the look's color for <see cref="AvatarColorMode"/>.</remarks>
    private protected override SkUiFill? BorderFill => ResolveBackgroundFill() ?? SkUiFill.From(_avatar.ResolveColors().Background);

    /// <summary>The content area of an avatar <paramref name="size"/> DIPs large: inside the padding and the border.</summary>
    private Size ContentSize(Size size)
    {
        var inset = ContentInset;
        return new Size(Math.Max(0, size.Width - inset.HorizontalThickness), Math.Max(0, size.Height - inset.VerticalThickness));
    }

    /// <summary>The content area the image is decoded for before the avatar is arranged: its size, else its requests, else the look's.</summary>
    private Size ExpectedContentSize()
    {
        if (Width > 0 && Height > 0)
            return ContentSize(new Size(Width, Height));
        var size = SkUiLook.Current.DefaultAvatarSize;
        return ContentSize(new Size(WidthRequest >= 0 ? WidthRequest : size.Width, HeightRequest >= 0 ? HeightRequest : size.Height));
    }

    /// <inheritdoc />
    /// <remarks>The text in a set <see cref="FontSize"/> plus the padding and border, else the look's avatar size; a <c>Content</c> view can make it larger.</remarks>
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
        var area = new SKRect((float)inset.Left, (float)inset.Top, (float)(Width - inset.Right), (float)(Height - inset.Bottom));
        _avatar.Paint(canvas, area, ContentClip(), _textColor);
    }

    /// <inheritdoc />
    private protected override void PaintOverOutline(SKCanvas canvas)
    {
        if (HasBadge && SkUiAvatarContent.BadgePaint(Width, Height, _badgeColor, _badgePosition,
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
        Microsoft.Maui.Controls.Shapes.Ellipse or SkUiEllipse => new CornerRadius(Math.Min(Width, Height) / 2),
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
