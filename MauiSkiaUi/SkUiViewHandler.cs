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
    private readonly Stopwatch _animationTime = new();
    private View? _surface;
    private SkUiFrameRenderer? _renderer;
    private TimeSpan _clockOffset;
    private SkUiOverlayContainer? _container;
    private int _animationVsyncQueued;

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
            _surface = gpu;
        }
        else
        {
            var software = new SKCanvasView { EnableTouchEvents = true, IgnorePixelScaling = false };
            software.PaintSurface += OnSoftwarePaint;
            software.Touch += OnTouch;
            _surface = software;
        }
        _surface.Parent = VirtualView;
        var surfaceNative = _surface.ToPlatform(MauiContext!);
#if ANDROID
        _container = new SkUiOverlayContainer(MauiContext!.Context!);
        _container.AddView(surfaceNative);
#elif IOS || MACCATALYST
        _container = new SkUiOverlayContainer();
        _container.AddSubview(surfaceNative);
#elif WINDOWS
        container = new SkUiOverlayContainer();
        container.Children.Add(surfaceNative);
#endif
        return _container;
    }

    /// <inheritdoc />
    protected override void ConnectHandler(PlatformView platformView)
    {
        base.ConnectHandler(platformView);
        _renderer = new SkUiFrameRenderer(VirtualView,
            action => VirtualView.Dispatcher.Dispatch(action), InvalidateSurface, beforePaint: static () => { });
        VirtualView.AnimationClock.RunningChanged += OnRunningChanged;
        VirtualView.Loaded += OnLoaded;
        VirtualView.Unloaded += OnUnloaded;
        OnRunningChanged(this, EventArgs.Empty);
        QueueFrame();
        NotifyRootAttached(VirtualView);
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(PlatformView platformView)
    {
        NotifyRootDetached(VirtualView);
        VirtualView.AnimationClock.RunningChanged -= OnRunningChanged;
        VirtualView.Loaded -= OnLoaded;
        VirtualView.Unloaded -= OnUnloaded;
        _renderer?.Dispose();
        _renderer = null;
        _animationTime.Reset();
        if (_surface is SKGLView gpu)
        {
            gpu.HasRenderLoop = false;
            gpu.PaintSurface -= OnGpuPaint;
            gpu.Touch -= OnTouch;
        }
        if (_surface is SKCanvasView software)
        {
            software.PaintSurface -= OnSoftwarePaint;
            software.Touch -= OnTouch;
        }
        _surface?.Handler?.DisconnectHandler();
        if (_surface is not null)
            _surface.Parent = null;
        _surface = null;
        _container = null;
        base.DisconnectHandler(platformView);
    }

    // Hosted SkUiMauiContentView nodes attach/detach their native overlay when the standalone root's own
    // handler (dis)connects, since that is the only time a MauiContext/native container is available.
    private static void NotifyRootAttached(SkUiView node)
    {
        if (node is SkUiMauiContentView overlay) overlay.NotifyRootAttached();
        foreach (var child in node.SkiaChildren)
            if (child is SkUiView view) NotifyRootAttached(view);
    }

    private static void NotifyRootDetached(SkUiView node)
    {
        if (node is SkUiMauiContentView overlay) overlay.NotifyRootDetached();
        foreach (var child in node.SkiaChildren)
            if (child is SkUiView view) NotifyRootDetached(view);
    }

    /// <summary>Finds the nearest ancestor whose handler is a standalone <see cref="SkUiViewHandler"/>, if any.</summary>
    internal static SkUiViewHandler? FindRoot(Element node)
    {
        for (IElement? ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor is Element element && element.Handler is SkUiViewHandler root)
                return root;
        return null;
    }

    /// <summary>Adds a native overlay view above the Skia surface.</summary>
    internal void AttachOverlay(PlatformView child)
    {
#if ANDROID
        _container?.AddView(child);
#elif IOS || MACCATALYST
        _container?.AddSubview(child);
#elif WINDOWS
        container?.Children.Add(child);
#endif
    }

    /// <summary>Removes a previously attached native overlay view.</summary>
    internal void DetachOverlay(PlatformView child)
    {
        _container?.ForgetOverlay(child);
#if ANDROID
        _container?.RemoveView(child);
#elif IOS || MACCATALYST
        child.RemoveFromSuperview();
#elif WINDOWS
        container?.Children.Remove(child);
#endif
    }

    /// <summary>Positions a native overlay view at the given root-relative DIP bounds.</summary>
    internal void UpdateOverlayBounds(PlatformView child, Rect dipBounds)
    {
#if ANDROID
        var context = MauiContext!.Context!;
        _container?.SetOverlayBounds(child,
            (int)context.ToPixels(dipBounds.X), (int)context.ToPixels(dipBounds.Y),
            (int)context.ToPixels(dipBounds.Right), (int)context.ToPixels(dipBounds.Bottom));
#elif IOS || MACCATALYST
        _container?.SetOverlayBounds(child, new CoreGraphics.CGRect(dipBounds.X, dipBounds.Y, dipBounds.Width, dipBounds.Height));
#elif WINDOWS
        container?.SetOverlayBounds(child, dipBounds.X, dipBounds.Y, dipBounds.Width, dipBounds.Height);
#endif
    }

    private void OnLoaded(object? sender, EventArgs args) => QueueFrame();

    private void OnUnloaded(object? sender, EventArgs args) => VirtualView.AnimationClock.StopAll();

    private void OnRunningChanged(object? sender, EventArgs args)
    {
        var clock = VirtualView.AnimationClock;
        if (clock.IsRunning)
        {
            _clockOffset = clock.FrameTime;
            _animationTime.Restart();
            if (_surface is SKGLView gpu)
                gpu.HasRenderLoop = true;
            // Drive the first tick from the surface cadence; do not force a full RecordFrame here.
            ScheduleAnimationVsync();
        }
        else
        {
            _animationTime.Stop();
            if (_surface is SKGLView gpu)
                gpu.HasRenderLoop = false;
            // Final present after the last animator stops (e.g. settle on final scroll offset).
            QueueFrame();
        }
    }

    private void QueueFrame() => _renderer?.RequestFrame();

    private void TickAnimation()
    {
        var clock = VirtualView.AnimationClock;
        if (clock.IsRunning)
            clock.Tick(_clockOffset + _animationTime.Elapsed);
    }

    /// <summary>
    /// Coalesces animation ticks onto the UI dispatcher. Tick may InvalidatePaint (record once);
    /// we do not call <see cref="QueueFrame"/> after every present — that was saturating the UI thread
    /// with full-tree records while <c>HasRenderLoop</c> fired.
    /// </summary>
    private void ScheduleAnimationVsync()
    {
        if (Interlocked.Exchange(ref _animationVsyncQueued, 1) == 1)
            return;
        VirtualView.Dispatcher.Dispatch(OnAnimationVsync);
    }

    private void OnAnimationVsync()
    {
        Interlocked.Exchange(ref _animationVsyncQueued, 0);
        if (_renderer is null || !_animationTime.IsRunning)
            return;
        TickAnimation();
        // Software surfaces have no HasRenderLoop; keep presenting so the next tick can run.
        // GL continues via HasRenderLoop. Record happens only when Tick invalidates paint.
        if (_animationTime.IsRunning && _surface is SKCanvasView)
            InvalidateSurface();
    }

    private void InvalidateSurface()
    {
        if (_surface is SKGLView gpu)
            gpu.InvalidateSurface();
        else if (_surface is SKCanvasView software)
            software.InvalidateSurface();
    }

    private void OnGpuPaint(object? sender, SKPaintGLSurfaceEventArgs args) => PaintSurface(args.Surface.Canvas, args.Info);

    private void OnSoftwarePaint(object? sender, SKPaintSurfaceEventArgs args) => PaintSurface(args.Surface.Canvas, args.Info);

    private void PaintSurface(SKCanvas canvas, SKImageInfo info)
    {
        _renderer?.Replay(canvas, info);
        if (_animationTime.IsRunning)
            ScheduleAnimationVsync();
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
        args.Handled = _renderer?.TouchPixels(new(args.Id, action.Value,
            new Point(args.Location.X, args.Location.Y), null, args.WheelDelta)) == true;
    }
}

/// <summary>
/// A native container that hosts the Skia surface (filling the container) plus zero or more absolutely
/// positioned native overlay views for <see cref="SkUiMauiContentView"/>. Positions are applied directly in
/// each platform's own layout pass rather than through normal layout params, since overlays are placed at
/// arbitrary DIP-derived coordinates unrelated to the container's own layout system.
/// </summary>
#if ANDROID
internal sealed class SkUiOverlayContainer(Android.Content.Context context) : Android.Widget.FrameLayout(context)
{
    private readonly Dictionary<Android.Views.View, Android.Graphics.Rect> _bounds = [];

    public void SetOverlayBounds(Android.Views.View child, int left, int top, int right, int bottom)
    {
        _bounds[child] = new Android.Graphics.Rect(left, top, right, bottom);
        RequestLayout();
    }

    public void ForgetOverlay(Android.Views.View child) => _bounds.Remove(child);

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        for (var index = 0; index < ChildCount; index++)
        {
            var child = GetChildAt(index)!;
            if (_bounds.TryGetValue(child, out var rect))
                child.Layout(rect.Left, rect.Top, rect.Right, rect.Bottom);
            else
                child.Layout(0, 0, right - left, bottom - top);
        }
    }
}
#elif IOS || MACCATALYST
internal sealed class SkUiOverlayContainer : UIKit.UIView
{
    private readonly Dictionary<UIKit.UIView, CoreGraphics.CGRect> _bounds = [];

    public void SetOverlayBounds(UIKit.UIView child, CoreGraphics.CGRect frame)
    {
        _bounds[child] = frame;
        SetNeedsLayout();
    }

    public void ForgetOverlay(UIKit.UIView child) => _bounds.Remove(child);

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        foreach (var view in Subviews)
            view.Frame = _bounds.TryGetValue(view, out var frame) ? frame : Bounds;
    }
}
#elif WINDOWS
internal sealed class SkUiOverlayContainer : Microsoft.UI.Xaml.Controls.Canvas
{
    public SkUiOverlayContainer() => SizeChanged += OnSizeChanged;

    private void OnSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs args)
    {
        if (Children.Count > 0 && Children[0] is Microsoft.UI.Xaml.FrameworkElement surface && !bounds.ContainsKey(surface))
        {
            surface.Width = args.NewSize.Width;
            surface.Height = args.NewSize.Height;
        }
    }

    private readonly Dictionary<Microsoft.UI.Xaml.FrameworkElement, bool> bounds = [];

    public void SetOverlayBounds(Microsoft.UI.Xaml.FrameworkElement child, double left, double top, double width, double height)
    {
        bounds[child] = true;
        SetLeft(child, left);
        SetTop(child, top);
        child.Width = width;
        child.Height = height;
    }

    public void ForgetOverlay(Microsoft.UI.Xaml.FrameworkElement child) => bounds.Remove(child);
}
#endif
#endif