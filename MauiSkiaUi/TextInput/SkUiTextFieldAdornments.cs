using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>What both layers' text fields share about their leading and trailing views (<c>LeadingView</c>, <c>TrailingView</c>).</summary>
internal static class SkUiTextFieldAdornments
{
    /// <summary>
    /// Whether a press at <paramref name="local"/> (the field's coordinates) lands on <paramref name="view"/>, arranged at
    /// <paramref name="frame"/>, and the view handles pointers itself: it is hit-test visible and it or a descendant has gesture
    /// recognizers (a button). An icon or a label does not: a press on it is the field's (it focuses the field).
    /// </summary>
    public static bool TakesPointer(ISkUiRenderable? view, Rect frame, Point local) =>
        view is ISkUiInputNode { IsHitTestVisible: true } && frame.Contains(local) && HasRecognizers(view, []);

    private static bool HasRecognizers(ISkUiRenderable node, List<SkUiGestureRecognizer> recognizers)
    {
        if (node is ISkUiInputNode input)
        {
            if (!input.IsHitTestVisible)
                return false;
            input.CollectGestureRecognizers(recognizers);
            if (recognizers.Count > 0)
                return true;
            if (!input.HitTestsChildren)
                return false;
        }
        var children = new List<ISkUiRenderable>();
        node.GetRenderChildren(children);
        foreach (var child in children)
            if (HasRecognizers(child, recognizers))
                return true;
        return false;
    }
}
