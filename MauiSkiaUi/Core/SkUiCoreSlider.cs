using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Drawn slider (Core analogue of <c>SkUiSlider</c>): horizontal or vertical (minimum at the bottom), drawn by
/// <see cref="SkUiLook.DrawSlider"/>. A drag along the slider moves the value; a tap moves it to the tapped position.
/// </summary>
public class SkUiCoreSlider : SkUiCoreNode
{
    private double _minimum;
    private double _maximum = 1;
    private double _value;
    private StackOrientation _orientation = StackOrientation.Horizontal;
    private Color _minimumTrackColor = SkUiColors.Accent;
    private Color _maximumTrackColor = SkUiColors.TrackOff;
    private Color _thumbColor = SkUiColors.Accent;
    private SkUiSliderGestureRecognizer? _gesture;
    private double _requestedValue;
    private SkUiSliderVisual? _visual;
    private bool _animateThumb;
    private SkUiImageSource? _thumbImageSource;
    private SkUiImageSlot? _thumbImage;

    /// <summary>Smallest value (default 0).</summary>
    public double Minimum { get => _minimum; set => SetMinimum(value); }

    /// <summary>Largest value (default 1).</summary>
    public double Maximum { get => _maximum; set => SetMaximum(value); }

    /// <summary>
    /// Current value, clamped between <see cref="Minimum"/> and <see cref="Maximum"/>; the requested value is kept, so
    /// it comes back when the range widens again (as with <see cref="SkUiSlider"/>).
    /// </summary>
    public double Value { get => _value; set => SetValue(value); }

    /// <summary>Horizontal (default; minimum at the start) or vertical (minimum at the bottom).</summary>
    public StackOrientation Orientation { get => _orientation; set => SetOrientation(value); }

    /// <summary>Track color between the minimum and the thumb.</summary>
    public Color MinimumTrackColor { get => _minimumTrackColor; set => SetMinimumTrackColor(value); }

    /// <summary>Track color between the thumb and the maximum.</summary>
    public Color MaximumTrackColor { get => _maximumTrackColor; set => SetMaximumTrackColor(value); }

    /// <summary>Thumb color.</summary>
    public Color ThumbColor { get => _thumbColor; set => SetThumbColor(value); }

    /// <summary>
    /// Image drawn instead of the look's thumb, at its intrinsic size, upright (MAUI's <c>ThumbImageSource</c>); loaded
    /// through the shared image cache.
    /// </summary>
    public SkUiImageSource? ThumbImageSource { get => _thumbImageSource; set => SetThumbImageSource(value); }

    /// <summary>Whether the thumb is being dragged.</summary>
    public bool IsDragging => _gesture?.IsDragging == true;

    /// <summary>Raised when <see cref="Value"/> changes, from code or from input.</summary>
    public event EventHandler<ValueChangedEventArgs>? ValueChanged;

    /// <summary>Raised when a drag starts.</summary>
    public event EventHandler? DragStarted;

    /// <summary>Raised when a drag ends.</summary>
    public event EventHandler? DragCompleted;

    /// <summary>Sets the minimum (the value is clamped into the new range).</summary>
    public SkUiCoreSlider SetMinimum(double value)
    {
        if (!SetProperty(ref _minimum, value, nameof(Minimum))) return this;
        InvalidatePaint();
        return ApplyValue(_requestedValue);
    }

    /// <summary>Sets the maximum (the value is clamped into the new range).</summary>
    public SkUiCoreSlider SetMaximum(double value)
    {
        if (!SetProperty(ref _maximum, value, nameof(Maximum))) return this;
        InvalidatePaint();
        return ApplyValue(_requestedValue);
    }

    /// <summary>Sets the value, clamped to the range.</summary>
    public SkUiCoreSlider SetValue(double value)
    {
        _requestedValue = value;
        return ApplyValue(value);
    }

    private SkUiCoreSlider ApplyValue(double value)
    {
        var old = _value;
        if (!SetProperty(ref _value, SkUiSliderMath.Clamp(value, _minimum, _maximum), nameof(Value))) return this;
        if (_animateThumb || _visual is not null)
            SliderVisual.Moved(SkUiSliderMath.Fraction(old, _minimum, _maximum), SkUiSliderMath.Fraction(_value, _minimum, _maximum), _animateThumb);
        InvalidatePaint();
        ValueChanged?.Invoke(this, new ValueChangedEventArgs(old, _value));
        return this;
    }

    /// <summary>Sets the orientation.</summary>
    public SkUiCoreSlider SetOrientation(StackOrientation value)
    {
        if (!SetProperty(ref _orientation, value, nameof(Orientation))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets the minimum-track color.</summary>
    public SkUiCoreSlider SetMinimumTrackColor(Color value) => SetColor(ref _minimumTrackColor, value, nameof(MinimumTrackColor));

    /// <summary>Sets the maximum-track color.</summary>
    public SkUiCoreSlider SetMaximumTrackColor(Color value) => SetColor(ref _maximumTrackColor, value, nameof(MaximumTrackColor));

    /// <summary>Sets the thumb color.</summary>
    public SkUiCoreSlider SetThumbColor(Color value) => SetColor(ref _thumbColor, value, nameof(ThumbColor));

    /// <summary>Sets the thumb image (<c>null</c>: the look's thumb).</summary>
    public SkUiCoreSlider SetThumbImageSource(SkUiImageSource? value)
    {
        if (!SetProperty(ref _thumbImageSource, value, nameof(ThumbImageSource))) return this;
        (_thumbImage ??= new SkUiImageSlot(this, () => InvalidatePaint())).Load(value, default);
        return this;
    }

    private SkUiCoreSlider SetColor(ref Color field, Color value, string name)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref field, value, name))
            InvalidatePaint();
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureSlider(widthConstraint, heightConstraint, _orientation);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var fraction = SkUiSliderMath.Fraction(_value, _minimum, _maximum);
        SkUiSliderMath.Draw(canvas, (float)Frame.Width, (float)Frame.Height, _orientation, IsRightToLeft, _visual?.Fraction(fraction) ?? fraction,
            ToSkColor(_minimumTrackColor), ToSkColor(_maximumTrackColor), ToSkColor(_thumbColor), _visual?.Pressed ?? 0, enabled: true, _thumbImage);
    }

    private SkUiSliderVisual SliderVisual => _visual ??= new SkUiSliderVisual(this);

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        base.CollectGestureRecognizers(recognizers);
        recognizers.Add(_gesture ??= new SkUiSliderGestureRecognizer
        {
            Orientation = () => _orientation,
            Began = position =>
            {
                SliderVisual.SetDragging(true);
                InvalidatePaint();
                DragStarted?.Invoke(this, EventArgs.Empty);
                SetValueAt(position);
            },
            Moved = SetValueAt,
            Ended = () =>
            {
                SliderVisual.SetDragging(false);
                InvalidatePaint();
                DragCompleted?.Invoke(this, EventArgs.Empty);
            },
            Tapped = position =>
            {
                // The thumb glides to a tapped value (the value itself changes at once).
                _animateThumb = true;
                try { SetValueAt(position); }
                finally { _animateThumb = false; }
            }
        });
    }

    private void SetValueAt(Point position) =>
        SetValue(SkUiSliderMath.ValueAt(position, Frame.Size, _orientation, IsRightToLeft, _minimum, _maximum));

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.Slider;
        info.IsHorizontal = _orientation == StackOrientation.Horizontal;
        // Maximum <= Minimum is a slider fixed at Minimum: an ordered, empty range that cannot be stepped.
        if (_maximum <= _minimum)
        {
            info.Range = new SkUiSemanticsRange(_minimum, _minimum, _minimum);
            return;
        }
        info.Range = new SkUiSemanticsRange(_minimum, _maximum, SkUiSliderMath.Clamp(_value, _minimum, _maximum));
        info.Actions |= SkUiSemanticsActions.Increment | SkUiSemanticsActions.Decrement;
    }

    /// <inheritdoc />
    protected override bool OnSemanticsAction(SkUiSemanticsActions action) => action switch
    {
        SkUiSemanticsActions.Increment => OnSemanticsSetValue(_value + SkUiSemantics.RangeStep(_minimum, _maximum)),
        SkUiSemanticsActions.Decrement => OnSemanticsSetValue(_value - SkUiSemantics.RangeStep(_minimum, _maximum)),
        _ => base.OnSemanticsAction(action)
    };

    /// <inheritdoc />
    protected override bool OnSemanticsSetValue(double value)
    {
        var clamped = SkUiSliderMath.Clamp(value, _minimum, _maximum);
        if (clamped == _value)
            return false;
        SetValue(clamped);
        return true;
    }

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;
}
