using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>Value mapping and drawing shared by <see cref="SkUiSlider"/> and <see cref="Core.SkUiCoreSlider"/>.</summary>
internal static class SkUiSliderMath
{
    public static double Clamp(double value, double minimum, double maximum) =>
        maximum <= minimum ? minimum : Math.Clamp(value, minimum, maximum);

    public static float Fraction(double value, double minimum, double maximum) =>
        maximum <= minimum ? 0 : (float)((Clamp(value, minimum, maximum) - minimum) / (maximum - minimum));

    /// <summary>
    /// The value under a point in the slider's own coordinates: the thumb's center travels from
    /// <see cref="SkUiLook.SliderThumbRadius"/> to the length minus it, bottom to top for vertical sliders and right to
    /// left in right-to-left layouts.
    /// </summary>
    public static double ValueAt(Point local, Size size, StackOrientation orientation, bool rightToLeft, double minimum, double maximum)
    {
        var vertical = orientation == StackOrientation.Vertical;
        var length = vertical ? size.Height : size.Width;
        var along = vertical ? size.Height - local.Y : rightToLeft ? size.Width - local.X : local.X;
        var inset = SkUiLook.Current.SliderThumbRadius;
        var usable = length - 2 * inset;
        var fraction = usable <= 0 ? 0 : Math.Clamp((along - inset) / usable, 0, 1);
        return minimum + (Math.Max(minimum, maximum) - minimum) * fraction;
    }

    /// <summary>Draws through the look in horizontal left-to-right coordinates (rotated / mirrored as needed).</summary>
    public static void Draw(SKCanvas canvas, float width, float height, StackOrientation orientation, bool rightToLeft,
        float fraction, SKColor minimumTrack, SKColor maximumTrack, SKColor thumb, float pressed, bool enabled)
    {
        var save = canvas.Save();
        SKRect bounds;
        if (orientation == StackOrientation.Vertical)
        {
            // Rotated -90° about the bottom-left corner: X runs up the slider, Y across it.
            canvas.Translate(0, height);
            canvas.RotateDegrees(-90);
            bounds = new SKRect(0, 0, height, width);
        }
        else
        {
            if (rightToLeft)
                canvas.Scale(-1, 1, width / 2, 0);
            bounds = new SKRect(0, 0, width, height);
        }
        SkUiLook.Current.DrawSlider(canvas, new SkUiSliderPaint(bounds, fraction, orientation, minimumTrack, maximumTrack, thumb, pressed, enabled));
        canvas.RestoreToCount(save);
    }
}

/// <summary>
/// Slider transitions shared by <see cref="SkUiSlider"/> and <see cref="Core.SkUiCoreSlider"/>: the thumb glides to a
/// tapped value (<see cref="SkUiTransitionKind.SliderThumb"/>; drags and code follow at once) and the press halo fades
/// in and out with dragging.
/// </summary>
internal sealed class SkUiSliderVisual(ISkUiTransitionHost host)
{
    private readonly SkUiTween _thumb = new(host);
    private readonly SkUiTween _pressed = new(host);

    /// <summary>The fraction to draw for the value's <paramref name="fraction"/>.</summary>
    public float Fraction(float fraction) => _thumb.IsRunning ? _thumb.Value : fraction;

    /// <summary>The press amount to draw.</summary>
    public float Pressed => _pressed.Value;

    /// <summary>The value moved from <paramref name="from"/> to <paramref name="to"/> (fractions).</summary>
    public void Moved(float from, float to, bool animate)
    {
        if (!animate)
        {
            _thumb.Jump(to);
            return;
        }
        if (!_thumb.IsRunning)
            _thumb.Jump(from);
        _thumb.AnimateTo(to, SkUiLook.Current.GetTransition(SkUiTransitionKind.SliderThumb));
    }

    /// <summary>A drag started or ended; a drag takes over from a glide at once (the thumb follows the finger).</summary>
    public void SetDragging(bool dragging)
    {
        if (dragging && _thumb.IsRunning)
            _thumb.Jump(_thumb.Target);
        _pressed.AnimateTo(dragging ? 1 : 0, SkUiLook.Current.GetTransition(dragging ? SkUiTransitionKind.Press : SkUiTransitionKind.Release));
    }
}

/// <summary>
/// Slider input: a drag along the slider's axis claims the pointer once it passes the touch slop (so a vertical drag
/// on a horizontal slider still scrolls its page), then follows the pointer; a tap moves the value to the tapped
/// position. Continuous, so it also holds off native ancestors while the drag may still become a slide.
/// </summary>
internal sealed class SkUiSliderGestureRecognizer : SkUiGestureRecognizer
{
    private long? _pointer;
    private Point _start;
    private bool _active;

    internal required Func<StackOrientation> Orientation { get; init; }
    internal required Action<Point> Began { get; init; }
    internal required Action<Point> Moved { get; init; }
    internal required Action Ended { get; init; }
    internal required Action<Point> Tapped { get; init; }

    /// <summary>Whether a drag is in progress.</summary>
    internal bool IsDragging => _active;

    /// <inheritdoc />
    protected internal override bool IsExclusive => true;

    /// <inheritdoc />
    protected internal override bool OnPointerPressed(SkUiPointer pointer)
    {
        if (_pointer is not null)
            return false;
        _pointer = pointer.Id;
        _start = pointer.Position;
        _active = false;
        return true;
    }

    /// <inheritdoc />
    protected internal override void OnPointerMoved(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        var local = Local(pointer);
        if (_active)
        {
            Moved(local);
            return;
        }
        var dx = pointer.Position.X - _start.X;
        var dy = pointer.Position.Y - _start.Y;
        var vertical = Orientation() == StackOrientation.Vertical;
        if (ExceedsSlop(dx, dy, vertical ? SkUiPanAxis.Vertical : SkUiPanAxis.Horizontal))
        {
            Claim();
            if (_pointer != pointer.Id)
                return;
            _active = true;
            Began(local);
        }
        else if (ExceedsSlop(dx, dy, vertical ? SkUiPanAxis.Horizontal : SkUiPanAxis.Vertical))
        {
            Resign(); // a drag across the slider belongs to scrollers around it
        }
    }

    /// <inheritdoc />
    protected internal override void OnPointerReleased(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        var local = Local(pointer);
        if (_active)
        {
            Moved(local);
            Finish();
            return;
        }
        _pointer = null;
        if (IsInsideOwner(local))
        {
            Claim();
            Tapped(local);
        }
        else
        {
            Resign();
        }
    }

    /// <inheritdoc />
    protected internal override void OnRejected(long pointerId)
    {
        if (_pointer != pointerId)
            return;
        if (_active)
            Finish();
        _pointer = null;
    }

    private void Finish()
    {
        _active = false;
        _pointer = null;
        Ended();
    }

    private Point Local(SkUiPointer pointer) => Owner is { } owner ? pointer.GetPosition(owner) : pointer.Position;
}
