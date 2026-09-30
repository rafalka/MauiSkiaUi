namespace MauiSkiaUiSamples;

/// <summary>Sections of the samples app, in display order (sections without examples are not shown).</summary>
public enum SampleSection
{
    GettingStarted,
    Controls,
    Customisation,
    Core
}

public static class SampleSectionText
{
    public static string DisplayName(this SampleSection section) => section switch
    {
        SampleSection.GettingStarted => "Getting started",
        SampleSection.Controls => "Controls",
        SampleSection.Customisation => "Customisation",
        SampleSection.Core => "Core layer",
        _ => section.ToString()
    };

    public static string Description(this SampleSection section) => section switch
    {
        SampleSection.GettingStarted => "Hosting a drawn surface in a MAUI page.",
        SampleSection.Controls => "Using the drawn controls.",
        SampleSection.Customisation => "Changing how controls are drawn and animated: looks, color schemes, transitions.",
        SampleSection.Core => "Lightweight Core nodes for dense or custom UI.",
        _ => string.Empty
    };
}
