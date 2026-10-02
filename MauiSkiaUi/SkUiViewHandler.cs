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
    public SkUiViewHandler() : base(SkiaMapper, SkiaCommandMapper) { }

    /// <summary>
    /// Diagnostics (leak tests): what this handler currently owns besides its platform view — renderer, compositor,
    /// platform surfaces, overlay container. All of it must be collectable once the handler is disconnected.
    /// </summary>
    internal IEnumerable<(object Instance, string Label)> GetOwnedObjects()
    {
        if (_renderer is { } renderer)
        {
            yield return (renderer, nameof(SkUiFrameRenderer));
            yield return (renderer.Compositor, nameof(Rendering.SkUiCompositor));
        }
        if (_container is not null)
            yield return (_container, nameof(SkUiOverlayContainer));
        if (_ticker is not null)
            yield return (_ticker, nameof(SkUiUiTicker));
        if (_mauiSurface is { } surface)
        {
            yield return (surface, $"{surface.GetType().Name} (MAUI surface)");
            if (surface.Handler is { } surfaceHandler)
            {
                yield return (surfaceHandler, $"{surfaceHandler.GetType().Name} (MAUI surface)");
                if (surfaceHandler.PlatformView is { } surfaceView)
                    yield return (surfaceView, $"{surfaceView.GetType().Name} (MAUI surface)");
            }
        }
#if ANDROID
        if (_gpu is not null)
            yield return (_gpu, nameof(SkUiGlTextureView));
#elif IOS || MACCATALYST
        if (_gpu is not null)
        {
            yield return (_gpu, nameof(SkUiMetalView));
            yield return (_gpu.Surface, nameof(SkUiMetalSurface));
        }
#endif
    }

    private static readonly CommandMapper<SkUiView, SkUiViewHandler> SkiaCommandMapper = new(ViewCommandMapper)
    {
#if WINDOWS
        [nameof(IView.InvalidateMeasure)] = static (handler, _, _) => handler.InvalidateNativeMeasure(),
#endif
    };

#if WINDOWS
    /// <summary>
    /// MAUI only invalidates the container itself, whose native desired size never changes, so WinUI never re-measured
    /// the MAUI panel that measures this root (cross-platform): WidthRequest / HeightRequest / content size changes of
    /// drawn children had no effect. Invalidate up to (and including) the nearest MAUI layout / content panel.
    /// </summary>
    private void InvalidateNativeMeasure()
    {
        for (var element = PlatformView as Microsoft.UI.Xaml.UIElement; element is not null;
             element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element) as Microsoft.UI.Xaml.UIElement)
        {
            element.InvalidateMeasure();
            element.InvalidateArrange();
            if (!ReferenceEquals(element, PlatformView) && element is LayoutPanel or ContentPanel)
                break;
        }
    }
#endif

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
#if WINDOWS
        // The drawn tree mirrors itself for RTL. WinUI FlowDirection (inherited through the XAML tree) would mirror
        // the surface's pixels on top of that (mirrored text, LTR order), so the container stays LeftToRight.
        mapper[nameof(IView.FlowDirection)] = static (handler, _) =>
            handler.PlatformView.FlowDirection = Microsoft.UI.Xaml.FlowDirection.LeftToRight;
#endif
        return mapper;
    }

    /// <summary>Compositor statistics of this surface, with its UI-thread animation frames.</summary>
    internal SkUiRenderStatistics RenderStatistics
    {
        get
        {
            var toMs = 1000.0 / Stopwatch.Frequency;
            return (_renderer?.Compositor.Statistics ?? default) with
            {
                UiFrames = _uiFrames,
                UiAverageMilliseconds = _uiFrames == 0 ? 0 : _uiTicks * toMs / _uiFrames,
                UiMaxMilliseconds = _uiMaxTicks * toMs
            };
        }
    }

    /// <summary>Pictures recorded and content shadows rasterized / drawn live on this surface since it was created.</summary>
    internal SkUiRenderCounters RenderCounters => _renderer is { } renderer
        ? new SkUiRenderCounters(renderer.RecordedPictures, renderer.Compositor.ShadowRasterizations, renderer.Compositor.LiveShadows)
        : default;

    /// <summary>Clears <see cref="RenderStatistics"/>.</summary>
    internal void ResetRenderStatistics()
    {
        _renderer?.Compositor.ResetStatistics();
        _uiFrames = _uiTicks = _uiMaxTicks = 0;
    }

    private long _uiFrames;
    private long _uiTicks;
    private long _uiMaxTicks;

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
        AttachHover(surfaceNative);
#if ANDROID
        _container = new SkUiOverlayContainer(MauiContext!.Context!);
        _container.AddView(surfaceNative);
#elif IOS || MACCATALYST
        _container = new SkUiOverlayContainer();
        _container.AddSubview(surfaceNative);
#elif WINDOWS
        _container = new SkUiOverlayContainer { FlowDirection = Microsoft.UI.Xaml.FlowDirection.LeftToRight };
        _container.Children.Add(surfaceNative);
#endif
        return _container!;
    }

#if WINDOWS
    // SKSwapChainPanel's first frame calls GRGlInterface.Create(). Skia's Windows path (GrGLInterfaces::MakeWin)
    // loads opengl32.dll, keeps pointers into it and frees it again; once nothing else holds opengl32 it unloads and
    // the next GPU panel calls into the unloaded module (native crash, fail-fast). Keep it loaded for the process.
    private static readonly Lazy<nint> _openGlPin = new(() =>
        System.Runtime.InteropServices.NativeLibrary.TryLoad("opengl32.dll", out var handle) ? handle : 0);
#endif

    /// <summary>MAUI SkiaSharp view composited on the UI thread (software, and Windows GPU).</summary>
    private PlatformView CreateMauiSurface(bool gpu)
    {
        if (gpu)
        {
#if WINDOWS
            _ = _openGlPin.Value;
#endif
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
        _mauiGate = new SkUiNativeGestureGate();
        _mauiDeliverer = new SkUiTouchDeliverer(_mauiGate) { TouchHandler = OnSurfaceTouch, NativeGestureState = GetNativeGestureState };
        platformSurface.AddGestureRecognizer(_mauiGate);
        platformSurface.AddGestureRecognizer(_mauiDeliverer);
        platformSurface.UserInteractionEnabled = true;
#endif
#if WINDOWS
        if (gpu)
            platformSurface.Unloaded += OnGpuPanelUnloaded;
        // After SkiaSharp's own handlers (handledEventsToo): the arena has seen the sample by then.
        platformSurface.AddHandler(Microsoft.UI.Xaml.UIElement.PointerMovedEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler(OnSurfacePointerMoved), true);
        platformSurface.AddHandler(Microsoft.UI.Xaml.UIElement.PointerCaptureLostEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler(OnSurfacePointerCaptureLost), true);
#endif
        return platformSurface;
    }

#if IOS || MACCATALYST
    private SkUiNativeGestureGate? _mauiGate;
    private SkUiTouchDeliverer? _mauiDeliverer;
#endif

#if WINDOWS
    /// <summary>Touch / pen pointers handed to an ancestor ScrollViewer (DirectManipulation) during this contact.</summary>
    private readonly HashSet<uint> _handedOver = [];
    private readonly Dictionary<long, Point> _lastTouchPixels = [];

    /// <summary>
    /// Native-parent coordination, like Android's intercept release: once no drawn gesture wants the touch (past the
    /// slop, nothing claimed — e.g. a drawn list at its end, a vertical drag on a carousel), give the pointer to the
    /// ancestor ScrollViewer. SkiaSharp captures every handled pointer and sets ManipulationMode = All, so
    /// DirectManipulation would otherwise never start from a drawn surface. Mouse drags never pan a ScrollViewer.
    /// </summary>
    private void OnSurfacePointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        var pointer = args.Pointer;
        if (pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse || !pointer.IsInContact
            || _handedOver.Contains(pointer.PointerId) || GetNativeGestureState() != SkUiNativeGestureState.None)
            return;
        _handedOver.Add(pointer.PointerId); // one attempt per contact
        var element = (Microsoft.UI.Xaml.UIElement)sender;
        if (!HasScrollViewerAncestor(element))
            return; // nothing native to hand over to
        // SkiaSharp set ManipulationMode = All and captured the pointer on press; both keep DirectManipulation off.
        var mode = element.ManipulationMode;
        _handingOver = pointer.PointerId;
        element.ManipulationMode = Microsoft.UI.Xaml.Input.ManipulationModes.System;
        element.ReleasePointerCapture(pointer);
        var started = Microsoft.UI.Xaml.UIElement.TryStartDirectManipulation(pointer);
        _handingOver = null;
        if (SkUiDiagnostics.TraceOn)
            SkUiDiagnostics.Write($"handover pointer {pointer.PointerId} to native ScrollViewer: {(started ? "started" : "failed, kept drawn")}");
        if (started)
        {
            CancelDrawnPointer(pointer.PointerId);
            return;
        }
        // Not taken: keep the drawn gesture as it was (capture, so the release still arrives here).
        element.ManipulationMode = mode;
        element.CapturePointer(pointer);
    }

    /// <summary>The pointer whose capture this handler releases itself during a handover attempt.</summary>
    private uint? _handingOver;

    private static bool HasScrollViewerAncestor(Microsoft.UI.Xaml.DependencyObject element)
    {
        for (var parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element); parent is not null;
             parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent))
            if (parent is Microsoft.UI.Xaml.Controls.ScrollViewer { ScrollableHeight: > 0 } or Microsoft.UI.Xaml.Controls.ScrollViewer { ScrollableWidth: > 0 })
                return true;
        return false;
    }

    /// <summary>
    /// The surface lost a contact that is still down (DirectManipulation took it, another element captured it, a system
    /// gesture): SkiaSharp reports nothing, so the drawn gesture never ended. The scroll gesture then kept tracking the
    /// lost pointer, ignored new drags and kept overlays frozen. Cancel it in the drawn tree. After a normal release
    /// the pointer is no longer tracked (PointerReleased comes first), so this does nothing.
    /// </summary>
    private void OnSurfacePointerCaptureLost(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        var id = args.Pointer.PointerId;
        if (_handingOver == id)
            return;
        if (SkUiDiagnostics.TraceOn && _lastTouchPixels.ContainsKey(id))
            SkUiDiagnostics.Write($"capture lost for active pointer {id} ({args.Pointer.PointerDeviceType}) -> cancel drawn gesture");
        CancelDrawnPointer(id);
    }

    private void CancelDrawnPointer(uint id)
    {
        if (_lastTouchPixels.Remove(id, out var position))
            _renderer?.TouchPixels(new(id, SkUiTouchAction.Cancelled, position));
    }

    private bool _reloadingGpuPanel;

    /// <summary>
    /// WinUI can raise a stale Unloaded (from an earlier removal, e.g. while MAUI wraps a Border's content) after the
    /// panel was loaded again. SKSwapChainPanel then disposes its GL context and ignores every Invalidate, so a surface
    /// created on a page that is already shown stayed blank. Reload the panel once so it recreates its context.
    /// </summary>
    private void OnGpuPanelUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs args)
    {
        if (_reloadingGpuPanel || sender is not Microsoft.UI.Xaml.FrameworkElement { IsLoaded: true } panel
            || _container is not { } container || !container.Children.Contains(panel))
            return;
        _reloadingGpuPanel = true;
        container.Children.Remove(panel);
        container.DispatcherQueue.TryEnqueue(() =>
        {
            _reloadingGpuPanel = false;
            if (ReferenceEquals(_container, container) && !container.Children.Contains(panel))
                container.Children.Insert(0, panel);
        });
    }
#endif

#if WINDOWS
    /// <inheritdoc />
    public override void PlatformArrange(Rect rect)
    {
        // SkUiView measures itself in cross-platform code and never asks the handler for a desired size, so WinUI never
        // measured the container, and it ignores Arrange on a measure-dirty element: inside a Border / native ScrollView
        // the container stayed 0x0 and GPU panels never got a size.
        PlatformView.Measure(new global::Windows.Foundation.Size(Math.Max(0, rect.Width), Math.Max(0, rect.Height)));
        base.PlatformArrange(rect);
    }
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
        SkUiLook.CurrentChanged += OnLookChanged;
        SkUiColorScheme.CurrentChanged += OnLookChanged;
        OnClockRunningChanged(this, EventArgs.Empty);
        QueueFrame();
        NotifyRootAttached(VirtualView);
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(PlatformView platformView)
    {
        DetachHover();
        NotifyRootDetached(VirtualView);
        VirtualView.AnimationClock.RunningChanged -= OnClockRunningChanged;
        VirtualView.Loaded -= OnLoaded;
        SkUiLook.CurrentChanged -= OnLookChanged;
        SkUiColorScheme.CurrentChanged -= OnLookChanged;
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
            // Waits for a frame the render thread is presenting, so nothing presents after the view is removed.
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
        if (_mauiSurface?.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement gpuPanel)
            gpuPanel.Unloaded -= OnGpuPanelUnloaded;
#endif
#if IOS || MACCATALYST
        // Detach before GC so removeFromSuperview is not deferred into the NSObject disposer. The recognizers go too:
        // the view retains them natively, which keeps their managed callbacks (into this handler) rooted.
        if (_mauiSurface?.Handler?.PlatformView is UIKit.UIView surfaceNative)
        {
            if (_mauiGate is not null)
                surfaceNative.RemoveGestureRecognizer(_mauiGate);
            if (_mauiDeliverer is not null)
                surfaceNative.RemoveGestureRecognizer(_mauiDeliverer);
            surfaceNative.RemoveFromSuperview();
        }
        if (_mauiDeliverer is not null)
        {
            _mauiDeliverer.TouchHandler = null;
            _mauiDeliverer.NativeGestureState = null;
        }
        _mauiGate = null;
        _mauiDeliverer = null;
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

    /// <summary>
    /// Adds a native overlay view above the Skia surface (inside its own clip wrapper). Drags that start on it are also
    /// offered to the continuous gestures of <paramref name="owner"/>'s drawn ancestors (e.g. a drawn scroller), which
    /// take the touch over from the native control once they claim it.
    /// </summary>
    internal void AttachOverlay(PlatformView child, SkUiMauiContentView owner) =>
        _container?.AddOverlay(child, touch => _renderer?.TouchOverlayDips(touch, owner) ?? SkUiNativeGestureState.None);

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
#if WINDOWS
        // Nothing may be dirty, so also repaint: restarts continuous frames stopped while unloaded.
        InvalidateMauiSurface();
#endif
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
        // Out of the tree (e.g. a Shell page navigated away from), continuous frames stop here instead of painting
        // forever; OnLoaded queues a frame again. Android / iOS stop on their own: detached views do not draw.
        if (VirtualView is not { IsLoaded: true })
        {
            Interlocked.Exchange(ref _invalidateQueued, 0);
            return;
        }
        InvalidateMauiSurfaceNow();
    }
#endif

    /// <summary>The look or color scheme changed: re-measure and redraw this surface's drawn tree with it.</summary>
    private void OnLookChanged(object? sender, EventArgs args)
    {
        if (VirtualView is not { } root)
            return;
        if (root.Dispatcher.IsDispatchRequired)
        {
            root.Dispatcher.Dispatch(() => OnLookChanged(sender, args));
            return;
        }
        Rendering.SkUiRenderInvalidation.MarkLookChanged(root);
        ((IView)root).InvalidateMeasure();
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
        var started = Stopwatch.GetTimestamp();
        clock.Tick(_clockOffset + _clockTime.Elapsed);
        _renderer?.PresentFrame();
        var ticks = Stopwatch.GetTimestamp() - started;
        _uiFrames++;
        _uiTicks += ticks;
        _uiMaxTicks = Math.Max(_uiMaxTicks, ticks);
    }

    private void OnMauiGpuPaint(object? sender, SKPaintGLSurfaceEventArgs args)
    {
        PaintMauiSurface(args.Surface.Canvas, args.Info);
#if WINDOWS
        // ANGLE resizes the swap chain at the next present after a panel size change, so the frame painted right after a
        // resize still targets the old size (stretched / cropped). Paint again until the target matches the panel.
        if (sender is SKGLView { Handler.PlatformView: Microsoft.UI.Xaml.Controls.SwapChainPanel panel }
            && (Math.Abs(args.Info.Width - panel.ActualWidth * panel.CompositionScaleX) > 1
                || Math.Abs(args.Info.Height - panel.ActualHeight * panel.CompositionScaleY) > 1)
            && _staleGpuFrames++ < 3)
            InvalidateMauiSurface();
        else
            _staleGpuFrames = 0;
#endif
    }

#if WINDOWS
    private int _staleGpuFrames;
#endif

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

#if ANDROID
    private Android.Views.View? _hoverView;
#elif IOS || MACCATALYST
    private UIKit.UIView? _hoverView;
    private UIKit.UIHoverGestureRecognizer? _hover;
#endif

    /// <summary>
    /// Hover input for <see cref="SkUiView.IsPointerOver"/> and the <c>PointerOver</c> visual state. Windows reports
    /// hover through SkiaSharp's touch events (<see cref="OnMauiTouch"/>); Android and Apple surfaces need their own
    /// listener (mouse, trackpad and stylus hover; the iPad pointer), for GPU and software surfaces alike.
    /// </summary>
    private void AttachHover(PlatformView surface)
    {
#if ANDROID
        _hoverView = surface;
        surface.Hover += OnAndroidHover;
#elif IOS || MACCATALYST
        // Weak: the view retains the recognizer natively, which would root this handler through the callback.
        var weak = new WeakReference<SkUiViewHandler>(this);
        _hover = new UIKit.UIHoverGestureRecognizer(recognizer =>
        {
            if (weak.TryGetTarget(out var handler))
                handler.OnAppleHover(recognizer);
        });
        _hoverView = surface;
        surface.AddGestureRecognizer(_hover);
#endif
    }

    private void DetachHover()
    {
#if ANDROID
        if (_hoverView is not null)
            _hoverView.Hover -= OnAndroidHover;
        _hoverView = null;
#elif IOS || MACCATALYST
        if (_hover is not null)
            _hoverView?.RemoveGestureRecognizer(_hover);
        _hover?.Dispose();
        _hover = null;
        _hoverView = null;
#endif
        // The pointer may still be over the surface: nothing stays pointer-over in a tree without a surface.
        VirtualView?.Router.ClearHover();
    }

#if ANDROID
    private void OnAndroidHover(object? sender, Android.Views.View.HoverEventArgs args)
    {
        args.Handled = false;
        if (args.Event is not { } motion)
            return;
        SkUiTouchAction? action = motion.ActionMasked switch
        {
            Android.Views.MotionEventActions.HoverEnter or Android.Views.MotionEventActions.HoverMove => SkUiTouchAction.HoverMoved,
            Android.Views.MotionEventActions.HoverExit => SkUiTouchAction.HoverExited,
            _ => null
        };
        if (action is not null)
            _renderer?.TouchPixels(new(0, action.Value, new Point(motion.GetX(), motion.GetY())));
    }
#elif IOS || MACCATALYST
    private void OnAppleHover(UIKit.UIHoverGestureRecognizer recognizer)
    {
        if (_hoverView is not { } view)
            return;
        if (recognizer.State is UIKit.UIGestureRecognizerState.Began or UIKit.UIGestureRecognizerState.Changed)
        {
            var location = recognizer.LocationInView(view);
            _renderer?.TouchDips(new(0, SkUiTouchAction.HoverMoved, new Point(location.X, location.Y)));
        }
        else
        {
            _renderer?.TouchDips(new(0, SkUiTouchAction.HoverExited, Point.Zero));
        }
    }
#endif

    private void OnMauiTouch(object? sender, SKTouchEventArgs args)
    {
        SkUiTouchAction? action = args.ActionType switch
        {
            SKTouchAction.Pressed => SkUiTouchAction.Pressed,
            // A pointer moving without contact hovers (Windows mouse / pen; SkiaSharp reports no hover elsewhere).
            SKTouchAction.Moved or SKTouchAction.Entered when !args.InContact => SkUiTouchAction.HoverMoved,
            SKTouchAction.Exited => SkUiTouchAction.HoverExited,
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
        if (SkUiDiagnostics.TraceOn)
            SkUiDiagnostics.Write($"touch {action} id={args.Id} {args.DeviceType} px={args.Location.X:F0},{args.Location.Y:F0} handled={args.Handled} native={GetNativeGestureState()} arenas={VirtualView?.Router.ActiveArenaCount} root={VirtualView?.AutomationId ?? VirtualView?.GetType().Name}");
#if WINDOWS
        if (action is SkUiTouchAction.Released or SkUiTouchAction.Cancelled)
        {
            _lastTouchPixels.Remove(args.Id);
            _handedOver.Remove((uint)args.Id);
        }
        else if (action is SkUiTouchAction.Pressed or SkUiTouchAction.Moved)
        {
            // Contacts cancelled by a handover never report Released here: forget them once nothing is down.
            if (action == SkUiTouchAction.Pressed && _lastTouchPixels.Count == 0)
                _handedOver.Clear();
            _lastTouchPixels[args.Id] = new Point(args.Location.X, args.Location.Y);
        }
#endif
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

    public void AddOverlay(Android.Views.View child, Func<SkUiTouchEvent, SkUiNativeGestureState> overlayTouch)
    {
        if (_overlays.ContainsKey(child)) return;
        var clip = new SkUiOverlayClip(Context!) { OverlayTouch = overlayTouch };
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
/// bounds inside, so it neither draws nor receives touches outside the ancestor scroll viewports. Like a native
/// scrolling parent it also watches the overlay's touches (<see cref="OnInterceptTouchEvent"/>): they are offered to
/// the drawn ancestors' continuous gestures, and once one claims the drag (e.g. a drawn scroller), the native control
/// gets ACTION_CANCEL and the rest of the drag goes to the drawn tree.
/// </summary>
internal sealed class SkUiOverlayClip : Android.Views.ViewGroup
{
    private static long _nextPointer = 1L << 40; // distinct from surface pointer ids
    private int _pointerId = -1;
    private long _pointer;

    public SkUiOverlayClip(Android.Content.Context context) : base(context) => SetClipChildren(true);

    /// <summary>Delivers a pointer (surface DIPs) to the drawn tree; returns the drawn state of that pointer.</summary>
    internal Func<SkUiTouchEvent, SkUiNativeGestureState>? OverlayTouch { get; set; }

    public override bool OnInterceptTouchEvent(Android.Views.MotionEvent? e)
    {
        if (e is null)
            return false;
        switch (e.ActionMasked)
        {
            case Android.Views.MotionEventActions.Down:
                _pointerId = e.GetPointerId(0);
                _pointer = ++_nextPointer;
                // Claimed at once when the press stops a drawn fling: the native control never sees it.
                return Forward(e, SkUiTouchAction.Pressed) == SkUiNativeGestureState.Claimed && TakeOver();
            case Android.Views.MotionEventActions.Move:
                return Forward(e, SkUiTouchAction.Moved) == SkUiNativeGestureState.Claimed && TakeOver();
            case Android.Views.MotionEventActions.Up:
                Forward(e, SkUiTouchAction.Released);
                _pointerId = -1;
                return false;
            case Android.Views.MotionEventActions.Cancel:
                Forward(e, SkUiTouchAction.Cancelled);
                _pointerId = -1;
                return false;
            default:
                return false;
        }
    }

    /// <summary>After <see cref="OnInterceptTouchEvent"/> took the drag over (or no native child wanted the press).</summary>
    public override bool OnTouchEvent(Android.Views.MotionEvent? e)
    {
        if (e is null || _pointerId < 0)
            return false;
        switch (e.ActionMasked)
        {
            case Android.Views.MotionEventActions.Move:
                Forward(e, SkUiTouchAction.Moved);
                break;
            case Android.Views.MotionEventActions.Up:
                Forward(e, SkUiTouchAction.Released);
                _pointerId = -1;
                break;
            case Android.Views.MotionEventActions.Cancel:
                Forward(e, SkUiTouchAction.Cancelled);
                _pointerId = -1;
                break;
        }
        return true;
    }

    private bool TakeOver()
    {
        // Keep native ancestors (e.g. a MAUI ScrollView around the surface) from intercepting the drawn drag.
        Parent?.RequestDisallowInterceptTouchEvent(true);
        return true;
    }

    private SkUiNativeGestureState Forward(Android.Views.MotionEvent e, SkUiTouchAction action)
    {
        if (OverlayTouch is not { } touch || _pointerId < 0)
            return SkUiNativeGestureState.None;
        var index = e.FindPointerIndex(_pointerId);
        if (index < 0)
            return SkUiNativeGestureState.None;
        var density = Resources?.DisplayMetrics?.Density ?? 1;
        // This view's position in the overlay container equals the surface's coordinate space.
        var position = new Point((Left + e.GetX(index)) / density, (Top + e.GetY(index)) / density);
        return touch(new SkUiTouchEvent(_pointer, action, position, TimeSpan.FromMilliseconds(e.EventTime)));
    }

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

    public void AddOverlay(UIKit.UIView child, Func<SkUiTouchEvent, SkUiNativeGestureState> overlayTouch)
    {
        if (_overlays.ContainsKey(child)) return;
        // Clip views are plain UIViews that MAUI never tracks (the KVO note above applies to MAUI-created views).
        var clip = new UIKit.UIView { ClipsToBounds = true, BackgroundColor = UIKit.UIColor.Clear };
        clip.AddSubview(child);
        clip.AddGestureRecognizer(new SkUiOverlayDragRecognizer(this, overlayTouch));
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
internal sealed partial class SkUiOverlayContainer : Microsoft.UI.Xaml.Controls.Canvas
{
    private readonly Dictionary<Microsoft.UI.Xaml.FrameworkElement, OverlayState> _overlays = [];

    private sealed class OverlayState(Microsoft.UI.Xaml.Controls.Canvas clip)
    {
        public Microsoft.UI.Xaml.Controls.Canvas Clip { get; } = clip;
        public OverlayDragWatcher Watcher { get; set; } = null!;
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

    public void AddOverlay(Microsoft.UI.Xaml.FrameworkElement child, Func<SkUiTouchEvent, SkUiNativeGestureState> overlayTouch)
    {
        if (_overlays.ContainsKey(child)) return;
        var clip = new Microsoft.UI.Xaml.Controls.Canvas();
        clip.Children.Add(child);
        var state = new OverlayState(clip);
        state.Watcher = new OverlayDragWatcher(this, clip, overlayTouch, () => ApplyVisibility(state));
        _overlays[child] = state;
        Children.Add(clip);
    }

    /// <summary>
    /// Touch / pen drags that start on an overlay are offered to the drawn ancestors' continuous gestures (as on
    /// Android / Apple): handledEventsToo handlers on the clip canvas see the native control's pointer events, and once
    /// a drawn scroller claims the drag the overlay container captures the pointer, so the native control loses it.
    /// Mouse input stays native (text selection). If the native control's own manipulation takes the contact first, the
    /// drawn side gets a cancel and the drag stays native. The capture is on the container, not the clip: the clip is
    /// collapsed as soon as the snapshot shows, and a collapsed element loses its capture (the drag stopped after a
    /// few DIPs, or never ended and left the overlays frozen).
    /// </summary>
    private sealed class OverlayDragWatcher
    {
        private static long _nextPointer = 1L << 40; // distinct from surface pointer ids
        private readonly Microsoft.UI.Xaml.UIElement _space;
        private readonly Microsoft.UI.Xaml.Controls.Canvas _clip;
        private readonly Func<SkUiTouchEvent, SkUiNativeGestureState> _touch;
        private readonly Action _contactEnded;
        private uint? _contact;

        /// <summary>A touch contact that started on this overlay is down.</summary>
        public bool HasContact => _contact is not null;
        private long _pointer;
        private bool _taken;

        public OverlayDragWatcher(Microsoft.UI.Xaml.UIElement space, Microsoft.UI.Xaml.Controls.Canvas clip, Func<SkUiTouchEvent, SkUiNativeGestureState> touch, Action contactEnded)
        {
            _contactEnded = contactEnded;
            _space = space;
            _clip = clip;
            _touch = touch;
            Add(Microsoft.UI.Xaml.UIElement.PointerPressedEvent, OnPressed);
            Add(Microsoft.UI.Xaml.UIElement.PointerMovedEvent, OnMoved);
            Add(Microsoft.UI.Xaml.UIElement.PointerReleasedEvent, (_, args) => End(args, SkUiTouchAction.Released));
            Add(Microsoft.UI.Xaml.UIElement.PointerCanceledEvent, (_, args) => { Why("PointerCanceled on clip", args); End(args, SkUiTouchAction.Cancelled); });
            Add(Microsoft.UI.Xaml.UIElement.PointerCaptureLostEvent, OnCaptureLost);
            Add(Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent, OnWheel);
            // After the takeover the container owns the pointer, and without one the finger may leave the overlay (the
            // release then lands on the drawn surface): the contact's events are also seen on the container. Events
            // that bubbled from the clip were already handled there (same args instance).
            AddToSpace(Microsoft.UI.Xaml.UIElement.PointerMovedEvent, (sender, args) => { if (!ReferenceEquals(args, _seen)) OnMoved(sender, args); });
            AddToSpace(Microsoft.UI.Xaml.UIElement.PointerReleasedEvent, (_, args) => End(args, SkUiTouchAction.Released));
            AddToSpace(Microsoft.UI.Xaml.UIElement.PointerCanceledEvent, (_, args) => { Why("PointerCanceled on container", args); End(args, SkUiTouchAction.Cancelled); });
            AddToSpace(Microsoft.UI.Xaml.UIElement.PointerCaptureLostEvent, (_, args) =>
            {
                if (_taken && ReferenceEquals(args.OriginalSource, _space))
                {
                    Why("PointerCaptureLost on container", args);
                    End(args, SkUiTouchAction.Cancelled);
                }
            });
        }

        private void Add(Microsoft.UI.Xaml.RoutedEvent routedEvent, Microsoft.UI.Xaml.Input.PointerEventHandler handler) =>
            _clip.AddHandler(routedEvent, handler, handledEventsToo: true);

        private readonly List<(Microsoft.UI.Xaml.Controls.ScrollViewer Viewer, Microsoft.UI.Xaml.Controls.ScrollMode Horizontal, Microsoft.UI.Xaml.Controls.ScrollMode Vertical)> _suspended = [];

        /// <summary>
        /// The native control's own ScrollViewers (e.g. a TextBox's text scroller) would start DirectManipulation a few
        /// DIPs later and take the contact from the container: switch their panning off for the rest of the drag.
        /// </summary>
        private void SuspendNativePanning()
        {
            foreach (var viewer in Descendants(_clip).OfType<Microsoft.UI.Xaml.Controls.ScrollViewer>())
            {
                _suspended.Add((viewer, viewer.HorizontalScrollMode, viewer.VerticalScrollMode));
                viewer.HorizontalScrollMode = Microsoft.UI.Xaml.Controls.ScrollMode.Disabled;
                viewer.VerticalScrollMode = Microsoft.UI.Xaml.Controls.ScrollMode.Disabled;
                (viewer.Content as Microsoft.UI.Xaml.UIElement)?.CancelDirectManipulations();
            }
        }

        private void RestoreNativePanning()
        {
            foreach (var (viewer, horizontal, vertical) in _suspended)
            {
                viewer.HorizontalScrollMode = horizontal;
                viewer.VerticalScrollMode = vertical;
            }
            _suspended.Clear();
        }

        private static IEnumerable<Microsoft.UI.Xaml.DependencyObject> Descendants(Microsoft.UI.Xaml.DependencyObject root)
        {
            var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
            for (var index = 0; index < count; index++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, index);
                yield return child;
                foreach (var descendant in Descendants(child))
                    yield return descendant;
            }
        }

        private void Why(string what, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            if (SkUiDiagnostics.TraceOn && args.Pointer.PointerId == _contact)
                SkUiDiagnostics.Write($"overlay drag {_pointer}: {what} (source {args.OriginalSource?.GetType().Name}, taken={_taken})");
        }

        private void AddToSpace(Microsoft.UI.Xaml.RoutedEvent routedEvent, Microsoft.UI.Xaml.Input.PointerEventHandler handler) =>
            _space.AddHandler(routedEvent, handler, handledEventsToo: true);

        private void OnPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            if (_contact is not null || args.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
                return;
            _contact = args.Pointer.PointerId;
            _pointer = ++_nextPointer;
            _taken = false;
            WatchWindow(true);
            // Claimed at once when the press stops a drawn fling: the native control never keeps it.
            if (Forward(args, SkUiTouchAction.Pressed) == SkUiNativeGestureState.Claimed)
                TakeOver(args);
        }

        private Microsoft.UI.Xaml.UIElement? _window;
        private Microsoft.UI.Xaml.Input.PointerEventHandler? _windowReleased;
        private Microsoft.UI.Xaml.Input.PointerEventHandler? _windowCanceled;

        /// <summary>
        /// Without a capture (the native control keeps the touch until a drawn gesture claims it) the finger may be
        /// lifted anywhere in the window, e.g. over native controls below the drawn surface: watch the window's root
        /// while the contact is down, or the drawn tree never gets the release and the scroll gesture keeps tracking
        /// that pointer (every later drag was ignored).
        /// </summary>
        private void WatchWindow(bool watch)
        {
            if (watch)
            {
                if (_window is not null || _space.XamlRoot?.Content is not Microsoft.UI.Xaml.UIElement root)
                    return;
                _window = root;
                _windowReleased ??= (_, args) => End(args, SkUiTouchAction.Released);
                _windowCanceled ??= (_, args) => { Why("PointerCanceled in window", args); End(args, SkUiTouchAction.Cancelled); };
                root.AddHandler(Microsoft.UI.Xaml.UIElement.PointerReleasedEvent, _windowReleased, handledEventsToo: true);
                root.AddHandler(Microsoft.UI.Xaml.UIElement.PointerCanceledEvent, _windowCanceled, handledEventsToo: true);
            }
            else if (_window is { } root)
            {
                root.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerReleasedEvent, _windowReleased);
                root.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerCanceledEvent, _windowCanceled);
                _window = null;
            }
        }

        /// <summary>The last event forwarded from the clip (it bubbles on to the container).</summary>
        private Microsoft.UI.Xaml.Input.PointerRoutedEventArgs? _seen;

        /// <summary>
        /// Wheel / touchpad scrolling over an overlay reaches the native control only: when it did not use it, the
        /// drawn scroller underneath scrolls (a native ScrollViewer also scrolls when the wheel is over a TextBox).
        /// </summary>
        private void OnWheel(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            if (args.Handled)
                return;
            var point = args.GetCurrentPoint(_space);
            if (point.Properties.IsHorizontalMouseWheel)
                return;
            _touch(new SkUiTouchEvent(++_nextPointer, SkUiTouchAction.Wheel, new Point(point.Position.X, point.Position.Y),
                null, point.Properties.MouseWheelDelta));
            args.Handled = true;
        }

        private void OnMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            if (args.Pointer.PointerId != _contact)
                return;
            _seen = args;
            if (Forward(args, SkUiTouchAction.Moved) == SkUiNativeGestureState.Claimed && !_taken)
                TakeOver(args);
            if (_taken)
                args.Handled = true;
        }

        private void OnCaptureLost(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            // The native child losing capture to the container (our takeover) bubbles here; after the takeover only
            // the container's own capture loss (handled on the container) ends the drawn gesture.
            if (_taken)
                return;
            Why("PointerCaptureLost on clip", args);
            End(args, SkUiTouchAction.Cancelled);
        }

        private void End(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args, SkUiTouchAction action)
        {
            if (args.Pointer.PointerId != _contact)
                return;
            Forward(args, action);
            var taken = _taken;
            _contact = null;
            _taken = false;
            WatchWindow(false);
            _contactEnded(); // apply a hide deferred while the contact was down
            if (taken)
            {
                args.Handled = true;
                _space.ReleasePointerCapture(args.Pointer);
                RestoreNativePanning();
            }
        }

        private void TakeOver(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            _taken = _space.CapturePointer(args.Pointer);
            if (_taken)
                SuspendNativePanning();
            if (SkUiDiagnostics.TraceOn)
                SkUiDiagnostics.Write($"overlay drag {_pointer} claimed by the drawn tree -> container capture {(_taken ? "ok" : "FAILED")}");
            if (_taken)
                args.Handled = true;
        }

        private SkUiNativeGestureState Forward(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args, SkUiTouchAction action)
        {
            if (SkUiDiagnostics.TraceOn && action != SkUiTouchAction.Moved)
                SkUiDiagnostics.Write($"overlay touch {action} contact={args.Pointer.PointerId} pointer={_pointer} taken={_taken}");
            // The overlay container's space is the surface's DIP space (the surface is its first child at 0, 0).
            var point = args.GetCurrentPoint(_space);
            return _touch(new SkUiTouchEvent(_pointer, action, new Point(point.Position.X, point.Position.Y),
                TimeSpan.FromTicks((long)point.Timestamp * 10)));
        }
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
        ApplyVisibility(overlay);
    }

    /// <summary>
    /// Hiding (Collapsed) or moving the element under an active touch makes WinUI drop that contact, which cancelled a
    /// drag that started on the overlay as soon as its snapshot showed. So the overlay a drag started on stays live
    /// until that drag ends (like a focused control); the pending hide is applied then. Fully clipped overlays are
    /// collapsed.
    /// </summary>
    private static void ApplyVisibility(OverlayState overlay)
    {
        var hidden = overlay.Hidden && !overlay.Watcher.HasContact;
        overlay.Clip.Visibility = hidden || overlay.Empty ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    }

    public void SetOverlayBounds(Microsoft.UI.Xaml.FrameworkElement child, Rect bounds, Rect clip)
    {
        if (!_overlays.TryGetValue(child, out var overlay)) return;
        var visible = bounds.Intersect(clip);
        overlay.Empty = visible.Width <= 0 || visible.Height <= 0;
        ApplyVisibility(overlay);
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