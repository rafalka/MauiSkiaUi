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
    private SkUiSliderVisual? _visual;
    private bool _animateThumb;
    private ImageSource? _thumbImageSource;
    private SkUiWeakListener<SkUiSlider>? _thumbImageListener; // a shared image source must not keep the slider alive
    private SkUiImageSlot? _thumbImage;

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
        StackOrientation.Horizontal, propertyChanged: (view, _, value) => ((SkUiSlider)view).OnOrientationChanged((StackOrientation)value));

    /// <summary>Bindable <see cref="MinimumTrackColor"/>.</summary>
    public static readonly BindableProperty MinimumTrackColorProperty = BindableProperty.Create(nameof(MinimumTrackColor), typeof(Color), typeof(SkUiSlider), null,
        defaultValueCreator: _ => SkUiColors.Accent, validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiSlider)view).OnMinimumTrackColorChanged((Color)value));

    /// <summary>Bindable <see cref="MaximumTrackColor"/>.</summary>
    public static readonly BindableProperty MaximumTrackColorProperty = BindableProperty.Create(nameof(MaximumTrackColor), typeof(Color), typeof(SkUiSlider), null,
        defaultValueCreator: _ => SkUiColors.TrackOff, validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiSlider)view).OnMaximumTrackColorChanged((Color)value));

    /// <summary>Bindable <see cref="ThumbColor"/>.</summary>
    public static readonly BindableProperty ThumbColorProperty = BindableProperty.Create(nameof(ThumbColor), typeof(Color), typeof(SkUiSlider), null,
        defaultValueCreator: _ => SkUiColors.Accent, validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiSlider)view).OnThumbColorChanged((Color)value));

    /// <summary>Bindable <see cref="ThumbImageSource"/>.</summary>
    public static readonly BindableProperty ThumbImageSourceProperty = BindableProperty.Create(nameof(ThumbImageSource), typeof(ImageSource), typeof(SkUiSlider), null,
        propertyChanged: (view, _, value) => ((SkUiSlider)view).OnThumbImageSourceChanged((ImageSource?)value));

    /// <summary>Bindable <see cref="DragStartedCommand"/>.</summary>
    public static readonly BindableProperty DragStartedCommandProperty = BindableProperty.Create(nameof(DragStartedCommand), typeof(ICommand), typeof(SkUiSlider), null,
        propertyChanged: (view, _, value) => ((SkUiSlider)view)._dragStartedCommand = (ICommand?)value);

    /// <summary>Bindable <see cref="DragCompletedCommand"/>.</summary>
    public static readonly BindableProperty DragCompletedCommandProperty = BindableProperty.Create(nameof(DragCompletedCommand), typeof(ICommand), typeof(SkUiSlider), null,
        propertyChanged: (view, _, value) => ((SkUiSlider)view)._dragCompletedCommand = (ICommand?)value);

    /// <summary>Smallest value (default 0).</summary>
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <summary>Largest value (default 1).</summary>
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>
    /// Current value, clamped between <see cref="Minimum"/> and <see cref="Maximum"/>. As in MAUI, the requested value
    /// is kept: when the range widens again, the value moves back towards it (so XAML property order doesn't matter).
    /// When <see cref="Maximum"/> is not above <see cref="Minimum"/>, the value is <see cref="Minimum"/>.
    /// </summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Horizontal (default; minimum at the start) or vertical (minimum at the bottom).</summary>
    public StackOrientation Orientation { get => (StackOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>Track color between the minimum and the thumb.</summary>
    public Color MinimumTrackColor { get => (Color)GetValue(MinimumTrackColorProperty); set => SetValue(MinimumTrackColorProperty, value); }

    /// <summary>Track color between the thumb and the maximum.</summary>
    public Color MaximumTrackColor { get => (Color)GetValue(MaximumTrackColorProperty); set => SetValue(MaximumTrackColorProperty, value); }

    /// <summary>Thumb color.</summary>
    public Color ThumbColor { get => (Color)GetValue(ThumbColorProperty); set => SetValue(ThumbColorProperty, value); }

    /// <summary>
    /// Image drawn instead of the look's thumb, at its intrinsic size, upright (MAUI's <c>ThumbImageSource</c>); loaded
    /// through the shared image cache like <see cref="SkUiImage.Source"/>. The thumb's travel and touch mapping stay the look's.
    /// </summary>
    public ImageSource? ThumbImageSource { get => (ImageSource?)GetValue(ThumbImageSourceProperty); set => SetValue(ThumbImageSourceProperty, value); }

    /// <summary>Executed when a drag starts.</summary>
    public ICommand? DragStartedCommand { get => (ICommand?)GetValue(DragStartedCommandProperty); set => SetValue(DragStartedCommandProperty, value); }

    /// <summary>Executed when a drag ends.</summary>
    public ICommand? DragCompletedCommand { get => (ICommand?)GetValue(DragCompletedCommandProperty); set => SetValue(DragCompletedCommandProperty, value); }

    /// <summary>Whether the thumb is being dragged.</summary>
    public bool IsDragging => _gesture?.IsDragging == true;

    /// <summary>Raised when <see cref="Value"/> changes, from code or from input.</summary>
    public event EventHandler<ValueChangedEventArgs>? ValueChanged;

    /// <summary>Raised when a drag starts.</summary>
    public event EventHandler? DragStarted;

    /// <summary>Raised when a drag ends.</summary>
    public event EventHandler? DragCompleted;

    /// <summary>Sets the minimum (same as the property setter).</summary>
    public SkUiSlider SetMinimum(double value) { Minimum = value; return this; }

    /// <summary>Sets the maximum (same as the property setter).</summary>
    public SkUiSlider SetMaximum(double value) { Maximum = value; return this; }

    /// <summary>Sets the value, clamped to the range (same as the property setter; the requested value is kept).</summary>
    public SkUiSlider SetSliderValue(double value) { Value = value; return this; }

    /// <summary>Sets the orientation (same as the property setter).</summary>
    public SkUiSlider SetOrientation(StackOrientation value)
    {
        Orientation = value;
        return this;
    }

    private void OnOrientationChanged(StackOrientation value)
    {
        if (_orientation == value) return;
        _orientation = value;
        InvalidateMeasureOverride();
    }

    /// <summary>Sets the minimum-track color (same as the property setter).</summary>
    public SkUiSlider SetMinimumTrackColor(Color value) { ArgumentNullException.ThrowIfNull(value); MinimumTrackColor = value; return this; }
    private void OnMinimumTrackColorChanged(Color value) { _minimumTrackColor = value; InvalidatePaint(); }

    /// <summary>Sets the maximum-track color (same as the property setter).</summary>
    public SkUiSlider SetMaximumTrackColor(Color value) { ArgumentNullException.ThrowIfNull(value); MaximumTrackColor = value; return this; }
    private void OnMaximumTrackColorChanged(Color value) { _maximumTrackColor = value; InvalidatePaint(); }

    /// <summary>Sets the thumb color (same as the property setter).</summary>
    public SkUiSlider SetThumbColor(Color value) { ArgumentNullException.ThrowIfNull(value); ThumbColor = value; return this; }
    private void OnThumbColorChanged(Color value) { _thumbColor = value; InvalidatePaint(); }

    /// <summary>Sets the thumb image (same as the property setter).</summary>
    public SkUiSlider SetThumbImageSource(ImageSource? value) { ThumbImageSource = value; return this; }

    private void OnThumbImageSourceChanged(ImageSource? value)
    {
        if (ReferenceEquals(_thumbImageSource, value)) return;
        // Bindings inside the source resolve against the slider (MAUI's Slider leaves them unresolved).
        AdoptImageSource(_thumbImageSource, value);
        _thumbImageSource = value;
        (_thumbImageListener ??= new(this, static (slider, change) =>
        {
            if (SkUiMauiImageSources.AffectsImage(change.PropertyName))
                slider.LoadThumbImage();
        })).Listen(value);
        LoadThumbImage();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        InheritBindingContext(_thumbImageSource);
    }

    private void LoadThumbImage() =>
        (_thumbImage ??= new SkUiImageSlot(this, () => InvalidatePaint())).Load(SkUiMauiImageSources.Convert(_thumbImageSource), default);

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
        var fraction = SkUiSliderMath.Fraction(_value, _minimum, _maximum);
        SkUiSliderMath.Draw(canvas, (float)Width, (float)Height, _orientation, IsRightToLeft, _visual?.Fraction(fraction) ?? fraction,
            ToSkColor(minimumTrack), ToSkColor(maximumTrack), ToSkColor(thumb), _visual?.Pressed ?? 0, IsEnabled, _thumbImage);
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
                SliderVisual.SetDragging(true);
                InvalidatePaint();
                DragStarted?.Invoke(this, EventArgs.Empty);
                _dragStartedCommand?.Execute(null);
                CommitAt(position);
            },
            Moved = CommitAt,
            Ended = () =>
            {
                SliderVisual.SetDragging(false);
                InvalidatePaint();
                DragCompleted?.Invoke(this, EventArgs.Empty);
                _dragCompletedCommand?.Execute(null);
            },
            Tapped = position =>
            {
                // The thumb glides to a tapped value (the value itself changes at once).
                _animateThumb = true;
                try { CommitAt(position); }
                finally { _animateThumb = false; }
            }
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

    private SkUiSliderVisual SliderVisual => _visual ??= new SkUiSliderVisual(this);

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
        if (_animateThumb || _visual is not null)
            SliderVisual.Moved(SkUiSliderMath.Fraction(old, _minimum, _maximum), SkUiSliderMath.Fraction(value, _minimum, _maximum), _animateThumb);
        InvalidatePaint();
        ValueChanged?.Invoke(this, new ValueChangedEventArgs(old, value));
    }

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
        Value = clamped;
        return true;
    }

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;
}
