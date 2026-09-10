#if ANDROID || IOS || MACCATALYST || WINDOWS
using System.Diagnostics;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
#if ANDROID
using PlatformView = Android.Views.View;
#elif IOS || MACCATALYST
using PlatformView = UIKit.UIView;
#elif WINDOWS
using PlatformView = Microsoft.UI.Xaml.FrameworkElement;
#endif

namespace MauiSkiaUi;

/// <summary>Owns one Skia surface and marshals tree updates onto the UI thread.</summary>
public sealed class SkUiViewHandler : ViewHandler<SkUiView, PlatformView>
{
    private readonly object pictureLock = new();
    private readonly Stopwatch animationTime = new();
    private View? surface;
    private SKPicture? picture;
    private Size pictureSize;
    private TimeSpan clockOffset;
    private int frameQueued;
    private bool connected;
    private bool recording;
    private float pixelWidth;
    private float pixelHeight;

    private static readonly IPropertyMapper<SkUiView, SkUiViewHandler> SkiaMapper = CreateMapper();

    /// <summary>Creates a handler using normal MAUI sizing and Skia-owned drawing properties.</summary>
    public SkUiViewHandler() : base(SkiaMapper) { }

    private static IPropertyMapper<SkUiView, SkUiViewHandler> CreateMapper()
    {
        var mapper = new PropertyMapper<SkUiView, SkUiViewHandler>(ViewMapper);
        foreach (var property in new[]
        {
            nameof(IView.Background), nameof(IView.Opacity), nameof(IView.TranslationX), nameof(IView.TranslationY),
            nameof(IView.Rotation), nameof(IView.Scale), nameof(IView.ScaleX), nameof(IView.ScaleY),
            nameof(IView.AnchorX), nameof(IView.AnchorY)
        })
            mapper[property] = (handler, _) => handler.QueueFrame();
        return mapper;
    }

    /// <inheritdoc />
    protected override PlatformView CreatePlatformView()
    {
        if (VirtualView.SkiaParent is not null)
            throw new InvalidOperationException("Hosted SkiaUi nodes must not create platform handlers.");
        if (VirtualView.HwAccelerated)
        {
            var gpu = new SKGLView { EnableTouchEvents = true, IgnorePixelScaling = false };
            gpu.PaintSurface += OnGpuPaint;
            gpu.Touch += OnTouch;
            surface = gpu;
        }
        else
        {
            var software = new SKCanvasView { EnableTouchEvents = true, IgnorePixelScaling = false };
            software.PaintSurface += OnSoftwarePaint;
            software.Touch += OnTouch;
            surface = software;
        }
        surface.Parent = VirtualView;
        return surface.ToPlatform(MauiContext!);
    }

    /// <inheritdoc />
    protected override void ConnectHandler(PlatformView platformView)
    {
        base.ConnectHandler(platformView);
        connected = true;
        VirtualView.PaintInvalidated += OnInvalidated;
        VirtualView.AnimationClock.RunningChanged += OnRunningChanged;
        VirtualView.Loaded += OnLoaded;
        VirtualView.Unloaded += OnUnloaded;
        OnRunningChanged(this, EventArgs.Empty);
        QueueFrame();
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(PlatformView platformView)
    {
        connected = false;
        VirtualView.PaintInvalidated -= OnInvalidated;
        VirtualView.AnimationClock.RunningChanged -= OnRunningChanged;
        VirtualView.Loaded -= OnLoaded;
        VirtualView.Unloaded -= OnUnloaded;
        VirtualView.AnimationClock.StopAll();
        animationTime.Reset();
        if (surface is SKGLView gpu)
        {
            gpu.HasRenderLoop = false;
            gpu.PaintSurface -= OnGpuPaint;
            gpu.Touch -= OnTouch;
        }
        if (surface is SKCanvasView software)
        {
            software.PaintSurface -= OnSoftwarePaint;
            software.Touch -= OnTouch;
        }
        surface?.Handler?.DisconnectHandler();
        if (surface is not null)
            surface.Parent = null;
        surface = null;
        lock (pictureLock)
        {
            picture?.Dispose();
            picture = null;
        }
        base.DisconnectHandler(platformView);
    }

    private void OnLoaded(object? sender, EventArgs args) => QueueFrame();

    private void OnUnloaded(object? sender, EventArgs args) => VirtualView.AnimationClock.StopAll();

    private void OnInvalidated(object? sender, EventArgs args)
    {
        if (!recording)
            QueueFrame();
    }

    private void OnRunningChanged(object? sender, EventArgs args)
    {
        var clock = VirtualView.AnimationClock;
        if (clock.IsRunning)
        {
            clockOffset = clock.FrameTime;
            animationTime.Restart();
        }
        else
        {
            animationTime.Stop();
        }
        if (surface is SKGLView gpu)
            gpu.HasRenderLoop = clock.IsRunning;
        QueueFrame();
    }

    private void QueueFrame()
    {
        if (!connected || Interlocked.Exchange(ref frameQueued, 1) != 0)
            return;
        VirtualView.Dispatcher.Dispatch(() =>
        {
            Interlocked.Exchange(ref frameQueued, 0);
            if (!connected || VirtualView.Width <= 0 || VirtualView.Height <= 0)
                return;
            recording = true;
            try
            {
                var clock = VirtualView.AnimationClock;
                if (clock.IsRunning)
                    clock.Tick(clockOffset + animationTime.Elapsed);
                using var recorder = new SKPictureRecorder();
                var size = new Size(VirtualView.Width, VirtualView.Height);
                var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)size.Width, (float)size.Height));
                VirtualView.Paint(canvas);
                var nextPicture = recorder.EndRecording();
                lock (pictureLock)
                {
                    picture?.Dispose();
                    picture = nextPicture;
                    pictureSize = size;
                }
            }
            finally
            {
                recording = false;
            }
            if (surface is SKGLView gpu)
                gpu.InvalidateSurface();
            else if (surface is SKCanvasView software)
                software.InvalidateSurface();
        });
    }

    private void OnGpuPaint(object? sender, SKPaintGLSurfaceEventArgs args) => PaintSurface(args.Surface.Canvas, args.Info);

    private void OnSoftwarePaint(object? sender, SKPaintSurfaceEventArgs args) => PaintSurface(args.Surface.Canvas, args.Info);

    private void PaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        pixelWidth = info.Width;
        pixelHeight = info.Height;
        canvas.Clear(SKColors.Transparent);
        lock (pictureLock)
        {
            if (picture is not null)
            {
                var saveCount = canvas.Save();
                canvas.Scale((float)(info.Width / pictureSize.Width), (float)(info.Height / pictureSize.Height));
                canvas.DrawPicture(picture);
                canvas.RestoreToCount(saveCount);
            }
        }
        if (animationTime.IsRunning)
            QueueFrame();
    }

    private void OnTouch(object? sender, SKTouchEventArgs args)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
            return;
        SkUiTouchAction? action = args.ActionType switch
        {
            SKTouchAction.Pressed => SkUiTouchAction.Pressed,
            SKTouchAction.Moved => SkUiTouchAction.Moved,
            SKTouchAction.Released => SkUiTouchAction.Released,
            SKTouchAction.Cancelled => SkUiTouchAction.Cancelled,
            _ => null
        };
        if (action is null)
            return;
        var point = new Point(
            args.Location.X * VirtualView.Width / pixelWidth + VirtualView.Frame.X,
            args.Location.Y * VirtualView.Height / pixelHeight + VirtualView.Frame.Y);
        if (SkUiView.MapPoint(VirtualView, point, out var local))
            args.Handled = VirtualView.Touch(new(args.Id, action.Value, local));
    }
}
#endif