using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.17 S6 the segmented controls' keys and screen readers, on both layers: one tab stop; Left / Right (mirrored right to
/// left) and Home / End selecting at once, skipping disabled segments, without wrapping, running the command; each segment an
/// element read with "Selected" and its place, activated to select it; the group's value and increment / decrement.
/// </summary>
public class SegmentedAccessibilityTests
{
    private static SkUiSegmentedControl Control()
    {
        var control = new SkUiSegmentedControl().SetSegments("Day", "Week", "Month", "Year").SetSelectedIndex(0);
        control.Segments[2].IsEnabled = false;
        return control;
    }

    [Fact]
    public void ArrowsHomeAndEndSelectAtOnceSkippingDisabledSegmentsWithoutWrapping()
    {
        var runs = 0;
        var control = Control();
        control.Command = new Command(() => runs++);
        using var surface = new SkUiTestSurface(control, 300, 40);
        surface.Frame(0);
        var keys = control.FocusManager;
        Assert.True(keys.Focus(control));

        Assert.True(keys.KeyDown(SkUiKey.Right));
        Assert.Equal(1, control.SelectedIndex);
        Assert.True(keys.KeyDown(SkUiKey.Right)); // Month is disabled
        Assert.Equal(3, control.SelectedIndex);
        Assert.True(keys.KeyDown(SkUiKey.Right)); // the end: taken, no wrap
        Assert.Equal(3, control.SelectedIndex);
        Assert.True(keys.KeyDown(SkUiKey.Home));
        Assert.Equal(0, control.SelectedIndex);
        Assert.True(keys.KeyDown(SkUiKey.End));
        Assert.Equal(3, control.SelectedIndex);
        Assert.Equal(4, runs); // each change by key runs the command

        control.FlowDirection = FlowDirection.RightToLeft;
        Assert.True(keys.KeyDown(SkUiKey.Right)); // towards the start, which is on the right
        Assert.Equal(1, control.SelectedIndex);
    }

    [Fact]
    public void EachSegmentIsAnElementReadWithItsStateAndPlaceAndActivatingItSelectsIt()
    {
        var control = Control();
        using var surface = new SkUiTestSurface(control, 300, 40);
        surface.Frame(0);
        var node = (ISkUiAccessibleNode)control;
        var info = new SkUiSemanticsInfo();
        node.GetSemantics(info);

        Assert.Equal("Day", info.Value);
        var parts = info.Parts!;
        Assert.Equal(["Day", "Week", "Month", "Year"], parts.Select(static part => part.Label));
        Assert.Equal("Selected, 1 of 4", parts[0].Value);
        Assert.Equal("2 of 4", parts[1].Value);
        Assert.False(parts[2].IsEnabled);
        Assert.Equal(control.SegmentBounds(1), parts[1].Bounds);

        Assert.True(node.PerformSemanticsPartAction(1, SkUiSemanticsActions.Activate));
        Assert.Equal(1, control.SelectedIndex);
        Assert.False(node.PerformSemanticsPartAction(2, SkUiSemanticsActions.Activate)); // disabled
        Assert.True(node.PerformSemanticsAction(SkUiSemanticsActions.Increment)); // past the disabled one
        Assert.Equal(3, control.SelectedIndex);
        Assert.False(node.PerformSemanticsAction(SkUiSemanticsActions.Increment)); // the last
        Assert.True(node.PerformSemanticsAction(SkUiSemanticsActions.Decrement));
        Assert.Equal(1, control.SelectedIndex);

        info.Reset();
        node.GetSemantics(info);
        Assert.Equal(parts[1].Id, info.Parts![1].Id); // an element keeps its id
    }

    [Fact]
    public void IconSegmentsAreReadByTheirDescriptionAndIdsComeFromTheSegments()
    {
        var control = new SkUiSegmentedControl().SetSegments("", "Grid");
        SemanticProperties.SetDescription(control.Segments[0], "List");
        control.Segments[1].AutomationId = "GridSegment";
        SkUiTestHelpers.Arrange(control, 200, 40);
        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)control).GetSemantics(info);

        Assert.Equal(["List", "Grid"], info.Parts!.Select(static part => part.Label));
        Assert.Equal(["_0", "_GridSegment"], info.Parts!.Select(static part => part.AutomationIdSuffix));
    }

    [Fact]
    public void TheCoreControlTakesTheSameKeysAndReadsTheSame()
    {
        var core = new SkUiCoreSegmentedControl().SetSegments("Day", "Week", "Month").SetSelectedIndex(0);
        core.Segments[1].SetIsEnabled(false);
        var host = new SkUiCoreHost().SetContent(core);
        using var surface = new SkUiTestSurface(host, 300, 40);
        surface.Frame(0);
        Assert.True(host.FocusManager.Focus(core));

        Assert.True(host.FocusManager.KeyDown(SkUiKey.Right));
        Assert.Equal(2, core.SelectedIndex);

        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)core).GetSemantics(info);
        Assert.Equal("Selected, 3 of 3", info.Parts![2].Value);
        Assert.True(((ISkUiAccessibleNode)core).PerformSemanticsPartAction(0, SkUiSemanticsActions.Activate));
        Assert.Equal(0, core.SelectedIndex);
    }
}
