#if ANDROID || IOS || MACCATALYST || WINDOWS
namespace MauiSkiaUi;

/// <summary>
/// UI-thread vsync callback used only for <see cref="SkUiAnimationClock"/> (callback animations that change
/// arbitrary properties and therefore must run on the UI thread). Composite-time motion — fling, animated
/// scroll, <c>AnimateAsync</c>, spinners — runs on the render thread and does not use this ticker.
/// Android: Choreographer; Apple: CADisplayLink in common modes (keeps firing while a UIScrollView tracks);
/// Windows: dispatcher timer.
/// </summary>
internal sealed class SkUiUiTicker : IDisposable
{
    private readonly Action _tick;
    private bool _running;
#if ANDROID
    private FrameCallback? _callback;
#elif IOS || MACCATALYST
    private CoreAnimation.CADisplayLink? _link;
#else
    private readonly IDispatcher _dispatcher;
#endif

    internal SkUiUiTicker(IDispatcher dispatcher, Action tick)
    {
        _tick = tick;
#if WINDOWS
        _dispatcher = dispatcher;
#endif
    }

    internal bool IsRunning => _running;

    internal void Start()
    {
        if (_running)
            return;
        _running = true;
#if ANDROID
        _callback ??= new FrameCallback(this);
        Android.Views.Choreographer.Instance?.PostFrameCallback(_callback);
#elif IOS || MACCATALYST
        _link = CoreAnimation.CADisplayLink.Create(OnFrame);
        _link.AddToRunLoop(Foundation.NSRunLoop.Main, Foundation.NSRunLoopMode.Common);
#else
        _dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(16), OnFrame);
#endif
    }

    internal void Stop()
    {
        if (!_running)
            return;
        _running = false;
#if ANDROID
        if (_callback is not null)
            Android.Views.Choreographer.Instance?.RemoveFrameCallback(_callback);
#elif IOS || MACCATALYST
        _link?.Invalidate();
        _link = null;
#endif
    }

    private void OnFrame()
    {
        if (!_running)
            return;
        _tick();
#if WINDOWS
        if (_running)
            _dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(16), OnFrame);
#endif
    }

    public void Dispose()
    {
        Stop();
#if ANDROID
        _callback?.Dispose();
        _callback = null;
#endif
    }

#if ANDROID
    private sealed class FrameCallback(SkUiUiTicker owner) : Java.Lang.Object, Android.Views.Choreographer.IFrameCallback
    {
        public void DoFrame(long frameTimeNanos)
        {
            owner.OnFrame();
            if (owner._running)
                Android.Views.Choreographer.Instance?.PostFrameCallback(this);
        }
    }
#endif
}
#endif
