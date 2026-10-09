using SkiaSharp;

namespace MauiSkiaUi.Rendering;

/// <summary>Render-thread node: retained pictures plus composite-time properties.</summary>
internal sealed class SkUiRenderNode
{
    /// <summary>Current (possibly animated) properties.</summary>
    internal SkUiRenderProps Props = SkUiRenderProps.Default;

    internal SKPicture? Before;
    internal SKPicture? After;
    internal SkUiRenderNode[] Children = [];
    internal SkUiRenderNode? Parent;
    internal int AnimatedMask;
    /// <summary>Properties were applied at least once (the first commit sets every property).</summary>
    internal bool PropsCommitted;
    internal int Epoch;
    internal bool Disposed;

    /// <summary>Bumped when this node or a descendant draws differently (content, children, props, animation).</summary>
    internal int Version;

    /// <summary><see cref="SkUiCompositor.FrameCount"/> when <see cref="Version"/> last changed.</summary>
    internal long ChangedFrame;

    /// <summary>Last change pass that bumped <see cref="Version"/> (one walk up per pass).</summary>
    internal int TouchStamp;

    /// <summary>Raster of the content shadow and what it was made from (see <see cref="SkUiCompositor"/>).</summary>
    internal SkUiShadowCache? ShadowCache;
}

/// <summary>
/// A content shadow rasterized on the render thread, reused while the node's subtree, shadow and density stay the same,
/// so offset, opacity and transform changes (scrolling, <c>AnimateAsync</c>) never blur again.
/// </summary>
internal sealed class SkUiShadowCache(SKImage? image, int version, SkUiRenderShadow shadow, float scale, bool moving) : IDisposable
{
    public SKImage? Image { get; } = image;
    public int Version { get; } = version;
    public SkUiRenderShadow Shadow { get; } = shadow;
    public float Scale { get; } = scale;

    /// <summary>The subtree had spinning or sliding content while rasterized: draw this shadow live while it lasts.</summary>
    public bool Moving { get; } = moving;

    public bool Matches(SkUiRenderNode node, SkUiRenderShadow shadow, float scale) =>
        Version == node.Version && Scale == scale && (ReferenceEquals(Shadow, shadow)
            || (ReferenceEquals(Shadow.Style, shadow.Style) && Shadow.Source == shadow.Source && shadow.Outline is null));

    public void Dispose() => Image?.Dispose();
}

/// <summary>One node's changes in a committed frame.</summary>
internal sealed class SkUiRenderUpdate(SkUiRenderNode node)
{
    public SkUiRenderNode Node { get; } = node;
    public bool HasProps;
    public SkUiRenderProps Props;

    /// <summary>Animatable properties the UI deliberately changed (acknowledged render-thread values excluded).</summary>
    public int ExplicitAnimatable;
    public bool HasContent;
    public SKPicture? Before;
    public SKPicture? After;
    public SkUiRenderNode[]? Children;

    /// <summary>
    /// A scroll correction (scroll anchoring: content before the viewport changed size): moves the node's running scroll
    /// motion by this much, in the same frame as the layout change that caused it.
    /// </summary>
    public SKPoint ScrollShift;
}

/// <summary>Everything the UI thread produced for one frame; applied atomically by the render thread.</summary>
internal sealed class SkUiRenderBatch
{
    public readonly List<SkUiRenderUpdate> Updates = [];
    public readonly List<SkUiRenderAnimation> Animations = [];
    public SkUiRenderNode? Root;
    public float RootWidth;
    public float RootHeight;
    public SKColor ClearColor;

    /// <summary>
    /// Releases pictures and effects of a batch that will never be applied (the compositor was disposed; the UI side then
    /// forgets its effects, <see cref="ISkUiRenderable.ReleaseDrawingResources"/>).
    /// </summary>
    public void Discard()
    {
        foreach (var update in Updates)
        {
            update.Before?.Dispose();
            update.After?.Dispose();
            if (update.HasProps)
                SkUiCompositor.ReleaseEffects(update.Props);
        }
        Updates.Clear();
    }
}

/// <summary>
/// Retained compositor for one drawn surface. The UI thread <see cref="Commit"/>s recorded batches; the render
/// thread (Metal/GL thread, or the UI thread for software surfaces) calls <see cref="Render"/>, which applies
/// pending batches, advances render-thread animations, and replays the retained tree. Nothing here touches
/// MAUI objects, so rendering and composite-time animation keep running while the UI thread is busy.
/// </summary>
internal sealed class SkUiCompositor : IDisposable
{
    private readonly object _pendingLock = new();
    private readonly object _renderLock = new();
    private readonly Action<Action> _postToUi;
    private readonly SKPaint _layerPaint = new();
    private readonly SKPaint _shadowPaint = new();
    private readonly SKPaint _clearPaint = new() { BlendMode = SKBlendMode.Src };
    private readonly List<SkUiRenderNode> _removed = [];
    private readonly List<SkUiRenderAnimation> _animations = [];
    private List<SkUiRenderBatch> _pending = [];
    private List<SkUiRenderBatch> _applying = [];
    private SkUiRenderNode? _root;
    private float _rootWidth;
    private float _rootHeight;
    private SKColor _clearColor = SKColors.Transparent;
    private TimeSpan _now;
    private bool _spinning;
    private bool _rasterizePending;
    private int _rasterizedThisFrame;

    /// <summary>
    /// Content shadows rasterized per frame at most; the others are drawn live and rasterized in the next frames, so
    /// many shadows appearing at once (a list scrolled into view) spread their CPU cost instead of stalling one frame.
    /// </summary>
    internal const int MaxShadowRasterizationsPerFrame = 3;
    private float _scale = 1;
    private int _touchStamp;
    private int _epoch;
    private int _activeAnimations;
    private volatile bool _hasPending;
    private volatile bool _continuous;
    private bool _disposed;

    /// <param name="postToUi">Posts render-thread feedback (animation reports / completion) to the UI thread.</param>
    internal SkUiCompositor(Action<Action> postToUi) => _postToUi = postToUi;

    /// <summary>Root DIP size last applied by the render thread.</summary>
    internal Size RootSize { get; private set; }

    /// <summary>True while another frame is required (pending commits, running animations, spinning content).</summary>
    internal bool NeedsFrame => _hasPending || _continuous || Volatile.Read(ref _activeAnimations) > 0;

    /// <summary>Frames drawn (diagnostics / tests).</summary>
    internal long FrameCount { get; private set; }

#if SKUI_DIAGNOSTICS
    /// <summary>Per-frame timings while set (diagnostics, stress pages); set from any thread, written on the render thread.</summary>
    internal volatile SkUiFrameTrace? FrameTrace;
#endif

    /// <summary>Content shadows rasterized into a cache since creation (diagnostics / tests).</summary>
    internal int ShadowRasterizations { get; private set; }

    /// <summary>Content shadows drawn live (their subtree was changing) since creation (diagnostics / tests).</summary>
    internal int LiveShadows { get; private set; }

    private long _statFrames;
    private long _statTicks;
    private long _statMaxTicks;

    /// <summary>Render-thread cost of <see cref="Render"/> since the last <see cref="ResetStatistics"/> (any thread).</summary>
    internal SkUiRenderStatistics Statistics
    {
        get
        {
            var frames = Interlocked.Read(ref _statFrames);
            var ticks = Interlocked.Read(ref _statTicks);
            var max = Interlocked.Read(ref _statMaxTicks);
            var toMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            return new SkUiRenderStatistics(frames, frames == 0 ? 0 : ticks * toMs / frames, max * toMs);
        }
    }

    /// <summary>Clears <see cref="Statistics"/> (any thread).</summary>
    internal void ResetStatistics()
    {
        Interlocked.Exchange(ref _statFrames, 0);
        Interlocked.Exchange(ref _statTicks, 0);
        Interlocked.Exchange(ref _statMaxTicks, 0);
    }

    /// <summary>UI thread: queues a recorded frame. Safe to call while the render thread renders.</summary>
    internal void Commit(SkUiRenderBatch batch)
    {
        lock (_pendingLock)
        {
            if (_disposed)
            {
                DiscardWithAnimations(batch);
                return;
            }
            _pending.Add(batch);
            _hasPending = true;
        }
    }

    /// <summary>
    /// Render thread: applies pending commits, ticks animations at <paramref name="now"/>, and draws into
    /// <paramref name="canvas"/> sized <paramref name="pixelWidth"/>×<paramref name="pixelHeight"/>.
    /// Returns <c>true</c> when another frame is needed.
    /// </summary>
    internal bool Render(SKCanvas canvas, int pixelWidth, int pixelHeight, TimeSpan now) =>
        RenderCore(canvas, pixelWidth, pixelHeight, now);

    /// <summary>True once a committed frame exists (frames before that only clear and are not counted).</summary>
    internal bool HasContent => _root is not null;

    /// <summary>
    /// Render thread: adds one frame to <see cref="Statistics"/>. Surfaces call it after flushing and presenting,
    /// so GPU command submission (and first-use pipeline compilation) is included.
    /// </summary>
    internal void RecordFrameStatistics(long elapsed)
    {
#if SKUI_DIAGNOSTICS
        FrameTrace?.End();
#endif
        Interlocked.Increment(ref _statFrames);
        Interlocked.Add(ref _statTicks, elapsed);
        long max;
        while (elapsed > (max = Interlocked.Read(ref _statMaxTicks))
            && Interlocked.CompareExchange(ref _statMaxTicks, elapsed, max) != max) { }
    }

    private bool RenderCore(SKCanvas canvas, int pixelWidth, int pixelHeight, TimeSpan now)
    {
        lock (_renderLock)
        {
            ResetCanvas(canvas);
            if (_disposed)
            {
                canvas.Clear(SKColors.Transparent);
                return false;
            }
#if SKUI_DIAGNOSTICS
            var trace = FrameTrace;
            trace?.Begin(ShadowRasterizations);
            var (batches, updates) = ApplyPending();
            trace?.Applied(batches, updates);
#else
            ApplyPending();
#endif
            _now = now;
            var reports = TickAnimations(now);
#if SKUI_DIAGNOSTICS
            trace?.Animated();
#endif
            if (_rootWidth > 0)
                _scale = pixelWidth / _rootWidth;

            _clearPaint.Color = _clearColor;
            canvas.DrawRect(SKRect.Create(pixelWidth, pixelHeight), _clearPaint);
            _spinning = false;
            _rasterizePending = false;
            _rasterizedThisFrame = 0;
            if (_root is { } root && _rootWidth > 0 && _rootHeight > 0 && pixelWidth > 0 && pixelHeight > 0)
            {
                var save = canvas.Save();
                canvas.Scale(pixelWidth / _rootWidth, pixelHeight / _rootHeight);
                DrawNode(canvas, root, isRoot: true);
                canvas.RestoreToCount(save);
            }
#if SKUI_DIAGNOSTICS
            trace?.Drawn(ShadowRasterizations);
#endif
            FrameCount++;
            // A content shadow drawn live because it just changed (or over the budget) is rasterized on the next frame,
            // while idle, instead of at the start of the next scroll.
            _continuous = _spinning || _animations.Count > 0 || _rasterizePending;
            if (reports is not null)
                _postToUi(() =>
                {
                    foreach (var report in reports)
                        report();
                });
            return NeedsFrame;
        }
    }

    private static void ResetCanvas(SKCanvas canvas)
    {
        while (canvas.SaveCount > 1)
            canvas.Restore();
        canvas.ResetMatrix();
    }

    /// <summary>Applies the committed batches; returns how many, and their node updates (diagnostics).</summary>
    private (int Batches, int Updates) ApplyPending()
    {
        lock (_pendingLock)
        {
            (_pending, _applying) = (_applying, _pending);
            _hasPending = false;
        }
        var batches = _applying.Count;
        var updates = 0;
        foreach (var batch in _applying)
        {
            updates += batch.Updates.Count;
            Apply(batch);
        }
        _applying.Clear();
        return (batches, updates);
    }

    private void Apply(SkUiRenderBatch batch)
    {
        var stamp = ++_touchStamp;
        foreach (var update in batch.Updates)
        {
            var node = update.Node;
            if (node.Disposed)
            {
                // Recorded before its UI node was reset (detached): nothing commits these objects again.
                update.Before?.Dispose();
                update.After?.Dispose();
                if (update.HasProps)
                    ReleaseEffects(update.Props);
                continue;
            }
            var bodyChanged = update.HasContent || update.Children is not null
                || (update.HasProps && BodyChanged(node.Props, update.Props));
            // The scroll offset axes whose shown value stays the render thread's after this update (a correction moves them).
            var retainedOffset = node.PropsCommitted ? ScrollOffsetMask : 0;
            if (update.HasProps)
            {
                var incoming = update.Props;
                var overridden = update.ExplicitAnimatable & node.AnimatedMask;
                if (overridden != 0)
                    CancelAnimations(node, overridden);
                // Properties still animating keep their render-thread value unless the UI set them. So do, once committed,
                // the scroll offset and scale, which only render-thread motions, corrections and explicit UI changes move: a
                // value the UI merely acknowledged can be older than what shows (a motion that ended here before its last
                // report reached the UI), and must not move the content back.
                var owned = node.AnimatedMask | (node.PropsCommitted ? SkUiRenderOverscrollSettle.ScrollMask : 0);
                var keep = owned & ~update.ExplicitAnimatable;
                retainedOffset = keep & ScrollOffsetMask;
                if (keep != 0)
                    for (var property = 0; property < SkUiRenderPropertyCount.Value; property++)
                        if ((keep & (1 << property)) != 0)
                            incoming.Set((SkUiRenderProperty)property, node.Props.Get((SkUiRenderProperty)property));
                ReleaseReplacedEffects(node.Props, incoming);
                node.Props = incoming;
                node.PropsCommitted = true;
            }
            if (update.ScrollShift != SKPoint.Empty)
                ShiftScroll(node, update.ScrollShift, retainedOffset);
            if (update.HasContent)
            {
                if (!ReferenceEquals(node.Before, update.Before)) node.Before?.Dispose();
                if (!ReferenceEquals(node.After, update.After)) node.After?.Dispose();
                node.Before = update.Before;
                node.After = update.After;
            }
            if (update.Children is { } children)
            {
                var epoch = ++_epoch;
                foreach (var child in children)
                {
                    child.Epoch = epoch;
                    child.Parent = node;
                }
                foreach (var old in node.Children)
                {
                    if (old.Epoch != epoch && ReferenceEquals(old.Parent, node))
                    {
                        old.Parent = null;
                        _removed.Add(old);
                    }
                }
                node.Children = children;
            }
            Touch(bodyChanged ? node : node.Parent, stamp);
        }

        if (batch.Root is { } root)
        {
            if (_root is { } previous && !ReferenceEquals(previous, root) && previous.Parent is null)
                _removed.Add(previous);
            _root = root;
            _rootWidth = batch.RootWidth;
            _rootHeight = batch.RootHeight;
            RootSize = new Size(batch.RootWidth, batch.RootHeight);
            _clearColor = batch.ClearColor;
        }

        foreach (var removed in _removed)
            if (removed.Parent is null && !ReferenceEquals(removed, _root))
                DisposeSubtree(removed);
        _removed.Clear();

        foreach (var animation in batch.Animations)
        {
            if (animation.Target is not { Disposed: false } target || animation.IsCancelled)
            {
                Finish(animation, completed: false, reports: null);
                continue;
            }
            if ((target.AnimatedMask & animation.PropertyMask) != 0)
                CancelAnimations(target, animation.PropertyMask);
            target.AnimatedMask |= animation.PropertyMask;
            _animations.Add(animation);
            Interlocked.Increment(ref _activeAnimations);
        }
    }

    /// <summary>
    /// Disposes the clip paths and shadow objects a commit replaced. The UI side keeps only its newest ones and never commits
    /// a replaced one again, so once the render thread stops drawing with them (here, between frames) nothing uses them:
    /// clip or shadow edits (theme toggles, animated brushes) do not pile up native Skia objects until the GC finalizes
    /// them. A removed node's and a disposed compositor's are released too (<see cref="ReleaseEffects"/>).
    /// </summary>
    private static void ReleaseReplacedEffects(in SkUiRenderProps current, in SkUiRenderProps incoming)
    {
        if (current.ClipPath is { } clip && !ReferenceEquals(clip, incoming.ClipPath))
            clip.Dispose();
        if (current.ChildrenClipPath is { } children && !ReferenceEquals(children, incoming.ChildrenClipPath))
            children.Dispose();
        if (current.ContentClipPath is { } content && !ReferenceEquals(content, incoming.ContentClipPath))
            content.Dispose();
        if (current.Shadow is not { } shadow || ReferenceEquals(shadow, incoming.Shadow))
            return;
        if (shadow.Outline is { } outline && !ReferenceEquals(outline, incoming.Shadow?.Outline))
            outline.Dispose();
        if (!ReferenceEquals(shadow.Style, incoming.Shadow?.Style))
            shadow.Style.Dispose();
    }

    /// <summary>
    /// Records that <paramref name="node"/> and its ancestors draw differently (their content shadows must be rasterized
    /// again); each node is bumped once per change pass.
    /// </summary>
    private void Touch(SkUiRenderNode? node, int stamp)
    {
        for (; node is not null && node.TouchStamp != stamp; node = node.Parent)
        {
            node.TouchStamp = stamp;
            node.Version++;
            node.ChangedFrame = FrameCount;
        }
    }

    /// <summary>
    /// Whether the node itself draws differently in its own coordinates; its offset, transform and opacity only change how
    /// its parent draws it.
    /// </summary>
    private static bool BodyChanged(in SkUiRenderProps a, in SkUiRenderProps b) =>
        a.Width != b.Width || a.Height != b.Height || a.IsVisible != b.IsVisible || a.ClipToBounds != b.ClipToBounds
        || a.ChildrenOffsetX != b.ChildrenOffsetX || a.ChildrenOffsetY != b.ChildrenOffsetY || a.ChildrenClipRect != b.ChildrenClipRect
        || a.ChildrenScaleX != b.ChildrenScaleX || a.ChildrenScaleY != b.ChildrenScaleY || a.ChildrenScaleOrigin != b.ChildrenScaleOrigin
        || !ReferenceEquals(a.ChildrenClipPath, b.ChildrenClipPath) || !ReferenceEquals(a.ClipPath, b.ClipPath)
        || a.ContentSpinPeriod != b.ContentSpinPeriod || a.ContentSlidePeriod != b.ContentSlidePeriod
        || a.ContentSlideDistance != b.ContentSlideDistance || !ReferenceEquals(a.ContentClipPath, b.ContentClipPath)
        || a.Overflow != b.Overflow;

    /// <summary>Children offsets and scales change how the animated node draws its children; the other properties only its place.</summary>
    private const int BodyAnimatedMask = (1 << (int)SkUiRenderProperty.ChildrenOffsetX) | (1 << (int)SkUiRenderProperty.ChildrenOffsetY)
        | (1 << (int)SkUiRenderProperty.ChildrenScaleX) | (1 << (int)SkUiRenderProperty.ChildrenScaleY);

    private const int ScrollOffsetMask = (1 << (int)SkUiRenderProperty.ChildrenOffsetX) | (1 << (int)SkUiRenderProperty.ChildrenOffsetY);

    /// <summary>
    /// Moves the scroll motion running on <paramref name="node"/> (fling, animated scroll, snap) by a scroll correction, and
    /// the offset it shows, so the frame that brings the corrected layout also brings the corrected offset.
    /// </summary>
    /// <param name="node">The scrolling node.</param>
    /// <param name="shift">The correction, in DIPs.</param>
    /// <param name="retainedOffset">
    /// The offset axes that show the render thread's value after the update (<see cref="ScrollOffsetMask"/> bits): all, unless
    /// the UI set the offset explicitly in the same update (which already includes the correction). Those move by it.
    /// </param>
    private void ShiftScroll(SkUiRenderNode node, SKPoint shift, int retainedOffset)
    {
        foreach (var animation in _animations)
            if (ReferenceEquals(animation.Target, node) && !animation.IsCancelled && (animation.PropertyMask & ScrollOffsetMask) != 0)
                animation.ShiftScroll(shift.X, shift.Y);
        // Also when the motion has ended here (finished or cancelled) before the correction arrived: the UI counts the
        // correction as shown, so the offset shown must move by it, or the two would stay apart until the next scroll.
        if ((retainedOffset & (1 << (int)SkUiRenderProperty.ChildrenOffsetX)) != 0)
            node.Props.ChildrenOffsetX += shift.X;
        if ((retainedOffset & (1 << (int)SkUiRenderProperty.ChildrenOffsetY)) != 0)
            node.Props.ChildrenOffsetY += shift.Y;
    }

    private void CancelAnimations(SkUiRenderNode node, int mask)
    {
        foreach (var animation in _animations)
            if (ReferenceEquals(animation.Target, node) && (animation.PropertyMask & mask) != 0)
                animation.Cancel();
    }

    private List<Action>? TickAnimations(TimeSpan now)
    {
        List<Action>? reports = null;
        var stamp = _animations.Count > 0 ? ++_touchStamp : 0;
        for (var index = 0; index < _animations.Count;)
        {
            var animation = _animations[index];
            var target = animation.Target!;
            if (animation.IsCancelled || target.Disposed)
            {
                RemoveAnimationAt(index, animation, completed: false, ref reports);
                continue;
            }
            if (!animation.Started)
            {
                animation.Started = true;
                animation.StartTime = now;
                animation.OnStart(target.Props);
            }
            var done = animation.Advance(now - animation.StartTime, ref target.Props);
            Touch((animation.PropertyMask & BodyAnimatedMask) != 0 ? target : target.Parent, stamp);
            if (animation.TakeReport(target.Props) is { } report)
                (reports ??= []).Add(report);
            if (done)
            {
                RemoveAnimationAt(index, animation, completed: true, ref reports);
                continue;
            }
            index++;
        }
        return reports;
    }

    private void RemoveAnimationAt(int index, SkUiRenderAnimation animation, bool completed, ref List<Action>? reports)
    {
        _animations.RemoveAt(index);
        Interlocked.Decrement(ref _activeAnimations);
        if (animation.Target is { } target)
        {
            var mask = 0;
            foreach (var other in _animations)
                if (ReferenceEquals(other.Target, target) && !other.IsCancelled)
                    mask |= other.PropertyMask;
            target.AnimatedMask = mask;
            if (!completed && !target.Disposed && animation.TakeReport(target.Props) is { } report)
                (reports ??= []).Add(report);
        }
        Finish(animation, completed, reports ??= []);
    }

    private void Finish(SkUiRenderAnimation animation, bool completed, List<Action>? reports)
    {
        if (animation.Finished is null)
            return;
        if (reports is not null)
            reports.Add(() => animation.NotifyFinished(completed));
        else
            _postToUi(() => animation.NotifyFinished(completed));
    }

    /// <summary>Drops a batch that will never be applied: releases its pictures and finishes its animations as cancelled.</summary>
    private void DiscardWithAnimations(SkUiRenderBatch batch)
    {
        batch.Discard();
        foreach (var animation in batch.Animations)
        {
            animation.Cancel();
            Finish(animation, completed: false, reports: null);
        }
        batch.Animations.Clear();
    }

    private void DrawNode(SKCanvas canvas, SkUiRenderNode node, bool isRoot)
    {
        ref readonly var props = ref node.Props;
        if (props.IsSkipped)
            return;
        var save = canvas.Save();
        var matrix = props.Link is { } link ? Follow(props, link) : props.Matrix;
        if (isRoot)
            matrix = matrix.PostConcat(SKMatrix.CreateTranslation(-props.X, -props.Y));
        canvas.Concat(in matrix);
        if (canvas.QuickReject(props.InkBounds))
        {
            canvas.RestoreToCount(save);
            return;
        }
        if (props.Opacity < 1)
        {
            _layerPaint.Color = SKColors.White.WithAlpha((byte)(255 * props.Opacity));
            canvas.SaveLayer(props.LayerBounds, _layerPaint);
        }
        if (props.Shadow is { } shadow)
            DrawShadow(canvas, node, shadow);
        else if (node.ShadowCache is not null)
            DisposeShadowCache(node);
        DrawBody(canvas, node, ownSave: false); // inside this node's save
        canvas.RestoreToCount(save);
    }

    /// <summary>
    /// The matrix of a scroll-linked node: its translation from the source's current (possibly render-thread animated)
    /// children offset. A disposed source (detached) leaves the committed translation.
    /// </summary>
    private static SKMatrix Follow(in SkUiRenderProps props, SkUiRenderLink link)
    {
        if (link.Source.Disposed)
            return props.Matrix;
        ref readonly var source = ref link.Source.Props;
        var translation = link.Evaluate(source.ChildrenOffsetX, source.ChildrenOffsetY);
        return props.GetMatrix(translation.X, translation.Y);
    }

    /// <summary>
    /// The node's shadow (FR-20), before the node and outside its clips. An outline shadow is blurred directly (Skia caches
    /// the masks); a content shadow is rasterized once its subtree stopped changing and reused while it stays the same, and
    /// drawn live while the subtree changes from frame to frame or has spinning content.
    /// </summary>
    /// <remarks>
    /// The raster is in the node's own coordinates and keyed by the node's subtree <see cref="SkUiRenderNode.Version"/>,
    /// the shadow's style and source area and the density (<see cref="SkUiShadowCache.Matches"/>); it lives on the node,
    /// so it is never shared between nodes. Moving, fading or transforming the node (or scrolling its parent) bumps only
    /// the ancestors' versions, and a new shadow object with the same style (a repaint) keeps the raster: none of these
    /// blur again. Content, children or size changes bump the node's version: the shadow is drawn live in that frame and
    /// rasterized in the next one, which is requested for it (at most <see cref="MaxShadowRasterizationsPerFrame"/> per
    /// frame).
    /// </remarks>
    private void DrawShadow(SKCanvas canvas, SkUiRenderNode node, SkUiRenderShadow shadow)
    {
        if (shadow.Outline is not null)
        {
            if (node.ShadowCache is not null)
                DisposeShadowCache(node);
            SkUiShadowPainter.DrawOutline(canvas, shadow, _shadowPaint);
            return;
        }
        if (node.ShadowCache is { } cache && cache.Matches(node, shadow, _scale))
        {
            if (!cache.Moving)
            {
                if (cache.Image is { } cached)
                    SkUiShadowPainter.DrawCached(canvas, shadow, cached);
                return;
            }
        }
        else if (node.ChangedFrame == FrameCount || _rasterizedThisFrame >= MaxShadowRasterizationsPerFrame)
        {
            _rasterizePending = true;
        }
        else
        {
            // Unchanged since the last frame: rasterize once (spinning content is only seen while drawing it).
            DisposeShadowCache(node);
            var spinning = _spinning;
            _spinning = false;
            var image = SkUiShadowPainter.Rasterize(shadow, _scale, _shadowPaint, (this, node), static (c, state) => state.Item1.DrawBody(c, state.Item2, ownSave: true));
            var moving = _spinning;
            _spinning |= spinning;
            ShadowRasterizations++;
            _rasterizedThisFrame++;
            if (moving)
            {
                image?.Dispose();
                image = null;
            }
            node.ShadowCache = new SkUiShadowCache(image, node.Version, shadow, _scale, moving);
            if (!moving)
            {
                if (image is not null)
                    SkUiShadowPainter.DrawCached(canvas, shadow, image);
                return;
            }
        }
        LiveShadows++;
        SkUiShadowPainter.DrawFromContent(canvas, shadow, _shadowPaint, (this, node), static (c, state) => state.Item1.DrawBody(c, state.Item2, ownSave: true));
    }

    private static void DisposeShadowCache(SkUiRenderNode node)
    {
        node.ShadowCache?.Dispose();
        node.ShadowCache = null;
    }

    /// <summary>
    /// What the node draws in its own coordinates, inside its clips: content, children, overlay. Without
    /// <paramref name="ownSave"/> the clips stay on the canvas (the caller restores).
    /// </summary>
    private void DrawBody(SKCanvas canvas, SkUiRenderNode node, bool ownSave)
    {
        ref readonly var props = ref node.Props;
        var save = ownSave ? canvas.Save() : -1;
        if (props.ClipToBounds)
            canvas.ClipRect(props.Bounds);
        if (props.ClipPath is { } clipPath)
            canvas.ClipPath(clipPath, antialias: true);
        if (node.Before is { } before)
        {
            if (props.ContentSpinPeriod > 0)
            {
                _spinning = true;
                var period = props.ContentSpinPeriod;
                var angle = (float)(_now.TotalSeconds % period / period * 360);
                var spin = canvas.Save();
                canvas.RotateDegrees(angle, props.Width / 2, props.Height / 2);
                canvas.DrawPicture(before);
                canvas.RestoreToCount(spin);
            }
            else if (props.ContentSlidePeriod > 0 && props.ContentSlideDistance != 0)
            {
                _spinning = true;
                var period = props.ContentSlidePeriod;
                var distance = props.ContentSlideDistance;
                var offset = (float)(_now.TotalSeconds % period / period) * distance;
                var slide = canvas.Save();
                if (props.ContentClipPath is { } clip)
                    canvas.ClipPath(clip, antialias: true);
                else
                    canvas.ClipRect(props.Bounds);
                // Two copies one distance apart cover the whole distance for any phase.
                canvas.Translate(offset, 0);
                canvas.DrawPicture(before);
                canvas.Translate(-distance, 0);
                canvas.DrawPicture(before);
                canvas.RestoreToCount(slide);
            }
            else
            {
                canvas.DrawPicture(before);
            }
        }
        var children = node.Children;
        if (children.Length > 0)
        {
            var childSave = canvas.Save();
            if (!props.ChildrenClipRect.IsEmpty)
                canvas.ClipRect(props.ChildrenClipRect);
            if (props.ChildrenClipPath is { } path)
                canvas.ClipPath(path, antialias: true);
            var pinned = false;
            props.TransformChildren(canvas);
            foreach (var child in children)
            {
                if (child.Props.Pinned)
                    pinned = true;
                else
                    DrawNode(canvas, child, isRoot: false);
            }
            canvas.RestoreToCount(childSave);
            // Pinned children: in local coordinates, outside the children clip.
            if (pinned)
                foreach (var child in children)
                    if (child.Props.Pinned)
                        DrawNode(canvas, child, isRoot: false);
        }
        if (node.After is { } after)
            canvas.DrawPicture(after);
        if (save >= 0)
            canvas.RestoreToCount(save);
    }

    /// <summary>
    /// Disposes the clip paths and shadow objects of <paramref name="props"/> that no frame draws any more: a removed node's,
    /// a disposed compositor's, a batch's that is never applied. Its UI node was reset (it left its drawn parent, or its
    /// surface is gone) and forgot them (<see cref="ISkUiRenderable.ReleaseDrawingResources"/>), so nothing commits them
    /// again: a render node leaves the tree only that way.
    /// </summary>
    internal static void ReleaseEffects(in SkUiRenderProps props)
    {
        props.ClipPath?.Dispose();
        props.ChildrenClipPath?.Dispose();
        props.ContentClipPath?.Dispose();
        if (props.Shadow is { } shadow)
        {
            shadow.Outline?.Dispose();
            shadow.Style.Dispose();
        }
    }

    private static void DisposeSubtree(SkUiRenderNode node)
    {
        node.Disposed = true;
        node.Before?.Dispose();
        node.After?.Dispose();
        node.Before = node.After = null;
        ReleaseEffects(node.Props);
        DisposeShadowCache(node);
        foreach (var child in node.Children)
            if (ReferenceEquals(child.Parent, node))
            {
                child.Parent = null;
                DisposeSubtree(child);
            }
        node.Children = [];
    }

    /// <summary>Releases all retained pictures; later commits are discarded.</summary>
    public void Dispose()
    {
        lock (_pendingLock)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var batch in _pending)
                DiscardWithAnimations(batch);
            _pending.Clear();
            _hasPending = false;
        }
        lock (_renderLock)
        {
            foreach (var animation in _animations)
            {
                animation.Cancel();
                Finish(animation, completed: false, reports: null);
            }
            _animations.Clear();
            Volatile.Write(ref _activeAnimations, 0);
            if (_root is not null)
                DisposeSubtree(_root);
            _root = null;
            _layerPaint.Dispose();
            _shadowPaint.Dispose();
            _clearPaint.Dispose();
        }
    }
}
