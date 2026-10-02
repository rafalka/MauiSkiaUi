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
        _gate = new SkUiNativeGestureGate();
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
    private int _activeTouches;
    private bool _synced;
    private CGPoint _start;

    public SkUiNativeGestureGate()
    {
        CancelsTouchesInView = false;
        DelaysTouchesBegan = false;
        DelaysTouchesEnded = false;
        Delegate = new GateDelegate();
    }

    /// <summary>Called after the owner delivered a touch batch to the drawn tree.</summary>
    public void Sync(SkUiNativeGestureState state, bool ended)
    {
        _synced = true;
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

    // The gate must also resolve on its own: a touch the drawn view never receives would otherwise leave it
    // "Possible" forever, with every ancestor pan waiting — the native page would stay frozen. That happens when the
    // touch lands while an ancestor scroll view is still moving (the tap that stops a deceleration is consumed by the
    // scroll view). A normal touch is only *delayed* (UIScrollView delaysContentTouches, ~150 ms), so "moved before
    // the drawn view saw it" must not fail the gate early — only after the delay could have elapsed.
    private const double DeliveryDelaySeconds = 0.3;
    private double _startTime;

    public override void TouchesBegan(NSSet touches, UIEvent evt)
    {
        base.TouchesBegan(touches, evt);
        // _synced is cleared in Reset (after each sequence), not here: a software surface's own touch recognizer may
        // deliver the press to the drawn tree before this recognizer sees it.
        if (_activeTouches == 0 && touches.AnyObject is UITouch touch && View is { } view)
        {
            _start = touch.LocationInView(view);
            _startTime = touch.Timestamp;
            if (State == UIGestureRecognizerState.Possible && AncestorScrollViewIsMoving(view))
                State = UIGestureRecognizerState.Failed;
        }
        _activeTouches += (int)touches.Count;
    }

    public override void TouchesMoved(NSSet touches, UIEvent evt)
    {
        base.TouchesMoved(touches, evt);
        if (State != UIGestureRecognizerState.Possible || _synced || touches.AnyObject is not UITouch touch || View is not { } view)
            return;
        if (touch.Timestamp - _startTime < DeliveryDelaySeconds)
            return; // the drawn view may still receive the (delayed) touch
        var location = touch.LocationInView(view);
        var dx = location.X - _start.X;
        var dy = location.Y - _start.Y;
        var slop = SkUiGestureSettings.TouchSlop;
        if (dx * dx + dy * dy > slop * slop)
            State = UIGestureRecognizerState.Failed;
    }

    private static bool AncestorScrollViewIsMoving(UIView view)
    {
        for (var ancestor = view.Superview; ancestor is not null; ancestor = ancestor.Superview)
            if (ancestor is UIScrollView { Decelerating: true } or UIScrollView { Dragging: true })
                return true;
        return false;
    }

    public override void TouchesEnded(NSSet touches, UIEvent evt)
    {
        base.TouchesEnded(touches, evt);
        Finish(touches, cancelled: false);
    }

    public override void TouchesCancelled(NSSet touches, UIEvent evt)
    {
        base.TouchesCancelled(touches, evt);
        Finish(touches, cancelled: true);
    }

    private void Finish(NSSet touches, bool cancelled)
    {
        _activeTouches = Math.Max(0, _activeTouches - (int)touches.Count);
        if (_activeTouches > 0 && !cancelled)
            return;
        State = State switch
        {
            UIGestureRecognizerState.Began or UIGestureRecognizerState.Changed =>
                cancelled ? UIGestureRecognizerState.Cancelled : UIGestureRecognizerState.Ended,
            UIGestureRecognizerState.Possible => UIGestureRecognizerState.Failed,
            _ => State
        };
    }

    public override void Reset()
    {
        base.Reset();
        _activeTouches = 0;
        _synced = false;
    }

    // Reads the owner from the recognizer: holding it would form a managed cycle (view → gate → delegate → view) that
    // the view's native retain of the gate keeps rooted, so the view and everything reachable from it would leak.
    private sealed class GateDelegate : UIGestureRecognizerDelegate
    {
        public override bool ShouldBeRequiredToFailBy(UIGestureRecognizer gestureRecognizer, UIGestureRecognizer otherGestureRecognizer) =>
            gestureRecognizer.View is { } owner && otherGestureRecognizer.View is { } view && !ReferenceEquals(view, owner) && owner.IsDescendantOfView(view)
            && otherGestureRecognizer is UIPanGestureRecognizer or UIPinchGestureRecognizer or UISwipeGestureRecognizer or UIRotationGestureRecognizer;

        public override bool ShouldRecognizeSimultaneously(UIGestureRecognizer gestureRecognizer, UIGestureRecognizer otherGestureRecognizer) => true;
    }
}

/// <summary>
/// Touch delivery for software (SKCanvasView) surfaces: feeds touches (DIPs) to the drawn tree and then updates the
/// surface's <see cref="SkUiNativeGestureGate"/>, in that order (a second, unrelated recognizer — SkiaSharp's own —
/// gives no ordering guarantee relative to the gate). It never changes state and cannot be prevented, so delivery
/// continues whatever the gate decides; when a native ancestor scroll view starts dragging, the drawn gesture is
/// cancelled (a content view would get touchesCancelled from the scroll view; a recognizer does not).
/// </summary>
internal sealed class SkUiTouchDeliverer : UIGestureRecognizer
{
    private readonly SkUiNativeGestureGate _gate;
    private readonly Dictionary<IntPtr, long> _touchIds = [];
    private readonly HashSet<IntPtr> _cancelled = [];
    private long _nextTouchId;

    public SkUiTouchDeliverer(SkUiNativeGestureGate gate)
    {
        _gate = gate;
        CancelsTouchesInView = false;
        DelaysTouchesBegan = false;
        DelaysTouchesEnded = false;
        Delegate = new SimultaneousDelegate();
    }

    /// <summary>Raw touches in DIPs; the result is ignored (UIKit keeps delivering).</summary>
    internal Func<SkUiTouchEvent, bool>? TouchHandler { get; set; }

    /// <summary>Drawn-gesture state for the gate.</summary>
    internal Func<SkUiNativeGestureState>? NativeGestureState { get; set; }

    public override bool CanBePreventedByGestureRecognizer(UIGestureRecognizer preventingGestureRecognizer) => false;

    public override bool CanPreventGestureRecognizer(UIGestureRecognizer preventedGestureRecognizer) => false;

    public override void TouchesBegan(NSSet touches, UIEvent evt)
    {
        base.TouchesBegan(touches, evt);
        Deliver(touches, SkUiTouchAction.Pressed);
    }

    public override void TouchesMoved(NSSet touches, UIEvent evt)
    {
        base.TouchesMoved(touches, evt);
        if (View is { } view && AncestorScrollViewIsDragging(view))
        {
            CancelAll(); // the native scroll view owns this touch now
            return;
        }
        Deliver(touches, SkUiTouchAction.Moved);
    }

    public override void TouchesEnded(NSSet touches, UIEvent evt)
    {
        base.TouchesEnded(touches, evt);
        Deliver(touches, SkUiTouchAction.Released);
    }

    public override void TouchesCancelled(NSSet touches, UIEvent evt)
    {
        base.TouchesCancelled(touches, evt);
        Deliver(touches, SkUiTouchAction.Cancelled);
    }

    private void Deliver(NSSet touches, SkUiTouchAction action)
    {
        if (TouchHandler is not { } handler || View is not { } view)
            return;
        foreach (var item in touches)
        {
            if (item is not UITouch touch)
                continue;
            var key = touch.Handle.Handle;
            if (action == SkUiTouchAction.Pressed)
            {
                _cancelled.Remove(key);
                _touchIds[key] = ++_nextTouchId;
            }
            if (_cancelled.Contains(key))
            {
                if (action is SkUiTouchAction.Released or SkUiTouchAction.Cancelled)
                    _cancelled.Remove(key);
                continue;
            }
            if (!_touchIds.TryGetValue(key, out var id))
                continue;
            if (action is SkUiTouchAction.Released or SkUiTouchAction.Cancelled)
                _touchIds.Remove(key);
            var location = touch.LocationInView(view);
            handler(new SkUiTouchEvent(id, action, new Point(location.X, location.Y), TimeSpan.FromSeconds(touch.Timestamp)));
        }
        _gate.Sync(NativeGestureState?.Invoke() ?? SkUiNativeGestureState.None, ended: _touchIds.Count == 0);
    }

    private void CancelAll()
    {
        if (TouchHandler is { } handler)
            foreach (var (key, id) in _touchIds)
            {
                handler(new SkUiTouchEvent(id, SkUiTouchAction.Cancelled, Point.Zero));
                _cancelled.Add(key);
            }
        _touchIds.Clear();
    }

    private static bool AncestorScrollViewIsDragging(UIView view)
    {
        for (var ancestor = view.Superview; ancestor is not null; ancestor = ancestor.Superview)
            if (ancestor is UIScrollView { Dragging: true })
                return true;
        return false;
    }

    private sealed class SimultaneousDelegate : UIGestureRecognizerDelegate
    {
        public override bool ShouldRecognizeSimultaneously(UIGestureRecognizer gestureRecognizer, UIGestureRecognizer otherGestureRecognizer) => true;
    }
}

/// <summary>
/// Watches touches that start on a native overlay (on its clip wrapper) and offers them to the continuous gestures of
/// the overlay's drawn ancestors. While nothing drawn has claimed, it stays <i>Possible</i> and the native control works
/// as usual (taps, text selection); when a drawn gesture claims (e.g. a drawn scroller), it begins, which cancels the
/// native view's touches, and the rest of the drag goes to the drawn tree. It fails as soon as nothing drawn can claim.
/// Controls that scroll their own content (WebView, a long Editor) keep precedence: their own pan begins first.
/// </summary>
internal sealed class SkUiOverlayDragRecognizer : UIGestureRecognizer
{
    private static long _nextPointer = 1L << 40; // distinct from surface pointer ids
    private readonly UIView _space;
    private readonly Func<SkUiTouchEvent, SkUiNativeGestureState> _overlayTouch;
    private UITouch? _touch;
    private long _pointer;

    /// <param name="space">View whose coordinates are the surface's (the overlay container).</param>
    /// <param name="overlayTouch">Delivers the pointer (surface DIPs) to the drawn tree and returns its drawn state.</param>
    public SkUiOverlayDragRecognizer(UIView space, Func<SkUiTouchEvent, SkUiNativeGestureState> overlayTouch)
    {
        _space = space;
        _overlayTouch = overlayTouch;
        CancelsTouchesInView = true;
        DelaysTouchesBegan = false;
        DelaysTouchesEnded = false;
    }

    public override void TouchesBegan(NSSet touches, UIEvent evt)
    {
        base.TouchesBegan(touches, evt);
        if (_touch is not null || touches.AnyObject is not UITouch touch)
            return;
        _touch = touch;
        _pointer = ++_nextPointer;
        Update(Forward(SkUiTouchAction.Pressed), ended: false);
    }

    public override void TouchesMoved(NSSet touches, UIEvent evt)
    {
        base.TouchesMoved(touches, evt);
        if (_touch is not null && touches.Contains(_touch))
            Update(Forward(SkUiTouchAction.Moved), ended: false);
    }

    public override void TouchesEnded(NSSet touches, UIEvent evt)
    {
        base.TouchesEnded(touches, evt);
        if (_touch is null || !touches.Contains(_touch))
            return;
        Forward(SkUiTouchAction.Released);
        _touch = null;
        Update(SkUiNativeGestureState.None, ended: true);
    }

    public override void TouchesCancelled(NSSet touches, UIEvent evt)
    {
        base.TouchesCancelled(touches, evt);
        if (_touch is null || !touches.Contains(_touch))
            return;
        Forward(SkUiTouchAction.Cancelled);
        _touch = null;
        State = State is UIGestureRecognizerState.Began or UIGestureRecognizerState.Changed
            ? UIGestureRecognizerState.Cancelled
            : UIGestureRecognizerState.Failed;
    }

    public override void Reset()
    {
        base.Reset();
        // Failed / prevented (e.g. a WebView's own pan began): end the drawn pointer too.
        if (_touch is not null)
        {
            _overlayTouch(new SkUiTouchEvent(_pointer, SkUiTouchAction.Cancelled, Point.Zero));
            _touch = null;
        }
    }

    private void Update(SkUiNativeGestureState state, bool ended)
    {
        switch (State)
        {
            case UIGestureRecognizerState.Possible:
                if (ended || state == SkUiNativeGestureState.None)
                    State = UIGestureRecognizerState.Failed;
                else if (state == SkUiNativeGestureState.Claimed)
                    State = UIGestureRecognizerState.Began; // cancels the native view's touches
                break;
            case UIGestureRecognizerState.Began:
            case UIGestureRecognizerState.Changed:
                State = ended ? UIGestureRecognizerState.Ended : UIGestureRecognizerState.Changed;
                break;
        }
    }

    private SkUiNativeGestureState Forward(SkUiTouchAction action)
    {
        if (_touch is not { } touch)
            return SkUiNativeGestureState.None;
        var location = touch.LocationInView(_space);
        return _overlayTouch(new SkUiTouchEvent(_pointer, action, new Point(location.X, location.Y), TimeSpan.FromSeconds(touch.Timestamp)));
    }
}

/// <summary>Per-view render state shared between the UI thread and <see cref="SkUiMetalRenderLoop"/>.</summary>
internal sealed class SkUiMetalSurface : IDisposable
{
    private readonly CAMetalLayer _layer;
    // Held by the render thread from render to present; taken by the UI thread when detaching, so once the renderer
    // is cleared (before the view leaves its superview) no frame is still presenting into it.
    private readonly object _frameLock = new();
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
            if (value is null)
            {
                lock (_frameLock)
                    _renderer = null;
                SkUiMetalRenderLoop.Unregister(this);
            }
            else
            {
                _renderer = value;
                SkUiMetalRenderLoop.Register(this);
            }
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
        if (_renderer is null || _disposed)
            return false;
        Interlocked.Exchange(ref _needsFrame, 0);
        // Acquired outside the frame lock: it can block for a vsync, and detaching must not wait for that.
        using var drawable = _layer.NextDrawable();
        if (drawable is null)
        {
            // No drawable yet (e.g. zero-size layer): retry next vsync.
            Interlocked.Exchange(ref _needsFrame, 1);
            return false;
        }
        lock (_frameLock)
        {
            if (_renderer is not { } renderer || _disposed)
                return false;
            var texture = drawable.Texture;
            var width = (int)texture.Width;
            var height = (int)texture.Height;
            using var target = new GRBackendRenderTarget(width, height, new GRMtlTextureInfo(texture));
            using var surface = SKSurface.Create(context, target, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888);
            if (surface is null)
            {
                // Transient (e.g. drawable resized mid-frame): keep the request for the next vsync.
                Interlocked.Exchange(ref _needsFrame, 1);
                return false;
            }
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
    }

    public void Dispose()
    {
        lock (_frameLock)
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
