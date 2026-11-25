using System.Collections.ObjectModel;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.17 S4 <see cref="SkUiSegmentedControl"/>: equal and content widths and the look's height, a tap selecting a segment
/// (events, command, the selected item), a tap on the selected or a disabled segment doing nothing, coercion of the selected
/// index (also before the segments), the selected item setting the index, items with a display binding (the selection kept
/// where it moves, clamped when removed), right to left, the marker drawn in the selection color and sliding after a tap.
/// </summary>
public class SegmentedControlTests
{
    private static long _pointer = 290_000;

    private static SkUiSegmentedControl Control(params string[] texts) =>
        new SkUiSegmentedControl { FontFamily = SkUiTestHelpers.BundledFontFamily }.SetSegments(texts.Length == 0 ? ["Day", "Week", "Month"] : texts);

    private static void Tap(SkUiView root, Point at, double ms = 0)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromMilliseconds(ms)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromMilliseconds(ms + 40)));
    }

    private static Point Center(SkUiSegmentedControl control, int index) => control.SegmentBounds(index).Center;

    [Fact]
    public void EqualSegmentsAreAsWideAsTheWidestAndTheControlIsTheLooksHeight()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var control = Control();

        var size = control.Measure(double.PositiveInfinity, double.PositiveInfinity);

        var metrics = SkUiLook.Current.SegmentedMetrics;
        Assert.Equal(metrics.Height, size.Height);
        var widest = control.Model.Count == 3 ? size.Width - 2 * metrics.Inset : 0;
        Assert.Equal(0, widest % 3, 6); // three equal segments
        control.SetSegmentWidthMode(SkUiSegmentWidthMode.Content);
        var content = control.Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.True(content.Width < size.Width, "content widths are narrower than three of the widest");

        using var surface = new SkUiTestSurface(control, 300, 40);
        surface.Frame(0);
        var day = control.SegmentBounds(0);
        var month = control.SegmentBounds(2);
        Assert.True(month.Width > day.Width, "Month is wider than Day in content mode");
        Assert.Equal(300 - metrics.Inset, month.Right, 3);
    }

    [Fact]
    public void ATapSelectsASegmentRaisesTheEventAndRunsTheCommandOnce()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var runs = 0;
        var control = Control();
        control.Command = new Command(() => runs++);
        var changes = new List<(int, int)>();
        control.SelectionChanged += (_, args) => changes.Add((args.PreviousIndex, args.CurrentIndex));
        using var surface = new SkUiTestSurface(control, 300, 40);
        surface.Frame(0);
        Assert.Equal(-1, control.SelectedIndex);

        Tap(control, Center(control, 1));

        Assert.Equal(1, control.SelectedIndex);
        Assert.Same(control.Segments[1], control.SelectedItem);
        Assert.Equal([(-1, 1)], changes);
        Assert.Equal(1, runs);

        Tap(control, Center(control, 1), 100); // the selected one: nothing
        Assert.Single(changes);
        Assert.Equal(1, runs);

        control.SelectedIndex = 2; // the app: the event, no command
        Assert.Equal([(-1, 1), (1, 2)], changes);
        Assert.Equal(1, runs);
    }

    [Fact]
    public void ADisabledSegmentIgnoresTapsAndTheAppCanStillSelectIt()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var control = Control();
        control.Segments[2].IsEnabled = false;
        control.SelectedIndex = 0;
        using var surface = new SkUiTestSurface(control, 300, 40);
        surface.Frame(0);

        Tap(control, Center(control, 2));
        Assert.Equal(0, control.SelectedIndex);
        control.SelectedIndex = 2;
        Assert.Equal(2, control.SelectedIndex);

        control.IsEnabled = false;
        Tap(control, Center(control, 0), 100);
        Assert.Equal(2, control.SelectedIndex);
    }

    [Fact]
    public void TheSelectedIndexIsCoercedToTheSegmentsAndKeptUntilTheyCome()
    {
        var control = new SkUiSegmentedControl { SelectedIndex = 4 };
        Assert.Equal(4, control.SelectedIndex);

        control.SetSegments("A", "B", "C", "D", "E", "F");
        Assert.Equal(4, control.SelectedIndex);
        Assert.Same(control.Segments[4], control.SelectedItem);

        control.SelectedIndex = 9;
        Assert.Equal(5, control.SelectedIndex);
        control.SelectedIndex = -5; // invalid: ignored, as MAUI ignores invalid values
        Assert.Equal(5, control.SelectedIndex);

        control.SelectedItem = control.Segments[1];
        Assert.Equal(1, control.SelectedIndex);
        control.SelectedItem = "not a segment";
        Assert.Same(control.Segments[1], control.SelectedItem);
        control.SelectedItem = null;
        Assert.Equal(-1, control.SelectedIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() => control.SetSelectedIndex(-2));
    }

    private sealed record Filter(string Name);

    [Fact]
    public void ItemsShowTheirDisplayTextsAndTheSelectedItemStaysWhereItMoves()
    {
        var items = new ObservableCollection<Filter> { new("All"), new("Unread"), new("Flagged") };
        var control = new SkUiSegmentedControl { ItemDisplayBinding = new Binding(nameof(Filter.Name)), ItemsSource = items, SelectedIndex = 1 };

        Assert.Equal(["All", "Unread", "Flagged"], Enumerable.Range(0, control.SegmentCount).Select(control.Model.TextOf));
        Assert.Same(items[1], control.SelectedItem);

        items.Insert(0, new Filter("Starred"));
        Assert.Equal(2, control.SelectedIndex); // Unread moved
        Assert.Same(items[2], control.SelectedItem);

        items.RemoveAt(2);
        Assert.Equal(2, control.SelectedIndex); // removed: the index stays within the items
        Assert.Equal("Flagged", ((Filter)control.SelectedItem!).Name);

        control.ItemsSource = new[] { new Filter("One") };
        Assert.Equal(0, control.SelectedIndex);

        control.SetSegments("Segments", "win"); // segments win over items
        Assert.Equal(["Segments", "win"], Enumerable.Range(0, control.SegmentCount).Select(control.Model.TextOf));
    }

    [Fact]
    public void RightToLeftPutsTheFirstSegmentAtTheRightEnd()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var control = Control();
        control.FlowDirection = FlowDirection.RightToLeft;
        using var surface = new SkUiTestSurface(control, 300, 40);
        surface.Frame(0);

        Assert.True(control.SegmentBounds(0).X > control.SegmentBounds(2).X);
        Tap(control, new Point(290, 20));
        Assert.Equal(0, control.SelectedIndex);
    }

    [Fact]
    public void TheMarkerIsDrawnInTheSelectionColorAndSlidesAfterATap()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var control = Control();
        control.SelectionColor = Colors.Red;
        control.SelectedIndex = 0;
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = control, Background = Colors.White }, 300, 36);
        surface.Frame(0);
        Assert.Equal(1, control.Model.SelectionProgress); // set before it was drawn: at once

        var first = control.SegmentBounds(0);
        Assert.Equal(SKColors.Red, surface.Bitmap.GetPixel((int)first.Left + 6, (int)first.Center.Y));

        Tap(control, Center(control, 2), 100);
        Assert.Equal(2, control.SelectedIndex);
        Assert.True(control.Model.SelectionProgress < 1, "the marker slides");
        for (var ms = 150d; ms < 800; ms += 16)
        {
            surface.Root.AnimationClock.Tick(TimeSpan.FromMilliseconds(ms)); // UI-thread transitions
            surface.Frame(ms);
        }
        Assert.Equal(1, control.Model.SelectionProgress);
        var third = control.SegmentBounds(2);
        Assert.Equal(SKColors.Red, surface.Bitmap.GetPixel((int)third.Right - 6, (int)third.Center.Y));
        Assert.NotEqual(SKColors.Red, surface.Bitmap.GetPixel((int)first.Left + 6, (int)first.Center.Y));
    }
}

/// <summary>1.17 S4: under reduce motion a segmented control's marker moves at once (global state: reduce motion).</summary>
[Collection(GlobalStateCollection.Name)]
public class SegmentedControlMotionTests
{
    [Fact]
    public void UnderReduceMotionTheMarkerMovesAtOnce()
    {
        var saved = SkUiMotion.ReduceMotion;
        SkUiMotion.ReduceMotion = true;
        try
        {
            var control = new SkUiSegmentedControl().SetSegments("A", "B").SetSelectedIndex(0);
            using var surface = new SkUiTestSurface(control, 200, 36);
            surface.Frame(0);
            control.SelectedIndex = 1;
            Assert.Equal(1, control.Model.SelectionProgress);
        }
        finally
        {
            SkUiMotion.ReduceMotion = saved;
        }
    }
}
