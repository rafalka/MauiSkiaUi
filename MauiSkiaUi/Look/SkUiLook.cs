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
        get => _current ??= DefaultSkUiLook.Instance;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_current, value)) return;
            _current = value;
            _currentChanged.Raise(null, EventArgs.Empty);
        }
    }

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
        var tl = (float)Math.Max(0, radii.TopLeft);
        var tr = (float)Math.Max(0, radii.TopRight);
        var bl = (float)Math.Max(0, radii.BottomLeft);
        var br = (float)Math.Max(0, radii.BottomRight);
        using var roundRect = new SKRoundRect();
        roundRect.SetRectRadii(bounds, [
            new SKPoint(tl, tl),
            new SKPoint(tr, tr),
            new SKPoint(br, br),
            new SKPoint(bl, bl),
        ]);
        using var builder = new SKPathBuilder();
        // Start where MAUI's rectangle paths start, clockwise: on the left edge below the top-left corner (at the corner
        // when it is square), so dashed borders lay out their pattern as MAUI's.
        builder.AddRoundRect(roundRect, SKPathDirection.Clockwise, 7);
        return builder.Detach();
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

    #endregion

    #region Button

    /// <summary>Default button minimum height in DIPs.</summary>
    public virtual double DefaultButtonMinimumHeight => 44;

    /// <summary>Default button corner radius in DIPs.</summary>
    public virtual double DefaultButtonCornerRadius => 6;

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

    /// <summary>Optional CheckBox intrinsic measure override.</summary>
    public Func<double, double, Size>? CheckBoxMeasure { get; set; }

    /// <summary>Optional CheckBox painter; when set, replaces <see cref="DrawCheckBoxCore"/>.</summary>
    public Action<SKCanvas, SkUiCheckBoxPaint>? CheckBoxPainter { get; set; }

    /// <summary>Intrinsic CheckBox size in DIPs.</summary>
    public Size MeasureCheckBox(double widthConstraint, double heightConstraint) =>
        CheckBoxMeasure?.Invoke(widthConstraint, heightConstraint)
        ?? MeasureCheckBoxCore(widthConstraint, heightConstraint);

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

    #endregion
}
