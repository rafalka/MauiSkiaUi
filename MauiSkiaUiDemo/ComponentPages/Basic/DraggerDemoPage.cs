using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Property playground for <see cref="SkUiDragger"/>: a dragger under a panel resizes it through its events (the panel's
/// size on start plus the drag's totals), as an app resizes its own views.
/// </summary>
public sealed class DraggerDemoPage : ComponentDemoPage
{
    private const double PanelWidth = 200;
    private const double PanelHeight = 100;
    private const double MinPanelSize = 40;

    public DraggerDemoPage()
        : base(nameof(SkUiDragger), CreatePreview(out var panel, out var dragger), widthRange: (240, 480, 320), heightRange: (160, 480, 260))
    {
        Choice(nameof(SkUiDragger.DragAxis), [SkUiPanAxis.Vertical, SkUiPanAxis.Horizontal, SkUiPanAxis.Both], SkUiPanAxis.Vertical,
            value => dragger.DragAxis = value, () => dragger.DragAxis);
        Toggle(nameof(SkUiDragger.ShowsGrip), true, value => dragger.ShowsGrip = value, () => dragger.ShowsGrip);
        Choice(nameof(SkUiDragger.GripAlignment), [LayoutAlignment.Start, LayoutAlignment.Center, LayoutAlignment.End, LayoutAlignment.Fill],
            LayoutAlignment.Center, value => dragger.GripAlignment = value, () => dragger.GripAlignment);
        Toggle(nameof(SkUiDragger.ShowsLine), true, value => dragger.ShowsLine = value, () => dragger.ShowsLine);
        ColorEditor(nameof(SkUiDragger.GripColor), dragger.GripColor, value => dragger.GripColor = value, () => dragger.GripColor);
        Number(nameof(SkUiDragger.KeyboardIncrement), 1, 50, SkUiDragger.DefaultKeyboardIncrement, value => dragger.KeyboardIncrement = value,
            () => dragger.KeyboardIncrement, whole: true);
        OnReset(() =>
        {
            panel.WidthRequest = PanelWidth;
            panel.HeightRequest = PanelHeight;
        });

        // The app's own handling: the panel's size when the drag starts, plus the travel since then.
        var start = Size.Zero;
        dragger.DragStarted += (_, _) => start = new Size(panel.WidthRequest, panel.HeightRequest);
        dragger.DragDelta += (_, args) =>
        {
            panel.WidthRequest = Math.Max(MinPanelSize, start.Width + args.TotalX);
            panel.HeightRequest = Math.Max(MinPanelSize, start.Height + args.TotalY);
        };
        dragger.DragCompleted += (_, args) =>
            Feedback($"Panel {panel.WidthRequest:F0} x {panel.HeightRequest:F0}{(args.IsKeyboard ? " (key)" : "")}{(args.IsCanceled ? " (canceled)" : "")}");
    }

    private static SkUiGrid CreatePreview(out SkUiLabel panel, out SkUiDragger dragger)
    {
        panel = new SkUiLabel
        {
            Text = "Drag the handle below", TextColor = Colors.White, FontSize = 14, Background = Accent, WidthRequest = PanelWidth,
            HeightRequest = PanelHeight, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
        };
        dragger = new SkUiDragger { DragAxis = SkUiPanAxis.Vertical, ShowsLine = true, AutomationId = "PanelDragger" };
        SemanticProperties.SetDescription(dragger, "Panel size");
        // The stack is as wide as the panel, so the dragger under it follows its width.
        var stack = new SkUiVerticalStackLayout { HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Children = { panel, dragger } };
        return new SkUiGrid { Padding = 8, Children = { stack } };
    }
}
