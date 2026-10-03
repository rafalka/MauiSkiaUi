#if ANDROID
using Android.Content;
using Android.Views;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Android;

namespace MauiSkiaUi.Surfaces;

/// <summary>
/// GPU surface for Android: a translucent <see cref="GLTextureView"/> whose own GL thread composites the
/// retained tree (render-on-demand, continuous only while render-thread animations run). The UI thread only
/// records and commits; it never blocks on GL. Touches are delivered on the UI thread in DIPs.
/// </summary>
internal sealed class SkUiGlTextureView : GLTextureView
{
    private readonly Renderer _renderer;
    private readonly float _density;

    public SkUiGlTextureView(Context context) : base(context)
    {
        _density = context.Resources?.DisplayMetrics?.Density ?? 1;
        SetEGLContextClientVersion(2);
        SetEGLConfigChooser(8, 8, 8, 8, 0, 8);
        _renderer = new Renderer(this);
        SetRenderer(_renderer);
        SetOpaque(false);
        RenderMode = global::Android.Opengl.Rendermode.WhenDirty;
    }

    /// <summary>Renderer that composites into this surface (read on the GL thread).</summary>
    internal SkUiFrameRenderer? FrameRenderer
    {
        get => _renderer.FrameRenderer;
        set
        {
            _renderer.FrameRenderer = value;
            RequestRender();
        }
    }

    /// <summary>UI thread: raw touches in DIPs; <c>true</c> keeps receiving the gesture.</summary>
    internal Func<SkUiTouchEvent, bool>? TouchHandler { get; set; }

    /// <summary>Whether drawn gestures want the touch (keeps native parents such as a ScrollView from intercepting).</summary>
    internal Func<SkUiNativeGestureState>? NativeGestureState { get; set; }

    private bool _disallowingIntercept;

    public override bool OnTouchEvent(MotionEvent? e)
    {
        var handled = HandleTouch(e);
        if (e is not null)
            SyncNativeParent(e.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel);
        return handled;
    }

    /// <summary>
    /// While a drawn continuous gesture may still claim (or owns) the touch, native ancestors must not intercept it;
    /// once none can (e.g. a drawn scroller at its edge), the request is dropped so the native parent can take over.
    /// </summary>
    private void SyncNativeParent(bool ended)
    {
        var disallow = !ended && NativeGestureState?.Invoke() is SkUiNativeGestureState.Pending or SkUiNativeGestureState.Claimed;
        if (disallow == _disallowingIntercept)
            return;
        _disallowingIntercept = disallow;
        Parent?.RequestDisallowInterceptTouchEvent(disallow);
    }

    private bool HandleTouch(MotionEvent? e)
    {
        if (e is null || TouchHandler is not { } touch)
            return false;
        var time = TimeSpan.FromMilliseconds(e.EventTime);
        var masked = e.ActionMasked;
        switch (masked)
        {
            case MotionEventActions.Down:
                // Not handled: let native parents (e.g. a MAUI ScrollView) take the gesture.
                return touch(Event(e, e.ActionIndex, SkUiTouchAction.Pressed, time));
            case MotionEventActions.PointerDown:
                touch(Event(e, e.ActionIndex, SkUiTouchAction.Pressed, time));
                return true;
            case MotionEventActions.Move:
                for (var index = 0; index < e.PointerCount; index++)
                    touch(Event(e, index, SkUiTouchAction.Moved, time));
                return true;
            case MotionEventActions.Up:
            case MotionEventActions.PointerUp:
                touch(Event(e, e.ActionIndex, SkUiTouchAction.Released, time));
                return true;
            case MotionEventActions.Cancel:
                for (var index = 0; index < e.PointerCount; index++)
                    touch(Event(e, index, SkUiTouchAction.Cancelled, time));
                return true;
            default:
                return false;
        }
    }

    public override bool OnGenericMotionEvent(MotionEvent? e)
    {
        if (e is not null && e.ActionMasked == MotionEventActions.Scroll && TouchHandler is { } touch)
        {
            var (x, y) = WheelDeltas(e);
            if (x != 0 || y != 0)
                return touch(new SkUiTouchEvent(0, SkUiTouchAction.Wheel, new Point(e.GetX() / _density, e.GetY() / _density),
                    TimeSpan.FromMilliseconds(e.EventTime), y, x));
        }
        return base.OnGenericMotionEvent(e);
    }

    /// <summary>
    /// An <c>ACTION_SCROLL</c> event as SkiaUi wheel deltas in DIPs (positive towards the start): one wheel notch is about
    /// 64 DIPs; positive AXIS_VSCROLL scrolls the content up, positive AXIS_HSCROLL scrolls it to the right.
    /// </summary>
    internal static (double X, double Y) WheelDeltas(MotionEvent e) =>
        (-e.GetAxisValue(Axis.Hscroll) * 64, e.GetAxisValue(Axis.Vscroll) * 64);

    private SkUiTouchEvent Event(MotionEvent e, int index, SkUiTouchAction action, TimeSpan time) =>
        new(e.GetPointerId(index), action, new Point(e.GetX(index) / _density, e.GetY(index) / _density), time);

    private sealed class Renderer(SkUiGlTextureView owner) : SKGLTextureViewRenderer
    {
        internal volatile SkUiFrameRenderer? FrameRenderer;

        protected override void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
        {
            if (FrameRenderer is not { } renderer)
            {
                e.Surface.Canvas.Clear(SKColors.Transparent);
                return;
            }
            // A frame requested only to run the warm-up (see ScheduleWarmUp) is not a real frame.
            var warmUpTurn = Interlocked.Exchange(ref _warmUpTurn, 0) == 1;
            if (!warmUpTurn)
                _lastFrame = System.Diagnostics.Stopwatch.GetTimestamp();
            var target = e.BackendRenderTarget;
            var info = new SKImageInfo(target.Width, target.Height, e.ColorType, SKAlphaType.Premul);
            // GL thread: composite only; continuous frames while render-thread animations run, paced by vsync.
            var continuous = renderer.Render(e.Surface.Canvas, info);
            // Flush here (the base flushes again, cheaply) so statistics include GPU command submission.
            e.Surface.Flush();
            renderer.CompleteFrame();
            // Once the surface has drawn nothing for a while: compile the next batch of GPU pipelines of this view's GL
            // context (one step per frame), so the first scroll or animation does not stall on them.
            if (!continuous && !_warmUp.IsDone && e.Surface.Context is { } context)
            {
                var idle = System.Diagnostics.Stopwatch.GetElapsedTime(_lastFrame) >= SkUiGpuWarmUp.IdleDelay;
                if (warmUpTurn && idle)
                {
                    try { _warmUp.RunNext(context, e.ColorType); }
                    catch (Exception exception) { System.Diagnostics.Debug.WriteLine($"SkiaUi GPU warm-up failed: {exception}"); }
                }
                if (!_warmUp.IsDone)
                    ScheduleWarmUp(idle && warmUpTurn);
            }
            if (continuous && Interlocked.Exchange(ref _framePending, 1) == 0)
                SkUiVsync.Post(_vsync ??= new VsyncCallback(this));
        }

        private readonly SkUiGpuWarmUp _warmUp = new();
        private long _lastFrame;
        private int _warmUpTurn;
        private int _warmUpScheduled;

        /// <summary>Requests a warm-up turn: on the next vsync while warming up, else after the idle delay.</summary>
        private void ScheduleWarmUp(bool next)
        {
            if (Interlocked.Exchange(ref _warmUpScheduled, 1) == 1)
                return;
            void Turn()
            {
                Interlocked.Exchange(ref _warmUpScheduled, 0);
                Interlocked.Exchange(ref _warmUpTurn, 1);
                owner.RequestRender();
            }
            if (next)
                owner.Post(Turn);
            else
                owner.PostDelayed(Turn, (long)SkUiGpuWarmUp.IdleDelay.TotalMilliseconds);
        }
        private int _framePending;
        private VsyncCallback? _vsync;

        private void OnVsync()
        {
            Interlocked.Exchange(ref _framePending, 0);
            owner.RequestRender();
        }

        private sealed class VsyncCallback(Renderer renderer) : Java.Lang.Object, Choreographer.IFrameCallback
        {
            public void DoFrame(long frameTimeNanos) => renderer.OnVsync();
        }
    }
}
#endif
