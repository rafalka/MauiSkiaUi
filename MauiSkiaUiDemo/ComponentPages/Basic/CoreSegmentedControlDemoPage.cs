using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCoreSegmentedControl"/> (1.17 S5): the segmented control on Core nodes, drawn by the same engine and look as
/// <see cref="SkUiSegmentedControl"/>; editors for the selection, the width mode, a disabled segment and the font.
/// </summary>
public sealed class CoreSegmentedControlDemoPage : ComponentDemoPage
{
    public CoreSegmentedControlDemoPage()
        : base(nameof(SkUiCoreSegmentedControl), CreateSample(out var control), native: null)
    {
        control.SelectionChanged += (_, args) => Feedback($"Selected {args.CurrentIndex} (was {args.PreviousIndex})");
        Number(nameof(SkUiCoreSegmentedControl.SelectedIndex), -1, 2, 0, value => control.SetSelectedIndex((int)value), () => control.SelectedIndex, whole: true);
        Choice(nameof(SkUiCoreSegmentedControl.SegmentWidthMode), Enum.GetValues<SkUiSegmentWidthMode>(), SkUiSegmentWidthMode.Equal,
            value => control.SetSegmentWidthMode(value), () => control.SegmentWidthMode);
        Toggle("Flagged segment enabled", true, value => control.Segments[2].SetIsEnabled(value), () => control.Segments[2].IsEnabled);
        Number(nameof(SkUiCoreSegmentedControl.FontSize), 10, 28, 16, value => control.SetFontSize(value), () => control.FontSize);
    }

    internal static SkUiCoreHost CreateSample(out SkUiCoreSegmentedControl control)
    {
        control = new SkUiCoreSegmentedControl().SetSegments("All", "Unread", "Flagged").SetSelectedIndex(0);
        return new SkUiCoreHost().SetContent(control);
    }
}
