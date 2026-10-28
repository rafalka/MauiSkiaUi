using MauiSkiaUi.Rendering;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiGridSplitter"/> / <see cref="SkUiCoreGridSplitter"/>: track math, drags, keys, semantics, limits, drawing.</summary>
public class GridSplitterTests
{
    private const double Tolerance = 0.01;
    private static long _pointer = 19_000;

    /// <summary>A drag from <paramref name="from"/> to <paramref name="to"/> (root coordinates) in 8 steps.</summary>
    private static void Drag(SkUiView root, Point from, Point to)
    {
        var id = ++_pointer;
        var start = TimeSpan.FromSeconds(10);
        root.Touch(new(id, SkUiTouchAction.Pressed, from, start));
        for (var step = 1; step <= 8; step++)
        {
            var t = step / 8.0;
            root.Touch(new(id, SkUiTouchAction.Moved, new Point(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t), start + TimeSpan.FromMilliseconds(160 * t)));
        }
        root.Touch(new(id, SkUiTouchAction.Released, to, start + TimeSpan.FromMilliseconds(170)));
    }

    /// <summary>
    /// A 200×400 surface: a grid with <paramref name="first"/>, an Auto row holding the splitter (16 high), and
    /// <paramref name="last"/>; a box fills each outer row.
    /// </summary>
    private static (SkUiContentView Root, SkUiGrid Grid, SkUiGridSplitter Splitter, SkUiBox Below) Rows(GridLength first, GridLength last)
    {
        var splitter = new SkUiGridSplitter();
        var above = new SkUiBox();
        var below = new SkUiBox();
        var grid = new SkUiGrid { RowDefinitions = [new(first), new(GridLength.Auto), new(last)], Children = { above, splitter, below } };
        Grid.SetRow(splitter, 1);
        Grid.SetRow(below, 2);
        var root = new SkUiContentView { Content = grid };
        SkUiTestHelpers.Arrange(root, 200, 400);
        return (root, grid, splitter, below);
    }

    private static SkUiGridTrack Track(SkUiGridTrackKind kind, double value, double actual, double minimum = 0, double maximum = double.PositiveInfinity) =>
        new(kind, value, actual, minimum, maximum);

    [Fact]
    public void TwoStarTracksShareTheirWeightInTheNewProportion()
    {
        var (previous, next) = SkUiGridSplitterMath.Resize(Track(SkUiGridTrackKind.Star, 1, 100), Track(SkUiGridTrackKind.Star, 3, 300), 200);
        Assert.Equal(new SkUiGridLengthChange(2, true), previous);
        Assert.Equal(new SkUiGridLengthChange(2, true), next);
    }

    [Fact]
    public void NextToAStarTrackOnlyTheOtherOneChangesToAbsolute()
    {
        var (previous, next) = SkUiGridSplitterMath.Resize(Track(SkUiGridTrackKind.Auto, 1, 80), Track(SkUiGridTrackKind.Star, 1, 220), 130);
        Assert.Equal(new SkUiGridLengthChange(130, false), previous);
        Assert.Null(next);

        (previous, next) = SkUiGridSplitterMath.Resize(Track(SkUiGridTrackKind.Star, 1, 220), Track(SkUiGridTrackKind.Absolute, 80, 80), 250);
        Assert.Null(previous);
        Assert.Equal(new SkUiGridLengthChange(50, false), next);
    }

    [Fact]
    public void LimitsOfBothTracksBoundTheResize()
    {
        var previous = Track(SkUiGridTrackKind.Absolute, 100, 100, minimum: 60);
        var next = Track(SkUiGridTrackKind.Absolute, 100, 100, maximum: 120);
        Assert.Equal(60, SkUiGridSplitterMath.ClampFirst(previous, Track(SkUiGridTrackKind.Absolute, 100, 100), 0));
        Assert.Equal(200, SkUiGridSplitterMath.ClampFirst(previous, Track(SkUiGridTrackKind.Absolute, 100, 100), 500));
        // The next track may not grow past 120: the previous one keeps at least 80.
        Assert.Equal(80, SkUiGridSplitterMath.ClampFirst(previous, next, 70));
        Assert.Equal((80, 200), SkUiGridSplitterMath.FirstRange(previous, next));
    }

    [Fact]
    public void AutoDirectionFollowsTheAlignmentThenThePlaceInTheGrid()
    {
        const LayoutAlignment fill = LayoutAlignment.Fill;
        Assert.False(SkUiGridSplitterMath.ResizesColumns(SkUiGridResizeDirection.Auto, fill, fill, rows: 3, columns: 1, rowSpan: 1, columnSpan: 1, new Size(200, 16)));
        Assert.True(SkUiGridSplitterMath.ResizesColumns(SkUiGridResizeDirection.Auto, fill, fill, rows: 1, columns: 3, rowSpan: 1, columnSpan: 1, new Size(16, 200)));
        Assert.False(SkUiGridSplitterMath.ResizesColumns(SkUiGridResizeDirection.Auto, fill, fill, rows: 3, columns: 3, rowSpan: 1, columnSpan: 3, new Size(16, 16)));
        Assert.True(SkUiGridSplitterMath.ResizesColumns(SkUiGridResizeDirection.Auto, fill, fill, rows: 3, columns: 3, rowSpan: 3, columnSpan: 1, new Size(16, 16)));
        Assert.True(SkUiGridSplitterMath.ResizesColumns(SkUiGridResizeDirection.Columns, fill, fill, rows: 3, columns: 1, rowSpan: 1, columnSpan: 1, new Size(200, 16)));
        // A splitter at the end of a header cell: columns, whatever the grid's shape.
        Assert.True(SkUiGridSplitterMath.ResizesColumns(SkUiGridResizeDirection.Auto, LayoutAlignment.End, fill, rows: 5, columns: 3, rowSpan: 1, columnSpan: 1, default));
        Assert.False(SkUiGridSplitterMath.ResizesColumns(SkUiGridResizeDirection.Auto, fill, LayoutAlignment.End, rows: 1, columns: 3, rowSpan: 1, columnSpan: 1, default));
    }

    [Fact]
    public void ResizeBehaviorPicksTheTracks()
    {
        Assert.Equal((1, 3), SkUiGridSplitterMath.Indices(SkUiGridResizeBehavior.BasedOnAlignment, LayoutAlignment.Fill, 2, 1));
        Assert.Equal((2, 3), SkUiGridSplitterMath.Indices(SkUiGridResizeBehavior.BasedOnAlignment, LayoutAlignment.End, 2, 1));
        Assert.Equal((1, 2), SkUiGridSplitterMath.Indices(SkUiGridResizeBehavior.BasedOnAlignment, LayoutAlignment.Start, 2, 1));
        Assert.Equal((3, 4), SkUiGridSplitterMath.Indices(SkUiGridResizeBehavior.CurrentAndNext, LayoutAlignment.Fill, 2, 2));
        Assert.Equal((2, -1), SkUiGridSplitterMath.Indices(SkUiGridResizeBehavior.CurrentOnly, LayoutAlignment.Fill, 2, 1));
        Assert.False(SkUiGridSplitterMath.AreTracks((2, 3), 3)); // the last column has no next one
        Assert.True(SkUiGridSplitterMath.AreTracks((2, -1), 3));
    }

    [Fact]
    public void ATrackResizedAloneBecomesAbsoluteWithinItsLimits()
    {
        var (first, second) = SkUiGridSplitterMath.Resize(Track(SkUiGridTrackKind.Star, 1, 100, maximum: 160), null, 250);
        Assert.Equal(new SkUiGridLengthChange(160, false), first);
        Assert.Null(second);
    }

    [Fact]
    public void DraggingBetweenStarRowsMovesTheBoundaryWithThePointer()
    {
        var (root, grid, splitter, below) = Rows(GridLength.Star, GridLength.Star);
        Assert.Equal(16, splitter.Height, Tolerance);
        Assert.Equal(192, splitter.Y, Tolerance);
        var events = new List<string>();
        splitter.DragStarted += (_, _) => events.Add("started");
        splitter.DragCompleted += (_, _) => events.Add("completed");

        Drag(root, new Point(100, 200), new Point(100, 260));
        Assert.Equal(["started", "completed"], events);
        Assert.False(splitter.IsDragging);
        Assert.True(grid.RowDefinitions[0].Height.IsStar);
        Assert.True(grid.RowDefinitions[2].Height.IsStar);
        Assert.Equal(252.0 / 384 * 2, grid.RowDefinitions[0].Height.Value, Tolerance);

        SkUiTestHelpers.Arrange(root, 200, 400);
        Assert.Equal(252, splitter.Y, Tolerance);
        Assert.Equal(268, below.Y, Tolerance);
        // Star rows keep sharing a resized grid in the new proportion.
        SkUiTestHelpers.Arrange(root, 200, 800);
        Assert.Equal(784.0 * 252 / 384, splitter.Y, Tolerance);
    }

    [Fact]
    public void TheSplitterFollowsThePointerWhileTheGridLaysOutDuringTheDrag()
    {
        var (root, grid, splitter, _) = Rows(new GridLength(100), GridLength.Star);
        var id = ++_pointer;
        var start = TimeSpan.FromSeconds(30);
        root.Touch(new(id, SkUiTouchAction.Pressed, new Point(100, 108), start));
        for (var step = 1; step <= 4; step++)
        {
            root.Touch(new(id, SkUiTouchAction.Moved, new Point(100, 108 + 20 * step), start + TimeSpan.FromMilliseconds(40 * step)));
            // The splitter moves under the pointer between events, as on a device.
            SkUiTestHelpers.Arrange(root, 200, 400);
            Assert.Equal(100 + 20 * step, grid.RowDefinitions[0].Height.Value, Tolerance);
            Assert.Equal(100 + 20 * step, splitter.Y, Tolerance);
        }
        root.Touch(new(id, SkUiTouchAction.Released, new Point(100, 188), start + TimeSpan.FromMilliseconds(200)));
        Assert.True(grid.RowDefinitions[2].Height.IsStar);
    }

    [Fact]
    public void MinimumAndMaximumLengthLimitTheDrag()
    {
        var (root, grid, _, _) = Rows(new GridLength(100), GridLength.Star);
        SkUiGridSplitter.SetMinimumLength(grid.RowDefinitions[0], 80);
        SkUiGridSplitter.SetMinimumLength(grid.RowDefinitions[2], 200);

        Drag(root, new Point(100, 108), new Point(100, 8));
        Assert.Equal(80, grid.RowDefinitions[0].Height.Value, Tolerance);

        SkUiTestHelpers.Arrange(root, 200, 400);
        Drag(root, new Point(100, 88), new Point(100, 388));
        // 400 - 16 - 200 left for the first row.
        Assert.Equal(184, grid.RowDefinitions[0].Height.Value, Tolerance);
    }

    [Fact]
    public void ArrowKeysAndAssistiveIncrementsMoveTheSplitter()
    {
        var (root, grid, splitter, _) = Rows(new GridLength(100), GridLength.Star);
        var keys = (ISkUiKeyHandler)splitter;
        Assert.True(keys.OnKeyDown(new SkUiKeyEvent(SkUiKey.Down, SkUiKeyModifiers.None, false)));
        Assert.Equal(110, grid.RowDefinitions[0].Height.Value, Tolerance);
        Assert.False(keys.OnKeyDown(new SkUiKeyEvent(SkUiKey.Left, SkUiKeyModifiers.None, false))); // rows: left / right are not ours

        splitter.KeyboardIncrement = 25;
        SkUiTestHelpers.Arrange(root, 200, 400);
        Assert.True(keys.OnKeyDown(new SkUiKeyEvent(SkUiKey.Up, SkUiKeyModifiers.None, false)));
        Assert.Equal(85, grid.RowDefinitions[0].Height.Value, Tolerance);

        SkUiTestHelpers.Arrange(root, 200, 400);
        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)splitter).GetSemantics(info);
        Assert.Equal(SkUiSemanticsRole.Slider, info.Role);
        Assert.False(info.IsHorizontal);
        Assert.Equal(new SkUiSemanticsRange(0, 384, 85), info.Range);
        Assert.True(((ISkUiAccessibleNode)splitter).PerformSemanticsAction(SkUiSemanticsActions.Increment));
        Assert.Equal(110, grid.RowDefinitions[0].Height.Value, Tolerance);
    }

    [Fact]
    public void ColumnSplittersResizeColumnsAndMirrorRightToLeft()
    {
        var splitter = new SkUiGridSplitter();
        var grid = new SkUiGrid { ColumnDefinitions = [new(new GridLength(100)), new(GridLength.Auto), new(GridLength.Star)], Children = { splitter } };
        Grid.SetColumn(splitter, 1);
        var root = new SkUiContentView { Content = grid };
        SkUiTestHelpers.Arrange(root, 400, 200);
        Assert.Equal(16, splitter.Width, Tolerance);

        Drag(root, new Point(108, 100), new Point(158, 100));
        Assert.Equal(150, grid.ColumnDefinitions[0].Width.Value, Tolerance);
        Assert.True(((ISkUiKeyHandler)splitter).OnKeyDown(new SkUiKeyEvent(SkUiKey.Right, SkUiKeyModifiers.None, false)));
        Assert.Equal(160, grid.ColumnDefinitions[0].Width.Value, Tolerance);

        root.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(root, 400, 200);
        // Right to left the first column is on the right: Left widens it.
        Assert.True(((ISkUiKeyHandler)splitter).OnKeyDown(new SkUiKeyEvent(SkUiKey.Left, SkUiKeyModifiers.None, false)));
        Assert.Equal(170, grid.ColumnDefinitions[0].Width.Value, Tolerance);
    }

    [Fact]
    public void ASplitterOnASurfaceOfItsOwnResizesAMauiGrid()
    {
        var splitter = new SkUiGridSplitter();
        var grid = new Grid { RowDefinitions = [new(new GridLength(120)), new(GridLength.Auto), new(GridLength.Star)], Children = { splitter } };
        Grid.SetRow(splitter, 1);
        // A MAUI layout without a handler leaves its children to the platform: lay them out directly.
        ((Microsoft.Maui.ILayout)grid).CrossPlatformMeasure(300, 500);
        ((IView)grid).Arrange(new Rect(0, 0, 300, 500));
        ((Microsoft.Maui.ILayout)grid).CrossPlatformArrange(new Rect(0, 0, 300, 500));
        Assert.Equal(120, splitter.Frame.Y, Tolerance);

        // The splitter is the surface's root: the pointer is in its own coordinates.
        Drag(splitter, new Point(150, 8), new Point(150, -32));
        Assert.Equal(80, grid.RowDefinitions[0].Height.Value, Tolerance);
        Assert.True(grid.RowDefinitions[2].Height.IsStar);
    }

    [Fact]
    public void WithoutATrackOnBothSidesTheSplitterDoesNothing()
    {
        var splitter = new SkUiGridSplitter();
        var grid = new SkUiGrid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)], Children = { splitter } };
        var root = new SkUiContentView { Content = grid };
        SkUiTestHelpers.Arrange(root, 200, 400);
        Drag(root, new Point(100, 8), new Point(100, 108));
        Assert.True(grid.RowDefinitions[0].Height.IsAuto);
        Assert.False(((ISkUiKeyHandler)splitter).OnKeyDown(new SkUiKeyEvent(SkUiKey.Down, SkUiKeyModifiers.None, false)));
    }

    [Fact]
    public void CoreSplitterMatches()
    {
        var splitter = new SkUiCoreGridSplitter();
        var grid = new SkUiCoreGrid()
            .SetRowDefinitions([new(SkUiCoreGridLength.Star) { MinHeight = 50 }, new(SkUiCoreGridLength.Auto), new(SkUiCoreGridLength.Star)])
            .Add(splitter, 1, 0);
        var host = new SkUiCoreHost().SetContent(grid);
        SkUiTestHelpers.Arrange(host, 200, 400);
        Assert.Equal(192, grid.GetRowHeight(0), Tolerance);
        var events = new List<string>();
        splitter.DragStarted += (_, _) => events.Add("started");
        splitter.DragCompleted += (_, _) => events.Add("completed");

        Drag(host, new Point(100, 200), new Point(100, 260));
        Assert.Equal(["started", "completed"], events);
        SkUiTestHelpers.Arrange(host, 200, 400);
        Assert.Equal(252, grid.GetRowHeight(0), Tolerance);
        Assert.True(grid.RowDefinitions[0].Height.IsStar);

        // The definition's MinHeight bounds the drag.
        Drag(host, new Point(100, 260), new Point(100, 0));
        SkUiTestHelpers.Arrange(host, 200, 400);
        Assert.Equal(50, grid.GetRowHeight(0), Tolerance);

        Assert.True(((ISkUiKeyHandler)splitter).OnKeyDown(new SkUiKeyEvent(SkUiKey.Down, SkUiKeyModifiers.None, false)));
        SkUiTestHelpers.Arrange(host, 200, 400);
        Assert.Equal(60, grid.GetRowHeight(0), Tolerance);
    }

    [Fact]
    public void BothLayersDrawTheSameGrip()
    {
        var skui = new SkUiGridSplitter { GripColor = Colors.Red, WidthRequest = 200, HeightRequest = 16 };
        SkUiTestHelpers.Arrange(skui, 200, 16);
        using var skuiBitmap = Render(skui.Paint);
        var core = new SkUiCoreGridSplitter().SetResizeDirection(SkUiGridResizeDirection.Rows).SetGripColor(Colors.Red);
        core.Measure(200, 16);
        core.Arrange(new Rect(0, 0, 200, 16));
        using var coreBitmap = Render(core.Paint);

        Assert.Equal(SKColors.Red, skuiBitmap.GetPixel(100, 8));
        Assert.Equal(0, skuiBitmap.GetPixel(10, 8).Alpha);
        for (var x = 0; x < 200; x += 5)
            for (var y = 0; y < 16; y++)
                Assert.Equal(skuiBitmap.GetPixel(x, y), coreBitmap.GetPixel(x, y));

        static SKBitmap Render(Action<SKCanvas> paint)
        {
            var bitmap = new SKBitmap(200, 16);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            paint(canvas);
            return bitmap;
        }
    }

    [Fact]
    public void ADraggerReportsItsTravelInItsParentWhileAHandlerMovesIt()
    {
        var box = new SkUiBox { HeightRequest = 100 };
        var dragger = new SkUiDragger { DragAxis = SkUiPanAxis.Vertical };
        var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { box, dragger } } };
        SkUiTestHelpers.Arrange(root, 200, 400);
        Assert.Equal(16, dragger.Height, Tolerance);
        var events = new List<string>();
        double start = 0;
        dragger.DragStarted += (_, args) => { events.Add($"started {args.TotalY}"); start = box.HeightRequest; };
        dragger.DragDelta += (_, args) =>
        {
            Assert.Equal(0, args.TotalX); // a vertical dragger reports no horizontal travel
            box.HeightRequest = start + args.TotalY;
        };
        dragger.DragCompleted += (_, args) => events.Add($"completed {args.TotalY} {args.IsCanceled}");

        var id = ++_pointer;
        var time = TimeSpan.FromSeconds(50);
        root.Touch(new(id, SkUiTouchAction.Pressed, new Point(100, 108), time));
        for (var step = 1; step <= 4; step++)
        {
            root.Touch(new(id, SkUiTouchAction.Moved, new Point(100 + step, 108 + 15 * step), time + TimeSpan.FromMilliseconds(40 * step)));
            // The dragger moves down with the box between events, as on a device.
            SkUiTestHelpers.Arrange(root, 200, 400);
            Assert.Equal(100 + 15 * step, box.Height, Tolerance);
            Assert.Equal(100 + 15 * step, dragger.Y, Tolerance);
        }
        root.Touch(new(id, SkUiTouchAction.Released, new Point(104, 168), time + TimeSpan.FromMilliseconds(200)));
        Assert.Equal(["started 0", "completed 60 False"], events);
        Assert.False(dragger.IsDragging);
    }

    [Fact]
    public void ArrowKeysAreDragsOfOneIncrementAlongTheAxis()
    {
        var dragger = new SkUiDragger { DragAxis = SkUiPanAxis.Horizontal, KeyboardIncrement = 5 };
        SkUiTestHelpers.Arrange(dragger, 16, 100);
        var events = new List<string>();
        dragger.DragStarted += (_, args) => events.Add($"started {args.IsKeyboard}");
        dragger.DragDelta += (_, args) => events.Add($"delta {args.TotalX} {args.DeltaX} {args.TotalY}");
        dragger.DragCompleted += (_, args) => events.Add($"completed {args.TotalX}");
        var keys = (ISkUiKeyHandler)dragger;

        Assert.True(keys.OnKeyDown(new SkUiKeyEvent(SkUiKey.Left, SkUiKeyModifiers.None, false)));
        Assert.Equal(["started True", "delta -5 -5 0", "completed -5"], events);
        Assert.False(keys.OnKeyDown(new SkUiKeyEvent(SkUiKey.Down, SkUiKeyModifiers.None, false))); // across the axis
        dragger.DragAxis = SkUiPanAxis.Both;
        Assert.True(keys.OnKeyDown(new SkUiKeyEvent(SkUiKey.Down, SkUiKeyModifiers.None, false)));
        Assert.Equal("delta 0 0 5", events[^2]);
    }

    [Fact]
    public void ATwoAxisDraggerReportsBothDirections()
    {
        var dragger = new SkUiDragger { WidthRequest = 40, HeightRequest = 40 };
        var root = new SkUiContentView { Content = dragger };
        SkUiTestHelpers.Arrange(root, 40, 40);
        SkUiDragEventArgs? last = null;
        dragger.DragDelta += (_, args) => last = args;
        Drag(root, new Point(20, 20), new Point(50, 60));
        Assert.Equal(30, last!.TotalX, Tolerance);
        Assert.Equal(40, last.TotalY, Tolerance);
    }

    /// <summary>A Core table of 3 rows (a 36 DIP header) and columns of 100, 100 and *, a splitter at the end of the first header cell.</summary>
    private static (SkUiCoreHost Host, SkUiCoreTable Table, SkUiCoreGridSplitter Splitter) Table(SkUiGridResizeBehavior behavior)
    {
        var splitter = new SkUiCoreGridSplitter().SetResizeBehavior(behavior);
        splitter.SetHorizontalAlignment(LayoutAlignment.End);
        var table = new SkUiCoreTable()
            .SetRowDefinitions([new(new SkUiCoreGridLength(36)), new(SkUiCoreGridLength.Star), new(SkUiCoreGridLength.Star)])
            .SetColumnDefinitions([new(new SkUiCoreGridLength(100)) { MinWidth = 40 }, new(new SkUiCoreGridLength(100)), new(SkUiCoreGridLength.Star)])
            .SetColumnSeparatorColor(Colors.Gray);
        table.Add(new SkUiCoreLabel().SetText("Name"), 0, 0);
        table.Add(splitter, 0, 0);
        var host = new SkUiCoreHost().SetContent(table);
        SkUiTestHelpers.Arrange(host, 400, 300);
        return (host, table, splitter);
    }

    [Fact]
    public void ASplitterAtTheEndOfATableHeaderCellResizesThatColumnAndTheNext()
    {
        var (host, table, splitter) = Table(SkUiGridResizeBehavior.BasedOnAlignment);
        Assert.Equal(new Rect(84, 0, 16, 36), splitter.Frame);

        Drag(host, new Point(92, 18), new Point(122, 18));
        Assert.Equal(130, table.ColumnDefinitions[0].Width.Value, Tolerance);
        Assert.Equal(70, table.ColumnDefinitions[1].Width.Value, Tolerance);
        Assert.True(table.ColumnDefinitions[2].Width.IsStar);

        // The column's MinWidth bounds the drag.
        SkUiTestHelpers.Arrange(host, 400, 300);
        Drag(host, new Point(122, 18), new Point(0, 18));
        Assert.Equal(40, table.ColumnDefinitions[0].Width.Value, Tolerance);
        Assert.Equal(160, table.ColumnDefinitions[1].Width.Value, Tolerance);
    }

    [Fact]
    public void ASplitterDownTheWholeTableKeepsItsRowSeparatorsAndGripsInTheHeader()
    {
        var (host, table, splitter) = Table(SkUiGridResizeBehavior.BasedOnAlignment);
        table.SetRowSeparatorColor(Colors.Gray);
        table.SetRowSpan(splitter, 3);
        splitter.SetShowsLine(true).SetGripAlignment(LayoutAlignment.Start);
        SkUiTestHelpers.Arrange(host, 400, 300);
        Assert.Equal(new Rect(84, 0, 16, 300), splitter.Frame);
        // The splitter merges no cells: each row separator still crosses the whole table.
        Assert.Single(table.GetRowSeparatorSegments(0));
        Assert.Equal(400, table.GetRowSeparatorSegments(0)[0].Width, Tolerance);

        Drag(host, new Point(92, 150), new Point(112, 150));
        Assert.Equal(120, table.ColumnDefinitions[0].Width.Value, Tolerance);
    }

    [Fact]
    public void CurrentOnlyResizesOneColumnAndTheTableFollows()
    {
        var (host, table, _) = Table(SkUiGridResizeBehavior.CurrentOnly);
        Drag(host, new Point(92, 18), new Point(142, 18));
        Assert.Equal(150, table.ColumnDefinitions[0].Width.Value, Tolerance);
        Assert.Equal(100, table.ColumnDefinitions[1].Width.Value, Tolerance);
    }

    [Fact]
    public void TheLookDrawsTheLineAndTheGripWhereItIsAligned()
    {
        var skui = new SkUiDragger
        {
            DragAxis = SkUiPanAxis.Horizontal, ShowsLine = true, LineColor = Colors.Blue, GripColor = Colors.Red, GripAlignment = LayoutAlignment.Start,
            WidthRequest = 17, HeightRequest = 200
        };
        SkUiTestHelpers.Arrange(skui, 17, 200);
        using var skuiBitmap = Render(skui.Paint, 17, 200);
        var core = new SkUiCoreDragger().SetDragAxis(SkUiPanAxis.Horizontal).SetShowsLine(true).SetLineColor(Colors.Blue).SetGripColor(Colors.Red)
            .SetGripAlignment(LayoutAlignment.Start);
        core.Measure(17, 200);
        core.Arrange(new Rect(0, 0, 17, 200));
        using var coreBitmap = Render(core.Paint, 17, 200);

        Assert.Equal(SKColors.Red, skuiBitmap.GetPixel(8, 20)); // the grip, 4..40 from the top
        Assert.Equal(SKColors.Blue, skuiBitmap.GetPixel(8, 150)); // the line, along the whole dragger
        Assert.Equal(0, skuiBitmap.GetPixel(2, 150).Alpha);
        for (var x = 0; x < 17; x++)
            for (var y = 0; y < 200; y += 4)
                Assert.Equal(skuiBitmap.GetPixel(x, y), coreBitmap.GetPixel(x, y));
    }

    /// <summary>
    /// A 200×400 surface: star rows with a tappable box each, around a 1 DIP Auto row holding <paramref name="splitter"/>,
    /// added after the boxes so it is on top.
    /// </summary>
    private static (SkUiContentView Root, SkUiGrid Grid, List<string> Taps) ThinRows(SkUiGridSplitter splitter)
    {
        var taps = new List<string>();
        var above = new SkUiBox();
        var below = new SkUiBox();
        var aboveTap = new SkUiTapGestureRecognizer();
        aboveTap.Tapped += (_, _) => taps.Add("above");
        above.Gestures.Add(aboveTap);
        var belowTap = new SkUiTapGestureRecognizer();
        belowTap.Tapped += (_, _) => taps.Add("below");
        below.Gestures.Add(belowTap);
        splitter.HeightRequest = 1;
        var grid = new SkUiGrid { RowDefinitions = [new(GridLength.Star), new(GridLength.Auto), new(GridLength.Star)], Children = { above, below, splitter } };
        Grid.SetRow(below, 2);
        Grid.SetRow(splitter, 1);
        var root = new SkUiContentView { Content = grid };
        SkUiTestHelpers.Arrange(root, 200, 400);
        return (root, grid, taps);
    }

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(30)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(30.05)));
    }

    [Fact]
    public void AThinSplitterTakesPointersAcrossItsTouchThicknessOverItsNeighbors()
    {
        var (root, grid, taps) = ThinRows(new SkUiGridSplitter());
        var splitter = (SkUiGridSplitter)grid.Children[2];
        Assert.Equal(1, splitter.Height, Tolerance);
        Assert.Equal(199.5, splitter.Y, Tolerance);

        // 10 DIPs above the line is inside the look's 24 DIP touch area: the drag is the splitter's.
        Drag(root, new Point(100, 190), new Point(100, 250));
        SkUiTestHelpers.Arrange(root, 200, 400);
        Assert.Equal(259.5, splitter.Y, Tolerance);

        // Beyond it, and on the neighbors' taps, the boxes keep their pointers.
        Tap(root, new Point(100, 259.5 - 14));
        Tap(root, new Point(100, 260.5 + 14));
        Assert.Equal(["above", "below"], taps);
        Tap(root, new Point(100, 259.5 - 8));
        Assert.Equal(2, taps.Count);
    }

    [Fact]
    public void TouchThicknessOverridesTheLookAndADisabledSplitterLeavesItsNeighborsAlone()
    {
        var (root, _, taps) = ThinRows(new SkUiGridSplitter { TouchThickness = 40 });
        Tap(root, new Point(100, 199.5 - 18));
        Assert.Empty(taps);

        var (disabledRoot, disabledGrid, disabledTaps) = ThinRows(new SkUiGridSplitter { IsEnabled = false });
        Tap(disabledRoot, new Point(100, 199.5 - 6));
        Assert.Equal(["above"], disabledTaps);
        Assert.Equal(1, disabledGrid.Children[2].Frame.Height, Tolerance);
    }

    [Fact]
    public void AThinDraggerDrawsUnclippedOverItsTouchArea()
    {
        var thin = new SkUiGridSplitter { HeightRequest = 1, WidthRequest = 200, ResizeDirection = SkUiGridResizeDirection.Rows };
        SkUiTestHelpers.Arrange(thin, 200, 1);
        var props = SkUiRenderProps.Default;
        ((ISkUiRenderable)thin).GetRenderProps(ref props);
        Assert.False(props.ClipToBounds);
        Assert.Equal(new SKRect(0, 11.5f, 0, 11.5f), props.Overflow);

        // A dragger as thick as its touch area (and the grip) is its own touch area, clipped as usual.
        var thick = new SkUiCoreDragger().SetDragAxis(SkUiPanAxis.Vertical).SetTouchThickness(0);
        thick.Measure(200, 16);
        thick.Arrange(new Rect(0, 0, 200, 16));
        var thickProps = SkUiRenderProps.Default;
        ((ISkUiRenderable)thick).GetRenderProps(ref thickProps);
        Assert.True(thickProps.ClipToBounds);
        Assert.Equal(SKRect.Empty, thickProps.Overflow);

        // The grip, 4 DIPs thick, is centered on the 1 DIP line on both layers.
        var core = new SkUiCoreGridSplitter().SetResizeDirection(SkUiGridResizeDirection.Rows).SetGripColor(Colors.Red);
        core.Measure(200, 1);
        core.Arrange(new Rect(0, 0, 200, 1));
        thin.GripColor = Colors.Red;
        using var skuiBitmap = Render(canvas => { canvas.Translate(0, 12); thin.Paint(canvas); }, 200, 25);
        using var coreBitmap = Render(canvas => { canvas.Translate(0, 12); core.Paint(canvas); }, 200, 25);
        Assert.Equal(SKColors.Red, skuiBitmap.GetPixel(100, 11));
        Assert.Equal(SKColors.Red, skuiBitmap.GetPixel(100, 13));
        Assert.Equal(0, skuiBitmap.GetPixel(100, 16).Alpha);
        for (var x = 0; x < 200; x += 5)
            for (var y = 0; y < 25; y++)
                Assert.Equal(skuiBitmap.GetPixel(x, y), coreBitmap.GetPixel(x, y));
    }

    [Fact]
    public void AOneDipTableHeaderSplitterTakesThePressInsideTheCellNextToIt()
    {
        var (host, table, splitter) = Table(SkUiGridResizeBehavior.BasedOnAlignment);
        splitter.SetWidth(1);
        SkUiTestHelpers.Arrange(host, 400, 300);
        Assert.Equal(new Rect(99, 0, 1, 36), splitter.Frame);

        // 8 DIPs left of the line, on the header label.
        Drag(host, new Point(91, 18), new Point(121, 18));
        Assert.Equal(130, table.ColumnDefinitions[0].Width.Value, Tolerance);
        Assert.Equal(70, table.ColumnDefinitions[1].Width.Value, Tolerance);
    }

    private static SKBitmap Render(Action<SKCanvas> paint, int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        paint(canvas);
        return bitmap;
    }
}
