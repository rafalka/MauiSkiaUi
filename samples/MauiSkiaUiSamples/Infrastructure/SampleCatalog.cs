using MauiSkiaUiSamples.Samples.Controls;
using MauiSkiaUiSamples.Samples.Customisation;
using MauiSkiaUiSamples.Samples.GettingStarted;

namespace MauiSkiaUiSamples;

/// <summary>Every example, in display order within its section. Add new examples here (explicit: no reflection).</summary>
public static class SampleCatalog
{
    public static IReadOnlyList<SampleEntry> All { get; } =
    [
        SampleEntry.For<AppFontsSample>(),
        SampleEntry.For<TextThatFitsSample>(),
        SampleEntry.For<TextWithLinksSample>(),
        SampleEntry.For<CachedAvatarsSample>(),
        SampleEntry.For<ShapesAndBordersSample>(),
        SampleEntry.For<CardsWithShadowsSample>(),
        SampleEntry.For<ContentViewsSample>(),
        SampleEntry.For<OrderListSample>(),
        SampleEntry.For<PlanPickerSample>(),
        SampleEntry.For<CrossCheckBoxSample>(),
    ];

    /// <summary>Sections that have examples, in <see cref="SampleSection"/> order.</summary>
    public static IEnumerable<SampleSection> Sections =>
        Enum.GetValues<SampleSection>().Where(section => All.Any(entry => entry.Info.Section == section));
}
