namespace MauiSkiaUi;

/// <summary>
/// What an OpenType feature tag is: a short <see cref="Name"/> and <see cref="Description"/> for a picker or a tooltip, and
/// whether text has it without asking (<see cref="Default"/>). <see cref="SkUiFontFeature.Describe"/> gives the info of a tag,
/// <see cref="SkUiFontFeature.Known"/> every registered one; with <see cref="SkUiFontFeatures.SupportedBy"/>, a list of the
/// features a font offers.
/// </summary>
public sealed record SkUiFontFeatureInfo
{
    internal SkUiFontFeatureInfo(string tag, string name, string description, SkUiFontFeatureDefault @default) =>
        (Tag, Name, Description, Default) = (tag, name, description, @default);

    /// <summary>The four-character tag (<c>"tnum"</c>).</summary>
    public string Tag { get; }

    /// <summary>A short name (<c>"Tabular figures"</c>).</summary>
    public string Name { get; }

    /// <summary>One sentence on what it does.</summary>
    public string Description { get; }

    /// <summary>Whether text has it when it is not listed.</summary>
    public SkUiFontFeatureDefault Default { get; }
}
