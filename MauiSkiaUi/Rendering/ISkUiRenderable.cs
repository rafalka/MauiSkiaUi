using SkiaSharp;

namespace MauiSkiaUi.Rendering;

/// <summary>
/// UI-thread side of a drawn node (<see cref="SkUiView"/> or <see cref="Core.SkUiCoreNode"/>).
/// The frame recorder reads these members on the UI thread only; the render thread never touches them.
/// </summary>
internal interface ISkUiRenderable
{
    /// <summary>Per-node recording bookkeeping and the render-thread node it feeds.</summary>
    SkUiRenderState RenderState { get; }

    /// <summary>Drawn parent in the unified render tree, or <c>null</c> for a surface root / detached node.</summary>
    ISkUiRenderable? RenderParent { get; }

    /// <summary>Fills composite-time properties from the live node.</summary>
    void GetRenderProps(ref SkUiRenderProps props);

    /// <summary>Records Background + Content phases (not children) in local DIPs.</summary>
    void RecordContent(SKCanvas canvas);

    /// <summary>Whether <see cref="RecordOverlay"/> draws anything.</summary>
    bool HasOverlay { get; }

    /// <summary>Records the Overlay phase (after children) in local DIPs.</summary>
    void RecordOverlay(SKCanvas canvas);

    /// <summary>
    /// Records its content even while fully transparent (opacity 0), so that showing it later (a render-thread fade, a
    /// scroll bar appearing when scrolling starts) records nothing.
    /// </summary>
    bool RecordsWhenTransparent => false;

    /// <summary>Appends drawn children in paint (back-to-front) order.</summary>
    void GetRenderChildren(List<ISkUiRenderable> children);

    /// <summary>
    /// Called when invalidation reaches a node with no render parent (surface root).
    /// <paramref name="fromDescendant"/> is <c>false</c> when the root itself was marked.
    /// </summary>
    void OnRenderRootDirty(bool fromDescendant);
}

/// <summary>What changed on a node since it was last recorded.</summary>
[Flags]
internal enum SkUiRenderDirty : byte
{
    None = 0,
    /// <summary>Content/overlay pictures must be re-recorded.</summary>
    Content = 1,
    /// <summary>Composite-time properties (offset, size, transform, opacity, clip) changed.</summary>
    Props = 2,
    /// <summary>The child list or its order changed.</summary>
    Children = 4,
    /// <summary>This node or a descendant has pending work (walk needed).</summary>
    Subtree = 8,
    All = Content | Props | Children | Subtree
}

/// <summary>UI-thread recording bookkeeping for one node.</summary>
internal sealed class SkUiRenderState
{
    public SkUiRenderNode Node { get; private set; } = new();
    public SkUiRenderDirty Dirty = SkUiRenderDirty.All;
    public SkUiRenderProps Committed;
    public bool HasCommitted;
    public ISkUiRenderable[] CommittedSources = [];
    public List<SkUiRenderAnimation>? PendingAnimations;
    public List<SkUiRenderAnimation>? ActiveAnimations;

    /// <summary>
    /// Records a value the render thread already shows (animation report / completion) as committed, so the next
    /// sync does not treat it as a UI change that would cancel the running animation.
    /// </summary>
    public void Acknowledge(SkUiRenderProperty property, float value)
    {
        if (HasCommitted)
            Committed.Set(property, value);
    }

    /// <summary>Forgets everything sent to the compositor: the next sync records this node from scratch into a new render node.</summary>
    public void Reset()
    {
        // Committed animations are finished by the compositor once it sees the cancel; ones never sent to it
        // would never finish, so they are finished here (after the reset, so handlers see a consistent state).
        SkUiRenderAnimation[]? unsent = PendingAnimations is { Count: > 0 } pending ? [.. pending] : null;
        if (ActiveAnimations is { } active)
        {
            foreach (var animation in active)
                animation.Cancel();
            active.Clear();
        }
        PendingAnimations?.Clear();
        Node = new SkUiRenderNode();
        Dirty = SkUiRenderDirty.All;
        HasCommitted = false;
        CommittedSources = [];
        if (unsent is not null)
            foreach (var animation in unsent)
            {
                animation.Cancel();
                animation.NotifyFinished(completed: false);
            }
    }
}

/// <summary>Invalidation helpers shared by <see cref="SkUiView"/> and <see cref="Core.SkUiCoreNode"/>.</summary>
internal static class SkUiRenderInvalidation
{
    /// <summary>
    /// Marks <paramref name="node"/> dirty and propagates a <see cref="SkUiRenderDirty.Subtree"/> mark upward,
    /// stopping at the first ancestor already marked. Only the first mark per frame reaches the root, so N
    /// invalidations cost O(N + depth) instead of O(N × depth).
    /// </summary>
    public static void Mark(ISkUiRenderable node, SkUiRenderDirty flags)
    {
        var state = node.RenderState;
        var alreadyMarked = (state.Dirty & SkUiRenderDirty.Subtree) != 0;
        state.Dirty |= flags | SkUiRenderDirty.Subtree;
        if (alreadyMarked)
        {
            // A root whose previous frame was skipped (e.g. zero size) keeps stale marks; always wake it.
            if (node.RenderParent is null)
                node.OnRenderRootDirty(fromDescendant: false);
            return;
        }
        var current = node;
        while (true)
        {
            var parent = current.RenderParent;
            if (parent is null)
            {
                current.OnRenderRootDirty(fromDescendant: !ReferenceEquals(current, node));
                return;
            }
            var parentState = parent.RenderState;
            if ((parentState.Dirty & SkUiRenderDirty.Subtree) != 0)
                return;
            parentState.Dirty |= SkUiRenderDirty.Subtree;
            current = parent;
        }
    }

    /// <summary>
    /// The look or color scheme changed: every drawn node under <paramref name="node"/> forgets its cached measure and
    /// re-records. Marks stop at already-marked ancestors, so the walk costs O(nodes); the caller re-lays out the root.
    /// </summary>
    public static void MarkLookChanged(ISkUiRenderable node, List<ISkUiRenderable>? scratch = null)
    {
        switch (node)
        {
            case SkUiView view: view.MarkLookChanged(); break;
            case Core.SkUiCoreNode core: core.MarkLookChanged(); break;
            default: Mark(node, SkUiRenderDirty.Content); break;
        }
        scratch ??= [];
        var start = scratch.Count;
        node.GetRenderChildren(scratch);
        var end = scratch.Count;
        for (var index = start; index < end; index++)
            MarkLookChanged(scratch[index], scratch);
        scratch.RemoveRange(start, end - start);
    }

    /// <summary>Resets a detached subtree so a later attach re-records it into fresh render nodes.</summary>
    public static void ResetSubtree(ISkUiRenderable node, List<ISkUiRenderable>? scratch = null)
    {
        node.RenderState.Reset();
        scratch ??= [];
        var start = scratch.Count;
        node.GetRenderChildren(scratch);
        var end = scratch.Count;
        for (var index = start; index < end; index++)
            ResetSubtree(scratch[index], scratch);
        scratch.RemoveRange(start, end - start);
    }

    /// <summary>Queues a render-thread animation on the node; it is sent with the next committed frame.</summary>
    public static void Enqueue(ISkUiRenderable node, SkUiRenderAnimation animation)
    {
        var state = node.RenderState;
        (state.PendingAnimations ??= []).Add(animation);
        (state.ActiveAnimations ??= []).Add(animation);
        animation.Owner = state;
        Mark(node, SkUiRenderDirty.None);
    }
}
