using MauiSkiaUi;
using MauiSkiaUi.Core;
using SkiaSharp;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCoreHorizontalShrinkLayout"/> (white) inside the available space (the gray host): an icon, two
/// shrinkable labels and a fixed label. While the row fits it is as wide as its content, like a horizontal stack;
/// narrow the width below that and the shrinkable labels shrink.
/// </summary>
public sealed class CoreHorizontalShrinkLayoutDemoPage : ComponentDemoPage
{
    public CoreHorizontalShrinkLayoutDemoPage()
        : base(nameof(SkUiCoreHorizontalShrinkLayout), CreateHost(out var row, out var first, out var second),
            native: null, widthRange: (100, 480, 420), heightRange: (40, 200, 80))
    {
        var host = SkiaControl;
        var state = new ShrinkLayoutDemoState(vertical: false);
        void Update() => state.Update(row.Frame.Size, new Size(host.Width, host.Height));
        row.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(SkUiCoreNode.Frame)) Update(); };
        host.SizeChanged += (_, _) => Update();
        AddEditor("Layout", state.View);

        Number(nameof(SkUiCoreShrinkLayout.Spacing), 0, 24, 8, value => row.Spacing = value, () => row.Spacing);
        // Edit the text to watch the row switch between a plain stack (fits) and shrinking (too long).
        Text("Label2.Text", second.Text, value => second.SetText(value), () => second.Text);
        Choice<SkUiShrinkFactor>("Label1.Shrink", [SkUiShrinkFactor.None, SkUiShrinkFactor.Auto, 1, 2], SkUiShrinkFactor.Auto, value => row.SetShrink(first, value), () => row.GetShrink(first));
        Choice<SkUiShrinkFactor>("Label2.Shrink", [SkUiShrinkFactor.None, SkUiShrinkFactor.Auto, 1, 2], SkUiShrinkFactor.Auto, value => row.SetShrink(second, value), () => row.GetShrink(second));
        Choice<LineBreakMode>(nameof(SkUiCoreLabel.LineBreakMode), [LineBreakMode.TailTruncation, LineBreakMode.WordWrap], LineBreakMode.TailTruncation,
            value => { first.SetLineBreakMode(value); second.SetLineBreakMode(value); }, () => first.LineBreakMode ?? LineBreakMode.WordWrap);
    }

    private static SkUiCoreHost CreateHost(out SkUiCoreHorizontalShrinkLayout row, out SkUiCoreLabel first, out SkUiCoreLabel second)
    {
        first = CoreShrinkDemo.Label("A first label", "#BFDBFE");
        second = CoreShrinkDemo.Label("A longer second label", "#BBF7D0");
        row = new SkUiCoreHorizontalShrinkLayout();
        row.SetSpacing(8)
            .Add(new SkUiCoreBox().SetColor(DemoColors.Accent).SetWidth(32).SetHeight(32).SetVerticalAlignment(LayoutAlignment.Center))
            .Add(first, SkUiShrinkFactor.Auto)
            .Add(second, SkUiShrinkFactor.Auto)
            .Add(CoreShrinkDemo.Label("Fixed", "#FBCFE8"));
        row.SetHorizontalAlignment(LayoutAlignment.Start).SetVerticalAlignment(LayoutAlignment.Center);
        CoreShrinkDemo.PaintWhite(row);
        return new SkUiCoreHost { BackgroundColor = Colors.LightGray }.SetContent(row);
    }
}

/// <summary>
/// <see cref="SkUiCoreVerticalShrinkLayout"/> (white) inside the available space (the gray host): a header, a
/// shrinkable label and a footer. While they fit the layout is as tall as its content, like a vertical stack; lower
/// the height below that and the middle label is cut to the height that is left.
/// </summary>
public sealed class CoreVerticalShrinkLayoutDemoPage : ComponentDemoPage
{
    public CoreVerticalShrinkLayoutDemoPage()
        : base(nameof(SkUiCoreVerticalShrinkLayout), CreateHost(out var column, out var body),
            native: null, widthRange: (80, 360, 240), heightRange: (60, 480, 320))
    {
        var host = SkiaControl;
        var state = new ShrinkLayoutDemoState(vertical: true);
        void Update() => state.Update(column.Frame.Size, new Size(host.Width, host.Height));
        column.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(SkUiCoreNode.Frame)) Update(); };
        host.SizeChanged += (_, _) => Update();
        AddEditor("Layout", state.View);

        Number(nameof(SkUiCoreShrinkLayout.Spacing), 0, 24, 6, value => column.Spacing = value, () => column.Spacing);
        Choice<SkUiShrinkFactor>("Body.Shrink", [SkUiShrinkFactor.None, SkUiShrinkFactor.Auto, 1, 2], SkUiShrinkFactor.Auto, value => column.SetShrink(body, value), () => column.GetShrink(body));
    }

    private static SkUiCoreHost CreateHost(out SkUiCoreVerticalShrinkLayout column, out SkUiCoreLabel body)
    {
        body = CoreShrinkDemo.Label(
            "A long shrinkable label in a vertical shrink layout. While header, body and footer fit, the layout is only as tall "
            + "as they are; when they do not, the body gets the height the header and the footer leave.", "#BFDBFE");
        body.SetLineBreakMode(LineBreakMode.WordWrap);
        column = new SkUiCoreVerticalShrinkLayout();
        column.SetSpacing(6)
            .Add(CoreShrinkDemo.Label("Header", "#FDE68A"))
            .Add(body, SkUiShrinkFactor.Auto)
            .Add(CoreShrinkDemo.Label("Footer", "#FDE68A"));
        column.SetVerticalAlignment(LayoutAlignment.Start);
        CoreShrinkDemo.PaintWhite(column);
        return new SkUiCoreHost { BackgroundColor = Colors.LightGray }.SetContent(column);
    }
}

internal static class CoreShrinkDemo
{
    public static SkUiCoreLabel Label(string text, string fill) =>
        new SkUiCoreLabel().SetText(text).SetTextColor(DemoColors.Ink).SetFontSize(14).SetPadding(new Thickness(4))
            .SetFillColor(Color.FromArgb(fill)).SetLineBreakMode(LineBreakMode.TailTruncation).SetVerticalTextAlignment(TextAlignment.Center);

    /// <summary>Paints the layout's own frame white, so it stands out from the gray available space.</summary>
    public static void PaintWhite(SkUiCoreNode layout) => layout.SetPaintBackground(canvas =>
    {
        using var paint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        canvas.DrawRect(0, 0, (float)layout.Frame.Width, (float)layout.Frame.Height, paint);
    });
}
