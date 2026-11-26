using HarfBuzzSharp;

namespace MauiSkiaUi;

/// <summary>
/// One OpenType feature text is shaped with: a registered four-character <see cref="Tag"/> and a <see cref="Value"/> (0 off,
/// 1 on, higher numbers pick an alternate of features that have several, such as <c>salt</c>). The common features are
/// named members (<see cref="TabularFigures"/>, <see cref="Kerning"/>…); <see cref="Disabled"/> turns one off, and any
/// registered tag works through the constructor, <see cref="Enable"/> and <see cref="Disable"/>. Whether a feature shows
/// depends on the font: <see cref="SkUiFontFeatures.SupportedBy"/> lists a font's. A list of them is
/// <see cref="SkUiFontFeatures"/>, which controls' <c>FontFeatures</c> take. Registry of tags:
/// <see href="https://learn.microsoft.com/typography/opentype/spec/featurelist"/>.
/// </summary>
public readonly struct SkUiFontFeature : IEquatable<SkUiFontFeature>
{
    private readonly string? _tag;

    /// <summary>A feature <paramref name="tag"/> set to <paramref name="value"/> (1: on, 0: off, higher: an alternate).</summary>
    /// <exception cref="ArgumentException"><paramref name="tag"/> is not four ASCII letters or digits, as registered tags are.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public SkUiFontFeature(string tag, int value = 1)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (!IsTag(tag))
            throw new ArgumentException($"'{tag}' is not an OpenType feature tag: four ASCII letters or digits (for example \"tnum\").", nameof(tag));
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        _tag = tag;
        Value = value;
    }

    /// <summary>The feature's four-character tag (<c>"tnum"</c>); empty for <c>default</c>, which no list takes.</summary>
    public string Tag => _tag ?? string.Empty;

    /// <summary>0 off, 1 on, a higher number an alternate (the font's numbering).</summary>
    public int Value { get; }

    /// <summary>Whether the feature is on (<see cref="Value"/> is not 0).</summary>
    public bool IsEnabled => Value != 0;

    /// <summary>This feature turned off (<see cref="Value"/> 0): <c>SkUiFontFeature.Kerning.Disabled</c>. A <c>default</c> feature has no tag to turn off (<see cref="ArgumentException"/>).</summary>
    public SkUiFontFeature Disabled => new(Tag, 0);

    /// <summary>This feature with <paramref name="value"/>: an alternate (<c>SkUiFontFeature.StylisticAlternates.WithValue(2)</c>). A <c>default</c> feature throws (<see cref="ArgumentException"/>).</summary>
    public SkUiFontFeature WithValue(int value) => new(Tag, value);

    internal bool IsDefault => _tag is null;

    /// <summary>The feature <paramref name="tag"/>, on.</summary>
    public static SkUiFontFeature Enable(string tag) => new(tag);

    /// <summary>The feature <paramref name="tag"/>, off: a feature the font applies by default (<c>liga</c>, <c>kern</c>, <c>calt</c>).</summary>
    public static SkUiFontFeature Disable(string tag) => new(tag, 0);

    #region Named features

    /// <summary><c>kern</c>, on by default: the font's spacing between pairs of letters (<c>AV</c>). <c>Kerning.Disabled</c> turns it off.</summary>
    public static SkUiFontFeature Kerning => new("kern");

    /// <summary><c>liga</c>, on by default: standard ligatures (<c>fi</c>, <c>fl</c> as one glyph). <c>StandardLigatures.Disabled</c> turns them off.</summary>
    public static SkUiFontFeature StandardLigatures => new("liga");

    /// <summary><c>clig</c>, on by default: ligatures that depend on the letters around them.</summary>
    public static SkUiFontFeature ContextualLigatures => new("clig");

    /// <summary><c>dlig</c>: decorative ligatures the font offers for display text (<c>ct</c>, <c>st</c>).</summary>
    public static SkUiFontFeature DiscretionaryLigatures => new("dlig");

    /// <summary><c>calt</c>, on by default: glyphs that change with the letters around them (a script font's joins).</summary>
    public static SkUiFontFeature ContextualAlternates => new("calt");

    /// <summary><c>salt</c>: the font's stylistic alternates; <see cref="WithValue"/> picks one where it has several.</summary>
    public static SkUiFontFeature StylisticAlternates => new("salt");

    /// <summary><c>swsh</c>: swash letters with flourishes.</summary>
    public static SkUiFontFeature Swash => new("swsh");

    /// <summary><c>tnum</c>: digits of one width, so numbers line up in columns and a clock or counter does not jitter.</summary>
    public static SkUiFontFeature TabularFigures => new("tnum");

    /// <summary><c>pnum</c>: digits as wide as their shape (a narrow 1), where the font's default is tabular.</summary>
    public static SkUiFontFeature ProportionalFigures => new("pnum");

    /// <summary><c>lnum</c>: digits of capital height.</summary>
    public static SkUiFontFeature LiningFigures => new("lnum");

    /// <summary><c>onum</c>: old-style digits that rise and fall like lower-case letters, for running text.</summary>
    public static SkUiFontFeature OldstyleFigures => new("onum");

    /// <summary><c>zero</c>: a zero with a slash or dot, told apart from the letter O.</summary>
    public static SkUiFontFeature SlashedZero => new("zero");

    /// <summary><c>frac</c>: fractions written with a slash (<c>1/2</c>) drawn as one fraction.</summary>
    public static SkUiFontFeature Fractions => new("frac");

    /// <summary><c>ordn</c>: ordinal endings raised (<c>1st</c>, <c>2º</c>).</summary>
    public static SkUiFontFeature Ordinals => new("ordn");

    /// <summary><c>sups</c>: the font's own superscript glyphs.</summary>
    public static SkUiFontFeature Superscript => new("sups");

    /// <summary><c>subs</c>: the font's own subscript glyphs.</summary>
    public static SkUiFontFeature Subscript => new("subs");

    /// <summary><c>smcp</c>: lower-case letters as small capitals. With <see cref="CapitalsToSmallCaps"/>, all letters are.</summary>
    public static SkUiFontFeature SmallCaps => new("smcp");

    /// <summary><c>c2sc</c>: capital letters as small capitals.</summary>
    public static SkUiFontFeature CapitalsToSmallCaps => new("c2sc");

    /// <summary><c>case</c>: punctuation and symbols moved up to suit capitals (hyphens, brackets in all-caps text).</summary>
    public static SkUiFontFeature CaseSensitiveForms => new("case");

    /// <summary>A stylistic set, <c>ss01</c> to <c>ss20</c>: a group of alternate glyphs the font names (often in its documentation).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is not 1 to 20.</exception>
    public static SkUiFontFeature StylisticSet(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, 20);
        return new($"ss{number:00}");
    }

    /// <summary>A character variant, <c>cv01</c> to <c>cv99</c>: alternates of one character; <see cref="WithValue"/> picks among several.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is not 1 to 99.</exception>
    public static SkUiFontFeature CharacterVariant(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, 99);
        return new($"cv{number:00}");
    }

    #endregion

    /// <summary>
    /// The feature <paramref name="text"/> writes, in CSS's <c>font-feature-settings</c> forms HarfBuzz reads: <c>"tnum"</c>,
    /// <c>"+tnum"</c>, <c>"-liga"</c>, <c>"salt=2"</c>, <c>"kern=0"</c>, <c>"kern off"</c>, <c>"'liga' 0"</c>. Ranges
    /// (<c>"liga[3:5]"</c>) are not taken: give that text a span of its own.
    /// </summary>
    /// <exception cref="FormatException"><paramref name="text"/> is not one feature.</exception>
    public static SkUiFontFeature Parse(string text) =>
        TryParse(text, out var feature) ? feature : throw new FormatException($"'{text}' is not an OpenType feature (for example \"tnum\" or \"-liga\").");

    /// <summary>Reads one feature as <see cref="Parse"/> does; <c>false</c> when <paramref name="text"/> is not one.</summary>
    public static bool TryParse(string? text, out SkUiFontFeature feature)
    {
        feature = default;
        if (string.IsNullOrWhiteSpace(text) || !HarfBuzzSharp.Feature.TryParse(text.Trim(), out var parsed)
            || parsed.Start != 0 || parsed.End != uint.MaxValue /* a range */ || parsed.Value > int.MaxValue)
            return false;
        var tag = parsed.Tag.ToString();
        if (!IsTag(tag))
            return false;
        feature = new SkUiFontFeature(tag, (int)parsed.Value);
        return true;
    }

    /// <summary>
    /// Whether <paramref name="tag"/> is four ASCII letters or digits. Registered feature tags are; the rule keeps every tag
    /// written back by <see cref="ToString"/> readable by <see cref="Parse"/>, and refuses the short tags HarfBuzz would pad.
    /// </summary>
    private static bool IsTag(string tag)
    {
        if (tag.Length != 4)
            return false;
        foreach (var character in tag)
            if (!char.IsAsciiLetterOrDigit(character))
                return false;
        return true;
    }

    internal Feature ToHarfBuzz() => new(HarfBuzzSharp.Tag.Parse(Tag), (uint)Value);

    /// <summary>The feature as <see cref="Parse"/> reads it: <c>"tnum"</c>, <c>"-liga"</c>, <c>"salt=2"</c>.</summary>
    public override string ToString() => Value switch
    {
        _ when IsDefault => string.Empty,
        0 => "-" + Tag,
        1 => Tag,
        _ => $"{Tag}={Value}"
    };

    /// <inheritdoc />
    public bool Equals(SkUiFontFeature other) => string.Equals(_tag, other._tag, StringComparison.Ordinal) && Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SkUiFontFeature other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_tag is null ? 0 : StringComparer.Ordinal.GetHashCode(_tag), Value);

    /// <summary>Whether <paramref name="left"/> and <paramref name="right"/> have the same tag and value.</summary>
    public static bool operator ==(SkUiFontFeature left, SkUiFontFeature right) => left.Equals(right);

    /// <summary>Whether <paramref name="left"/> and <paramref name="right"/> differ in tag or value.</summary>
    public static bool operator !=(SkUiFontFeature left, SkUiFontFeature right) => !left.Equals(right);
}
