using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Registers Skia typefaces for font family names that <see cref="SKTypeface.FromFamilyName(string, SKFontStyleWeight, SKFontStyleWidth, SKFontStyleSlant)"/>
/// cannot resolve as an installed system font — most notably app-embedded MAUI fonts registered via <c>ConfigureFonts</c>.
/// <see cref="SkUiLabel"/> and its subclasses consult this registry before falling back to system font lookup.
/// </summary>
public static class SkUiFonts
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Func<Stream>> Factories = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, SKTypeface> Cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a typeface loader for a font family name, e.g. the same alias passed to <c>fonts.AddFont(file, alias)</c>.
    /// The stream is opened lazily and only once; call this from app startup before the family name is first painted.
    /// </summary>
    public static void Register(string familyName, Func<Stream> openFont)
    {
        ArgumentException.ThrowIfNullOrEmpty(familyName);
        ArgumentNullException.ThrowIfNull(openFont);
        lock (Gate)
        {
            Factories[familyName] = openFont;
            // Not disposed: retained pictures on the render thread may still reference the old typeface.
            Cache.Remove(familyName);
        }
    }

    /// <summary>Removes a registration and forgets its cached typeface (left to the GC: retained pictures may still use it).</summary>
    public static void Unregister(string familyName)
    {
        lock (Gate)
        {
            Factories.Remove(familyName);
            Cache.Remove(familyName);
        }
    }

    /// <summary>Resolves a registered family name to a cached, registry-owned typeface, or null if not registered/loadable.</summary>
    internal static SKTypeface? TryResolve(string familyName)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(familyName, out var typeface)) return typeface;
            if (!Factories.TryGetValue(familyName, out var open)) return null;
            using var stream = open();
            typeface = SKTypeface.FromStream(stream);
            if (typeface is not null) Cache[familyName] = typeface;
            return typeface;
        }
    }
}
