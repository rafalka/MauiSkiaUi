using Microsoft.Maui.Layouts;

namespace MauiSkiaUi;

/// <summary>A drawn horizontal stack using MAUI's <see cref="HorizontalStackLayoutManager"/>.</summary>
public class SkUiHorizontalStackLayout : SkUiLayout, IStackLayout
{
    private readonly HorizontalStackLayoutManager _manager;
    private double _spacing = 0;

    /// <summary>Bindable gap between children in DIPs.</summary>
    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(nameof(Spacing), typeof(double), typeof(SkUiHorizontalStackLayout), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiHorizontalStackLayout)view).OnSpacingChanged((double)value));

    /// <summary>Gap between children in DIPs.</summary>
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>Sets spacing (same as the property setter).</summary>
    public SkUiHorizontalStackLayout SetSpacing(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); Spacing = value; return this; }
    private void OnSpacingChanged(double value) { _spacing = value; InvalidateMeasureOverride(); }

    /// <summary>Creates a stack with MAUI layout management.</summary>
    public SkUiHorizontalStackLayout() => _manager = new HorizontalStackLayoutManager(this);

    double IStackLayout.Spacing => _spacing;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => _manager.Measure(widthConstraint, heightConstraint);
    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => _manager.ArrangeChildren(new Rect(Point.Zero, size));
}
