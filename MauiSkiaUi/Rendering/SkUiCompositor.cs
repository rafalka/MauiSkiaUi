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
    internal int Epoch;
    internal bool Disposed;
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

    /// <summary>Releases pictures of a batch that will never be applied.</summary>
    public void Discard()
    {
        foreach (var update in Updates)
        {
            update.Before?.Dispose();
            update.After?.Dispose();
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
                batch.Discard();
                foreach (var animation in batch.Animations)
                    animation.Cancel();
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
    internal bool Render(SKCanvas canvas, int pixelWidth, int pixelHeight, TimeSpan now)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            return RenderCore(canvas, pixelWidth, pixelHeight, now);
        }
        finally
        {
            // Only frames that composited committed content count (not clears before the first commit).
            if (_root is not null)
                RecordFrameStatistics(System.Diagnostics.Stopwatch.GetTimestamp() - started);
        }
    }

    private void RecordFrameStatistics(long elapsed)
    {
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
            ApplyPending();
            _now = now;
            var reports = TickAnimations(now);

            _clearPaint.Color = _clearColor;
            canvas.DrawRect(SKRect.Create(pixelWidth, pixelHeight), _clearPaint);
            _spinning = false;
            if (_root is { } root && _rootWidth > 0 && _rootHeight > 0 && pixelWidth > 0 && pixelHeight > 0)
            {
                var save = canvas.Save();
                canvas.Scale(pixelWidth / _rootWidth, pixelHeight / _rootHeight);
                DrawNode(canvas, root, isRoot: true);
                canvas.RestoreToCount(save);
            }
            FrameCount++;
            _continuous = _spinning || _animations.Count > 0;
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

    private void ApplyPending()
    {
        lock (_pendingLock)
        {
            (_pending, _applying) = (_applying, _pending);
            _hasPending = false;
        }
        foreach (var batch in _applying)
            Apply(batch);
        _applying.Clear();
    }

    private void Apply(SkUiRenderBatch batch)
    {
        foreach (var update in batch.Updates)
        {
            var node = update.Node;
            if (node.Disposed)
            {
                update.Before?.Dispose();
                update.After?.Dispose();
                continue;
            }
            if (update.HasProps)
            {
                var incoming = update.Props;
                if (node.AnimatedMask != 0)
                {
                    var overridden = update.ExplicitAnimatable & node.AnimatedMask;
                    if (overridden != 0)
                        CancelAnimations(node, overridden);
                    // Properties still animating keep their render-thread value unless the UI set them.
                    var keep = node.AnimatedMask & ~overridden;
                    for (var property = 0; property < SkUiRenderPropertyCount.Value; property++)
                        if ((keep & (1 << property)) != 0)
                            incoming.Set((SkUiRenderProperty)property, node.Props.Get((SkUiRenderProperty)property));
                }
                node.Props = incoming;
            }
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

    private void CancelAnimations(SkUiRenderNode node, int mask)
    {
        foreach (var animation in _animations)
            if (ReferenceEquals(animation.Target, node) && (animation.PropertyMask & mask) != 0)
                animation.Cancel();
    }

    private List<Action>? TickAnimations(TimeSpan now)
    {
        List<Action>? reports = null;
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
        if (animation.Finished is not { } finished)
            return;
        if (reports is not null)
            reports.Add(() => finished(animation, completed));
        else
            _postToUi(() => finished(animation, completed));
    }

    private void DrawNode(SKCanvas canvas, SkUiRenderNode node, bool isRoot)
    {
        ref readonly var props = ref node.Props;
        if (props.IsSkipped)
            return;
        var save = canvas.Save();
        var matrix = props.Matrix;
        if (isRoot)
            matrix = matrix.PostConcat(SKMatrix.CreateTranslation(-props.X, -props.Y));
        canvas.Concat(in matrix);
        var visual = props.VisualBounds;
        if (canvas.QuickReject(visual))
        {
            canvas.RestoreToCount(save);
            return;
        }
        if (props.ClipToBounds)
            canvas.ClipRect(props.Bounds);
        if (props.Opacity < 1)
        {
            _layerPaint.Color = SKColors.White.WithAlpha((byte)(255 * props.Opacity));
            canvas.SaveLayer(props.ClipToBounds ? props.Bounds : visual, _layerPaint);
        }
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
            if (props.ChildrenOffsetX != 0 || props.ChildrenOffsetY != 0)
                canvas.Translate(-props.ChildrenOffsetX, -props.ChildrenOffsetY);
            foreach (var child in children)
                DrawNode(canvas, child, isRoot: false);
            canvas.RestoreToCount(childSave);
        }
        if (node.After is { } after)
            canvas.DrawPicture(after);
        canvas.RestoreToCount(save);
    }

    private static void DisposeSubtree(SkUiRenderNode node)
    {
        node.Disposed = true;
        node.Before?.Dispose();
        node.After?.Dispose();
        node.Before = node.After = null;
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
                batch.Discard();
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
            _clearPaint.Dispose();
        }
    }
}
