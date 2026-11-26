using System.Collections.Concurrent;
using HarfBuzzSharp;

namespace MauiSkiaUi;

/// <summary>
/// OpenType features text is shaped with (a control's <c>FontFeatures</c>): a comma-separated list in HarfBuzz's syntax,
/// which takes CSS's <c>font-feature-settings</c> forms (<c>"tnum"</c>, <c>"-liga"</c>, <c>"ss01=1"</c>, <c>"kern=0"</c>).
/// Parsed once per distinct string and shared; equal when their normalized text is, so a style that carries it stays a
/// cache key (<see cref="SkUiTextStyle.Features"/>).
/// </summary>
internal sealed class SkUiFontFeatures : IEquatable<SkUiFontFeatures>
{
    private static readonly ConcurrentDictionary<string, SkUiFontFeatures> Parsed = new(StringComparer.Ordinal);

    private SkUiFontFeatures(string text, Feature[] features) => (Text, Features) = (text, features);

    /// <summary>The features as HarfBuzz takes them, in the order written (a later setting of a tag wins).</summary>
    internal Feature[] Features { get; }

    /// <summary>The list normalized: items trimmed, joined by <c>", "</c>.</summary>
    internal string Text { get; }

    /// <summary>
    /// The features <paramref name="text"/> lists, or <c>null</c> when it lists none (<c>null</c>, empty or blank) or is not
    /// valid (<see cref="IsValid"/>; controls never store an invalid value).
    /// </summary>
    internal static SkUiFontFeatures? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (Parsed.TryGetValue(text, out var known))
            return known;
        var items = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var features = new Feature[items.Length];
        for (var index = 0; index < items.Length; index++)
            if (!Feature.TryParse(items[index], out features[index]))
                return null;
        if (features.Length == 0)
            return null;
        var parsed = new SkUiFontFeatures(string.Join(", ", items), features);
        // Bounded: a few distinct strings per app, unless an app builds them from data.
        if (Parsed.Count < 256)
            Parsed.TryAdd(text, parsed);
        return parsed;
    }

    /// <summary>Whether <paramref name="text"/> is <c>null</c>, blank, or a comma-separated list of features HarfBuzz parses.</summary>
    internal static bool IsValid(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;
        foreach (var item in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (!Feature.TryParse(item, out _))
                return false;
        return true;
    }

    public bool Equals(SkUiFontFeatures? other) => other is not null && (ReferenceEquals(this, other) || Text == other.Text);

    public override bool Equals(object? obj) => Equals(obj as SkUiFontFeatures);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Text);

    public override string ToString() => Text;
}
