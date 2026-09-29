using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn slider, similar to MAUI's Slider, plus <see cref="Orientation"/> for vertical sliders (minimum at the
/// bottom). Drawn by <see cref="SkUiLook.DrawSlider"/>. A drag along the slider moves the value; a tap moves it to
/// the tapped position. User changes are written back to <see cref="Value"/>, so two-way bindings see them.
/// </summary>
public class SkUiSlider : SkUiView
{
    private double _minimum;
    private double _maximum = 1;
    private double _value;
    private StackOrientation _orientation = StackOrientation.Horizontal;
    private Color _minimumTrackColor = SkUiColors.Accent;
    private Color _maximumTrackColor = SkUiColors.TrackOff;
    private Color _thumbColor = SkUiColors.Accent;
    private ICommand? _dragStartedCommand;
    private ICommand? _dragCompletedCommand;
    private SkUiSliderGestureRecognizer? _gesture;
    private double _requestedValue;
    private bool _recoercing;

    /// <summary>Bindable <see cref="Minimum"/>.</summary>
    public static readonly BindableProperty MinimumProperty = BindableProperty.Create(nameof(Minimum), typeof(double), typeof(SkUiSlider), 0d,
        propertyChanged: (view, _, value) => ((SkUiSlider)view).OnRangeChanged(minimum: (double)value));

    /// <summary>Bindable <see cref="Maximum"/>.</summary>
    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(nameof(Maximum), typeof(double), typeof(SkUiSlider), 1d,
        propertyChanged: (view, _, value) => ((SkUiSlider)view).OnRangeChanged(maximum: (double)value));

    /// <summary>Bindable <see cref="Value"/> (two-way by default, clamped to the range).</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(double), typeof(SkUiSlider), 0d, BindingMode.TwoWay,
        coerceValue: (view, value) => ((SkUiSlider)view).CoerceValue((double)value),
        propertyChanged: (view, _, value) => ((SkUiSlider)view).ApplyValue((double)value));

    /// <summary>Bindable <see cref="Orientation"/>.</summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(StackOrientation), typeof(SkUiSlider),
        StackOrientation.Horizontal, propertyChanged: (view, _, value) => ((SkUiSlider)view).SetOrientation((StackOrientation)value));

    /// <summary>Bindable <see cref="MinimumTrackColor"/>.</summary>
    public static readonly BindableProperty MinimumTrackColorProperty = BindableProperty.Create(nameof(MinimumTrackColor), typeof(Color), typeof(SkUiSlider), null,
        defaultValueCreator: _ => SkUiColors.Accent, propertyChanged: (view, _, value) => ((SkUiSlider)view).SetMinimumTrackColor((Color)value));

    /// <summary>Bindable <see cref="MaximumTrackColor"/>.</summary>
    public static readonly BindableProperty MaximumTrackColorProperty = BindableProperty.Create(nameof(MaximumTrackColor), typeof(Color), typeof(SkUiSlider), null,
        defaultValueCreator: _ => SkUiColors.TrackOff, propertyChanged: (view, _, value) => ((SkUiSlider)view).SetMaximumTrackColor((Color)value));

    /// <summary>Bindable <see cref="ThumbColor"/>.</summary>
    public static readonly BindableProperty ThumbColorProperty = BindableProperty.Create(nameof(ThumbColor), typeof(Color), typeof(SkUiSlider), null,
        defaultValueCreator: _ => SkUiColors.Accent, propertyChanged: (view, _, value) => ((SkUiSlider)view).SetThumbColor((Color)value));

    /// <summary>Bindable <see cref="DragStartedCommand"/>.</summary>
    public static readonly BindableProperty DragStartedCommandProperty = BindableProperty.Create(nameof(DragStartedCommand), typeof(ICommand), typeof(SkUiSlider), null,
        propertyChanged: (view, _, value) => ((SkUiSlider)view)._dragStartedCommand = (ICommand?)value);

    /// <summary>Bindable <see cref="DragCompletedCommand"/>.</summary>
    public static readonly BindableProperty DragCompletedCommandProperty = BindableProperty.Create(nameof(DragCompletedCommand), typeof(ICommand), typeof(SkUiSlider), null,
        propertyChanged: (view, _, value) => ((SkUiSlider)view)._dragCompletedCommand = (ICommand?)value);

    /// <summary>Smallest value (default 0).</summary>
    public double Minimum { get => _minimum; set => SetValue(MinimumProperty, value); }

    /// <summary>Largest value (default 1).</summary>
    public double Maximum { get => _maximum; set => SetValue(MaximumProperty, value); }

    /// <summary>
    /// Current value, clamped between <see cref="Minimum"/> and <see cref="Maximum"/>. As in MAUI, the requested value
    /// is kept: when the range widens again, the value moves back towards it (so XAML property order doesn't matter).
    /// When <see cref="Maximum"/> is not above <see cref="Minimum"/>, the value is <see cref="Minimum"/>.
    /// </summary>
    public double Value { get => _value; set => SetValue(ValueProperty, value); }

    /// <summary>Horizontal (default; minimum at the start) or vertical (minimum at the bottom).</summary>
    public StackOrientation Orientation { get => _orientation; set => SetValue(OrientationProperty, value); }

    /// <summary>Track color between the minimum and the thumb.</summary>
    public Color MinimumTrackColor { get => _minimumTrackColor; set => SetValue(MinimumTrackColorProperty, value); }

    /// <summary>Track color between the thumb and the maximum.</summary>
    public Color MaximumTrackColor { get => _maximumTrackColor; set => SetValue(MaximumTrackColorProperty, value); }

    /// <summary>Thumb color.</summary>
    public Color ThumbColor { get => _thumbColor; set => SetValue(ThumbColorProperty, value); }

    /// <summary>Executed when a drag starts.</summary>
    public ICommand? DragStartedCommand { get => _dragStartedCommand; set => SetValue(DragStartedCommandProperty, value); }

    /// <summary>Executed when a drag ends.</summary>
    public ICommand? DragCompletedCommand { get => _dragCompletedCommand; set => SetValue(DragCompletedCommandProperty, value); }

    /// <summary>Whether the thumb is being dragged.</summary>
    public bool IsDragging => _gesture?.IsDragging == true;

    /// <summary>Raised when <see cref="Value"/> changes, from code or from input.</summary>
    public event EventHandler<ValueChangedEventArgs>? ValueChanged;

    /// <summary>Raised when a drag starts.</summary>
    public event EventHandler? DragStarted;

    /// <summary>Raised when a drag ends.</summary>
    public event EventHandler? DragCompleted;

    /// <summary>Sets the minimum without bindable write-back.</summary>
    public SkUiSlider SetMinimum(double value) { OnRangeChanged(minimum: value); return this; }

    /// <summary>Sets the maximum without bindable write-back.</summary>
    public SkUiSlider SetMaximum(double value) { OnRangeChanged(maximum: value); return this; }

    /// <summary>Sets the value (clamped) without bindable write-back.</summary>
    public SkUiSlider SetSliderValue(double value) { _requestedValue = value; ApplyValue(Clamp(value)); return this; }

    /// <summary>Sets the orientation without bindable write-back.</summary>
    public SkUiSlider SetOrientation(StackOrientation value)
    {
        if (_orientation == value) return this;
        _orientation = value;
        InvalidateMeasureOverride();
        return this;
    }

    /// <summary>Sets the minimum-track color without bindable write-back.</summary>
    public SkUiSlider SetMinimumTrackColor(Color value) { ArgumentNullException.ThrowIfNull(value); _minimumTrackColor = value; InvalidatePaint(); return this; }

    /// <summary>Sets the maximum-track color without bindable write-back.</summary>
    public SkUiSlider SetMaximumTrackColor(Color value) { ArgumentNullException.ThrowIfNull(value); _maximumTrackColor = value; InvalidatePaint(); return this; }

    /// <summary>Sets the thumb color without bindable write-back.</summary>
    public SkUiSlider SetThumbColor(Color value) { ArgumentNullException.ThrowIfNull(value); _thumbColor = value; InvalidatePaint(); return this; }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureSlider(widthConstraint, heightConstraint, _orientation);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var minimumTrack = _minimumTrackColor;
        var maximumTrack = _maximumTrackColor;
        var thumb = _thumbColor;
        if (!IsEnabled)
        {
            minimumTrack = minimumTrack.MultiplyAlpha(0.5f);
            maximumTrack = maximumTrack.MultiplyAlpha(0.5f);
            thumb = thumb.MultiplyAlpha(0.5f);
        }
        SkUiSliderMath.Draw(canvas, (float)Width, (float)Height, _orientation, IsRightToLeft, SkUiSliderMath.Fraction(_value, _minimum, _maximum),
            ToSkColor(minimumTrack), ToSkColor(maximumTrack), ToSkColor(thumb), IsDragging, IsEnabled);
    }

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        base.CollectGestureRecognizers(recognizers);
        recognizers.Add(_gesture ??= new SkUiSliderGestureRecognizer
        {
            Orientation = () => _orientation,
            Began = position =>
            {
                InvalidatePaint();
                DragStarted?.Invoke(this, EventArgs.Empty);
                _dragStartedCommand?.Execute(null);
                CommitAt(position);
            },
            Moved = CommitAt,
            Ended = () =>
            {
                InvalidatePaint();
                DragCompleted?.Invoke(this, EventArgs.Empty);
                _dragCompletedCommand?.Execute(null);
            },
            Tapped = CommitAt
        });
    }

    /// <summary>A user change: writes the value under <paramref name="position"/> back, then applies it (raising <see cref="ValueChanged"/>).</summary>
    private void CommitAt(Point position)
    {
        var value = Clamp(SkUiSliderMath.ValueAt(position, new Size(Width, Height), _orientation, IsRightToLeft, _minimum, _maximum));
        SetValue(ValueProperty, value);
        ApplyValue(value);
    }

    private double Clamp(double value) => SkUiSliderMath.Clamp(value, _minimum, _maximum);

    private double CoerceValue(double value)
    {
        if (!_recoercing)
            _requestedValue = value;
        return Clamp(value);
    }

    private void OnRangeChanged(double? minimum = null, double? maximum = null)
    {
        _minimum = minimum ?? _minimum;
        _maximum = maximum ?? _maximum;
        InvalidatePaint();
        // Keep the value inside the new range, as near the requested value as it allows (bindings in step), as MAUI does.
        var value = Clamp(_requestedValue);
        if ((double)GetValue(ValueProperty) != value)
        {
            _recoercing = true;
            try
            {
                SetValue(ValueProperty, value);
            }
            finally
            {
                _recoercing = false;
            }
        }
        ApplyValue(value);
    }

    private void ApplyValue(double value)
    {
        if (_value == value) return;
        var old = _value;
        _value = value;
        InvalidatePaint();
        ValueChanged?.Invoke(this, new ValueChangedEventArgs(old, value));
    }
}
