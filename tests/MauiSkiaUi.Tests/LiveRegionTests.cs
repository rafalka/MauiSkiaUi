using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Live regions (1.18 L, #131) on the semantics owner, platform-neutral: which changes are read, coalescing, hidden and
/// appearing elements, <see cref="SkUiAccessibility.Announce(SkUiView, string, SkUiLiveRegion)"/> and <c>UpdatesFrequently</c>.
/// The owner's report and read timers are driven by hand (<c>Flush(now)</c>, <c>ReadDueLiveRegions(now)</c>): headless has no dispatcher.
/// </summary>
public class LiveRegionTests
{
    private sealed class Reader
    {
        public readonly List<(int? Id, string Text, SkUiLiveRegion Politeness)> Read = [];
        public readonly SkUiSemanticsOwner Owner;
        private readonly SkUiView _root;

        public Reader(SkUiView root)
        {
            _root = root;
            SkUiTestHelpers.Arrange(root, 300, 400);
            Owner = new SkUiSemanticsOwner(root);
            Owner.Changed += (_, _) => { };
            Owner.Announcement += (node, text, politeness) => Read.Add((node?.Id, text, politeness));
        }

        /// <summary>The drawn tree changed at <paramref name="now"/> ms: relayout and report.</summary>
        public void Changed(long now)
        {
            SkUiTestHelpers.Arrange(_root, 300, 400);
            Owner.Invalidate();
            Owner.Flush(now);
        }

        public void ReadDue(long now) => Owner.ReadDueLiveRegions(now);
    }

    private static SkUiLabel Label(string text, SkUiLiveRegion live = SkUiLiveRegion.None)
    {
        var label = new SkUiLabel { Text = text, WidthRequest = 120, HeightRequest = 20 };
        SkUiAccessibility.SetLiveRegion(label, live);
        return label;
    }

    private static SkUiVerticalStackLayout Stack(params ISkUiView[] children)
    {
        var stack = new SkUiVerticalStackLayout();
        foreach (var child in children)
            stack.Children.Add(child);
        return stack;
    }

    [Fact]
    public void TheLastTextWithinTheDelayIsReadOnce()
    {
        var status = Label("Clocked out", SkUiLiveRegion.Polite);
        var other = Label("Not live");
        var reader = new Reader(Stack(status, other));
        var id = reader.Owner.Tree.Find(status)!.Id;
        Assert.Empty(reader.Read); // what shows when reading starts is not a change

        status.Text = "Clocking in";
        other.Text = "Changed too";
        reader.Changed(1_000);
        status.Text = "Clocked in at 9:02";
        reader.Changed(1_100);
        reader.ReadDue(1_299);
        Assert.Empty(reader.Read);
        reader.ReadDue(1_300);
        Assert.Equal([(id, "Clocked in at 9:02", SkUiLiveRegion.Polite)], reader.Read);

        reader.Changed(2_000); // nothing new
        reader.ReadDue(5_000);
        Assert.Single(reader.Read);

        status.Text = "Clocking out";
        reader.Changed(6_000);
        status.Text = "Clocked in at 9:02"; // back to what was read within the delay: nothing to say
        reader.Changed(6_100);
        reader.ReadDue(7_000);
        Assert.Single(reader.Read);
    }

    [Fact]
    public void HiddenAndOffScreenElementsAreNotRead()
    {
        var status = Label("Idle", SkUiLiveRegion.Assertive);
        var reader = new Reader(Stack(status));

        status.IsVisible = false;
        status.Text = "Hidden change";
        reader.Changed(0);
        reader.ReadDue(1_000);
        Assert.Empty(reader.Read);

        status.IsVisible = true;
        status.TranslationX = 1_000; // outside the surface
        status.Text = "Off-screen change";
        reader.Changed(2_000);
        reader.ReadDue(3_000);
        Assert.Empty(reader.Read);

        status.TranslationX = 0; // back: read once, as an element that appears
        reader.Changed(4_000);
        reader.ReadDue(5_000);
        Assert.Equal(("Off-screen change", SkUiLiveRegion.Assertive), (reader.Read.Single().Text, reader.Read.Single().Politeness));
    }

    [Fact]
    public void AnElementThatAppearsIsReadOnce()
    {
        var stack = Stack(Label("Form"));
        var reader = new Reader(stack);
        var error = Label("Enter a time after 6:00", SkUiLiveRegion.Assertive);
        stack.Children.Add(error);
        reader.Changed(0);
        reader.ReadDue(300);
        Assert.Equal("Enter a time after 6:00", reader.Read.Single().Text);

        reader.Changed(400);
        reader.ReadDue(1_000);
        Assert.Single(reader.Read);
    }

    [Fact]
    public void AContainersRegionCoversTheElementsInside()
    {
        var hours = Label("7 h");
        var breaks = Label("30 min");
        var card = Stack(hours, breaks);
        SkUiAccessibility.SetLiveRegion(card, SkUiLiveRegion.Polite);
        var reader = new Reader(Stack(card));
        Assert.All(new[] { hours, breaks }, label => Assert.Equal(SkUiLiveRegion.Polite, reader.Owner.Tree.Find(label)!.LiveRegion));

        breaks.Text = "45 min";
        reader.Changed(0);
        reader.ReadDue(300);
        Assert.Equal("45 min", reader.Read.Single().Text);

        // A live text inside a button: the button reads it as part of its name.
        var badge = Label("3 new", SkUiLiveRegion.Polite);
        var button = new SkUiBorder { Content = badge, WidthRequest = 120, HeightRequest = 40 };
        button.Tapped += (_, _) => { };
        var second = new Reader(Stack(button));
        Assert.Equal(SkUiLiveRegion.Polite, second.Owner.Tree.Find(button)!.LiveRegion);
        badge.Text = "4 new";
        second.Changed(0);
        second.ReadDue(300);
        Assert.Equal("4 new", second.Read.Single().Text);
    }

    [Fact]
    public void ValuesAreReadWithTheName()
    {
        var bar = new SkUiProgressBar { Progress = 0.2 };
        SemanticProperties.SetDescription(bar, "Upload");
        SkUiAccessibility.SetLiveRegion(bar, SkUiLiveRegion.Polite);
        var reader = new Reader(Stack(bar));
        bar.Progress = 0.5;
        reader.Changed(0);
        reader.ReadDue(300);
        var text = reader.Read.Single().Text;
        Assert.StartsWith("Upload, ", text);
        Assert.Contains("50", text);
    }

    [Fact]
    public void CoreNodesTakeLiveRegionsAndUpdatesFrequently()
    {
        var label = new SkUiCoreLabel().SetText("0:00");
        label.SetWidth(120).SetHeight(20);
        label.SetLiveRegion(SkUiLiveRegion.Polite).SetUpdatesFrequently(true);
        var host = new SkUiCoreHost().SetContent(label);
        var reader = new Reader(host);
        var node = reader.Owner.Tree.Find(label)!;
        Assert.Equal(SkUiLiveRegion.Polite, node.LiveRegion);
        Assert.True(node.UpdatesFrequently);

        label.SetText("0:01");
        reader.Changed(0);
        reader.ReadDue(300);
        Assert.Equal("0:01", reader.Read.Single().Text);
        Assert.Throws<ArgumentOutOfRangeException>(() => label.SetLiveRegion((SkUiLiveRegion)7));

        var timer = Label("12:00");
        SkUiAccessibility.SetUpdatesFrequently(timer, true);
        Assert.True(new Reader(Stack(timer)).Owner.Tree.Find(timer)!.UpdatesFrequently);
    }

    [Fact]
    public void AnnounceReadsTextTiedToNoElement()
    {
        var button = new SkUiButton { Text = "Save" };
        var reader = new Reader(Stack(button));
        SkUiAccessibility.Announce(button, "  Saved ");
        SkUiAccessibility.Announce(button, "Ignored", SkUiLiveRegion.None);
        SkUiAccessibility.Announce(button, " ");
        Assert.Equal([(null, "Saved", SkUiLiveRegion.Polite)], reader.Read);

        var core = new SkUiCoreButton().SetText("Core");
        var host = new SkUiCoreHost().SetContent(core);
        var second = new Reader(host);
        SkUiAccessibility.Announce(core, "Done", SkUiLiveRegion.Assertive);
        Assert.Equal([(null, "Done", SkUiLiveRegion.Assertive)], second.Read);

        // No screen reader on the surface: nothing happens.
        SkUiAccessibility.Announce(new SkUiButton(), "Nobody hears this");
    }

    [Fact]
    public void NothingIsKeptOnceNobodyListens()
    {
        var status = Label("A", SkUiLiveRegion.Polite);
        var root = Stack(status);
        SkUiTestHelpers.Arrange(root, 300, 400);
        var owner = new SkUiSemanticsOwner(root);
        var read = 0;
        owner.Announcement += (_, _, _) => read++;
        status.Text = "B"; // no change listener: no detection
        owner.Invalidate();
        owner.Flush(0);
        owner.ReadDueLiveRegions(1_000);
        Assert.Equal(0, read);
    }
}
