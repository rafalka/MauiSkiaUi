using MauiSkiaUiSamples.Samples.Customisation;

namespace MauiSkiaUiSamples;

/// <summary>Every example, in display order within its section. Add new examples here (explicit: no reflection).</summary>
public static class SampleCatalog
{
    public static IReadOnlyList<SampleEntry> All { get; } =
    [
        SampleEntry.For<CrossCheckBoxSample>(),
    ];

    /// <summary>Sections that have examples, in <see cref="SampleSection"/> order.</summary>
    public static IEnumerable<SampleSection> Sections =>
        Enum.GetValues<SampleSection>().Where(section => All.Any(entry => entry.Info.Section == section));
}
