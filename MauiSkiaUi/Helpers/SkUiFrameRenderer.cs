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
/// ghost strokes. <see cref="UseOpaquePresentBlit"/> composes into a retained offscreen surface
/// (GPU when a <see cref="GRContext"/> is available, otherwise CPU) then Src-blits the complete
/// frame onto the platform canvas — same present contract as Uno's retained layer / DrawnUI's
/// Metal texture copy.
/// </remarks>
internal sealed class SkUiFrameRenderer : IDisposable
{
    private readonly SkUiView _root;
    private readonly Action<Action> _dispatch;
    private readonly Action _invalidateSurface;
    private readonly Action _beforePaint;
    private readonly object _paintLock = new();
    private readonly SKPaint _srcBlitPaint = new() { BlendMode = SKBlendMode.Src, IsAntialias = false };
    private SKSurface? _gpuPresentSurface;
    private GRContext? _gpuPresentContext;
    private SKBitmap? _cpuPresentBuffer;
    private SKCanvas? _cpuPresentCanvas;
    private SKSizeI _presentPixelSize;
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
    /// When true, compose the frame offscreen and Src-blit it onto the platform canvas.
    /// Required on iOS <c>SKGLView</c> to avoid retained prior-frame strokes after shrink.
    /// Prefers a GPU-budgeted surface when <see cref="Replay"/> receives a <see cref="GRContext"/>.
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
    /// <param name="canvas">Platform drawable canvas (swapchain / SKGLView surface).</param>
    /// <param name="info">Pixel size and color type of <paramref name="canvas"/>.</param>
    /// <param name="grContext">
    /// Optional GPU context from the hosting <c>SKGLView</c>. When <see cref="UseOpaquePresentBlit"/>
    /// is set, a non-null context enables GPU-retained compose; otherwise a CPU bitmap is used.
    /// </param>
    internal void Replay(SKCanvas canvas, SKImageInfo info, GRContext? grContext = null)
    {
        var clear = _disposed ? SKColors.White : _root.SurfaceClearColor;
        var skipContent = _disposed
            || _root.Width <= 0 || _root.Height <= 0 || info.Width <= 0 || info.Height <= 0;

        if (UseOpaquePresentBlit && info.Width > 0 && info.Height > 0)
        {
            ReplayViaOpaqueBlit(canvas, info, clear, skipContent, grContext);
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

    private void ReplayViaOpaqueBlit(SKCanvas canvas, SKImageInfo info, SKColor clear, bool skipContent, GRContext? grContext)
    {
        var back = AcquirePresentCanvas(info, grContext);
        back.Clear(clear);
        if (!skipContent)
            PaintTreeOnto(back, info);

        ResetPlatformCanvas(canvas);
        if (_gpuPresentSurface is { } gpu)
        {
            gpu.Canvas.Flush();
            gpu.Draw(canvas, 0, 0, _srcBlitPaint);
        }
        else
        {
            canvas.DrawBitmap(
                _cpuPresentBuffer!,
                SKRect.Create(info.Width, info.Height),
                new SKSamplingOptions(SKFilterMode.Nearest),
                _srcBlitPaint);
        }

        lock (_paintLock)
        {
            _pixelSize = info.Size;
            if (!skipContent)
                _dipSize = new Size(_root.Width, _root.Height);
        }

        if (_gate == 0 && !_disposed && Interlocked.Exchange(ref _repaintPending, 0) != 0)
            RequestFrame();
    }

    private SKCanvas AcquirePresentCanvas(SKImageInfo info, GRContext? grContext)
    {
        if (grContext is not null && TryEnsureGpuPresentSurface(grContext, info) is { } gpuCanvas)
            return gpuCanvas;
        EnsureCpuPresentBuffer(info);
        return _cpuPresentCanvas!;
    }

    private SKCanvas? TryEnsureGpuPresentSurface(GRContext context, SKImageInfo info)
    {
        if (_gpuPresentSurface is not null
            && ReferenceEquals(_gpuPresentContext, context)
            && _presentPixelSize.Width == info.Width
            && _presentPixelSize.Height == info.Height)
            return _gpuPresentSurface.Canvas;

        DisposeGpuPresentSurface();
        DisposeCpuPresentBuffer();

        var colorType = info.ColorType == SKColorType.Unknown ? SKColorType.Rgba8888 : info.ColorType;
        var alphaType = info.AlphaType == SKAlphaType.Unknown ? SKAlphaType.Premul : info.AlphaType;
        var surfaceInfo = new SKImageInfo(info.Width, info.Height, colorType, alphaType);
        var surface = SKSurface.Create(context, budgeted: true, surfaceInfo);
        if (surface is null && colorType != SKColorType.Rgba8888)
            surface = SKSurface.Create(context, budgeted: true,
                new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (surface is null)
            return null;

        _gpuPresentSurface = surface;
        _gpuPresentContext = context;
        _presentPixelSize = info.Size;
        return surface.Canvas;
    }

    private void EnsureCpuPresentBuffer(SKImageInfo info)
    {
        if (_cpuPresentBuffer is { } existing
            && existing.Width == info.Width
            && existing.Height == info.Height)
            return;

        DisposeGpuPresentSurface();
        DisposeCpuPresentBuffer();
        _cpuPresentBuffer = new SKBitmap(info.Width, info.Height, info.ColorType, info.AlphaType);
        if (_cpuPresentBuffer.ColorType == SKColorType.Unknown)
        {
            _cpuPresentBuffer.Dispose();
            _cpuPresentBuffer = new SKBitmap(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        }
        _cpuPresentCanvas = new SKCanvas(_cpuPresentBuffer);
        _presentPixelSize = info.Size;
    }

    private void PaintTreeOnto(SKCanvas canvas, SKImageInfo info)
    {
        _gate++;
#if SKUI_DIAGNOSTICS
        var paintWatch = Stopwatch.StartNew();
#endif
        try
        {
            var save = canvas.Save();
            try
            {
                canvas.Scale(info.Width / (float)_root.Width, info.Height / (float)_root.Height);
                _root.Paint(canvas);
            }
            finally
            {
                canvas.RestoreToCount(save);
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

    private void PaintTree(SKCanvas canvas, SKImageInfo info)
    {
        PaintTreeOnto(canvas, info);
        lock (_paintLock)
        {
            _pixelSize = info.Size;
            _dipSize = new Size(_root.Width, _root.Height);
        }

        if (_gate == 0 && !_disposed && Interlocked.Exchange(ref _repaintPending, 0) != 0)
            RequestFrame();
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

    private void DisposeGpuPresentSurface()
    {
        _gpuPresentSurface?.Dispose();
        _gpuPresentSurface = null;
        _gpuPresentContext = null;
    }

    private void DisposeCpuPresentBuffer()
    {
        _cpuPresentCanvas?.Dispose();
        _cpuPresentCanvas = null;
        _cpuPresentBuffer?.Dispose();
        _cpuPresentBuffer = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _root.PaintInvalidated -= OnInvalidated;
        _root.AnimationClock.StopAll();
        DisposeGpuPresentSurface();
        DisposeCpuPresentBuffer();
        _srcBlitPaint.Dispose();
        lock (_paintLock)
        {
            _pixelSize = default;
            _dipSize = default;
        }
    }
}
