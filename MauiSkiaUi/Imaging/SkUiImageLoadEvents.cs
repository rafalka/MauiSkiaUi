namespace MauiSkiaUi;

/// <summary>Where a loaded image came from (FFImageLoading's <c>LoadingResult</c>).</summary>
public enum SkUiImageOrigin
{
    /// <summary>Already decoded in the memory cache (no I/O, no decode).</summary>
    MemoryCache,

    /// <summary>A download read back from the disk cache.</summary>
    DiskCache,

    /// <summary>Downloaded over HTTP(S).</summary>
    Network,

    /// <summary>A <c>MauiImage</c>: the file Resizetizer made for the display density.</summary>
    MauiImage,

    /// <summary>A file in the app package (a <c>MauiAsset</c> in Resources/Raw).</summary>
    PackageAsset,

    /// <summary>A file by absolute path.</summary>
    File,

    /// <summary>A stream source.</summary>
    Stream,

    /// <summary>A font glyph drawn by the text engine.</summary>
    Font
}

/// <summary>How an image load ended.</summary>
public enum SkUiImageLoadStatus
{
    /// <summary>The image is shown.</summary>
    Succeeded,

    /// <summary>The load failed: <see cref="SkUiImageLoadFinishedEventArgs.Error"/> says why; the error placeholder shows.</summary>
    Failed,

    /// <summary>The load was replaced (a new source, a cleared or disposed view) before it finished.</summary>
    Cancelled
}

/// <summary>An image load started (<c>LoadingStarted</c>).</summary>
public sealed class SkUiImageLoadStartedEventArgs(SkUiImageSource source) : EventArgs
{
    /// <summary>The source being loaded (on SkUi* views, converted from MAUI's <c>ImageSource</c>).</summary>
    public SkUiImageSource Source { get; } = source;
}

/// <summary>
/// An image load ended (<c>LoadingFinished</c>): raised exactly once for every <c>LoadingStarted</c>, on the UI thread,
/// after the view's state (<c>IsLoading</c>, <c>LoadError</c>, <c>ImageSize</c>) is updated.
/// </summary>
public sealed class SkUiImageLoadFinishedEventArgs(SkUiImageSource source, SkUiImageLoadStatus status, SkUiImageOrigin? origin,
    Exception? error, Size imageSize, TimeSpan elapsed) : EventArgs
{
    /// <summary>The source that was loaded.</summary>
    public SkUiImageSource Source { get; } = source;

    /// <summary>Succeeded, failed or cancelled.</summary>
    public SkUiImageLoadStatus Status { get; } = status;

    /// <summary>Whether the image is shown (<see cref="Status"/> is <see cref="SkUiImageLoadStatus.Succeeded"/>).</summary>
    public bool IsSuccess => Status == SkUiImageLoadStatus.Succeeded;

    /// <summary>Where the image came from; <c>null</c> unless it succeeded. Views that joined another view's load of the same image report that load's origin.</summary>
    public SkUiImageOrigin? Origin { get; } = origin;

    /// <summary>Why it failed, or <c>null</c>.</summary>
    public Exception? Error { get; } = error;

    /// <summary>The loaded image's intrinsic size in DIPs (zero unless it succeeded).</summary>
    public Size ImageSize { get; } = imageSize;

    /// <summary>Time from the start of the load to its end.</summary>
    public TimeSpan Elapsed { get; } = elapsed;
}
