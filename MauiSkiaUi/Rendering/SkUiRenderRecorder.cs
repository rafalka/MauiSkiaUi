using SkiaSharp;

namespace MauiSkiaUi.Rendering;

/// <summary>
/// UI-thread half of the pipeline: walks only dirty paths of the drawn tree, re-records pictures for nodes
/// whose content changed, snapshots composite-time properties, and packages the result as a
/// <see cref="SkUiRenderBatch"/> for the compositor. Offset / transform / opacity changes never re-record.
/// </summary>
internal sealed class SkUiRenderRecorder : IDisposable
{
    private readonly SKPictureRecorder _recorder = new();
    private readonly List<ISkUiRenderable> _scratch = [];

    /// <summary>Pictures recorded since creation (diagnostics / tests).</summary>
    internal int RecordedPictures { get; private set; }

    /// <summary>Records pending changes under <paramref name="root"/>; returns <c>null</c> when nothing changed.</summary>
    internal SkUiRenderBatch? Sync(ISkUiRenderable root, float rootWidth, float rootHeight, SKColor clearColor, bool forceRoot)
    {
        var batch = new SkUiRenderBatch();
        SyncNode(root, isRoot: true, batch);
        if (!forceRoot && batch.Updates.Count == 0 && batch.Animations.Count == 0)
            return null;
        batch.Root = root.RenderState.Node;
        batch.RootWidth = rootWidth;
        batch.RootHeight = rootHeight;
        batch.ClearColor = clearColor;
        return batch;
    }

    private void SyncNode(ISkUiRenderable node, bool isRoot, SkUiRenderBatch batch)
    {
        var state = node.RenderState;
        var dirty = state.Dirty;
        if (dirty == SkUiRenderDirty.None)
            return;
        // Clear before recording so invalidations raised while recording schedule a follow-up frame.
        state.Dirty = SkUiRenderDirty.None;
        SkUiRenderUpdate? update = null;

        if ((dirty & (SkUiRenderDirty.Props | SkUiRenderDirty.Content)) != 0 || !state.HasCommitted)
        {
            var props = SkUiRenderProps.Default;
            node.GetRenderProps(ref props);
            if (isRoot)
                props.X = props.Y = 0;
            var sizeChanged = !state.HasCommitted
                || props.Width != state.Committed.Width || props.Height != state.Committed.Height
                || props.Overflow != state.Committed.Overflow;
            if (!state.HasCommitted || props != state.Committed)
            {
                var explicitChanges = state.HasCommitted ? SkUiRenderProps.ChangedAnimatable(state.Committed, props) : 0;
                if (explicitChanges != 0 && state.ActiveAnimations is { Count: > 0 } active)
                    foreach (var animation in active)
                        // Only animations committed in earlier frames; ones queued with this frame start after it.
                        if ((animation.PropertyMask & explicitChanges) != 0 && animation.Target is not null)
                            animation.Supersede();
                update = new SkUiRenderUpdate(state.Node)
                {
                    HasProps = true,
                    Props = props,
                    ExplicitAnimatable = explicitChanges
                };
                state.Committed = props;
                state.HasCommitted = true;
            }
            if ((dirty & SkUiRenderDirty.Content) != 0 || sizeChanged)
            {
                if (props.IsSkipped && !FadesIn(state, props) && !RecordsTransparent(node, props))
                {
                    // Nothing visible to record; keep the request until the node can paint again.
                    state.Dirty |= SkUiRenderDirty.Content;
                }
                else
                {
                    update ??= new SkUiRenderUpdate(state.Node);
                    update.HasContent = true;
                    update.Before = Record(node, props.VisualBounds, overlay: false);
                    update.After = node.HasOverlay ? Record(node, props.VisualBounds, overlay: true) : null;
                }
            }
        }

        if (state.PendingScrollShift != SKPoint.Empty)
        {
            update ??= new SkUiRenderUpdate(state.Node);
            update.ScrollShift = state.PendingScrollShift;
            state.PendingScrollShift = SKPoint.Empty;
        }

        if ((dirty & SkUiRenderDirty.Children) != 0)
        {
            var start = _scratch.Count;
            node.GetRenderChildren(_scratch);
            var count = _scratch.Count - start;
            var committed = state.CommittedSources;
            var changed = committed.Length != count;
            for (var index = 0; !changed && index < count; index++)
                changed = !ReferenceEquals(committed[index], _scratch[start + index]);
            if (changed)
                state.CommittedSources = count == 0 ? [] : _scratch.GetRange(start, count).ToArray();
            _scratch.RemoveRange(start, count);
            // Child render nodes are replaced when a child is reset, so always resend identities on a children pass.
            var sources = state.CommittedSources;
            var nodes = new SkUiRenderNode[sources.Length];
            for (var index = 0; index < sources.Length; index++)
                nodes[index] = sources[index].RenderState.Node;
            update ??= new SkUiRenderUpdate(state.Node);
            update.Children = nodes;
        }

        if (state.PendingAnimations is { Count: > 0 } pending)
        {
            foreach (var animation in pending)
            {
                if (animation.IsCancelled)
                    continue;
                animation.Target = state.Node;
                batch.Animations.Add(animation);
            }
            pending.Clear();
        }

        if (update is not null)
            batch.Updates.Add(update);

        foreach (var child in state.CommittedSources)
            SyncNode(child, isRoot: false, batch);
    }

    /// <summary>
    /// True when a node hidden only by <c>Opacity == 0</c> has an opacity animation queued or running: the render
    /// thread will make it visible without another UI sync, so its content must be recorded now.
    /// </summary>
    private static bool FadesIn(SkUiRenderState state, in SkUiRenderProps props)
    {
        if (!props.IsVisible || props.Width <= 0 || props.Height <= 0 || state.ActiveAnimations is not { Count: > 0 } active)
            return false;
        foreach (var animation in active)
            if (!animation.IsCancelled && (animation.PropertyMask & (1 << (int)SkUiRenderProperty.Opacity)) != 0)
                return true;
        return false;
    }

    /// <summary>A node hidden only by <c>Opacity == 0</c> that asks to be recorded ahead of being shown.</summary>
    private static bool RecordsTransparent(ISkUiRenderable node, in SkUiRenderProps props) =>
        props.IsVisible && props.Width > 0 && props.Height > 0 && node.RecordsWhenTransparent;

    private SKPicture? Record(ISkUiRenderable node, SKRect cull, bool overlay)
    {
        var canvas = _recorder.BeginRecording(cull);
        if (overlay)
            node.RecordOverlay(canvas);
        else
            node.RecordContent(canvas);
        var picture = _recorder.EndRecording();
        RecordedPictures++;
        if (picture.ApproximateOperationCount == 0)
        {
            picture.Dispose();
            return null;
        }
        return picture;
    }

    public void Dispose() => _recorder.Dispose();
}

/// <summary>
/// Immediate-mode paint of a live subtree onto any canvas (offscreen snapshots, tests, software fallbacks).
/// Mirrors <see cref="SkUiCompositor"/> composition order exactly, without retained pictures.
/// </summary>
internal static class SkUiImmediatePainter
{
    [ThreadStatic] private static SKPaint? _layerPaint;
    [ThreadStatic] private static SKPaint? _shadowPaint;
    [ThreadStatic] private static List<ISkUiRenderable>? _children;

    internal static void Paint(ISkUiRenderable node, SKCanvas canvas, bool applyOffset) =>
        Paint(node, canvas, applyOffset, pinned: null);

    /// <summary>
    /// Paints <paramref name="node"/> when its <see cref="SkUiRenderProps.Pinned"/> matches <paramref name="pinned"/> (any
    /// when <c>null</c>); returns <c>true</c> when it was left out because it is pinned.
    /// </summary>
    private static bool Paint(ISkUiRenderable node, SKCanvas canvas, bool applyOffset, bool? pinned)
    {
        var props = SkUiRenderProps.Default;
        node.GetRenderProps(ref props);
        if (pinned is { } expected && props.Pinned != expected)
            return props.Pinned;
        if (!applyOffset)
            props.X = props.Y = 0;
        if (props.IsSkipped)
            return false;
        var save = canvas.Save();
        try
        {
            var matrix = props.Matrix;
            canvas.Concat(in matrix);
            if (canvas.QuickReject(props.InkBounds))
                return false;
            if (props.Opacity < 1)
            {
                var paint = _layerPaint ??= new SKPaint();
                paint.Color = SKColors.White.WithAlpha((byte)(255 * props.Opacity));
                canvas.SaveLayer(props.LayerBounds, paint);
            }
            if (props.Shadow is { } shadow)
            {
                // The compositor rasterizes content shadows; drawn live they give the same pixels.
                var shadowPaint = _shadowPaint ??= new SKPaint();
                if (shadow.Outline is not null)
                    SkUiShadowPainter.DrawOutline(canvas, shadow, shadowPaint);
                else
                    SkUiShadowPainter.DrawFromContent(canvas, shadow, shadowPaint, (node, props), static (c, state) => PaintBody(state.node, c, state.props));
            }
            PaintBody(node, canvas, props);
            return false;
        }
        finally
        {
            canvas.RestoreToCount(save);
        }
    }

    /// <summary>The node in its own coordinates, inside its clips: content, children, overlay (the compositor's <c>DrawBody</c>).</summary>
    private static void PaintBody(ISkUiRenderable node, SKCanvas canvas, in SkUiRenderProps props)
    {
        var save = canvas.Save();
        try
        {
            if (props.ClipToBounds)
                canvas.ClipRect(props.Bounds);
            if (props.ClipPath is { } clipPath)
                canvas.ClipPath(clipPath, antialias: true);
            node.RecordContent(canvas);
            var children = _children ??= [];
            var start = children.Count;
            node.GetRenderChildren(children);
            var end = children.Count;
            if (end > start)
            {
                var childSave = canvas.Save();
                if (!props.ChildrenClipRect.IsEmpty)
                    canvas.ClipRect(props.ChildrenClipRect);
                if (props.ChildrenClipPath is { } path)
                    canvas.ClipPath(path, antialias: true);
                try
                {
                    // Unpinned children in the children space, then pinned ones (in local coordinates, outside the
                    // children clip) above them.
                    props.TransformChildren(canvas);
                    var pinned = false;
                    for (var index = start; index < end; index++)
                        pinned |= Paint(children[index], canvas, applyOffset: true, pinned: false);
                    canvas.RestoreToCount(childSave);
                    if (pinned)
                        for (var index = start; index < end; index++)
                            Paint(children[index], canvas, applyOffset: true, pinned: true);
                }
                finally
                {
                    canvas.RestoreToCount(childSave);
                    children.RemoveRange(start, end - start);
                }
            }
            if (node.HasOverlay)
                node.RecordOverlay(canvas);
        }
        finally
        {
            canvas.RestoreToCount(save);
        }
    }
}
