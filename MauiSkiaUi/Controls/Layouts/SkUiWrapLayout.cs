namespace MauiSkiaUi;

/// <summary>
/// A drawn wrap layout: children flow left to right and wrap onto a new row when the next one does not fit
/// (chips, tags, filter rows). <see cref="Spacing"/> separates items in a row, <see cref="RowSpacing"/> separates
/// rows; each child aligns vertically inside its row. Same engine as <see cref="Core.SkUiCoreWrapLayout"/>.
/// </summary>
public class SkUiWrapLayout : SkUiLayout
{
    private double _spacing;
    private double _rowSpacing;
    private SkUiWrapState _state;

    /// <summary>Bindable gap between items in a row, in DIPs.</summary>
    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(nameof(Spacing), typeof(double), typeof(SkUiWrapLayout), 0d,
        propertyChanged: (view, _, value) => ((SkUiWrapLayout)view).SetSpacing((double)value));
    /// <summary>Bindable gap between rows, in DIPs.</summary>
    public static readonly BindableProperty RowSpacingProperty = BindableProperty.Create(nameof(RowSpacing), typeof(double), typeof(SkUiWrapLayout), 0d,
        propertyChanged: (view, _, value) => ((SkUiWrapLayout)view).SetRowSpacing((double)value));

    /// <summary>Gap between items in a row, in DIPs.</summary>
    public double Spacing { get => _spacing; set => SetValue(SpacingProperty, value); }
    /// <summary>Gap between rows, in DIPs.</summary>
    public double RowSpacing { get => _rowSpacing; set => SetValue(RowSpacingProperty, value); }

    /// <summary>Sets <see cref="Spacing"/> without bindable write-back.</summary>
    public SkUiWrapLayout SetSpacing(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); _spacing = value; InvalidateMeasureOverride(); return this; }
    /// <summary>Sets <see cref="RowSpacing"/> without bindable write-back.</summary>
    public SkUiWrapLayout SetRowSpacing(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); _rowSpacing = value; InvalidateMeasureOverride(); return this; }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var children = new SkUiViewChildren(Children);
        return SkUiWrapEngine.Measure(ref children, ref _state, widthConstraint, Padding, _spacing, _rowSpacing);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var children = new SkUiViewChildren(Children);
        SkUiWrapEngine.Arrange(ref children, ref _state, size, Padding, _spacing, _rowSpacing);
    }
}
