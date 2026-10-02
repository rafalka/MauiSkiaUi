using System.Security.Cryptography;
using System.Text;

namespace MauiSkiaUi;

/// <summary>
/// Downloaded image bytes on disk (<see cref="SkUiImageCache.DiskCacheDirectory"/>), one file per URI named by its
/// SHA-256. A file is valid for the requesting source's validity after it was written (MAUI's <c>CacheValidity</c>
/// rule). Writes go to a temporary file that replaces the entry at once, so readers never see a partial file. Past
/// <see cref="SkUiImageCache.DiskCacheMaxBytes"/> the oldest files are removed.
/// </summary>
internal static class SkUiImageDiskCache
{
    private const string Extension = ".img";
    private static readonly object Gate = new();
    private static long _bytes = -1; // unknown until the folder is first measured
    private static int _trimming;

    /// <summary>The cached bytes for <paramref name="key"/> when written less than <paramref name="validity"/> ago; else <c>null</c>.</summary>
    internal static async Task<byte[]?> TryReadAsync(string key, TimeSpan validity, CancellationToken token)
    {
        if (SkUiImageCache.DiskCacheMaxBytes <= 0)
            return null;
        var path = PathFor(key);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return null;
            if (DateTime.UtcNow - info.LastWriteTimeUtc >= validity)
            {
                Delete(path);
                return null;
            }
            return await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null; // replaced or removed meanwhile: download instead
        }
    }

    /// <summary>Stores <paramref name="bytes"/> for <paramref name="key"/> in the background; failures only skip caching.</summary>
    internal static void Write(string key, byte[] bytes)
    {
        if (SkUiImageCache.DiskCacheMaxBytes <= 0)
            return;
        _ = Task.Run(() =>
        {
            try
            {
                var path = PathFor(key);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllBytes(temporary, bytes);
                long replaced = 0;
                try { replaced = new FileInfo(path).Length; }
                catch (FileNotFoundException) { }
                File.Move(temporary, path, overwrite: true);
                lock (Gate)
                {
                    if (_bytes >= 0)
                        _bytes += bytes.Length - replaced;
                }
                TrimIfNeeded();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Cache folder unavailable or full: the image still shows, it is only not cached.
            }
        });
    }

    /// <summary>The folder changed: its size is measured again on the next write.</summary>
    internal static void Reset()
    {
        lock (Gate)
            _bytes = -1;
    }

    /// <summary>The bytes of the cached files (and refreshes the running total the trimming uses).</summary>
    internal static Task<long> MeasureAsync() => Task.Run(() =>
    {
        long total = 0;
        try
        {
            var directory = new DirectoryInfo(SkUiImageCache.DiskCacheDirectory);
            if (directory.Exists)
            {
                foreach (var file in directory.EnumerateFiles("*" + Extension))
                    total += file.Length;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return 0L;
        }
        lock (Gate)
            _bytes = total;
        return total;
    });

    internal static Task RemoveAsync(string key) => Task.Run(() => Delete(PathFor(key)));

    internal static Task ClearAsync() => Task.Run(() =>
    {
        var directory = SkUiImageCache.DiskCacheDirectory;
        if (!Directory.Exists(directory))
            return;
        // Only the cache's own files (and leftover temporary writes): the folder is configurable and may hold others.
        foreach (var file in Directory.EnumerateFiles(directory, "*" + Extension))
            Delete(file);
        foreach (var file in Directory.EnumerateFiles(directory, "*" + Extension + ".*.tmp"))
            Delete(file);
        lock (Gate)
            _bytes = 0;
    });

    private static string PathFor(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(SkUiImageCache.DiskCacheDirectory, hash + Extension);
    }

    private static void Delete(string path)
    {
        try
        {
            var length = new FileInfo(path).Length;
            File.Delete(path);
            lock (Gate)
            {
                if (_bytes >= 0)
                    _bytes -= length;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Removes the oldest files until the folder is under 80% of the budget (one trim at a time).</summary>
    private static void TrimIfNeeded()
    {
        var max = SkUiImageCache.DiskCacheMaxBytes;
        lock (Gate)
        {
            if (_bytes >= 0 && _bytes <= max)
                return;
        }
        if (Interlocked.Exchange(ref _trimming, 1) == 1)
            return;
        try
        {
            var directory = new DirectoryInfo(SkUiImageCache.DiskCacheDirectory);
            if (!directory.Exists)
                return;
            var files = directory.GetFiles("*" + Extension);
            long total = 0;
            foreach (var file in files)
                total += file.Length;
            if (total > max)
            {
                Array.Sort(files, static (a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                var target = max / 10 * 8;
                foreach (var file in files)
                {
                    if (total <= target)
                        break;
                    try
                    {
                        var length = file.Length;
                        file.Delete();
                        total -= length;
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
                }
            }
            lock (Gate)
                _bytes = total;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        finally
        {
            Volatile.Write(ref _trimming, 0);
        }
    }
}
