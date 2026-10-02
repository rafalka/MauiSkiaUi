using System.Text;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>How a view wants its image: caches to use, decode bounds and transformations (part of the cache key).</summary>
/// <param name="CacheType">Caches the load may use.</param>
/// <param name="DownsampleWidth">Decode at most this wide, in DIPs (0: no bound); the intrinsic size stays the source's.</param>
/// <param name="DownsampleHeight">Decode at most this high, in DIPs (0: no bound).</param>
/// <param name="Transformations">Applied in order to the decoded image; <c>null</c> or empty: none.</param>
internal readonly record struct SkUiImageOptions(
    SkUiImageCacheType CacheType = SkUiImageCacheType.All,
    double DownsampleWidth = 0,
    double DownsampleHeight = 0,
    IReadOnlyList<ISkUiImageTransformation>? Transformations = null);

/// <summary>A lease on a loaded image and where it came from (<see cref="SkUiImageOrigin.MemoryCache"/> for a cache hit).</summary>
internal readonly record struct SkUiImageLoad(SkUiCachedImage Image, SkUiImageOrigin Origin);

/// <summary>What a source needs while loading: the display scale, whether it may use the disk cache, decode bounds.</summary>
internal sealed class SkUiImageLoadContext(float displayScale, bool useDiskCache, SKSizeI maxPixels, string? sourceKey)
{
    /// <summary>Where the source read its bytes from; each source sets it while loading.</summary>
    public SkUiImageOrigin Origin { get; set; } = SkUiImageOrigin.Stream;

    /// <summary>The source's cache key (custom from <see cref="SkUiImageCache.CacheKeyFactory"/>, else its own); names its disk-cache file.</summary>
    public string? SourceKey { get; } = sourceKey;

    /// <summary>Pixels per DIP of the main display (picks density-specific MauiImage files and font raster size).</summary>
    public float DisplayScale { get; } = displayScale;

    /// <summary>Whether downloads may be read from and written to the disk cache.</summary>
    public bool UseDiskCache { get; } = useDiskCache;

    /// <summary>Decode bounds in pixels per axis (0: none).</summary>
    public SKSizeI MaxPixels { get; } = maxPixels;

    /// <summary>Decodes off the UI thread, a few images at a time so a list of images does not flood the thread pool.</summary>
    public async Task<SkUiDecodedImage> DecodeAsync(byte[] bytes, float sourceScale, CancellationToken token)
    {
        await SkUiImageLoader.DecodeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var bounds = MaxPixels;
            return await Task.Run(() => SkUiImageDecoder.Decode(bytes, sourceScale, bounds), token).ConfigureAwait(false);
        }
        finally
        {
            SkUiImageLoader.DecodeGate.Release();
        }
    }
}

/// <summary>
/// The image pipeline shared by both layers (N9): memory cache → one shared load per key → source (disk cache,
/// download, package, file, stream, font) → decode → transformations → memory cache. Returns leases
/// (<see cref="SkUiCachedImage"/>) the caller releases.
/// </summary>
internal static class SkUiImageLoader
{
    /// <summary>The largest encoded image accepted.</summary>
    internal const int MaxEncodedBytes = 32 * 1024 * 1024;

    internal static readonly SemaphoreSlim DecodeGate = new(Math.Max(2, Environment.ProcessorCount));
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Pending> InFlight = new(StringComparer.Ordinal);
    private static float _displayScale;
    private static int _decodes;

    /// <summary>Decodes and glyph renders so far (tests: a second view of a cached source adds none).</summary>
    internal static int LoadCount => Volatile.Read(ref _decodes);

    /// <summary>Pixels per DIP of the main display; resolved on first use from the UI thread, 1 without a platform.</summary>
    internal static float DisplayScale
    {
        get
        {
            var scale = Volatile.Read(ref _displayScale);
            if (scale > 0)
                return scale;
#if ANDROID || IOS || MACCATALYST || WINDOWS
            try
            {
                var density = DeviceDisplay.Current.MainDisplayInfo.Density;
                if (density > 0)
                    Volatile.Write(ref _displayScale, scale = (float)density);
            }
            catch (Exception)
            {
                // Not on the UI thread or no display yet: 1 for now, resolved again next time.
            }
            return scale > 0 ? scale : 1;
#else
            return 1;
#endif
        }
        set => Volatile.Write(ref _displayScale, value);
    }

    /// <summary>
    /// A lease on the image for <paramref name="source"/>. A memory-cache hit (and a font glyph) completes synchronously.
    /// Concurrent loads of one key share the work, which is cancelled only when every waiter cancelled.
    /// </summary>
    internal static Task<SkUiImageLoad> LoadAsync(SkUiImageSource source, in SkUiImageOptions options, CancellationToken token)
    {
        var load = Load(source, options, token, out var cached);
        if (load.IsCompletedSuccessfully)
            return Task.FromResult(new SkUiImageLoad(load.Result, cached ? SkUiImageOrigin.MemoryCache : load.Result.Origin));
        return WithOrigin(load);
    }

    private static async Task<SkUiImageLoad> WithOrigin(Task<SkUiCachedImage> load)
    {
        var image = await load.ConfigureAwait(false);
        return new SkUiImageLoad(image, image.Origin);
    }

    private static Task<SkUiCachedImage> Load(SkUiImageSource source, in SkUiImageOptions options, CancellationToken token, out bool cached)
    {
        cached = false;
        var scale = DisplayScale;
        var maxPixels = new SKSizeI(Pixels(options.DownsampleWidth, scale), Pixels(options.DownsampleHeight, scale));
        string? sourceKey;
        try
        {
            sourceKey = SourceKey(source);
        }
        catch (Exception error)
        {
            return Task.FromException<SkUiCachedImage>(new InvalidOperationException("SkUiImageCache.CacheKeyFactory failed.", error));
        }
        var context = new SkUiImageLoadContext(scale, options.CacheType is SkUiImageCacheType.All or SkUiImageCacheType.Disk, maxPixels, sourceKey);
        var key = options.CacheType is SkUiImageCacheType.All or SkUiImageCacheType.Memory ? KeyFor(sourceKey, options, maxPixels) : null;
        if (key is null)
            return LoadCoreAsync(source, options.Transformations, context, null, token);
        if (SkUiImageMemoryCache.TryGet(key) is { } hit)
        {
            cached = true;
            return Task.FromResult(hit);
        }
        return LoadSharedAsync(source, options.Transformations, context, key, token);
    }

    private static int Pixels(double dips, float scale) =>
        dips > 0 && double.IsFinite(dips) ? Math.Max(1, (int)Math.Ceiling(dips * scale)) : 0;

    /// <summary>
    /// The source's cache identity: the app's <see cref="SkUiImageCache.CacheKeyFactory"/> key (kept apart from the
    /// built-in keys by a prefix), else the source's own key; <c>null</c>: not cacheable.
    /// </summary>
    internal static string? SourceKey(SkUiImageSource source)
    {
        if (source is SkUiFailedImageSource)
            return null;
        var custom = SkUiImageCache.CacheKeyFactory?.Invoke(source);
        return string.IsNullOrEmpty(custom) ? source.CacheKey : "key:" + custom;
    }

    /// <summary>The memory-cache key: the source's key, then the decode bounds and transformation keys.</summary>
    internal static string? KeyFor(string? sourceKey, in SkUiImageOptions options, SKSizeI maxPixels)
    {
        if (sourceKey is null)
            return null;
        var key = new StringBuilder(sourceKey.Length + 32)
            .Append(sourceKey).Append(SkUiImageMemoryCache.OptionsSeparator)
            .Append(maxPixels.Width).Append('x').Append(maxPixels.Height).Append('/').Append(SkUiImageDecoder.MaxDecodeDimension);
        if (options.Transformations is { Count: > 0 } transformations)
        {
            foreach (var transformation in transformations)
                key.Append(SkUiImageMemoryCache.OptionsSeparator).Append(transformation.Key);
        }
        return key.ToString();
    }

    private static async Task<SkUiCachedImage> LoadSharedAsync(SkUiImageSource source, IReadOnlyList<ISkUiImageTransformation>? transformations,
        SkUiImageLoadContext context, string key, CancellationToken token)
    {
        Pending pending;
        var created = false;
        lock (Gate)
        {
            if (!InFlight.TryGetValue(key, out pending!))
            {
                InFlight[key] = pending = new Pending(key);
                created = true;
            }
            pending.Waiters++;
        }
        if (created)
            _ = RunAsync(pending, LoadCoreAsync(source, transformations, context, key, pending.Cancellation.Token));

        SkUiCachedImage image;
        try
        {
            image = await pending.Completion.Task.WaitAsync(token).ConfigureAwait(false);
        }
        catch
        {
            Leave(pending, acquire: false);
            throw;
        }
        Leave(pending, acquire: true);
        return image;
    }

    /// <summary>Publishes the shared load; while waiters remain, the result keeps a reference of its own for them.</summary>
    private static async Task RunAsync(Pending pending, Task<SkUiCachedImage> load)
    {
        try
        {
            var image = await load.ConfigureAwait(false);
            lock (Gate)
            {
                Finish(pending);
                pending.Result = image;
                if (pending.Waiters == 0)
                    image.Release(); // everyone cancelled meanwhile; the memory cache may still hold it
            }
            pending.Completion.TrySetResult(image);
        }
        catch (OperationCanceledException)
        {
            lock (Gate)
                Finish(pending);
            pending.Completion.TrySetCanceled();
        }
        catch (Exception error)
        {
            lock (Gate)
                Finish(pending);
            pending.Completion.TrySetException(error);
        }
    }

    private static void Finish(Pending pending)
    {
        pending.Done = true;
        if (InFlight.TryGetValue(pending.Key, out var current) && ReferenceEquals(current, pending))
            InFlight.Remove(pending.Key);
        pending.Cancellation.Dispose();
    }

    private static void Leave(Pending pending, bool acquire)
    {
        lock (Gate)
        {
            if (acquire)
                pending.Result!.TryAcquire(); // cannot fail: the shared reference lives until the last waiter leaves
            if (--pending.Waiters > 0)
                return;
            if (!pending.Done)
            {
                pending.Cancellation.Cancel(); // nobody wants it any more
                InFlight.Remove(pending.Key);
            }
            else
            {
                pending.Result?.Release();
            }
        }
    }

    private static async Task<SkUiCachedImage> LoadCoreAsync(SkUiImageSource source, IReadOnlyList<ISkUiImageTransformation>? transformations,
        SkUiImageLoadContext context, string? key, CancellationToken token)
    {
        var loading = source.LoadAsync(context, token);
        var synchronous = loading.IsCompleted;
        var decoded = await loading.ConfigureAwait(false);
        Interlocked.Increment(ref _decodes);
        if (transformations is { Count: > 0 })
        {
            var input = decoded;
            // Glyphs stay synchronous; decoded bitmaps are transformed off the UI thread. Either way Transform
            // consumes the input, also when it fails.
            decoded = synchronous
                ? Transform(input, transformations)
                : await Task.Run(() => Transform(input, transformations), CancellationToken.None).ConfigureAwait(false);
        }
        if (token.IsCancellationRequested)
        {
            Dispose(decoded.Frames);
            token.ThrowIfCancellationRequested();
        }
        var image = new SkUiCachedImage(decoded, key, origin: context.Origin);
        SkUiImageMemoryCache.Add(image);
        return image;
    }

    private static void Dispose(SKImage?[] frames)
    {
        foreach (var frame in frames)
            frame?.Dispose();
    }

    /// <summary>
    /// Applies <paramref name="transformations"/> to every frame. Consumes <paramref name="decoded"/>: its frames are
    /// disposed when replaced, and all of them when a transformation fails.
    /// </summary>
    internal static SkUiDecodedImage Transform(SkUiDecodedImage decoded, IReadOnlyList<ISkUiImageTransformation> transformations)
    {
        var first = decoded.Frames[0];
        var pixelsPerDip = (float)(first.Width / decoded.Size.Width);
        var dipsPerPixel = new SKSize((float)(decoded.Size.Width / first.Width), (float)(decoded.Size.Height / first.Height));
        var inputs = (SKImage?[])decoded.Frames.Clone();
        var frames = new SKImage?[inputs.Length];
        try
        {
            for (var index = 0; index < frames.Length; index++)
            {
                var current = inputs[index]!;
                inputs[index] = null;
                try
                {
                    foreach (var transformation in transformations)
                    {
                        var next = transformation.Transform(current, pixelsPerDip) ?? throw new InvalidOperationException(
                            $"Image transformation '{transformation.Key}' returned null.");
                        if (!ReferenceEquals(next, current))
                            current.Dispose();
                        current = next;
                    }
                }
                catch
                {
                    current.Dispose();
                    throw;
                }
                frames[index] = current;
            }
            return decoded.WithFrames(frames!, dipsPerPixel);
        }
        catch
        {
            Dispose(frames);
            Dispose(inputs);
            throw;
        }
    }

    /// <summary>Reads and disposes <paramref name="stream"/>; at most <see cref="MaxEncodedBytes"/>.</summary>
    internal static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken token)
    {
        await using (stream.ConfigureAwait(false))
        {
            using var bytes = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
            {
                if (bytes.Length + read > MaxEncodedBytes)
                    throw new InvalidDataException("Image exceeds the 32 MiB encoded limit.");
                bytes.Write(buffer, 0, read);
            }
            return bytes.ToArray();
        }
    }

    /// <summary>Downloads an image; a non-success status or a text / JSON response fails.</summary>
    internal static async Task<byte[]> DownloadAsync(Uri uri, CancellationToken token)
    {
        using var response = await SkUiImageCache.HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null && (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException($"The response is not an image ({mediaType}).");
        var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        return await ReadAllAsync(stream, token).ConfigureAwait(false);
    }

    private sealed class Pending(string key)
    {
        public string Key { get; } = key;
        public CancellationTokenSource Cancellation { get; } = new();
        public TaskCompletionSource<SkUiCachedImage> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Waiters;
        public bool Done;
        public SkUiCachedImage? Result;
    }
}
