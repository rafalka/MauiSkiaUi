namespace MauiSkiaUi;

/// <summary>
/// Where a drawn image comes from, on both layers: Core nodes take it directly (<c>SkUiCoreImage.SetSource</c>), and
/// SkUi* controls convert MAUI's <c>ImageSource</c> to it. Loading goes through one shared loader and cache
/// (<see cref="SkUiImageCache"/>), so a second view of the same source decodes nothing. Sources are immutable records:
/// equal sources share cache entries and reassigning an equal source does not reload.
/// </summary>
public abstract record SkUiImageSource
{
    private protected SkUiImageSource() { }

    /// <summary>
    /// The identity of the source in the caches, or <c>null</c> when the same source can produce different images (a
    /// stream without a cache key): such images are decoded for each view and not cached. The app's
    /// <see cref="SkUiImageCache.CacheKeyFactory"/> can replace it.
    /// </summary>
    internal abstract string? CacheKey { get; }

    /// <summary>
    /// Loads and decodes the image. Runs on the caller's thread until its first await; work that completes without
    /// awaiting (font glyphs) makes the whole load synchronous, so the image shows without a frame's delay.
    /// </summary>
    internal abstract Task<SkUiDecodedImage> LoadAsync(SkUiImageLoadContext context, CancellationToken token);

    /// <summary>
    /// A file: an absolute path, or a name in the app package. A package name resolves like MAUI's <c>FileImageSource</c>:
    /// first the build-processed <c>MauiImage</c> for the display density (Android drawables, iOS / Mac Catalyst
    /// <c>@2x</c> / <c>@3x</c> bundle files, Windows <c>.scale-NNN</c> files; an <c>.svg</c> item is referenced by its
    /// generated <c>.png</c> name), then a <c>MauiAsset</c> in Resources/Raw.
    /// </summary>
    public static SkUiImageSource FromFile(string path) => new SkUiFileImageSource(path);

    /// <summary>
    /// An HTTP(S) download (plain http needs the platform's cleartext permission). With <paramref name="cachingEnabled"/>
    /// the bytes are kept in the disk cache for <paramref name="cacheValidity"/> (default one day, as MAUI's
    /// <c>UriImageSource</c>).
    /// </summary>
    public static SkUiImageSource FromUri(Uri uri, bool cachingEnabled = true, TimeSpan? cacheValidity = null) =>
        new SkUiUriImageSource(uri, cachingEnabled, cacheValidity ?? SkUiUriImageSource.DefaultCacheValidity);

    /// <summary>
    /// Encoded bytes from a stream factory; the stream is disposed after reading. Without <paramref name="cacheKey"/>
    /// the image is not cached (the factory may return different content each time); with one, views with the same
    /// key share one decoded image.
    /// </summary>
    public static SkUiImageSource FromStream(Func<CancellationToken, Task<Stream>> open, string? cacheKey = null) =>
        new SkUiStreamImageSource(open, cacheKey);

    /// <summary>
    /// A glyph of a font (MAUI's <c>FontImageSource</c>), drawn through the text engine: fonts registered with
    /// <c>ConfigureFonts</c> or <see cref="SkUiFonts"/>, ligatures (icon names in Material Symbols) and fallback.
    /// <paramref name="size"/> is the font size in DIPs; <paramref name="color"/> defaults to white, as in MAUI. With
    /// <paramref name="fontAutoScalingEnabled"/> (default, as MAUI's) the glyph follows the system text size
    /// (<see cref="SkUiFontScaling"/>) when the source is created.
    /// </summary>
    public static SkUiImageSource FromFont(string glyph, string? fontFamily = null, double size = SkUiFontImageSource.DefaultSize,
        Color? color = null, FontAttributes fontAttributes = FontAttributes.None, bool fontAutoScalingEnabled = true) =>
        new SkUiFontImageSource(glyph, fontFamily, size, color, fontAttributes, fontAutoScalingEnabled);
}

/// <summary>An absolute file path or a name in the app package (see <see cref="SkUiImageSource.FromFile"/>).</summary>
public sealed record SkUiFileImageSource : SkUiImageSource
{
    /// <summary>Creates a file source.</summary>
    public SkUiFileImageSource(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        File = file;
    }

    /// <summary>The absolute path or package name.</summary>
    public string File { get; }

    /// <summary>Whether <see cref="File"/> is an absolute path rather than a package name.</summary>
    public bool IsAbsolute => Path.IsPathRooted(File);

    internal override string CacheKey => (IsAbsolute ? "file:" : "app:") + File;

    internal override Task<SkUiDecodedImage> LoadAsync(SkUiImageLoadContext context, CancellationToken token) =>
        IsAbsolute ? LoadAbsoluteAsync(context, token) : LoadPackageAsync(context, token);

    private async Task<SkUiDecodedImage> LoadAbsoluteAsync(SkUiImageLoadContext context, CancellationToken token)
    {
        context.Origin = SkUiImageOrigin.File;
        var bytes = await SkUiImageLoader.ReadAllAsync(System.IO.File.OpenRead(File), token).ConfigureAwait(false);
        return await context.DecodeAsync(bytes, sourceScale: 1, token).ConfigureAwait(false);
    }

    private async Task<SkUiDecodedImage> LoadPackageAsync(SkUiImageLoadContext context, CancellationToken token)
    {
        // MAUI's rule: a FileImageSource names a MauiImage; SkiaUi also finds MauiAsset files (Resources/Raw) by name.
        var scale = 1f;
        Stream stream;
        if (SkUiPackageImages.TryOpen(File, context.DisplayScale) is { } image)
        {
            (stream, scale) = image;
            context.Origin = SkUiImageOrigin.MauiImage;
        }
        else
        {
            stream = await FileSystem.Current.OpenAppPackageFileAsync(File).ConfigureAwait(false);
            context.Origin = SkUiImageOrigin.PackageAsset;
        }
        var bytes = await SkUiImageLoader.ReadAllAsync(stream, token).ConfigureAwait(false);
        return await context.DecodeAsync(bytes, scale, token).ConfigureAwait(false);
    }
}

/// <summary>An HTTP(S) download with an optional disk cache (see <see cref="SkUiImageSource.FromUri"/>).</summary>
public sealed record SkUiUriImageSource : SkUiImageSource
{
    /// <summary>MAUI's default <c>UriImageSource.CacheValidity</c>: one day.</summary>
    public static readonly TimeSpan DefaultCacheValidity = TimeSpan.FromDays(1);

    /// <summary>Creates a URI source.</summary>
    public SkUiUriImageSource(Uri uri, bool cachingEnabled = true, TimeSpan? cacheValidity = null)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new NotSupportedException("Only absolute HTTP(S) image URIs are supported.");
        Uri = uri;
        CachingEnabled = cachingEnabled;
        CacheValidity = cacheValidity ?? DefaultCacheValidity;
        if (CacheValidity <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(cacheValidity), cacheValidity, "The cache validity must be positive.");
    }

    /// <summary>The image URI.</summary>
    public Uri Uri { get; }

    /// <summary>Whether the downloaded bytes are kept in the disk cache (MAUI's <c>UriImageSource.CachingEnabled</c>).</summary>
    public bool CachingEnabled { get; }

    /// <summary>How long a disk-cached download is used before it is downloaded again (MAUI's <c>UriImageSource.CacheValidity</c>).</summary>
    public TimeSpan CacheValidity { get; }

    // The memory cache is keyed by the URI only: validity and disk caching decide where bytes come from, not the image.
    internal override string CacheKey => "uri:" + Uri.AbsoluteUri;

    internal override async Task<SkUiDecodedImage> LoadAsync(SkUiImageLoadContext context, CancellationToken token)
    {
        var useDisk = CachingEnabled && context.UseDiskCache;
        var diskKey = context.SourceKey ?? CacheKey; // a custom key shares the download between URLs
        var bytes = useDisk ? await SkUiImageDiskCache.TryReadAsync(diskKey, CacheValidity, token).ConfigureAwait(false) : null;
        var fromDisk = bytes is not null;
        context.Origin = fromDisk ? SkUiImageOrigin.DiskCache : SkUiImageOrigin.Network;
        bytes ??= await SkUiImageLoader.DownloadAsync(Uri, token).ConfigureAwait(false);
        SkUiDecodedImage decoded;
        try
        {
            decoded = await context.DecodeAsync(bytes, sourceScale: 1, token).ConfigureAwait(false);
        }
        catch (Exception) when (fromDisk && !token.IsCancellationRequested)
        {
            _ = SkUiImageDiskCache.RemoveAsync(diskKey); // an unreadable cached file is downloaded again next time
            throw;
        }
        // Only bytes that decode are kept: a corrupt response must not stay in the cache for its validity.
        if (useDisk && !fromDisk)
            SkUiImageDiskCache.Write(diskKey, bytes);
        return decoded;
    }
}

/// <summary>Encoded bytes from a stream factory (see <see cref="SkUiImageSource.FromStream"/>).</summary>
public sealed record SkUiStreamImageSource : SkUiImageSource
{
    /// <summary>Creates a stream source.</summary>
    public SkUiStreamImageSource(Func<CancellationToken, Task<Stream>> open, string? cacheKey = null)
    {
        ArgumentNullException.ThrowIfNull(open);
        Open = open;
        Key = cacheKey;
    }

    /// <summary>Opens the encoded stream; it is disposed after reading.</summary>
    public Func<CancellationToken, Task<Stream>> Open { get; }

    /// <summary>The cache identity, or <c>null</c>: not cached.</summary>
    public string? Key { get; }

    internal override string? CacheKey => Key is null ? null : "stream:" + Key;

    internal override async Task<SkUiDecodedImage> LoadAsync(SkUiImageLoadContext context, CancellationToken token)
    {
        context.Origin = SkUiImageOrigin.Stream;
        var stream = await Open(token).ConfigureAwait(false) ?? throw new InvalidDataException("The stream factory returned null.");
        var bytes = await SkUiImageLoader.ReadAllAsync(stream, token).ConfigureAwait(false);
        return await context.DecodeAsync(bytes, sourceScale: 1, token).ConfigureAwait(false);
    }
}

/// <summary>A font glyph drawn as an image (see <see cref="SkUiImageSource.FromFont"/>).</summary>
public sealed record SkUiFontImageSource : SkUiImageSource
{
    /// <summary>MAUI's default <c>FontImageSource.Size</c>.</summary>
    public const double DefaultSize = 30;

    /// <summary>Creates a font glyph source; see <see cref="SkUiImageSource.FromFont"/>.</summary>
    public SkUiFontImageSource(string glyph, string? fontFamily = null, double size = DefaultSize, Color? color = null,
        FontAttributes fontAttributes = FontAttributes.None, bool fontAutoScalingEnabled = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(glyph);
        if (!double.IsFinite(size) || size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), size, "The font size must be finite and positive.");
        Glyph = glyph;
        FontFamily = fontFamily;
        Size = size;
        Color = color;
        FontAttributes = fontAttributes;
        FontAutoScalingEnabled = fontAutoScalingEnabled;
        DrawnSize = SkUiFontScaling.ScaleFontSize(size, fontAutoScalingEnabled);
    }

    /// <summary>The text to draw: usually one icon code point, or a ligature name.</summary>
    public string Glyph { get; }

    /// <summary>Family name or alias (<c>ConfigureFonts</c>); <c>null</c>: the default font.</summary>
    public string? FontFamily { get; }

    /// <summary>Font size in DIPs.</summary>
    public double Size { get; }

    /// <summary>Glyph color; <c>null</c>: white, as MAUI's <c>FontImageSource</c>.</summary>
    public Color? Color { get; }

    /// <summary>Bold / italic.</summary>
    public FontAttributes FontAttributes { get; }

    /// <summary>Whether the glyph follows the system text size (as MAUI's <c>FontImageSource.FontAutoScalingEnabled</c>).</summary>
    public bool FontAutoScalingEnabled { get; }

    /// <summary>
    /// The font size drawn: <see cref="Size"/> with <see cref="SkUiLook.FontScale"/> and (auto scaling) the system text size,
    /// both at creation; a later change does not resize an existing source (a new one is needed).
    /// </summary>
    internal double DrawnSize { get; }

    internal override string CacheKey =>
        FormattableString.Invariant($"font:{FontFamily}|{FontAttributes}|{DrawnSize}|{(Color ?? Colors.White).ToArgbHex(includeAlpha: true)}|{Glyph}");

    internal override Task<SkUiDecodedImage> LoadAsync(SkUiImageLoadContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        context.Origin = SkUiImageOrigin.Font;
        return Task.FromResult(SkUiFontImages.Render(this, context.DisplayScale));
    }
}
