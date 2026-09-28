using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Registers Skia typefaces for font family names that <see cref="SKTypeface.FromFamilyName(string, SKFontStyleWeight, SKFontStyleWidth, SKFontStyleSlant)"/>
/// cannot resolve as an installed system font. Fonts registered with MAUI <c>ConfigureFonts</c> are also picked up automatically.
/// <see cref="SkUiLabel"/> and its subclasses consult this registry before falling back to system font lookup.
/// </summary>
public static class SkUiFonts
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Func<Stream>> Factories = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, SKTypeface> Cache = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Misses = new(StringComparer.Ordinal);

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
            Misses.Remove(familyName);
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
            Misses.Remove(familyName);
            Cache.Remove(familyName);
        }
    }

    /// <summary>Resolves a registered family name to a cached, registry-owned typeface, or null if not registered/loadable.</summary>
    internal static SKTypeface? TryResolve(string familyName)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(familyName, out var typeface)) return typeface;
            if (Misses.Contains(familyName)) return null;
            var definitive = false;
            if (Factories.TryGetValue(familyName, out var open))
            {
                using var stream = open();
                typeface = SKTypeface.FromStream(stream);
            }
            else
            {
                typeface = ResolveMauiFont(familyName, out definitive);
            }
            if (typeface is not null) Cache[familyName] = typeface;
            // Remember a miss only when the MAUI registrar answered: before the app's services exist, retry later.
            else if (definitive) Misses.Add(familyName);
            return typeface;
        }
    }

    /// <summary>
    /// Falls back to fonts registered with MAUI <c>ConfigureFonts</c> (via <see cref="Microsoft.Maui.IFontRegistrar"/>),
    /// so app-embedded fonts work without an explicit <see cref="Register"/> call. The registrar returns a file path
    /// (Android / Windows) or a registered family / PostScript name (Apple).
    /// </summary>
    /// <param name="alias">Family name / alias to look up.</param>
    /// <param name="definitive"><c>false</c> when the registrar could not be asked (app not started yet, lookup failed).</param>
    private static SKTypeface? ResolveMauiFont(string alias, out bool definitive)
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        definitive = false;
        try
        {
            if (IPlatformApplication.Current?.Services.GetService(typeof(Microsoft.Maui.IFontRegistrar)) is not Microsoft.Maui.IFontRegistrar registrar)
                return null;
            var font = registrar.GetFont(alias);
            definitive = true;
            if (font is not { Length: > 0 })
                return null;
            if (File.Exists(font))
                return SKTypeface.FromFile(font);
            return SKFontManager.Default.MatchFamily(font);
        }
        catch (Exception)
        {
            return null;
        }
#else
        definitive = true;
        return null;
#endif
    }
}
