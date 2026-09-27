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

    /// <summary>Compositor statistics of this surface.</summary>
    internal SkUiRenderStatistics RenderStatistics => _renderer?.Compositor.Statistics ?? default;

    /// <summary>Clears <see cref="RenderStatistics"/>.</summary>
    internal void ResetRenderStatistics() => _renderer?.Compositor.ResetStatistics();

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
            _gpu = new SkUiGlTextureView(MauiContext!.Context!) { TouchHandler = OnSurfaceTouch, NativeGestureState = GetNativeGestureState };
            surfaceNative = _gpu;
        }
        else
            surfaceNative = CreateMauiSurface(gpu: false);
#elif IOS || MACCATALYST
        if (VirtualView.HwAccelerated)
        {
            _gpu = new SkUiMetalView { TouchHandler = OnSurfaceTouch, NativeGestureState = GetNativeGestureState };
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
        var platformSurface = _mauiSurface.ToPlatform(MauiContext!);
#if IOS || MACCATALYST
        // Same native-ancestor coordination as the Metal view. SkiaSharp's own touch recognizer is disabled; our
        // deliverer feeds the drawn tree and then updates the gate, in a fixed order.
        if (_mauiSurface is SKCanvasView canvasView)
            canvasView.EnableTouchEvents = false;
        else if (_mauiSurface is SKGLView glView)
            glView.EnableTouchEvents = false;
        _mauiGate = new SkUiNativeGestureGate(platformSurface);
        platformSurface.AddGestureRecognizer(_mauiGate);
        platformSurface.AddGestureRecognizer(new SkUiTouchDeliverer(_mauiGate) { TouchHandler = OnSurfaceTouch, NativeGestureState = GetNativeGestureState });
        platformSurface.UserInteractionEnabled = true;
#endif
        return platformSurface;
    }

#if IOS || MACCATALYST
    private SkUiNativeGestureGate? _mauiGate;
#endif

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
#if WINDOWS
        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnCompositionRendering;
#endif
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

    /// <summary>Adds a native overlay view above the Skia surface (inside its own clip wrapper).</summary>
    internal void AttachOverlay(PlatformView child) => _container?.AddOverlay(child);

    /// <summary>Removes a previously attached native overlay view.</summary>
    internal void DetachOverlay(PlatformView child) => _container?.RemoveOverlay(child);

    /// <summary>Hides / shows a native overlay (e.g. while its snapshot is drawn instead).</summary>
    internal void SetOverlayHidden(PlatformView child, bool hidden) => _container?.SetOverlayHidden(child, hidden);

    /// <summary>
    /// Positions a native overlay at root-relative DIP <paramref name="bounds"/>, visible and touchable only inside
    /// <paramref name="clip"/> (ancestor scroll viewports / clipping ancestors).
    /// </summary>
    internal void UpdateOverlayBounds(PlatformView child, Rect bounds, Rect clip)
    {
#if ANDROID
        var context = MauiContext!.Context!;
        Android.Graphics.Rect Px(Rect rect) => new(
            (int)Math.Floor(context.ToPixels(rect.X)), (int)Math.Floor(context.ToPixels(rect.Y)),
            (int)Math.Ceiling(context.ToPixels(rect.Right)), (int)Math.Ceiling(context.ToPixels(rect.Bottom)));
        _container?.SetOverlayBounds(child, Px(bounds), Px(clip));
#elif IOS || MACCATALYST
        _container?.SetOverlayBounds(child,
            new CoreGraphics.CGRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            new CoreGraphics.CGRect(clip.X, clip.Y, clip.Width, clip.Height));
#elif WINDOWS
        _container?.SetOverlayBounds(child, bounds, clip);
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
#if WINDOWS
        // SKXamlCanvas / SKSwapChainPanel repaint as soon as the dispatcher runs the request, so continuous frames
        // (flings, spinners) re-queued from each paint starve input and the window stops responding. Pace them to
        // the compositor's frame, like invalidate() on Android and setNeedsDisplay on iOS.
        VirtualView.Dispatcher.Dispatch(() => Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnCompositionRendering);
#else
        VirtualView.Dispatcher.Dispatch(InvalidateMauiSurfaceNow);
#endif
    }

    private void InvalidateMauiSurfaceNow()
    {
        Interlocked.Exchange(ref _invalidateQueued, 0);
        if (_mauiSurface is SKGLView gl) gl.InvalidateSurface();
        else if (_mauiSurface is SKCanvasView canvas) canvas.InvalidateSurface();
    }

#if WINDOWS
    private void OnCompositionRendering(object? sender, object args)
    {
        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnCompositionRendering;
        InvalidateMauiSurfaceNow();
    }
#endif

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
        if (_renderer is not { } renderer)
            return;
        var continuous = renderer.Render(canvas, info);
        canvas.Flush();
        renderer.CompleteFrame();
        if (continuous)
            InvalidateMauiSurface();
    }

    private bool OnSurfaceTouch(SkUiTouchEvent touch) => _renderer?.TouchDips(touch) == true;

    private SkUiNativeGestureState GetNativeGestureState() =>
        VirtualView is { } view ? view.Router.NativeState : SkUiNativeGestureState.None;

#if ANDROID
    private bool _mauiDisallowingIntercept;
#endif

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
#if ANDROID
        // Same native-parent coordination as the GL surface (requests propagate to every ancestor).
        var disallow = action is not (SkUiTouchAction.Released or SkUiTouchAction.Cancelled)
            && GetNativeGestureState() != SkUiNativeGestureState.None;
        if (disallow != _mauiDisallowingIntercept)
        {
            _mauiDisallowingIntercept = disallow;
            _container?.RequestDisallowInterceptTouchEvent(disallow);
        }
#endif
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

    private readonly Dictionary<Android.Views.View, OverlayState> _overlays = [];

    private sealed class OverlayState(SkUiOverlayClip clip)
    {
        public SkUiOverlayClip Clip { get; } = clip;
        public Android.Graphics.Rect Bounds = new();
        public Android.Graphics.Rect ClipRect = new();
        public bool Hidden;
    }

    public void AddOverlay(Android.Views.View child)
    {
        if (_overlays.ContainsKey(child)) return;
        var clip = new SkUiOverlayClip(Context!);
        clip.AddView(child);
        _overlays[child] = new OverlayState(clip);
        AddView(clip);
    }

    public void RemoveOverlay(Android.Views.View child)
    {
        if (!_overlays.Remove(child, out var overlay)) return;
        overlay.Clip.RemoveView(child);
        RemoveView(overlay.Clip);
    }

    public void SetOverlayHidden(Android.Views.View child, bool hidden)
    {
        if (!_overlays.TryGetValue(child, out var overlay)) return;
        overlay.Hidden = hidden;
        UpdateVisibility(overlay);
    }

    public void SetOverlayBounds(Android.Views.View child, Android.Graphics.Rect bounds, Android.Graphics.Rect clip)
    {
        if (!_overlays.TryGetValue(child, out var overlay)) return;
        overlay.Bounds = bounds;
        overlay.ClipRect = new Android.Graphics.Rect(clip);
        if (!overlay.ClipRect.Intersect(bounds))
            overlay.ClipRect.SetEmpty();
        UpdateVisibility(overlay);
        // Position directly: waiting for RequestLayout is unreliable here, because the MAUI parent skips re-measuring
        // this container when its own size is unchanged, and then Android never calls OnLayout.
        LayoutOverlay(overlay);
    }

    private static void LayoutOverlay(OverlayState overlay)
    {
        var clip = overlay.ClipRect;
        overlay.Clip.ChildRect = new Android.Graphics.Rect(
            overlay.Bounds.Left - clip.Left, overlay.Bounds.Top - clip.Top,
            overlay.Bounds.Right - clip.Left, overlay.Bounds.Bottom - clip.Top);
        overlay.Clip.Measure(
            Android.Views.View.MeasureSpec.MakeMeasureSpec(clip.Width(), Android.Views.MeasureSpecMode.Exactly),
            Android.Views.View.MeasureSpec.MakeMeasureSpec(clip.Height(), Android.Views.MeasureSpecMode.Exactly));
        overlay.Clip.Layout(clip.Left, clip.Top, clip.Right, clip.Bottom);
    }

    private static void UpdateVisibility(OverlayState overlay) =>
        overlay.Clip.Visibility = overlay.Hidden || overlay.ClipRect.IsEmpty ? Android.Views.ViewStates.Invisible : Android.Views.ViewStates.Visible;

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        for (var index = 0; index < ChildCount; index++)
        {
            var child = GetChildAt(index)!;
            if (child is SkUiOverlayClip clipView && _overlays.Values.FirstOrDefault(o => ReferenceEquals(o.Clip, clipView)) is { } overlay)
            {
                LayoutOverlay(overlay);
            }
            else
            {
                child.Layout(0, 0, right - left, bottom - top);
            }
        }
    }
}

/// <summary>
/// Clips one overlay to its visible rectangle: a view group sized to the clip, with the overlay placed at its full
/// bounds inside, so it neither draws nor receives touches outside the ancestor scroll viewports.
/// </summary>
internal sealed class SkUiOverlayClip : Android.Views.ViewGroup
{
    public SkUiOverlayClip(Android.Content.Context context) : base(context) => SetClipChildren(true);

    /// <summary>JNI activation constructor.</summary>
    public SkUiOverlayClip(IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { }

    private Android.Graphics.Rect _childRect = new();

    /// <summary>
    /// Overlay rectangle relative to this clip view (pixels). A change forces the next layout pass: when only the
    /// offset changes (same clip size), Android would otherwise skip re-measuring / re-laying out this view.
    /// </summary>
    public Android.Graphics.Rect ChildRect
    {
        get => _childRect;
        set
        {
            if (_childRect.Equals(value))
                return;
            _childRect = value;
            ForceLayout();
        }
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        for (var index = 0; index < ChildCount; index++)
            GetChildAt(index)!.Measure(
                MeasureSpec.MakeMeasureSpec(Math.Max(0, ChildRect.Width()), Android.Views.MeasureSpecMode.Exactly),
                MeasureSpec.MakeMeasureSpec(Math.Max(0, ChildRect.Height()), Android.Views.MeasureSpecMode.Exactly));
        SetMeasuredDimension(MeasureSpec.GetSize(widthMeasureSpec), MeasureSpec.GetSize(heightMeasureSpec));
    }

    protected override void OnLayout(bool changed, int l, int t, int r, int b)
    {
        for (var index = 0; index < ChildCount; index++)
            GetChildAt(index)!.Layout(ChildRect.Left, ChildRect.Top, ChildRect.Right, ChildRect.Bottom);
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
    private readonly Dictionary<UIKit.UIView, OverlayState> _overlays = [];

    private sealed class OverlayState(UIKit.UIView clip)
    {
        public UIKit.UIView Clip { get; } = clip;
        public CoreGraphics.CGRect Bounds;
        public CoreGraphics.CGRect ClipRect;
        public bool Hidden;
    }

    public void AddOverlay(UIKit.UIView child)
    {
        if (_overlays.ContainsKey(child)) return;
        // Clip views are plain UIViews that MAUI never tracks (the KVO note above applies to MAUI-created views).
        var clip = new UIKit.UIView { ClipsToBounds = true, BackgroundColor = UIKit.UIColor.Clear };
        clip.AddSubview(child);
        _overlays[child] = new OverlayState(clip);
        AddSubview(clip);
    }

    public void RemoveOverlay(UIKit.UIView child)
    {
        if (!_overlays.Remove(child, out var overlay)) return;
        child.RemoveFromSuperview();
        overlay.Clip.RemoveFromSuperview();
    }

    public void SetOverlayHidden(UIKit.UIView child, bool hidden)
    {
        if (!_overlays.TryGetValue(child, out var overlay)) return;
        overlay.Hidden = hidden;
        overlay.Clip.Hidden = hidden || overlay.ClipRect.IsEmpty;
    }

    public void SetOverlayBounds(UIKit.UIView child, CoreGraphics.CGRect bounds, CoreGraphics.CGRect clip)
    {
        if (!_overlays.TryGetValue(child, out var overlay)) return;
        overlay.Bounds = bounds;
        var visible = CoreGraphics.CGRect.Intersect(bounds, clip);
        overlay.ClipRect = visible.Width <= 0 || visible.Height <= 0 || double.IsInfinity(visible.X) ? CoreGraphics.CGRect.Empty : visible;
        overlay.Clip.Hidden = overlay.Hidden || overlay.ClipRect.IsEmpty;
        SetNeedsLayout();
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var overlays = _overlays.Values;
        foreach (var view in Subviews)
        {
            if (overlays.FirstOrDefault(o => ReferenceEquals(o.Clip, view)) is { } overlay)
            {
                view.Frame = overlay.ClipRect;
                foreach (var child in view.Subviews)
                    child.Frame = new CoreGraphics.CGRect(overlay.Bounds.X - overlay.ClipRect.X, overlay.Bounds.Y - overlay.ClipRect.Y,
                        overlay.Bounds.Width, overlay.Bounds.Height);
            }
            else
            {
                view.Frame = Bounds;
            }
        }
    }
}
#elif WINDOWS
internal sealed class SkUiOverlayContainer : Microsoft.UI.Xaml.Controls.Canvas
{
    private readonly Dictionary<Microsoft.UI.Xaml.FrameworkElement, OverlayState> _overlays = [];

    private sealed class OverlayState(Microsoft.UI.Xaml.Controls.Canvas clip)
    {
        public Microsoft.UI.Xaml.Controls.Canvas Clip { get; } = clip;
        public bool Hidden;
        public bool Empty;
    }

    public SkUiOverlayContainer() => SizeChanged += OnSizeChanged;

    private void OnSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs args)
    {
        if (Children.Count > 0 && Children[0] is Microsoft.UI.Xaml.FrameworkElement surface && !_overlays.Values.Any(o => ReferenceEquals(o.Clip, surface)))
        {
            surface.Width = args.NewSize.Width;
            surface.Height = args.NewSize.Height;
        }
    }

    public void AddOverlay(Microsoft.UI.Xaml.FrameworkElement child)
    {
        if (_overlays.ContainsKey(child)) return;
        var clip = new Microsoft.UI.Xaml.Controls.Canvas();
        clip.Children.Add(child);
        _overlays[child] = new OverlayState(clip);
        Children.Add(clip);
    }

    public void RemoveOverlay(Microsoft.UI.Xaml.FrameworkElement child)
    {
        if (!_overlays.Remove(child, out var overlay)) return;
        overlay.Clip.Children.Remove(child);
        Children.Remove(overlay.Clip);
    }

    public void SetOverlayHidden(Microsoft.UI.Xaml.FrameworkElement child, bool hidden)
    {
        if (!_overlays.TryGetValue(child, out var overlay)) return;
        overlay.Hidden = hidden;
        overlay.Clip.Visibility = hidden || overlay.Empty ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    }

    public void SetOverlayBounds(Microsoft.UI.Xaml.FrameworkElement child, Rect bounds, Rect clip)
    {
        if (!_overlays.TryGetValue(child, out var overlay)) return;
        var visible = bounds.Intersect(clip);
        overlay.Empty = visible.Width <= 0 || visible.Height <= 0;
        overlay.Clip.Visibility = overlay.Hidden || overlay.Empty ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        if (overlay.Empty) return;
        SetLeft(overlay.Clip, visible.X);
        SetTop(overlay.Clip, visible.Y);
        overlay.Clip.Width = visible.Width;
        overlay.Clip.Height = visible.Height;
        // Clip also limits hit-testing to the visible part.
        overlay.Clip.Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry { Rect = new global::Windows.Foundation.Rect(0, 0, visible.Width, visible.Height) };
        SetLeft(child, bounds.X - visible.X);
        SetTop(child, bounds.Y - visible.Y);
        child.Width = bounds.Width;
        child.Height = bounds.Height;
    }
}
#endif
#endif