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
