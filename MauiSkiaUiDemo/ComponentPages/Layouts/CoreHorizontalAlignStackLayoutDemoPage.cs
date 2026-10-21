using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCoreHorizontalAlignStackLayout"/>: a bar (white) with a back button at the start side, a title in the middle
/// and a narrower menu button at the end side, over a marker of the layout's center. Edit the buttons' texts, hide them or
/// make the title longer to watch the title keep its place or move aside, and compare the center strategies.
/// </summary>
public sealed class CoreHorizontalAlignStackLayoutDemoPage : ComponentDemoPage
{
    private const string TitleText = "Page title";
    private const string BackText = "‹ Back to list";
    private const string MenuText = "Menu";

    public CoreHorizontalAlignStackLayoutDemoPage()
        : base(nameof(SkUiCoreHorizontalAlignStackLayout), CreateHost(out var bar, out var title, out var back, out var menu),
            native: null, widthRange: (160, 480, 360), heightRange: (72, 160, AlignStackDemo.AreaHeight))
    {
        DocumentTypes(typeof(SkUiCoreLabel), typeof(SkUiCoreButton));
        Choice(nameof(SkUiCoreHorizontalAlignStackLayout.CenterStrategy), Enum.GetValues<SkUiCenterStrategy>(), SkUiCenterStrategy.Parent,
            value => bar.CenterStrategy = value, () => bar.CenterStrategy);
        Text("Title.Text", TitleText, value => title.SetText(value), () => title.Text ?? string.Empty);
        // Longer or shorter side buttons: the title stays centered while it fits, then moves aside.
        Text("Back.Text", BackText, value => back.SetText(value), () => back.Text ?? string.Empty);
        Text("Menu.Text", MenuText, value => menu.SetText(value), () => menu.Text ?? string.Empty);
        Toggle("Back.IsVisible", true, value => back.IsVisible = value, () => back.IsVisible);
        Toggle("Menu.IsVisible", true, value => menu.IsVisible = value, () => menu.IsVisible);
        Number(nameof(SkUiCoreHorizontalAlignStackLayout.Spacing), 0, 24, 8, value => bar.Spacing = value, () => bar.Spacing);
    }

    private static SkUiCoreHost CreateHost(out SkUiCoreHorizontalAlignStackLayout bar, out SkUiCoreLabel title, out SkUiCoreButton back,
        out SkUiCoreButton menu)
    {
        title = new SkUiCoreLabel().SetText(TitleText).SetTextColor(DemoColors.Ink).SetFontSize(16).SetFontAttributes(FontAttributes.Bold)
            .SetMaxLines(1).SetLineBreakMode(LineBreakMode.TailTruncation);
        title.SetVerticalAlignment(LayoutAlignment.Center);
        back = Button(BackText);
        menu = Button(MenuText);
        bar = new SkUiCoreHorizontalAlignStackLayout()
            .SetSpacing(8)
            .SetPadding(new Thickness(8, 0))
            .Add(back, LayoutAlignment.Start)
            .Add(title, LayoutAlignment.Center)
            .Add(menu, LayoutAlignment.End);
        bar.SetHeight(AlignStackDemo.BarHeight).SetVerticalAlignment(LayoutAlignment.Center);
        CoreShrinkDemo.PaintWhite(bar);
        var line = new SkUiCoreBox().SetColor(AlignStackDemo.MarkerColor).SetWidth(1).SetHeight(AlignStackDemo.MarkerHeight)
            .SetHorizontalAlignment(LayoutAlignment.Center).SetVerticalAlignment(LayoutAlignment.Center);
        line.InputTransparent = true;
        var caption = new SkUiCoreLabel().SetText(AlignStackDemo.MarkerText).SetTextColor(AlignStackDemo.MarkerColor)
            .SetFontSize(AlignStackDemo.MarkerFontSize);
        caption.SetHorizontalAlignment(LayoutAlignment.Center).SetVerticalAlignment(LayoutAlignment.End).SetMargin(new Thickness(0, 0, 0, 4));
        caption.InputTransparent = true;
        // One grid cell: each child aligns itself in the whole area.
        var area = new SkUiCoreGrid().Add(bar, 0, 0).Add(line, 0, 0).Add(caption, 0, 0);
        return new SkUiCoreHost { BackgroundColor = Colors.LightGray }.SetContent(area);
    }

    private static SkUiCoreButton Button(string text)
    {
        var button = new SkUiCoreButton();
        button.SetText(text);
        button.SetFontSize(14);
        button.SetPadding(new Thickness(8, 4));
        button.SetVerticalAlignment(LayoutAlignment.Center);
        return button;
    }
}
