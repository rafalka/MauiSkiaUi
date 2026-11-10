using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// The path operations a border's outline needs (the content clip inside the stroke, the shadow silhouette grown by it),
/// shared by every border whose outline has the same points: a path operation on a shaped outline costs about 1.5 ms on a
/// desktop CPU, so a list of 400 equal ticket-shaped cards spent most of its first frame computing the same clip. Keyed by
/// the outline's contents, not the shape object, so cards that each make their own shape (the usual case) share too.
/// </summary>
/// <remarks>
/// The cache owns its results and disposes them when they are evicted; callers get a copy they own (a Skia path copy shares
/// its points with the original, so it is cheap, and the compositor disposes committed copies as it does any other clip).
/// Thread-safe: borders compute their geometry on their surface's UI thread, and tests run surfaces in parallel.
/// </remarks>
internal static class SkUiStrokedOutlines
{
    /// <summary>Distinct outlines kept (most recently used first); a page rarely has more shaped border sizes.</summary>
    internal const int Capacity = 32;

    private static readonly Lock Gate = new();
    private static readonly List<Entry> Entries = new(Capacity);

    private enum Kind : byte
    {
        Inside,
        Grown
    }

    private sealed class Entry(SKPath outline, int hash, float thickness, Kind kind, SKPath result)
    {
        public SKPath Outline { get; } = outline;
        public int Hash { get; } = hash;
        public float Thickness { get; } = thickness;
        public Kind Kind { get; } = kind;
        public SKPath Result { get; } = result;
    }

    /// <summary>Path operations computed (not found in the cache), for tests.</summary>
    internal static int Computed { get; private set; }

    /// <summary>Outlines held, for tests.</summary>
    internal static int Count
    {
        get
        {
            lock (Gate)
                return Entries.Count;
        }
    }

    /// <summary>
    /// The area of <paramref name="outline"/> inside a stroke of <paramref name="thickness"/> centered on it (round joins):
    /// the content clip of a border. A new path the caller owns.
    /// </summary>
    public static SKPath Inside(SKPath outline, float thickness) => Get(outline, thickness, Kind.Inside);

    /// <summary>
    /// <paramref name="outline"/> grown to the outer edge of a stroke of <paramref name="thickness"/> centered on it (miter
    /// joins): the shadow silhouette of a border with an opaque stroke. A new path the caller owns.
    /// </summary>
    public static SKPath Grown(SKPath outline, float thickness) => Get(outline, thickness, Kind.Grown);

    /// <summary>Disposes every cached path (tests).</summary>
    internal static void Clear()
    {
        lock (Gate)
        {
            foreach (var entry in Entries)
                Dispose(entry);
            Entries.Clear();
            Computed = 0;
        }
    }

    private static SKPath Get(SKPath outline, float thickness, Kind kind)
    {
        var hash = Hash(outline);
        lock (Gate)
        {
            for (var index = 0; index < Entries.Count; index++)
            {
                var entry = Entries[index];
                // Exact comparisons: a cache key, the same outline gives the same result.
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (entry.Hash != hash || entry.Kind != kind || entry.Thickness != thickness || !SamePath(entry.Outline, outline))
                    continue;
                if (index > 0)
                {
                    Entries.RemoveAt(index);
                    Entries.Insert(0, entry);
                }
                return new SKPath(entry.Result);
            }
        }

        // Computed outside the lock: other surfaces' threads need not wait for it.
        var result = Compute(outline, thickness, kind);
        lock (Gate)
        {
            Computed++;
            if (Entries.Count == Capacity)
            {
                Dispose(Entries[^1]);
                Entries.RemoveAt(Entries.Count - 1);
            }
            Entries.Insert(0, new Entry(new SKPath(outline), hash, thickness, kind, result));
            return new SKPath(result);
        }
    }

    private static SKPath Compute(SKPath outline, float thickness, Kind kind)
    {
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = thickness };
        if (kind == Kind.Inside)
            paint.StrokeJoin = SKStrokeJoin.Round;
        using var strokeArea = paint.GetFillPath(outline);
        return outline.Op(strokeArea, kind == Kind.Inside ? SKPathOp.Difference : SKPathOp.Union) ?? new SKPath(outline);
    }

    private static void Dispose(Entry entry)
    {
        entry.Outline.Dispose();
        entry.Result.Dispose();
    }

    private static int Hash(SKPath path)
    {
        var hash = new HashCode();
        hash.Add(path.FillType);
        using var iterator = path.CreateRawIterator();
        Span<SKPoint> points = stackalloc SKPoint[4];
        SKPathVerb verb;
        while ((verb = iterator.Next(points)) != SKPathVerb.Done)
        {
            hash.Add(verb);
            var count = PointCount(verb);
            for (var index = 0; index < count; index++)
                hash.Add(points[index]);
        }
        return hash.ToHashCode();
    }

    private static bool SamePath(SKPath a, SKPath b)
    {
        if (a.FillType != b.FillType || a.PointCount != b.PointCount || a.VerbCount != b.VerbCount)
            return false;
        using var first = a.CreateRawIterator();
        using var second = b.CreateRawIterator();
        Span<SKPoint> pointsA = stackalloc SKPoint[4];
        Span<SKPoint> pointsB = stackalloc SKPoint[4];
        while (true)
        {
            var verb = first.Next(pointsA);
            if (verb != second.Next(pointsB))
                return false;
            if (verb == SKPathVerb.Done)
                return true;
            if (!pointsA[..PointCount(verb)].SequenceEqual(pointsB[..PointCount(verb)]))
                return false;
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (verb == SKPathVerb.Conic && first.ConicWeight() != second.ConicWeight())
                return false;
        }
    }

    /// <summary>The points <see cref="SKPath.RawIterator.Next(Span{SKPoint})"/> fills for a verb (the first is the previous end).</summary>
    private static int PointCount(SKPathVerb verb) => verb switch
    {
        SKPathVerb.Move => 1,
        SKPathVerb.Line => 2,
        SKPathVerb.Quad or SKPathVerb.Conic => 3,
        SKPathVerb.Cubic => 4,
        _ => 0
    };
}
