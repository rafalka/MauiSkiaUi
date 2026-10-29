using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Automatic optical size, as CoreText applies it: a font with an <c>opsz</c> axis (Apple's system font: 17 to 96) is drawn
/// at the optical size of the size it is drawn at, so large text gets the display design, tighter than the text one. The
/// looks that mimic an Apple platform turn it on (<see cref="SkUiLook.AppliesOpticalSize"/>); without it Skia keeps the
/// typeface's own position (the system font's: 17), and large text measures wider than native text.
/// </summary>
/// <remarks>
/// An instance is cloned from a typeface made of the font's data (<see cref="SkUiFontData"/>, which HarfBuzz shapes from
/// too), at the original's position (its weight, width) with only the optical size changed: CoreText does not move its system
/// font's optical size when the system typeface itself is cloned (the clone draws at 17, whatever position it reports).
/// </remarks>
internal static class SkUiOpticalSize
{
    private static readonly SKFourByteTag Tag = SKFourByteTag.Parse("opsz");
    // CoreText returns some fractional optical sizes slightly off (20.3 reads back as 20.2999).
    private const float OpticalSizeTolerance = 0.01f;
    private static readonly ConditionalWeakTable<SKTypeface, Axis> Axes = new();
    private static readonly ConditionalWeakTable<SKTypeface, Axis>.CreateValueCallback ReadAxis = Read;
    // Instances are kept for the process, as the fonts that draw them (SkUiTextResources): one per typeface and size drawn.
    private static readonly ConcurrentDictionary<(SKTypeface Typeface, float Size), SKTypeface> Instances = new();
    private static readonly ConditionalWeakTable<SKTypeface, SKTypeface> Originals = new();

    /// <summary>
    /// <paramref name="typeface"/> at the optical size of text drawn <paramref name="drawnSize"/> points (clamped to its
    /// axis), or the typeface it was made from (itself, unless an instance) when it has no <c>opsz</c> axis, is already
    /// there, or cannot move.
    /// </summary>
    internal static SKTypeface At(SKTypeface typeface, float drawnSize)
    {
        // Instances are made from the original only, so an instance asked for another size does not make satellites of its own.
        typeface = Original(typeface);
        var axis = Axes.GetValue(typeface, ReadAxis);
        if (!axis.Exists)
            return typeface;
        var size = Math.Clamp(drawnSize, axis.Min, axis.Max);
        if (Math.Abs(size - axis.Current) < OpticalSizeTolerance)
            return typeface;
        return Instances.GetOrAdd((typeface, size), static key => Create(key.Typeface, key.Size));
    }

    /// <summary>The typeface an instance was made from (its style: bold, italic), or <paramref name="typeface"/> itself.</summary>
    internal static SKTypeface Original(SKTypeface typeface) => Originals.TryGetValue(typeface, out var original) ? original : typeface;

    private static SKTypeface Create(SKTypeface typeface, float size)
    {
        var instance = SkUiFontData.Typeface(typeface)?.Clone(Position(typeface, size));
        // Negated: a missing axis (NaN) is not at the size either.
        if (instance is null || !(Math.Abs(OpticalSize(instance) - size) < OpticalSizeTolerance))
        {
            instance?.Dispose();
            return typeface; // the platform cannot draw it at another optical size
        }
        Originals.AddOrUpdate(instance, typeface);
        return instance;
    }

    /// <summary>The typeface's position with the optical size set to <paramref name="size"/>.</summary>
    private static SKFontVariationPositionCoordinate[] Position(SKTypeface typeface, float size)
    {
        var position = typeface.VariationDesignPosition;
        for (var index = 0; index < position.Length; index++)
        {
            if (position[index].Axis != Tag)
                continue;
            position[index].Value = size;
            return position;
        }
        return [.. position, new SKFontVariationPositionCoordinate { Axis = Tag, Value = size }];
    }

    private static float OpticalSize(SKTypeface typeface)
    {
        foreach (var coordinate in typeface.VariationDesignPosition)
            if (coordinate.Axis == Tag)
                return coordinate.Value;
        return float.NaN;
    }

    private static Axis Read(SKTypeface typeface)
    {
        var count = typeface.VariationDesignParameterCount;
        if (count <= 0)
            return Axis.None;
        var axes = new SKFontVariationAxis[count];
        count = typeface.GetVariationDesignParameters(axes);
        for (var index = 0; index < count; index++)
        {
            if (axes[index].Tag == Tag)
                return At(typeface, axes[index].Min, axes[index].Max, axes[index].Default);
        }
        // CoreText's system font on iOS counts its axes but lists none: read them from the font data.
        return count < 0 && SkUiFontData.Of(typeface) is { } data && data.OpticalSizeAxis() is var (min, max, @default)
            ? At(typeface, min, max, @default)
            : Axis.None;
    }

    private static Axis At(SKTypeface typeface, float min, float max, float @default)
    {
        var current = OpticalSize(typeface);
        return new Axis(true, min, max, float.IsNaN(current) ? @default : current);
    }

    /// <summary>A typeface's <c>opsz</c> axis: its range and the position the typeface is at.</summary>
    private sealed record Axis(bool Exists, float Min, float Max, float Current)
    {
        public static readonly Axis None = new(false, 0, 0, 0);
    }
}

/// <summary>
/// The font file behind a typeface, read once and shared: by the typefaces made of it (<see cref="SkUiOpticalSize"/>) and by
/// HarfBuzz, which shapes from the font data. A system font is several megabytes, so typefaces of one file (regular and bold
/// of a variable font, the optical size instances) share one copy, keyed by family, size and index.
/// </summary>
internal static class SkUiFontData
{
    private static readonly ConditionalWeakTable<SKTypeface, Entry> ByTypeface = new();
    private static readonly ConditionalWeakTable<SKTypeface, Entry>.CreateValueCallback LoadEntry = Load;
    private static readonly ConcurrentDictionary<(string Family, long Size, int Index), Entry> ByFile = new();

    /// <summary>The data of <paramref name="typeface"/>'s file (of the typeface an instance was made from), or <c>null</c> when it exposes none.</summary>
    internal static Entry? Of(SKTypeface typeface)
    {
        var entry = ByTypeface.GetValue(SkUiOpticalSize.Original(typeface), LoadEntry);
        return entry.Data is null ? null : entry;
    }

    /// <summary>A typeface made of <paramref name="typeface"/>'s font data (its default instance), or <c>null</c>.</summary>
    internal static SKTypeface? Typeface(SKTypeface typeface) => Of(typeface)?.Typeface;

    private static Entry Load(SKTypeface typeface)
    {
        var stream = typeface.OpenStream(out var index);
        if (stream is null)
            return Entry.None;
        var key = (typeface.FamilyName, (long)stream.Length, index);
        if (ByFile.TryGetValue(key, out var known))
        {
            stream.Dispose(); // the same file, read before through another typeface
            return known;
        }
        // A memory-based stream (Android maps font files) is wrapped, not copied: its pages stay the file's. Others are read once.
        var memory = stream.GetMemoryBase();
        SKData? data;
        if (memory != IntPtr.Zero)
        {
            data = SKData.Create(memory, stream.Length, (_, _) => stream.Dispose());
        }
        else
        {
            data = SKData.Create(stream);
            stream.Dispose();
        }
        if (data is null)
            return Entry.None;
        var entry = ByFile.GetOrAdd(key, static (_, loaded) => loaded, new Entry(data, index));
        if (!ReferenceEquals(entry.Data, data))
            data.Dispose(); // read meanwhile through another typeface
        return entry;
    }

    /// <summary>A font file's bytes and face index, and a typeface made of them on first use; kept for the process.</summary>
    internal sealed class Entry(SKData? data, int index)
    {
        public static readonly Entry None = new(null, 0);

        public SKData? Data { get; } = data;

        public int Index { get; } = index;

        public SKTypeface? Typeface => field ??= Data is null ? null : SKFontManager.Default.CreateTypeface(Data, Index);

        /// <summary>The font's <c>opsz</c> axis (minimum, maximum, default) as its data declares it, or <c>null</c>.</summary>
        public (float Min, float Max, float Default)? OpticalSizeAxis()
        {
            if (Data is null)
                return null;
            using var blob = new HarfBuzzSharp.Blob(Data.Data, (int)Data.Size, HarfBuzzSharp.MemoryMode.ReadOnly);
            using var face = new HarfBuzzSharp.Face(blob, Index);
            Span<HarfBuzzSharp.OpenTypeVarAxisInfo> axes = stackalloc HarfBuzzSharp.OpenTypeVarAxisInfo[16];
            var count = face.GetVariationAxisInfos(axes);
            var tag = (uint)SKFourByteTag.Parse("opsz");
            for (var index = 0; index < Math.Min(count, axes.Length); index++)
                if (axes[index].Tag == tag)
                    return (axes[index].MinValue, axes[index].MaxValue, axes[index].DefaultValue);
            return null;
        }
    }
}
