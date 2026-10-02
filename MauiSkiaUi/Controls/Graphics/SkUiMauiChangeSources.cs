using Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUi;

/// <summary>
/// The MAUI Controls sources that report changes through their own events rather than the standard interfaces, for the
/// layer-agnostic <see cref="SkUiChangeHub"/>: a gradient brush's stops, a geometry group's children.
/// </summary>
internal static class SkUiMauiChangeSources
{
    public static void Subscribe(object source, EventHandler onInvalidated)
    {
        if (source is GradientBrush gradient) gradient.InvalidateGradientBrushRequested += onInvalidated;
        if (source is GeometryGroup group) group.InvalidateGeometryRequested += onInvalidated;
    }
}
