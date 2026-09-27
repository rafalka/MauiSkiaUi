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
using MauiSkiaUi.Surfaces;
using PlatformView = Android.Views.View;
#elif IOS || MACCATALYST
using MauiSkiaUi.Surfaces;
using PlatformView = UIKit.UIView;
#elif WINDOWS
using PlatformView = Microsoft.UI.Xaml.FrameworkElement;
#endif

namespace MauiSkiaUi;

/// <summary>
/// Owns one drawn surface for a standalone <see cref="SkUiView"/> root.
/// <para>
/// Threading: the UI thread only records dirty nodes and commits them (<see cref="SkUiFrameRenderer"/>).
/// With <see cref="SkUiView.HwAccelerated"/>, compositing, rasterization and composite-time animations run
/// on a render thread — a shared Metal render thread on iOS / Mac Catalyst, the GL thread of a
/// <c>GLTextureView</c> on Android — so they keep going while the UI thread is busy. Software surfaces
/// (and Windows) composite on the UI thread.
/// </para>
/// </summary>
public sealed class SkUiViewHandler : ViewHandler<SkUiView, PlatformView>
{
    private readonly Stopwatch _clockTime = new();
    private TimeSpan _clockOffset;
    private SkUiFrameRenderer? _renderer;
    private SkUiOverlayContainer? _container;
    private SkUiUiTicker? _ticker;
    private View? _mauiSurface;
    private int _invalidateQueued;
#if ANDROID
    private SkUiGlTextureView? _gpu;
#elif IOS || MACCATALYST
    private SkUiMetalView? _gpu;
#endif

    private static readonly IPropertyMapper<SkUiView, SkUiViewHandler> SkiaMapper = CreateMapper();

    /// <summary>Creates a handler using normal MAUI sizing and Skia-owned drawing properties.</summary>
    public SkUiViewHandler() : base(SkiaMapper) { }

    private static IPropertyMapper<SkUiView, SkUiViewHandler> CreateMapper()
    {
        var mapper = new PropertyMapper<SkUiView, SkUiViewHandler>(ViewMapper);
        // Background / opacity / transforms of the root are drawn inside the surface by the compositor,
        // not applied to the native container. The view already requests a frame when they change.
        foreach (var property in new[]
        {
            nameof(IView.Background), nameof(IView.Opacity), nameof(IView.TranslationX), nameof(IView.TranslationY),
            nameof(IView.Rotation), nameof(IView.Scale), nameof(IView.ScaleX), nameof(IView.ScaleY),
            nameof(IView.AnchorX), nameof(IView.AnchorY)
        })
            mapper[property] = static (handler, _) => handler.QueueFrame();
        return mapper;
    }

    /// <summary>True when compositing runs on a dedicated render thread (GPU surface on Apple / Android).</summary>
    internal bool RendersOffUiThread
    {
        get
        {
#if ANDROID || IOS || MACCATALYST
            return _gpu is not null;
#else
            return false;
#endif
        }
    }

    /// <inheritdoc />
    protected override PlatformView CreatePlatformView()
    {
        SkUiFrameRenderer.EnsureStandalone(VirtualView);
        PlatformView surfaceNative;
#if ANDROID
        if (VirtualView.HwAccelerated)
        {
            _gpu = new SkUiGlTextureView(MauiContext!.Context!) { TouchHandler = OnSurfaceTouch };
            surfaceNative = _gpu;
        }
        else
            surfaceNative = CreateMauiSurface(gpu: false);
#elif IOS || MACCATALYST
        if (VirtualView.HwAccelerated)
        {
            _gpu = new SkUiMetalView { TouchHandler = OnSurfaceTouch };
            surfaceNative = _gpu;
        }
        else
            surfaceNative = CreateMauiSurface(gpu: false);
#else
        surfaceNative = CreateMauiSurface(gpu: VirtualView.HwAccelerated);
#endif
#if ANDROID
        _container = new SkUiOverlayContainer(MauiContext!.Context!);
        _container.AddView(surfaceNative);
#elif IOS || MACCATALYST
        _container = new SkUiOverlayContainer();
        _container.AddSubview(surfaceNative);
#elif WINDOWS
        _container = new SkUiOverlayContainer();
        _container.Children.Add(surfaceNative);
#endif
        return _container!;
    }

    /// <summary>MAUI SkiaSharp view composited on the UI thread (software, and Windows GPU).</summary>
    private PlatformView CreateMauiSurface(bool gpu)
    {
        if (gpu)
        {
            var view = new SKGLView { EnableTouchEvents = true, IgnorePixelScaling = false };
            view.PaintSurface += OnMauiGpuPaint;
            view.Touch += OnMauiTouch;
            _mauiSurface = view;
        }
        else
        {
            var view = new SKCanvasView { EnableTouchEvents = true, IgnorePixelScaling = false };
            view.PaintSurface += OnMauiSoftwarePaint;
            view.Touch += OnMauiTouch;
            _mauiSurface = view;
        }
        _mauiSurface.Parent = VirtualView;
        return _mauiSurface.ToPlatform(MauiContext!);
    }

    /// <inheritdoc />
    protected override void ConnectHandler(PlatformView platformView)
    {
        base.ConnectHandler(platformView);
        _renderer = new SkUiFrameRenderer(VirtualView,
            action => VirtualView.Dispatcher.Dispatch(action), RequestRender, beforeFrame: static () => { });
#if ANDROID
        if (_gpu is not null) _gpu.FrameRenderer = _renderer;
#elif IOS || MACCATALYST
        if (_gpu is not null) _gpu.Surface.Renderer = _renderer;
#endif
        _ticker = new SkUiUiTicker(VirtualView.Dispatcher, OnUiTick);
        VirtualView.AnimationClock.RunningChanged += OnClockRunningChanged;
        VirtualView.Loaded += OnLoaded;
        OnClockRunningChanged(this, EventArgs.Empty);
        QueueFrame();
        NotifyRootAttached(VirtualView);
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(PlatformView platformView)
    {
        NotifyRootDetached(VirtualView);
        VirtualView.AnimationClock.RunningChanged -= OnClockRunningChanged;
        VirtualView.Loaded -= OnLoaded;
        VirtualView.AnimationClock.StopAll();
        _ticker?.Dispose();
        _ticker = null;
        _clockTime.Reset();

#if ANDROID
        if (_gpu is not null)
        {
            _gpu.FrameRenderer = null;
            _gpu.TouchHandler = null;
            // Detach synchronously while the managed peer is alive: OnDetachedFromWindow stops the GL thread now
            // instead of calling back into a dead peer later (GLTextureView has no JNI activation constructor).
            (_gpu.Parent as Android.Views.ViewGroup)?.RemoveView(_gpu);
        }
#elif IOS || MACCATALYST
        if (_gpu is not null)
        {
            _gpu.Surface.Renderer = null;
            _gpu.TouchHandler = null;
            _gpu.RemoveFromSuperview();
        }
#endif
        // Waits for an in-flight render-thread frame before releasing retained pictures.
        _renderer?.Dispose();
        _renderer = null;

        if (_mauiSurface is SKGLView gl)
        {
            gl.PaintSurface -= OnMauiGpuPaint;
            gl.Touch -= OnMauiTouch;
        }
        if (_mauiSurface is SKCanvasView canvas)
        {
            canvas.PaintSurface -= OnMauiSoftwarePaint;
            canvas.Touch -= OnMauiTouch;
        }
#if IOS || MACCATALYST
        // Detach before GC so removeFromSuperview is not deferred into the NSObject disposer.
        if (_mauiSurface?.Handler?.PlatformView is UIKit.UIView surfaceNative)
            surfaceNative.RemoveFromSuperview();
#endif
        _mauiSurface?.Handler?.DisconnectHandler();
        if (_mauiSurface is not null)
            _mauiSurface.Parent = null;
        _mauiSurface = null;
#if IOS || MACCATALYST
        _gpu?.Dispose();
#endif
        // Android: do not Dispose the GL view's managed peer. Java may still deliver SurfaceTexture / detach
        // callbacks, and without an activation constructor a disposed peer cannot be recreated (NotSupportedException).
        // The GC releases it once Java no longer references the view.
#if ANDROID || IOS || MACCATALYST
        _gpu = null;
#endif
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
        _container?.Children.Add(child);
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
        _container?.Children.Remove(child);
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
        _container?.SetOverlayBounds(child, dipBounds.X, dipBounds.Y, dipBounds.Width, dipBounds.Height);
#endif
    }

    private void OnLoaded(object? sender, EventArgs args)
    {
        QueueFrame();
        OnClockRunningChanged(this, EventArgs.Empty);
    }

    private void QueueFrame() => _renderer?.RequestFrame();

    /// <summary>Called by the frame renderer after a commit (UI thread) to get the new frame presented.</summary>
    private void RequestRender()
    {
#if ANDROID
        if (_gpu is not null) { _gpu.RequestRender(); return; }
#elif IOS || MACCATALYST
        if (_gpu is not null) { _gpu.Surface.RequestRender(); return; }
#endif
        InvalidateMauiSurface();
    }

    private void InvalidateMauiSurface()
    {
        if (Interlocked.Exchange(ref _invalidateQueued, 1) == 1)
            return;
        VirtualView.Dispatcher.Dispatch(() =>
        {
            Interlocked.Exchange(ref _invalidateQueued, 0);
            if (_mauiSurface is SKGLView gl) gl.InvalidateSurface();
            else if (_mauiSurface is SKCanvasView canvas) canvas.InvalidateSurface();
        });
    }

    private void OnClockRunningChanged(object? sender, EventArgs args)
    {
        var clock = VirtualView.AnimationClock;
        if (clock.IsRunning)
        {
            if (!_clockTime.IsRunning)
            {
                _clockOffset = clock.FrameTime;
                _clockTime.Restart();
            }
            _ticker?.Start();
        }
        else
        {
            _clockTime.Stop();
            _ticker?.Stop();
            QueueFrame();
        }
    }

    /// <summary>UI vsync while UI-clock animations run: tick them, then record + commit immediately.</summary>
    private void OnUiTick()
    {
        var clock = VirtualView.AnimationClock;
        if (!clock.IsRunning)
            return;
        clock.Tick(_clockOffset + _clockTime.Elapsed);
        _renderer?.PresentFrame();
    }

    private void OnMauiGpuPaint(object? sender, SKPaintGLSurfaceEventArgs args) =>
        PaintMauiSurface(args.Surface.Canvas, args.Info);

    private void OnMauiSoftwarePaint(object? sender, SKPaintSurfaceEventArgs args) =>
        PaintMauiSurface(args.Surface.Canvas, args.Info);

    private void PaintMauiSurface(SKCanvas canvas, SKImageInfo info)
    {
        if (_renderer?.Render(canvas, info) == true)
            InvalidateMauiSurface();
    }

    private bool OnSurfaceTouch(SkUiTouchEvent touch) => _renderer?.TouchDips(touch) == true;

    private void OnMauiTouch(object? sender, SKTouchEventArgs args)
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
internal sealed class SkUiOverlayContainer : Android.Widget.FrameLayout
{
    public SkUiOverlayContainer(Android.Content.Context context) : base(context) { }

    /// <summary>JNI activation constructor, used if Java calls back after the managed peer was released.</summary>
    public SkUiOverlayContainer(IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { }

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
// Derives from MauiView (with no cross-platform layout attached, so it only does plain UIKit layout)
// because MAUI's Loaded/Unloaded tracking special-cases its internal lifecycle interface: for any other platform
// view it installs bounds/frame KVO observers on the view's CALayer. When those managed
// observers are collected before removal, the layer keeps dangling observation info and the next KVO lookup —
// typically removeFromSuperview during UIView dealloc from the NSObject disposer — crashes in
// _NSKeyValueObservationInfoGetObservances. With MauiView MAUI uses MovedToWindow instead.
internal sealed class SkUiOverlayContainer : MauiView
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