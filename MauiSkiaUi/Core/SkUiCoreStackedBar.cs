using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Drawn horizontal bar of several parts side by side (Core analogue of <c>SkUiStackedBar</c>): each
/// <see cref="SkUiBarValue"/> of <see cref="Segments"/> is as long as its share of <see cref="Maximum"/>, with the
/// <see cref="TrackColor"/> after the last. Changes animate with the look's <see cref="SkUiTransitionKind.StackedBar"/>
/// transition. Drawn by <see cref="SkUiLook.DrawStackedBar"/>.
/// </summary>
public class SkUiCoreStackedBar : SkUiCoreNode
{
    private SkUiStackedBarModel? _model;
    private double _maximum = -1;
    private double _segmentSpacing = -1;
    private double _cornerRadius = -1;
    private Color _trackColor = SkUiColors.TrackOff;

    private SkUiStackedBarModel Model => _model ??= new SkUiStackedBarModel(this);

    /// <summary>The parts, in order from the start.</summary>
    public IReadOnlyList<SkUiBarValue> Segments { get => Model.Values; set => SetSegments(value); }

    /// <summary>The value the whole bar stands for (default -1: the sum of the values). Parts past it are cut at the bar's end.</summary>
    public double Maximum { get => _maximum; set => SetMaximum(value); }

    /// <summary>Gap between parts in DIPs (default -1: the look's <see cref="SkUiLook.StackedBarSegmentSpacing"/>).</summary>
    public double SegmentSpacing { get => _segmentSpacing; set => SetSegmentSpacing(value); }

    /// <summary>Radius of the bar's outer ends in DIPs (default -1: the look's <see cref="SkUiLook.GetStackedBarCornerRadius"/>).</summary>
    public double CornerRadius { get => _cornerRadius; set => SetCornerRadius(value); }

    /// <summary>Color after the last part, up to <see cref="Maximum"/>.</summary>
    public Color TrackColor { get => _trackColor; set => SetTrackColor(value); }

    /// <summary>Replaces <see cref="Segments"/>; the parts move to their new values.</summary>
    public SkUiCoreStackedBar SetSegments(params IEnumerable<SkUiBarValue> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (Model.Set(segments.ToArray(), _maximum))
        {
            OnPropertyChanged(nameof(Segments));
            InvalidatePaint();
        }
        return this;
    }

    /// <summary>Sets <see cref="Maximum"/>: a value, or -1 for the sum.</summary>
    public SkUiCoreStackedBar SetMaximum(double value)
    {
        SkUiValidate.ThrowIfNotNonNegativeOrLook(value, nameof(value));
        if (SetProperty(ref _maximum, value, nameof(Maximum)) && Model.Set(Model.Values.ToArray(), value))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="SegmentSpacing"/>: DIPs, or -1 for the look's.</summary>
    public SkUiCoreStackedBar SetSegmentSpacing(double value)
    {
        SkUiValidate.ThrowIfNotNonNegativeOrLook(value, nameof(value));
        if (SetProperty(ref _segmentSpacing, value, nameof(SegmentSpacing)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="CornerRadius"/>: DIPs, or -1 for the look's.</summary>
    public SkUiCoreStackedBar SetCornerRadius(double value)
    {
        SkUiValidate.ThrowIfNotNonNegativeOrLook(value, nameof(value));
        if (SetProperty(ref _cornerRadius, value, nameof(CornerRadius)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets the track color.</summary>
    public SkUiCoreStackedBar SetTrackColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref _trackColor, value, nameof(TrackColor)))
            InvalidatePaint();
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureStackedBar(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected internal override void AddSkeletonBones(SkUiSkeletonBuilder bones) => bones.Add(SkUiSkeletonPartKind.ProgressBar, bones.Bounds);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        Model.Draw(canvas, (float)Frame.Width, (float)Frame.Height, IsRightToLeft, IsEnabled, _trackColor, _segmentSpacing, _cornerRadius);

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.ProgressBar;
        info.Value = Model.Describe();
    }
}
