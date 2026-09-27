using System.Diagnostics;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Frame pipeline for one standalone root. On the UI thread it coalesces invalidation into one frame, records
/// only the dirty nodes (<see cref="SkUiRenderRecorder"/>) and commits the batch to the root's
/// <see cref="SkUiCompositor"/>. The platform surface then calls <see cref="Render"/> on its render thread
/// (Metal / GL thread; the UI thread only for software surfaces), which composites the retained tree and runs
/// render-thread animations without touching MAUI objects.
/// </summary>
internal sealed class SkUiFrameRenderer : IDisposable
{
    private readonly SkUiView _root;
    private readonly Action<Action> _dispatch;
    private readonly Action _requestRender;
    private readonly Action _beforeFrame;
    private readonly SkUiRenderRecorder _recorder = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly object _sizeLock = new();
    private SKSizeI _pixelSize;
    private Size _dipSize;
    private int _frameQueued;
    private int _repaintPending;
    private int _gate;
    private bool _committedOnce;
    private long _frameStarted;
    private volatile bool _disposed;

    /// <param name="root">Standalone root (no drawn parent).</param>
    /// <param name="dispatch">Posts work to the UI thread.</param>
    /// <param name="requestRender">Asks the surface for a render pass (any thread).</param>
    /// <param name="beforeFrame">UI-thread hook run before recording (e.g. UI-clock animation tick).</param>
    internal SkUiFrameRenderer(SkUiView root, Action<Action> dispatch, Action requestRender, Action beforeFrame)
    {
        EnsureStandalone(root);
        _root = root;
        _dispatch = dispatch;
        _requestRender = requestRender;
        _beforeFrame = beforeFrame;
        Compositor = new SkUiCompositor(dispatch);
        root.RenderRootDirty += OnRootDirty;
    }

    /// <summary>Retained compositor fed by this renderer.</summary>
    internal SkUiCompositor Compositor { get; }

    /// <summary>Pictures recorded so far (diagnostics / tests).</summary>
    internal int RecordedPictures => _recorder.RecordedPictures;

    /// <summary>Monotonic time used for render-thread animations when no explicit time is given.</summary>
    internal TimeSpan Now => _clock.Elapsed;

    internal static void EnsureStandalone(SkUiView root)
    {
        if (root.SkiaParent is not null)
            throw new InvalidOperationException("Hosted SkiaUi nodes must not create platform handlers.");
    }

    private void OnRootDirty(object? sender, EventArgs args) => RequestFrame();

    /// <summary>Schedules one UI-thread record/commit pass (coalesced).</summary>
    internal void RequestFrame()
    {
        if (_disposed)
            return;
        if (_gate > 0)
        {
            Interlocked.Exchange(ref _repaintPending, 1);
            return;
        }
        if (Interlocked.Exchange(ref _frameQueued, 1) == 0)
            _dispatch(PresentFrame);
    }

    /// <summary>UI thread: records pending changes and commits them. Also callable directly (tests, first frame).</summary>
    internal void PresentFrame()
    {
        Interlocked.Exchange(ref _frameQueued, 0);
        if (_disposed)
            return;

        _gate++;
        try
        {
            _beforeFrame();
            Interlocked.Exchange(ref _repaintPending, 0);
            if (_disposed || _root.Width <= 0 || _root.Height <= 0)
                return;
#if SKUI_DIAGNOSTICS
            var recordWatch = Stopwatch.StartNew();
#endif
            var batch = _recorder.Sync(_root, (float)_root.Width, (float)_root.Height, _root.SurfaceClearColor, forceRoot: !_committedOnce);
#if SKUI_DIAGNOSTICS
            // UI-thread cost of recording this frame (stress harness metric).
            if (batch is not null)
                _root.NoteDiagnosticRecordFrame(recordWatch.Elapsed.TotalMilliseconds);
#endif
            if (batch is not null)
            {
                _committedOnce = true;
                Compositor.Commit(batch);
                _requestRender();
            }
        }
        finally
        {
            _gate--;
        }

        if (!_disposed && Interlocked.Exchange(ref _repaintPending, 0) != 0)
            RequestFrame();
    }

    /// <summary>
    /// Render thread: composites the latest committed frame into <paramref name="canvas"/>. Returns <c>true</c>
    /// when the surface must render again (render-thread animations or content spin are running).
    /// </summary>
    internal bool Render(SKCanvas canvas, SKImageInfo info, TimeSpan? now = null)
    {
        if (_disposed)
        {
            canvas.Clear(SKColors.Transparent);
            return false;
        }
        _frameStarted = Stopwatch.GetTimestamp();
        var needsFrame = Compositor.Render(canvas, info.Width, info.Height, now ?? _clock.Elapsed);
        lock (_sizeLock)
        {
            _pixelSize = info.Size;
            _dipSize = Compositor.RootSize;
        }
        return needsFrame;
    }

    /// <summary>
    /// Render thread: call after the frame started by <see cref="Render"/> was flushed / submitted / presented;
    /// records its full duration in the compositor statistics.
    /// </summary>
    internal void CompleteFrame()
    {
        if (_frameStarted != 0 && Compositor.HasContent)
            Compositor.RecordFrameStatistics(Stopwatch.GetTimestamp() - _frameStarted);
        _frameStarted = 0;
    }

    /// <summary>Test / compatibility alias for <see cref="Render"/>.</summary>
    internal void Replay(SKCanvas canvas, SKImageInfo info) => Render(canvas, info);

    /// <summary>UI thread: maps a surface-pixel touch into root DIPs and dispatches it into the tree.</summary>
    internal bool TouchPixels(SkUiTouchEvent touch)
    {
        SKSizeI pixels;
        Size dips;
        lock (_sizeLock)
        {
            pixels = _pixelSize;
            dips = _dipSize;
        }
        if (_disposed || pixels.Width <= 0 || pixels.Height <= 0 || dips.Width <= 0 || dips.Height <= 0)
            return false;
        return TouchDips(touch with
        {
            Position = new Point(touch.Position.X * dips.Width / pixels.Width, touch.Position.Y * dips.Height / pixels.Height)
        });
    }

    /// <summary>UI thread: dispatches a touch already in root-surface DIPs.</summary>
    internal bool TouchDips(SkUiTouchEvent touch)
    {
        if (_disposed)
            return false;
        var position = new Point(touch.Position.X + _root.Frame.X, touch.Position.Y + _root.Frame.Y);
        return SkUiView.MapPoint(_root, position, out var local)
            && _root.Touch(touch with { Position = local });
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _root.RenderRootDirty -= OnRootDirty;
        _root.AnimationClock.StopAll();
        Compositor.Dispose();
        _recorder.Dispose();
        // Retained pictures are gone; a future surface must record the whole tree again.
        SkUiRenderInvalidation.ResetSubtree(_root);
        lock (_sizeLock)
        {
            _pixelSize = default;
            _dipSize = default;
        }
    }
}
