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

    /// <summary>The lines the text engine breaks <paramref name="text"/> into for a stock <paramref name="mode"/>, as strings.</summary>
    public static IReadOnlyList<string> BreakLines(string text, double width, LineBreakMode mode, SKTypeface typeface, double fontSize = 16, int maxLines = -1, double characterSpacing = 0)
    {
        IReadOnlyList<string> lines = [];
        new SkUiTextLayout().Measure(text, new SkUiTextStyle(typeface, fontSize, mode, context => lines = context.Break(), maxLines, CharacterSpacing: characterSpacing), default, width);
        return lines;
    }

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

/// <summary>
/// Dispatcher provider that answers only in the test flow that enabled it (see <see cref="SkUiTestHelpers.UseTestDispatcher"/>).
/// Async-local, not thread-static: an async test resumes on another thread, and a thread-static dispatcher would stay on
/// the first one, holding its queued work (and the views it captures) for the tests run there later.
/// </summary>
internal sealed class TestDispatcherProvider : IDispatcherProvider
{
    private static readonly AsyncLocal<TestDispatcher?> _dispatcher = new();
    private static readonly object Gate = new();
    private static bool _installed;

    public IDispatcher? GetForCurrentThread() => _dispatcher.Value;

    public static IDisposable Enable()
    {
        lock (Gate)
        {
            if (!_installed)
            {
                DispatcherProvider.SetCurrent(new TestDispatcherProvider());
                _installed = true;
            }
        }
        _dispatcher.Value = new TestDispatcher();
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose() => _dispatcher.Value = null;
    }

    /// <summary>Runs the delayed work queued on this flow's test dispatcher so far (it never runs by itself).</summary>
    public static void RunDelayed()
    {
        if (_dispatcher.Value is not { } dispatcher)
            return;
        var pending = dispatcher.Delayed.ToArray();
        dispatcher.Delayed.Clear();
        foreach (var action in pending)
            action();
    }

    /// <summary>Runs dispatched work inline; delayed work (gesture timers) is queued, and runs only on <see cref="RunDelayed"/>.</summary>
    private sealed class TestDispatcher : IDispatcher
    {
        public readonly List<Action> Delayed = [];

        public bool IsDispatchRequired => false;

        public bool Dispatch(Action action)
        {
            action();
            return true;
        }

        public bool DispatchDelayed(TimeSpan delay, Action action)
        {
            Delayed.Add(action);
            return true;
        }

        public IDispatcherTimer CreateTimer() => throw new NotSupportedException("No timers in headless tests.");
    }
}

/// <summary>
/// The UI thread's synchronization context for headless tests: continuations of library async code (an animated state
/// change awaiting render-thread animations) are queued and run by <see cref="RunPending"/> on the test thread, between
/// frames, as the app's UI thread runs them, instead of on pool threads racing the frames the test drives. Install it
/// before starting the async work, and do not await unfinished tasks while it is installed (pump instead).
/// </summary>
internal sealed class TestUiContext : SynchronizationContext, IDisposable
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly SynchronizationContext? _previous;

    private TestUiContext(SynchronizationContext? previous) => _previous = previous;

    public static TestUiContext Install()
    {
        var context = new TestUiContext(Current);
        SetSynchronizationContext(context);
        return context;
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        lock (_queue)
        {
            _queue.Enqueue((d, state));
            Monitor.PulseAll(_queue);
        }
    }

    public override void Send(SendOrPostCallback d, object? state) => d(state);

    /// <summary>
    /// Runs queued continuations (and any they queue) on the calling thread. With <paramref name="wait"/>, first waits up
    /// to that long for one to arrive: a task completed with <c>RunContinuationsAsynchronously</c> (a finished render
    /// animation) completes <c>Task.WhenAll</c> on a pool thread, which posts the await's continuation here a moment later.
    /// </summary>
    public void RunPending(TimeSpan wait = default)
    {
        lock (_queue)
            if (_queue.Count == 0 && wait > TimeSpan.Zero)
                Monitor.Wait(_queue, wait);
        while (true)
        {
            (SendOrPostCallback Callback, object? State) item;
            lock (_queue)
            {
                if (_queue.Count == 0)
                    return;
                item = _queue.Dequeue();
            }
            item.Callback(item.State);
        }
    }

    public void Dispose() => SetSynchronizationContext(_previous);
}
