using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiTabView"/> (1.17 S7): four tabs over swiped panes (a long list that keeps its scroll offset, a pane made
/// from a template the first time it shows, a disabled tab, a badge). Editors for the selection, the strip's placement and
/// width mode, the indicator style and swiping. The feedback line reports each selection change and which panes exist.
/// </summary>
public sealed class TabViewDemoPage : ComponentDemoPage
{
    public TabViewDemoPage()
        : base(nameof(SkUiTabView), CreateSample(), native: null, widthRange: (200, 380, 320), heightRange: (240, 600, 420))
    {
        var tabs = (SkUiTabView)SkiaControl;
        tabs.SelectionChanged += (_, args) => Feedback($"Selected {args.CurrentIndex} (was {args.PreviousIndex})");
        Number(nameof(SkUiTabView.SelectedIndex), 0, 3, 0, value => tabs.SelectedIndex = (int)value, () => tabs.SelectedIndex, whole: true);
        Choice(nameof(SkUiTabView.TabStripPlacement), Enum.GetValues<SkUiTabStripPlacement>(), SkUiTabStripPlacement.Top,
            value => tabs.TabStripPlacement = value, () => tabs.TabStripPlacement);
        Choice(nameof(SkUiTabView.TabWidthMode), Enum.GetValues<SkUiSegmentWidthMode>(), SkUiSegmentWidthMode.Equal,
            value => tabs.TabWidthMode = value, () => tabs.TabWidthMode);
        Choice(nameof(SkUiTabView.IndicatorStyle), Enum.GetValues<SkUiTabIndicatorStyle>(), SkUiTabIndicatorStyle.Underline,
            value => tabs.IndicatorStyle = value, () => tabs.IndicatorStyle);
        Toggle(nameof(SkUiTabView.IsSwipeEnabled), true, value => tabs.IsSwipeEnabled = value, () => tabs.IsSwipeEnabled);
        Toggle("Info tab enabled", false, value => tabs.Tabs[2].IsEnabled = value, () => tabs.Tabs[2].IsEnabled);
    }

    internal static SkUiTabView CreateSample()
    {
        var list = new SkUiVerticalStackLayout { Padding = new Thickness(16), Spacing = 8 };
        for (var index = 1; index <= 40; index++)
            list.Children.Add(new SkUiLabel { Text = $"Post {index}: scroll down, switch tabs and come back" });
        var tabs = new SkUiTabView();
        tabs.AddTab(new SkUiTabItem("Posts", new SkUiScrollView { Content = list }));
        tabs.AddTab(new SkUiTabItem
        {
            Header = "Media", BadgeText = "3",
            ContentTemplate = new DataTemplate(() => new SkUiLabel
            {
                Text = $"Made from a template the first time it showed ({DateTime.Now:T})",
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
            })
        });
        tabs.AddTab(new SkUiTabItem("Info", new SkUiLabel { Text = "Disabled: swipes and keys skip it" }) { IsEnabled = false });
        tabs.AddTab(new SkUiTabItem("About", new SkUiLabel { Text = "The last tab", HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }));
        return tabs;
    }
}
