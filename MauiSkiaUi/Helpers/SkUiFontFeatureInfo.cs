using System.Collections.Frozen;
using System.Globalization;

namespace MauiSkiaUi;

/// <summary>Whether text is shaped with an OpenType feature when <c>FontFeatures</c> does not list it.</summary>
public enum SkUiFontFeatureDefault
{
    /// <summary>Off: applied only when listed (<c>tnum</c>, <c>smcp</c>, <c>ss01</c>).</summary>
    Off,

    /// <summary>On: applied unless listed off (<c>kern</c>, <c>liga</c>, <c>calt</c>, <c>clig</c>).</summary>
    On,

    /// <summary>
    /// Applied by the shaper where the script or the layout needs it (Arabic joining forms, Indic conjuncts, mark placement,
    /// localized forms): not a choice for the reader, and turning it off can break the text.
    /// </summary>
    Required
}

/// <summary>
/// What an OpenType feature tag is: a short <see cref="Name"/> and <see cref="Description"/> for a picker or a tooltip, and
/// whether text has it without asking (<see cref="Default"/>). <see cref="SkUiFontFeature.Describe"/> gives the info of a tag,
/// <see cref="SkUiFontFeature.Known"/> every registered one; with <see cref="SkUiFontFeatures.SupportedBy"/>, a list of the
/// features a font offers.
/// </summary>
/// <param name="Tag">The four-character tag (<c>"tnum"</c>).</param>
/// <param name="Name">A short name (<c>"Tabular figures"</c>).</param>
/// <param name="Description">One sentence on what it does.</param>
/// <param name="Default">Whether text has it when it is not listed.</param>
public sealed record SkUiFontFeatureInfo(string Tag, string Name, string Description, SkUiFontFeatureDefault Default);

/// <summary>The registered feature tags (OpenType feature registry) with their names and descriptions.</summary>
internal static class SkUiFontFeatureRegistry
{
    private const SkUiFontFeatureDefault Off = SkUiFontFeatureDefault.Off;
    private const SkUiFontFeatureDefault On = SkUiFontFeatureDefault.On;
    private const SkUiFontFeatureDefault Required = SkUiFontFeatureDefault.Required;
    private const int StylisticSets = 20;
    private const int CharacterVariants = 99;

    private static readonly SkUiFontFeatureInfo[] Listed =
    [
        // Ligatures and contextual forms.
        new("liga", "Standard ligatures", "Letter pairs the font joins, such as fi and fl, drawn as one glyph.", On),
        new("clig", "Contextual ligatures", "Ligatures that depend on the letters around them.", On),
        new("calt", "Contextual alternates", "Glyphs that change with the letters around them, such as a script font's joins.", On),
        new("dlig", "Discretionary ligatures", "Decorative ligatures for display text, such as ct and st.", Off),
        new("hlig", "Historical ligatures", "Ligatures of older typography, such as the long s.", Off),
        new("rlig", "Required ligatures", "Ligatures the script needs, such as Arabic lam-alef.", Required),
        // Spacing.
        new("kern", "Kerning", "The font's spacing between pairs of letters, such as AV.", On),
        new("cpsp", "Capital spacing", "Extra space between capitals, for all-caps text.", Off),
        new("case", "Case-sensitive forms", "Punctuation and symbols moved up to suit capitals.", Off),
        new("opbd", "Optical bounds", "Glyph edges aligned to how they look rather than their boxes.", Off),
        new("lfbd", "Left bounds", "A line's first glyph aligned optically at the left edge.", Off),
        new("rtbd", "Right bounds", "A line's last glyph aligned optically at the right edge.", Off),
        // Figures.
        new("tnum", "Tabular figures", "Digits of one width, so numbers line up and counters do not jitter.", Off),
        new("pnum", "Proportional figures", "Digits as wide as their shape, such as a narrow 1.", Off),
        new("lnum", "Lining figures", "Digits of capital height.", Off),
        new("onum", "Old-style figures", "Digits that rise and fall like lower-case letters, for running text.", Off),
        new("zero", "Slashed zero", "A zero with a slash or dot, told apart from the letter O.", Off),
        new("frac", "Fractions", "Numbers around a slash, such as 1/2, drawn as one fraction.", Off),
        new("afrc", "Alternative fractions", "Fractions drawn stacked, with a horizontal bar.", Off),
        new("numr", "Numerators", "Digits drawn as fraction numerators.", Off),
        new("dnom", "Denominators", "Digits drawn as fraction denominators.", Off),
        new("sups", "Superscript", "The font's own superscript glyphs.", Off),
        new("subs", "Subscript", "The font's own subscript glyphs.", Off),
        new("sinf", "Scientific inferiors", "Small digits set low, for chemical formulas.", Off),
        new("ordn", "Ordinals", "Ordinal endings raised, such as 1st and 2º.", Off),
        // Capitals.
        new("smcp", "Small capitals", "Lower-case letters drawn as small capitals.", Off),
        new("c2sc", "Capitals to small capitals", "Capital letters drawn as small capitals.", Off),
        new("pcap", "Petite capitals", "Lower-case letters drawn as capitals smaller than small capitals.", Off),
        new("c2pc", "Capitals to petite capitals", "Capital letters drawn as petite capitals.", Off),
        new("unic", "Unicase", "Upper- and lower-case letters drawn at one height.", Off),
        new("titl", "Titling", "Capitals designed for large display sizes.", Off),
        // Alternates and decoration.
        new("aalt", "Access all alternates", "Every alternate the font has for a glyph; the value picks one.", Off),
        new("salt", "Stylistic alternates", "The font's alternate glyphs; the value picks one where it has several.", Off),
        new("swsh", "Swash", "Letters with flourishes.", Off),
        new("cswh", "Contextual swash", "Swash letters where the letters around them allow.", Off),
        new("hist", "Historical forms", "Letter shapes of older typography.", Off),
        new("ornm", "Ornaments", "Decorative glyphs in place of some characters.", Off),
        new("nalt", "Alternate annotation forms", "Characters in circles, boxes or other enclosures.", Off),
        new("rand", "Randomize", "A different alternate for each occurrence of a glyph, for a hand-made look.", Off),
        new("ital", "Italics", "The italic glyphs of a font that has both in one file.", Off),
        new("ssty", "Math script style", "Smaller forms for math superscripts and subscripts.", Off),
        new("dtls", "Dotless forms", "i and j without their dots, for math accents.", Off),
        new("flac", "Flattened accents", "Flatter accents over capitals, for math.", Off),
        new("mgrk", "Mathematical Greek", "Greek letters in their mathematical forms.", Off),
        new("size", "Optical size", "The design size range of the font; information, not a change.", Required),
        // East Asian.
        new("fwid", "Full widths", "Glyphs one em wide, such as full-width Latin letters.", Off),
        new("hwid", "Half widths", "Glyphs half an em wide.", Off),
        new("pwid", "Proportional widths", "Glyphs as wide as their shape, for CJK fonts.", Off),
        new("qwid", "Quarter widths", "Glyphs a quarter of an em wide.", Off),
        new("twid", "Third widths", "Glyphs a third of an em wide.", Off),
        new("palt", "Proportional alternate widths", "Full-width glyphs fitted to their shape.", Off),
        new("halt", "Alternate half widths", "Full-width glyphs set in half an em.", Off),
        new("chws", "Contextual half-width spacing", "Punctuation tightened where it meets other punctuation.", Off),
        new("cpct", "Centered CJK punctuation", "Punctuation centered in its em.", Off),
        new("expt", "Expert forms", "Japanese kanji in their expert forms.", Off),
        new("hojo", "Hojo kanji forms", "Japanese kanji as in the JIS X 0212 standard.", Off),
        new("jp78", "JIS78 forms", "Japanese kanji as in the 1978 standard.", Off),
        new("jp83", "JIS83 forms", "Japanese kanji as in the 1983 standard.", Off),
        new("jp90", "JIS90 forms", "Japanese kanji as in the 1990 standard.", Off),
        new("jp04", "JIS2004 forms", "Japanese kanji as in the 2004 standard.", Off),
        new("nlck", "NLC kanji forms", "Japanese kanji as the National Language Council recommends.", Off),
        new("smpl", "Simplified forms", "Chinese characters in their simplified forms.", Off),
        new("trad", "Traditional forms", "Chinese characters in their traditional forms.", Off),
        new("tnam", "Traditional name forms", "Traditional forms used in Japanese names.", Off),
        new("hkna", "Horizontal kana alternates", "Kana designed for horizontal text.", Off),
        new("pkna", "Proportional kana", "Kana as wide as their shape.", Off),
        new("ruby", "Ruby notation forms", "Small kana for ruby annotations.", Off),
        new("hngl", "Hangul", "Hanja shown as Hangul.", Off),
        new("jalt", "Justification alternates", "Wider or narrower glyphs used to justify Arabic lines.", Off),
        // Vertical text (SkiaUi lays text out horizontally).
        new("vert", "Vertical writing", "Glyphs for vertical text.", Required),
        new("vrt2", "Vertical alternates and rotation", "Glyphs for vertical text, rotated.", Required),
        new("vkna", "Vertical kana alternates", "Kana designed for vertical text.", Off),
        new("vkrn", "Vertical kerning", "Kerning for vertical text.", Off),
        new("vpal", "Proportional alternate vertical metrics", "Vertical glyphs fitted to their shape.", Off),
        new("vhal", "Alternate vertical half metrics", "Vertical glyphs set in half an em.", Off),
        new("valt", "Alternate vertical metrics", "Vertical glyphs re-centred.", Off),
        new("vrtr", "Vertical alternates for rotation", "Glyphs designed to be rotated in vertical text.", Off),
        new("vchw", "Vertical contextual half-width spacing", "Punctuation tightened in vertical text.", Off),
        // Applied by the shaper.
        new("ccmp", "Glyph composition", "Characters composed or decomposed, such as a letter and its accent.", Required),
        new("locl", "Localized forms", "Glyph shapes of the text's language, such as Serbian Cyrillic.", Required),
        new("mark", "Mark positioning", "Accents and marks placed on their letters.", Required),
        new("mkmk", "Mark to mark positioning", "Marks placed on other marks.", Required),
        new("curs", "Cursive positioning", "Letters of a joining script connected.", Required),
        new("dist", "Distances", "Spacing between glyphs that the script needs.", Required),
        new("rclt", "Required contextual alternates", "Contextual forms the script needs.", Required),
        new("rvrn", "Required variation alternates", "Glyphs chosen for the variable font's settings.", Required),
        new("init", "Initial forms", "Letters at the start of a word, in joining scripts.", Required),
        new("medi", "Medial forms", "Letters inside a word, in joining scripts.", Required),
        new("med2", "Medial forms 2", "Second medial forms, in Syriac.", Required),
        new("fina", "Terminal forms", "Letters at the end of a word, in joining scripts.", Required),
        new("fin2", "Terminal forms 2", "Second terminal forms, in Syriac.", Required),
        new("fin3", "Terminal forms 3", "Third terminal forms, in Syriac.", Required),
        new("isol", "Isolated forms", "Letters standing alone, in joining scripts.", Required),
        new("stch", "Stretching glyph decomposition", "Glyphs stretched to fill space, in Syriac.", Required),
        new("ltra", "Left-to-right alternates", "Glyphs for left-to-right text.", Required),
        new("ltrm", "Left-to-right mirrored forms", "Mirrored glyphs for left-to-right text.", Required),
        new("rtla", "Right-to-left alternates", "Glyphs for right-to-left text.", Required),
        new("rtlm", "Right-to-left mirrored forms", "Mirrored glyphs, such as brackets, in right-to-left text.", Required),
        new("mset", "Mark positioning via substitution", "Marks placed by swapping glyphs, in older Arabic fonts.", Required),
        new("falt", "Final glyph on line alternates", "Forms for the last glyph of a justified line.", Off),
        new("abvf", "Above-base forms", "Indic forms above the base consonant.", Required),
        new("abvm", "Above-base mark positioning", "Indic marks placed above the base.", Required),
        new("abvs", "Above-base substitutions", "Indic ligatures above the base.", Required),
        new("akhn", "Akhand", "Indic consonant clusters drawn as one glyph.", Required),
        new("blwf", "Below-base forms", "Indic forms below the base consonant.", Required),
        new("blwm", "Below-base mark positioning", "Indic marks placed below the base.", Required),
        new("blws", "Below-base substitutions", "Indic ligatures below the base.", Required),
        new("cfar", "Conjunct form after ro", "Khmer forms after the ro consonant.", Required),
        new("cjct", "Conjunct forms", "Indic conjunct consonants.", Required),
        new("half", "Half forms", "Indic half consonants.", Required),
        new("haln", "Halant forms", "Indic forms ending in a halant.", Required),
        new("nukt", "Nukta forms", "Indic consonants with a nukta.", Required),
        new("pref", "Pre-base forms", "Indic forms before the base consonant.", Required),
        new("pres", "Pre-base substitutions", "Indic ligatures before the base.", Required),
        new("pstf", "Post-base forms", "Indic forms after the base consonant.", Required),
        new("psts", "Post-base substitutions", "Indic ligatures after the base.", Required),
        new("rkrf", "Rakar forms", "Indic rakar ligatures.", Required),
        new("rphf", "Reph form", "The Indic reph above the cluster.", Required),
        new("vatu", "Vattu variants", "Indic vattu ligatures.", Required),
        new("ljmo", "Leading jamo forms", "Hangul leading consonants composed into syllables.", Required),
        new("vjmo", "Vowel jamo forms", "Hangul vowels composed into syllables.", Required),
        new("tjmo", "Trailing jamo forms", "Hangul trailing consonants composed into syllables.", Required),
    ];

    /// <summary>Every listed tag, then the stylistic sets (character variants are described on request, not listed).</summary>
    internal static readonly IReadOnlyList<SkUiFontFeatureInfo> Known =
        [.. Listed, .. Enumerable.Range(1, StylisticSets).Select(StylisticSet)];

    private static readonly FrozenDictionary<string, SkUiFontFeatureInfo> ByTag = Known.ToFrozenDictionary(info => info.Tag, StringComparer.Ordinal);

    /// <summary>The info of <paramref name="tag"/>: a registered one, a stylistic set or character variant; <c>null</c> otherwise.</summary>
    internal static SkUiFontFeatureInfo? Describe(string tag)
    {
        if (ByTag.TryGetValue(tag, out var info))
            return info;
        return tag.Length == 4 && tag.StartsWith("cv", StringComparison.Ordinal) && int.TryParse(tag.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            && number is >= 1 and <= CharacterVariants
            ? new SkUiFontFeatureInfo(tag, $"Character variant {number}", "Alternates of particular characters the font defines; the value picks one.", Off)
            : null;
    }

    private static SkUiFontFeatureInfo StylisticSet(int number) =>
        new($"ss{number:00}", $"Stylistic set {number}", "A set of alternate glyphs the font defines (its documentation names them).", Off);
}
