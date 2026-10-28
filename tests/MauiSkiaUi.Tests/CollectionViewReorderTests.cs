using System.Collections.ObjectModel;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <see cref="SkUiCollectionView.CanReorderItems"/>: lifting with a long press (touch) or a press and drag (mouse), the rows
/// and cells moving aside, the drop (RemoveAt and Insert, as MAUI's handlers), groups with and without
/// <see cref="SkUiCollectionView.CanMixGroups"/>, grids, horizontal lists, auto-scroll, cancelling, and the screen-reader moves.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class CollectionViewReorderTests
{
    private static long _pointer = 900_000;

    private sealed record Row(int Index, double Height = 50);

    private sealed class RowView : SkUiBox
    {
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is Row row)
                HeightRequest = row.Height;
        }
    }

    private sealed class Section(string name, IEnumerable<Row> rows) : ObservableCollection<Row>(rows)
    {
        public string Name { get; } = name;
    }

    private static ObservableCollection<Row> Rows(int count, int from = 0) => new(Enumerable.Range(from, count).Select(index => new Row(index)));

    private static SkUiCollectionView List(System.Collections.IEnumerable rows, int span = 1)
    {
        var list = new SkUiCollectionView
        {
            ItemsSource = rows,
            ItemTemplate = new DataTemplate(() => new RowView()),
            CanReorderItems = true,
            Span = span,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Overscroll = SkUiOverscrollMode.None,
            PrefetchBudget = TimeSpan.FromSeconds(10)
        };
        return list;
    }

    private static int[] Order(IEnumerable<Row> rows) => rows.Select(row => row.Index).ToArray();

    /// <summary>The container of the item at <paramref name="index"/> (its realized view's parent).</summary>
    private static View Host(SkUiCollectionView list, int index) => (View)((Element)list.GetRealizedView(index)!).Parent;

    private static long Press(SkUiView root, Point at, SkUiPointerDevice device)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.Zero, Device: device));
        return id;
    }

    private static void MoveTo(SkUiView root, long id, Point from, Point to, SkUiPointerDevice device, int steps = 6)
    {
        for (var step = 1; step <= steps; step++)
        {
            var point = new Point(from.X + (to.X - from.X) * step / steps, from.Y + (to.Y - from.Y) * step / steps);
            root.Touch(new(id, SkUiTouchAction.Moved, point, TimeSpan.FromMilliseconds(step * 100), Device: device));
        }
    }

    private static void Release(SkUiView root, long id, Point at, SkUiPointerDevice device) =>
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(2), Device: device));

    /// <summary>A mouse press at <paramref name="from"/> dragged to <paramref name="to"/>; released unless <paramref name="release"/> is false.</summary>
    private static long MouseDrag(SkUiView root, Point from, Point to, bool release = true)
    {
        var id = Press(root, from, SkUiPointerDevice.Mouse);
        MoveTo(root, id, from, to, SkUiPointerDevice.Mouse);
        if (release)
            Release(root, id, to, SkUiPointerDevice.Mouse);
        return id;
    }

    [Fact]
    public void AMouseDragMovesAnItemAndTheRowsItPassesMoveAside()
    {
        var rows = Rows(20);
        var list = List(rows);
        list.SelectionMode = SkUiSelectionMode.Single;
        list.SelectedItem = rows[0];
        var completed = 0;
        list.ReorderCompleted += (_, _) => completed++;
        SkUiTestHelpers.Arrange(list, 300, 500);

        // Item 0 dragged down until its center passes the middle of item 2.
        var id = MouseDrag(list, new Point(150, 25), new Point(150, 145), release: false);
        Assert.Equal((-1, 0, -1, 2), list.ReorderState);
        Assert.Equal(0, Host(list, 0).Opacity);
        Assert.Equal(-50, Host(list, 1).TranslationY);
        Assert.Equal(-50, Host(list, 2).TranslationY);
        Assert.Equal(0, Host(list, 3).TranslationY);
        // The proxy follows the pointer along the list's axis only.
        var proxy = list.ReorderProxy!;
        Assert.Equal(0, proxy.TranslationX);
        Assert.Equal(120, proxy.TranslationY);
        Assert.Same(rows[0], proxy.BindingContext);

        Release(list, id, new Point(150, 145), SkUiPointerDevice.Mouse);
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal([1, 2, 0, 3, 4], Order(rows).Take(5));
        Assert.Equal(1, completed);
        Assert.Null(list.ReorderState);
        Assert.Null(list.ReorderProxy);
        // The item stayed selected although it left the list for a moment; nothing is translated or transparent.
        Assert.Same(rows[2], list.SelectedItem);
        for (var index = 0; index < 5; index++)
        {
            Assert.Equal(0, Host(list, index).TranslationY);
            Assert.Equal(1, Host(list, index).Opacity);
            Assert.Same(rows[index], ((BindableObject)list.GetRealizedView(index)!).BindingContext);
        }
    }

    [Fact]
    public void ATouchLiftsAnItemAfterALongPressAndADragWithoutTheHoldScrolls()
    {
        using var clock = new ManualGestureClock();
        var rows = Rows(50);
        var list = List(rows);
        SkUiTestHelpers.Arrange(list, 300, 500);

        // Moved before the hold: the list scrolls, nothing is lifted.
        var scroll = Press(list, new Point(150, 225), SkUiPointerDevice.Touch);
        MoveTo(list, scroll, new Point(150, 225), new Point(150, 125), SkUiPointerDevice.Touch);
        Assert.Null(list.ReorderState);
        Assert.True(list.ScrollY > 0);
        Release(list, scroll, new Point(150, 125), SkUiPointerDevice.Touch);
        list.ScrollView.ScrollTo(0, 0);
        SkUiTestHelpers.Arrange(list, 300, 500);

        var id = Press(list, new Point(150, 125), SkUiPointerDevice.Touch); // item 2
        clock.Advance(SkUiGestureSettings.LongPressDuration);
        Assert.Equal((-1, 2, -1, 2), list.ReorderState);
        MoveTo(list, id, new Point(150, 125), new Point(150, 25), SkUiPointerDevice.Touch);
        Assert.Equal((-1, 2, -1, 0), list.ReorderState);
        Assert.Equal(50, Host(list, 0).TranslationY);
        Assert.Equal(50, Host(list, 1).TranslationY);
        Release(list, id, new Point(150, 25), SkUiPointerDevice.Touch);

        Assert.Equal([2, 0, 1, 3], Order(rows).Take(4));
    }

    [Fact]
    public void ATapStillSelectsAndAnItemPutBackMovesNothing()
    {
        var rows = Rows(10);
        var list = List(rows);
        list.SelectionMode = SkUiSelectionMode.Single;
        var completed = 0;
        list.ReorderCompleted += (_, _) => completed++;
        SkUiTestHelpers.Arrange(list, 300, 500);

        var tap = Press(list, new Point(150, 75), SkUiPointerDevice.Touch);
        Release(list, tap, new Point(150, 75), SkUiPointerDevice.Touch);
        Assert.Same(rows[1], list.SelectedItem);

        MouseDrag(list, new Point(150, 75), new Point(150, 90));
        Assert.Equal(0, completed);
        Assert.Equal([0, 1, 2], Order(rows).Take(3));
    }

    [Fact]
    public void ListsThatDoNotNotifyAreMovedAndShownInTheirNewOrder()
    {
        var rows = Rows(10).ToList();
        var list = List(rows);
        SkUiTestHelpers.Arrange(list, 300, 500);

        MouseDrag(list, new Point(150, 175), new Point(150, 25)); // item 3 to the top
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal([3, 0, 1, 2, 4], Order(rows).Take(5));
        for (var index = 0; index < 5; index++)
            Assert.Same(rows[index], ((BindableObject)list.GetRealizedView(index)!).BindingContext);
    }

    [Fact]
    public void ItemsOfListsThatCannotChangeAreNotLifted()
    {
        var rows = Rows(30).ToArray();
        var list = List(rows);
        SkUiTestHelpers.Arrange(list, 300, 500);

        MouseDrag(list, new Point(150, 225), new Point(150, 125), release: false);
        Assert.Null(list.ReorderState);
        Assert.True(list.ScrollY > 0); // the drag scrolls instead

        var other = List(Rows(30));
        other.CanReorderItems = false;
        SkUiTestHelpers.Arrange(other, 300, 500);
        MouseDrag(other, new Point(150, 225), new Point(150, 125), release: false);
        Assert.Null(other.ReorderState);
        Assert.True(other.ScrollY > 0);
    }

    [Fact]
    public void GroupedItemsStayInTheirGroupUnlessGroupsMayMix()
    {
        // Rows: header A (0–30), A0–A2 (30–180), header B (180–210), B0–B2 (210–360).
        ObservableCollection<Section> Sections() => [new("A", Rows(3)), new("B", Rows(3, 10))];
        SkUiCollectionView Grouped(ObservableCollection<Section> sections) => new()
        {
            IsGrouped = true,
            ItemsSource = sections,
            ItemTemplate = new DataTemplate(() => new RowView()),
            GroupHeaderTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = 30 }),
            CanReorderItems = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Overscroll = SkUiOverscrollMode.None
        };

        var sections = Sections();
        var list = Grouped(sections);
        SkUiTestHelpers.Arrange(list, 300, 500);
        // A2 dragged onto B0: B is no place for it.
        MouseDrag(list, new Point(150, 155), new Point(150, 235), release: false);
        Assert.Equal((0, 2, 0, 2), list.ReorderState);
        Release(list, _pointer, new Point(150, 235), SkUiPointerDevice.Mouse);
        Assert.Equal([0, 1, 2], Order(sections[0]));

        sections = Sections();
        list = Grouped(sections);
        list.CanMixGroups = true;
        SkUiTestHelpers.Arrange(list, 300, 500);
        var id = MouseDrag(list, new Point(150, 155), new Point(150, 235), release: false);
        Assert.Equal((0, 2, 1, 1), list.ReorderState);
        // The header and B0 move up by the lifted row.
        Assert.Equal(-50, ((View)list.RowsLayout.GetRealizedView(4)!).TranslationY);
        Assert.Equal(-50, ((View)list.RowsLayout.GetRealizedView(5)!).TranslationY);
        Release(list, id, new Point(150, 235), SkUiPointerDevice.Mouse);
        Assert.Equal([0, 1], Order(sections[0]));
        Assert.Equal([10, 2, 11, 12], Order(sections[1]));

        // Rows now: header A (0–30), A0–A1 (30–130), header B (130–160), B0–B3 (160–360). Item 2 (B1) dragged up past the
        // middle of B's header: the end of A.
        SkUiTestHelpers.Arrange(list, 300, 500);
        MouseDrag(list, new Point(150, 235), new Point(150, 140));
        Assert.Equal([0, 1, 2], Order(sections[0]));
        Assert.Equal([10, 11, 12], Order(sections[1]));
    }

    [Fact]
    public void GridCellsMoveToTheirNeighboursCell()
    {
        var rows = Rows(9);
        var list = List(rows, span: 3);
        SkUiTestHelpers.Arrange(list, 300, 500);

        var id = MouseDrag(list, new Point(50, 25), new Point(250, 25), release: false);
        Assert.Equal((-1, 0, -1, 2), list.ReorderState);
        Assert.Equal(-100, Host(list, 1).TranslationX);
        Assert.Equal(-100, Host(list, 2).TranslationX);
        Assert.Equal(0, Host(list, 3).TranslationX);
        // A grid's proxy moves freely.
        Assert.Equal(200, list.ReorderProxy!.TranslationX);
        Release(list, id, new Point(250, 25), SkUiPointerDevice.Mouse);
        Assert.Equal([1, 2, 0, 3], Order(rows).Take(4));
        SkUiTestHelpers.Arrange(list, 300, 500);

        // Into the next row: the cells from there on move back one cell, the first of the row to the end of the one before.
        id = MouseDrag(list, new Point(50, 25), new Point(150, 75), release: false);
        Assert.Equal((-1, 0, -1, 4), list.ReorderState);
        Assert.Equal(new Point(200, -50), new Point(Host(list, 3).TranslationX, Host(list, 3).TranslationY));
        Assert.Equal(-100, Host(list, 4).TranslationX);
        Release(list, id, new Point(150, 75), SkUiPointerDevice.Mouse);
        Assert.Equal([2, 0, 3, 4, 1, 5], Order(rows).Take(6));
    }

    [Fact]
    public void AHorizontalListMovesItemsAlongItsAxis()
    {
        var rows = Rows(10);
        var list = List(rows);
        list.Orientation = ItemsLayoutOrientation.Horizontal;
        list.ItemExtent = 100;
        SkUiTestHelpers.Arrange(list, 500, 200);

        var id = MouseDrag(list, new Point(50, 100), new Point(250, 120), release: false);
        Assert.Equal((-1, 0, -1, 2), list.ReorderState);
        Assert.Equal(-100, Host(list, 1).TranslationX);
        Assert.Equal(0, list.ReorderProxy!.TranslationY);
        Release(list, id, new Point(250, 120), SkUiPointerDevice.Mouse);
        Assert.Equal([1, 2, 0], Order(rows).Take(3));
    }

    [Fact]
    public void TheListScrollsWhileAnItemIsHeldNearItsEnd()
    {
        var rows = Rows(100);
        var list = List(rows);
        SkUiTestHelpers.Arrange(list, 300, 500);

        var id = MouseDrag(list, new Point(150, 25), new Point(150, 490), release: false);
        Assert.True(list.IsReorderAutoScrolling);
        var before = list.ReorderState!.Value.TargetIndex;
        for (var step = 0; step < 40; step++)
        {
            list.StepReorderAutoScroll(0.05);
            SkUiTestHelpers.Arrange(list, 300, 500);
        }
        Assert.True(list.ScrollY > 1000);
        // The lifted item's container was released (recycled): the drag goes on.
        Assert.True(list.RowsLayout.RealizedRange.First > 0);
        var target = list.ReorderState!.Value.TargetIndex;
        Assert.True(target > before + 3);
        // Rows realized while scrolling show where the drag wants them.
        Assert.Equal(-50, Host(list, target).TranslationY);

        var scrolled = list.ScrollY;
        Release(list, id, new Point(150, 490), SkUiPointerDevice.Mouse);
        Assert.False(list.IsReorderAutoScrolling);
        Assert.Equal(0, rows[target].Index);
        Assert.Equal(scrolled, list.ScrollY);
    }

    [Fact]
    public void AChangeOfTheSourceWhileDraggingCancelsTheDrag()
    {
        var rows = Rows(10);
        var list = List(rows);
        var completed = 0;
        list.ReorderCompleted += (_, _) => completed++;
        SkUiTestHelpers.Arrange(list, 300, 500);

        var id = MouseDrag(list, new Point(150, 25), new Point(150, 145), release: false);
        rows.Add(new Row(10));
        Assert.Null(list.ReorderState);
        Assert.Null(list.ReorderProxy);
        Assert.Equal(1, Host(list, 0).Opacity);
        Assert.Equal(0, Host(list, 1).TranslationY);
        Release(list, id, new Point(150, 145), SkUiPointerDevice.Mouse);
        Assert.Equal(0, completed);
        Assert.Equal([0, 1, 2], Order(rows).Take(3));

        // Turning reordering off cancels too.
        MouseDrag(list, new Point(150, 25), new Point(150, 145), release: false);
        Assert.NotNull(list.ReorderState);
        list.CanReorderItems = false;
        Assert.Null(list.ReorderState);
    }

    [Fact]
    public void ScreenReadersMoveItemsOnePlace()
    {
        var rows = Rows(5);
        var list = List(rows);
        var completed = 0;
        list.ReorderCompleted += (_, _) => completed++;
        SkUiTestHelpers.Arrange(list, 300, 500);

        static List<string> Actions(View host)
        {
            var info = new SkUiSemanticsInfo();
            ((ISkUiAccessibleNode)host).GetSemantics(info);
            return info.CustomActions?.ToList() ?? [];
        }

        Assert.Equal(["Move down"], Actions(Host(list, 0)));
        Assert.Equal(["Move up", "Move down"], Actions(Host(list, 2)));
        Assert.Equal(["Move up"], Actions(Host(list, 4)));

        Assert.True(((ISkUiAccessibleNode)Host(list, 2)).PerformSemanticsCustomAction(0)); // up
        Assert.Equal([0, 2, 1, 3, 4], Order(rows));
        Assert.Equal(1, completed);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.True(((ISkUiAccessibleNode)Host(list, 0)).PerformSemanticsCustomAction(0)); // down: the only move offered
        Assert.Equal([2, 0, 1, 3, 4], Order(rows));

        list.CanReorderItems = false;
        Assert.Empty(Actions(Host(list, 2)));

        // A grid steps sideways.
        var grid = List(Rows(6), span: 3);
        SkUiTestHelpers.Arrange(grid, 300, 500);
        Assert.Equal(["Move left", "Move right"], Actions(Host(grid, 1)));
    }
}
