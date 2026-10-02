namespace MauiSkiaUi;

/// <summary>Which caches an image load may use (FFImageLoading's <c>CacheType</c>).</summary>
public enum SkUiImageCacheType
{
    /// <summary>Decoded images in memory and downloads on disk (default).</summary>
    All,

    /// <summary>Decoded images in memory only; downloads are not kept on disk.</summary>
    Memory,

    /// <summary>Downloads on disk only; every view decodes its own copy.</summary>
    Disk,

    /// <summary>No caching: download and decode for each load.</summary>
    None
}

/// <summary>
/// The image caches shared by SkUi* and Core images, image buttons and slider thumbs:
/// <list type="bullet">
/// <item><b>Memory:</b> decoded images, least recently used first out, keyed by source, decode size and
/// transformations. A second view of the same source decodes nothing; images still shown stay alive after eviction
/// until their views let go. Cleared when the OS reports low memory (Android, iOS, Mac Catalyst).</item>
/// <item><b>Disk:</b> downloaded bytes of HTTP(S) sources with caching enabled (MAUI's <c>UriImageSource.CachingEnabled</c> /
/// <c>CacheValidity</c>), in the app's cache folder; oldest files are removed past <see cref="DiskCacheMaxBytes"/>.</item>
/// </list>
/// Concurrent loads of the same image share one download and decode. Settings are read when a load starts.
/// </summary>
public static class SkUiImageCache
{
    private static long _diskCacheMaxBytes = 100L * 1024 * 1024;
    private static string? _diskCacheDirectory;
    private static HttpClient? _httpClient;
    private static Func<SkUiImageSource, string?>? _cacheKeyFactory;

    /// <summary>
    /// The decoded-image budget in bytes (4 per pixel). Default: an eighth of the memory available to the process,
    /// between 32 and 256 MiB. Lowering it evicts at once; 0 disables the memory cache.
    /// </summary>
    public static long MemoryCacheMaxBytes
    {
        get => SkUiImageMemoryCache.MaxBytes;
        set => SkUiImageMemoryCache.MaxBytes = Math.Max(0, value);
    }

    /// <summary>Decoded bytes currently in the memory cache.</summary>
    public static long MemoryCacheBytes => SkUiImageMemoryCache.Bytes;

    /// <summary>Images currently in the memory cache.</summary>
    public static int MemoryCacheCount => SkUiImageMemoryCache.Count;

    /// <summary>Empties the memory cache (views keep showing their images).</summary>
    public static void ClearMemoryCache() => SkUiImageMemoryCache.Clear();

    /// <summary>
    /// Folder of the download cache. Default: <c>SkiaUi.Images</c> in the app's cache directory
    /// (<c>FileSystem.CacheDirectory</c>; the temporary folder where there is none). Set it before images load.
    /// </summary>
    public static string DiskCacheDirectory
    {
        get => Volatile.Read(ref _diskCacheDirectory) ?? DefaultDiskCacheDirectory();
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            Volatile.Write(ref _diskCacheDirectory, value);
            SkUiImageDiskCache.Reset();
        }
    }

    /// <summary>Download cache budget in bytes (default 100 MiB); past it the oldest files are removed. 0 disables the disk cache.</summary>
    public static long DiskCacheMaxBytes
    {
        get => Volatile.Read(ref _diskCacheMaxBytes);
        set => Volatile.Write(ref _diskCacheMaxBytes, Math.Max(0, value));
    }

    /// <summary>Removes every downloaded file from the disk cache.</summary>
    public static Task ClearDiskCacheAsync() => SkUiImageDiskCache.ClearAsync();

    /// <summary>
    /// Empties the caches in <paramref name="caches"/> (default: memory and disk), e.g. from a "Clear cache" setting or
    /// at sign-out. Views keep showing their images; the next load of a source decodes and downloads it again. A
    /// download finishing during the call may still be stored afterwards.
    /// </summary>
    public static Task ClearAsync(SkUiImageCacheType caches = SkUiImageCacheType.All)
    {
        if (caches is SkUiImageCacheType.All or SkUiImageCacheType.Memory)
            ClearMemoryCache();
        return caches is SkUiImageCacheType.All or SkUiImageCacheType.Disk ? ClearDiskCacheAsync() : Task.CompletedTask;
    }

    /// <summary>The size of the downloaded files in the disk cache, in bytes (measured on a worker thread).</summary>
    public static Task<long> GetDiskCacheBytesAsync() => SkUiImageDiskCache.MeasureAsync();

    /// <summary>
    /// Forgets <paramref name="source"/> (by its key, so with a <see cref="CacheKeyFactory"/> every source sharing that
    /// key): its decoded images in memory (every decode size and transformation) and, for a URI, its downloaded file,
    /// so the next load fetches it again. Views showing it keep their image until reloaded.
    /// </summary>
    public static Task RemoveAsync(SkUiImageSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (SkUiImageLoader.SourceKey(source) is not { } key)
            return Task.CompletedTask;
        SkUiImageMemoryCache.RemoveSource(key);
        return source is SkUiUriImageSource ? SkUiImageDiskCache.RemoveAsync(key) : Task.CompletedTask;
    }

    /// <summary>Forgets the image at <paramref name="uri"/> (see <see cref="RemoveAsync(SkUiImageSource)"/>).</summary>
    public static Task RemoveAsync(Uri uri) => RemoveAsync(SkUiImageSource.FromUri(uri));

    /// <summary>
    /// App-wide cache identity of image sources: returns the key under which a source's image is cached, or <c>null</c>
    /// for the default (the URI, file path, stream key or glyph settings). Sources with the same key share one cache
    /// entry in memory (per decode size and transformations) and one download on disk, so URLs that differ only in an
    /// access token or a signature can hit the cache:
    /// <code>SkUiImageCache.CacheKeyFactory = SkUiImageCacheKeys.IgnoreQueryParameters("token", "sig");</code>
    /// A key for a stream source makes it cacheable. Called on the thread that starts the load (usually the UI
    /// thread) for every load: keep it fast and thread-safe; an exception fails the load (<c>LoadError</c>). Set it at
    /// startup: images already cached keep the keys they were stored under.
    /// </summary>
    public static Func<SkUiImageSource, string?>? CacheKeyFactory
    {
        get => Volatile.Read(ref _cacheKeyFactory);
        set => Volatile.Write(ref _cacheKeyFactory, value);
    }

    /// <summary>The client that downloads HTTP(S) images (default: a shared <see cref="System.Net.Http.HttpClient"/>).</summary>
    public static HttpClient HttpClient
    {
        get => Volatile.Read(ref _httpClient) ?? LazyInitializer.EnsureInitialized(ref _httpClient, () => new HttpClient());
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Volatile.Write(ref _httpClient, value);
        }
    }

    private static string DefaultDiskCacheDirectory()
    {
        string root;
        try
        {
            root = FileSystem.Current.CacheDirectory;
        }
        catch (Exception)
        {
            root = Path.GetTempPath(); // no MAUI platform (headless)
        }
        var directory = Path.Combine(root, "SkiaUi.Images");
        Interlocked.CompareExchange(ref _diskCacheDirectory, directory, null);
        return Volatile.Read(ref _diskCacheDirectory)!;
    }
}

/// <summary>The decoded-image LRU behind <see cref="SkUiImageCache"/>; thread-safe.</summary>
internal static class SkUiImageMemoryCache
{
    /// <summary>Separates a source's key from the decode options in full cache keys.</summary>
    internal const char OptionsSeparator = '\u001F';

    private static readonly object Gate = new();
    private static readonly Dictionary<string, LinkedListNode<SkUiCachedImage>> Entries = new(StringComparer.Ordinal);
    private static readonly LinkedList<SkUiCachedImage> Recency = new(); // most recent first
    private static long _maxBytes = DefaultMaxBytes();
    private static long _bytes;
    private static bool _pressureRegistered;

    internal static long MaxBytes
    {
        get => Volatile.Read(ref _maxBytes);
        set
        {
            lock (Gate)
            {
                _maxBytes = value;
                Trim(value);
            }
        }
    }

    internal static long Bytes
    {
        get { lock (Gate) return _bytes; }
    }

    internal static int Count
    {
        get { lock (Gate) return Entries.Count; }
    }

    /// <summary>A lease on the cached image for <paramref name="key"/>, or <c>null</c>.</summary>
    internal static SkUiCachedImage? TryGet(string key)
    {
        lock (Gate)
        {
            if (!Entries.TryGetValue(key, out var node) || !node.Value.TryAcquire())
                return null;
            Recency.Remove(node);
            Recency.AddFirst(node);
            return node.Value;
        }
    }

    /// <summary>Caches <paramref name="image"/> (taking a reference of its own) unless it alone exceeds the budget.</summary>
    internal static void Add(SkUiCachedImage image)
    {
        if (image.Key is not { } key)
            return;
        lock (Gate)
        {
            if (image.Bytes > _maxBytes || !image.TryAcquire())
                return;
            if (Entries.Remove(key, out var old))
                Evict(old);
            var node = Recency.AddFirst(image);
            Entries[key] = node;
            _bytes += image.Bytes;
            Trim(_maxBytes);
            if (!_pressureRegistered)
            {
                _pressureRegistered = true;
                SkUiImageMemoryPressure.Register(Clear);
            }
        }
    }

    internal static void Clear()
    {
        lock (Gate)
            Trim(0);
    }

    /// <summary>Removes every entry of one source (all decode sizes and transformations).</summary>
    internal static void RemoveSource(string sourceKey)
    {
        var prefix = sourceKey + OptionsSeparator;
        lock (Gate)
        {
            for (var node = Recency.First; node is not null;)
            {
                var next = node.Next;
                if (node.Value.Key is { } key && (key == sourceKey || key.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    Entries.Remove(key);
                    Evict(node);
                }
                node = next;
            }
        }
    }

    private static void Trim(long budget)
    {
        while (_bytes > budget && Recency.Last is { } oldest)
        {
            Entries.Remove(oldest.Value.Key!);
            Evict(oldest);
        }
    }

    private static void Evict(LinkedListNode<SkUiCachedImage> node)
    {
        Recency.Remove(node);
        _bytes -= node.Value.Bytes;
        node.Value.Release();
    }

    private static long DefaultMaxBytes()
    {
        const long MiB = 1024 * 1024;
        long available;
        try
        {
            available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        }
        catch (Exception)
        {
            available = 0;
        }
        return available <= 0 ? 64 * MiB : Math.Clamp(available / 8, 32 * MiB, 256 * MiB);
    }
}

/// <summary>Clears the memory cache when the OS reports memory pressure.</summary>
internal static class SkUiImageMemoryPressure
{
#if IOS || MACCATALYST
    private static Foundation.NSObject? _observer; // kept for the app's lifetime
#endif

    internal static void Register(Action clear)
    {
        try
        {
#if ANDROID
            Android.App.Application.Context.RegisterComponentCallbacks(new TrimCallbacks(clear));
#elif IOS || MACCATALYST
            _observer = UIKit.UIApplication.Notifications.ObserveDidReceiveMemoryWarning((_, _) => clear());
#endif
        }
        catch (Exception)
        {
            // No platform application (tests, design time): nothing to listen to.
        }
    }

#if ANDROID
    private sealed class TrimCallbacks(Action clear) : Java.Lang.Object, Android.Content.IComponentCallbacks2
    {
        public void OnTrimMemory(Android.Content.TrimMemory level)
        {
            // Running low, or backgrounded (UI hidden and later): give the unused decoded images back.
            if (level >= Android.Content.TrimMemory.RunningLow)
                clear();
        }

        public void OnLowMemory() => clear();

        public void OnConfigurationChanged(Android.Content.Res.Configuration newConfig) { }
    }
#endif
}
