using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Control look: default Skia geometry, painters, and intrinsic sizes for stock controls (FR-18).
/// Replace <see cref="Current"/> for a full pack, subclass and override virtuals, and/or set
/// per-control painter / measure delegates. This is <b>look</b>, not color scheme (FR-19) and not
/// MAUI <c>Style</c>/VSM (FR-12).
/// </summary>
/// <remarks>
/// Entry points such as <see cref="DrawSwitch"/> honor an optional delegate first, then the virtual
/// implementation. After swapping <see cref="Current"/> or changing sizes, invalidate measure and paint.
/// State changes animate: controls draw every frame of a transition through the same entry points, with the progress
/// in the paint struct (<see cref="SkUiToggleVisual"/>, <see cref="SkUiPressVisual"/>), and <see cref="GetTransition"/>
/// sets each transition's duration and easing. Painters run on the UI thread.
/// </remarks>
public class SkUiLook
{
    #region Current

    private static SkUiLook? _current;
    [ThreadStatic] private static SkUiLook? _scoped;
    private static volatile bool _anyTreeLook;

    /// <summary>The look of the subtree being measured or recorded on this thread (see <see cref="SkUiLookScope"/>).</summary>
    internal static SkUiLook? Scoped { get => _scoped; set => _scoped = value; }

    /// <summary>Whether any node set its own look so far.</summary>
    internal static bool AnyTreeLook => _anyTreeLook;

    /// <summary>Records that a node set its own look, so measuring and recording look for it.</summary>
    internal static void NoteTreeLook()
    {
        _anyTreeLook = true;
        TreeChanged();
    }

    /// <summary>Bumped when a node's look or a drawn parent changes once a tree look exists: nodes resolve their look again.</summary>
    internal static int TreeLookVersion { get; private set; }

    /// <summary>A look or the tree changed: forget the looks nodes resolved (UI thread; nothing to do without tree looks).</summary>
    internal static void TreeChanged()
    {
        if (_anyTreeLook)
            TreeLookVersion++;
    }

    /// <summary>The app-wide look, ignoring the scope of the subtree being measured or recorded.</summary>
    internal static SkUiLook App => _current ??= DefaultSkUiLook.Instance;

    /// <summary>
    /// Raised after <see cref="Current"/> is replaced, or by <see cref="NotifyChanged"/>. Live surfaces then re-measure and
    /// redraw their drawn trees.
    /// </summary>
    /// <remarks>Subscribers are not kept alive by the event (a page that forgets to unsubscribe can still be collected).</remarks>
    public static event EventHandler? CurrentChanged
    {
        add => _currentChanged.Add(value);
        remove => _currentChanged.Remove(value);
    }

    private static readonly SkUiWeakEvent _currentChanged = new();

    /// <summary>
    /// Call after changing the current look in place (sizes, painters, options such as
    /// <see cref="DefaultSkUiLook.PressEffect"/>): raises <see cref="CurrentChanged"/> so live surfaces re-measure and
    /// redraw with it. Replacing <see cref="Current"/> does this by itself.
    /// </summary>
    public static void NotifyChanged() => _currentChanged.Raise(null, EventArgs.Empty);

    /// <summary>
    /// Active app-wide look. Defaults to <see cref="DefaultSkUiLook.Instance"/>.
    /// </summary>
    public static SkUiLook Current
    {
        get => _scoped ?? (_current ??= DefaultSkUiLook.Instance);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_current, value)) return;
            _current = value;
            _currentChanged.Raise(null, EventArgs.Empty);
        }
    }

    #endregion

    #region Text

    private double _fontScale = 1;

    /// <summary>
    /// App-wide prescale of every drawn text size (labels, buttons, radio button text, spans, HTML, font images; both
    /// layers), whatever its <c>FontAutoScalingEnabled</c>: a font size is multiplied by it first, then the system text size
    /// applies to text that auto scales (<see cref="SkUiFontScaling"/>), so the drawn size is prescale × system scale.
    /// Default 1. Changing it on the current look re-measures and redraws live surfaces; font images take it when their
    /// source is created.
    /// </summary>
    public double FontScale
    {
        get => _fontScale;
        set
        {
            if (!double.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The font scale must be finite and positive.");
            if (_fontScale == value) return;
            _fontScale = value;
            if (ReferenceEquals(_current, this))
                NotifyChanged();
        }
    }

    /// <summary>
    /// The font size of labels, buttons, radio buttons, text fields, pickers and steppers that set none, in DIPs (default 16;
    /// the platform looks use their platform's body size). Read when text is laid out, so a subtree's own look
    /// (<see cref="SkUiView.Look"/>) applies; the controls' <c>FontSize</c> getters return the app look's.
    /// </summary>
    public virtual double DefaultFontSize => 16;

    /// <summary>
    /// Whether text adds the font's padding above its first line and below its last (the extents of its tallest glyphs
    /// instead of its ascent and descent), as Android's TextView does (default <c>false</c>; the Android looks: <c>true</c>).
    /// </summary>
    public virtual bool TextIncludesFontPadding => false;

    /// <summary>
    /// Whether text applies its font's own tracking (the <c>trak</c> table), as CoreText does on Apple platforms (default
    /// <c>false</c>; the iOS and Mac looks: <c>true</c>). SF at 17 pt is 0.43 pt tighter per character than its advances.
    /// </summary>
    public virtual bool AppliesFontTracking => false;

    /// <summary>
    /// The size the font's tracking is looked up at, relative to the font size (default 1). Mac Catalyst in the iPad idiom
    /// draws at 77% and CoreText tracks at the drawn size: 17 pt text tracks as 13 pt.
    /// </summary>
    public virtual double FontTrackingSizeScale => 1;

    /// <summary>
    /// Text at least this large (in points) tracks at its point size, not at <see cref="FontTrackingSizeScale"/> of it
    /// (default: no limit, the scale applies to all text; Mac Catalyst: 20, where Apple's system font turns to its display
    /// design and Catalyst tracks it as iOS does).
    /// </summary>
    public virtual double FontTrackingScaledBelow => double.PositiveInfinity;

    /// <summary>
    /// Whether fonts with an optical size axis (<c>opsz</c>) are drawn at the optical size of their drawn size, as CoreText
    /// does on Apple platforms (default <c>false</c>; the iOS and Mac looks: <c>true</c>). Apple's system font has one:
    /// above 17 pt it gets tighter, so 28 pt text is about 11% narrower than at the text design. The optical size is the point
    /// size, also on Mac Catalyst (unlike the tracking, <see cref="FontTrackingSizeScale"/>, which follows the drawn size).
    /// </summary>
    public virtual bool AppliesOpticalSize => false;

    /// <summary>A control's font size field: <paramref name="size"/>, or the look's <see cref="DefaultFontSize"/> when it is NaN (not set).</summary>
    internal static double FontSizeOrDefault(double size) => double.IsNaN(size) ? Current.DefaultFontSize : size;

    /// <summary>
    /// The font family of text that sets none (default <c>null</c>: the platform's system font, which is SF on Apple and Roboto
    /// on Android). Read when text is laid out; a family the device lacks falls back to the system font.
    /// </summary>
    public virtual string? DefaultFontFamily => null;

    #endregion

    #region Shared geometry

    /// <summary>Optional uniform rounded-box painter (single radius for all corners).</summary>
    public Action<SKCanvas, SKRect, float, SKColor, SKColor, float>? RoundedBoxPainter { get; set; }

    /// <summary>Optional per-corner rounded-box painter.</summary>
    public Action<SKCanvas, SKRect, CornerRadius, SKColor, SKColor, float>? RoundedBoxCornersPainter { get; set; }

    /// <summary>
    /// Custom rounded-rect geometry for fills, borders and clips (e.g. squircle corners), or <c>null</c> (default) for
    /// plain circular corners, which looks draw with Skia's round-rect primitives (no path). Override this to change
    /// the shape of every rounded control; <see cref="CreateRoundRectPath(SKRect, CornerRadius)"/> uses it.
    /// </summary>
    /// <remarks>Corner order matches MAUI <see cref="CornerRadius"/>: top-left, top-right, bottom-left, bottom-right.</remarks>
    public virtual SKPath? CreateCustomRoundRectPath(SKRect bounds, CornerRadius radii) => null;

    /// <summary>Rounded-rect path with a uniform corner radius for fill/border/clip (see <see cref="CreateCustomRoundRectPath"/>).</summary>
    public SKPath CreateRoundRectPath(SKRect bounds, float radius) =>
        CreateRoundRectPath(bounds, new CornerRadius(radius));

    /// <summary>
    /// Rounded-rect path with independent corner radii for fill/border/clip: the look's
    /// <see cref="CreateCustomRoundRectPath"/> geometry, else plain circular corners.
    /// </summary>
    /// <remarks>
    /// Corner order matches MAUI <see cref="CornerRadius"/>: top-left, top-right, bottom-left, bottom-right.
    /// Skia’s rect-radii order is TL, TR, BR, BL; this method remaps accordingly.
    /// </remarks>
    public SKPath CreateRoundRectPath(SKRect bounds, CornerRadius radii)
    {
        if (CreateCustomRoundRectPath(bounds, radii) is { } custom)
            return custom;
        using var roundRect = new SKRoundRect();
        SetRadii(roundRect, bounds, radii);
        using var builder = new SKPathBuilder();
        // Start where MAUI's rectangle paths start, clockwise: on the left edge below the top-left corner (at the corner
        // when it is square), so dashed borders lay out their pattern as MAUI's.
        builder.AddRoundRect(roundRect, SKPathDirection.Clockwise, 7);
        return builder.Detach();
    }

    /// <summary>Sets <paramref name="roundRect"/> to <paramref name="bounds"/> with MAUI's corner order (Skia's is TL, TR, BR, BL).</summary>
    private static void SetRadii(SKRoundRect roundRect, SKRect bounds, CornerRadius radii)
    {
        var tl = (float)Math.Max(0, radii.TopLeft);
        var tr = (float)Math.Max(0, radii.TopRight);
        var bl = (float)Math.Max(0, radii.BottomLeft);
        var br = (float)Math.Max(0, radii.BottomRight);
        roundRect.SetRectRadii(bounds, [
            new SKPoint(tl, tl),
            new SKPoint(tr, tr),
            new SKPoint(br, br),
            new SKPoint(bl, bl),
        ]);
    }

    /// <summary>Fills/strokes a rounded rectangle with a uniform corner radius.</summary>
    public void DrawRoundedBox(SKCanvas canvas, SKRect bounds, float radius, SKColor fill, SKColor border, float width)
    {
        if (RoundedBoxPainter is { } painter)
        {
            painter(canvas, bounds, radius, fill, border, width);
            return;
        }
        DrawRoundedBoxCore(canvas, bounds, radius, fill, border, width);
    }

    /// <summary>Fills/strokes a rounded rectangle with independent corner radii.</summary>
    public void DrawRoundedBox(SKCanvas canvas, SKRect bounds, CornerRadius radii, SKColor fill, SKColor border, float width)
    {
        if (RoundedBoxCornersPainter is { } painter)
        {
            painter(canvas, bounds, radii, fill, border, width);
            return;
        }
        if (IsUniformCornerRadius(radii))
        {
            if (RoundedBoxPainter is { } uniformPainter)
            {
                uniformPainter(canvas, bounds, (float)radii.TopLeft, fill, border, width);
                return;
            }
            DrawRoundedBoxCore(canvas, bounds, (float)radii.TopLeft, fill, border, width);
            return;
        }
        DrawRoundedBoxCore(canvas, bounds, radii, fill, border, width);
    }

    /// <summary>
    /// Fills a rounded rectangle with a MAUI Graphics paint (a solid color or a linear or radial gradient, mapped onto
    /// <paramref name="bounds"/>) and strokes its border. Solid fills (and <c>null</c>: none) take the color overload, so
    /// <see cref="RoundedBoxPainter"/> / <see cref="RoundedBoxCornersPainter"/> still draw them; gradients go to
    /// <see cref="DrawRoundedBoxCore(SKCanvas, SKRect, CornerRadius, Paint, SKColor, float)"/>.
    /// </summary>
    public void DrawRoundedBox(SKCanvas canvas, SKRect bounds, CornerRadius radii, Paint? fill, SKColor border, float width)
    {
        if (fill is not GradientPaint gradient)
        {
            DrawRoundedBox(canvas, bounds, radii, SkUiShapePainter.RepresentativeColor(fill), border, width);
            return;
        }
        DrawRoundedBoxCore(canvas, bounds, radii, gradient, border, width);
    }

    /// <summary>
    /// Default gradient rounded box: the gradient fills the look's rounded geometry (<see cref="CreateRoundRectPath(SKRect, CornerRadius)"/>),
    /// then the border is drawn as with a transparent fill.
    /// </summary>
    protected virtual void DrawRoundedBoxCore(SKCanvas canvas, SKRect bounds, CornerRadius radii, Paint fill, SKColor border, float width)
    {
        if (SkUiCornerRadii.HasAny(radii))
        {
            using var path = CreateRoundRectPath(bounds, radii);
            SkUiShapePainter.Fill(canvas, path, fill, bounds);
        }
        else
        {
            using var builder = new SKPathBuilder();
            builder.AddRect(bounds);
            using var path = builder.Detach();
            SkUiShapePainter.Fill(canvas, path, fill, bounds);
        }
        if (width > 0 && border.Alpha > 0)
            DrawRoundedBox(canvas, bounds, radii, SKColors.Transparent, border, width);
    }

    /// <summary>Default rounded-box geometry (uniform radius).</summary>
    /// <remarks>
    /// Base implementation forwards to the per-corner virtual so subclasses that only override
    /// <see cref="DrawRoundedBoxCore(SKCanvas, SKRect, CornerRadius, SKColor, SKColor, float)"/> still run.
    /// Prefer overriding this float hook when customizing uniformly rounded chrome.
    /// </remarks>
    protected virtual void DrawRoundedBoxCore(SKCanvas canvas, SKRect bounds, float radius, SKColor fill, SKColor border, float width) =>
        DrawRoundedBoxCore(canvas, bounds, new CornerRadius(radius), fill, border, width);

    /// <summary>Default rounded-box geometry (per-corner radii).</summary>
    protected virtual void DrawRoundedBoxCore(SKCanvas canvas, SKRect bounds, CornerRadius radii, SKColor fill, SKColor border, float width) { }

    /// <summary>True when all four corner radii are equal.</summary>
    protected static bool IsUniformCornerRadius(CornerRadius radii) =>
        radii.TopLeft == radii.TopRight
        && radii.TopLeft == radii.BottomLeft
        && radii.TopLeft == radii.BottomRight;

    /// <summary>Reduces each corner radius by <paramref name="inset"/> (clamped at zero), for stroked inset paths.</summary>
    protected static CornerRadius ShrinkCornerRadius(CornerRadius radii, float inset) =>
        new(
            Math.Max(0, radii.TopLeft - inset),
            Math.Max(0, radii.TopRight - inset),
            Math.Max(0, radii.BottomLeft - inset),
            Math.Max(0, radii.BottomRight - inset));

    /// <summary>Blends two colors (straight alpha): 0 gives <paramref name="from"/>, 1 <paramref name="to"/>.</summary>
    protected static SKColor Mix(SKColor from, SKColor to, float amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        if (amount <= 0) return from;
        if (amount >= 1) return to;
        static byte Channel(byte a, byte b, float t) => (byte)Math.Round(a + (b - a) * t);
        return new SKColor(Channel(from.Red, to.Red, amount), Channel(from.Green, to.Green, amount),
            Channel(from.Blue, to.Blue, amount), Channel(from.Alpha, to.Alpha, amount));
    }

    /// <summary>
    /// Fills or strokes (as <paramref name="paint"/> is set up) a rectangle with <paramref name="radii"/>: a plain rounded rectangle
    /// for uniform radii (no allocation), the look's path (<see cref="CreateRoundRectPath(SKRect, CornerRadius)"/>) otherwise.
    /// </summary>
    private protected void DrawRoundRect(SKCanvas canvas, SKRect bounds, CornerRadius radii, SKPaint paint)
    {
        if (IsUniformCornerRadius(radii))
        {
            var radius = (float)Math.Max(0, radii.TopLeft);
            canvas.DrawRoundRect(bounds, radius, radius, paint);
            return;
        }
        if (CreateCustomRoundRectPath(bounds, radii) is { } custom)
        {
            using (custom)
                canvas.DrawPath(custom, paint);
            return;
        }
        using var shape = new SKRoundRect();
        SetRadii(shape, bounds, radii);
        canvas.DrawRoundRect(shape, paint);
    }

    /// <summary>
    /// Strokes a ring <paramref name="width"/> DIPs wide just inside <paramref name="bounds"/> (its outer edge on the bounds), the
    /// radii shrunk by half the width so it follows the shape <paramref name="radii"/> fill.
    /// </summary>
    private protected void DrawInnerRing(SKCanvas canvas, SKRect bounds, CornerRadius radii, float width, SKPaint paint)
    {
        if (width <= 0)
            return;
        bounds.Inflate(-width / 2, -width / 2);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = width;
        DrawRoundRect(canvas, bounds, ShrinkCornerRadius(radii, width / 2), paint);
    }

    #endregion

    #region Button

    /// <summary>Default button minimum height in DIPs.</summary>
    public virtual double DefaultButtonMinimumHeight => 44;

    /// <summary>Default button corner radius in DIPs.</summary>
    public virtual double DefaultButtonCornerRadius => 6;

    /// <summary>The padding of buttons that set none, DIPs (default 18 × 12).</summary>
    public virtual Thickness DefaultButtonPadding => new(18, 12);

    /// <summary>
    /// The border width of buttons that set none, drawn in the color scheme's <see cref="SkUiColorScheme.ButtonBorder"/>, DIPs
    /// (default 0; Windows: Fluent's 1 epx). Read when the button is created; it does not change the button's size.
    /// </summary>
    public virtual double DefaultButtonBorderWidth => 0;

    /// <summary>
    /// The opacity of a disabled button's text and image when the button draws no fill (a text button: a transparent fill or
    /// background, and no <c>DisabledFillColor</c>), 0–1 (default 0.38, Material's disabled content). Buttons with a fill show
    /// they are disabled by it: the scheme's default fill turns into <see cref="SkUiColorScheme.Disabled"/>.
    /// </summary>
    public virtual double DisabledButtonContentOpacity => 0.38;

    /// <summary>
    /// The opacity a view or node with <c>ShowsPressEffect</c> and <see cref="SkUiPressFeedback.Fade"/> fades to while pressed,
    /// 0–1 (default 0.4), with the look's press and release transitions.
    /// </summary>
    public virtual double PressedContentOpacity => 0.4;

    /// <summary>Optional Button painter (fill, border and press feedback behind the text); replaces <see cref="DrawButtonCore"/>.</summary>
    public Action<SKCanvas, SkUiButtonPaint>? ButtonPainter { get; set; }

    /// <summary>Draws a Button's fill, border and press feedback (delegate or <see cref="DrawButtonCore"/>); the text follows.</summary>
    public void DrawButton(SKCanvas canvas, SkUiButtonPaint button)
    {
        if (ButtonPainter is { } painter)
        {
            painter(canvas, button);
            return;
        }
        DrawButtonCore(canvas, button);
    }

    /// <summary>
    /// Default Button chrome: the rounded fill (<see cref="SkUiButtonPaint.FillPaint"/> when set, else
    /// <see cref="SkUiButtonPaint.Fill"/>) and border.
    /// </summary>
    protected virtual void DrawButtonCore(SKCanvas canvas, SkUiButtonPaint button)
    {
        if (button.FillPaint is { } fill)
            DrawRoundedBox(canvas, button.Bounds, button.CornerRadii, fill, button.Border, button.BorderWidth);
        else
            DrawRoundedBox(canvas, button.Bounds, button.CornerRadii, button.Fill, button.Border, button.BorderWidth);
    }

    #endregion

    #region Transitions

    /// <summary>
    /// Optional transition override; when set, replaces <see cref="GetTransitionCore"/> (return
    /// <see cref="SkUiTransition.None"/> to turn a transition off).
    /// </summary>
    public Func<SkUiTransitionKind, SkUiTransition>? TransitionProvider { get; set; }

    /// <summary>
    /// How a state change of <paramref name="kind"/> animates (delegate or <see cref="GetTransitionCore"/>); always
    /// <see cref="SkUiTransition.None"/> while <see cref="SkUiMotion.IsMotionReduced"/>. Read when a transition starts.
    /// </summary>
    public SkUiTransition GetTransition(SkUiTransitionKind kind)
    {
        if (SkUiMotion.IsMotionReduced)
            return SkUiTransition.None;
        return TransitionProvider is { } provider ? provider(kind) : GetTransitionCore(kind);
    }

    /// <summary>Default transitions: none (<see cref="DefaultSkUiLook"/> animates).</summary>
    protected virtual SkUiTransition GetTransitionCore(SkUiTransitionKind kind) => SkUiTransition.None;

    #endregion

    #region Switch

    /// <summary>Default Switch intrinsic size in DIPs.</summary>
    public virtual Size DefaultSwitchSize => new(51, 31);

    /// <summary>Optional Switch intrinsic measure override.</summary>
    public Func<double, double, Size>? SwitchMeasure { get; set; }

    /// <summary>Optional Switch painter; when set, replaces <see cref="DrawSwitchCore"/>.</summary>
    public Action<SKCanvas, SkUiSwitchPaint>? SwitchPainter { get; set; }

    /// <summary>Intrinsic Switch size in DIPs.</summary>
    public Size MeasureSwitch(double widthConstraint, double heightConstraint) =>
        SwitchMeasure?.Invoke(widthConstraint, heightConstraint)
        ?? MeasureSwitchCore(widthConstraint, heightConstraint);

    /// <summary>Default Switch size from <see cref="DefaultSwitchSize"/>.</summary>
    protected virtual Size MeasureSwitchCore(double widthConstraint, double heightConstraint) => DefaultSwitchSize;

    /// <summary>Draws a Switch (delegate or <see cref="DrawSwitchCore"/>), every frame of its transitions.</summary>
    public void DrawSwitch(SKCanvas canvas, SkUiSwitchPaint toggle)
    {
        if (SwitchPainter is { } painter)
        {
            painter(canvas, toggle);
            return;
        }
        DrawSwitchCore(canvas, toggle);
    }

    /// <summary>Default Switch geometry.</summary>
    protected virtual void DrawSwitchCore(SKCanvas canvas, SkUiSwitchPaint toggle) { }

    #endregion

    #region CheckBox

    /// <summary>Default CheckBox intrinsic size in DIPs (square side length on both axes).</summary>
    public virtual Size DefaultCheckBoxSize => new(24, 24);

    /// <summary>
    /// The smallest width of a check box that sets no minimum width, DIPs; the box stays at the start (default 0; Windows: 120,
    /// WinUI's MinWidth). As natively, a <c>MinimumWidthRequest</c> (Core: <c>MinimumWidth</c>) replaces it.
    /// </summary>
    public virtual double DefaultCheckBoxMinimumWidth => 0;

    /// <summary>Optional CheckBox intrinsic measure override.</summary>
    public Func<double, double, Size>? CheckBoxMeasure { get; set; }

    /// <summary>Optional CheckBox painter; when set, replaces <see cref="DrawCheckBoxCore"/>.</summary>
    public Action<SKCanvas, SkUiCheckBoxPaint>? CheckBoxPainter { get; set; }

    /// <summary>Intrinsic CheckBox size in DIPs.</summary>
    public Size MeasureCheckBox(double widthConstraint, double heightConstraint) =>
        CheckBoxMeasure?.Invoke(widthConstraint, heightConstraint)
        ?? MeasureCheckBoxCore(widthConstraint, heightConstraint);

    /// <summary>A check box's size, widened to <see cref="DefaultCheckBoxMinimumWidth"/> unless it sets a minimum width of its own.</summary>
    internal Size MeasureCheckBox(double widthConstraint, double heightConstraint, bool hasMinimumWidth)
    {
        var size = MeasureCheckBox(widthConstraint, heightConstraint);
        return hasMinimumWidth ? size : new Size(Math.Max(size.Width, DefaultCheckBoxMinimumWidth), size.Height);
    }

    /// <summary>Default CheckBox size from <see cref="DefaultCheckBoxSize"/>.</summary>
    protected virtual Size MeasureCheckBoxCore(double widthConstraint, double heightConstraint) => DefaultCheckBoxSize;

    /// <summary>Draws a CheckBox (delegate or <see cref="DrawCheckBoxCore"/>), every frame of its transitions.</summary>
    public void DrawCheckBox(SKCanvas canvas, SkUiCheckBoxPaint box)
    {
        if (CheckBoxPainter is { } painter)
        {
            painter(canvas, box);
            return;
        }
        DrawCheckBoxCore(canvas, box);
    }

    /// <summary>Default CheckBox geometry.</summary>
    protected virtual void DrawCheckBoxCore(SKCanvas canvas, SkUiCheckBoxPaint box) { }

    #endregion

    #region RadioButton

    /// <summary>Default RadioButton intrinsic size in DIPs (square side length on both axes).</summary>
    public virtual Size DefaultRadioButtonSize => new(24, 24);

    /// <summary>
    /// Space in DIPs between a RadioButton's circle and its <c>Content</c> (text or a drawn view); 8 by default (MAUI's
    /// default template: 6 between a 21-DIP circle and the content).
    /// </summary>
    public virtual double DefaultRadioButtonContentSpacing => 8;

    /// <summary>
    /// Space a radio button keeps around its circle and content, added to its own <c>Padding</c>, DIPs (default 0; MAUI's
    /// radio button template on Apple: 8 × 7).
    /// </summary>
    public virtual Thickness RadioButtonPadding => default;

    /// <summary>
    /// The smallest width of a radio button (circle, content and padding) that sets no minimum width, DIPs (default 0; Windows:
    /// 120, WinUI's MinWidth). As natively, a <c>MinimumWidthRequest</c> replaces it.
    /// </summary>
    public virtual double DefaultRadioButtonMinimumWidth => 0;

    /// <summary>Optional RadioButton intrinsic measure override.</summary>
    public Func<double, double, Size>? RadioButtonMeasure { get; set; }

    /// <summary>Optional RadioButton painter; when set, replaces <see cref="DrawRadioButtonCore"/>.</summary>
    public Action<SKCanvas, SkUiRadioButtonPaint>? RadioButtonPainter { get; set; }

    /// <summary>Intrinsic RadioButton size in DIPs.</summary>
    public Size MeasureRadioButton(double widthConstraint, double heightConstraint) =>
        RadioButtonMeasure?.Invoke(widthConstraint, heightConstraint)
        ?? MeasureRadioButtonCore(widthConstraint, heightConstraint);

    /// <summary>Default RadioButton size from <see cref="DefaultRadioButtonSize"/>.</summary>
    protected virtual Size MeasureRadioButtonCore(double widthConstraint, double heightConstraint) => DefaultRadioButtonSize;

    /// <summary>Draws a RadioButton (delegate or <see cref="DrawRadioButtonCore"/>), every frame of its transitions.</summary>
    public void DrawRadioButton(SKCanvas canvas, SkUiRadioButtonPaint radio)
    {
        if (RadioButtonPainter is { } painter)
        {
            painter(canvas, radio);
            return;
        }
        DrawRadioButtonCore(canvas, radio);
    }

    /// <summary>Default RadioButton geometry.</summary>
    protected virtual void DrawRadioButtonCore(SKCanvas canvas, SkUiRadioButtonPaint radio) { }

    #endregion

    #region ActivityIndicator

    /// <summary>Default ActivityIndicator intrinsic size in DIPs.</summary>
    public virtual Size DefaultActivityIndicatorSize => new(36, 36);

    /// <summary>Optional ActivityIndicator intrinsic measure override.</summary>
    public Func<double, double, Size>? ActivityIndicatorMeasure { get; set; }

    /// <summary>Optional ActivityIndicator painter.</summary>
    public Action<SKCanvas, float, float, float, SKPaint>? ActivityIndicatorPainter { get; set; }

    /// <summary>Intrinsic ActivityIndicator size in DIPs.</summary>
    public Size MeasureActivityIndicator(double widthConstraint, double heightConstraint) =>
        ActivityIndicatorMeasure?.Invoke(widthConstraint, heightConstraint)
        ?? MeasureActivityIndicatorCore(widthConstraint, heightConstraint);

    /// <summary>Default ActivityIndicator size from <see cref="DefaultActivityIndicatorSize"/>.</summary>
    protected virtual Size MeasureActivityIndicatorCore(double widthConstraint, double heightConstraint) =>
        DefaultActivityIndicatorSize;

    /// <summary>
    /// Draws ActivityIndicator chrome. Controls record it once (<paramref name="sweepStart"/> = 0) and the
    /// compositor rotates it about the slot center on the render thread, so painters should draw centered,
    /// rotation-symmetric geometry (e.g. an arc of a centered circle).
    /// </summary>
    public void DrawActivityIndicator(SKCanvas canvas, float width, float height, float sweepStart, SKPaint paint)
    {
        if (ActivityIndicatorPainter is { } painter)
        {
            painter(canvas, width, height, sweepStart, paint);
            return;
        }
        DrawActivityIndicatorCore(canvas, width, height, sweepStart, paint);
    }

    /// <summary>Default ActivityIndicator geometry.</summary>
    protected virtual void DrawActivityIndicatorCore(SKCanvas canvas, float width, float height, float sweepStart, SKPaint paint) { }

    #endregion

    #region Slider

    /// <summary>Default Slider size across the track (height of a horizontal slider), in DIPs.</summary>
    public virtual double DefaultSliderThickness => 32;

    /// <summary>Default Slider length along the track when the layout offers unlimited space, in DIPs.</summary>
    public virtual double DefaultSliderLength => 200;

    /// <summary>
    /// Thumb radius in DIPs. The thumb's center travels from this inset to the length minus it, so input maps a
    /// touch to the value the thumb shows there.
    /// </summary>
    public virtual float SliderThumbRadius => 10;

    /// <summary>
    /// Where the thumb's center travels: from this inset to the length minus it, in DIPs (default
    /// <see cref="SliderThumbRadius"/>; a capsule thumb's half width, Android's track padding). Input maps a touch to the value
    /// the thumb shows there.
    /// </summary>
    public virtual float SliderThumbTravelInset => SliderThumbRadius;

    /// <summary>Optional Slider intrinsic measure override (width constraint, height constraint, orientation).</summary>
    public Func<double, double, StackOrientation, Size>? SliderMeasure { get; set; }

    /// <summary>Optional Slider painter; when set, replaces <see cref="DrawSliderCore"/>.</summary>
    public Action<SKCanvas, SkUiSliderPaint>? SliderPainter { get; set; }

    /// <summary>Intrinsic Slider size in DIPs.</summary>
    public Size MeasureSlider(double widthConstraint, double heightConstraint, StackOrientation orientation) =>
        SliderMeasure?.Invoke(widthConstraint, heightConstraint, orientation)
        ?? MeasureSliderCore(widthConstraint, heightConstraint, orientation);

    /// <summary>Default Slider size: the offered length (else <see cref="DefaultSliderLength"/>) by <see cref="DefaultSliderThickness"/>.</summary>
    protected virtual Size MeasureSliderCore(double widthConstraint, double heightConstraint, StackOrientation orientation)
    {
        static double Length(double constraint, double fallback) => double.IsFinite(constraint) ? constraint : fallback;
        return orientation == StackOrientation.Vertical
            ? new Size(DefaultSliderThickness, Length(heightConstraint, DefaultSliderLength))
            : new Size(Length(widthConstraint, DefaultSliderLength), DefaultSliderThickness);
    }

    /// <summary>
    /// Draws a Slider (delegate or <see cref="DrawSliderCore"/>). Always in horizontal, left-to-right coordinates:
    /// the control rotates the canvas for vertical sliders and mirrors it for right-to-left layouts.
    /// </summary>
    public void DrawSlider(SKCanvas canvas, SkUiSliderPaint slider)
    {
        if (SliderPainter is { } painter)
        {
            painter(canvas, slider);
            return;
        }
        DrawSliderCore(canvas, slider);
    }

    /// <summary>Default Slider geometry.</summary>
    protected virtual void DrawSliderCore(SKCanvas canvas, SkUiSliderPaint slider) { }

    #endregion

    #region ProgressBar

    /// <summary>Default ProgressBar height in DIPs.</summary>
    public virtual double DefaultProgressBarHeight => 4;

    /// <summary>Default ProgressBar width when the layout offers unlimited space, in DIPs.</summary>
    public virtual double DefaultProgressBarLength => 200;

    /// <summary>Length of the moving segment of an indeterminate ProgressBar, as a fraction of the bar.</summary>
    public virtual float IndeterminateProgressSegment => 0.35f;

    /// <summary>Seconds for the indeterminate segment to cross the bar once.</summary>
    public virtual float IndeterminateProgressPeriod => 1.5f;

    /// <summary>Corner radius of a ProgressBar of <paramref name="height"/> (default: fully rounded ends).</summary>
    public virtual float GetProgressBarCornerRadius(float height) => height / 2;

    /// <summary>Optional ProgressBar intrinsic measure override.</summary>
    public Func<double, double, Size>? ProgressBarMeasure { get; set; }

    /// <summary>Optional ProgressBar painter; when set, replaces <see cref="DrawProgressBarCore"/>.</summary>
    public Action<SKCanvas, SkUiProgressBarPaint>? ProgressBarPainter { get; set; }

    /// <summary>Intrinsic ProgressBar size in DIPs.</summary>
    public Size MeasureProgressBar(double widthConstraint, double heightConstraint) =>
        ProgressBarMeasure?.Invoke(widthConstraint, heightConstraint)
        ?? MeasureProgressBarCore(widthConstraint, heightConstraint);

    /// <summary>Default ProgressBar size: the offered width (else <see cref="DefaultProgressBarLength"/>) by <see cref="DefaultProgressBarHeight"/>.</summary>
    protected virtual Size MeasureProgressBarCore(double widthConstraint, double heightConstraint) =>
        new(double.IsFinite(widthConstraint) ? widthConstraint : DefaultProgressBarLength, DefaultProgressBarHeight);

    /// <summary>
    /// Draws a ProgressBar (delegate or <see cref="DrawProgressBarCore"/>), in left-to-right coordinates (the control
    /// mirrors right-to-left layouts). Indeterminate: fill the whole bounds with the track, with square ends, and draw
    /// one segment of <see cref="IndeterminateProgressSegment"/> at the start. The compositor slides that picture along
    /// the bar on the render thread (so the motion needs no UI-thread work), tiling it one bar width apart and clipping
    /// to the bar's shape (<see cref="GetProgressBarCornerRadius"/>).
    /// </summary>
    public void DrawProgressBar(SKCanvas canvas, SkUiProgressBarPaint bar)
    {
        if (ProgressBarPainter is { } painter)
        {
            painter(canvas, bar);
            return;
        }
        DrawProgressBarCore(canvas, bar);
    }

    /// <summary>Default ProgressBar geometry.</summary>
    protected virtual void DrawProgressBarCore(SKCanvas canvas, SkUiProgressBarPaint bar) { }

    #endregion

    #region Dragger

    /// <summary>
    /// Default size across a dragger (the height of a row splitter), in DIPs: the room it takes in a layout. A thinner one (a
    /// 1 DIP divider) still has a <see cref="DraggerTouchThickness"/> touch area.
    /// </summary>
    public virtual double DefaultDraggerThickness => 16;

    /// <summary>
    /// Least thickness of a dragger's touch area, in DIPs (<c>TouchThickness</c> on the control overrides it): a thinner
    /// dragger takes pointers this far across, centered on it, over its neighbors, and its grip draws over them.
    /// </summary>
    public virtual double DraggerTouchThickness => 24;

    /// <summary>Default length of the grip a look draws along a dragger, in DIPs (<c>GripLength</c> on the control overrides it).</summary>
    public virtual double DraggerGripLength => 36;

    /// <summary>Thickness of the grip, in DIPs (twice as much while dragged).</summary>
    public virtual double DraggerGripThickness => 4;

    /// <summary>Distance between a grip at the start or end of a dragger and its edge, in DIPs.</summary>
    public virtual double DraggerGripInset => 4;

    /// <summary>Thickness of a dragger's line (<c>ShowsLine</c>), in DIPs.</summary>
    public virtual double DraggerLineThickness => 1;

    /// <summary>Optional dragger painter.</summary>
    public Action<SKCanvas, SkUiDraggerPaint>? DraggerPainter { get; set; }

    /// <summary>
    /// Intrinsic dragger size: <see cref="DraggerGripLength"/> along its line, <see cref="DefaultDraggerThickness"/> across
    /// (a dragger in a grid stretches along its row or column).
    /// </summary>
    public Size MeasureDragger(bool lineRunsVertically) => lineRunsVertically
        ? new Size(DefaultDraggerThickness, DraggerGripLength)
        : new Size(DraggerGripLength, DefaultDraggerThickness);

    /// <summary>Draws a dragger (delegate or <see cref="DrawDraggerCore"/>): its line, then its grip.</summary>
    public void DrawDragger(SKCanvas canvas, SkUiDraggerPaint dragger)
    {
        if (DraggerPainter is { } painter)
        {
            painter(canvas, dragger);
            return;
        }
        DrawDraggerCore(canvas, dragger);
    }

    /// <summary>Default dragger geometry.</summary>
    protected virtual void DrawDraggerCore(SKCanvas canvas, SkUiDraggerPaint dragger) { }

    #endregion

    #region ScrollView

    /// <summary>Scroll bar thickness in DIPs.</summary>
    public virtual double ScrollBarThickness => 4;

    /// <summary>Scroll bar thickness while a pointer hovers or drags it (desktop), in DIPs; the track shows then.</summary>
    public virtual double ScrollBarExpandedThickness => 8;

    /// <summary>
    /// Width of the strip along a scroller's edge where a hovering pointer expands its scroll bar (and can drag the thumb or
    /// page), in DIPs. Touches there still scroll the content.
    /// </summary>
    public virtual double ScrollBarHitThickness => 16;

    /// <summary>
    /// Room a scroller reserves beside its content for a scroll bar with <see cref="ScrollBarVisibility.Always"/>, in DIPs
    /// (default: the expanded bar and its margins). Fading bars draw over the content instead.
    /// </summary>
    public virtual double ScrollBarReservedThickness => ScrollBarExpandedThickness + 2 * ScrollBarMargin;

    /// <summary>Gap between a scroll bar and the viewport edges, in DIPs.</summary>
    public virtual double ScrollBarMargin => 2;

    /// <summary>Shortest scroll bar thumb, in DIPs (long content would otherwise shrink it to a dot).</summary>
    public virtual double ScrollBarMinimumThumbLength => 24;

    /// <summary>How long scroll bars with <see cref="ScrollBarVisibility.Default"/> stay visible after scrolling stops.</summary>
    public virtual TimeSpan ScrollBarFadeDelay => TimeSpan.FromMilliseconds(500);

    /// <summary>How long scroll bars with <see cref="ScrollBarVisibility.Default"/> take to fade out after <see cref="ScrollBarFadeDelay"/>.</summary>
    public virtual TimeSpan ScrollBarFadeDuration => TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// What scrollers whose <c>Overscroll</c> is <see cref="SkUiOverscrollMode.Default"/> do past their edges: <see cref="SkUiOverscrollMode.None"/>
    /// (stop at the edge), <see cref="SkUiOverscrollMode.Bounce"/> or <see cref="SkUiOverscrollMode.Stretch"/>.
    /// </summary>
    public virtual SkUiOverscrollMode DefaultOverscroll => SkUiOverscrollMode.None;

    /// <summary>Optional scroll bar painter; when set, replaces <see cref="DrawScrollBarCore"/>.</summary>
    public Action<SKCanvas, SkUiScrollBarPaint>? ScrollBarPainter { get; set; }

    /// <summary>
    /// Draws a scroll bar thumb (delegate or <see cref="DrawScrollBarCore"/>) into <see cref="SkUiScrollBarPaint.Bounds"/>.
    /// It is drawn once per thumb length and color, and the compositor moves and fades the picture on the render thread
    /// while the content scrolls, so it must not depend on the scroll offset.
    /// </summary>
    public void DrawScrollBar(SKCanvas canvas, SkUiScrollBarPaint bar)
    {
        if (ScrollBarPainter is { } painter)
        {
            painter(canvas, bar);
            return;
        }
        DrawScrollBarCore(canvas, bar);
    }

    /// <summary>Default scroll bar thumb geometry.</summary>
    protected virtual void DrawScrollBarCore(SKCanvas canvas, SkUiScrollBarPaint bar) { }

    /// <summary>Optional scroll bar track painter; when set, replaces <see cref="DrawScrollBarTrackCore"/>.</summary>
    public Action<SKCanvas, SkUiScrollBarTrackPaint>? ScrollBarTrackPainter { get; set; }

    /// <summary>
    /// Draws the track behind an expanded scroll bar's thumb (delegate or <see cref="DrawScrollBarTrackCore"/>), while a
    /// pointer hovers or drags the bar.
    /// </summary>
    public void DrawScrollBarTrack(SKCanvas canvas, SkUiScrollBarTrackPaint track)
    {
        if (ScrollBarTrackPainter is { } painter)
        {
            painter(canvas, track);
            return;
        }
        DrawScrollBarTrackCore(canvas, track);
    }

    /// <summary>Default scroll bar track geometry.</summary>
    protected virtual void DrawScrollBarTrackCore(SKCanvas canvas, SkUiScrollBarTrackPaint track) { }

    #endregion

    #region Item reordering

    /// <summary>How much a collection view item lifted to be reordered is scaled (<see cref="SkUiTransitionKind.ReorderLift"/>); 1: not at all.</summary>
    public virtual double ReorderLiftScale => 1;

    /// <summary>The shadow of a collection view item lifted to be reordered, or <c>null</c> (default: none).</summary>
    public virtual Shadow? GetReorderLiftShadow() => null;

    /// <summary>
    /// How close to an end of a reorderable list the pointer brings a lifted item for the list to scroll that way, in DIPs
    /// (at most 15 % of the list's length).
    /// </summary>
    public virtual double ReorderAutoScrollZone => 48;

    /// <summary>How fast a reorderable list scrolls with the pointer at its very end, in DIPs per second (slower further in).</summary>
    public virtual double ReorderAutoScrollSpeed => 1200;

    #endregion

    #region Pull to refresh

    /// <summary>Size of the refresh indicator's square, in DIPs (<see cref="Core.SkUiCoreRefreshIndicator"/>).</summary>
    public virtual double RefreshIndicatorSize => 40;

    /// <summary>How far the content must be pulled past its top (shown distance, DIPs) for a release to start a refresh, where controls do not set their own.</summary>
    public virtual double RefreshTriggerDistance => 64;

    /// <summary>
    /// While refreshing: how far below the top the indicator's area ends, in DIPs. An overlay badge rests with its bottom
    /// there; an inline indicator is centered in it, the content held down by it.
    /// </summary>
    public virtual double RefreshRestDistance => 64;

    /// <summary>How pull-to-refresh controls whose style is <see cref="SkUiRefreshStyle.Default"/> show their indicator.</summary>
    public virtual SkUiRefreshStyle DefaultRefreshStyle => SkUiRefreshStyle.Overlay;

    /// <summary>Seconds per turn of the refresh indicator while refreshing (spun on the render thread); 0: it does not spin.</summary>
    public virtual double RefreshIndicatorSpinPeriod => 1;

    /// <summary>
    /// Whether <see cref="DrawRefreshIndicator"/> draws the pull itself (<see cref="SkUiRefreshIndicatorPaint.PullProgress"/>):
    /// the indicator is then recorded again as the pull changes. Default <c>false</c>: a pull shows through
    /// <see cref="GetRefreshPullFeedback"/>, which re-records nothing.
    /// </summary>
    public virtual bool RefreshIndicatorDrawsPullProgress => false;

    /// <summary>
    /// How a pull that got <paramref name="progress"/> of the trigger distance (1 at the trigger, more past it) shows the
    /// indicator, at composite time. Default: it fades in and, as an overlay badge, turns up to 270°.
    /// </summary>
    public virtual SkUiRefreshPullFeedback GetRefreshPullFeedback(double progress, SkUiRefreshStyle style)
    {
        var shown = Math.Clamp(progress, 0, 1);
        return new SkUiRefreshPullFeedback(shown, style == SkUiRefreshStyle.Overlay ? shown * 270 : 0);
    }

    /// <summary>The refresh indicator's shadow for <paramref name="style"/>, or <c>null</c> (default: none).</summary>
    public virtual Shadow? GetRefreshIndicatorShadow(SkUiRefreshStyle style) => null;

    /// <summary>Optional refresh indicator painter; when set, replaces <see cref="DrawRefreshIndicatorCore"/>.</summary>
    public Action<SKCanvas, SkUiRefreshIndicatorPaint>? RefreshIndicatorPainter { get; set; }

    /// <summary>
    /// Draws the refresh indicator of pull-to-refresh (delegate or <see cref="DrawRefreshIndicatorCore"/>) into
    /// <see cref="SkUiRefreshIndicatorPaint.Bounds"/>. While refreshing the compositor spins the picture about its center, so
    /// draw centered, rotation-symmetric geometry (as <see cref="DrawActivityIndicator"/>).
    /// </summary>
    public void DrawRefreshIndicator(SKCanvas canvas, SkUiRefreshIndicatorPaint paint)
    {
        if (RefreshIndicatorPainter is { } painter)
        {
            painter(canvas, paint);
            return;
        }
        DrawRefreshIndicatorCore(canvas, paint);
    }

    /// <summary>
    /// Default refresh indicator: the look's activity indicator (<see cref="DrawActivityIndicator"/>), on a round badge of
    /// the default background as an overlay, bare inline.
    /// </summary>
    protected virtual void DrawRefreshIndicatorCore(SKCanvas canvas, SkUiRefreshIndicatorPaint paint)
    {
        var bounds = paint.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        var inset = 0f;
        if (paint.Style == SkUiRefreshStyle.Overlay)
        {
            using var fill = new SKPaint { IsAntialias = true, Color = SkUiToggleDrawing.ToSkColor(SkUiColors.DefaultBackground) };
            canvas.DrawCircle(bounds.MidX, bounds.MidY, Math.Min(bounds.Width, bounds.Height) / 2, fill);
            inset = Math.Min(bounds.Width, bounds.Height) / 4;
        }
        using var arc = new SKPaint { Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, IsAntialias = true, Color = paint.Color };
        canvas.Save();
        canvas.Translate(bounds.Left + inset, bounds.Top + inset);
        DrawActivityIndicator(canvas, bounds.Width - 2 * inset, bounds.Height - 2 * inset, 0, arc);
        canvas.Restore();
    }

    #endregion

    #region IndicatorView

    /// <summary>The gap between indicators of indicator views that do not set their own (<c>IndicatorSpacing</c>), in DIPs.</summary>
    public virtual double DefaultIndicatorSpacing => 8;

    /// <summary>
    /// How much longer than the others the selected indicator is along the row, in DIPs, for indicators of
    /// <paramref name="indicatorSize"/> (a pill); 0 (default): all are the same size. Indicator views lay the row out with it,
    /// so the selected indicator's room moves with the selection.
    /// </summary>
    public virtual double GetSelectedIndicatorExtraLength(double indicatorSize, IndicatorShape shape) => 0;

    /// <summary>Optional indicator view painter; when set, replaces <see cref="DrawIndicatorsCore"/>.</summary>
    public Action<SKCanvas, SkUiIndicatorPaint>? IndicatorPainter { get; set; }

    /// <summary>
    /// Draws an indicator view's row of indicators (delegate or <see cref="DrawIndicatorsCore"/>): each in its
    /// <see cref="SkUiIndicatorPaint.GetIndicatorBounds"/>, colored by how selected it is
    /// (<see cref="SkUiIndicatorPaint.GetSelection"/>, <see cref="SkUiIndicatorPaint.GetColor"/>). It is drawn again while the
    /// selection moves (a linked carousel scrolls), so keep it cheap.
    /// </summary>
    public void DrawIndicators(SKCanvas canvas, SkUiIndicatorPaint paint)
    {
        if (IndicatorPainter is { } painter)
        {
            painter(canvas, paint);
            return;
        }
        DrawIndicatorsCore(canvas, paint);
    }

    /// <summary>Default indicator geometry: none (<see cref="DefaultSkUiLook"/> draws dots or a pill).</summary>
    protected virtual void DrawIndicatorsCore(SKCanvas canvas, SkUiIndicatorPaint paint) { }

    #endregion

    #region Picker and popups

    /// <summary>The minimum height of pickers that do not set their own (<c>MinimumHeightRequest</c>), in DIPs (default 44, a touch target).</summary>
    public virtual double DefaultPickerMinimumHeight => 44;

    /// <summary>The space between a picker field's edges and its text, in DIPs (the chevron's room is added at the end).</summary>
    public virtual Thickness PickerFieldPadding => new(12, 8);

    /// <summary>The room a picker field keeps for its chevron at the end, in DIPs (the chevron is drawn centered in it).</summary>
    public virtual double PickerChevronWidth => 32;

    /// <summary>Optional picker field painter; when set, replaces <see cref="DrawPickerFieldCore"/>.</summary>
    public Action<SKCanvas, SkUiPickerFieldPaint>? PickerFieldPainter { get; set; }

    /// <summary>
    /// Draws a picker's field chrome behind its text (delegate or <see cref="DrawPickerFieldCore"/>): an underline, an outline
    /// or a fill, the chevron in <see cref="SkUiPickerFieldPaint.ChevronBounds"/>, and the states (pointer over, pressed,
    /// focused, open, disabled; <see cref="SkUiPickerFieldPaint.Focused"/> and <see cref="SkUiPickerFieldPaint.Open"/> animate
    /// with the look's <see cref="SkUiTransitionKind.FieldFocus"/> and <see cref="SkUiTransitionKind.PickerOpen"/> transitions).
    /// The control draws its text over it.
    /// </summary>
    public void DrawPickerField(SKCanvas canvas, SkUiPickerFieldPaint field)
    {
        if (PickerFieldPainter is { } painter)
        {
            painter(canvas, field);
            return;
        }
        DrawPickerFieldCore(canvas, field);
    }

    /// <summary>Default picker field: none (<see cref="DefaultSkUiLook"/> draws <see cref="DefaultSkUiLook.PickerFieldStyle"/>).</summary>
    protected virtual void DrawPickerFieldCore(SKCanvas canvas, SkUiPickerFieldPaint field) { }

    /// <summary>The height of a row of a picker's wheels (<c>SkUiMultiColumnPicker</c>), in DIPs (default <see cref="PopupItemHeight"/>).</summary>
    public virtual double PickerWheelRowHeight => PopupItemHeight;

    /// <summary>How the items of a picker's wheels change as they leave the middle row (default: they fade, shrink a little and turn away as on a drum).</summary>
    public virtual SkUiCarouselEffect PickerWheelEffect => SkUiWheelEffect.Instance;

    /// <summary>The band behind the middle row of a picker's wheels (default <see cref="PopupHighlightColor"/>).</summary>
    public virtual Color PickerWheelBandColor => PopupHighlightColor;

    /// <summary>The corners of the band behind the middle row of a picker's wheels (default 8).</summary>
    public virtual CornerRadius PickerWheelBandCornerRadius => new(8);

    /// <summary>
    /// Whether a picker's list to choose one item from shows as a wheel in a <see cref="SkUiPopupPlacement.BottomPane"/> (default
    /// <c>false</c>: the list; <see cref="IosSkUiLook"/>: <c>true</c>, as iOS's picker). A list with several choices stays a list.
    /// </summary>
    public virtual bool PickerPaneShowsWheel => false;

    /// <summary>
    /// Whether OK comes before Cancel in a popup's footer, at its end side (default <c>false</c>: Cancel, then OK, as on Android,
    /// Apple platforms and the web; <c>true</c> as Windows' dialogs).
    /// </summary>
    public virtual bool PopupAcceptButtonFirst => false;

    /// <summary>
    /// Styles a button of a popup's footer or a bottom pane's toolbar after SkiaUi created it with its text and the picker's font: by
    /// default OK is a filled button in the accent color, Cancel and Clear are text in the accent color. Override it for the look's buttons.
    /// </summary>
    public virtual void StylePopupButton(SkUiButton button, SkUiPopupButton kind)
    {
        if (kind == SkUiPopupButton.Accept)
            return;
        button.FillColor = Colors.Transparent;
        button.TextColor = SkUiColors.Accent;
    }

    /// <summary>Optional painter of a picker field's tokens; when set, replaces <see cref="DrawPickerTokenCore"/>.</summary>
    public Action<SKCanvas, SkUiPickerTokenPaint>? PickerTokenPainter { get; set; }

    /// <summary>
    /// Draws a token of a picker field (several choices shown as tokens; delegate or <see cref="DrawPickerTokenCore"/>): its background
    /// and its × in <see cref="SkUiPickerTokenPaint.RemoveBounds"/> (empty when it cannot be removed); the control draws the text.
    /// </summary>
    public void DrawPickerToken(SKCanvas canvas, SkUiPickerTokenPaint token)
    {
        if (PickerTokenPainter is { } painter)
        {
            painter(canvas, token);
            return;
        }
        DrawPickerTokenCore(canvas, token);
    }

    /// <summary>Default token: a pill in <see cref="SkUiPickerTokenPaint.Fill"/> (outlined for "+N"), a cross in the muted color.</summary>
    protected virtual void DrawPickerTokenCore(SKCanvas canvas, SkUiPickerTokenPaint token)
    {
        const float arm = 3.5f;
        var radius = token.Bounds.Height / 2;
        using var paint = new SKPaint { IsAntialias = true, Color = token.Fill };
        if (token.IsMore)
        {
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            paint.Color = token.Foreground;
            var outline = token.Bounds;
            outline.Inflate(-0.5f, -0.5f);
            canvas.DrawRoundRect(outline, radius, radius, paint);
        }
        else
        {
            canvas.DrawRoundRect(token.Bounds, radius, radius, paint);
        }
        if (token.RemoveBounds.IsEmpty)
            return;
        var center = new SKPoint(token.RemoveBounds.MidX - 2, token.RemoveBounds.MidY);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.5f;
        paint.StrokeCap = SKStrokeCap.Round;
        paint.Color = token.Foreground.WithAlpha((byte)(token.Foreground.Alpha * (token.IsRemovePressed ? 0.6f : token.IsEnabled ? 1f : 0.5f)));
        canvas.DrawLine(center.X - arm, center.Y - arm, center.X + arm, center.Y + arm, paint);
        canvas.DrawLine(center.X - arm, center.Y + arm, center.X + arm, center.Y - arm, paint);
    }

    /// <summary>The room a picker field keeps for its clear button (<c>IsClearButtonVisible</c>) before the chevron, in DIPs (default 32).</summary>
    public virtual double PickerClearButtonWidth => 32;

    /// <summary>Optional painter of a picker field's clear button; when set, replaces <see cref="DrawPickerClearButtonCore"/>.</summary>
    public Action<SKCanvas, SkUiClearButtonPaint>? PickerClearButtonPainter { get; set; }

    /// <summary>
    /// Draws the clear button of a picker field (delegate or <see cref="DrawPickerClearButtonCore"/>) in
    /// <see cref="SkUiClearButtonPaint.Bounds"/>, over the field's chrome; shown while something is chosen.
    /// </summary>
    public void DrawPickerClearButton(SKCanvas canvas, SkUiClearButtonPaint button)
    {
        if (PickerClearButtonPainter is { } painter)
        {
            painter(canvas, button);
            return;
        }
        DrawPickerClearButtonCore(canvas, button);
    }

    /// <summary>Default clear button: a filled circle in the muted color with a cross in the field's background color, centered.</summary>
    protected virtual void DrawPickerClearButtonCore(SKCanvas canvas, SkUiClearButtonPaint button)
    {
        const float radius = 8;
        const float arm = 3.5f;
        var center = new SKPoint(button.Bounds.MidX, button.Bounds.MidY);
        var alpha = button.IsEnabled ? (button.IsPressed ? 0.75f : 1f) : 0.5f;
        using var paint = new SKPaint { IsAntialias = true, Color = button.Color.WithAlpha((byte)(button.Color.Alpha * alpha)) };
        canvas.DrawCircle(center, radius, paint);
        paint.Color = button.Background;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.5f;
        paint.StrokeCap = SKStrokeCap.Round;
        canvas.DrawLine(center.X - arm, center.Y - arm, center.X + arm, center.Y + arm, paint);
        canvas.DrawLine(center.X - arm, center.Y + arm, center.X + arm, center.Y - arm, paint);
    }

    /// <summary>Optional painter of a bottom pane's close button; when set, replaces <see cref="DrawPopupCloseButtonCore"/>.</summary>
    public Action<SKCanvas, SkUiClearButtonPaint>? PopupCloseButtonPainter { get; set; }

    /// <summary>
    /// Draws the close button (×) at the end of a bottom pane's toolbar (delegate or <see cref="DrawPopupCloseButtonCore"/>) in
    /// <see cref="SkUiClearButtonPaint.Bounds"/> (its touch target): <see cref="SkUiClearButtonPaint.Color"/> is the cross's,
    /// <see cref="SkUiClearButtonPaint.Background"/> the circle's.
    /// </summary>
    public void DrawPopupCloseButton(SKCanvas canvas, SkUiClearButtonPaint button)
    {
        if (PopupCloseButtonPainter is { } painter)
        {
            painter(canvas, button);
            return;
        }
        DrawPopupCloseButtonCore(canvas, button);
    }

    /// <summary>Default close button: a soft circle with a cross, centered, as iOS 26's sheets (dimmed while pressed).</summary>
    protected virtual void DrawPopupCloseButtonCore(SKCanvas canvas, SkUiClearButtonPaint button)
    {
        const float radius = 15;
        const float arm = 5;
        var center = new SKPoint(button.Bounds.MidX, button.Bounds.MidY);
        var alpha = button.IsEnabled ? (button.IsPressed ? 0.6f : 1f) : 0.5f;
        using var paint = new SKPaint { IsAntialias = true, Color = button.Background.WithAlpha((byte)(button.Background.Alpha * alpha)) };
        canvas.DrawCircle(center, radius, paint);
        paint.Color = button.Color.WithAlpha((byte)(button.Color.Alpha * alpha));
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 2;
        paint.StrokeCap = SKStrokeCap.Round;
        canvas.DrawLine(center.X - arm, center.Y - arm, center.X + arm, center.Y + arm, paint);
        canvas.DrawLine(center.X - arm, center.Y + arm, center.X + arm, center.Y - arm, paint);
    }

    /// <summary>
    /// Where pickers that do not set their own <c>PopupPlacement</c> open their list (default <see cref="SkUiPopupPlacement.Anchored"/>;
    /// <see cref="DefaultSkUiLook"/> follows the device: anchored on desktops and tablets, a dialog on phones).
    /// </summary>
    public virtual SkUiPopupPlacement DefaultPopupPlacement => SkUiPopupPlacement.Anchored;

    /// <summary>How <see cref="SkUiPopupPlacement.Window"/> popups align horizontally in the window (default centered).</summary>
    public virtual LayoutOptions PopupHorizontalOptions => LayoutOptions.Center;

    /// <summary>How <see cref="SkUiPopupPlacement.Window"/> popups align vertically in the window (default centered; <c>End</c>: a bottom sheet).</summary>
    public virtual LayoutOptions PopupVerticalOptions => LayoutOptions.Center;

    /// <summary>The space popups keep from the window's safe area edges, in DIPs (default 8).</summary>
    public virtual Thickness PopupMargin => new(8);

    /// <summary>The gap between an anchored popup and its anchor, in DIPs (default 4).</summary>
    public virtual double PopupAnchorGap => 4;

    /// <summary>The width of a <see cref="SkUiPopupPlacement.Window"/> popup's list (unless the window is narrower), in DIPs (default 320).</summary>
    public virtual double PopupPreferredWidth => 320;

    /// <summary>The height of a popup list's rows without an item template, in DIPs (default 44).</summary>
    public virtual double PopupItemHeight => 44;

    /// <summary>The space around a popup list row's text, in DIPs (default 16 across, 8 down).</summary>
    public virtual Thickness PopupItemPadding => new(16, 8);

    /// <summary>
    /// What a modal popup system dims the page with behind a popup of <paramref name="placement"/>
    /// (<see cref="SkUiPopupRequest.OverlayColor"/>); default: nothing for anchored popups and bottom panes (as a keyboard), black
    /// at 32 % for the others.
    /// </summary>
    public virtual Color? GetPopupOverlayColor(SkUiPopupPlacement placement) =>
        placement is SkUiPopupPlacement.Anchored or SkUiPopupPlacement.BottomPane ? null : Color.FromRgba(0, 0, 0, 0.32);

    /// <summary>
    /// The corner radii of a popup of <paramref name="placement"/> (<see cref="SkUiPopupRequest.CornerRadius"/>), in DIPs: the
    /// drawn content is clipped to them, and presenters shape their container the same. Default 10, and square for bottom
    /// panes (docked to the window's edges, as a keyboard).
    /// </summary>
    public virtual CornerRadius GetPopupCornerRadius(SkUiPopupPlacement placement) =>
        placement == SkUiPopupPlacement.BottomPane ? default : new CornerRadius(10);

    /// <summary>
    /// The shadow under a popup of <paramref name="placement"/> (<see cref="SkUiPopupRequest.Shadow"/>), or <c>null</c> for
    /// none; a new object for each call. Default: a soft black drop shadow (25 %, blurred 16, 4 down).
    /// </summary>
    public virtual Shadow? GetPopupShadow(SkUiPopupPlacement placement) =>
        new() { Brush = Brush.Black, Opacity = 0.25f, Radius = 16, Offset = new Point(0, 4) };

    /// <summary>A popup list's highlighted row background (the selection and the keyboard's position): default the accent at 12 %.</summary>
    public virtual Color PopupHighlightColor => SkUiColors.Accent.WithAlpha(0.12f);

    #endregion

    #region Text fields

    /// <summary>The minimum height of text fields (entries, search bars) that do not set their own, in DIPs (default 44, a touch target).</summary>
    public virtual double DefaultTextFieldMinimumHeight => 44;

    /// <summary>The space between a text field's edges and its text, in DIPs (a clear button's room is added at the end).</summary>
    public virtual Thickness TextFieldPadding => new(12, 8);

    /// <summary>The room a text field keeps for its clear button (<c>ClearButtonVisibility</c>) at the end, in DIPs (default 32).</summary>
    public virtual double TextFieldClearButtonWidth => 32;

    /// <summary>
    /// The space between a text field's leading or trailing view (<c>LeadingView</c>, <c>TrailingView</c>) and its text, in DIPs
    /// (default 8). A clear button between the text and the trailing view is the space there.
    /// </summary>
    public virtual double TextFieldAdornmentSpacing => 8;

    /// <summary>
    /// The space between a text field and its message under it (<c>HelperText</c>, or <c>ErrorText</c> while it has an error), in
    /// DIPs (default 4).
    /// </summary>
    public virtual double TextFieldMessageSpacing => 4;

    /// <summary>The font size of a text field's message under it, in DIPs before the system text size (default 12).</summary>
    public virtual double TextFieldMessageFontSize => 12;

    /// <summary>The caret's width in DIPs (default 2).</summary>
    public virtual double CaretWidth => 2;

    /// <summary>
    /// How long the caret shows, then hides, while it blinks (default 500 ms; zero: it does not blink). Typing and moving the
    /// caret show it solid until the next interval.
    /// </summary>
    public virtual TimeSpan CaretBlinkInterval => TimeSpan.FromMilliseconds(500);

    /// <summary>The caret's color for fields that set none (their <c>CaretColor</c>; default: the color scheme's accent).</summary>
    public virtual Color DefaultCaretColor => SkUiColors.Accent;

    /// <summary>
    /// The color behind selected text for fields that set none (their <c>SelectionColor</c>; default: the color scheme's accent, 30 %
    /// opaque).
    /// </summary>
    public virtual Color DefaultSelectionColor => SkUiColors.Accent.WithAlpha(0.3f);

    /// <summary>
    /// Optional painter of the handles of a selection made by touch; when set, replaces <see cref="DrawTextSelectionHandleCore"/>.
    /// </summary>
    public Action<SKCanvas, SkUiTextSelectionHandlePaint>? TextSelectionHandlePainter { get; set; }

    /// <summary>
    /// Draws a handle of a text selection made by touch (delegate or <see cref="DrawTextSelectionHandleCore"/>): dragging it moves that
    /// end of the selection. The handle sits on the selection's edge (<see cref="SkUiTextSelectionHandlePaint.Edge"/>), inside the field.
    /// </summary>
    public void DrawTextSelectionHandle(SKCanvas canvas, SkUiTextSelectionHandlePaint handle)
    {
        if (TextSelectionHandlePainter is { } painter)
        {
            painter(canvas, handle);
            return;
        }
        DrawTextSelectionHandleCore(canvas, handle);
    }

    /// <summary>Default handle (as iOS's): a bar along the selection's edge with a knob above the start and below the end.</summary>
    protected virtual void DrawTextSelectionHandleCore(SKCanvas canvas, SkUiTextSelectionHandlePaint handle)
    {
        var edge = handle.Edge;
        using var paint = new SKPaint { IsAntialias = true, Color = handle.Color };
        canvas.DrawRect(new SKRect(edge.MidX - 1, edge.Top, edge.MidX + 1, edge.Bottom), paint);
        const float knob = 4.5f;
        canvas.DrawCircle(edge.MidX, handle.IsStart ? edge.Top - knob + 1 : edge.Bottom + knob - 1, knob, paint);
    }

    /// <summary>
    /// The rectangle a selection handle draws in around its <paramref name="edge"/> (field coordinates): it may hang outside the
    /// field, and a touch grabs the handle near it. Default: the edge with its knob above the start or below the end.
    /// </summary>
    public virtual SKRect GetTextSelectionHandleBounds(SKRect edge, bool isStart) => isStart
        ? new SKRect(edge.MidX - 5, edge.Top - 9, edge.MidX + 5, edge.Bottom)
        : new SKRect(edge.MidX - 5, edge.Top, edge.MidX + 5, edge.Bottom + 9);

    /// <summary>The distance within which a touch grabs a selection handle, in DIPs (default 22).</summary>
    public virtual double TextSelectionHandleTouchRadius => 22;

    /// <summary>
    /// Whether a tap in a text field shows a handle under the caret that drags it, as Android's fields do (default: on Android).
    /// Elsewhere the caret itself is dragged, as on iOS. Both show the platform's magnifier while the caret moves.
    /// </summary>
    public virtual bool ShowsTextInsertionHandle => OperatingSystem.IsAndroid();

    /// <summary>How long the insertion handle stays after a tap when it is not used (default 4 s, as Android's).</summary>
    public virtual TimeSpan TextInsertionHandleTimeout => TimeSpan.FromSeconds(4);

    /// <summary>
    /// Optional painter of the insertion handle (<see cref="ShowsTextInsertionHandle"/>); when set, replaces
    /// <see cref="DrawTextInsertionHandleCore"/>. It draws inside <see cref="GetTextInsertionHandleBounds"/>.
    /// </summary>
    public Action<SKCanvas, SkUiTextInsertionHandlePaint>? TextInsertionHandlePainter { get; set; }

    /// <summary>
    /// Draws the handle under a text field's caret after a tap (delegate or <see cref="DrawTextInsertionHandleCore"/>): dragging it
    /// moves the caret. It hangs below the caret, also outside the field.
    /// </summary>
    public void DrawTextInsertionHandle(SKCanvas canvas, SkUiTextInsertionHandlePaint handle)
    {
        if (TextInsertionHandlePainter is { } painter)
        {
            painter(canvas, handle);
            return;
        }
        DrawTextInsertionHandleCore(canvas, handle);
    }

    /// <summary>Default insertion handle (as Android's): a drop hanging from the caret's bottom.</summary>
    protected virtual void DrawTextInsertionHandleCore(SKCanvas canvas, SkUiTextInsertionHandlePaint handle)
    {
        var bounds = GetTextInsertionHandleBounds(handle.Caret);
        var radius = bounds.Width / 2;
        var center = new SKPoint(bounds.MidX, bounds.Bottom - radius);
        using var paint = new SKPaint { IsAntialias = true, Color = handle.Color };
        canvas.DrawCircle(center, radius, paint);
        // The point: from the caret down to where its lines touch the circle (45° either side, the circle being √2 radii below).
        var tangent = radius / MathF.Sqrt(2);
        using var builder = new SKPathBuilder();
        builder.MoveTo(bounds.MidX, bounds.Top);
        builder.LineTo(center.X + tangent, center.Y - tangent);
        builder.LineTo(center.X, center.Y);
        builder.LineTo(center.X - tangent, center.Y - tangent);
        builder.Close();
        using var path = builder.Detach();
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// The insertion handle's rectangle under <paramref name="caret"/> (field coordinates): where it draws and where a touch grabs
    /// it. Default: a drop 22 DIPs wide, its point at the caret's bottom.
    /// </summary>
    public virtual SKRect GetTextInsertionHandleBounds(SKRect caret)
    {
        const float radius = 11;
        return new SKRect(caret.MidX - radius, caret.Bottom, caret.MidX + radius, caret.Bottom + radius * (1 + MathF.Sqrt(2)));
    }

    /// <summary>Optional text field painter; when set, replaces <see cref="DrawTextFieldCore"/>.</summary>
    public Action<SKCanvas, SkUiTextFieldPaint>? TextFieldPainter { get; set; }

    /// <summary>
    /// Draws a text field's chrome behind its text (delegate or <see cref="DrawTextFieldCore"/>): an underline, an outline or a fill
    /// with its states (pointer over, focused, read-only, disabled, an error; <see cref="SkUiTextFieldPaint.Focused"/> animates with
    /// the look's <see cref="SkUiTransitionKind.FieldFocus"/> transition). The control draws the selection, text, placeholder and
    /// caret over it, and its message (helper or error text) under it.
    /// </summary>
    public void DrawTextField(SKCanvas canvas, SkUiTextFieldPaint field)
    {
        if (TextFieldPainter is { } painter)
        {
            painter(canvas, field);
            return;
        }
        DrawTextFieldCore(canvas, field);
    }

    /// <summary>Default text field: none (<see cref="DefaultSkUiLook"/> draws <see cref="DefaultSkUiLook.TextFieldStyle"/>).</summary>
    protected virtual void DrawTextFieldCore(SKCanvas canvas, SkUiTextFieldPaint field) { }

    /// <summary>
    /// The size of a numeric entry's − or + button, in DIPs (default 36 × 28): its width beside the text (both buttons side by side
    /// take twice that, stacked in one column once), its height as a bar above or below the text (split and stacked). A button's
    /// own text that needs more makes it larger.
    /// </summary>
    public virtual Size TextFieldStepButtonSize => new(36, 28);

    /// <summary>Optional painter of a numeric entry's − and + buttons; when set, replaces <see cref="DrawTextFieldStepButtonsCore"/>.</summary>
    public Action<SKCanvas, SkUiTextFieldStepButtonsPaint>? TextFieldStepButtonsPainter { get; set; }

    /// <summary>
    /// Draws a numeric entry's − and + buttons (delegate or <see cref="DrawTextFieldStepButtonsCore"/>) in
    /// <see cref="SkUiTextFieldStepButtonsPaint.DecrementBounds"/> and <see cref="SkUiTextFieldStepButtonsPaint.IncrementBounds"/>,
    /// inside the field's chrome, with their pressed states and dimmed when they cannot step. A button with a text of the entry's
    /// own (<see cref="SkUiTextFieldStepButtonsPaint.HasDecrementText"/>, <see cref="SkUiTextFieldStepButtonsPaint.HasIncrementText"/>)
    /// gets no glyph: the entry draws its text over the look's drawing.
    /// </summary>
    public void DrawTextFieldStepButtons(SKCanvas canvas, SkUiTextFieldStepButtonsPaint buttons)
    {
        if (TextFieldStepButtonsPainter is { } painter)
        {
            painter(canvas, buttons);
            return;
        }
        DrawTextFieldStepButtonsCore(canvas, buttons);
    }

    /// <summary>
    /// Default step buttons: a light rounded tint while pressed, a short muted divider between the buttons and the text and between
    /// the two buttons, and a − and a + in the foreground (dimmed when the button cannot step or the entry is disabled).
    /// </summary>
    protected virtual void DrawTextFieldStepButtonsCore(SKCanvas canvas, SkUiTextFieldStepButtonsPaint buttons)
    {
        var decrement = buttons.DecrementBounds;
        var increment = buttons.IncrementBounds;
        if (decrement.IsEmpty && increment.IsEmpty)
            return;
        var enabled = buttons.IsEnabled;
        using var paint = new SKPaint { IsAntialias = true };
        void Press(SKRect button, float amount)
        {
            if (amount <= 0 || button.Width <= 4 || button.Height <= 4)
                return;
            paint.Color = buttons.Foreground.WithAlpha((byte)(28 * Math.Clamp(amount, 0, 1)));
            var tint = button;
            tint.Inflate(-2, -2);
            canvas.DrawRoundRect(tint, 4, 4, paint);
        }
        Press(decrement, buttons.DecrementPressed);
        Press(increment, buttons.IncrementPressed);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1;
        paint.Color = buttons.Border.WithAlpha((byte)(buttons.Border.Alpha * (enabled ? 0.6f : 0.3f)));
        var field = buttons.Field;
        // A divider on the side of a button that faces the text (or the other button), short as the stepper's.
        void Divider(SKRect button)
        {
            if (button.IsEmpty)
                return;
            if (button.Width >= field.Width - 0.5f)
            {
                // A bar across the field: its edge towards the text.
                var y = button.MidY < field.MidY ? button.Bottom : button.Top;
                var inset = Math.Min(12, field.Width / 4);
                canvas.DrawLine(field.Left + inset, y, field.Right - inset, y, paint);
                return;
            }
            var x = button.MidX < field.MidX ? button.Right : button.Left;
            var vertical = button.Height * 0.22f;
            canvas.DrawLine(x, button.Top + vertical, x, button.Bottom - vertical, paint);
        }
        if (buttons.Orientation == StackOrientation.Vertical && buttons.Placement != SkUiStepButtonPlacement.Split)
        {
            // One column: its edge towards the text, and a line between + and −.
            Divider(SKRect.Union(decrement, increment));
            var y = (increment.Bottom + decrement.Top) / 2;
            var inset = decrement.Width * 0.22f;
            canvas.DrawLine(decrement.Left + inset, y, decrement.Right - inset, y, paint);
        }
        else if (buttons.Placement == SkUiStepButtonPlacement.Split)
        {
            Divider(decrement);
            Divider(increment);
        }
        else
        {
            // Side by side at one end: the inner button's edge towards the text, and a line between the two.
            var (outer, inner) = Math.Abs(decrement.MidX - field.MidX) > Math.Abs(increment.MidX - field.MidX) ? (decrement, increment) : (increment, decrement);
            Divider(inner);
            var x = outer.MidX < inner.MidX ? outer.Right : outer.Left;
            var vertical = outer.Height * 0.22f;
            canvas.DrawLine(x, outer.Top + vertical, x, outer.Bottom - vertical, paint);
        }
        paint.StrokeWidth = 1.75f;
        paint.StrokeCap = SKStrokeCap.Round;
        void Glyph(SKRect button, bool plus, bool canStep)
        {
            if (button.IsEmpty)
                return;
            var color = buttons.Foreground;
            paint.Color = color.WithAlpha((byte)(color.Alpha * (enabled && canStep ? 1f : StepperDimmedOpacity)));
            var arm = Math.Min(5.5f, Math.Min(button.Width, button.Height) / 4);
            canvas.DrawLine(button.MidX - arm, button.MidY, button.MidX + arm, button.MidY, paint);
            if (plus)
                canvas.DrawLine(button.MidX, button.MidY - arm, button.MidX, button.MidY + arm, paint);
        }
        if (!buttons.HasDecrementText)
            Glyph(decrement, plus: false, buttons.CanDecrement);
        if (!buttons.HasIncrementText)
            Glyph(increment, plus: true, buttons.CanIncrement);
    }

    /// <summary>Optional painter of a text field's clear button; when set, replaces <see cref="DrawTextFieldClearButtonCore"/>.</summary>
    public Action<SKCanvas, SkUiClearButtonPaint>? TextFieldClearButtonPainter { get; set; }

    /// <summary>
    /// Draws the clear button of a text field (delegate or <see cref="DrawTextFieldClearButtonCore"/>) in
    /// <see cref="SkUiClearButtonPaint.Bounds"/>, at the field's end; shown while it is focused and has text.
    /// </summary>
    public void DrawTextFieldClearButton(SKCanvas canvas, SkUiClearButtonPaint button)
    {
        if (TextFieldClearButtonPainter is { } painter)
        {
            painter(canvas, button);
            return;
        }
        DrawTextFieldClearButtonCore(canvas, button);
    }

    /// <summary>Default text field clear button: the picker's (<see cref="DrawPickerClearButtonCore"/>).</summary>
    protected virtual void DrawTextFieldClearButtonCore(SKCanvas canvas, SkUiClearButtonPaint button) => DrawPickerClearButtonCore(canvas, button);

    #endregion

    #region Search bar

    /// <summary>The room a search bar keeps for its icon at the start, in DIPs (default 28).</summary>
    public virtual double SearchIconWidth => 28;

    /// <summary>Optional search field painter; when set, replaces <see cref="DrawSearchFieldCore"/>.</summary>
    public Action<SKCanvas, SkUiSearchFieldPaint>? SearchFieldPainter { get; set; }

    /// <summary>
    /// Draws a search bar's field behind its text (delegate or <see cref="DrawSearchFieldCore"/>): its background and the search icon
    /// in <see cref="SkUiSearchFieldPaint.IconBounds"/>, with its states. The control draws the text, the clear button and the caret.
    /// </summary>
    public void DrawSearchField(SKCanvas canvas, SkUiSearchFieldPaint field)
    {
        if (SearchFieldPainter is { } painter)
        {
            painter(canvas, field);
            return;
        }
        DrawSearchFieldCore(canvas, field);
    }

    /// <summary>
    /// Default search field: a lightly filled pill (a little darker under a pointer, an accent outline while focused, an outline in
    /// the error color while it has an error) and a magnifying glass in the icon color. The search bar's own chrome
    /// (<see cref="SkUiSearchFieldPaint.Chrome"/>) replaces the pill's fill (drawn as set, half as opaque while disabled) and corner
    /// radii and adds an idle outline; with a field style of its own (<see cref="SkUiSearchFieldPaint.Style"/>) only the icon is drawn.
    /// </summary>
    protected virtual void DrawSearchFieldCore(SKCanvas canvas, SkUiSearchFieldPaint field)
    {
        var bounds = field.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        using var paint = new SKPaint { IsAntialias = true };
        if (field.Style == SkUiTextFieldStyle.Default)
            DrawSearchFieldBackground(canvas, field, paint);
        // The magnifying glass: a ring and a handle to the bottom right (left in right-to-left layouts).
        var icon = field.IconBounds;
        var size = Math.Min(14f, Math.Min(icon.Width, icon.Height) * 0.7f);
        var ringRadius = size * 0.36f;
        var center = new SKPoint(icon.MidX - size * 0.1f * (field.IsRightToLeft ? -1 : 1), icon.MidY - size * 0.1f);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.75f;
        paint.StrokeCap = SKStrokeCap.Round;
        paint.Color = field.IconColor.WithAlpha((byte)(field.IconColor.Alpha * (field.IsEnabled ? 1f : 0.5f)));
        canvas.DrawCircle(center, ringRadius, paint);
        var direction = field.IsRightToLeft ? -1 : 1;
        var start = new SKPoint(center.X + direction * ringRadius * 0.72f, center.Y + ringRadius * 0.72f);
        canvas.DrawLine(start, new SKPoint(start.X + direction * size * 0.3f, start.Y + size * 0.3f), paint);
    }

    private void DrawSearchFieldBackground(SKCanvas canvas, SkUiSearchFieldPaint field, SKPaint paint)
    {
        var bounds = field.Bounds;
        var chrome = field.Chrome;
        var radii = chrome.CornerRadii ?? new CornerRadius(Math.Min(10, bounds.Height / 2));
        if (chrome.Fill is { } own)
            paint.Color = field.IsEnabled ? own : own.WithAlpha((byte)(own.Alpha / 2));
        else
        {
            var fill = field.IsPointerOver && field.IsEnabled ? 0.24f : 0.16f;
            paint.Color = field.Fill.WithAlpha((byte)(field.Fill.Alpha * (field.IsEnabled ? fill : fill / 2)));
        }
        DrawRoundRect(canvas, bounds, radii, paint);
        var focus = field.IsEnabled ? Math.Clamp(field.Focused, 0, 1) : 0;
        var idle = chrome.BorderWidth ?? 0;
        if (focus > 0 || field.HasError)
        {
            // An error outlines the pill while unfocused too (1 DIP), as an error outlines a text field.
            var width = Math.Max(idle, field.HasError ? 1 + 0.5f * focus : 1.5f * focus);
            paint.Color = !field.HasError ? Mix(chrome.Border ?? field.Accent, field.Accent, focus)
                : field.IsEnabled ? field.Error : field.Error.WithAlpha((byte)(field.Error.Alpha / 2));
            DrawInnerRing(canvas, bounds, radii, width, paint);
        }
        else if (idle > 0)
        {
            var border = chrome.Border ?? field.Fill;
            paint.Color = field.IsEnabled ? border : border.WithAlpha((byte)(border.Alpha / 2));
            DrawInnerRing(canvas, bounds, radii, idle, paint);
        }
        paint.Style = SKPaintStyle.Fill;
    }

    #endregion

    #region Stepper

    /// <summary>
    /// The size of each of a stepper's two buttons, in DIPs (default 47 × 32: the pair is iOS's 94 × 32 stepper; stacked, 47 × 64).
    /// </summary>
    public virtual Size StepperButtonSize => new(47, 32);

    /// <summary>The opacity of a stepper's − and + (glyphs or texts) on a button that cannot step, and of its outline when disabled.</summary>
    internal const float StepperDimmedOpacity = 0.35f;

    internal const float StepperCornerRadiusValue = 8;

    /// <summary>A stepper's corner radius in DIPs when its <c>CornerRadius</c> is not set (default 8).</summary>
    public virtual double DefaultStepperCornerRadius => StepperCornerRadiusValue;

    /// <summary>How long a held stepper button waits before it repeats (default 400 ms).</summary>
    public virtual TimeSpan StepperRepeatDelay => TimeSpan.FromMilliseconds(400);

    /// <summary>How often a held stepper button repeats (default every 100 ms).</summary>
    public virtual TimeSpan StepperRepeatInterval => TimeSpan.FromMilliseconds(100);

    /// <summary>Optional stepper painter; when set, replaces <see cref="DrawStepperCore"/>.</summary>
    public Action<SKCanvas, SkUiStepperPaint>? StepperPainter { get; set; }

    /// <summary>
    /// Draws a stepper (delegate or <see cref="DrawStepperCore"/>): its − and + buttons in <see cref="SkUiStepperPaint.DecrementBounds"/>
    /// and <see cref="SkUiStepperPaint.IncrementBounds"/>, side by side or stacked (<see cref="SkUiStepperPaint.Orientation"/>), with
    /// their pressed and disabled states (a button is disabled at its end of the range). A button with a text of the control's own
    /// (<see cref="SkUiStepperPaint.HasDecrementText"/>, <see cref="SkUiStepperPaint.HasIncrementText"/>) gets no glyph: the control
    /// draws its text over the look's drawing. The shape is rounded by <see cref="SkUiStepperPaint.CornerRadius"/> and outlined by
    /// <see cref="SkUiStepperPaint.BorderWidth"/> in <see cref="SkUiStepperPaint.BorderColor"/>.
    /// </summary>
    public void DrawStepper(SKCanvas canvas, SkUiStepperPaint stepper)
    {
        if (StepperPainter is { } painter)
        {
            painter(canvas, stepper);
            return;
        }
        DrawStepperCore(canvas, stepper);
    }

    /// <summary>
    /// Default stepper: one rounded (<see cref="SkUiStepperPaint.CornerRadius"/>), lightly filled segment per button, divided by a
    /// short line, a − and a + drawn in the foreground (dimmed when the button cannot step), a darker fill while pressed and an accent
    /// outline while focused. With a <see cref="SkUiStepperPaint.BorderWidth"/>, an outline in <see cref="SkUiStepperPaint.BorderColor"/>
    /// and a divider of the same width and color across the whole stepper, as a segmented control's.
    /// </summary>
    protected virtual void DrawStepperCore(SKCanvas canvas, SkUiStepperPaint stepper)
    {
        var bounds = stepper.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        // A radius past half the short side gives a pill.
        var radius = Math.Clamp(stepper.CornerRadius, 0, Math.Min(bounds.Width, bounds.Height) / 2);
        var borderWidth = Math.Clamp(stepper.BorderWidth, 0, Math.Min(bounds.Width, bounds.Height) / 2);
        var bordered = borderWidth > 0 && stepper.BorderColor.Alpha > 0;
        var enabled = stepper.IsEnabled;
        using var paint = new SKPaint { IsAntialias = true };
        paint.Color = stepper.Border.WithAlpha((byte)(stepper.Border.Alpha * (enabled ? 0.18f : 0.1f)));
        canvas.DrawRoundRect(bounds, radius, radius, paint);
        // Pressed buttons darken (clipped to the rounded shape).
        var save = canvas.Save();
        using (var clip = new SKRoundRect(bounds, radius))
            canvas.ClipRoundRect(clip, antialias: true);
        void Press(SKRect button, float amount)
        {
            if (amount <= 0)
                return;
            paint.Color = stepper.Foreground.WithAlpha((byte)(28 * Math.Clamp(amount, 0, 1)));
            canvas.DrawRect(button, paint);
        }
        Press(stepper.DecrementBounds, stepper.DecrementPressed);
        Press(stepper.IncrementBounds, stepper.IncrementPressed);
        canvas.RestoreToCount(save);
        paint.Style = SKPaintStyle.Stroke;
        // The outline, inside the bounds.
        var borderColor = stepper.BorderColor.WithAlpha((byte)(stepper.BorderColor.Alpha * (enabled ? 1f : StepperDimmedOpacity)));
        if (bordered)
        {
            var outline = bounds;
            outline.Inflate(-borderWidth / 2, -borderWidth / 2);
            var outlineRadius = Math.Max(0, radius - borderWidth / 2);
            paint.Color = borderColor;
            paint.StrokeWidth = borderWidth;
            canvas.DrawRoundRect(outline, outlineRadius, outlineRadius, paint);
        }
        // The divider between the buttons: across the stepper in the outline's width and color, or a short muted line.
        if (bordered)
            paint.Color = borderColor;
        else
        {
            paint.Color = stepper.Border.WithAlpha((byte)(stepper.Border.Alpha * 0.6f));
            paint.StrokeWidth = 1;
        }
        if (stepper.Orientation == StackOrientation.Vertical)
        {
            var y = (stepper.IncrementBounds.Bottom + stepper.DecrementBounds.Top) / 2;
            var inset = bordered ? borderWidth : bounds.Width * 0.22f;
            canvas.DrawLine(bounds.Left + inset, y, bounds.Right - inset, y, paint);
        }
        else
        {
            var (start, end) = stepper.IsRightToLeft ? (stepper.IncrementBounds, stepper.DecrementBounds) : (stepper.DecrementBounds, stepper.IncrementBounds);
            var x = (start.Right + end.Left) / 2;
            var inset = bordered ? borderWidth : bounds.Height * 0.22f;
            canvas.DrawLine(x, bounds.Top + inset, x, bounds.Bottom - inset, paint);
        }
        // − and +.
        paint.StrokeWidth = 1.75f;
        paint.StrokeCap = SKStrokeCap.Round;
        void Glyph(SKRect button, bool plus, bool canStep)
        {
            var color = stepper.Foreground;
            paint.Color = color.WithAlpha((byte)(color.Alpha * (enabled && canStep ? 1f : StepperDimmedOpacity)));
            var arm = Math.Min(6f, Math.Min(button.Width, button.Height) / 4);
            canvas.DrawLine(button.MidX - arm, button.MidY, button.MidX + arm, button.MidY, paint);
            if (plus)
                canvas.DrawLine(button.MidX, button.MidY - arm, button.MidX, button.MidY + arm, paint);
        }
        if (!stepper.HasDecrementText)
            Glyph(stepper.DecrementBounds, plus: false, stepper.CanDecrement);
        if (!stepper.HasIncrementText)
            Glyph(stepper.IncrementBounds, plus: true, stepper.CanIncrement);
        if (enabled && stepper.Focused > 0)
        {
            var width = 2 * Math.Clamp(stepper.Focused, 0, 1);
            var ring = bounds;
            ring.Inflate(-width / 2, -width / 2);
            paint.Color = stepper.Accent;
            paint.StrokeWidth = width;
            var ringRadius = Math.Max(0, radius - width / 2);
            canvas.DrawRoundRect(ring, ringRadius, ringRadius, paint);
        }
    }

    #endregion

    #region Avatar

    // Hues the avatar palette picks from by name; light backgrounds are tints, dark ones shades, text the other way round.
    private static readonly Color[] AvatarHues =
    [
        Color.FromArgb("#E53935"), Color.FromArgb("#D81B60"), Color.FromArgb("#8E24AA"), Color.FromArgb("#5E35B1"),
        Color.FromArgb("#3949AB"), Color.FromArgb("#1E88E5"), Color.FromArgb("#00897B"), Color.FromArgb("#43A047"),
        Color.FromArgb("#7CB342"), Color.FromArgb("#F4511E"), Color.FromArgb("#6D4C41"), Color.FromArgb("#546E7A")
    ];
    // Built once: avatars resolve their colors on every recording.
    private static readonly SkUiAvatarColors[] LightAvatarColors = [.. AvatarHues.Select(hue => new SkUiAvatarColors(Blend(hue, Colors.White, 0.8f), Blend(hue, Colors.Black, 0.45f)))];
    private static readonly SkUiAvatarColors[] DarkAvatarColors = [.. AvatarHues.Select(hue => new SkUiAvatarColors(Blend(hue, Colors.Black, 0.2f), Colors.White))];

    private static Color Blend(Color from, Color to, float amount) => new(
        from.Red + (to.Red - from.Red) * amount, from.Green + (to.Green - from.Green) * amount, from.Blue + (to.Blue - from.Blue) * amount);

    /// <summary>The size of avatars that set no size of their own, in DIPs (48 × 48, the Community Toolkit's).</summary>
    public virtual Size DefaultAvatarSize => new(48, 48);

    /// <summary>
    /// The text size of avatars that set no <c>FontSize</c>, for a content area of <paramref name="content"/> DIPs (inside
    /// the padding and the border): 40 % of its smaller side, as Material's avatars. Text that is still too wide shrinks.
    /// </summary>
    public virtual double GetAvatarFontSize(Size content) => 0.4 * Math.Min(content.Width, content.Height);

    /// <summary>
    /// The colors of an avatar whose background is not set: <see cref="SkUiAvatarColorMode.Default"/> draws the color
    /// scheme's <see cref="SkUiColorScheme.TrackOff"/> with <see cref="SkUiColorScheme.DefaultForeground"/> text; the other
    /// modes pick a hue from a palette by <paramref name="key"/> (the avatar's name, else its text: the same name always gets
    /// the same color, also in recycled list rows).
    /// </summary>
    public virtual SkUiAvatarColors GetAvatarColors(SkUiAvatarColorMode mode, string? key)
    {
        if (mode == SkUiAvatarColorMode.Default)
            return new SkUiAvatarColors(SkUiColors.TrackOff, SkUiColors.DefaultForeground);
        var index = AvatarPaletteIndex(key, AvatarHues.Length);
        return mode == SkUiAvatarColorMode.LightBackground ? LightAvatarColors[index] : DarkAvatarColors[index];
    }

    /// <summary>
    /// A palette index for <paramref name="key"/> that is the same in every run (FNV-1a over its upper-case letters; string
    /// hash codes change per process): looks with their own palettes use it to pick a color.
    /// </summary>
    public static int AvatarPaletteIndex(string? key, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var hash = 2166136261u;
        foreach (var character in (key ?? string.Empty).AsSpan().Trim())
        {
            hash ^= char.ToUpperInvariant(character);
            hash *= 16777619u;
        }
        return (int)(hash % (uint)count);
    }

    /// <summary>
    /// Where an avatar's badge sits in <paramref name="avatar"/> (the avatar's bounds): a circle about 28 % of the avatar's
    /// smaller side, centered on the outline's corner arc at 45° (<paramref name="cornerRadius"/>: the radius of that
    /// corner; a circle's is half its side), kept inside the bounds.
    /// </summary>
    public virtual SKRect GetAvatarBadgeBounds(SKRect avatar, SkUiAvatarBadgePosition position, double cornerRadius)
    {
        var side = Math.Min(avatar.Width, avatar.Height);
        if (side <= 0)
            return SKRect.Empty;
        var diameter = Math.Max(8, 0.28f * side);
        var radius = (float)Math.Clamp(cornerRadius, 0, side / 2);
        var inset = Math.Max(diameter / 2, radius * (1 - MathF.Sqrt(0.5f)));
        var right = position is SkUiAvatarBadgePosition.BottomRight or SkUiAvatarBadgePosition.TopRight;
        var bottom = position is SkUiAvatarBadgePosition.BottomRight or SkUiAvatarBadgePosition.BottomLeft;
        var x = right ? avatar.Right - inset : avatar.Left + inset;
        var y = bottom ? avatar.Bottom - inset : avatar.Top + inset;
        return new SKRect(x - diameter / 2, y - diameter / 2, x + diameter / 2, y + diameter / 2);
    }

    /// <summary>The width of the ring around an avatar badge <paramref name="diameter"/> DIPs wide (about 15 %, at least 1.5).</summary>
    public virtual float GetAvatarBadgeRingWidth(float diameter) => Math.Max(1.5f, 0.15f * diameter);

    /// <summary>Optional avatar badge painter; when set, replaces <see cref="DrawAvatarBadgeCore"/>.</summary>
    public Action<SKCanvas, SkUiAvatarBadgePaint>? AvatarBadgePainter { get; set; }

    /// <summary>Draws an avatar's badge (delegate or <see cref="DrawAvatarBadgeCore"/>) over its border, in <see cref="SkUiAvatarBadgePaint.Bounds"/>.</summary>
    public void DrawAvatarBadge(SKCanvas canvas, SkUiAvatarBadgePaint paint)
    {
        if (AvatarBadgePainter is { } painter)
        {
            painter(canvas, paint);
            return;
        }
        DrawAvatarBadgeCore(canvas, paint);
    }

    /// <summary>Default badge: a dot in the badge's color inside a ring of the ring color.</summary>
    protected virtual void DrawAvatarBadgeCore(SKCanvas canvas, SkUiAvatarBadgePaint paint)
    {
        var bounds = paint.Bounds;
        var radius = Math.Min(bounds.Width, bounds.Height) / 2;
        if (radius <= 0)
            return;
        using var fill = new SKPaint { IsAntialias = true };
        if (paint.RingWidth > 0 && paint.Ring.Alpha > 0)
        {
            fill.Color = paint.Ring;
            canvas.DrawCircle(bounds.MidX, bounds.MidY, radius, fill);
        }
        fill.Color = paint.Color;
        canvas.DrawCircle(bounds.MidX, bounds.MidY, Math.Max(0, radius - paint.RingWidth), fill);
    }

    #endregion

    #region Shimmer

    /// <summary>
    /// A shimmer's colors while its <c>BoneColor</c> / <c>HighlightColor</c> are not set: bones halfway between the color scheme's
    /// <see cref="SkUiColorScheme.TrackOff"/> and its background, and a wave lighter than the bones (towards white on a
    /// light background, a little towards the foreground on a dark one).
    /// </summary>
    public virtual SkUiShimmerColors GetShimmerColors()
    {
        var background = SkUiColors.DefaultBackground;
        var fill = Blend(SkUiColors.TrackOff, background, 0.5f);
        var light = 0.299f * background.Red + 0.587f * background.Green + 0.114f * background.Blue >= 0.5f;
        return new SkUiShimmerColors(fill, light ? Blend(fill, Colors.White, 0.7f) : Blend(fill, SkUiColors.DefaultForeground, 0.15f));
    }

    /// <summary>
    /// How far a shimmer's wave in <see cref="SkUiShimmerWaveDirection.Default"/> leans from upright, in degrees (default 20:
    /// its top leads); the explicit directions move straight.
    /// </summary>
    public virtual double ShimmerWaveTilt => 20;

    /// <summary>Optional shimmer wave shader factory; when set, replaces <see cref="CreateShimmerWaveShaderCore"/>.</summary>
    public Func<SkUiShimmerWavePaint, SKShader>? ShimmerWaveShader { get; set; }

    /// <summary>
    /// Makes the shader of a shimmer's wave band (delegate or <see cref="CreateShimmerWaveShaderCore"/>) for
    /// <see cref="SkUiShimmerWavePaint.Bounds"/>, x across the band in the direction it moves. It is made when the wave's
    /// settings or size change (per bone in <see cref="SkUiShimmerMode.Bones"/>) and moved on the render thread, drawn over
    /// the bones only. Outside the band it must be transparent (a gradient clamped to transparent ends, as the default, or
    /// decal): a bone fills its whole shape with it. The caller disposes the shader.
    /// </summary>
    public SKShader CreateShimmerWaveShader(SkUiShimmerWavePaint wave) =>
        ShimmerWaveShader is { } factory ? factory(wave) : CreateShimmerWaveShaderCore(wave);

    /// <summary>Optional skeleton bones provider; when set, replaces <see cref="AddSkeletonBonesCore"/>.</summary>
    public Action<SkUiSkeletonPart, IList<SkUiSkeletonBone>>? SkeletonBones { get; set; }

    /// <summary>
    /// Adds the bones a part of a control stands for in a skeleton made from a shimmer's content (delegate or
    /// <see cref="AddSkeletonBonesCore"/>): a label's lines, a button's chrome, a switch's track. Called on the UI thread when
    /// the control's size or skeleton changes, not per frame; the shimmer draws the bones with its effect.
    /// </summary>
    public void AddSkeletonBones(SkUiSkeletonPart part, IList<SkUiSkeletonBone> bones)
    {
        if (SkeletonBones is { } provider)
            provider(part, bones);
        else
            AddSkeletonBonesCore(part, bones);
    }

    /// <summary>
    /// Default bones: shapes as they are; a text line as a bar 60 % of its spacing high, centered on it, with 4 DIP corners;
    /// a switch's track as a pill; a check box's box with the default box's corners; a radio button's and an activity
    /// indicator's circle; a slider's and a progress bar's track as a pill (a slider's as high as its thumb's radius).
    /// </summary>
    protected virtual void AddSkeletonBonesCore(SkUiSkeletonPart part, IList<SkUiSkeletonBone> bones)
    {
        var bounds = part.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        switch (part.Kind)
        {
            case SkUiSkeletonPartKind.TextLine:
            {
                var height = Math.Max(2, bounds.Height * 0.6f);
                var top = bounds.MidY - height / 2;
                bones.Add(new SkUiSkeletonBone(new SKRect(bounds.Left, top, bounds.Right, top + height), new CornerRadius(Math.Min(4, height / 2))));
                break;
            }
            case SkUiSkeletonPartKind.Switch:
                bones.Add(new SkUiSkeletonBone(bounds, new CornerRadius(bounds.Height / 2)));
                break;
            case SkUiSkeletonPartKind.CheckBox:
                bones.Add(new SkUiSkeletonBone(bounds, new CornerRadius(Math.Min(bounds.Width, bounds.Height) * 0.2)));
                break;
            case SkUiSkeletonPartKind.RadioButton or SkUiSkeletonPartKind.ActivityIndicator:
            {
                var diameter = Math.Min(bounds.Width, bounds.Height);
                var circle = SKRect.Create(bounds.MidX - diameter / 2, bounds.MidY - diameter / 2, diameter, diameter);
                bones.Add(new SkUiSkeletonBone(circle, new CornerRadius(diameter / 2)));
                break;
            }
            case SkUiSkeletonPartKind.Slider:
            {
                var height = Math.Min(bounds.Height, SliderThumbRadius);
                var inset = Math.Min(bounds.Width / 2, SliderThumbRadius - height / 2);
                var track = new SKRect(bounds.Left + inset, bounds.MidY - height / 2, bounds.Right - inset, bounds.MidY + height / 2);
                bones.Add(new SkUiSkeletonBone(track, new CornerRadius(height / 2)));
                break;
            }
            case SkUiSkeletonPartKind.ProgressBar:
                bones.Add(new SkUiSkeletonBone(bounds, new CornerRadius(GetProgressBarCornerRadius(bounds.Height))));
                break;
            default:
                bones.Add(new SkUiSkeletonBone(bounds, part.CornerRadius));
                break;
        }
    }

    /// <summary>Default wave: a soft band, transparent at both edges and <see cref="SkUiShimmerWavePaint.Color"/> in the middle.</summary>
    protected virtual SKShader CreateShimmerWaveShaderCore(SkUiShimmerWavePaint wave)
    {
        var bounds = wave.Bounds;
        return SKShader.CreateLinearGradient(new SKPoint(bounds.Left, 0), new SKPoint(Math.Max(bounds.Left + 1, bounds.Right), 0),
            [wave.Color.WithAlpha(0), wave.Color, wave.Color.WithAlpha(0)], [0, 0.5f, 1], SKShaderTileMode.Clamp);
    }

    #endregion

    #region Image

    /// <summary>Optional image painter.</summary>
    public Action<SKCanvas, SKImage, float, float, Aspect>? ImagePainter { get; set; }

    /// <summary>Optional press-overlay painter (press feedback over content, ImageButton disabled dimming); replaces <see cref="DrawPressOverlayCore"/>.</summary>
    public Action<SKCanvas, SkUiPressOverlayPaint>? PressOverlayPainter { get; set; }

    /// <summary>Draws an image with aspect fit / fill / stretch / center.</summary>
    public void DrawImage(SKCanvas canvas, SKImage image, float viewWidth, float viewHeight, Aspect aspect)
    {
        if (ImagePainter is { } painter)
        {
            painter(canvas, image, viewWidth, viewHeight, aspect);
            return;
        }
        DrawImageCore(canvas, image, viewWidth, viewHeight, aspect);
    }

    /// <summary>Default image destination drawing.</summary>
    protected virtual void DrawImageCore(SKCanvas canvas, SKImage image, float viewWidth, float viewHeight, Aspect aspect) { }

    /// <summary>Computes the destination rect for an image under the given aspect (<see cref="Aspect.Center"/>: unscaled, centered).</summary>
    public virtual SKRect ComputeImageDestination(float viewWidth, float viewHeight, float imageWidth, float imageHeight, Aspect aspect)
    {
        if (aspect == Aspect.Center)
            return SKRect.Create((viewWidth - imageWidth) / 2, (viewHeight - imageHeight) / 2, imageWidth, imageHeight);
        var scale = aspect == Aspect.AspectFill
            ? Math.Max(viewWidth / imageWidth, viewHeight / imageHeight)
            : Math.Min(viewWidth / imageWidth, viewHeight / imageHeight);
        var width = aspect == Aspect.Fill ? viewWidth : imageWidth * scale;
        var height = aspect == Aspect.Fill ? viewHeight : imageHeight * scale;
        return SKRect.Create(
            (viewWidth - width) / 2,
            (viewHeight - height) / 2,
            width,
            height);
    }

    /// <summary>
    /// Draws press feedback over a control's content (ImageButton, or any node with <c>ShowsPressEffect</c>), every frame
    /// of its transitions; also an ImageButton's disabled dimming.
    /// </summary>
    public void DrawPressOverlay(SKCanvas canvas, SkUiPressOverlayPaint overlay)
    {
        if (PressOverlayPainter is { } painter)
        {
            painter(canvas, overlay);
            return;
        }
        DrawPressOverlayCore(canvas, overlay);
    }

    /// <summary>Default press-overlay geometry.</summary>
    protected virtual void DrawPressOverlayCore(SKCanvas canvas, SkUiPressOverlayPaint overlay) { }

    /// <summary>Optional focus-ring painter; replaces <see cref="DrawFocusRingCore"/>.</summary>
    public Action<SKCanvas, SkUiFocusRingPaint>? FocusRingPainter { get; set; }

    /// <summary>
    /// Draws the keyboard focus ring over the focused drawn control (any control of either layer that takes keyboard focus),
    /// while focus came from the keyboard (Tab, or programmatic focus after a key press).
    /// </summary>
    public void DrawFocusRing(SKCanvas canvas, SkUiFocusRingPaint ring)
    {
        if (FocusRingPainter is { } painter)
        {
            painter(canvas, ring);
            return;
        }
        DrawFocusRingCore(canvas, ring);
    }

    /// <summary>Default focus-ring geometry.</summary>
    protected virtual void DrawFocusRingCore(SKCanvas canvas, SkUiFocusRingPaint ring) { }

    #endregion
}
