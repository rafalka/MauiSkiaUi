using System.Collections.ObjectModel;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.10 C4: animated inserts, removes and moves of <see cref="SkUiCollectionView"/> (<see cref="SkUiCollectionView.AnimatesItemChanges"/>):
/// moved rows and cells glide from their old places on the render thread while their views keep the final layout,
/// inserted rows fade in, removed rows fade out in a host under the items and go back to their pool, and the cases that
/// skip the animation. In the global collection: Reduce Motion and the look are process-wide.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class CollectionViewItemAnimationTests
{
    private sealed record Row(int Index);

    private sealed class Section(string name, IEnumerable<Row> rows) : ObservableCollection<Row>(rows)
    {
        public string Name { get; } = name;
    }

    /// <summary>A 50 DIP row (or column), red for item 5, white otherwise.</summary>
    private sealed class RowView : SkUiBox
    {
        public bool Horizontal { get; init; }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (Horizontal)
                WidthRequest = 50;
            else
                HeightRequest = 50;
            Color = BindingContext is Row { Index: 5 } ? Colors.Red : Colors.White;
        }
    }

    private static ObservableCollection<Row> Rows(int count) => new(Enumerable.Range(0, count).Select(index => new Row(index)));

    private static SkUiCollectionView List(IEnumerable<object> source, bool? animates = true, bool horizontal = false) => new()
    {
        ItemsSource = source,
        ItemTemplate = new DataTemplate(() => new RowView { Horizontal = horizontal }),
        AnimatesItemChanges = animates,
        Orientation = horizontal ? ItemsLayoutOrientation.Horizontal : ItemsLayoutOrientation.Vertical,
        VerticalScrollBarVisibility = ScrollBarVisibility.Never,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
        Overscroll = SkUiOverscrollMode.None,
        PrefetchBudget = TimeSpan.FromSeconds(10)
    };

    /// <summary>The container of the realized item at <paramref name="index"/>.</summary>
    private static SkUiView Host(SkUiCollectionView list, int index) => (SkUiView)((Element)list.GetRealizedView(index)!).Parent;

    private static Point? From(SkUiCollectionView list, int index) => list.MotionFrom(Host(list, index));

    [Fact]
    public void InsertedRowsFadeInAndTheRowsAfterThemGlideFromTheirOldPlaces()
    {
        var rows = Rows(100);
        var list = List(rows);
        using var surface = new SkUiTestSurface(list, 300, 500);
        surface.Frame(0);

        rows.Insert(2, new Row(100));
        surface.Frame(16); // the animation starts after the layout that follows the change
        Assert.True(list.IsFadingIn(Host(list, 2)));
        Assert.Null(From(list, 1)); // before the insert: in place
        Assert.Equal(new Point(0, -50), From(list, 3)); // was at 100, laid out at 150
        Assert.Equal(new Point(0, -50), From(list, 8));
        // The views keep their final layout (taps hit it): only the drawing starts from the old places.
        Assert.Equal(0, Host(list, 3).TranslationY);
        Assert.Equal(150, Host(list, 3).Y);
    }

    [Fact]
    public void TheRenderThreadDrawsMovedRowsFromTheirOldPlaces()
    {
        var rows = Rows(30);
        var list = List(rows);
        using var surface = new SkUiTestSurface(list, 300, 500);
        surface.Frame(0);
        Assert.Equal(SKColors.Red, surface.Frame(10).GetPixel(150, 275)); // item 5 at 250..300

        rows.Insert(0, new Row(100));
        // First frame of the glide: item 5 still drawn where it was; at the end at its new place (300..350).
        Assert.Equal(SKColors.Red, surface.Frame(20).GetPixel(150, 275));
        var end = surface.Frame(20 + 1000);
        Assert.Equal(SKColors.Red, end.GetPixel(150, 325));
        Assert.NotEqual(SKColors.Red, end.GetPixel(150, 275));
    }

    [Fact]
    public void RemovedRowsFadeOutWhereTheyWereAndGoBackToTheirPool()
    {
        var rows = Rows(100);
        var list = List(rows);
        using var surface = new SkUiTestSurface(list, 300, 500);
        surface.Frame(0);
        var removedView = Host(list, 3);
        var pooled = list.RowsLayout.PooledViews;

        rows.RemoveAt(3);
        surface.Frame(10);
        Assert.Equal(1, list.FadingViewCount);
        Assert.Equal(new Point(0, 50), From(list, 3)); // the next row came up from 200
        surface.Frame(10);
        Assert.Equal(new Rect(0, 150, 300, 50), removedView.Frame); // in the host under the items, at its old place
        Assert.Equal(pooled, list.RowsLayout.PooledViews);

        surface.Frame(10 + 1000); // the fade ends; its end is reported back to the UI
        Assert.Equal(0, list.FadingViewCount);
        Assert.Equal(1, removedView.Opacity);
        Assert.Equal(pooled + 1, list.RowsLayout.PooledViews);
    }

    [Fact]
    public void MovedItemsGlideBothWays()
    {
        var rows = Rows(30);
        var list = List(rows);
        using var surface = new SkUiTestSurface(list, 300, 500);
        surface.Frame(0);

        rows.Move(1, 5);
        surface.Frame(16);
        Assert.Equal(new Point(0, -200), From(list, 5)); // the moved item: from 50 to 250
        Assert.Equal(new Point(0, 50), From(list, 1)); // the ones it passed: up by a row
        Assert.Equal(new Point(0, 50), From(list, 4));
        Assert.Null(From(list, 6));
    }

    [Fact]
    public void GridCellsMoveToTheirNewCells()
    {
        var rows = Rows(20);
        var list = List(rows);
        list.Span = 2; // cells 150 wide, rows 50 high
        using var surface = new SkUiTestSurface(list, 300, 500);
        surface.Frame(0);

        rows.Insert(0, new Row(100));
        surface.Frame(16);
        Assert.True(list.IsFadingIn(Host(list, 0)));
        Assert.Equal(new Point(-150, 0), From(list, 1)); // item 0: from the first cell to the second
        Assert.Equal(new Point(150, -50), From(list, 2)); // item 1: from the second cell of row 0 to the first of row 1
    }

    [Fact]
    public void HorizontalListsRightToLeftGlideAlongX()
    {
        var rows = Rows(30);
        var list = List(rows, horizontal: true);
        list.FlowDirection = FlowDirection.RightToLeft;
        using var surface = new SkUiTestSurface(list, 500, 100);
        surface.Frame(0);

        rows.Insert(0, new Row(100));
        surface.Frame(16);
        // Items run from the right: inserting at the start pushes them left, so they come from 50 further right.
        Assert.Equal(new Point(50, 0), From(list, 1));
    }

    [Fact]
    public void GroupsExpandingAndCollapsingUseTheSamePath()
    {
        var sections = new ObservableCollection<Section>
        {
            new("A", Enumerable.Range(0, 3).Select(index => new Row(index))),
            new("B", Enumerable.Range(3, 3).Select(index => new Row(index)))
        };
        var list = List(sections);
        list.IsGrouped = true;
        list.GroupHeaderTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = 30 });
        using var surface = new SkUiTestSurface(list, 300, 500);
        surface.Frame(0);

        list.CollapseGroup(sections[0]);
        surface.Frame(5);
        Assert.Equal(3, list.FadingViewCount);
        Assert.Equal(new Point(0, 150), From(list, 3)); // B's first item comes up by A's three rows
        surface.Frame(10); // the fades start
        surface.Frame(1000);
        Assert.Equal(0, list.FadingViewCount);

        list.ExpandGroup(sections[0]);
        surface.Frame(1005);
        Assert.True(list.IsFadingIn(Host(list, 0)));
        Assert.Equal(new Point(0, -150), From(list, 3));
    }

    [Fact]
    public void TheChangesOfAFrameAnimateTogetherAndALaterOneStartsFromWhereRowsAreDrawn()
    {
        var rows = Rows(30);
        var list = List(rows);
        using var surface = new SkUiTestSurface(list, 300, 500);
        surface.Frame(0);

        // A burst before the next layout: one animation from where things showed before the first change.
        rows.Insert(0, new Row(100));
        rows.Insert(0, new Row(101));
        rows.RemoveAt(4); // item 2
        surface.Frame(16);
        Assert.Equal(new Point(0, -100), From(list, 3)); // item 1: 50 → 150
        Assert.Equal(new Point(0, -50), From(list, 4)); // item 3: came up by the removed row, down by the inserted two
        Assert.Equal(1, list.FadingViewCount);

        // A change while they glide: from where they are drawn (just started: about where they were).
        rows.Insert(0, new Row(102));
        surface.Frame(16);
        Assert.InRange(From(list, 4)!.Value.Y, -150.5, -145); // item 1 again: 50 → 200 on screen
    }

    [Fact]
    public void ChangesSkipTheAnimationWhenOffResetFarOrWithReducedMotion()
    {
        var rows = Rows(100);
        var off = List(rows, animates: null); // the look's default: off in 1.x
        using (var surface = new SkUiTestSurface(off, 300, 500))
        {
            surface.Frame(0);
            rows.Insert(0, new Row(100));
            surface.Frame(16);
            Assert.Null(From(off, 3));
            Assert.False(off.IsFadingIn(Host(off, 0)));
        }

        var list = List(Rows(100));
        using var shown = new SkUiTestSurface(list, 300, 500);
        shown.Frame(0);
        var source = (ObservableCollection<Row>)list.ItemsSource!;
        source.Insert(80, new Row(100)); // far below what shows: nothing moves on screen
        shown.Frame(16);
        Assert.Null(From(list, 3));
        source.RemoveAt(90);
        shown.Frame(32);
        Assert.Equal(0, list.FadingViewCount);
        source.RemoveAt(2);
        source.Clear(); // a reset drops the pending animation
        shown.Frame(48);
        Assert.Equal(0, list.FadingViewCount);

        var reduce = SkUiMotion.ReduceMotion;
        try
        {
            SkUiMotion.ReduceMotion = true;
            foreach (var row in Rows(20))
                source.Add(row);
            shown.Frame(60);
            source.Insert(0, new Row(100));
            shown.Frame(70);
            Assert.Null(From(list, 3));
            source.RemoveAt(0);
            shown.Frame(80);
            Assert.Equal(0, list.FadingViewCount);
        }
        finally
        {
            SkUiMotion.ReduceMotion = reduce;
        }
    }

    [Fact]
    public void ALookCanTurnTheAnimationsOnEverywhere()
    {
        var previous = SkUiLook.Current;
        SkUiLook.Current = new AnimatingLook();
        try
        {
            var rows = Rows(30);
            var list = List(rows, animates: null);
            using var surface = new SkUiTestSurface(list, 300, 500);
            surface.Frame(0);
            rows.Insert(0, new Row(100));
            surface.Frame(16);
            Assert.Equal(new Point(0, -50), From(list, 1));
            list.AnimatesItemChanges = false; // the list's own setting wins
            surface.Frame(2000);
            surface.Frame(2016);
            rows.Insert(0, new Row(101));
            surface.Frame(2032);
            Assert.Null(From(list, 1));
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    [Fact]
    public void RowsAfterARowThatTakesItsSelectedTemplateGlideToMakeRoom()
    {
        var rows = Rows(30);
        var list = List(rows);
        list.SelectionMode = SkUiSelectionMode.Single;
        list.SelectedItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = 120 });
        using var surface = new SkUiTestSurface(list, 300, 500);
        surface.Frame(0);

        list.SelectedItem = rows[2];
        surface.Frame(16);
        Assert.Null(From(list, 2)); // the selected row stays where it starts
        Assert.Equal(new Point(0, -70), From(list, 3)); // the next one makes room: from 150 to 220
    }

    private sealed class AnimatingLook : DefaultSkUiLook
    {
        public override bool CollectionViewAnimatesItemChanges => true;
    }
}
