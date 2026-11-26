using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiStackedBar"/> (1.18 P4, #129): a leave balance (taken, scheduled, available) as one bar of three parts; editors
/// for the values, the maximum, the spacing and the corner radius. Changes animate.
/// </summary>
public sealed class StackedBarDemoPage : ComponentDemoPage
{
    public StackedBarDemoPage() : base(nameof(SkUiStackedBar), CreateSample(out var bar))
    {
        Number("Taken", 0, 120, 16, value => bar.Segments[0].Value = value, () => bar.Segments[0].Value, whole: true);
        Number("Scheduled", 0, 120, 24, value => bar.Segments[1].Value = value, () => bar.Segments[1].Value, whole: true);
        Number("Available", 0, 120, 78, value => bar.Segments[2].Value = value, () => bar.Segments[2].Value, whole: true);
        Number(nameof(SkUiStackedBar.Maximum), -1, 240, -1, value => bar.Maximum = value < 0 ? -1 : value, () => bar.Maximum, whole: true);
        Number(nameof(SkUiStackedBar.SegmentSpacing), -1, 12, -1, value => bar.SegmentSpacing = value < 0 ? -1 : value, () => bar.SegmentSpacing);
        Number(nameof(SkUiStackedBar.CornerRadius), -1, 12, -1, value => bar.CornerRadius = value < 0 ? -1 : value, () => bar.CornerRadius);
        ColorEditor(nameof(SkUiStackedBar.TrackColor), SkUiColors.TrackOff, value => bar.TrackColor = value, () => bar.TrackColor);
    }

    internal static SkUiStackedBar CreateSample(out SkUiStackedBar bar)
    {
        bar = new SkUiStackedBar { HeightRequest = 12 }.SetSegments(
            new SkUiBarSegment(16, Colors.IndianRed, "Taken"),
            new SkUiBarSegment(24, Colors.Orange, "Scheduled"),
            new SkUiBarSegment(78, Colors.SeaGreen, "Available"));
        SemanticProperties.SetDescription(bar, "Leave hours");
        return bar;
    }
}
