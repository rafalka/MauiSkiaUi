namespace MauiSkiaUi;

/// <summary>
/// Base of the drawn shrink stacks (<see cref="SkUiHorizontalShrinkLayout"/>, <see cref="SkUiVerticalShrinkLayout"/>):
/// a stack that fits its main axis. Without overflow it is a plain stack that takes only the space its children need.
/// When the children overflow, children with an attached <see cref="ShrinkProperty"/> factor give up space (a label
/// then truncates or wraps); see <see cref="SkUiShrinkFactor"/>. Other children keep their size. Same engine as the
/// Core shrink layouts.
/// </summary>
public abstract class SkUiShrinkLayout : SkUiLayout
{
    private readonly bool _vertical;
    private double _spacing;
    private SkUiShrinkState _state;

    /// <summary>Bindable gap between children in DIPs.</summary>
    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(nameof(Spacing), typeof(double), typeof(SkUiShrinkLayout), 0d,
        propertyChanged: (view, _, value) => ((SkUiShrinkLayout)view).SetSpacing((double)value));

    /// <summary>
    /// Attached: how a child gives up space when the children do not fit (<c>sk:SkUiShrinkLayout.Shrink="Auto"</c>,
    /// <c>"2"</c>); default <see cref="SkUiShrinkFactor.None"/>.
    /// </summary>
    public static readonly BindableProperty ShrinkProperty = BindableProperty.CreateAttached("Shrink", typeof(SkUiShrinkFactor), typeof(SkUiShrinkLayout), SkUiShrinkFactor.None);

    /// <summary>Gets a child's shrink factor.</summary>
    public static SkUiShrinkFactor GetShrink(BindableObject view) => (SkUiShrinkFactor)view.GetValue(ShrinkProperty);
    /// <summary>Sets a child's shrink factor.</summary>
    public static void SetShrink(BindableObject view, SkUiShrinkFactor value) => view.SetValue(ShrinkProperty, value);

    private protected SkUiShrinkLayout(bool vertical)
    {
        _vertical = vertical;
        // Unlike other layouts, clip: children stuck at their minimum size can still overflow (as the Core layout).
        ClipToBounds = true;
    }

    /// <summary>Gap between children in DIPs.</summary>
    public double Spacing { get => _spacing; set => SetValue(SpacingProperty, value); }

    /// <summary>Sets <see cref="Spacing"/> without bindable write-back.</summary>
    public SkUiShrinkLayout SetSpacing(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); _spacing = value; InvalidateMeasureOverride(); return this; }

    /// <inheritdoc />
    private protected override bool AffectsChildLayout(string? propertyName) => propertyName == ShrinkProperty.PropertyName;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var children = new SkUiViewChildren(Children);
        var factors = new AttachedShrinkFactors(Children);
        return SkUiShrinkEngine.Measure(ref children, ref factors, ref _state, _vertical, widthConstraint, heightConstraint, Padding, _spacing);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var children = new SkUiViewChildren(Children);
        var factors = new AttachedShrinkFactors(Children);
        SkUiShrinkEngine.Arrange(ref children, ref factors, ref _state, _vertical, size, Padding, _spacing);
    }

    /// <summary>Reads each child's attached <see cref="ShrinkProperty"/>.</summary>
    private readonly struct AttachedShrinkFactors(IList<ISkUiView> children) : ISkUiShrinkFactors
    {
        public SkUiShrinkFactor Shrink(int index) => GetShrink((BindableObject)children[index]);
    }
}

/// <summary>A drawn horizontal shrink stack: children with a <c>SkUiShrinkLayout.Shrink</c> factor narrow to fit the width.</summary>
public class SkUiHorizontalShrinkLayout : SkUiShrinkLayout
{
    /// <summary>Creates a horizontal shrink stack.</summary>
    public SkUiHorizontalShrinkLayout() : base(vertical: false) { }
}

/// <summary>A drawn vertical shrink stack: children with a <c>SkUiShrinkLayout.Shrink</c> factor shorten to fit the height.</summary>
public class SkUiVerticalShrinkLayout : SkUiShrinkLayout
{
    /// <summary>Creates a vertical shrink stack.</summary>
    public SkUiVerticalShrinkLayout() : base(vertical: true) { }
}
