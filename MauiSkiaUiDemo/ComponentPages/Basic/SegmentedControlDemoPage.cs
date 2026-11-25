using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiSegmentedControl"/> (1.17 S4): a range switch drawn by the look (pick another look on the Look &amp; colors
/// page: a sliding pill, iOS's raised thumb, Material's outlined segments with a check, Fluent's bar). Editors for the
/// selection, the width mode, a disabled segment, the colors, the corner radius and the font. MAUI has no segmented control.
/// </summary>
public sealed class SegmentedControlDemoPage : ComponentDemoPage
{
    public SegmentedControlDemoPage()
        : base(nameof(SkUiSegmentedControl), CreateControl(), native: null)
    {
        var control = (SkUiSegmentedControl)SkiaControl;
        control.SelectionChanged += (_, args) => Feedback($"Selected {args.CurrentIndex} (was {args.PreviousIndex})");
        control.Command = new Command(() => Feedback($"Command after the user picked {control.SelectedIndex}"));
        Number(nameof(SkUiSegmentedControl.SelectedIndex), -1, 3, 1, value => control.SelectedIndex = (int)value, () => control.SelectedIndex, whole: true);
        Choice(nameof(SkUiSegmentedControl.SegmentWidthMode), Enum.GetValues<SkUiSegmentWidthMode>(), SkUiSegmentWidthMode.Equal,
            value => control.SegmentWidthMode = value, () => control.SegmentWidthMode);
        Toggle("Year segment enabled", true, value => control.Segments[3].IsEnabled = value, () => control.Segments[3].IsEnabled);
        Toggle(nameof(IsEnabled), true, value => control.IsEnabled = value, () => control.IsEnabled);
        ColorEditor(nameof(SkUiSegmentedControl.SelectionColor), Colors.SteelBlue, value => control.SelectionColor = value,
            () => control.SelectionColor ?? Colors.SteelBlue);
        Number("CornerRadius (-1: the look's)", -1, 24, -1, value => control.CornerRadius = value, () => control.CornerRadius, whole: true);
        Number(nameof(SkUiSegmentedControl.FontSize), 10, 28, 16, value => control.FontSize = value, () => control.FontSize);
    }

    internal static SkUiSegmentedControl CreateControl() => new SkUiSegmentedControl().SetSegments("Day", "Week", "Month", "Year").SetSelectedIndex(1);
}
