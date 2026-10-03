using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Locates drawn elements (<see cref="SkUiView"/> and <see cref="ISkUiCoreNode"/>) for diagnostics tools,
/// automation agents and tests. Drawn elements have no platform view, so MAUI's own bounds / hit-testing helpers
/// cannot see them; these use the same composite-time properties as the renderer (layout offset, translation,
/// rotation, scale, scroll offsets, RTL-mirrored frames). Call on the UI thread.
/// </summary>
public static class SkUiDiagnostics
{
    /// <summary>
    /// Trace sink for input / scrolling diagnostics (surface touches, native handover, scroll motion, overlay
    /// snapshots); <c>null</c> (default) = off. Call sites check <see cref="TraceOn"/> before formatting.
    /// </summary>
    internal static Action<string>? Trace { get; set; }

    internal static bool TraceOn => Trace is not null;

    internal static void Write(string message) => Trace?.Invoke(message);

    /// <summary>The standalone root (the view that owns the SkiaUi surface) drawing <paramref name="element"/>.</summary>
    public static SkUiView? GetSurfaceRoot(object element)
    {
        if (element is not ISkUiRenderable node)
            return null;
        while (node.RenderParent is { } parent)
            node = parent;
        return node as SkUiView;
    }

    /// <summary>
    /// Axis-aligned bounds of <paramref name="element"/> in its surface root's coordinates (DIPs), after all
    /// ancestor transforms and scroll offsets; <c>null</c> when it is not a drawn element under a surface root.
    /// </summary>
    public static Rect? GetRootBounds(object element)
    {
        if (element is not ISkUiRenderable node || GetSurfaceRoot(element) is null)
            return null;
        var props = GetProps(node);
        var matrix = SKMatrix.Identity;
        for (var current = node; current.RenderParent is { } parent; current = parent)
        {
            var currentProps = GetProps(current);
            matrix = (currentProps.Pinned ? SKMatrix.Identity : GetProps(parent).ChildrenMatrix)
                .PreConcat(currentProps.Matrix)
                .PreConcat(matrix);
        }
        var rect = matrix.MapRect(props.Bounds);
        return new Rect(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    /// <summary>
    /// Bounds of <paramref name="element"/> in window coordinates (DIPs): <see cref="GetRootBounds"/> offset by the
    /// surface root's platform view position. <c>null</c> without a connected platform view.
    /// </summary>
    public static Rect? GetWindowBounds(object element)
    {
        if (GetRootBounds(element) is not { } bounds || GetSurfaceRoot(element) is not { } root || GetWindowOrigin(root) is not { } origin)
            return null;
        return bounds.Offset(origin.X, origin.Y);
    }

    /// <summary>
    /// Deepest visible drawn element under <paramref name="rootPoint"/> (surface root coordinates, DIPs), honoring
    /// clips, transforms and scroll offsets; the root itself when no child is hit, <c>null</c> outside it.
    /// </summary>
    public static IVisualTreeElement? HitTest(SkUiView root, Point rootPoint)
    {
        ArgumentNullException.ThrowIfNull(root);
        ISkUiRenderable node = root;
        var props = GetProps(node);
        return HitTest(node, props, new SKPoint((float)rootPoint.X, (float)rootPoint.Y), []) as IVisualTreeElement;
    }

    /// <summary>
    /// Deepest drawn element at <paramref name="windowPoint"/> (window DIPs) among the SkiaUi surfaces under
    /// <paramref name="scope"/> (e.g. a page or window); the innermost surface wins when surfaces nest.
    /// </summary>
    public static IVisualTreeElement? HitTestWindow(IVisualTreeElement scope, Point windowPoint)
    {
        ArgumentNullException.ThrowIfNull(scope);
        IVisualTreeElement? found = null;
        foreach (var element in scope.GetVisualTreeDescendants())
        {
            if (element is not SkUiView { SkiaParent: null, Handler: not null } root || GetWindowOrigin(root) is not { } origin)
                continue;
            if (HitTest(root, new Point(windowPoint.X - origin.X, windowPoint.Y - origin.Y)) is { } hit)
                found = hit;
        }
        return found;
    }

    private static long _syntheticTouchId = long.MinValue / 2;

    /// <summary>
    /// Taps the center of <paramref name="element"/>'s bounds: a press and a release delivered through its surface
    /// root, like a real touch (hit-testing, capture, press states, <c>Clicked</c> / commands). Returns the element
    /// actually hit (the element itself, a descendant, or whatever covers that point), or <c>null</c> when the
    /// element is not on a surface or the point is clipped away.
    /// </summary>
    public static IVisualTreeElement? SimulateTap(object element)
    {
        if (GetSurfaceRoot(element) is not { } root || GetRootBounds(element) is not { } bounds)
            return null;
        var center = bounds.Center;
        if (HitTest(root, center) is not { } hit)
            return null;
        var id = Interlocked.Increment(ref _syntheticTouchId);
        var now = TimeSpan.FromMilliseconds(Environment.TickCount64);
        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Pressed, center, now));
        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Released, center, now + TimeSpan.FromMilliseconds(50)));
        return hit;
    }

    private static ISkUiRenderable? HitTest(ISkUiRenderable node, SkUiRenderProps props, SKPoint local, List<ISkUiRenderable> scratch)
    {
        var inside = props.Bounds.Contains(local.X, local.Y);
        if (props.ClipToBounds && !inside)
            return null;
        var childrenClip = props.ChildrenClipRect;
        var start = scratch.Count;
        node.GetRenderChildren(scratch);
        var childPoint = props.MapToChildren(local);
        try
        {
            // Paint order is back-to-front, pinned children above the others (and outside the children clip): test the
            // topmost child first.
            for (var index = scratch.Count - 1; index >= start; index--)
            {
                var child = scratch[index];
                var childProps = GetProps(child);
                if (!childProps.Pinned || childProps.IsSkipped || !childProps.Matrix.TryInvert(out var inverse))
                    continue;
                if (HitTest(child, childProps, inverse.MapPoint(local), scratch) is { } hit)
                    return hit;
            }
            if (childrenClip.IsEmpty || childrenClip.Contains(local.X, local.Y))
                for (var index = scratch.Count - 1; index >= start; index--)
                {
                    var child = scratch[index];
                    var childProps = GetProps(child);
                    if (childProps.Pinned || childProps.IsSkipped || !childProps.Matrix.TryInvert(out var inverse))
                        continue;
                    if (HitTest(child, childProps, inverse.MapPoint(childPoint), scratch) is { } hit)
                        return hit;
                }
        }
        finally
        {
            scratch.RemoveRange(start, scratch.Count - start);
        }
        return inside ? node : null;
    }

    /// <summary>
    /// Whether Core tree changes are reported to <see cref="VisualDiagnostics"/>: MAUI's diagnostics switch (on in
    /// Debug builds, off and trimmed away in Release) read once, so Core inserts pay a static field read instead of
    /// an <see cref="AppContext"/> lookup. Settable for tests.
    /// </summary>
    internal static bool TreeNotifications { get; set; } =
        (AppContext.TryGetSwitch("Microsoft.Maui.RuntimeFeature.EnableMauiDiagnostics", out var maui)
            ? maui
            : AppContext.TryGetSwitch("Microsoft.Maui.RuntimeFeature.EnableDiagnostics", out var any) && any)
        || Environment.GetEnvironmentVariable("ENABLE_XAML_DIAGNOSTICS_SOURCE_INFO") == "1";

    internal static void NotifyChildAdded(IVisualTreeElement parent, IVisualTreeElement child, int index)
    {
        if (TreeNotifications)
            VisualDiagnostics.OnChildAdded(parent, child, index);
    }

    internal static void NotifyChildRemoved(IVisualTreeElement parent, IVisualTreeElement child, int index)
    {
        if (TreeNotifications)
            VisualDiagnostics.OnChildRemoved(parent, child, index);
    }

    private static SkUiRenderProps GetProps(ISkUiRenderable node)
    {
        var props = SkUiRenderProps.Default;
        node.GetRenderProps(ref props);
        return props;
    }

    /// <summary>Window position (DIPs) of the root's platform view, or <c>null</c> without one.</summary>
    private static Point? GetWindowOrigin(SkUiView root)
    {
#if IOS || MACCATALYST
        if (root.Handler?.PlatformView is UIKit.UIView view && view.Window is not null)
        {
            var rect = view.ConvertRectToView(view.Bounds, null);
            return new Point(rect.X, rect.Y);
        }
#elif ANDROID
        if (root.Handler?.PlatformView is Android.Views.View view && view.IsAttachedToWindow)
        {
            var location = new int[2];
            view.GetLocationInWindow(location);
            var density = view.Resources?.DisplayMetrics?.Density ?? 1;
            return new Point(location[0] / density, location[1] / density);
        }
#elif WINDOWS
        if (root.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement view && view.XamlRoot is not null)
        {
            var point = view.TransformToVisual(null).TransformPoint(new global::Windows.Foundation.Point(0, 0));
            return new Point(point.X, point.Y);
        }
#endif
        return null;
    }
}
