using SkiaSharp;

namespace MauiSkiaUi.Tests;

internal static class SkUiTestHelpers
{
    public const string BundledFontFamily = "Test-RobotoMono";

    public static void Arrange(IView view, double width, double height)
    {
        view.Measure(width, height);
        view.Arrange(new Rect(0, 0, width, height));
    }

    /// <summary>
    /// Gives the calling thread a MAUI dispatcher (bindings need one) until disposed. Thread-scoped: tests in other
    /// classes, which run in parallel and rely on "no dispatcher, no gesture timers", are unaffected.
    /// </summary>
    public static IDisposable UseTestDispatcher() => TestDispatcherProvider.Enable();

    private static readonly Dictionary<string, int> FontUsers = [];

    /// <summary>
    /// Registers the Roboto Mono TTF copied next to the test assembly. Linux CI agents often have no
    /// usable system fonts, so Skia <c>FromFamilyName</c> measures as empty without a registered face.
    /// Reference-counted per family: test classes run in parallel, and one test's dispose must not unregister the
    /// family while another test is measuring with it.
    /// </summary>
    public static IDisposable UseBundledFont(string familyName = BundledFontFamily)
    {
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf");
        if (!File.Exists(fontPath))
            throw new FileNotFoundException($"Bundled test font missing at '{fontPath}'.");
        lock (FontUsers)
        {
            var users = FontUsers.GetValueOrDefault(familyName);
            if (users == 0)
                SkUiFonts.Register(familyName, () => File.OpenRead(fontPath));
            FontUsers[familyName] = users + 1;
        }
        return new FontRegistration(familyName);
    }

    private sealed class FontRegistration(string familyName) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            lock (FontUsers)
            {
                if (_disposed)
                    return;
                _disposed = true;
                if (--FontUsers[familyName] == 0)
                    SkUiFonts.Unregister(familyName);
            }
        }
    }
}

/// <summary>
/// Headless surface: runs the UI-thread frame pipeline and the compositor synchronously with explicit
/// render times, so render-thread animations (fling, spin, AnimateAsync) are deterministic in tests.
/// </summary>
internal sealed class SkUiTestSurface : IDisposable
{
    private readonly Queue<Action> _ui = new();
    private readonly SKBitmap _bitmap;
    private readonly SKCanvas _canvas;

    public SkUiTestSurface(SkUiView root, int width, int height, int density = 1)
    {
        Root = root;
        SkUiTestHelpers.Arrange(root, width, height);
        _bitmap = new SKBitmap(width * density, height * density);
        _canvas = new SKCanvas(_bitmap);
        Renderer = new SkUiFrameRenderer(root, _ui.Enqueue, () => RenderRequests++, () => { });
        Renderer.RequestFrame();
    }

    public SkUiView Root { get; }
    public SkUiFrameRenderer Renderer { get; }
    public int RenderRequests { get; private set; }
    public int RecordedPictures => Renderer.RecordedPictures;
    public bool NeedsFrame => Renderer.Compositor.NeedsFrame;
    public SKBitmap Bitmap => _bitmap;

    /// <summary>Runs queued UI work (frame commits, render-thread feedback).</summary>
    public void PumpUi()
    {
        while (_ui.Count > 0)
            _ui.Dequeue()();
    }

    /// <summary>UI pass, then one render at <paramref name="time"/>, then delivers feedback to the UI.</summary>
    public SKBitmap Frame(TimeSpan time)
    {
        PumpUi();
        Renderer.PresentFrame();
        Renderer.Render(_canvas, _bitmap.Info, time);
        PumpUi();
        return _bitmap;
    }

    public SKBitmap Frame(double milliseconds = 0) => Frame(TimeSpan.FromMilliseconds(milliseconds));

    public void Dispose()
    {
        Renderer.Dispose();
        _canvas.Dispose();
        _bitmap.Dispose();
    }
}

/// <summary>Dispatcher provider that answers only on threads that enabled it (see <see cref="SkUiTestHelpers.UseTestDispatcher"/>).</summary>
internal sealed class TestDispatcherProvider : IDispatcherProvider
{
    [ThreadStatic] private static TestDispatcher? t_dispatcher;
    private static readonly object Gate = new();
    private static bool s_installed;

    public IDispatcher? GetForCurrentThread() => t_dispatcher;

    public static IDisposable Enable()
    {
        lock (Gate)
        {
            if (!s_installed)
            {
                DispatcherProvider.SetCurrent(new TestDispatcherProvider());
                s_installed = true;
            }
        }
        t_dispatcher = new TestDispatcher();
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose() => t_dispatcher = null;
    }

    /// <summary>Runs dispatched work inline; delayed work (gesture timers) is dropped.</summary>
    private sealed class TestDispatcher : IDispatcher
    {
        public bool IsDispatchRequired => false;

        public bool Dispatch(Action action)
        {
            action();
            return true;
        }

        public bool DispatchDelayed(TimeSpan delay, Action action) => true;

        public IDispatcherTimer CreateTimer() => throw new NotSupportedException("No timers in headless tests.");
    }
}
