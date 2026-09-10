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
    private readonly Stopwatch animationTime = new();
    private View? surface;
    private SkUiFrameRenderer? renderer;
    private TimeSpan clockOffset;

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
        SkUiFrameRenderer.EnsureStandalone(VirtualView);
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
        renderer = new SkUiFrameRenderer(VirtualView,
            action => VirtualView.Dispatcher.Dispatch(action), InvalidateSurface, TickAnimation);
        VirtualView.AnimationClock.RunningChanged += OnRunningChanged;
        VirtualView.Loaded += OnLoaded;
        VirtualView.Unloaded += OnUnloaded;
        OnRunningChanged(this, EventArgs.Empty);
        QueueFrame();
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(PlatformView platformView)
    {
        VirtualView.AnimationClock.RunningChanged -= OnRunningChanged;
        VirtualView.Loaded -= OnLoaded;
        VirtualView.Unloaded -= OnUnloaded;
        renderer?.Dispose();
        renderer = null;
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
        base.DisconnectHandler(platformView);
    }

    private void OnLoaded(object? sender, EventArgs args) => QueueFrame();

    private void OnUnloaded(object? sender, EventArgs args) => VirtualView.AnimationClock.StopAll();

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

    private void QueueFrame() => renderer?.RequestFrame();

    private void TickAnimation()
    {
        var clock = VirtualView.AnimationClock;
        if (clock.IsRunning)
            clock.Tick(clockOffset + animationTime.Elapsed);
    }

    private void InvalidateSurface()
    {
        if (surface is SKGLView gpu)
            gpu.InvalidateSurface();
        else if (surface is SKCanvasView software)
            software.InvalidateSurface();
    }

    private void OnGpuPaint(object? sender, SKPaintGLSurfaceEventArgs args) => PaintSurface(args.Surface.Canvas, args.Info);

    private void OnSoftwarePaint(object? sender, SKPaintSurfaceEventArgs args) => PaintSurface(args.Surface.Canvas, args.Info);

    private void PaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        renderer?.Replay(canvas, info);
        if (animationTime.IsRunning)
            QueueFrame();
    }

    private void OnTouch(object? sender, SKTouchEventArgs args)
    {
        SkUiTouchAction? action = args.ActionType switch
        {
            SKTouchAction.Pressed => SkUiTouchAction.Pressed,
            SKTouchAction.Moved => SkUiTouchAction.Moved,
            SKTouchAction.Released => SkUiTouchAction.Released,
            SKTouchAction.Cancelled => SkUiTouchAction.Cancelled,
            SKTouchAction.WheelChanged => SkUiTouchAction.Wheel,
            _ => null
        };
        if (action is null)
            return;
        args.Handled = renderer?.TouchPixels(new(args.Id, action.Value,
            new Point(args.Location.X, args.Location.Y), null, args.WheelDelta)) == true;
    }
}
#endif