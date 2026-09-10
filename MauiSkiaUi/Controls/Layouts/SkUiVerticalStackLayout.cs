using Microsoft.Maui.Layouts;

namespace MauiSkiaUi;

/// <summary>A drawn vertical stack using MAUI's <see cref="VerticalStackLayoutManager"/>.</summary>
public class SkUiVerticalStackLayout : SkUiLayout, IStackLayout
{
    private readonly VerticalStackLayoutManager _manager;
    private double _spacing = 0;

    /// <summary>Bindable gap between children in DIPs.</summary>
    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(nameof(Spacing), typeof(double), typeof(SkUiVerticalStackLayout), 0d,
        propertyChanged: (view, _, value) => ((SkUiVerticalStackLayout)view).SetSpacing((double)value));

    /// <summary>Gap between children in DIPs.</summary>
    public double Spacing { get => _spacing; set => SetValue(SpacingProperty, value); }

    /// <summary>Sets spacing without bindable write-back.</summary>
    public SkUiVerticalStackLayout SetSpacing(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); _spacing = value; InvalidateMeasureOverride(); return this; }

    /// <summary>Creates a stack with MAUI layout management.</summary>
    public SkUiVerticalStackLayout() => _manager = new VerticalStackLayoutManager(this);

    double IStackLayout.Spacing => _spacing;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => _manager.Measure(widthConstraint, heightConstraint);
    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => _manager.ArrangeChildren(new Rect(Point.Zero, size));
}
