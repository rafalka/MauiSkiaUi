using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A font's own tracking (its <c>trak</c> table), which CoreText applies to every line on Apple platforms and HarfBuzz does
/// not: SF's text is 0.43 pt tighter per character at 17 pt than its plain advances. Read once per typeface; fonts without
/// the table track by 0. The looks that mimic an Apple platform turn it on (<see cref="SkUiLook.AppliesFontTracking"/>).
/// </summary>
internal static class SkUiFontTracking
{
    private const uint TrakTag = 0x7472616B; // 'trak'

    // Weak keys: a typeface the app disposes (loaded from a stream or file) is not kept alive by its table.
    private static readonly ConditionalWeakTable<SKTypeface, Table> Tables = new();

    private static readonly ConditionalWeakTable<SKTypeface, Table>.CreateValueCallback ReadTable = face => Read(face) ?? Table.None;

    /// <summary>The tracking of <paramref name="typeface"/> at <paramref name="fontSize"/>, DIPs added after each character.</summary>
    internal static float At(SKTypeface typeface, double fontSize) =>
        Tables.GetValue(typeface, ReadTable) is { IsEmpty: false } table ? table.At((float)fontSize) : 0;

    /// <summary>The normal (track 0) per-size values of the table's horizontal data, in ems; <c>null</c> without a usable table.</summary>
    private static Table? Read(SKTypeface typeface)
    {
        try
        {
            if (!typeface.TryGetTableData(TrakTag, out var data) || data.Length < 12)
                return null;
            var span = data.AsSpan();
            var horizontal = BinaryPrimitives.ReadUInt16BigEndian(span[6..]);
            if (horizontal == 0 || horizontal + 8 > data.Length)
                return null;
            var tracks = BinaryPrimitives.ReadUInt16BigEndian(span[horizontal..]);
            var sizes = BinaryPrimitives.ReadUInt16BigEndian(span[(horizontal + 2)..]);
            var sizeTable = (int)BinaryPrimitives.ReadUInt32BigEndian(span[(horizontal + 4)..]);
            if (sizes == 0 || sizeTable + 4 * sizes > data.Length)
                return null;
            var unitsPerEm = typeface.UnitsPerEm > 0 ? typeface.UnitsPerEm : 1000f;
            for (var track = 0; track < tracks; track++)
            {
                var entry = horizontal + 8 + 8 * track;
                if (entry + 8 > data.Length)
                    return null;
                // The normal track is 0 (16.16 fixed); others are tighter or looser styles.
                if (BinaryPrimitives.ReadInt32BigEndian(span[entry..]) != 0)
                    continue;
                var values = BinaryPrimitives.ReadUInt16BigEndian(span[(entry + 6)..]);
                if (values + 2 * sizes > data.Length)
                    return null;
                var points = new float[sizes];
                var ems = new float[sizes];
                for (var index = 0; index < sizes; index++)
                {
                    points[index] = BinaryPrimitives.ReadInt32BigEndian(span[(sizeTable + 4 * index)..]) / 65536f;
                    ems[index] = BinaryPrimitives.ReadInt16BigEndian(span[(values + 2 * index)..]) / unitsPerEm;
                }
                return new Table(points, ems);
            }
            return null;
        }
        catch (Exception exception) when (exception is ArgumentException or IndexOutOfRangeException)
        {
            return null; // a malformed table tracks by 0
        }
    }

    /// <summary>Per-size tracking in ems, interpolated linearly between sizes and held beyond the ends (as CoreText).</summary>
    private sealed class Table(float[] points, float[] ems)
    {
        /// <summary>A font without a usable table (tracks by 0).</summary>
        public static readonly Table None = new([], []);

        public bool IsEmpty => points.Length == 0;

        public float At(float fontSize)
        {
            if (fontSize <= points[0])
                return ems[0] * fontSize;
            for (var index = 1; index < points.Length; index++)
            {
                if (fontSize > points[index])
                    continue;
                var span = points[index] - points[index - 1];
                var t = span > 0 ? (fontSize - points[index - 1]) / span : 0;
                return (ems[index - 1] + (ems[index] - ems[index - 1]) * t) * fontSize;
            }
            return ems[^1] * fontSize;
        }
    }
}
