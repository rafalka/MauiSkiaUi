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
/// </remarks>
public class SkUiLook
{
    #region Current

    private static SkUiLook? _current;

    /// <summary>Raised after <see cref="Current"/> is replaced.</summary>
    public static event EventHandler? CurrentChanged;

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
            CurrentChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    #endregion

    #region Shared geometry

    /// <summary>Optional uniform rounded-box painter (single radius for all corners).</summary>
    public Action<SKCanvas, SKRect, float, SKColor, SKColor, float>? RoundedBoxPainter { get; set; }

    /// <summary>Optional per-corner rounded-box painter.</summary>
    public Action<SKCanvas, SKRect, CornerRadius, SKColor, SKColor, float>? RoundedBoxCornersPainter { get; set; }

    /// <summary>Builds a rounded-rect path with a uniform corner radius for fill/border/clip.</summary>
    public virtual SKPath CreateRoundRectPath(SKRect bounds, float radius) =>
        CreateRoundRectPath(bounds, new CornerRadius(radius));

    /// <summary>Builds a rounded-rect path with independent corner radii for fill/border/clip.</summary>
    /// <remarks>
    /// Corner order matches MAUI <see cref="CornerRadius"/>: top-left, top-right, bottom-left, bottom-right.
    /// Skia’s rect-radii order is TL, TR, BR, BL; this method remaps accordingly.
    /// </remarks>
    public virtual SKPath CreateRoundRectPath(SKRect bounds, CornerRadius radii)
    {
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
        builder.AddRoundRect(roundRect);
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

    #endregion

    #region Switch

    /// <summary>Default Switch intrinsic size in DIPs.</summary>
    public virtual Size DefaultSwitchSize => new(51, 31);

    /// <summary>Optional Switch intrinsic measure override.</summary>
    public Func<double, double, Size>? SwitchMeasure { get; set; }

    /// <summary>Optional Switch painter; when set, replaces <see cref="DrawSwitchCore"/>.</summary>
    public Action<SKCanvas, SKRect, bool, SKColor, SKColor>? SwitchPainter { get; set; }

    /// <summary>Intrinsic Switch size in DIPs.</summary>
    public Size MeasureSwitch(double widthConstraint, double heightConstraint) =>
        SwitchMeasure?.Invoke(widthConstraint, heightConstraint)
        ?? MeasureSwitchCore(widthConstraint, heightConstraint);

    /// <summary>Default Switch size from <see cref="DefaultSwitchSize"/>.</summary>
    protected virtual Size MeasureSwitchCore(double widthConstraint, double heightConstraint) => DefaultSwitchSize;

    /// <summary>Draws Switch chrome (delegate or <see cref="DrawSwitchCore"/>).</summary>
    public void DrawSwitch(SKCanvas canvas, SKRect bounds, bool isChecked, SKColor track, SKColor thumb)
    {
        if (SwitchPainter is { } painter)
        {
            painter(canvas, bounds, isChecked, track, thumb);
            return;
        }
        DrawSwitchCore(canvas, bounds, isChecked, track, thumb);
    }

    /// <summary>Default Switch geometry.</summary>
    protected virtual void DrawSwitchCore(SKCanvas canvas, SKRect bounds, bool isChecked, SKColor track, SKColor thumb) { }

    #endregion

    #region CheckBox

    /// <summary>Default CheckBox intrinsic size in DIPs (square side length on both axes).</summary>
    public virtual Size DefaultCheckBoxSize => new(24, 24);

    /// <summary>Optional CheckBox intrinsic measure override.</summary>
    public Func<double, double, Size>? CheckBoxMeasure { get; set; }

    /// <summary>Optional CheckBox painter.</summary>
    public Action<SKCanvas, float, bool, SKColor, SKColor>? CheckBoxPainter { get; set; }

    /// <summary>Intrinsic CheckBox size in DIPs.</summary>
    public Size MeasureCheckBox(double widthConstraint, double heightConstraint) =>
        CheckBoxMeasure?.Invoke(widthConstraint, heightConstraint)
        ?? MeasureCheckBoxCore(widthConstraint, heightConstraint);

    /// <summary>Default CheckBox size from <see cref="DefaultCheckBoxSize"/>.</summary>
    protected virtual Size MeasureCheckBoxCore(double widthConstraint, double heightConstraint) => DefaultCheckBoxSize;

    /// <summary>Draws CheckBox chrome.</summary>
    public void DrawCheckBox(SKCanvas canvas, float size, bool isChecked, SKColor fill, SKColor border)
    {
        if (CheckBoxPainter is { } painter)
        {
            painter(canvas, size, isChecked, fill, border);
            return;
        }
        DrawCheckBoxCore(canvas, size, isChecked, fill, border);
    }

    /// <summary>Default CheckBox geometry.</summary>
    protected virtual void DrawCheckBoxCore(SKCanvas canvas, float size, bool isChecked, SKColor fill, SKColor border) { }

    #endregion

    #region RadioButton

    /// <summary>Default RadioButton intrinsic size in DIPs (square side length on both axes).</summary>
    public virtual Size DefaultRadioButtonSize => new(24, 24);

    /// <summary>Optional RadioButton intrinsic measure override.</summary>
    public Func<double, double, Size>? RadioButtonMeasure { get; set; }

    /// <summary>Optional RadioButton painter.</summary>
    public Action<SKCanvas, float, bool, SKColor, SKColor>? RadioButtonPainter { get; set; }

    /// <summary>Intrinsic RadioButton size in DIPs.</summary>
    public Size MeasureRadioButton(double widthConstraint, double heightConstraint) =>
        RadioButtonMeasure?.Invoke(widthConstraint, heightConstraint)
        ?? MeasureRadioButtonCore(widthConstraint, heightConstraint);

    /// <summary>Default RadioButton size from <see cref="DefaultRadioButtonSize"/>.</summary>
    protected virtual Size MeasureRadioButtonCore(double widthConstraint, double heightConstraint) => DefaultRadioButtonSize;

    /// <summary>Draws RadioButton chrome.</summary>
    public void DrawRadioButton(SKCanvas canvas, float size, bool isChecked, SKColor ring, SKColor dot)
    {
        if (RadioButtonPainter is { } painter)
        {
            painter(canvas, size, isChecked, ring, dot);
            return;
        }
        DrawRadioButtonCore(canvas, size, isChecked, ring, dot);
    }

    /// <summary>Default RadioButton geometry.</summary>
    protected virtual void DrawRadioButtonCore(SKCanvas canvas, float size, bool isChecked, SKColor ring, SKColor dot) { }

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

    /// <summary>Draws ActivityIndicator chrome.</summary>
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

    #region Image

    /// <summary>Optional image painter.</summary>
    public Action<SKCanvas, SKImage, float, float, Aspect>? ImagePainter { get; set; }

    /// <summary>Optional press-tint painter (ImageButton pressed/disabled chrome).</summary>
    public Action<SKCanvas, SKRect, float, bool, bool>? PressTintPainter { get; set; }

    /// <summary>Draws an image with aspect fit/fill.</summary>
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

    /// <summary>Computes the destination rect for an image under the given aspect.</summary>
    public virtual SKRect ComputeImageDestination(float viewWidth, float viewHeight, float imageWidth, float imageHeight, Aspect aspect)
    {
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

    /// <summary>Draws ImageButton pressed/disabled tint.</summary>
    public void DrawPressTint(SKCanvas canvas, SKRect bounds, float cornerRadius, bool disabled, bool pressed)
    {
        if (PressTintPainter is { } painter)
        {
            painter(canvas, bounds, cornerRadius, disabled, pressed);
            return;
        }
        DrawPressTintCore(canvas, bounds, cornerRadius, disabled, pressed);
    }

    /// <summary>Default press-tint geometry.</summary>
    protected virtual void DrawPressTintCore(SKCanvas canvas, SKRect bounds, float cornerRadius, bool disabled, bool pressed) { }

    #endregion
}
