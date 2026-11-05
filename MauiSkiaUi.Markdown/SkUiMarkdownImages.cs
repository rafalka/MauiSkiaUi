using System.Text;

namespace MauiSkiaUi.Markdown;

/// <summary>
/// What a URL in a document may load: the default rule of a Markdown view, which an <c>ImageResolver</c> replaces.
/// Never gives a document's URL to <see cref="SkUiImageSource.FromString"/>, which opens absolute paths and <c>file:</c> URIs.
/// </summary>
public static class SkUiMarkdownImages
{
    /// <summary>Longest <c>data:</c> URL that is decoded, in characters.</summary>
    public const int MaximumDataUrlLength = 8 * 1024 * 1024;

    private const string DataPrefix = "data:";
    private const string SvgType = "image/svg+xml";
    private const string SvgExtension = ".svg";
    private const string ImageTypePrefix = "image/";
    private const string Base64Marker = ";base64";

    /// <summary>
    /// The source for <paramref name="url"/>, or <c>null</c> when it may not load (the view shows the alt text):
    /// <list type="bullet">
    /// <item><c>data:</c> image URLs, up to <see cref="MaximumDataUrlLength"/>, decode in place;</item>
    /// <item>a bare relative name (no scheme, no leading <c>/</c>, no <c>..</c>) is one of the app's own images or assets;</item>
    /// <item><c>http</c> and <c>https</c> load only with <paramref name="allowRemote"/>: an image draws as soon as it shows, which tells its server;</item>
    /// <item>everything else, absolute paths and <c>file:</c> included, does not load.</item>
    /// </list>
    /// </summary>
    /// <param name="url">The URL as written in the document.</param>
    /// <param name="allowRemote">Whether <c>http</c> and <c>https</c> load.</param>
    /// <param name="vectorColor">The <c>currentColor</c> of an SVG.</param>
    public static SkUiImageSource? Resolve(string? url, bool allowRemote = false, Color? vectorColor = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        url = url.Trim();
        if (url.StartsWith(DataPrefix, StringComparison.OrdinalIgnoreCase))
            return FromDataUrl(url, vectorColor);
        if (IsBareName(url))
            return IsSvgPath(url) ? SkUiImageSource.FromVector(SkUiImageSource.FromFile(url), vectorColor) : SkUiImageSource.FromFile(url);
        if (allowRemote && Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            return IsSvgPath(uri.AbsolutePath) ? SkUiImageSource.FromVector(SkUiImageSource.FromUri(uri), vectorColor) : SkUiImageSource.FromUri(uri);
        return null;
    }

    /// <summary>A name inside the app's own images and assets: nothing that could leave them, or be read as something else on some platform.</summary>
    private static bool IsBareName(string url)
    {
        if (url[0] is '/' or '~' or '.')
            return false;
        foreach (var character in url)
            if (character is ':' or '\\' or '%' or '?' or '#' || char.IsControl(character))
                return false;
        foreach (var segment in url.Split('/'))
            if (segment is "" or "." or "..")
                return false;
        return true;
    }

    private static bool IsSvgPath(string path) => path.EndsWith(SvgExtension, StringComparison.OrdinalIgnoreCase);

    private static SkUiImageSource? FromDataUrl(string url, Color? vectorColor)
    {
        var comma = url.IndexOf(',');
        if (comma < 0 || url.Length > MaximumDataUrlLength)
            return null;
        var header = url.AsSpan(DataPrefix.Length, comma - DataPrefix.Length);
        if (!header.StartsWith(ImageTypePrefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var isBase64 = header.EndsWith(Base64Marker, StringComparison.OrdinalIgnoreCase);
        try
        {
            if (header.StartsWith(SvgType, StringComparison.OrdinalIgnoreCase))
            {
                var svg = isBase64 ? Encoding.UTF8.GetString(Convert.FromBase64String(url[(comma + 1)..])) : Uri.UnescapeDataString(url[(comma + 1)..]);
                return string.IsNullOrWhiteSpace(svg) ? null : SkUiImageSource.FromVectorText(svg, vectorColor);
            }
        }
        catch (FormatException)
        {
            return null;
        }
        if (!isBase64)
            return null;
        // Decoded when the image loads, off the UI thread; bad data fails the load and the alt text shows.
        return SkUiImageSource.FromStream(_ => Task.FromResult<Stream>(new MemoryStream(Convert.FromBase64String(url[(comma + 1)..]), writable: false)));
    }
}
