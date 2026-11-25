using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCoreTabView"/> (1.17 S8): the tab view on Core nodes, with a long list that keeps its scroll offset, a pane
/// made by a factory the first time it shows, a disabled tab and a badge; the editors of <see cref="TabViewDemoPage"/>.
/// </summary>
public sealed class CoreTabViewDemoPage : ComponentDemoPage
{
    public CoreTabViewDemoPage()
        : base(nameof(SkUiCoreTabView), CreateSample(out var tabs), native: null, widthRange: (200, 380, 320), heightRange: (240, 600, 420))
    {
        tabs.SelectionChanged += (_, args) => Feedback($"Selected {args.CurrentIndex} (was {args.PreviousIndex})");
        Number(nameof(SkUiCoreTabView.SelectedIndex), 0, 3, 0, value => tabs.SetSelectedIndex((int)value), () => tabs.SelectedIndex, whole: true);
        Choice(nameof(SkUiCoreTabView.TabStripPlacement), Enum.GetValues<SkUiTabStripPlacement>(), SkUiTabStripPlacement.Top,
            value => tabs.SetTabStripPlacement(value), () => tabs.TabStripPlacement);
        Choice(nameof(SkUiCoreTabView.TabWidthMode), Enum.GetValues<SkUiSegmentWidthMode>(), SkUiSegmentWidthMode.Equal,
            value => tabs.SetTabWidthMode(value), () => tabs.TabWidthMode);
        Choice(nameof(SkUiCoreTabView.IndicatorStyle), Enum.GetValues<SkUiTabIndicatorStyle>(), SkUiTabIndicatorStyle.Underline,
            value => tabs.SetIndicatorStyle(value), () => tabs.IndicatorStyle);
        Toggle(nameof(SkUiCoreTabView.IsSwipeEnabled), true, value => tabs.SetIsSwipeEnabled(value), () => tabs.IsSwipeEnabled);
        Toggle("Info tab enabled", false, value => tabs.Tabs[2].SetIsEnabled(value), () => tabs.Tabs[2].IsEnabled);
    }

    internal static SkUiCoreHost CreateSample(out SkUiCoreTabView tabs)
    {
        var list = new SkUiCoreVerticalStackLayout { Spacing = 8, Padding = new Thickness(16) };
        for (var index = 1; index <= 40; index++)
            list.Add(new SkUiCoreLabel().SetText($"Post {index}: scroll down, switch tabs and come back"));
        tabs = new SkUiCoreTabView()
            .AddTab(new SkUiCoreTabItem("Posts", new SkUiCoreScrollView().SetContent(list)))
            .AddTab(new SkUiCoreTabItem("Media", static () => Text($"Made by a factory the first time it showed ({DateTime.Now:T})")).SetBadgeText("3"))
            .AddTab(new SkUiCoreTabItem("Info", Text("Disabled: swipes and keys skip it")).SetIsEnabled(false))
            .AddTab(new SkUiCoreTabItem("About", Text("The last tab")));
        return new SkUiCoreHost().SetContent(tabs);

        static SkUiCoreLabel Text(string text) =>
            new SkUiCoreLabel().SetText(text).SetHorizontalTextAlignment(TextAlignment.Center).SetVerticalTextAlignment(TextAlignment.Center);
    }
}
