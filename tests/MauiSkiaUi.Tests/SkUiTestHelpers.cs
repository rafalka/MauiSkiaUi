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
    /// A sample's example out of its page (whose drawn surface it joins in the app), with the page's binding context: to
    /// lay it out or draw it as a root of its own.
    /// </summary>
    public static View DetachSampleContent(MauiSkiaUiSamples.SamplePage page)
    {
        var content = page.SampleContent!;
        // Set before it leaves the page: an inherited context would become null meanwhile, and two-way bindings (a
        // picker's SelectedItem) would write that back.
        content.BindingContext = page.BindingContext;
        page.SampleContent = null;
        return content;
    }

    /// <summary>
    /// Gives the calling thread a MAUI dispatcher (bindings need one) until disposed. Thread-scoped: tests in other
    /// classes, which run in parallel and rely on "no dispatcher, no gesture timers", are unaffected.
    /// </summary>
    public static IDisposable UseTestDispatcher() => TestDispatcherProvider.Enable();

    /// <summary>As <see cref="UseTestDispatcher()"/>, with <paramref name="dispatcher"/> answering.</summary>
    public static IDisposable UseTestDispatcher(IDispatcher dispatcher) => TestDispatcherProvider.Enable(dispatcher);

    /// <summary>
    /// As <see cref="UseTestDispatcher()"/>, with the calling thread as the UI thread: work dispatched from it runs inline,
    /// work dispatched from other threads (a background build or load finishing) is queued, as a UI thread's dispatcher
    /// queues it, and runs on <see cref="TestDispatcherProvider.RunDispatched"/>. For synchronous tests that tear a tree
    /// down while background work may still finish: the inline dispatcher would change the tree from a pool thread.
    /// </summary>
    public static IDisposable UseUiThreadDispatcher() =>
        TestDispatcherProvider.Enable(new TestDispatcherProvider.TestDispatcher(Environment.CurrentManagedThreadId));

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
        var fontPath = BundledFontPath();
        return UseFont(familyName, () => File.OpenRead(fontPath));
    }

    public const string LineGapFontFamily = "Test-RobotoMonoGap";

    /// <summary>
    /// Registers <see cref="LineGapFontFamily"/>: the bundled Roboto Mono with a line gap (hhea and OS/2) of half its em and a
    /// font box whose top is a quarter em higher (Android's font padding), neither of which Roboto Mono itself has.
    /// Reference-counted as <see cref="UseBundledFont"/>.
    /// </summary>
    public static IDisposable UseBundledFontWithLineGap()
    {
        var bytes = BundledFontWithLineGap();
        return UseFont(LineGapFontFamily, () => new MemoryStream(bytes));
    }

    /// <summary>The bytes of the font <see cref="UseBundledFontWithLineGap"/> registers.</summary>
    public static byte[] BundledFontWithLineGap()
    {
        var bytes = File.ReadAllBytes(BundledFontPath());
        int Read16(int at) => bytes[at] << 8 | bytes[at + 1];
        void Write16(int at, int value) => (bytes[at], bytes[at + 1]) = ((byte)(value >> 8), (byte)value);
        int Table(string tag)
        {
            for (var record = 12; record < 12 + 16 * Read16(4); record += 16)
                if (System.Text.Encoding.ASCII.GetString(bytes, record, 4) == tag)
                    return bytes[record + 8] << 24 | bytes[record + 9] << 16 | bytes[record + 10] << 8 | bytes[record + 11];
            throw new InvalidDataException(tag);
        }
        var head = Table("head");
        var em = Read16(head + 18);
        Write16(Table("hhea") + 8, em / 2);
        Write16(Table("OS/2") + 72, em / 2);
        Write16(head + 42, (short)Read16(head + 42) + em / 4); // yMax
        return bytes;
    }

    private static string BundledFontPath()
    {
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf");
        if (!File.Exists(fontPath))
            throw new FileNotFoundException($"Bundled test font missing at '{fontPath}'.");
        return fontPath;
    }

    private static IDisposable UseFont(string familyName, Func<Stream> open)
    {
        lock (FontUsers)
        {
            var users = FontUsers.GetValueOrDefault(familyName);
            if (users == 0)
                SkUiFonts.Register(familyName, open);
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
    private static readonly AsyncLocal<IDispatcher?> _dispatcher = new();
    private static readonly object Gate = new();
    private static bool _installed;

    public IDispatcher? GetForCurrentThread() => _dispatcher.Value;

    public static IDisposable Enable(IDispatcher? dispatcher = null)
    {
        lock (Gate)
        {
            if (!_installed)
            {
                DispatcherProvider.SetCurrent(new TestDispatcherProvider());
                _installed = true;
            }
        }
        _dispatcher.Value = dispatcher ?? new TestDispatcher();
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose() => _dispatcher.Value = null;
    }

    /// <summary>Runs the delayed work queued on this flow's test dispatcher so far (it never runs by itself).</summary>
    public static void RunDelayed()
    {
        if (_dispatcher.Value is not TestDispatcher dispatcher)
            return;
        var pending = dispatcher.Delayed.ToArray();
        dispatcher.Delayed.Clear();
        foreach (var action in pending)
            action();
    }

    /// <summary>Whether work dispatched from another thread waits for <see cref="RunDispatched"/> (<see cref="SkUiTestHelpers.UseUiThreadDispatcher"/>).</summary>
    public static bool HasDispatched => _dispatcher.Value is TestDispatcher dispatcher && dispatcher.HasDispatched;

    /// <summary>Runs the work other threads dispatched to this flow's UI-thread dispatcher so far, on the calling thread.</summary>
    public static void RunDispatched()
    {
        if (_dispatcher.Value is TestDispatcher dispatcher)
            dispatcher.RunDispatched();
    }

    /// <summary>
    /// Runs dispatched work inline, or, with a UI thread, inline only on that thread and queued from others (run by
    /// <see cref="RunDispatched"/>); delayed work (gesture timers) is queued, and runs only on <see cref="RunDelayed"/>.
    /// </summary>
    internal sealed class TestDispatcher(int? uiThread = null) : IDispatcher
    {
        public readonly List<Action> Delayed = [];
        private readonly Queue<Action> _dispatched = new();

        public bool IsDispatchRequired => uiThread is { } thread && thread != Environment.CurrentManagedThreadId;

        public bool HasDispatched
        {
            get
            {
                lock (_dispatched)
                    return _dispatched.Count > 0;
            }
        }

        public bool Dispatch(Action action)
        {
            if (IsDispatchRequired)
            {
                lock (_dispatched)
                    _dispatched.Enqueue(action);
                return true;
            }
            action();
            return true;
        }

        public void RunDispatched()
        {
            while (true)
            {
                Action action;
                lock (_dispatched)
                {
                    if (!_dispatched.TryDequeue(out action!))
                        return;
                }
                action();
            }
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
        }
    }

    public override void Send(SendOrPostCallback d, object? state) => d(state);

    /// <summary>Runs queued continuations (and any they queue) on the calling thread.</summary>
    public void RunPending()
    {
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
