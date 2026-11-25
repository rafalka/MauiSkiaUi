using System.Collections.ObjectModel;
using System.ComponentModel;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.17 S5 <see cref="SkUiCoreSegmentedControl"/>: drawn the same as <see cref="SkUiSegmentedControl"/> (pixels compared, with
/// a disabled segment, in both width modes and right to left), taps (events, command, disabled and selected segments), the
/// selected index coerced and the index asked for coming back as segments are added, the selected item, items with a
/// display function keeping the selection where it moves, and change notification.
/// </summary>
public class CoreSegmentedControlTests
{
    private static long _pointer = 330_000;

    private static SkUiCoreSegmentedControl Control() =>
        new SkUiCoreSegmentedControl().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetSegments("Day", "Week", "Month");

    private static SkUiCoreHost Host(SkUiCoreNode node) => new SkUiCoreHost().SetContent(node);

    private static void Tap(SkUiView root, Point at, double ms = 0)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromMilliseconds(ms)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromMilliseconds(ms + 40)));
    }

    [Theory]
    [InlineData(SkUiSegmentWidthMode.Equal, false)]
    [InlineData(SkUiSegmentWidthMode.Content, false)]
    [InlineData(SkUiSegmentWidthMode.Equal, true)]
    public void BothLayersDrawTheSame(SkUiSegmentWidthMode mode, bool rightToLeft)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var skui = new SkUiSegmentedControl { FontFamily = SkUiTestHelpers.BundledFontFamily, SegmentWidthMode = mode, SelectedIndex = 1 }
            .SetSegments("Day", "Week", "Month");
        skui.Segments[2].IsEnabled = false;
        var core = Control().SetSegmentWidthMode(mode).SetSelectedIndex(1);
        core.Segments[2].IsEnabled = false;
        var direction = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        var host = Host(core);
        host.FlowDirection = direction;
        using var skuiSurface = new SkUiTestSurface(new SkUiContentView { Content = skui, Background = Colors.White, FlowDirection = direction }, 300, 36);
        using var coreSurface = new SkUiTestSurface(new SkUiContentView { Content = host, Background = Colors.White }, 300, 36);
        skuiSurface.Frame(0);
        coreSurface.Frame(0);

        Assert.Equal(skui.SegmentBounds(2), core.SegmentBounds(2));
        var (differing, ink) = (0, 0);
        for (var y = 0; y < 36; y++)
            for (var x = 0; x < 300; x++)
            {
                var pixel = skuiSurface.Bitmap.GetPixel(x, y);
                if (pixel != coreSurface.Bitmap.GetPixel(x, y))
                    differing++;
                if (pixel != SKColors.White)
                    ink++;
            }
        Assert.True(ink > 1000, $"only {ink} drawn pixels: compare a drawn control");
        Assert.Equal(0, differing);
    }

    [Fact]
    public void ATapSelectsRunsTheCommandAndDisabledOrSelectedSegmentsDoNothing()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var runs = 0;
        var core = Control().SetCommand(new Command(() => runs++));
        core.Segments[2].SetIsEnabled(false);
        var changes = new List<(int, int)>();
        core.SelectionChanged += (_, args) => changes.Add((args.PreviousIndex, args.CurrentIndex));
        var host = Host(core);
        using var surface = new SkUiTestSurface(host, 300, 40);
        surface.Frame(0);

        Tap(host, core.SegmentBounds(1).Center);
        Assert.Equal(1, core.SelectedIndex);
        Assert.Same(core.Segments[1], core.SelectedItem);
        Tap(host, core.SegmentBounds(1).Center, 100);
        Tap(host, core.SegmentBounds(2).Center, 200);
        Assert.Equal(1, core.SelectedIndex);
        Assert.Equal([(-1, 1)], changes);
        Assert.Equal(1, runs);

        core.SetSelectedIndex(2); // code can select a disabled segment, and runs no command
        Assert.Equal([(-1, 1), (1, 2)], changes);
        Assert.Equal(1, runs);
    }

    [Fact]
    public void TheIndexAskedForComesBackAsSegmentsAreAddedAndIsCoercedToThem()
    {
        var core = new SkUiCoreSegmentedControl().SetSelectedIndex(4);
        Assert.Equal(4, core.SelectedIndex);
        foreach (var text in new[] { "A", "B", "C", "D", "E", "F" })
            core.Segments.Add(new SkUiCoreSegment(text)); // one by one
        Assert.Equal(4, core.SelectedIndex);

        core.SetSelectedIndex(9);
        Assert.Equal(5, core.SelectedIndex);
        core.SetSelectedItem(core.Segments[0]);
        Assert.Equal(0, core.SelectedIndex);
        core.SetSelectedItem("not a segment");
        Assert.Equal(0, core.SelectedIndex);
        core.SetSelectedItem(null);
        Assert.Equal(-1, core.SelectedIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetSelectedIndex(-2));
    }

    private sealed record Filter(string Name);

    [Fact]
    public void ItemsShowTheirTextsAndTheSelectionStaysWhereTheItemMoves()
    {
        var items = new ObservableCollection<Filter> { new("All"), new("Unread") };
        var core = new SkUiCoreSegmentedControl().SetItemDisplay(static item => ((Filter)item!).Name).SetItemsSource(items).SetSelectedIndex(1);
        var notified = new List<string?>();
        ((INotifyPropertyChanged)core).PropertyChanged += (_, args) => notified.Add(args.PropertyName);

        Assert.Equal(["All", "Unread"], Enumerable.Range(0, core.SegmentCount).Select(core.Model.TextOf));
        items.Insert(0, new Filter("Starred"));

        Assert.Equal(2, core.SelectedIndex);
        Assert.Same(items[2], core.SelectedItem);
        Assert.Contains(nameof(SkUiCoreSegmentedControl.SelectedIndex), notified);
        Assert.DoesNotContain(nameof(SkUiCoreSegmentedControl.SelectedItem), notified); // the same item
    }
}
