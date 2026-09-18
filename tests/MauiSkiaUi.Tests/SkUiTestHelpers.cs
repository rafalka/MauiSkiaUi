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
    /// Registers the Roboto Mono TTF copied next to the test assembly. Linux CI agents often have no
    /// usable system fonts, so Skia <c>FromFamilyName</c> measures as empty without a registered face.
    /// </summary>
    public static IDisposable UseBundledFont(string familyName = BundledFontFamily)
    {
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf");
        if (!File.Exists(fontPath))
            throw new FileNotFoundException($"Bundled test font missing at '{fontPath}'.");
        SkUiFonts.Register(familyName, () => File.OpenRead(fontPath));
        return new FontRegistration(familyName);
    }

    private sealed class FontRegistration(string familyName) : IDisposable
    {
        public void Dispose() => SkUiFonts.Unregister(familyName);
    }
}
