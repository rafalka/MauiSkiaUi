using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn horizontal bar of several parts side by side, each as long as its share of <see cref="Maximum"/> (a balance: taken,
/// scheduled, available), with the <see cref="TrackColor"/> after the last up to the maximum. Parts come from
/// <see cref="Segments"/>; changes to them animate (the look's <see cref="SkUiTransitionKind.StackedBar"/> transition; at
/// once under reduce motion). Drawn by <see cref="SkUiLook.DrawStackedBar"/>; right-to-left layouts fill from the right. A
/// screen reader reads one element whose value lists the parts ("Taken 0, Scheduled 24"); a legend stays app layout.
/// </summary>
[ContentProperty(nameof(Segments))]
public class SkUiStackedBar : SkUiView
{
    private ObservableCollection<SkUiBarSegment>? _segments;
    private bool _batching;
    private static readonly NotifyCollectionChangedEventArgs ResetArgs = new(NotifyCollectionChangedAction.Reset);
    private readonly HashSet<SkUiBarSegment> _attached = [];
    private SkUiStackedBarModel? _model;
    private double _maximum = -1;
    private double _segmentSpacing = -1;
    private double _cornerRadius = -1;
    private Color _trackColor = SkUiColors.TrackOff;

    /// <summary>Created on first use: property callbacks can run before the constructor (see <see cref="SkUiView.StyledOr{T}"/>).</summary>
    private SkUiStackedBarModel Model => _model ??= new SkUiStackedBarModel(this);

    /// <summary>Bindable <see cref="Maximum"/>.</summary>
    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(nameof(Maximum), typeof(double), typeof(SkUiStackedBar), -1d,
        validateValue: SkUiValidate.NonNegativeOrLook,
        propertyChanged: (view, _, value) => ((SkUiStackedBar)view).OnMaximumChanged((double)value));

    /// <summary>Bindable <see cref="SegmentSpacing"/>.</summary>
    public static readonly BindableProperty SegmentSpacingProperty = BindableProperty.Create(nameof(SegmentSpacing), typeof(double), typeof(SkUiStackedBar), -1d,
        validateValue: SkUiValidate.NonNegativeOrLook,
        propertyChanged: (view, _, value) => ((SkUiStackedBar)view).OnSegmentSpacingChanged((double)value));

    /// <summary>Bindable <see cref="CornerRadius"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(SkUiStackedBar), -1d,
        validateValue: SkUiValidate.NonNegativeOrLook,
        propertyChanged: (view, _, value) => ((SkUiStackedBar)view).OnCornerRadiusChanged((double)value));

    /// <summary>Bindable <see cref="TrackColor"/>.</summary>
    public static readonly BindableProperty TrackColorProperty = BindableProperty.Create(nameof(TrackColor), typeof(Color), typeof(SkUiStackedBar), null,
        defaultValueCreator: _ => SkUiColors.TrackOff, validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiStackedBar)view).OnTrackColorChanged((Color)value));

    /// <summary>The parts, in order from the start. Each takes the bar's binding context, so its properties can bind.</summary>
    public IList<SkUiBarSegment> Segments => _segments ??= CreateSegments();

    /// <summary>
    /// The value the whole bar stands for (default -1: the sum of the values, so the parts fill the bar). Parts past it are cut
    /// at the bar's end.
    /// </summary>
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>Gap between parts in DIPs (default -1: the look's <see cref="SkUiLook.StackedBarSegmentSpacing"/>).</summary>
    public double SegmentSpacing { get => (double)GetValue(SegmentSpacingProperty); set => SetValue(SegmentSpacingProperty, value); }

    /// <summary>Radius of the bar's outer ends in DIPs (default -1: the look's <see cref="SkUiLook.GetStackedBarCornerRadius"/>).</summary>
    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Color after the last part, up to <see cref="Maximum"/>.</summary>
    public Color TrackColor { get => (Color)GetValue(TrackColorProperty); set => SetValue(TrackColorProperty, value); }

    /// <summary>Replaces <see cref="Segments"/>.</summary>
    public SkUiStackedBar SetSegments(params IEnumerable<SkUiBarSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var list = segments.ToList();
        var current = (ObservableCollection<SkUiBarSegment>)Segments;
        // One change for the bar: each Clear / Add would animate from what it showed halfway (empty after the Clear).
        _batching = true;
        try
        {
            current.Clear();
            foreach (var segment in list)
                current.Add(segment);
        }
        finally
        {
            _batching = false;
        }
        OnSegmentsChanged(current, ResetArgs);
        return this;
    }

    /// <summary>Sets <see cref="Maximum"/>: a value, or -1 for the sum.</summary>
    public SkUiStackedBar SetMaximum(double value)
    {
        SkUiValidate.ThrowIfNotNonNegativeOrLook(value, nameof(value));
        Maximum = value;
        return this;
    }

    /// <summary>Sets <see cref="SegmentSpacing"/>: DIPs, or -1 for the look's.</summary>
    public SkUiStackedBar SetSegmentSpacing(double value)
    {
        SkUiValidate.ThrowIfNotNonNegativeOrLook(value, nameof(value));
        SegmentSpacing = value;
        return this;
    }

    /// <summary>Sets <see cref="CornerRadius"/>: DIPs, or -1 for the look's.</summary>
    public SkUiStackedBar SetCornerRadius(double value)
    {
        SkUiValidate.ThrowIfNotNonNegativeOrLook(value, nameof(value));
        CornerRadius = value;
        return this;
    }

    /// <summary>Sets the track color (same as the property setter).</summary>
    public SkUiStackedBar SetTrackColor(Color value) { ArgumentNullException.ThrowIfNull(value); TrackColor = value; return this; }

    private void OnMaximumChanged(double value) { _maximum = value; Update(); }
    private void OnSegmentSpacingChanged(double value) { _segmentSpacing = value; InvalidatePaint(); }
    private void OnCornerRadiusChanged(double value) { _cornerRadius = value; InvalidatePaint(); }
    private void OnTrackColorChanged(Color value) { _trackColor = value; InvalidatePaint(); }

    private ObservableCollection<SkUiBarSegment> CreateSegments()
    {
        var segments = new ObservableCollection<SkUiBarSegment>();
        segments.CollectionChanged += OnSegmentsChanged;
        return segments;
    }

    private void OnSegmentsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (_batching)
            return; // SetSegments reports its changes once
        foreach (var segment in _attached.ToArray())
            if (!_segments!.Contains(segment))
                Detach(segment);
        foreach (var segment in _segments!)
            Attach(segment);
        Update();
    }

    private void Attach(SkUiBarSegment segment)
    {
        if (!_attached.Add(segment))
            return;
        segment.Parent = this;
        SetInheritedBindingContext(segment, BindingContext);
        segment.PropertyChanged += OnSegmentPropertyChanged;
    }

    private void Detach(SkUiBarSegment segment)
    {
        if (!_attached.Remove(segment))
            return;
        segment.PropertyChanged -= OnSegmentPropertyChanged;
        if (ReferenceEquals(segment.Parent, this))
            segment.Parent = null;
    }

    private void OnSegmentPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(SkUiBarSegment.Value) or nameof(SkUiBarSegment.Color) or nameof(SkUiBarSegment.Label))
            Update();
    }

    private void Update()
    {
        var segments = _segments;
        var count = segments?.Count ?? 0;
        var values = count == 0 ? [] : new SkUiBarValue[count];
        for (var i = 0; i < count; i++)
        {
            var segment = segments![i];
            values[i] = new SkUiBarValue(segment.Value, segment.Color, segment.Label);
        }
        if (Model.Set(values, _maximum))
            InvalidatePaint();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        foreach (var segment in _attached)
            SetInheritedBindingContext(segment, BindingContext);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureStackedBar(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected internal override void AddSkeletonBones(SkUiSkeletonBuilder bones) => bones.Add(SkUiSkeletonPartKind.ProgressBar, bones.Bounds);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        Model.Draw(canvas, (float)Width, (float)Height, IsRightToLeft, IsEnabled, _trackColor, _segmentSpacing, _cornerRadius);

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.ProgressBar;
        info.Value = Model.Describe();
    }
}

/// <summary>
/// One part of a <see cref="SkUiStackedBar"/>: its <see cref="Value"/>, <see cref="Color"/> and the <see cref="Label"/> a
/// screen reader reads with the value. It takes the bar's binding context, so its properties can bind.
/// </summary>
public class SkUiBarSegment : Element
{
    /// <summary>Bindable property for <see cref="Value"/>.</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(double), typeof(SkUiBarSegment), 0d);

    /// <summary>Bindable property for <see cref="Color"/>.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiBarSegment), null);

    /// <summary>Bindable property for <see cref="Label"/>.</summary>
    public static readonly BindableProperty LabelProperty = BindableProperty.Create(nameof(Label), typeof(string), typeof(SkUiBarSegment), null);

    /// <summary>Creates a part of value 0.</summary>
    public SkUiBarSegment() { }

    /// <summary>Creates a part.</summary>
    public SkUiBarSegment(double value, Color? color = null, string? label = null)
    {
        Value = value;
        Color = color;
        Label = label;
    }

    /// <summary>The part's value (default 0); negative values and NaN count as 0.</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>The part's color (default <c>null</c>: the color scheme's accent).</summary>
    public Color? Color { get => (Color?)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <summary>What a screen reader reads before the value (default <c>null</c>: the value alone).</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Sets <see cref="Value"/> (same as the property setter; not <c>SetValue</c>, which is <see cref="BindableObject"/>'s).</summary>
    public SkUiBarSegment SetSegmentValue(double value) { Value = value; return this; }

    /// <summary>Sets <see cref="Color"/> (same as the property setter).</summary>
    public SkUiBarSegment SetColor(Color? value) { Color = value; return this; }

    /// <summary>Sets <see cref="Label"/> (same as the property setter).</summary>
    public SkUiBarSegment SetLabel(string? value) { Label = value; return this; }
}

/// <summary>One part of a stacked bar (<c>SkUiCoreStackedBar</c>; <see cref="SkUiBarSegment"/> on the SkUi layer).</summary>
/// <param name="Value">The part's value; negative values and NaN count as 0.</param>
/// <param name="Color">Its color (<c>null</c>: the color scheme's accent).</param>
/// <param name="Label">What a screen reader reads before the value (<c>null</c>: the value alone).</param>
public readonly record struct SkUiBarValue(double Value, Color? Color = null, string? Label = null);

/// <summary>
/// The parts of a stacked bar, shared by <see cref="SkUiStackedBar"/> and <see cref="Core.SkUiCoreStackedBar"/>: the values
/// set, the values drawn (each tweens from what was drawn with the look's <see cref="SkUiTransitionKind.StackedBar"/>
/// transition, the total too), drawing and the semantics value.
/// </summary>
internal sealed class SkUiStackedBarModel(ISkUiTransitionHost host)
{
    private SkUiBarValue[] _values = [];
    private IReadOnlyList<SkUiBarValue> _readOnlyValues = [];
    private double[] _from = [];
    private double _maximum = -1;
    private double _fromTotal;
    private double _toTotal;
    private readonly SkUiTween _tween = new(host);
    private SkUiStackedBarPart[] _parts = [];

    /// <summary>How close to the bar's end a part counts as reaching it (rounding of the shares).</summary>
    private const double FullTolerance = 1e-5;

    /// <summary>The values set.</summary>
    public IReadOnlyList<SkUiBarValue> Values => _readOnlyValues;

    /// <summary>A value as drawn: negative values, NaN and infinities count as 0.</summary>
    private static double Clean(double value) => double.IsFinite(value) && value > 0 ? value : 0;

    private float Progress => _tween.IsRunning ? _tween.Value : 1;

    private double Drawn(int index, double progress) => _from[index] + (Clean(_values[index].Value) - _from[index]) * progress;

    private double DrawnTotal(double progress) => _fromTotal + (_toTotal - _fromTotal) * progress;

    /// <summary>Sets the values and maximum (-1: the sum); returns whether anything changed (the caller repaints).</summary>
    public bool Set(ReadOnlySpan<SkUiBarValue> values, double maximum)
    {
        if (maximum == _maximum && values.SequenceEqual(_values)) // exact: did the values change
            return false;
        var progress = Progress;
        var from = values.Length == 0 ? [] : new double[values.Length];
        for (var i = 0; i < from.Length; i++)
            from[i] = i < _values.Length ? Drawn(i, progress) : 0;
        var fromTotal = DrawnTotal(progress);
        var toTotal = 0d;
        if (maximum >= 0)
            toTotal = maximum;
        else
            foreach (var value in values)
                toTotal += Clean(value.Value);
        _values = values.ToArray();
        _readOnlyValues = _values.Length == 0 ? [] : Array.AsReadOnly(_values);
        _maximum = maximum;
        _from = from;
        // Nothing drawn yet (or an empty bar): the parts grow against the new scale.
        _fromTotal = fromTotal > 0 ? fromTotal : toTotal;
        _toTotal = toTotal;
        _tween.Jump(0);
        _tween.AnimateTo(1, SkUiTransitionKind.StackedBar);
        return true;
    }

    /// <summary>Draws the bar as it is now (mid-transition too).</summary>
    public void Draw(SKCanvas canvas, float width, float height, bool rightToLeft, bool enabled, Color track, double spacing, double cornerRadius)
    {
        var look = SkUiLook.Current;
        var progress = Progress;
        var total = DrawnTotal(progress);
        if (_parts.Length < _values.Length)
            _parts = new SkUiStackedBarPart[_values.Length];
        var count = 0;
        // In doubles, snapped to the end: a bar the parts fill ends at exactly 1 (no track, so no gap cut from the last part).
        var start = 0d;
        for (var i = 0; i < _values.Length && total > 0 && start < 1; i++)
        {
            var end = Math.Min(1, start + Drawn(i, progress) / total);
            if (1 - end < FullTolerance)
                end = 1;
            if (end <= start)
                continue;
            var color = _values[i].Color ?? SkUiColors.Accent;
            _parts[count++] = new SkUiStackedBarPart((float)start, (float)end, SkUiToggleDrawing.ToSkColor(SkUiColors.EnabledTint(color, enabled)));
            start = end;
        }
        var save = canvas.Save();
        if (rightToLeft)
            canvas.Scale(-1, 1, width / 2, 0);
        look.DrawStackedBar(canvas, new SkUiStackedBarPaint(new SKRect(0, 0, width, height), new ArraySegment<SkUiStackedBarPart>(_parts, 0, count),
            SkUiToggleDrawing.ToSkColor(SkUiColors.EnabledTint(track, enabled)),
            spacing < 0 ? look.StackedBarSegmentSpacing : (float)spacing,
            cornerRadius < 0 ? look.GetStackedBarCornerRadius(height) : (float)cornerRadius, enabled));
        canvas.RestoreToCount(save);
    }

    /// <summary>The value a screen reader reads: each part's label and value ("Taken 0, Scheduled 24").</summary>
    public string? Describe()
    {
        if (_values.Length == 0)
            return null;
        var builder = new System.Text.StringBuilder();
        foreach (var value in _values)
        {
            if (builder.Length > 0)
                builder.Append(", ");
            if (!string.IsNullOrEmpty(value.Label))
                builder.Append(value.Label).Append(' ');
            builder.Append(Clean(value.Value).ToString("0.##", CultureInfo.CurrentCulture));
        }
        return builder.ToString();
    }
}
