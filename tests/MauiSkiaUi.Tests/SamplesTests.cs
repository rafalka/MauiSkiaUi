using MauiSkiaUiSamples;
using MauiSkiaUi.Core;
using MauiSkiaUiSamples.Samples.Controls;
using MauiSkiaUiSamples.Samples.Core;
using MauiSkiaUiSamples.Samples.Customisation;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>The samples app (samples/MauiSkiaUiSamples): every example builds, describes itself and ships its source.</summary>
[Collection(GlobalStateCollection.Name)]
public class SamplesTests
{
    [Fact]
    public void EveryExampleBuildsDescribesItselfAndShipsItsSource()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher(); // compiled bindings of XAML pages dispatch changes
        Assert.NotEmpty(SampleCatalog.All);
        foreach (var entry in SampleCatalog.All)
        {
            var info = entry.Info;
            Assert.False(string.IsNullOrWhiteSpace(info.Title));
            Assert.False(string.IsNullOrWhiteSpace(info.Summary));
            Assert.NotEmpty(info.HowTo);
            Assert.NotEmpty(info.ThingsToKnow);
            foreach (var text in info.HowTo.Concat(info.ThingsToKnow).Append(info.Summary))
                Assert.True(text.Count(c => c == '`') % 2 == 0, $"{info.Title}: unbalanced backticks in \"{text}\"");

            var page = entry.Create();
            Assert.Equal(info.Title, page.Title);
            // [CallerFilePath] names the example's own file; the embedded copy is found by that name.
            Assert.EndsWith(info.SourceFileName, info.SourcePath.Replace('\\', '/'));
            Assert.EndsWith("/" + info.SourceFileName, "/" + info.SourceKey);
            var source = SampleSource.Load(info.SourceKey);
            var className = info.SourceFileName.Split('.')[0];
            Assert.Contains($"class {className}", source);
            Assert.Contains("SampleInfo Info", source); // the description is declared in the example's own file
            // A XAML page shows its markup first, then the code-behind, then the example's other files (all embedded).
            var xaml = info.SourceKey.EndsWith(".xaml.cs");
            Assert.Equal((xaml ? 2 : 1) + info.MoreSources.Count, info.SourceFiles.Count);
            if (xaml)
                Assert.Matches($"x:Class=\"MauiSkiaUiSamples\\.Samples\\.{info.Section}(\\.\\w+)*\\.{className}\"", SampleSource.Load(info.SourceFiles[0].Key));
            Assert.All(info.SourceFiles, file => Assert.DoesNotContain("is not embedded", SampleSource.Load(file.Key)));
            var sourcePage = new SourcePage(info);
            Assert.Equal(Path.GetFileName(info.SourceFiles[0].Key), sourcePage.Title);
            // Other files: a picker; a XAML page and its code-behind alone: a switch.
            Assert.Equal(info.MoreSources.Count > 0, sourcePage.ToolbarItems.Any(item => item.Text == "Files"));
            Assert.Equal(xaml && info.MoreSources.Count == 0, sourcePage.ToolbarItems.Any(item => item.Text == "C#"));
        }
        Assert.All(SampleCatalog.Sections, section => Assert.NotEmpty(section.Description()));
        // Embedded by path under Samples/ (Section/File.cs): two sections may reuse a file name.
        var keys = SampleCatalog.All.Select(entry => entry.Info.SourceKey).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Contains("Customisation/CrossCheckBoxSample.cs", keys);
    }

    [Fact]
    public void SourceIsShownSyntaxHighlighted()
    {
        var html = SampleSource.ToHtml("public sealed class A { string s = \"<x>\"; } // note");
        Assert.Contains("<span", html);            // keywords, strings and comments get colors
        Assert.Contains("&lt;x&gt;", html);        // code is escaped
        Assert.Contains("white-space: pre", html); // not wrapped: scrolls sideways
    }

    [Fact]
    public void EditorUrlsEscapePathSegmentsButKeepTheDrive()
    {
        Assert.Equal("vscode://file/C:/My%20Projects/A%23B/Sample%3F.cs", SourceEditor.FileUrl("vscode", @"C:\My Projects\A#B\Sample?.cs"));
        Assert.Equal("cursor://file/Users/me/My%20App/Sample.cs", SourceEditor.FileUrl("cursor", "/Users/me/My App/Sample.cs"));
    }

    [Fact]
    public void PresenceAvatarsDrawTeamsBadgesAndChangeOnTap()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var page = new PresenceAvatarsSample();
        var root = Assert.IsAssignableFrom<SkUiView>(page.SampleContent);
        root.HeightRequest = 1200;
        using var surface = new SkUiTestSurface(root, 380, 1200);
        var bitmap = surface.Frame();
        if (Environment.GetEnvironmentVariable("SKIAUI_SAMPLE_PNG") is { Length: > 0 } png)
        {
            using var file = File.Create(png);
            bitmap.Encode(file, SKEncodedImageFormat.Png, 100);
        }
        var avatars = Descendants(root).OfType<PresenceAvatar>().ToList();
        var available = avatars.First(avatar => avatar.Presence == Presence.Available);
        var offline = avatars.First(avatar => avatar.Presence == Presence.Offline);

        // The badge sits at the look's place for a dot at the bottom right, in the avatar's local space.
        var tree = SkUiSemanticsTree.Build(root).Flatten().ToList();
        Point Origin(SkUiView avatar) => tree.Single(element => ReferenceEquals(element.Source, avatar)).Bounds.Location; // surface coordinates
        SKPointI BadgeCenter(SkUiView avatar)
        {
            var bounds = SkUiLook.Current.GetAvatarBadgeBounds(new SKRect(0, 0, (float)avatar.Width, (float)avatar.Height), SkUiAvatarBadgePosition.BottomRight, avatar.Width / 2);
            var origin = Origin(avatar);
            return new SKPointI((int)(origin.X + bounds.MidX), (int)(origin.Y + bounds.MidY));
        }
        var green = bitmap.GetPixel(BadgeCenter(available).X - 3, BadgeCenter(available).Y);
        Assert.True(green.Green > 150 && green.Red < 140 && green.Blue < 80, $"Available: green badge, got {green}");
        var white = bitmap.GetPixel(BadgeCenter(offline).X - 3, BadgeCenter(offline).Y - 1);
        Assert.True(white is { Red: > 230, Green: > 230, Blue: > 230 }, $"Offline: a white badge with a grey ring, got {white}");

        // A tap moves to the next status and screen readers hear it.
        var center = Origin(available);
        var id = 1;
        root.Touch(new(id, SkUiTouchAction.Pressed, new Point(center.X + 10, center.Y + 10), TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, new Point(center.X + 10, center.Y + 10), TimeSpan.FromSeconds(1.02)));
        Assert.Equal(Presence.Busy, available.Presence);
        var info = SkUiSemanticsTree.Build(root).Flatten().Single(element => ReferenceEquals(element.Source, available));
        Assert.Equal($"{available.Text}, Busy", info.Label);
    }

    private static IEnumerable<ISkUiView> Descendants(ISkUiView view)
    {
        yield return view;
        if (view is SkUiView node)
            foreach (var child in node.SkiaChildren)
                foreach (var descendant in Descendants(child))
                    yield return descendant;
    }

    [Fact]
    public void CoreStyleMethodsBuildOnABaseTakeParametersAndYieldToLocalValues()
    {
        // BasedOn: Primary starts from ButtonBase, then sets its own colors.
        var button = CoreUi.PrimaryButton("Pay", () => { });
        Assert.Equal(new CornerRadius(10), button.CornerRadii);
        Assert.Equal(new Thickness(16, 10), button.Padding);
        Assert.Equal(Colors.White, button.TextColor);
        Assert.Equal("Pay", button.Text);
        // A style with a parameter, called again on the live node.
        button.Density(compact: true);
        Assert.Equal((new Thickness(10, 4), 13d), (button.Padding, button.FontSize));
        // A value set after the factory's style wins; the rest of the style stays.
        var cancel = CoreUi.SecondaryButton("Cancel", () => { });
        cancel.SetTextColor(Colors.Red);
        Assert.Equal((Colors.Red, SampleColors.Accent), (cancel.TextColor, cancel.BorderColor));
        // The chain keeps the node's type.
        SkUiCoreButton typed = new SkUiCoreButton().Primary().Density(compact: false);
        Assert.NotNull(typed);
    }

    [Fact]
    public void CrossCheckBoxArmsSpreadFromADot()
    {
        var previous = SkUiLook.Current;
        var reduce = SkUiMotion.ReduceMotion;
        try
        {
            SkUiLook.Current = new CrossCheckBoxLook();
            SkUiMotion.ReduceMotion = false;
            var box = new SkUiCheckBox();
            var root = new SkUiContentView { Content = box };
            using var surface = new SkUiTestSurface(root, 24, 24);
            surface.Frame();
            box.IsChecked = true;

            int WhiteOnDiagonal(double milliseconds)
            {
                root.AnimationClock.Tick(TimeSpan.FromMilliseconds(milliseconds));
                var bitmap = surface.Frame(milliseconds);
                return Enumerable.Range(0, 24).Count(i => bitmap.GetPixel(i, i) is { Red: > 200, Green: > 200, Blue: > 200 });
            }
            var start = WhiteOnDiagonal(20);
            var end = WhiteOnDiagonal(400);
            Assert.InRange(start, 1, end - 1); // a dot first, then the full arm
            Assert.True(end >= 8, $"full arm {end} px");
        }
        finally
        {
            SkUiLook.Current = previous;
            SkUiMotion.ReduceMotion = reduce;
        }
    }

    [Fact]
    public void ContentViewsSampleTemplatesItsCards()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var page = new ContentViewsSample();
        var cards = ((SkUiVerticalStackLayout)page.SampleContent!).Children.OfType<InfoCard>().ToArray();
        var (delivery, address, status, details) = (cards[0], cards[2], cards[3], cards[4]);

        // The style's template: the title by ancestor type, the card's own content in the presenter.
        Assert.Contains(Descendants(delivery).OfType<SkUiLabel>(), label => label.Text == "Delivery");
        Assert.Same(delivery.Content, Descendants(delivery).OfType<SkUiContentPresenter>().Single().Content);

        // The trigger's template takes the same content.
        var content = address.Content;
        var root = address.TemplateRoot;
        page.FindByName<SkUiSwitch>("compactSwitch").IsToggled = true;
        Assert.NotSame(root, address.TemplateRoot);
        Assert.Same(content, Descendants(address).OfType<SkUiContentPresenter>().Single().Content);

        // The selector: a status of the same type keeps the content, another type replaces it.
        var packing = status.Content;
        Assert.Contains(Descendants(status).OfType<SkUiLabel>(), label => label.Text == "1 of 3 items packed");
        ((ContentViewsModel)page.BindingContext).NextStatusCommand.Execute(null);
        Assert.Same(packing, status.Content);
        Assert.Contains(Descendants(status).OfType<SkUiLabel>(), label => label.Text == "3 of 3 items packed");
        ((ContentViewsModel)page.BindingContext).NextStatusCommand.Execute(null);
        Assert.NotSame(packing, status.Content);
        Assert.Contains(Descendants(status).OfType<SkUiLabel>(), label => label.Text == "Tracking number PX 4071 2290");

        // Hidden and deferred: neither the template nor the content exists yet.
        Assert.False(details.IsContentLoaded);
        Assert.Null(details.TemplateRoot);
        details.LoadContent();
        Assert.NotNull(details.TemplateRoot);
        Assert.NotNull(details.Content);
    }

    [Fact]
    public void OrderListSampleSelectsTapsAndKeepsTheButtonsTaps()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var page = new OrderListSample();
        var model = (OrderListModel)page.BindingContext;
        var list = ((SkUiGrid)page.SampleContent!).Children.OfType<SkUiCollectionView>().Single();
        SkUiTestHelpers.Arrange(list, 360, 420);
        Assert.True(list.IsStickyHeader);
        Assert.InRange(list.LastVisibleIndex, 3, 12);

        var row = (SkUiView)list.GetRealizedView(1)!;
        var rowTop = ((View)row.Parent).Frame.Y + ((View)list.ItemsLayout).Frame.Y + ((View)list.ItemsLayout.Parent).Frame.Y - list.ScrollY; // container, items, body
        void TapAt(double x, long id)
        {
            list.Touch(new(id, SkUiTouchAction.Pressed, new Point(x, rowTop + row.Height / 2), TimeSpan.FromSeconds(id)));
            list.Touch(new(id, SkUiTouchAction.Released, new Point(x, rowTop + row.Height / 2), TimeSpan.FromSeconds(id + 0.05)));
        }

        TapAt(40, 1);
        Assert.Same(model.Orders[1], model.Selected);
        Assert.Equal($"Opened {model.Orders[1].Title}", model.Status);
        Assert.Equal("Selected", VisualStateManager.GetVisualStateGroups(row)[0].CurrentState?.Name);

        var button = Descendants(row).OfType<SkUiButton>().Single();
        TapAt(button.Frame.X + row.Frame.X + button.Width / 2, 2);
        Assert.Equal($"Paying order {model.Orders[1].Title[6..]}", model.Status);
        Assert.Same(model.Orders[1], model.Selected); // SingleDeselect would have cleared it on a row tap

        TapAt(40, 3);
        Assert.Null(model.Selected);
    }

    private static IEnumerable<SkUiView> Descendants(SkUiView view)
    {
        yield return view;
        foreach (var child in view.SkiaChildren.OfType<SkUiView>())
            foreach (var descendant in Descendants(child))
                yield return descendant;
    }
}
