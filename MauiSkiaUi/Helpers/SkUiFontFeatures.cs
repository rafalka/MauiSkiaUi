using System.Buffers.Binary;
using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using HarfBuzzSharp;

namespace MauiSkiaUi;

/// <summary>
/// The OpenType features a control's text is shaped with (its <c>FontFeatures</c>): an immutable list of
/// <see cref="SkUiFontFeature"/>s, applied in order (a later setting of a tag wins). In code, a collection expression of
/// named features: <c>label.FontFeatures = [SkUiFontFeature.TabularFigures, SkUiFontFeature.Kerning.Disabled];</c>. In XAML
/// and styles, the CSS <c>font-feature-settings</c> text: <c>FontFeatures="tnum, -liga"</c> (<see cref="Parse"/>,
/// <see cref="SkUiFontFeaturesConverter"/>). A font without a feature draws as before; <see cref="SupportedBy"/> lists the
/// features a font has. Equal when their features are, in the same order.
/// </summary>
[TypeConverter(typeof(SkUiFontFeaturesConverter))]
[CollectionBuilder(typeof(SkUiFontFeatures), nameof(Create))]
public sealed class SkUiFontFeatures : IReadOnlyList<SkUiFontFeature>, IEquatable<SkUiFontFeatures>
{
    private const uint GsubTag = 0x47535542; // 'GSUB'
    private const uint GposTag = 0x47504F53; // 'GPOS'
    private const int ParsedLimit = 256;

    // Parse results by text: XAML and styles repeat a few lists. Bounded, unless an app builds them from data.
    private static readonly ConcurrentDictionary<string, SkUiFontFeatures> Parsed = new(StringComparer.Ordinal);

    private readonly SkUiFontFeature[] _features;
    private readonly Feature[]? _harfBuzz;
    private readonly int _hash;
    private string? _text;

    private SkUiFontFeatures(SkUiFontFeature[] features)
    {
        _features = features;
        if (features.Length == 0)
            return;
        _harfBuzz = new Feature[features.Length];
        var hash = new HashCode();
        for (var index = 0; index < features.Length; index++)
        {
            _harfBuzz[index] = features[index].ToHarfBuzz();
            hash.Add(features[index]);
        }
        _hash = hash.ToHashCode();
    }

    /// <summary>No features: the font's defaults, as a <c>null</c> <c>FontFeatures</c>.</summary>
    public static SkUiFontFeatures Empty { get; } = new([]);

    /// <summary>A list of <paramref name="features"/>, in order; what a collection expression (<c>[…]</c>) builds.</summary>
    /// <exception cref="ArgumentException">A feature is <c>default</c> (it has no tag).</exception>
    public static SkUiFontFeatures Create(params ReadOnlySpan<SkUiFontFeature> features)
    {
        if (features.IsEmpty)
            return Empty;
        foreach (var feature in features)
            if (feature.IsDefault)
                throw new ArgumentException("A default SkUiFontFeature has no tag; use a named feature or new SkUiFontFeature(tag).", nameof(features));
        return new SkUiFontFeatures(features.ToArray());
    }

    /// <summary>The number of features.</summary>
    public int Count => _features.Length;

    /// <summary>The feature at <paramref name="index"/>.</summary>
    public SkUiFontFeature this[int index] => _features[index];

    /// <summary>The features as HarfBuzz takes them; <c>null</c> when there are none (the shaping fast paths stay open).</summary>
    internal Feature[]? HarfBuzzFeatures => _harfBuzz;

    /// <summary><paramref name="features"/>, or <c>null</c> when it lists none: what a text style keys on.</summary>
    internal static SkUiFontFeatures? OrNull(SkUiFontFeatures? features) => features is { Count: > 0 } ? features : null;

    /// <summary>
    /// The features <paramref name="text"/> lists, comma-separated in CSS's <c>font-feature-settings</c> forms
    /// (<see cref="SkUiFontFeature.Parse"/>): <c>"tnum, -liga, ss01, salt=2"</c>. Blank text lists none (<see cref="Empty"/>).
    /// </summary>
    /// <exception cref="FormatException">An item is not a feature.</exception>
    public static SkUiFontFeatures Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out var features)
            ? features
            : throw new FormatException($"'{text}' is not a list of OpenType features (for example \"tnum, -liga\").");
    }

    /// <summary>Reads a list as <see cref="Parse"/> does; <c>false</c> when <paramref name="text"/> is <c>null</c> or an item is not a feature.</summary>
    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out SkUiFontFeatures? features)
    {
        features = null;
        if (text is null)
            return false;
        if (Parsed.TryGetValue(text, out features))
            return true;
        var items = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsed = new SkUiFontFeature[items.Length];
        for (var index = 0; index < items.Length; index++)
            if (!SkUiFontFeature.TryParse(items[index], out parsed[index]))
                return false;
        features = parsed.Length == 0 ? Empty : new SkUiFontFeatures(parsed);
        if (Parsed.Count < ParsedLimit)
            Parsed.TryAdd(text, features);
        return true;
    }

    /// <summary>
    /// The tags of the features the font of <paramref name="fontFamily"/> (a registered alias, a family name, or <c>null</c>
    /// for the default font) with <paramref name="attributes"/> has, sorted (<c>"kern"</c>, <c>"liga"</c>, <c>"tnum"</c>…): those
    /// its substitution and positioning tables (GSUB, GPOS) list, as they are written (compare with <see cref="SkUiFontFeature.Tag"/>).
    /// A feature not listed draws nothing different. Fonts with only Apple's AAT tables (<c>morx</c>, some system fonts) list none,
    /// though HarfBuzz maps some features onto them. Text in other scripts may fall back to other fonts, with features of their own.
    /// </summary>
    public static IReadOnlyList<string> SupportedBy(string? fontFamily, FontAttributes attributes = FontAttributes.None)
    {
        var tags = new SortedSet<string>(StringComparer.Ordinal);
        var typeface = SkUiTypefaces.Resolve(fontFamily, attributes);
        AddFeatureTags(typeface, GsubTag, tags);
        AddFeatureTags(typeface, GposTag, tags);
        return [.. tags];
    }

    /// <summary>Adds the feature tags of the FeatureList of <paramref name="typeface"/>'s GSUB or GPOS table; nothing when it has none or it is malformed.</summary>
    private static void AddFeatureTags(SkiaSharp.SKTypeface typeface, uint table, SortedSet<string> tags)
    {
        if (!typeface.TryGetTableData(table, out var data) || data.Length < 10)
            return;
        var span = data.AsSpan();
        // Header: version (4 bytes), ScriptList offset, FeatureList offset (16 bits each); a FeatureList: a count, then
        // records of a tag (4 bytes) and an offset (2).
        int list = BinaryPrimitives.ReadUInt16BigEndian(span[6..]);
        if (list == 0 || list + 2 > span.Length)
            return;
        int count = BinaryPrimitives.ReadUInt16BigEndian(span[list..]);
        for (var index = 0; index < count; index++)
        {
            var record = list + 2 + 6 * index;
            if (record + 6 > span.Length)
                return;
            tags.Add(System.Text.Encoding.Latin1.GetString(span.Slice(record, 4)));
        }
    }

    /// <inheritdoc />
    public IEnumerator<SkUiFontFeature> GetEnumerator() => ((IEnumerable<SkUiFontFeature>)_features).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public bool Equals(SkUiFontFeatures? other) =>
        other is not null && (ReferenceEquals(this, other) || _hash == other._hash && _features.AsSpan().SequenceEqual(other._features));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as SkUiFontFeatures);

    /// <inheritdoc />
    public override int GetHashCode() => _hash;

    /// <summary>The list as <see cref="Parse"/> reads it: <c>"tnum, -liga, salt=2"</c> (empty for none).</summary>
    public override string ToString() => _text ??= string.Join(", ", _features);
}

/// <summary>
/// Converts the text of XAML and styles (<c>FontFeatures="tnum, -liga"</c>) to <see cref="SkUiFontFeatures"/>
/// (<see cref="SkUiFontFeatures.Parse"/>; blank text: <see cref="SkUiFontFeatures.Empty"/>, the font's defaults, also on a span
/// whose label has features), and back.
/// </summary>
public sealed class SkUiFontFeaturesConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) => value switch
    {
        string text => SkUiFontFeatures.Parse(text),
        _ => throw new NotSupportedException($"Cannot convert {value.GetType().Name} to {nameof(SkUiFontFeatures)}.")
    };

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string) && value is SkUiFontFeatures or null
            ? value?.ToString()
            : throw new NotSupportedException($"Cannot convert to {destinationType.Name}.");
}
