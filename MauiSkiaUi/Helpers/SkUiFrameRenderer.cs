#if SKUI_DIAGNOSTICS
using System.Diagnostics;
#endif
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Coalesces paint invalidation onto the UI thread and presents the root tree into the platform Skia surface.
/// </summary>
/// <remarks>
/// On iOS HW, painting the tree directly onto the <c>SKGLView</c> canvas can leave WidthRequest
/// ghost strokes. <see cref="UseOpaquePresentBlit"/> paints into a CPU back-buffer then Src-blits
/// the complete frame onto the platform canvas.
/// </remarks>
internal sealed class SkUiFrameRenderer : IDisposable
{
    private readonly SkUiView _root;
    private readonly Action<Action> _dispatch;
    private readonly Action _invalidateSurface;
    private readonly Action _beforePaint;
    private readonly object _paintLock = new();
    private SKBitmap? _presentBuffer;
    private SKCanvas? _presentCanvas;
    private SKSizeI _pixelSize;
    private Size _dipSize;
    private int _frameQueued;
    private int _repaintPending;
    private int _gate;
    private volatile bool _disposed;

    internal SkUiFrameRenderer(SkUiView root, Action<Action> dispatch, Action invalidateSurface, Action beforePaint)
    {
        EnsureStandalone(root);
        _root = root;
        _dispatch = dispatch;
        _invalidateSurface = invalidateSurface;
        _beforePaint = beforePaint;
        root.PaintInvalidated += OnInvalidated;
    }

    /// <summary>
    /// When true, compose the frame into a CPU bitmap and Src-blit it onto the platform canvas.
    /// Required on iOS <c>SKGLView</c> to avoid retained prior-frame strokes after shrink.
    /// </summary>
    internal bool UseOpaquePresentBlit { get; set; }

    internal static void EnsureStandalone(SkUiView root)
    {
        if (root.SkiaParent is not null)
            throw new InvalidOperationException("Hosted SkiaUi nodes must not create platform handlers.");
    }

    private void OnInvalidated(object? sender, EventArgs args) => RequestFrame();

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

    private void PresentFrame()
    {
        Interlocked.Exchange(ref _frameQueued, 0);
        if (_disposed)
            return;

        _gate++;
        try
        {
            _beforePaint();
            Interlocked.Exchange(ref _repaintPending, 0);
            if (_disposed)
                return;
            if (_root.Width <= 0 || _root.Height <= 0)
                return;
            _invalidateSurface();
        }
        finally
        {
            _gate--;
        }

        if (!_disposed && Interlocked.Exchange(ref _repaintPending, 0) != 0)
            RequestFrame();
    }

    /// <summary>
    /// Paints the current tree into the platform canvas. Called from the surface's PaintSurface handler.
    /// </summary>
    internal void Replay(SKCanvas canvas, SKImageInfo info)
    {
        var clear = _disposed ? SKColors.White : _root.SurfaceClearColor;
        var skipContent = _disposed
            || _root.Width <= 0 || _root.Height <= 0 || info.Width <= 0 || info.Height <= 0;

        if (UseOpaquePresentBlit && info.Width > 0 && info.Height > 0)
        {
            ReplayViaOpaqueBlit(canvas, info, clear, skipContent);
            return;
        }

        ResetPlatformCanvas(canvas);
        FillOpaque(canvas, info, clear);

        if (skipContent)
        {
            lock (_paintLock)
                _pixelSize = info.Size;
            return;
        }

        PaintTree(canvas, info);
    }

    private void ReplayViaOpaqueBlit(SKCanvas canvas, SKImageInfo info, SKColor clear, bool skipContent)
    {
        EnsurePresentBuffer(info);
        var buffer = _presentBuffer!;
        var back = _presentCanvas!;

        back.Clear(clear);
        if (!skipContent)
        {
            _gate++;
#if SKUI_DIAGNOSTICS
            var paintWatch = Stopwatch.StartNew();
#endif
            try
            {
                var save = back.Save();
                try
                {
                    back.Scale(info.Width / (float)_root.Width, info.Height / (float)_root.Height);
                    _root.Paint(back);
                }
                finally
                {
                    back.RestoreToCount(save);
                }
            }
            finally
            {
#if SKUI_DIAGNOSTICS
                paintWatch.Stop();
                if (!_disposed)
                    _root.NoteDiagnosticRecordFrame(paintWatch.Elapsed.TotalMilliseconds);
#endif
                _gate--;
            }
        }

        ResetPlatformCanvas(canvas);
        using (var blit = new SKPaint { BlendMode = SKBlendMode.Src, IsAntialias = false })
            canvas.DrawBitmap(
                buffer,
                SKRect.Create(info.Width, info.Height),
                new SKSamplingOptions(SKFilterMode.Nearest),
                blit);

        lock (_paintLock)
        {
            _pixelSize = info.Size;
            if (!skipContent)
                _dipSize = new Size(_root.Width, _root.Height);
        }

        if (_gate == 0 && !_disposed && Interlocked.Exchange(ref _repaintPending, 0) != 0)
            RequestFrame();
    }

    private void PaintTree(SKCanvas canvas, SKImageInfo info)
    {
        _gate++;
#if SKUI_DIAGNOSTICS
        var paintWatch = Stopwatch.StartNew();
#endif
        try
        {
            var saveCount = canvas.Save();
            try
            {
                canvas.Scale(info.Width / (float)_root.Width, info.Height / (float)_root.Height);
                _root.Paint(canvas);
            }
            finally
            {
                canvas.RestoreToCount(saveCount);
            }
            lock (_paintLock)
            {
                _pixelSize = info.Size;
                _dipSize = new Size(_root.Width, _root.Height);
            }
        }
        finally
        {
#if SKUI_DIAGNOSTICS
            paintWatch.Stop();
            if (!_disposed)
                _root.NoteDiagnosticRecordFrame(paintWatch.Elapsed.TotalMilliseconds);
#endif
            _gate--;
        }

        if (_gate == 0 && !_disposed && Interlocked.Exchange(ref _repaintPending, 0) != 0)
            RequestFrame();
    }

    private void EnsurePresentBuffer(SKImageInfo info)
    {
        if (_presentBuffer is { } existing
            && existing.Width == info.Width
            && existing.Height == info.Height)
            return;

        _presentCanvas?.Dispose();
        _presentBuffer?.Dispose();
        _presentBuffer = new SKBitmap(info.Width, info.Height, info.ColorType, info.AlphaType);
        if (_presentBuffer.ColorType == SKColorType.Unknown)
        {
            _presentBuffer.Dispose();
            _presentBuffer = new SKBitmap(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        }
        _presentCanvas = new SKCanvas(_presentBuffer);
    }

    private static void ResetPlatformCanvas(SKCanvas canvas)
    {
        while (canvas.SaveCount > 1)
            canvas.Restore();
        canvas.ResetMatrix();
    }

    private static void FillOpaque(SKCanvas canvas, SKImageInfo info, SKColor clear)
    {
        using var fill = new SKPaint { Color = clear, BlendMode = SKBlendMode.Src };
        canvas.DrawRect(SKRect.Create(info.Width, info.Height), fill);
    }

    internal bool TouchPixels(SkUiTouchEvent touch)
    {
        SKSizeI pixels;
        Size dips;
        lock (_paintLock)
        {
            pixels = _pixelSize;
            dips = _dipSize;
        }
        if (_disposed || pixels.Width <= 0 || pixels.Height <= 0 || dips.Width <= 0 || dips.Height <= 0)
            return false;
        var position = new Point(
            touch.Position.X * dips.Width / pixels.Width + _root.Frame.X,
            touch.Position.Y * dips.Height / pixels.Height + _root.Frame.Y);
        return SkUiView.MapPoint(_root, position, out var local)
            && _root.Touch(touch with { Position = local });
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _root.PaintInvalidated -= OnInvalidated;
        _root.AnimationClock.StopAll();
        _presentCanvas?.Dispose();
        _presentCanvas = null;
        _presentBuffer?.Dispose();
        _presentBuffer = null;
        lock (_paintLock)
        {
            _pixelSize = default;
            _dipSize = default;
        }
    }
}
