using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;

namespace MauiSkiaUi;

/// <summary>A drawn horizontal stack using MAUI's <see cref="HorizontalStackLayoutManager"/>.</summary>
public class SkUiHorizontalStackLayout : SkUiLayout, IStackLayout
{
    private readonly HorizontalStackLayoutManager manager;
    private double spacing = 0;

    /// <summary>Bindable gap between children in DIPs.</summary>
    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(nameof(Spacing), typeof(double), typeof(SkUiHorizontalStackLayout), 0d,
        propertyChanged: (view, _, value) => ((SkUiHorizontalStackLayout)view).SetSpacing((double)value));

    /// <summary>Gap between children in DIPs.</summary>
    public double Spacing { get => spacing; set => SetValue(SpacingProperty, value); }

    /// <summary>Sets spacing without bindable write-back.</summary>
    public SkUiHorizontalStackLayout SetSpacing(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); spacing = value; InvalidateMeasureOverride(); return this; }

    /// <summary>Creates a stack with MAUI layout management.</summary>
    public SkUiHorizontalStackLayout() => manager = new HorizontalStackLayoutManager(this);

    double IStackLayout.Spacing => spacing;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => manager.Measure(widthConstraint, heightConstraint);
    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => manager.ArrangeChildren(new Rect(Point.Zero, size));
}
