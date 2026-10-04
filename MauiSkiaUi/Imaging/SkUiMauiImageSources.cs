namespace MauiSkiaUi;

/// <summary>Maps MAUI's image sources to <see cref="SkUiImageSource"/> for the SkUi* controls.</summary>
internal static class SkUiMauiImageSources
{
    /// <summary>
    /// The SkiaUi source for <paramref name="source"/>; <c>null</c> for no or an empty source. An unsupported or
    /// invalid source becomes one that fails to load, so the error shows in <c>LoadError</c>.
    /// </summary>
    internal static SkUiImageSource? Convert(ImageSource? source)
    {
        try
        {
            return source switch
            {
                null => null,
                { IsEmpty: true } => null,
                FileImageSource file => SkUiImageSource.FromFile(file.File),
                UriImageSource uri => SkUiImageSource.FromUri(uri.Uri, uri.CachingEnabled, uri.CacheValidity),
                StreamImageSource stream => SkUiImageSource.FromStream(stream.Stream),
                FontImageSource font => SkUiImageSource.FromFont(font.Glyph, font.FontFamily, font.Size, font.Color,
                    fontAutoScalingEnabled: font.FontAutoScalingEnabled),
                _ => new SkUiFailedImageSource(new NotSupportedException(
                    $"{source.GetType().Name} is not supported: use a file, MauiImage, raw package asset, stream, font or HTTP(S) source."))
            };
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException)
        {
            return new SkUiFailedImageSource(error);
        }
    }

    /// <summary>Whether a change of <paramref name="propertyName"/> on a MAUI image source changes the image.</summary>
    internal static bool AffectsImage(string? propertyName) => propertyName is
        nameof(FileImageSource.File) or nameof(StreamImageSource.Stream)
        or nameof(UriImageSource.Uri) or nameof(UriImageSource.CachingEnabled) or nameof(UriImageSource.CacheValidity)
        or nameof(FontImageSource.Glyph) or nameof(FontImageSource.FontFamily) or nameof(FontImageSource.Size) or nameof(FontImageSource.Color)
        or nameof(FontImageSource.FontAutoScalingEnabled);
}

/// <summary>A source that could not be mapped: loading it reports the error.</summary>
internal sealed record SkUiFailedImageSource(Exception Error) : SkUiImageSource
{
    internal override string? CacheKey => null;

    internal override Task<SkUiDecodedImage> LoadAsync(SkUiImageLoadContext context, CancellationToken token) =>
        Task.FromException<SkUiDecodedImage>(Error);
}
