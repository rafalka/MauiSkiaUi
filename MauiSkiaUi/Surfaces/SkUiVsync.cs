#if ANDROID
using Android.OS;
using Android.Views;

namespace MauiSkiaUi.Surfaces;

/// <summary>
/// Vsync source for GL surfaces that is independent of the UI thread: a <see cref="Choreographer"/> owned by a
/// dedicated looper thread. <c>eglSwapBuffers</c> on a <see cref="TextureView"/> does not block on vsync, so without
/// pacing continuous render-thread animations would render hundreds of discarded frames per second.
/// </summary>
internal static class SkUiVsync
{
    private static readonly Lazy<Choreographer> Shared = new(Create, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Runs <paramref name="callback"/> (on the vsync thread) at the next display frame.</summary>
    internal static void Post(Choreographer.IFrameCallback callback) => Shared.Value.PostFrameCallback(callback);

    private static Choreographer Create()
    {
        var thread = new HandlerThread("SkUiVsync", (int)global::Android.OS.ThreadPriority.Display);
        thread.Start();
        Choreographer? choreographer = null;
        using var ready = new ManualResetEventSlim();
        new Handler(thread.Looper!).Post(() =>
        {
            choreographer = Choreographer.Instance;
            ready.Set();
        });
        ready.Wait();
        return choreographer!;
    }
}
#endif
