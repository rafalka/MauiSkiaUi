using MauiSkiaUi;
using Microsoft.Maui.Storage;

namespace MauiSkiaUiDemo;

/// <summary>
/// Loads the demo's app-embedded fonts as raw bytes and registers them with <see cref="SkUiFonts"/> so
/// <see cref="SkUiLabel"/>/<see cref="SkUiButton"/> can draw the same glyphs as the native MAUI counterparts,
/// which resolve these family names through <c>ConfigureFonts</c> aliases instead.
/// </summary>
internal static class DemoFonts
{
    /// <summary>Family names registered here, matching the aliases passed to <c>fonts.AddFont</c> in MauiProgram.</summary>
    public static readonly IReadOnlyList<string> RegisteredFamilies = ["OpenSansRegular", "OpenSansSemibold", "Lobster", "RobotoMono"];

    public static async Task PreloadAsync()
    {
        foreach (var family in RegisteredFamilies)
        {
            var bytes = await ReadRawFontAsync(family);
            SkUiFonts.Register(family, () => new MemoryStream(bytes, writable: false));
        }
    }

    private static async Task<byte[]> ReadRawFontAsync(string family)
    {
        var fileName = family switch
        {
            "OpenSansRegular" => "OpenSans-Regular.ttf",
            "OpenSansSemibold" => "OpenSans-Semibold.ttf",
            "Lobster" => "Lobster-Regular.ttf",
            "RobotoMono" => "RobotoMono-Regular.ttf",
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
        using var stream = await FileSystem.Current.OpenAppPackageFileAsync($"Fonts/{fileName}");
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes);
        return bytes.ToArray();
    }
}
