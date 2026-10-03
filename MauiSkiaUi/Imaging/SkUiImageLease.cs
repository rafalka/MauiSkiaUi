namespace MauiSkiaUi;

/// <summary>
/// One view's reference on a <see cref="SkUiCachedImage"/>, held by its <see cref="SkUiImageSlot"/>. The slot releases it
/// when it shows something else; when the view is collected (views are not disposable), the
/// finalizer releases it, like a <see cref="System.Runtime.InteropServices.SafeHandle"/>, so the shared entry's frames are
/// freed once nothing else holds them. Cached frames are raster images, safe to free on the finalizer thread.
/// </summary>
internal sealed class SkUiImageLease
{
    private SkUiCachedImage? _image;

    /// <summary>Takes over the caller's reference on <paramref name="image"/>.</summary>
    public SkUiImageLease(SkUiCachedImage image) => _image = image;

    /// <summary>The leased entry (<c>null</c> once released).</summary>
    public SkUiCachedImage? Image => _image;

    /// <summary>Drops the reference now; later calls do nothing.</summary>
    public void Release()
    {
        Interlocked.Exchange(ref _image, null)?.Release();
        GC.SuppressFinalize(this);
    }

    ~SkUiImageLease() => Interlocked.Exchange(ref _image, null)?.Release();
}
