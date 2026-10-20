namespace MauiSkiaUi;

/// <summary>Change tracking of MAUI's image sources for the SkUi* controls (the conversion is <see cref="SkUiImageSource.FromMaui"/>).</summary>
internal static class SkUiMauiImageSources
{
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
