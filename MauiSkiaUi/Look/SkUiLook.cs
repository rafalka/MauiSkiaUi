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

    /// <summary>Optional Switch painter; when set, replaces <see cref="DrawSwitchCore"/>.</summary>
    public Action<SKCanvas, SKRect, bool, SKColor, SKColor>? SwitchPainter { get; set; }

    /// <summary>Optional CheckBox painter.</summary>
    public Action<SKCanvas, float, bool, SKColor, SKColor>? CheckBoxPainter { get; set; }

    /// <summary>Optional RadioButton painter.</summary>
    public Action<SKCanvas, float, bool, SKColor, SKColor>? RadioButtonPainter { get; set; }

    /// <summary>Optional ActivityIndicator painter.</summary>
    public Action<SKCanvas, float, float, float, SKPaint>? ActivityIndicatorPainter { get; set; }

    /// <summary>Optional rounded-box painter.</summary>
    public Action<SKCanvas, SKRect, float, SKColor, SKColor, float>? RoundedBoxPainter { get; set; }

    /// <summary>Optional press-tint painter.</summary>
    public Action<SKCanvas, SKRect, float, bool, bool>? PressTintPainter { get; set; }

    /// <summary>Optional image painter.</summary>
    public Action<SKCanvas, SKImage, float, float, Aspect>? ImagePainter { get; set; }

    /// <summary>Optional Switch intrinsic measure override.</summary>
    public Func<double, double, Size>? SwitchMeasure { get; set; }

    /// <summary>Optional CheckBox intrinsic measure override.</summary>
    public Func<double, double, Size>? CheckBoxMeasure { get; set; }

    /// <summary>Optional RadioButton intrinsic measure override.</summary>
    public Func<double, double, Size>? RadioButtonMeasure { get; set; }

    /// <summary>Optional ActivityIndicator intrinsic measure override.</summary>
    public Func<double, double, Size>? ActivityIndicatorMeasure { get; set; }

    /// <summary>Default button minimum height in DIPs.</summary>
    public virtual double DefaultButtonMinimumHeight => 44;

    /// <summary>Default button corner radius in DIPs.</summary>
    public virtual double DefaultButtonCornerRadius => 6;

    /// <summary>Builds a rounded-rect path for fill/border/clip.</summary>
    public virtual SKPath CreateRoundRectPath(SKRect bounds, float radius)
    {
        using var roundRect = new SKRoundRect(bounds, radius, radius);
        using var builder = new SKPathBuilder();
        builder.AddRoundRect(roundRect);
        return builder.Detach();
    }

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

    /// <summary>Fills/strokes a rounded rectangle.</summary>
    public void DrawRoundedBox(SKCanvas canvas, SKRect bounds, float radius, SKColor fill, SKColor border, float width)
    {
        if (RoundedBoxPainter is { } painter)
        {
            painter(canvas, bounds, radius, fill, border, width);
            return;
        }
        DrawRoundedBoxCore(canvas, bounds, radius, fill, border, width);
    }

    /// <summary>Default rounded-box geometry.</summary>
    protected virtual void DrawRoundedBoxCore(SKCanvas canvas, SKRect bounds, float radius, SKColor fill, SKColor border, float width) { }

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

    /// <summary>Intrinsic Switch size in DIPs.</summary>
    public Size MeasureSwitch(double widthConstraint, double heightConstraint) =>
        SwitchMeasure?.Invoke(widthConstraint, heightConstraint)
        ?? MeasureSwitchCore(widthConstraint, heightConstraint);

    /// <summary>Default Switch size.</summary>
    protected virtual Size MeasureSwitchCore(double widthConstraint, double heightConstraint) => new(51, 31);

    /// <summary>Intrinsic CheckBox size in DIPs.</summary>
    public Size MeasureCheckBox(double widthConstraint, double heightConstraint) =>
        CheckBoxMeasure?.Invoke(widthConstraint, heightConstraint)
        ?? MeasureCheckBoxCore(widthConstraint, heightConstraint);

    /// <summary>Default CheckBox size.</summary>
    protected virtual Size MeasureCheckBoxCore(double widthConstraint, double heightConstraint) => new(24, 24);

    /// <summary>Intrinsic RadioButton size in DIPs.</summary>
    public Size MeasureRadioButton(double widthConstraint, double heightConstraint) =>
        RadioButtonMeasure?.Invoke(widthConstraint, heightConstraint)
        ?? MeasureRadioButtonCore(widthConstraint, heightConstraint);

    /// <summary>Default RadioButton size.</summary>
    protected virtual Size MeasureRadioButtonCore(double widthConstraint, double heightConstraint) => new(24, 24);

    /// <summary>Intrinsic ActivityIndicator size in DIPs.</summary>
    public Size MeasureActivityIndicator(double widthConstraint, double heightConstraint) =>
        ActivityIndicatorMeasure?.Invoke(widthConstraint, heightConstraint)
        ?? MeasureActivityIndicatorCore(widthConstraint, heightConstraint);

    /// <summary>Default ActivityIndicator size.</summary>
    protected virtual Size MeasureActivityIndicatorCore(double widthConstraint, double heightConstraint) => new(36, 36);
}
