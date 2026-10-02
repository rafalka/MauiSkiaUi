namespace MauiSkiaUi;

/// <summary>Ready-made <see cref="SkUiImageCache.CacheKeyFactory"/> factories and helpers for writing your own.</summary>
public static class SkUiImageCacheKeys
{
    /// <summary>
    /// Keys URI sources by their URL without the query parameters in <paramref name="names"/> (case-insensitive), so
    /// signed or tokenized URLs of one image share its cache entries; with no names, without the whole query. Other
    /// sources keep their default key.
    /// <code>SkUiImageCache.CacheKeyFactory = SkUiImageCacheKeys.IgnoreQueryParameters("token", "sig", "expires");</code>
    /// </summary>
    public static Func<SkUiImageSource, string?> IgnoreQueryParameters(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var ignored = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        return source => source is SkUiUriImageSource uri ? WithoutQueryParameters(uri.Uri, ignored) : null;
    }

    /// <summary>
    /// <paramref name="uri"/> without the query parameters in <paramref name="names"/> (case-insensitive) and without
    /// the fragment; the remaining parameters keep their order and encoding. With no names, the whole query goes.
    /// </summary>
    public static string WithoutQueryParameters(Uri uri, params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return WithoutQueryParameters(uri, new HashSet<string>(names, StringComparer.OrdinalIgnoreCase));
    }

    private static string WithoutQueryParameters(Uri uri, HashSet<string> names)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var path = uri.GetLeftPart(UriPartial.Path);
        if (names.Count == 0 || uri.Query.Length <= 1)
            return path;
        var kept = new List<string>();
        foreach (var parameter in uri.Query[1..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = parameter.IndexOf('=');
            var name = Uri.UnescapeDataString((separator < 0 ? parameter : parameter[..separator]).Replace('+', ' '));
            if (!names.Contains(name))
                kept.Add(parameter);
        }
        return kept.Count == 0 ? path : path + "?" + string.Join('&', kept);
    }
}
