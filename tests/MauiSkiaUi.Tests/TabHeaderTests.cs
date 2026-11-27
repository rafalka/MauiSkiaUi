using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.18 T custom tab headers: a header template for the whole strip or one tab (<see cref="SkUiTabView.HeaderTemplate"/>,
/// <see cref="SkUiTabItem.HeaderTemplate"/>, a selector choosing by the tab), the view bound to its tab item, measured by its
/// content in content widths and sharing the strip in equal ones, making the strip taller, laid out again when its content
/// changes, placed in its header right to left, the look drawing nothing under it, the selected and disabled states and the
/// look's text color reaching it, taps going through to the strip, screen readers reading the header text, and the Core
/// tab view's header nodes and factory, drawn as the SkUi tab view's.
/// </summary>
public class TabHeaderTests
{
    private static long _pointer = 420_000;

    private static SkUiTabView TabView(params string[] headers)
    {
        var tabs = new SkUiTabView { FontFamily = SkUiTestHelpers.BundledFontFamily, Background = Colors.White };
        foreach (var header in headers.Length == 0 ? ["Posts", "Media", "Likes"] : headers)
            tabs.AddTab(new SkUiTabItem(header, new SkUiBox { Color = Colors.LightGray }));
        return tabs;
    }

    private static DataTemplate LabelTemplate() => new(() =>
    {
        var label = new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, Padding = new Thickness(12, 0), VerticalOptions = LayoutOptions.Center };
        label.SetBinding(SkUiLabel.TextProperty, static (SkUiTabItem tab) => tab.Header);
        return label;
    });

    private static DataTemplate BoxTemplate(Color color, double width = -1, double height = -1) =>
        new(() => new SkUiBox { Color = color, WidthRequest = width, HeightRequest = height });

    private static void Tap(SkUiView root, Point at, double ms)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromMilliseconds(ms)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromMilliseconds(ms + 40)));
    }

    private static Rect CellFrame(ISkUiView? view) => ((View)((Element)view!).Parent).Frame;

    private static SKColor Sk(Color color) => new((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), (byte)(color.Alpha * 255));

    [Fact]
    public void AStripTemplateMakesOneViewPerTabBoundToItsTabInItsHeader()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var tabs = TabView();
        tabs.HeaderTemplate = LabelTemplate();
        using var surface = new SkUiTestSurface(tabs, 300, 300);
        surface.Frame(0);

        for (var index = 0; index < 3; index++)
        {
            var label = Assert.IsType<SkUiLabel>(tabs.HeaderViewOf(index));
            Assert.Same(tabs.Tabs[index], label.BindingContext);
            Assert.Equal(tabs.Tabs[index].Header, label.Text);
            Assert.Equal(tabs.HeaderBounds(index), CellFrame(label));
        }
        var first = tabs.HeaderViewOf(0);
        tabs.Tabs[0].Header = "Stories";
        surface.Frame(16);
        Assert.Same(first, tabs.HeaderViewOf(0)); // made once
        Assert.Equal("Stories", ((SkUiLabel)first!).Text);
    }

    [Fact]
    public void HeaderViewsMeasureByTheirContentAndMakeTheStripTaller()
    {
        var tabs = TabView();
        tabs.TabWidthMode = SkUiSegmentWidthMode.Content;
        tabs.Tabs[0].HeaderTemplate = BoxTemplate(Colors.Red, 40, 20);
        tabs.Tabs[1].HeaderTemplate = BoxTemplate(Colors.Green, 80, 70);
        tabs.Tabs[2].HeaderTemplate = BoxTemplate(Colors.Blue, 120, 20);
        using var surface = new SkUiTestSurface(tabs, 200, 300); // narrower than the headers: they are not stretched to fill
        surface.Frame(0);

        Assert.Equal([40d, 80d, 120d], Enumerable.Range(0, 3).Select(index => tabs.HeaderBounds(index).Width));
        Assert.Equal(70, tabs.HeaderBounds(0).Height, 1); // the tallest header view, above the look's 44

        // A header view whose size changes lays the strip out again.
        ((SkUiBox)tabs.HeaderViewOf(0)!).WidthRequest = 60;
        surface.Frame(16);
        Assert.Equal(60, tabs.HeaderBounds(0).Width, 1);
        Assert.Equal(60, tabs.HeaderBounds(1).Left, 1);
        Assert.Equal(tabs.HeaderBounds(0), CellFrame(tabs.HeaderViewOf(0)));

        tabs.TabWidthMode = SkUiSegmentWidthMode.Equal;
        surface.Frame(32);
        Assert.All(Enumerable.Range(0, 3), index => Assert.Equal(200 / 3d, tabs.HeaderBounds(index).Width, 1));
    }

    [Fact]
    public void ALabelWhoseTextGrowsWidensItsHeader()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var tabs = TabView();
        tabs.TabWidthMode = SkUiSegmentWidthMode.Content;
        tabs.HeaderTemplate = LabelTemplate();
        using var surface = new SkUiTestSurface(tabs, 400, 300);
        surface.Frame(0);
        var before = tabs.HeaderBounds(0).Width;

        tabs.Tabs[0].Header = "Posts and stories";
        surface.Frame(16);

        Assert.True(tabs.HeaderBounds(0).Width > before + 20, $"{tabs.HeaderBounds(0).Width} after {before}");
        Assert.Equal(tabs.HeaderBounds(0).Right, tabs.HeaderBounds(1).Left, 1);
    }

    [Fact]
    public void ATabsOwnTemplateWinsASelectorChoosesAndWithoutOneTheLookDrawsTheHeader()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView();
        tabs.IndicatorStyle = SkUiTabIndicatorStyle.None;
        var empty = new DataTemplate(() => new SkUiBox());
        var boxes = new DataTemplate(() => new SkUiBox { Color = Colors.Red });
        tabs.HeaderTemplate = new HeaderSelector(empty, boxes);
        tabs.Tabs[2].HeaderTemplate = LabelTemplate();
        using var surface = new SkUiTestSurface(tabs, 300, 300);
        surface.Frame(0);

        Assert.IsType<SkUiBox>(tabs.HeaderViewOf(0));
        Assert.Equal(Colors.Red, ((SkUiBox)tabs.HeaderViewOf(1)!).Color); // the selector chose by the tab
        Assert.IsType<SkUiLabel>(tabs.HeaderViewOf(2));
        // Under an empty header view the look draws no text.
        var posts = tabs.HeaderBounds(0);
        var background = surface.Bitmap.GetPixel((int)posts.Left + 1, (int)posts.Top + 1);
        Assert.True(Pixels(surface, posts).All(pixel => pixel == background));

        tabs.HeaderTemplate = null;
        surface.Frame(16);
        Assert.Null(tabs.HeaderViewOf(0));
        Assert.IsType<SkUiLabel>(tabs.HeaderViewOf(2));
        Assert.Contains(Pixels(surface, posts), pixel => pixel != background); // the look's text again
    }

    private static IEnumerable<SKColor> Pixels(SkUiTestSurface surface, Rect bounds)
    {
        for (var y = (int)bounds.Top + 1; y < (int)bounds.Bottom - 1; y++)
            for (var x = (int)bounds.Left + 1; x < (int)bounds.Right - 1; x++)
                yield return surface.Bitmap.GetPixel(x, y);
    }

    private sealed class HeaderSelector(DataTemplate first, DataTemplate others) : DataTemplateSelector
    {
        protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
            ((SkUiTabItem)item).Header == "Posts" ? first : others;
    }

    [Fact]
    public void TheSelectedAndDisabledStatesAndTheLooksTextColorReachTheHeaderViews()
    {
        var tabs = TabView();
        tabs.HeaderTextColor = Colors.Blue;
        tabs.SelectedHeaderTextColor = Colors.Red;
        tabs.HeaderTemplate = BoxTemplate(Colors.Gray);
        tabs.Tabs[2].IsEnabled = false;
        using var surface = new SkUiTestSurface(tabs, 300, 300);
        surface.Frame(0);

        Assert.Equal([true, false, false], tabs.Tabs.Select(static tab => tab.IsSelected));
        Assert.Equal(Colors.Red.ToArgbHex(), tabs.Tabs[0].CurrentTextColor!.ToArgbHex());
        Assert.Equal(Colors.Blue.ToArgbHex(), tabs.Tabs[1].CurrentTextColor!.ToArgbHex());
        Assert.NotEqual(Colors.Blue.ToArgbHex(), tabs.Tabs[2].CurrentTextColor!.ToArgbHex()); // dimmed
        Assert.True(((SkUiView)tabs.HeaderViewOf(0)!).IsSelectedItem);
        Assert.False(((SkUiView)tabs.HeaderViewOf(2)!).IsEnabled);

        var changes = new List<string?>();
        tabs.Tabs[1].PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        tabs.SelectedIndex = 1;
        Assert.Equal([false, true, false], tabs.Tabs.Select(static tab => tab.IsSelected));
        Assert.Equal(Colors.Red.ToArgbHex(), tabs.Tabs[1].CurrentTextColor!.ToArgbHex());
        Assert.Contains(nameof(SkUiTabItem.IsSelected), changes);
        Assert.False(((SkUiView)tabs.HeaderViewOf(0)!).IsSelectedItem);
        Assert.True(((SkUiView)tabs.HeaderViewOf(1)!).IsSelectedItem);

        tabs.SelectedHeaderTextColor = Colors.Green;
        Assert.Equal(Colors.Green.ToArgbHex(), tabs.Tabs[1].CurrentTextColor!.ToArgbHex());

        var removed = tabs.Tabs[1];
        tabs.Tabs.RemoveAt(1);
        Assert.False(removed.IsSelected);
        Assert.Null(removed.CurrentTextColor);
    }

    [Fact]
    public void ATapOnAHeaderViewSelectsItsTabAndScreenReadersReadTheHeaderText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tabs = TabView();
        var presses = 0;
        tabs.HeaderTemplate = new DataTemplate(() =>
        {
            var button = new SkUiButton { Text = "Inner", FontFamily = SkUiTestHelpers.BundledFontFamily };
            button.Clicked += (_, _) => presses++;
            return button;
        });
        using var surface = new SkUiTestSurface(tabs, 300, 300);
        surface.Frame(0);

        Tap(tabs, tabs.HeaderBounds(2).Center, 0);
        Assert.Equal(2, tabs.SelectedIndex);
        Assert.Equal(0, presses); // the header view takes no input of its own

        var labels = new List<string?>();
        void Walk(SkUiSemanticsNode node)
        {
            labels.Add(node.Label);
            foreach (var child in node.Children)
                Walk(child);
        }
        Walk(SkUiSemanticsTree.Build(tabs).Root);
        Assert.Contains("Posts", labels);
        Assert.Contains("Likes", labels);
        Assert.DoesNotContain("Inner", labels);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeaderViewsSitInTheirHeadersAndTheCoreTabViewDrawsTheSame(bool rightToLeft)
    {
        Color[] colors = [Colors.Red, Colors.Green, Colors.Blue];
        var tabs = TabView();
        tabs.FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        tabs.TabWidthMode = SkUiSegmentWidthMode.Content;
        for (var index = 0; index < 3; index++)
            tabs.Tabs[index].HeaderTemplate = BoxTemplate(colors[index], 50 + 20 * index, 30);
        using var surface = new SkUiTestSurface(tabs, 300, 200);
        surface.Frame(0);

        var core = new SkUiCoreTabView().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetTabWidthMode(SkUiSegmentWidthMode.Content);
        foreach (var header in new[] { "Posts", "Media", "Likes" })
            core.AddTab(new SkUiCoreTabItem(header, new SkUiCoreBox().SetColor(Colors.LightGray)));
        core.SetHeaderFactory(tab =>
        {
            var index = core.Tabs.IndexOf(tab);
            var box = new SkUiCoreBox().SetColor(colors[index]);
            box.SetWidth(50 + 20 * index);
            box.SetHeight(30);
            return box;
        });
        using var coreSurface = new SkUiTestSurface(new SkUiCoreHost
        {
            FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, Background = Colors.White
        }.SetContent(core), 300, 200);
        coreSurface.Frame(0);

        for (var index = 0; index < 3; index++)
        {
            var header = tabs.HeaderBounds(index);
            Assert.Equal(header, core.HeaderBounds(index));
            Assert.Equal(Sk(colors[index]), surface.Bitmap.GetPixel((int)header.Center.X, (int)header.Center.Y));
        }
        Assert.True(rightToLeft == tabs.HeaderBounds(0).Left > tabs.HeaderBounds(1).Left);
        for (var y = 0; y < 60; y++)
            for (var x = 0; x < 300; x++)
                Assert.True(surface.Bitmap.GetPixel(x, y) == coreSurface.Bitmap.GetPixel(x, y), $"Pixel ({x}, {y}) differs.");
    }

    [Fact]
    public void CoreHeaderNodesComeFromTheTabOrTheFactoryOnceAndFollowTheState()
    {
        var core = new SkUiCoreTabView().SetFontFamily(SkUiTestHelpers.BundledFontFamily)
            .SetHeaderTextColor(Colors.Blue).SetSelectedHeaderTextColor(Colors.Red);
        foreach (var header in new[] { "Posts", "Media", "Likes" })
            core.AddTab(new SkUiCoreTabItem(header, new SkUiCoreBox()));
        var own = new SkUiCoreBox().SetColor(Colors.Purple);
        core.Tabs[2].SetHeaderContent(own);
        var made = new List<string?>();
        core.SetHeaderFactory(tab =>
        {
            made.Add(tab.Header);
            return tab.Header == "Media" ? null : new SkUiCoreLabel().SetText(tab.Header);
        });
        using var surface = new SkUiTestSurface(new SkUiCoreHost { Background = Colors.White }.SetContent(core), 300, 200);
        surface.Frame(0);

        Assert.IsType<SkUiCoreLabel>(core.HeaderNodeOf(0));
        Assert.Null(core.HeaderNodeOf(1)); // the factory made none: the look's header
        Assert.Same(own, core.HeaderNodeOf(2));
        core.Tabs[0].SetHeader("Stories"); // the strip changes: nothing is made again
        surface.Frame(16);
        Assert.Equal(["Posts", "Media"], made);

        Assert.True(core.Tabs[0].IsSelected);
        Assert.Equal(Colors.Red.ToArgbHex(), core.Tabs[0].CurrentTextColor!.ToArgbHex());
        var changes = new List<string?>();
        core.Tabs[2].PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        var id = ++_pointer;
        var at = core.HeaderBounds(2).Center;
        surface.Root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.Zero));
        surface.Root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromMilliseconds(40)));
        Assert.Equal(2, core.SelectedIndex); // through the header node
        Assert.Equal([nameof(SkUiCoreTabItem.IsSelected), nameof(SkUiCoreTabItem.CurrentTextColor)], changes);

        // A new factory makes the nodes again; the tab's own node stays.
        made.Clear();
        core.SetHeaderFactory(tab => new SkUiCoreBox());
        Assert.IsType<SkUiCoreBox>(core.HeaderNodeOf(0));
        Assert.IsType<SkUiCoreBox>(core.HeaderNodeOf(1));
        Assert.Same(own, core.HeaderNodeOf(2));

        // A tab that leaves lets its own node go.
        core.Tabs.RemoveAt(2);
        Assert.Null(own.Parent);
        core.SetHeaderFactory(null);
        Assert.Null(core.HeaderNodeOf(0));
    }
}
