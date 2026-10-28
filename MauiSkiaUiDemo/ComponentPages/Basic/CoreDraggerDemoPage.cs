using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// Property playground for <see cref="SkUiCoreDragger"/>: a corner handle that moves in both directions resizes a Core panel
/// through its events.
/// </summary>
public sealed class CoreDraggerDemoPage : ComponentDemoPage
{
    private const double PanelWidth = 200;
    private const double PanelHeight = 120;
    private const double MinPanelSize = 40;
    private const double HandleSize = 24;

    public CoreDraggerDemoPage()
        : base(nameof(SkUiCoreDragger), CreateHost(out var panel, out var dragger), native: null, widthRange: (240, 480, 320), heightRange: (160, 480, 260))
    {
        Choice(nameof(SkUiCoreDragger.DragAxis), [SkUiPanAxis.Both, SkUiPanAxis.Horizontal, SkUiPanAxis.Vertical], SkUiPanAxis.Both,
            value => dragger.DragAxis = value, () => dragger.DragAxis);
        ColorEditor(nameof(SkUiCoreDragger.GripColor), dragger.GripColor, value => dragger.GripColor = value, () => dragger.GripColor);
        Number(nameof(SkUiCoreDragger.KeyboardIncrement), 1, 50, SkUiDragger.DefaultKeyboardIncrement, value => dragger.KeyboardIncrement = value,
            () => dragger.KeyboardIncrement, whole: true);
        OnReset(() => panel.SetWidth(PanelWidth).SetHeight(PanelHeight));

        var start = Size.Zero;
        dragger.DragStarted += (_, _) => start = new Size(panel.Width, panel.Height);
        dragger.DragDelta += (_, args) =>
            panel.SetWidth(Math.Max(MinPanelSize, start.Width + args.TotalX)).SetHeight(Math.Max(MinPanelSize, start.Height + args.TotalY));
        dragger.DragCompleted += (_, _) => Feedback($"Panel {panel.Width:F0} x {panel.Height:F0}");
    }

    private static SkUiCoreHost CreateHost(out SkUiCoreLabel panel, out SkUiCoreDragger dragger)
    {
        panel = new SkUiCoreLabel()
            .SetText("Drag the corner")
            .SetTextColor(Colors.White)
            .SetFontSize(14)
            .SetHorizontalTextAlignment(TextAlignment.Center)
            .SetVerticalTextAlignment(TextAlignment.Center);
        panel.SetBackground(DemoColors.SampleA);
        panel.SetWidth(PanelWidth).SetHeight(PanelHeight);
        // A handle in the panel's corner: the grid around them is as large as the panel.
        dragger = new SkUiCoreDragger().SetGripLength(HandleSize / 2).SetGripAlignment(LayoutAlignment.Center);
        dragger.SetWidth(HandleSize).SetHeight(HandleSize).SetHorizontalAlignment(LayoutAlignment.End).SetVerticalAlignment(LayoutAlignment.End);
        dragger.SetBackground(Colors.White.WithAlpha(0.6f));
        var grid = new SkUiCoreGrid().Add(panel, 0, 0).Add(dragger, 0, 0);
        grid.SetHorizontalAlignment(LayoutAlignment.Start).SetVerticalAlignment(LayoutAlignment.Start).SetMargin(new Thickness(8));
        var host = new SkUiCoreHost();
        host.SetContent(grid);
        return host;
    }
}
