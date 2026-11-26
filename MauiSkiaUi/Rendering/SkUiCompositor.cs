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

    /// <summary>
    /// The scroll corrections applied to this node's offset so far (anchoring: rows inserted above move the offset, not the
    /// list), so a scroll-direction reveal (<see cref="SkUiRenderReveal"/>) can tell them from scrolling.
    /// </summary>
    internal SKPoint ScrollCorrection;
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
    public SkUiRenderNode Node = node;
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

    /// <summary>
    /// A scroll correction the UI made while no render-thread motion ran: the offset in <see cref="Props"/> already includes
    /// it; it is only added up into <see cref="SkUiRenderNode.ScrollCorrection"/> (scroll-direction reveals leave it out).
    /// </summary>
    public SKPoint ScrollCorrection;

    /// <summary>
    /// Forgets everything for reuse (<see cref="SkUiRenderBatch.NewUpdate"/>). Disposes nothing: once applied, what the
    /// update carried belongs to the compositor's tree.
    /// </summary>
    public void Clear()
    {
        Node = null!;
        HasProps = false;
        Props = default;
        ExplicitAnimatable = 0;
        HasContent = false;
        Before = null;
        After = null;
        Children = null;
        ScrollShift = default;
        ScrollCorrection = default;
    }
}

/// <summary>Everything the UI thread produced for one frame; applied atomically by the render thread.</summary>
internal sealed class SkUiRenderBatch
{
    /// <summary>Cleared updates kept for reuse, at most this many (a frame that changed a large tree does not keep them all).</summary>
    internal const int MaxSpareUpdates = 256;

    private readonly List<SkUiRenderUpdate> _spare = [];

    public readonly List<SkUiRenderUpdate> Updates = [];
    public readonly List<SkUiRenderAnimation> Animations = [];
    public SkUiRenderNode? Root;
    public float RootWidth;
    public float RootHeight;
    public SKColor ClearColor;

    /// <summary>The root's <see cref="SkUiView.SurfaceKeepsBottom"/>.</summary>
    public bool KeepsBottom;

    /// <summary>An update for <paramref name="node"/>: a cleared one this batch kept from an earlier frame, else a new one.</summary>
    public SkUiRenderUpdate NewUpdate(SkUiRenderNode node)
    {
        if (_spare.Count == 0)
            return new SkUiRenderUpdate(node);
        var update = _spare[^1];
        _spare.RemoveAt(_spare.Count - 1);
        update.Node = node;
        return update;
    }

    /// <summary>
    /// Forgets the frame for reuse once it was applied (or recorded nothing): every reference is cleared, nothing is
    /// disposed, so a pooled batch keeps no picture, node or effect alive. The updates are kept as spares.
    /// </summary>
    internal void Recycle()
    {
        foreach (var update in Updates)
        {
            update.Clear();
            if (_spare.Count < MaxSpareUpdates)
                _spare.Add(update);
        }
        Updates.Clear();
        Animations.Clear();
        Root = null;
        RootWidth = 0;
        RootHeight = 0;
        ClearColor = default;
        KeepsBottom = false;
    }

    /// <summary>
    /// Releases pictures and effects of a batch that will never be applied (the compositor was disposed; the UI side then
    /// forgets its effects, <see cref="ISkUiRenderable.ReleaseDrawingResources"/>). Called only while no frame draws: the
    /// effects may be the ones a node has committed.
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
    private readonly Lock _pendingLock = new();
    private readonly Lock _renderLock = new();
    private readonly Action<Action> _postToUi;
    private readonly SKPaint _layerPaint = new();
    private readonly SKPaint _shadowPaint = new();

    /// <summary>Reveals whose timed move ended while drawing this frame: reported to the UI side with the frame's reports.</summary>
    private readonly List<SkUiRenderReveal> _endedReveals = [];

    /// <summary>Reused for rounded scroll-linked clips and the shadows that follow them (no native object per frame).</summary>
    private readonly SKRoundRect _clipRoundRect = new();
    private readonly SKPaint _drawablePaint = new();
    private readonly List<SkUiRenderNode> _removed = [];
    private readonly List<SkUiRenderAnimation> _animations = [];
    private List<SkUiRenderBatch> _pending = [];
    private List<SkUiRenderBatch> _applying = [];
    // Applied batches for the recorder to fill again: a Lock and a fixed array (a lock-free stack allocates a node per
    // push); each side enters it once per frame.
    private readonly Lock _poolLock = new();
    private readonly SkUiRenderBatch?[] _pool = new SkUiRenderBatch?[MaxPooledBatches];
    private int _pooled;
    // Delivered feedback batches for the render thread to fill again, under the same lock.
    private readonly SkUiRenderReports?[] _reportsPool = new SkUiRenderReports?[MaxPooledBatches];
    private int _pooledReports;
    private SkUiRenderNode? _root;
    private float _rootWidth;
    private float _rootHeight;
    private SKColor _clearColor = SKColors.Transparent;
    private bool _keepsBottom;
    // The last frame drawn at the root's own scale (SurfaceMatrix): its sizes and scale, and the frames held at that scale since.
    private float _drawnRootWidth;
    private float _drawnRootHeight;
    private int _drawnPixelWidth;
    private int _drawnPixelHeight;
    private float _drawnScaleX;
    private float _drawnScaleY;
    private int _heldFrames;
    private TimeSpan _now;
    private bool _spinning;
    private bool _rasterizePending;
    private int _rasterizedThisFrame;

    /// <summary>
    /// Content shadows rasterized per frame at most; the others are drawn live and rasterized in the next frames, so
    /// many shadows appearing at once (a list scrolled into view) spread their CPU cost instead of stalling one frame.
    /// </summary>
    internal const int MaxShadowRasterizationsPerFrame = 3;

    /// <summary>
    /// Frames a surface resized ahead of its root's layout keeps drawing the root at the scale it was drawn at
    /// (<see cref="SurfaceMatrix"/>); after that the root fills it.
    /// </summary>
    internal const int MaxHeldFrames = 8;

    /// <summary>
    /// Whether the platform shows a frame scaled to the view's current size (Android's texture view, whose buffer can lag behind
    /// the view): a root laid out ahead of its pixels then fills them (<see cref="SurfaceMatrix"/>). Set before the surface renders.
    /// </summary>
    internal bool ScalesFrameToView { get; set; }

    /// <summary>How much a surface's pixel width and height may scale differently and still count as a new pixel density.</summary>
    private const float DensityTolerance = 0.001f;

    /// <summary>How far a side of the surface may be from the root's at the other side's scale (pixel rounding) and still match it.</summary>
    private const float PixelTolerance = 1;
    private float _scale = 1;
    private SKMatrix _surfaceMatrix = SKMatrix.Identity;
    private SKMatrix _space = SKMatrix.Identity;
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

    /// <summary>
    /// Render thread: the root's transform into the surface's pixels the last frame was drawn with (<see cref="SurfaceMatrix"/>):
    /// it fills the surface, or keeps its scale while its layout and the surface's size disagree. Touches are mapped back through it.
    /// </summary>
    internal SKMatrix PresentedSurfaceMatrix => _surfaceMatrix;

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

    /// <summary>Text shadows drawn from their glyphs (<see cref="SkUiRenderShadow.Glyphs"/>) since creation (diagnostics / tests).</summary>
    internal int GlyphShadows { get; private set; }

    /// <summary>
    /// Whether text shadows are drawn from their glyphs on a raster canvas too (tests). By default only GPU canvases draw them:
    /// a raster canvas would blur the glyphs on the CPU every frame, where a cached raster costs one blur per change.
    /// </summary>
    internal bool GlyphShadowsOnRaster { get; set; }

    /// <summary>This frame draws text shadows from their glyphs (a GPU canvas, or <see cref="GlyphShadowsOnRaster"/>).</summary>
    private bool _glyphShadows;

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

    /// <summary>
    /// Batches kept for reuse: one per frame in flight, and a few for frames committed before a render. Feedback batches
    /// (<see cref="SkUiRenderReports"/>) too: a few for frames rendered before the UI thread takes their feedback.
    /// </summary>
    internal const int MaxPooledBatches = 4;

    /// <summary>UI thread: an empty batch to record a frame into, from the pool when one was applied (A3).</summary>
    internal SkUiRenderBatch RentBatch()
    {
        lock (_poolLock)
        {
            if (_pooled == 0)
                return new SkUiRenderBatch();
            var batch = _pool[--_pooled]!;
            _pool[_pooled] = null;
            return batch;
        }
    }

    /// <summary>
    /// A batch applied (render thread) or one that recorded nothing (UI thread) goes back to the pool, cleared. A batch
    /// discarded unapplied is not returned: its contents were released, not handed over.
    /// </summary>
    internal void ReturnBatch(SkUiRenderBatch batch)
    {
        batch.Recycle();
        lock (_poolLock)
        {
            if (_pooled < MaxPooledBatches)
                _pool[_pooled++] = batch;
        }
    }

    /// <summary>Render thread: an empty batch of UI feedback, from the pool when one was delivered.</summary>
    private SkUiRenderReports RentReports()
    {
        lock (_poolLock)
        {
            if (_pooledReports == 0)
                return new SkUiRenderReports(this);
            var reports = _reportsPool[--_pooledReports]!;
            _reportsPool[_pooledReports] = null;
            return reports;
        }
    }

    /// <summary>UI thread: a delivered (empty) batch goes back to the pool.</summary>
    internal void ReturnReports(SkUiRenderReports reports)
    {
        lock (_poolLock)
        {
            if (_pooledReports < MaxPooledBatches)
                _reportsPool[_pooledReports++] = reports;
        }
    }

    /// <summary>UI thread: queues a recorded frame. Safe to call while the render thread renders.</summary>
    internal void Commit(SkUiRenderBatch batch)
    {
        lock (_pendingLock)
        {
            if (_disposed)
            {
                // No frame draws after Dispose (it took the render lock and dropped the tree), so nothing uses its effects.
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
            if (_rootWidth > 0 && _rootHeight > 0 && pixelWidth > 0 && pixelHeight > 0)
            {
                _surfaceMatrix = SurfaceMatrix(pixelWidth, pixelHeight);
                _scale = _surfaceMatrix.ScaleX;
            }
            else if (_rootWidth > 0)
            {
                _scale = pixelWidth / _rootWidth;
            }
            _space = _surfaceMatrix;

            // The whole canvas: it can be larger than the frame (a Windows swap chain with slack, SkUiSurfaceSlack).
            canvas.Clear(_clearColor);
            _spinning = false;
            _rasterizePending = false;
            _rasterizedThisFrame = 0;
            _glyphShadows = GlyphShadowsOnRaster || canvas.Context is not null;
            if (_root is { } root && _rootWidth > 0 && _rootHeight > 0 && pixelWidth > 0 && pixelHeight > 0)
            {
                var save = canvas.Save();
                canvas.Concat(in _surfaceMatrix);
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
            if (_endedReveals.Count > 0)
            {
                reports ??= RentReports();
                foreach (var reveal in _endedReveals)
                    reports.AddMoveEnded(reveal);
                _endedReveals.Clear();
            }
            if (reports is not null)
                _postToUi(reports.Deliver);
            return NeedsFrame;
        }
    }

    /// <summary>
    /// The root's transform into a surface of <paramref name="pixelWidth"/>×<paramref name="pixelHeight"/>: the root fills it. While
    /// the root's layout and the surface's size disagree (the platform view gets its size in its own layout pass, the UI thread
    /// commits the frame that lays out the new size on its next one, and either can come first: a popup that shrinks to its rows),
    /// the root keeps the scale it was last drawn at, against the surface's top or, for a root that keeps its bottom
    /// (<see cref="SkUiView.SurfaceKeepsBottom"/>), its bottom edge, instead of being stretched over the surface until the two agree
    /// again; for <see cref="MaxHeldFrames"/> at most, and not when the surface only changed its pixel density. On a surface that
    /// <see cref="ScalesFrameToView"/>, a root laid out ahead of its pixels fills them: the view already has the root's size.
    /// </summary>
    private SKMatrix SurfaceMatrix(int pixelWidth, int pixelHeight)
    {
        var scaleX = pixelWidth / _rootWidth;
        var scaleY = pixelHeight / _rootHeight;
        // Drawn at the root's own scale: the same both ways, but for the pixel rounding of either side.
        if (Math.Abs(pixelHeight - _rootHeight * scaleX) <= PixelTolerance || Math.Abs(pixelWidth - _rootWidth * scaleY) <= PixelTolerance)
        {
            _heldFrames = 0;
            _drawnRootWidth = _rootWidth;
            _drawnRootHeight = _rootHeight;
            _drawnPixelWidth = pixelWidth;
            _drawnPixelHeight = pixelHeight;
            _drawnScaleX = scaleX;
            _drawnScaleY = scaleY;
            return SKMatrix.CreateScale(scaleX, scaleY);
        }
        // Exact: whether the root changed since the last frame drawn at its own scale.
        var rootChanged = _rootWidth != _drawnRootWidth || _rootHeight != _drawnRootHeight;
        var hold = _drawnPixelWidth > 0 && _drawnPixelHeight > 0 && _heldFrames < MaxHeldFrames && (rootChanged
            ? !ScalesFrameToView
            : Math.Abs((float)pixelWidth / _drawnPixelWidth - (float)pixelHeight / _drawnPixelHeight) >= DensityTolerance);
        if (!hold)
            return SKMatrix.CreateScale(scaleX, scaleY);
        _heldFrames++;
        return SKMatrix.CreateScaleTranslation(_drawnScaleX, _drawnScaleY, 0, _keepsBottom ? pixelHeight - _rootHeight * _drawnScaleY : 0);
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
            ReturnBatch(batch);
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
            if (update.ScrollCorrection != SKPoint.Empty)
                node.ScrollCorrection += update.ScrollCorrection;
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
            _keepsBottom = batch.KeepsBottom;
        }

        foreach (var removed in _removed)
            if (removed.Parent is null && !ReferenceEquals(removed, _root))
                DisposeSubtree(removed);
        _removed.Clear();

        foreach (var animation in batch.Animations)
        {
            if (animation.Target is not { Disposed: false } target || animation.IsCancelled)
            {
                PostCancelled(animation);
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
        if (current.Drawable is { } drawable && !ReferenceEquals(drawable, incoming.Drawable))
            drawable.Dispose();
        if (current.Shadow is not { } shadow || ReferenceEquals(shadow, incoming.Shadow))
            return;
        if (shadow.Outline is { } outline && !ReferenceEquals(outline, incoming.Shadow?.Outline))
            outline.Dispose();
        if (shadow.Glyphs is { } glyphs && !ReferenceEquals(glyphs, incoming.Shadow?.Glyphs))
            glyphs.Dispose();
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
        || a.Overflow != b.Overflow || a.ChildrenLayer != b.ChildrenLayer || !ReferenceEquals(a.Drawable, b.Drawable)
        || a.DrawableSpace != b.DrawableSpace;

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
        node.ScrollCorrection += shift;
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

    private SkUiRenderReports? TickAnimations(TimeSpan now)
    {
        SkUiRenderReports? reports = null;
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
            if (animation.TakeReport(target.Props, out var report))
                (reports ??= RentReports()).AddReport(animation, report);
            if (done)
            {
                RemoveAnimationAt(index, animation, completed: true, ref reports);
                continue;
            }
            index++;
        }
        return reports;
    }

    private void RemoveAnimationAt(int index, SkUiRenderAnimation animation, bool completed, ref SkUiRenderReports? reports)
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
            if (!completed && !target.Disposed && animation.TakeReport(target.Props, out var report))
                (reports ??= RentReports()).AddReport(animation, report);
        }
        if (animation.Finished is not null)
            (reports ??= RentReports()).AddFinished(animation, completed);
    }

    /// <summary>Posts the end of an animation that no rendered frame reports: one dropped before it ran, or with the compositor.</summary>
    private void PostCancelled(SkUiRenderAnimation animation)
    {
        if (animation.Finished is not null)
            _postToUi(() => animation.NotifyFinished(completed: false));
    }

    /// <summary>Drops a batch that will never be applied: releases its pictures and finishes its animations as cancelled.</summary>
    private void DiscardWithAnimations(SkUiRenderBatch batch)
    {
        batch.Discard();
        foreach (var animation in batch.Animations)
        {
            animation.Cancel();
            PostCancelled(animation);
        }
        batch.Animations.Clear();
    }

    private void DrawNode(SKCanvas canvas, SkUiRenderNode node, bool isRoot)
    {
        ref readonly var props = ref node.Props;
        if (props.IsSkippedWhenDrawn)
            return;
        SKMatrix matrix;
        var opacity = props.DrawnOpacity;
        var clipped = false;
        var clip = default(SkUiRenderLinkClip);
        if (props.ItemEffect is { Source.Disposed: false } effect)
        {
            // A carousel item: placed (and faded) by its effect at the scroller's current offset.
            var placed = props;
            ref readonly var source = ref effect.Source.Props;
            opacity *= (float)Math.Clamp(effect.Apply(ref placed, source.ChildrenOffsetX, source.ChildrenOffsetY), 0, 1);
            if (opacity <= 0)
                return;
            matrix = placed.DrawnMatrix;
        }
        else if (props.Links is { } links)
        {
            // Scroll-linked: translation, scale, opacity and clip from the sources' current (possibly animated) offsets.
            // A reveal's timed move keeps the frames coming (as spinning content does).
            var values = SkUiRenderLinkValues.Evaluate(links, props.LinkValues, _now, _endedReveals, ref _spinning);
            opacity *= values.Opacity;
            if (opacity <= 0)
                return;
            clipped = SkUiRenderLinkClip.TryGet(links, values, props.Width, props.Height, out clip);
            if (clipped && clip.IsEmpty)
                return;
            matrix = props.GetDrawnMatrix(values);
        }
        else
        {
            matrix = props.DrawnMatrix;
        }
        var save = canvas.Save();
        if (isRoot)
            matrix = matrix.PostConcat(SKMatrix.CreateTranslation(-props.X, -props.Y));
        canvas.Concat(in matrix);
        if (canvas.QuickReject(props.InkBounds))
        {
            canvas.RestoreToCount(save);
            return;
        }
        if (opacity < 1)
        {
            _layerPaint.Color = SKColors.White.WithAlpha((byte)(255 * opacity));
            canvas.SaveLayer(props.LayerBounds, _layerPaint);
        }
        if (props.Shadow is { } shadow)
        {
            if (clipped)
                DrawClippedShadow(canvas, node, shadow, clip);
            else
                DrawShadow(canvas, node, shadow);
        }
        else if (node.ShadowCache is not null)
        {
            DisposeShadowCache(node);
        }
        if (clipped)
            clip.Apply(canvas, _clipRoundRect); // inside this node's save, around its body
        if (props.DrawableSpace)
        {
            // The drawables below position themselves in this node's coordinates, wherever it is drawn.
            var space = _space;
            _space = canvas.TotalMatrix;
            DrawBody(canvas, node, ownSave: false);
            _space = space;
        }
        else
        {
            DrawBody(canvas, node, ownSave: false); // inside this node's save
        }
        canvas.RestoreToCount(save);
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
        if (shadow.Glyphs is { } glyphs && _glyphShadows)
        {
            if (node.ShadowCache is not null)
                DisposeShadowCache(node);
            GlyphShadows++;
            SkUiShadowPainter.DrawGlyphs(canvas, shadow, glyphs);
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

    /// <summary>
    /// The shadow of a node with a scroll-linked clip, which follows the clip as it changes: an outline shadow is cast by the
    /// clip's rounded rectangle (within the outline's bounds), a content shadow by the clipped content, drawn live (a cached
    /// raster would be of another clip).
    /// </summary>
    private void DrawClippedShadow(SKCanvas canvas, SkUiRenderNode node, SkUiRenderShadow shadow, in SkUiRenderLinkClip clip)
    {
        if (shadow.Outline is { } outline)
        {
            if (node.ShadowCache is not null)
                DisposeShadowCache(node); // a content shadow's raster from before: not this shadow
            if (clip.TryGetOutlineShadow(outline.Bounds, shadow.OutlineCornerRadius, out var rect, out var radius, out var hidden))
            {
                _clipRoundRect.SetRect(rect, radius, radius);
                SkUiShadowPainter.DrawRoundRect(canvas, shadow, _clipRoundRect, _shadowPaint);
            }
            else if (!hidden)
            {
                DrawShadow(canvas, node, shadow); // the clip leaves the outline whole: its own shape
            }
            return;
        }
        if (clip.Rect.Contains(node.Props.VisualBounds))
        {
            DrawShadow(canvas, node, shadow); // the clip cuts nothing it draws: the cached shadow serves
            return;
        }
        if (node.ShadowCache is not null)
            DisposeShadowCache(node);
        LiveShadows++;
        SkUiShadowPainter.DrawFromContent(canvas, shadow, _shadowPaint, (this, node, clip), static (c, state) =>
        {
            var save = c.Save();
            state.clip.Apply(c, state.Item1._clipRoundRect);
            state.Item1.DrawBody(c, state.node, ownSave: true);
            c.RestoreToCount(save);
        });
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
        // The children (and the drawable over them) as one group: what the drawable blends acts on the children only.
        var layer = props.ChildrenLayer ? canvas.SaveLayer(props.Bounds, null) : -1;
        if (node.Children.Length > 0)
            DrawChildren(canvas, node);
        if (props.Drawable is { } drawable)
        {
            if (drawable.IsAnimated)
                _spinning = true; // the frames keep coming while it is drawn
            _drawablePaint.Reset();
            _drawablePaint.IsAntialias = true;
            drawable.Draw(canvas, new SkUiRenderDrawContext(props.Bounds, _now, in _surfaceMatrix, in _space, _drawablePaint));
        }
        if (layer >= 0)
            canvas.RestoreToCount(layer);
        if (node.After is { } after)
            canvas.DrawPicture(after);
        if (save >= 0)
            canvas.RestoreToCount(save);
    }

    /// <summary>The node's children, inside its children clip and transform; pinned ones above, outside the children clip.</summary>
    private void DrawChildren(SKCanvas canvas, SkUiRenderNode node)
    {
        ref readonly var props = ref node.Props;
        var children = node.Children;
        var childSave = canvas.Save();
        if (!props.ChildrenClipRect.IsEmpty)
            canvas.ClipRect(props.ChildrenClipRect);
        if (props.ChildrenClipPath is { } path)
            canvas.ClipPath(path, antialias: true);
        var pinned = false;
        props.TransformChildren(canvas);
        if (props.SortsChildrenByDepth && children.Length > 1)
            pinned = DrawChildrenByDepth(canvas, children);
        else
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

    /// <summary>The most children sorted on the stack; more are sorted in arrays from the pool.</summary>
    private const int StackSortLimit = 64;

    /// <summary>
    /// Draws the unpinned <paramref name="children"/> back to front by the depth their item effects give them at the current
    /// scroll offset (equal depths keep their order); returns whether any child is pinned.
    /// </summary>
    private bool DrawChildrenByDepth(SKCanvas canvas, SkUiRenderNode[] children)
    {
        var count = children.Length;
        float[]? rentedDepths = null;
        int[]? rentedOrder = null;
        var depths = count <= StackSortLimit ? stackalloc float[count] : (rentedDepths = System.Buffers.ArrayPool<float>.Shared.Rent(count)).AsSpan(0, count);
        var order = count <= StackSortLimit ? stackalloc int[count] : (rentedOrder = System.Buffers.ArrayPool<int>.Shared.Rent(count)).AsSpan(0, count);
        var pinned = false;
        for (var index = 0; index < count; index++)
        {
            ref readonly var props = ref children[index].Props;
            pinned |= props.Pinned;
            depths[index] = props.ItemEffect is { Source.Disposed: false } effect
                ? (float)effect.Evaluate(effect.Source.Props.ChildrenOffsetX, effect.Source.Props.ChildrenOffsetY).ZIndex
                : 0;
            // Insertion sort: a handful of items, nearly sorted from frame to frame.
            var position = index;
            while (position > 0 && depths[order[position - 1]] > depths[index])
            {
                order[position] = order[position - 1];
                position--;
            }
            order[position] = index;
        }
        foreach (var index in order)
            if (!children[index].Props.Pinned)
                DrawNode(canvas, children[index], isRoot: false);
        if (rentedDepths is not null)
            System.Buffers.ArrayPool<float>.Shared.Return(rentedDepths);
        if (rentedOrder is not null)
            System.Buffers.ArrayPool<int>.Shared.Return(rentedOrder);
        return pinned;
    }

    /// <summary>
    /// Disposes the clip paths and shadow objects of <paramref name="props"/> that no frame draws any more: a removed node's,
    /// a disposed compositor's, a batch's that is never applied. Its UI node was reset (it left its drawn parent, or its
    /// surface is gone) and forgot them (<see cref="ISkUiRenderable.ReleaseDrawingResources"/>), so nothing commits them
    /// again: a render node leaves the tree only that way.
    /// </summary>
    /// <remarks>
    /// The props of one node alias each other's effects (an unchanged clip path, a shadow's style reused by the next shadow),
    /// so releasing a node's committed props and its discarded pending ones may release an object twice: disposal is
    /// idempotent (SkiaSharp objects, <see cref="SkUiShadowStyle.Dispose"/>). Nodes never share effects.
    /// </remarks>
    internal static void ReleaseEffects(in SkUiRenderProps props)
    {
        props.ClipPath?.Dispose();
        props.ChildrenClipPath?.Dispose();
        props.ContentClipPath?.Dispose();
        props.Drawable?.Dispose();
        if (props.Shadow is { } shadow)
        {
            shadow.Outline?.Dispose();
            shadow.Glyphs?.Dispose();
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
        List<SkUiRenderBatch> discarded;
        lock (_pendingLock)
        {
            if (_disposed)
                return;
            _disposed = true;
            discarded = [.. _pending];
            _pending.Clear();
            _hasPending = false;
        }
        lock (_renderLock)
        {
            // Discarded only now: a props update of a pending batch carries the node's effects (its clip path, shadow) as
            // committed, and a frame still drawing them holds the render lock.
            foreach (var batch in discarded)
                DiscardWithAnimations(batch);
            foreach (var animation in _animations)
            {
                animation.Cancel();
                PostCancelled(animation);
            }
            _animations.Clear();
            Volatile.Write(ref _activeAnimations, 0);
            if (_root is not null)
                DisposeSubtree(_root);
            _root = null;
            _layerPaint.Dispose();
            _shadowPaint.Dispose();
            _clipRoundRect.Dispose();
            _drawablePaint.Dispose();
        }
    }
}
