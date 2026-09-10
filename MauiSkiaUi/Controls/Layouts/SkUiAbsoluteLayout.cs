using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;

namespace MauiSkiaUi;

/// <summary>A drawn absolute layout using MAUI's <see cref="AbsoluteLayoutManager"/> and <c>AbsoluteLayout.LayoutBounds</c>/<c>LayoutFlags</c> attached properties.</summary>
public class SkUiAbsoluteLayout : SkUiLayout, IAbsoluteLayout
{
    private readonly AbsoluteLayoutManager manager;

    /// <summary>Creates an absolute layout with MAUI layout management.</summary>
    public SkUiAbsoluteLayout() => manager = new AbsoluteLayoutManager(this);

    /// <summary>Gets a child's proportional/absolute bounds, set via <c>AbsoluteLayout.SetLayoutBounds</c>.</summary>
    public static Rect GetLayoutBounds(BindableObject view) => AbsoluteLayout.GetLayoutBounds(view);
    /// <summary>Sets a child's proportional/absolute bounds.</summary>
    public static void SetLayoutBounds(BindableObject view, Rect bounds) => AbsoluteLayout.SetLayoutBounds(view, bounds);
    /// <summary>Gets which components of a child's bounds are proportional.</summary>
    public static AbsoluteLayoutFlags GetLayoutFlags(BindableObject view) => AbsoluteLayout.GetLayoutFlags(view);
    /// <summary>Sets which components of a child's bounds are proportional.</summary>
    public static void SetLayoutFlags(BindableObject view, AbsoluteLayoutFlags flags) => AbsoluteLayout.SetLayoutFlags(view, flags);

    Rect IAbsoluteLayout.GetLayoutBounds(IView view) => AbsoluteLayout.GetLayoutBounds((BindableObject)view);
    AbsoluteLayoutFlags IAbsoluteLayout.GetLayoutFlags(IView view) => AbsoluteLayout.GetLayoutFlags((BindableObject)view);

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => manager.Measure(widthConstraint, heightConstraint);
    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => manager.ArrangeChildren(new Rect(Point.Zero, size));
}
