#if ANDROID
using Android.Content;
using Android.Views;
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

    public override bool OnTouchEvent(MotionEvent? e)
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
            // One wheel notch ≈ 64 DIPs; positive AXIS_VSCROLL scrolls content up.
            var delta = e.GetAxisValue(Axis.Vscroll) * 64;
            if (delta != 0)
                return touch(new SkUiTouchEvent(0, SkUiTouchAction.Wheel, new Point(e.GetX() / _density, e.GetY() / _density),
                    TimeSpan.FromMilliseconds(e.EventTime), delta));
        }
        return base.OnGenericMotionEvent(e);
    }

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
            var target = e.BackendRenderTarget;
            var info = new SKImageInfo(target.Width, target.Height, e.ColorType, SKAlphaType.Premul);
            // GL thread: composite only; continuous frames while render-thread animations run, paced by vsync.
            if (renderer.Render(e.Surface.Canvas, info) && Interlocked.Exchange(ref _framePending, 1) == 0)
                SkUiVsync.Post(_vsync ??= new VsyncCallback(this));
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
