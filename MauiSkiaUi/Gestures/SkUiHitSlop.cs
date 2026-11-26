namespace MauiSkiaUi;

/// <summary>
/// Hit slop (<c>HitSlop</c>, <c>MinimumTouchTargetSize</c> on views and Core nodes): the area a node is pressed in grows past
/// its rectangle without changing its layout or drawing. The nodes that have one are tracked here, per UI thread and weakly,
/// so the pointer router only looks for them after its ordinary search (<see cref="SkUiPointerRouter"/>) and costs nothing
/// while no node has a slop.
/// </summary>
internal static class SkUiHitSlop
{
    [ThreadStatic] private static List<WeakReference<ISkUiInputNode>>? _nodes;

    /// <summary>Whether a node of this thread may have a slop (some were tracked).</summary>
    internal static bool Any => _nodes is { Count: > 0 };

    /// <summary>
    /// The slop a node shows at <paramref name="width"/> × <paramref name="height"/>: each side of <paramref name="slop"/>, or
    /// more where <paramref name="minimum"/> needs it (half the shortfall on each side of an axis).
    /// </summary>
    internal static Thickness Effective(Thickness slop, double minimum, double width, double height)
    {
        var horizontal = minimum > width ? (minimum - width) / 2 : 0;
        var vertical = minimum > height ? (minimum - height) / 2 : 0;
        return new Thickness(Math.Max(slop.Left, horizontal), Math.Max(slop.Top, vertical), Math.Max(slop.Right, horizontal), Math.Max(slop.Bottom, vertical));
    }

    /// <summary>Whether a node with these settings may have a slop (it is tracked).</summary>
    internal static bool Has(Thickness slop, double minimum) => minimum > 0 || slop.Left > 0 || slop.Top > 0 || slop.Right > 0 || slop.Bottom > 0;

    /// <summary>Tracks <paramref name="node"/> while <paramref name="tracked"/> (it has a slop), forgets it otherwise.</summary>
    internal static void Track(ISkUiInputNode node, bool tracked)
    {
        var nodes = _nodes ??= [];
        for (var index = nodes.Count - 1; index >= 0; index--)
            if (!nodes[index].TryGetTarget(out var known) || ReferenceEquals(known, node))
                nodes.RemoveAt(index);
        if (tracked)
            nodes.Add(new WeakReference<ISkUiInputNode>(node));
    }

    /// <summary>Appends the tracked nodes still alive to <paramref name="into"/>.</summary>
    internal static void Collect(List<ISkUiInputNode> into)
    {
        if (_nodes is not { } nodes)
            return;
        for (var index = nodes.Count - 1; index >= 0; index--)
        {
            if (nodes[index].TryGetTarget(out var node))
                into.Add(node);
            else
                nodes.RemoveAt(index);
        }
    }
}
