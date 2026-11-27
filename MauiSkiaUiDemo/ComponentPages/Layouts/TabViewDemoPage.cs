using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiTabView"/> (1.17 S7): four tabs over swiped panes (a long list that keeps its scroll offset, a pane made
/// from a template the first time it shows, a disabled tab, a badge). Editors for the selection, the strip's placement and
/// width mode, the indicator style, swiping, and the headers (1.18 T): the look's, a template for the whole strip (a title in
/// the look's text color over a count), or one tab's own. The feedback line reports each selection change.
/// </summary>
public sealed class TabViewDemoPage : ComponentDemoPage
{
    private const string LookHeaders = "Look";
    private const string StripTemplate = "Strip template";
    private const string OneTabsOwn = "One tab's own";

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
        Choice("Headers", [LookHeaders, StripTemplate, OneTabsOwn], LookHeaders, value =>
        {
            tabs.HeaderTemplate = value == StripTemplate ? StripHeaders() : null;
            tabs.Tabs[0].HeaderTemplate = value == OneTabsOwn ? OwnHeader() : null;
        }, () => tabs.HeaderTemplate is not null ? StripTemplate : tabs.Tabs[0].HeaderTemplate is not null ? OneTabsOwn : LookHeaders);
    }

    /// <summary>Every tab: its title in the look's text color over its count (the badge text), bound to the tab item.</summary>
    private static DataTemplate StripHeaders() => new(() =>
    {
        var title = new SkUiLabel { FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
        title.SetBinding(SkUiLabel.TextProperty, static (SkUiTabItem tab) => tab.Header);
        title.SetBinding(SkUiLabel.TextColorProperty, static (SkUiTabItem tab) => tab.CurrentTextColor);
        var count = new SkUiLabel { FontSize = 11, HorizontalTextAlignment = TextAlignment.Center };
        count.SetBinding(SkUiLabel.TextProperty, static (SkUiTabItem tab) => tab.BadgeText, stringFormat: "{0} new", targetNullValue: "none new");
        count.SetBinding(SkUiLabel.TextColorProperty, static (SkUiTabItem tab) => tab.CurrentTextColor);
        return new SkUiVerticalStackLayout
        {
            Padding = new Thickness(12, 6), Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { title, count }
        };
    });

    /// <summary>The first tab only: a colored dot before its title, which grows while the tab is selected.</summary>
    private static DataTemplate OwnHeader() => new(() =>
    {
        var dot = new SkUiBox { Color = Colors.OrangeRed, CornerRadius = 5, WidthRequest = 10, HeightRequest = 10, VerticalOptions = LayoutOptions.Center };
        var title = new SkUiLabel { VerticalOptions = LayoutOptions.Center };
        title.SetBinding(SkUiLabel.TextProperty, static (SkUiTabItem tab) => tab.Header);
        title.SetBinding(SkUiLabel.TextColorProperty, static (SkUiTabItem tab) => tab.CurrentTextColor);
        var header = new SkUiHorizontalStackLayout
        {
            Padding = new Thickness(12, 0), Spacing = 6, HorizontalOptions = LayoutOptions.Center, Children = { dot, title }
        };
        // The root is in the Selected visual state while its tab is selected.
        VisualStateManager.SetVisualStateGroups(header, [new VisualStateGroup
        {
            Name = "CommonStates",
            States =
            {
                new VisualState { Name = VisualStateManager.CommonStates.Normal },
                new VisualState { Name = VisualStateManager.CommonStates.Selected, Setters = { new Setter { Property = SkUiView.ScaleProperty, Value = 1.1 } } }
            }
        }]);
        return header;
    });

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
