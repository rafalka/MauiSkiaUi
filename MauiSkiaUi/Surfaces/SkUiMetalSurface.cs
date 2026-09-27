#if IOS || MACCATALYST
using System.Diagnostics;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using Metal;
using ObjCRuntime;
using SkiaSharp;
using UIKit;

namespace MauiSkiaUi.Surfaces;

/// <summary>
/// A <see cref="CAMetalLayer"/>-backed view rendered by <see cref="SkUiMetalRenderLoop"/> on a dedicated render
/// thread. Replaces SkiaSharp's GLKView-based <c>SKGLView</c> (deprecated OpenGL ES) on iOS and Mac Catalyst.
/// Touches are delivered on the UI thread in points (= MAUI DIPs).
/// </summary>
internal sealed class SkUiMetalView : UIView
{
    private readonly SkUiMetalSurface _surface;
    private readonly Dictionary<IntPtr, long> _touchIds = [];
    private long _nextTouchId;

    [Export("layerClass")]
    public static Class LayerClass() => new(typeof(CAMetalLayer));

    public SkUiMetalView()
    {
        var layer = (CAMetalLayer)Layer;
        layer.Device = SkUiMetalRenderLoop.Device;
        layer.PixelFormat = MTLPixelFormat.BGRA8Unorm;
        layer.FramebufferOnly = false;
        layer.Opaque = false;
        BackgroundColor = UIColor.Clear;
        Opaque = false;
        MultipleTouchEnabled = true;
        ContentMode = UIViewContentMode.TopLeft;
        _surface = new SkUiMetalSurface(layer);
        var scroll = new UIPanGestureRecognizer(OnScrollGesture)
        {
            AllowedScrollTypesMask = UIScrollTypeMask.All,
            AllowedTouchTypes = [],
            CancelsTouchesInView = false
        };
        AddGestureRecognizer(scroll);
        _gate = new SkUiNativeGestureGate(this);
        AddGestureRecognizer(_gate);
    }

    private readonly SkUiNativeGestureGate _gate;

    /// <summary>Whether drawn gestures want the touch; ancestor pan / pinch recognizers wait for this surface's gate.</summary>
    internal Func<SkUiNativeGestureState>? NativeGestureState { get; set; }

    /// <summary>Surface registration with the render loop.</summary>
    internal SkUiMetalSurface Surface => _surface;

    /// <summary>UI thread: raw touches in DIPs; return value is ignored on Apple (UIKit keeps delivering).</summary>
    internal Func<SkUiTouchEvent, bool>? TouchHandler { get; set; }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var scale = Window?.Screen.Scale ?? UIScreen.MainScreen.Scale;
        var layer = (CAMetalLayer)Layer;
        layer.ContentsScale = scale;
        var size = new CGSize(Math.Max(1, Math.Round(Bounds.Width * scale)), Math.Max(1, Math.Round(Bounds.Height * scale)));
        if (layer.DrawableSize != size)
        {
            layer.DrawableSize = size;
            _surface.RequestRender();
        }
    }

    public override void MovedToWindow()
    {
        base.MovedToWindow();
        _surface.IsAttached = Window is not null;
        if (Window is not null)
        {
            SetNeedsLayout();
            _surface.RequestRender();
        }
    }

    public override void TouchesBegan(NSSet touches, UIEvent? evt) => Deliver(touches, SkUiTouchAction.Pressed);
    public override void TouchesMoved(NSSet touches, UIEvent? evt) => Deliver(touches, SkUiTouchAction.Moved);
    public override void TouchesEnded(NSSet touches, UIEvent? evt) => Deliver(touches, SkUiTouchAction.Released);
    public override void TouchesCancelled(NSSet touches, UIEvent? evt) => Deliver(touches, SkUiTouchAction.Cancelled);

    private void Deliver(NSSet touches, SkUiTouchAction action)
    {
        foreach (var item in touches)
        {
            if (item is not UITouch touch)
                continue;
            var key = touch.Handle.Handle;
            if (action == SkUiTouchAction.Pressed || !_touchIds.TryGetValue(key, out var id))
                _touchIds[key] = id = ++_nextTouchId;
            if (action is SkUiTouchAction.Released or SkUiTouchAction.Cancelled)
                _touchIds.Remove(key);
            var location = touch.LocationInView(this);
            TouchHandler?.Invoke(new SkUiTouchEvent(id, action, new Point(location.X, location.Y), TimeSpan.FromSeconds(touch.Timestamp)));
        }
        _gate.Sync(NativeGestureState?.Invoke() ?? SkUiNativeGestureState.None, ended: _touchIds.Count == 0);
    }

    private void OnScrollGesture(UIPanGestureRecognizer recognizer)
    {
        var translation = recognizer.TranslationInView(this);
        recognizer.SetTranslation(CGPoint.Empty, this);
        var location = recognizer.LocationInView(this);
        if (translation.Y != 0)
            TouchHandler?.Invoke(new SkUiTouchEvent(0, SkUiTouchAction.Wheel, new Point(location.X, location.Y), null, translation.Y));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _surface.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// Coordinates drawn gestures with native ancestors (e.g. a MAUI ScrollView around the surface): ancestor pan /
/// pinch / swipe recognizers must wait for this gate, which begins when a drawn continuous gesture claims the touch
/// and fails as soon as none can (so the native scroll starts without delay when nothing drawn competes).
/// </summary>
internal sealed class SkUiNativeGestureGate : UIGestureRecognizer
{
    public SkUiNativeGestureGate(UIView owner)
    {
        CancelsTouchesInView = false;
        DelaysTouchesBegan = false;
        DelaysTouchesEnded = false;
        Delegate = new GateDelegate(owner);
    }

    /// <summary>Called after the owner delivered a touch batch to the drawn tree.</summary>
    public void Sync(SkUiNativeGestureState state, bool ended)
    {
        switch (State)
        {
            case UIGestureRecognizerState.Possible:
                if (state == SkUiNativeGestureState.Claimed && !ended)
                    State = UIGestureRecognizerState.Began;
                else if (state == SkUiNativeGestureState.None || ended)
                    State = UIGestureRecognizerState.Failed;
                break;
            case UIGestureRecognizerState.Began:
            case UIGestureRecognizerState.Changed:
                State = ended ? UIGestureRecognizerState.Ended : UIGestureRecognizerState.Changed;
                break;
        }
    }

    public override void TouchesCancelled(NSSet touches, UIEvent evt)
    {
        base.TouchesCancelled(touches, evt);
        State = State is UIGestureRecognizerState.Began or UIGestureRecognizerState.Changed
            ? UIGestureRecognizerState.Cancelled
            : UIGestureRecognizerState.Failed;
    }

    private sealed class GateDelegate(UIView owner) : UIGestureRecognizerDelegate
    {
        public override bool ShouldBeRequiredToFailBy(UIGestureRecognizer gestureRecognizer, UIGestureRecognizer otherGestureRecognizer) =>
            otherGestureRecognizer.View is { } view && !ReferenceEquals(view, owner) && owner.IsDescendantOfView(view)
            && otherGestureRecognizer is UIPanGestureRecognizer or UIPinchGestureRecognizer or UISwipeGestureRecognizer or UIRotationGestureRecognizer;

        public override bool ShouldRecognizeSimultaneously(UIGestureRecognizer gestureRecognizer, UIGestureRecognizer otherGestureRecognizer) => true;
    }
}

/// <summary>Per-view render state shared between the UI thread and <see cref="SkUiMetalRenderLoop"/>.</summary>
internal sealed class SkUiMetalSurface : IDisposable
{
    private readonly CAMetalLayer _layer;
    private int _needsFrame = 1;
    private volatile SkUiFrameRenderer? _renderer;
    private volatile bool _continuous;
    private volatile bool _disposed;

    internal SkUiMetalSurface(CAMetalLayer layer) => _layer = layer;

    internal volatile bool IsAttached;

    /// <summary>Renderer that composites into this surface; set by the handler after connection.</summary>
    internal SkUiFrameRenderer? Renderer
    {
        get => _renderer;
        set
        {
            _renderer = value;
            if (value is null)
                SkUiMetalRenderLoop.Unregister(this);
            else
                SkUiMetalRenderLoop.Register(this);
            RequestRender();
        }
    }

    /// <summary>Any thread: asks the render loop for a frame.</summary>
    internal void RequestRender()
    {
        Interlocked.Exchange(ref _needsFrame, 1);
        SkUiMetalRenderLoop.Wake();
    }

    /// <summary>Render thread: whether this surface wants a frame now.</summary>
    internal bool WantsFrame => !_disposed && IsAttached && _renderer is { } renderer
        // Nothing to draw before the first commit (the layer starts transparent).
        && (renderer.Compositor.HasContent || renderer.Compositor.NeedsFrame)
        && (_continuous || Volatile.Read(ref _needsFrame) != 0);

    /// <summary>
    /// Render thread: draws one frame. Returns <c>true</c> when it keeps animating; <paramref name="presented"/>
    /// tells whether a drawable was actually presented.
    /// </summary>
    internal bool RenderFrame(GRContext context, IMTLCommandQueue queue, TimeSpan now, out bool presented)
    {
        presented = false;
        if (_renderer is not { } renderer || _disposed)
            return false;
        Interlocked.Exchange(ref _needsFrame, 0);
        using var drawable = _layer.NextDrawable();
        if (drawable is null)
        {
            // No drawable yet (e.g. zero-size layer): retry next vsync.
            Interlocked.Exchange(ref _needsFrame, 1);
            return false;
        }
        var texture = drawable.Texture;
        var width = (int)texture.Width;
        var height = (int)texture.Height;
        using var target = new GRBackendRenderTarget(width, height, new GRMtlTextureInfo(texture));
        using var surface = SKSurface.Create(context, target, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888);
        if (surface is null)
            return false;
        var continuous = renderer.Render(surface.Canvas, new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul), now);
        surface.Flush();
        context.Flush();
        var commands = queue.CommandBuffer();
        if (commands is not null)
        {
            commands.PresentDrawable(drawable);
            commands.Commit();
        }
        renderer.CompleteFrame();
        presented = true;
        _continuous = continuous;
        return continuous;
    }

    public void Dispose()
    {
        _disposed = true;
        SkUiMetalRenderLoop.Unregister(this);
    }
}

/// <summary>
/// Process-wide Apple render thread: one Metal device, command queue and <see cref="GRContext"/>, driven by a
/// <see cref="CADisplayLink"/> on its own run loop. It is paused while no surface needs frames, so an idle UI
/// costs nothing, and it keeps presenting while the main thread is busy or a UIScrollView is tracking.
/// </summary>
internal static class SkUiMetalRenderLoop
{
    private static readonly object Gate = new();
    private static readonly List<SkUiMetalSurface> Surfaces = [];
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly Lazy<IMTLDevice> LazyDevice = new(() =>
        MTLDevice.SystemDefault ?? throw new PlatformNotSupportedException("Metal is not available on this device."));
    private static SkUiMetalSurface[] _snapshot = [];
    private static Thread? _thread;
    private static NSThread? _nativeThread;
    private static RenderLoopTarget? _target;
    private static CADisplayLink? _link;
    private static IMTLCommandQueue? _queue;
    private static GRContext? _context;
    private static int _idleTicks;
    private static TimeSpan _lastPresent = TimeSpan.MinValue / 2;
    private static int _wakeQueued;
    private static volatile bool _suspended;
    private static NSObject? _backgroundObserver;
    private static NSObject? _foregroundObserver;

    internal static IMTLDevice Device => LazyDevice.Value;

    internal static void Register(SkUiMetalSurface surface)
    {
        lock (Gate)
        {
            if (!Surfaces.Contains(surface))
                Surfaces.Add(surface);
            _snapshot = [.. Surfaces];
            EnsureThread();
        }
        Wake();
    }

    internal static void Unregister(SkUiMetalSurface surface)
    {
        lock (Gate)
        {
            if (Surfaces.Remove(surface))
                _snapshot = [.. Surfaces];
        }
    }

    /// <summary>Any thread: resumes the display link if it was paused.</summary>
    internal static void Wake()
    {
        if (_target is not { } target || _nativeThread is not { } thread)
            return;
        if (Interlocked.Exchange(ref _wakeQueued, 1) == 1)
            return;
        target.PerformSelector(new Selector("wake"), thread, null, false);
    }

    private static void EnsureThread()
    {
        if (_thread is not null)
            return;
        var started = new ManualResetEventSlim();
        _thread = new Thread(() => Run(started)) { IsBackground = true, Name = "SkiaUi Metal render", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
        started.Wait();
        _backgroundObserver = UIApplication.Notifications.ObserveDidEnterBackground((_, _) => _suspended = true);
        _foregroundObserver = UIApplication.Notifications.ObserveWillEnterForeground((_, _) =>
        {
            _suspended = false;
            foreach (var surface in _snapshot)
                surface.RequestRender();
        });
    }

    private static void Run(ManualResetEventSlim started)
    {
        _nativeThread = NSThread.Current;
        _target = new RenderLoopTarget();
        _queue = Device.CreateCommandQueue();
        _link = CADisplayLink.Create(OnTick);
        _link.AddToRunLoop(NSRunLoop.Current, NSRunLoopMode.Common);
        started.Set();
        while (true)
        {
            using var pool = new NSAutoreleasePool();
            NSRunLoop.Current.RunUntil(NSDate.FromTimeIntervalSinceNow(1));
        }
    }

    private static void OnTick()
    {
        using var pool = new NSAutoreleasePool();
        var busy = RenderSurfaces();
        // Pause after a short idle period; Wake() resumes it from any thread.
        _idleTicks = busy ? 0 : _idleTicks + 1;
        if (_idleTicks > 2 && _link is { } link)
            link.Paused = true;
    }

    /// <summary>Render thread: renders every surface that wants a frame; <c>true</c> when any did.</summary>
    private static bool RenderSurfaces()
    {
        var busy = false;
        if (!_suspended && _queue is { } queue)
        {
            var now = Clock.Elapsed;
            foreach (var surface in _snapshot)
            {
                if (!surface.WantsFrame)
                    continue;
                _context ??= GRContext.CreateMetal(new GRMtlBackendContext { Device = Device, Queue = queue });
                try
                {
                    surface.RenderFrame(_context, queue, now, out var presented);
                    if (presented)
                        _lastPresent = now;
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"SkiaUi Metal render failed: {exception}");
                }
                busy = true;
            }
        }
        return busy;
    }

    private sealed class RenderLoopTarget : NSObject
    {
        [Export("wake")]
        public void WakeOnRenderThread()
        {
            Interlocked.Exchange(ref _wakeQueued, 0);
            _idleTicks = 0;
            if (_link is { } link)
                link.Paused = false;
            // Render on commit when idle: waiting for the next display-link tick would add up to a full refresh
            // period of latency to every UI-driven change. While frames are flowing (animations) the tick paces them.
            var period = _link is { Duration: > 0 } running ? TimeSpan.FromSeconds(running.Duration) : TimeSpan.FromMilliseconds(16.7);
            if (Clock.Elapsed - _lastPresent >= period / 2)
            {
                using var pool = new NSAutoreleasePool();
                RenderSurfaces();
            }
        }
    }
}
#endif
