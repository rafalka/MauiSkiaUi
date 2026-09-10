using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace MauiSkiaUi;

internal sealed class SkUiFrameRenderer : IDisposable
{
    private readonly SkUiView root;
    private readonly Action<Action> dispatch;
    private readonly Action invalidateSurface;
    private readonly Action beforePaint;
    private readonly object pictureLock = new();
    private SKPicture? picture;
    private Size pictureSize;
    private SKSizeI pixelSize;
    private int frameQueued;
    private int repaintPending;
    private volatile bool recording;
    private volatile bool disposed;

    internal SkUiFrameRenderer(SkUiView root, Action<Action> dispatch, Action invalidateSurface, Action beforePaint)
    {
        EnsureStandalone(root);
        this.root = root;
        this.dispatch = dispatch;
        this.invalidateSurface = invalidateSurface;
        this.beforePaint = beforePaint;
        root.PaintInvalidated += OnInvalidated;
    }

    internal static void EnsureStandalone(SkUiView root)
    {
        if (root.SkiaParent is not null)
            throw new InvalidOperationException("Hosted SkiaUi nodes must not create platform handlers.");
    }

    private void OnInvalidated(object? sender, EventArgs args) => RequestFrame();

    internal void RequestFrame()
    {
        if (disposed)
            return;
        if (recording)
        {
            Interlocked.Exchange(ref repaintPending, 1);
            return;
        }
        if (Interlocked.Exchange(ref frameQueued, 1) == 0)
            dispatch(RecordFrame);
    }

    private void RecordFrame()
    {
        Interlocked.Exchange(ref frameQueued, 0);
        if (disposed || root.Width <= 0 || root.Height <= 0)
            return;
        recording = true;
        try
        {
            beforePaint();
            Interlocked.Exchange(ref repaintPending, 0);
            if (disposed)
                return;
            using var recorder = new SKPictureRecorder();
            var size = new Size(root.Width, root.Height);
            var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)size.Width, (float)size.Height));
            root.Paint(canvas);
            var nextPicture = recorder.EndRecording();
            lock (pictureLock)
            {
                if (disposed)
                {
                    nextPicture.Dispose();
                    return;
                }
                picture?.Dispose();
                picture = nextPicture;
                pictureSize = size;
            }
        }
        finally
        {
            recording = false;
        }
        if (disposed)
            return;
        invalidateSurface();
        if (Interlocked.Exchange(ref repaintPending, 0) != 0)
            RequestFrame();
    }

    internal void Replay(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.Transparent);
        lock (pictureLock)
        {
            if (disposed)
                return;
            pixelSize = info.Size;
            if (picture is null || info.Width <= 0 || info.Height <= 0)
                return;
            var saveCount = canvas.Save();
            try
            {
                canvas.Scale((float)(info.Width / pictureSize.Width), (float)(info.Height / pictureSize.Height));
                canvas.DrawPicture(picture);
            }
            finally
            {
                canvas.RestoreToCount(saveCount);
            }
        }
    }

    internal bool TouchPixels(SkUiTouchEvent touch)
    {
        SKSizeI pixels;
        lock (pictureLock)
            pixels = pixelSize;
        if (disposed || pixels.Width <= 0 || pixels.Height <= 0)
            return false;
        var position = new Point(
            touch.Position.X * root.Width / pixels.Width + root.Frame.X,
            touch.Position.Y * root.Height / pixels.Height + root.Frame.Y);
        return SkUiView.MapPoint(root, position, out var local)
            && root.Touch(touch with { Position = local });
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        root.PaintInvalidated -= OnInvalidated;
        root.AnimationClock.StopAll();
        lock (pictureLock)
        {
            picture?.Dispose();
            picture = null;
            pixelSize = default;
        }
    }
}