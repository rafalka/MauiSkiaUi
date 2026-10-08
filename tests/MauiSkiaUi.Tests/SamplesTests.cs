using MauiSkiaUiSamples;
using MauiSkiaUiSamples.Samples.Controls;
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
            Assert.Equal(Path.GetFileName(info.SourceFiles[0].Key), new SourcePage(info).Title);
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
