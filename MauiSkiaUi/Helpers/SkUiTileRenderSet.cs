using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// The children a tile layout of either layer last sent to the compositor. A child that is still the layout's but no longer
/// drawn (past the row limit, or the overflow tile once everything fits) loses its render node with the next frame, so it
/// is reset here, as a detached child is, and records from scratch when it is drawn again. Compared only when some children
/// are left out now or were before: otherwise children leave the drawn set only by being detached.
/// </summary>
internal struct SkUiTileRenderSet
{
    private List<ISkUiRenderable>? _previous;
    private int _shownEnd;
    private bool _overflow;
    private bool _partial;
    private bool _sent;

    /// <summary>Whether a layout with these results draws other children than were last sent.</summary>
    public readonly bool Differs(int shownEnd, bool overflow) => _sent && (shownEnd != _shownEnd || overflow != _overflow);

    /// <summary>Records what was sent (<paramref name="sent"/> from <paramref name="start"/>) and resets the children left out.</summary>
    /// <remarks>
    /// Hit tests, hover, semantics and focus collect the render children too, mostly with nothing changed since: the same children
    /// as last time return without allocating.
    /// </remarks>
    public void Update(ISkUiRenderable owner, List<ISkUiRenderable> sent, int start, int shownEnd, bool overflow, int childCount)
    {
        var partial = shownEnd < childCount || overflow;
        var previous = _previous ??= [];
        if (_sent && SameAs(previous, sent, start))
        {
            (_shownEnd, _overflow, _partial) = (shownEnd, overflow, partial);
            return;
        }
        if ((partial || _partial) && previous.Count > 0)
        {
            var now = new HashSet<ISkUiRenderable>(ReferenceEqualityComparer.Instance);
            for (var index = start; index < sent.Count; index++)
                now.Add(sent[index]);
            foreach (var node in previous)
                if (!now.Contains(node) && ReferenceEquals(node.RenderParent, owner))
                    SkUiRenderInvalidation.ResetSubtree(node);
        }
        previous.Clear();
        for (var index = start; index < sent.Count; index++)
            previous.Add(sent[index]);
        (_shownEnd, _overflow, _partial, _sent) = (shownEnd, overflow, partial, true);
    }

    private static bool SameAs(List<ISkUiRenderable> previous, List<ISkUiRenderable> sent, int start)
    {
        if (sent.Count - start != previous.Count)
            return false;
        for (var index = 0; index < previous.Count; index++)
            if (!ReferenceEquals(previous[index], sent[start + index]))
                return false;
        return true;
    }
}
