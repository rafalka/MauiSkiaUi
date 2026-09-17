using Microsoft.Maui.Layouts;

namespace MauiSkiaUi;

/// <summary>A drawn absolute layout using MAUI's <see cref="AbsoluteLayoutManager"/> and <c>AbsoluteLayout.LayoutBounds</c>/<c>LayoutFlags</c> attached properties.</summary>
public class SkUiAbsoluteLayout : SkUiLayout, IAbsoluteLayout
{
    private readonly AbsoluteLayoutManager _manager;

    /// <summary>Creates an absolute layout with MAUI layout management.</summary>
    public SkUiAbsoluteLayout() => _manager = new AbsoluteLayoutManager(this);

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

    /// <summary>
    /// Adds many children inside one <see cref="SkUiView.StartUpdating"/> / <see cref="SkUiView.EndUpdating"/>
    /// batch so measure, arrange, and paint invalidate once after all inserts.
    /// Nested batches from an outer <see cref="SkUiView.StartUpdating"/> remain open until that outer end.
    /// </summary>
    /// <param name="views">SkiaUi children to append; each must be an <see cref="ISkUiView"/>.</param>
    public void Add(IEnumerable<IView> views)
    {
        ArgumentNullException.ThrowIfNull(views);
        StartUpdating();
        try
        {
            var children = (ICollection<IView>)this;
            foreach (var view in views)
                children.Add(view);
        }
        finally
        {
            EndUpdating();
        }
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => _manager.Measure(widthConstraint, heightConstraint);
    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => _manager.ArrangeChildren(new Rect(Point.Zero, size));
}
