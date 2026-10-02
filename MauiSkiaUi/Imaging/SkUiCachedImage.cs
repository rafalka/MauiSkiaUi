using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A decoded image shared by the views that show it and the memory cache, reference counted: the cache holds one
/// reference while the entry is cached, each view one lease (<see cref="TryAcquire"/> / <see cref="Release"/>). The
/// frames are disposed when the last reference goes; pictures already recorded keep their own native references, so
/// a frame being drawn on the render thread stays valid.
/// </summary>
internal sealed class SkUiCachedImage
{
    private bool _ownsFrames;
    private int _references;

    /// <summary>A new entry with one reference (the caller's).</summary>
    public SkUiCachedImage(SkUiDecodedImage decoded, string? key, bool ownsFrames = true, SkUiImageOrigin origin = SkUiImageOrigin.Stream)
    {
        Origin = origin;
        Frames = decoded.Frames;
        Durations = decoded.Durations;
        Size = decoded.Size;
        Bytes = decoded.Bytes;
        Key = key;
        _ownsFrames = ownsFrames;
        _references = 1;
        if (Durations.Length > 1)
        {
            foreach (var duration in Durations)
                TotalDuration += duration;
        }
    }

    /// <summary>Where the image was loaded from when it was decoded.</summary>
    public SkUiImageOrigin Origin { get; }

    /// <summary>Frames to draw; one for a still image.</summary>
    public SKImage[] Frames { get; }

    /// <summary>Frame durations in milliseconds (empty for a still image).</summary>
    public int[] Durations { get; }

    /// <summary>One loop of an animation in milliseconds (0 for a still image).</summary>
    public int TotalDuration { get; }

    /// <summary>Whether there is more than one frame to play.</summary>
    public bool IsAnimated => Frames.Length > 1;

    /// <summary>Intrinsic size in DIPs.</summary>
    public Size Size { get; }

    /// <summary>Decoded pixel memory.</summary>
    public long Bytes { get; }

    /// <summary>The memory-cache key, or <c>null</c> when not cacheable.</summary>
    public string? Key { get; }

    /// <summary>Whether the frames were disposed (no references left).</summary>
    public bool IsReleased => Volatile.Read(ref _references) <= 0;

    /// <summary>Adds a reference unless the entry was already released (then the frames are gone).</summary>
    public bool TryAcquire()
    {
        while (true)
        {
            var current = Volatile.Read(ref _references);
            if (current <= 0)
                return false;
            if (Interlocked.CompareExchange(ref _references, current + 1, current) == current)
                return true;
        }
    }

    /// <summary>Keeps the frames alive when the last reference goes (another entry took them over).</summary>
    public void Disown() => _ownsFrames = false;

    /// <summary>Drops a reference; the last one disposes the frames (when owned).</summary>
    public void Release()
    {
        if (Interlocked.Decrement(ref _references) != 0 || !_ownsFrames)
            return;
        foreach (var frame in Frames)
            frame.Dispose();
    }

    /// <summary>The frame shown at <paramref name="milliseconds"/> into the loop.</summary>
    public int FrameAt(long milliseconds)
    {
        if (TotalDuration <= 0)
            return 0;
        var time = milliseconds % TotalDuration;
        for (var index = 0; index < Durations.Length; index++)
        {
            time -= Durations[index];
            if (time < 0)
                return index;
        }
        return Durations.Length - 1;
    }
}
