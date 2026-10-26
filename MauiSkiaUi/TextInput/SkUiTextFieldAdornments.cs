using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>What both layers' text fields share about their leading and trailing views (<c>LeadingView</c>, <c>TrailingView</c>).</summary>
internal static class SkUiTextFieldAdornments
{
    /// <summary>
    /// Whether a press at <paramref name="local"/> (the field's coordinates) is taken by <paramref name="view"/> (a render child of the
    /// field): it lands on a node of the view that handles pointers itself (a button: it has gesture recognizers) or blocks them
    /// (disabled), found as the pointer router finds the press's target. A press on an icon, a label, or empty space in a layout
    /// around a button is the field's (it focuses the field).
    /// </summary>
    public static bool TakesPointer(ISkUiRenderable? view, Point local)
    {
        if (view is not ISkUiInputNode { IsHitTestVisible: true } input || !Props(view).Matrix.TryInvert(out var inverse))
            return false;
        var children = new List<ISkUiRenderable>();
        var recognizers = new List<SkUiGestureRecognizer>();
        return Takes(input, inverse.MapPoint((float)local.X, (float)local.Y), children, recognizers);
    }

    // The pointer router's FindTarget (requiring a participant): inside the node's bounds, the topmost child that takes the press,
    // else the node when it is disabled or has recognizers.
    private static bool Takes(ISkUiInputNode node, SKPoint local, List<ISkUiRenderable> children, List<SkUiGestureRecognizer> recognizers)
    {
        if (!node.IsHitTestVisible)
            return false;
        var props = Props(node);
        if (!props.Bounds.Contains(local.X, local.Y))
            return false;
        if (!node.IsInputEnabled && node.HitTestsSelf)
            return true;
        var clip = props.ChildrenClipRect;
        var passes = !node.HitTestsChildren ? 0 : clip.IsEmpty || clip.Contains(local.X, local.Y) ? 2 : 1;
        var start = children.Count;
        node.GetRenderChildren(children);
        var childrenPoint = props.MapToChildren(local);
        try
        {
            // Pinned children first, then (inside the clip) the others, topmost first.
            for (var pass = 0; pass < passes; pass++)
                for (var index = children.Count - 1; index >= start; index--)
                {
                    if (children[index] is not ISkUiInputNode child || !child.IsHitTestVisible)
                        continue;
                    var childProps = Props(children[index]);
                    if (childProps.Pinned != (pass == 0) || !childProps.Matrix.TryInvert(out var inverse))
                        continue;
                    if (Takes(child, inverse.MapPoint(childProps.Pinned ? local : childrenPoint), children, recognizers))
                        return true;
                }
        }
        finally
        {
            children.RemoveRange(start, children.Count - start);
        }
        if (!node.HitTestsSelf)
            return false;
        recognizers.Clear();
        node.CollectGestureRecognizers(recognizers);
        var participates = recognizers.Count > 0;
        recognizers.Clear();
        return participates;
    }

    private static SkUiRenderProps Props(ISkUiRenderable node)
    {
        var props = SkUiRenderProps.Default;
        node.GetRenderProps(ref props);
        return props;
    }
}
