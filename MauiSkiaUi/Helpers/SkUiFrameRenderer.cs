using System.Diagnostics;
using SkiaSharp;

namespace MauiSkiaUi;

internal sealed class SkUiFrameRenderer : IDisposable
{
    private readonly SkUiView _root;
    private readonly Action<Action> _dispatch;
    private readonly Action _invalidateSurface;
    private readonly Action _beforePaint;
    private readonly object _pictureLock = new();
    private SKPicture? _picture;
    private Size _pictureSize;
    private SKSizeI _pixelSize;
    private int _frameQueued;
    private int _repaintPending;
    private volatile bool _recording;
    private volatile bool _disposed;

    internal SkUiFrameRenderer(SkUiView root, Action<Action> dispatch, Action invalidateSurface, Action beforePaint)
    {
        EnsureStandalone(root);
        this._root = root;
        this._dispatch = dispatch;
        this._invalidateSurface = invalidateSurface;
        this._beforePaint = beforePaint;
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
        if (_disposed)
            return;
        if (_recording)
        {
            Interlocked.Exchange(ref _repaintPending, 1);
            return;
        }
        if (Interlocked.Exchange(ref _frameQueued, 1) == 0)
            _dispatch(RecordFrame);
    }

    private void RecordFrame()
    {
        Interlocked.Exchange(ref _frameQueued, 0);
        if (_disposed || _root.Width <= 0 || _root.Height <= 0)
            return;
        _recording = true;
        var recordWatch = Stopwatch.StartNew();
        try
        {
            _beforePaint();
            Interlocked.Exchange(ref _repaintPending, 0);
            if (_disposed)
                return;
            using var recorder = new SKPictureRecorder();
            var size = new Size(_root.Width, _root.Height);
            var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)size.Width, (float)size.Height));
            _root.Paint(canvas);
            var nextPicture = recorder.EndRecording();
            lock (_pictureLock)
            {
                if (_disposed)
                {
                    nextPicture.Dispose();
                    return;
                }
                _picture?.Dispose();
                _picture = nextPicture;
                _pictureSize = size;
            }
        }
        finally
        {
            recordWatch.Stop();
            _recording = false;
            if (!_disposed)
                _root.NoteDiagnosticRecordFrame(recordWatch.Elapsed.TotalMilliseconds);
        }
        if (_disposed)
            return;
        _invalidateSurface();
        if (Interlocked.Exchange(ref _repaintPending, 0) != 0)
            RequestFrame();
    }

    internal void Replay(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.Transparent);
        lock (_pictureLock)
        {
            if (_disposed)
                return;
            _pixelSize = info.Size;
            if (_picture is null || info.Width <= 0 || info.Height <= 0)
                return;
            var saveCount = canvas.Save();
            try
            {
                canvas.Scale((float)(info.Width / _pictureSize.Width), (float)(info.Height / _pictureSize.Height));
                canvas.DrawPicture(_picture);
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
        lock (_pictureLock)
            pixels = _pixelSize;
        if (_disposed || pixels.Width <= 0 || pixels.Height <= 0)
            return false;
        var position = new Point(
            touch.Position.X * _root.Width / pixels.Width + _root.Frame.X,
            touch.Position.Y * _root.Height / pixels.Height + _root.Frame.Y);
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
        lock (_pictureLock)
        {
            _picture?.Dispose();
            _picture = null;
            _pixelSize = default;
        }
    }
}