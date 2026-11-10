using System.Collections.ObjectModel;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.10 C3: <see cref="SkUiCollectionView.SelectedItemTemplate"/>. A selected item's row is made by its own template: a
/// selection change swaps the row's view (recycled per template) and measures it again, in lists and grids, single and
/// multiple, grouped, with what shows kept in place; a selector may choose none; unset, nothing changes.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class CollectionViewSelectedTemplateTests
{
    private static long _pointer = 720_000;

    private sealed record Row(int Index);

    private sealed class Section(string name, IEnumerable<Row> rows) : ObservableCollection<Row>(rows)
    {
        public string Name { get; } = name;
    }

    /// <summary>The item template's view: 50 high.</summary>
    private sealed class Plain : SkUiBox
    {
        public Plain() => HeightRequest = 50;
    }

    /// <summary>The selected item template's view: 120 high.</summary>
    private sealed class Detail : SkUiBox
    {
        public Detail() => HeightRequest = 120;
    }

    private sealed class EvenOnly : DataTemplateSelector
    {
        private readonly DataTemplate _detail = new(() => new Detail());

        protected override DataTemplate? OnSelectTemplate(object item, BindableObject container) => ((Row)item).Index % 2 == 0 ? _detail : null;
    }

    private static ObservableCollection<Row> Rows(int count) => new(Enumerable.Range(0, count).Select(index => new Row(index)));

    private static SkUiCollectionView List(IEnumerable<object> source, SkUiSelectionMode mode = SkUiSelectionMode.SingleDeselect, bool selected = true)
    {
        var list = new SkUiCollectionView
        {
            ItemsSource = source,
            ItemTemplate = new DataTemplate(() => new Plain()),
            SelectedItemTemplate = selected ? new DataTemplate(() => new Detail()) : null,
            SelectionMode = mode,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Overscroll = SkUiOverscrollMode.None,
            PrefetchBudget = TimeSpan.FromSeconds(10)
        };
        SkUiTestHelpers.Arrange(list, 300, 500);
        return list;
    }

    private static void Layout(SkUiCollectionView list) => SkUiTestHelpers.Arrange(list, 300, 500);

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    /// <summary>The container of the realized item at <paramref name="index"/>, where it shows in the list.</summary>
    private static Rect OnScreen(SkUiCollectionView list, int index)
    {
        var view = (View)((Element)list.GetRealizedView(index)!).Parent;
        var x = view.Frame.X;
        var y = view.Frame.Y;
        for (var parent = view.Parent as View; parent is not null && !ReferenceEquals(parent, list); parent = parent.Parent as View)
        {
            x += parent.Frame.X;
            y += parent.Frame.Y;
            if (parent is SkUiScrollView scroller)
                y -= scroller.ScrollY;
        }
        return new Rect(x, y, view.Frame.Width, view.Frame.Height);
    }

    [Fact]
    public void ASelectedRowSwapsToItsTemplateAndBackAndIsMeasuredAgain()
    {
        var rows = Rows(50);
        var list = List(rows);
        var plain = list.GetRealizedView(2);
        Assert.IsType<Plain>(plain);

        Tap(list, new Point(150, 125)); // item 2
        Layout(list);
        Assert.Same(rows[2], list.SelectedItem);
        var detail = Assert.IsType<Detail>(list.GetRealizedView(2));
        Assert.Equal(new Rect(0, 100, 300, 120), OnScreen(list, 2));
        Assert.Equal(220, OnScreen(list, 3).Y); // the rows after it move down
        Assert.True(detail.IsSelectedItem); // the Selected state on its root, as for the item template

        // Deselected: a view of the item template again; another selected row reuses the recycled detail view.
        Tap(list, new Point(150, 160));
        Layout(list);
        Assert.Null(list.SelectedItem);
        Assert.IsType<Plain>(list.GetRealizedView(2));
        Assert.Equal(150, OnScreen(list, 3).Y);
        list.SelectedItem = rows[4];
        Layout(list);
        Assert.Same(detail, list.GetRealizedView(4));
        Assert.Equal(120, OnScreen(list, 4).Height);
        list.SelectedItem = rows[5];
        Layout(list);
        Assert.IsType<Plain>(list.GetRealizedView(4));
        Assert.Same(detail, list.GetRealizedView(5));
    }

    [Fact]
    public void MultipleSelectionSelectAllAndRowsNotRealizedUseTheTemplate()
    {
        var rows = Rows(200);
        var list = List(rows, SkUiSelectionMode.Multiple);
        list.SelectedItems.Add(rows[1]);
        list.SelectedItems.Add(rows[3]);
        Layout(list);
        Assert.IsType<Detail>(list.GetRealizedView(1));
        Assert.IsType<Plain>(list.GetRealizedView(2));
        Assert.IsType<Detail>(list.GetRealizedView(3));

        // Selected while far from view: realized later with the template, measured at its size.
        list.SelectAll();
        Layout(list);
        Assert.All(Enumerable.Range(0, 4), index => Assert.IsType<Detail>(list.GetRealizedView(index)));
        list.ScrollToIndex(150, ScrollToPosition.Start, animated: false);
        Layout(list);
        Assert.IsType<Detail>(list.GetRealizedView(150));
        Assert.Equal(120, OnScreen(list, 150).Height);
        Assert.Equal(OnScreen(list, 150).Bottom, OnScreen(list, 151).Y, 1);

        list.ClearSelection();
        Layout(list);
        Assert.IsType<Plain>(list.GetRealizedView(150));
        Assert.Equal(50, OnScreen(list, 150).Height);
    }

    [Fact]
    public void WhatShowsStaysInPlaceWhenRowsBeforeItChangeSize()
    {
        var rows = Rows(100);
        var list = List(rows, SkUiSelectionMode.Multiple);
        list.ScrollView.ScrollTo(0, 20 * 50);
        Layout(list);
        Assert.Equal(0, OnScreen(list, 20).Y, 1);

        list.SelectedItems.Add(rows[10]); // above the viewport
        Layout(list);
        Assert.Equal(0, OnScreen(list, 20).Y, 1);
        list.SelectedItems.Add(rows[21]); // in view, after the first visible row: the rows after it move
        Layout(list);
        Assert.Equal(0, OnScreen(list, 20).Y, 1);
        Assert.Equal(50, OnScreen(list, 21).Y, 1);
        Assert.Equal(170, OnScreen(list, 22).Y, 1);
        list.SelectedItems.Remove(rows[10]);
        Layout(list);
        Assert.Equal(0, OnScreen(list, 20).Y, 1);
    }

    [Fact]
    public void GridCellsSwapAndTheRowTakesTheTallestCell()
    {
        var rows = Rows(20);
        var list = List(rows, SkUiSelectionMode.Single);
        list.Span = 2;
        Layout(list);
        Assert.Equal(50, OnScreen(list, 2).Y);

        list.SelectedItem = rows[1];
        Layout(list);
        Assert.IsType<Plain>(list.GetRealizedView(0));
        Assert.IsType<Detail>(list.GetRealizedView(1));
        Assert.Equal(120, OnScreen(list, 2).Y); // the next row starts after the tallest cell
        list.SelectedItem = rows[2];
        Layout(list);
        Assert.IsType<Plain>(list.GetRealizedView(1));
        Assert.IsType<Detail>(list.GetRealizedView(2));
        Assert.Equal(50, OnScreen(list, 2).Y);
        Assert.Equal(170, OnScreen(list, 4).Y);
    }

    [Fact]
    public void AnItemInSeveralGroupsSwapsWhereverItShows()
    {
        var rows = Rows(6);
        var sections = new ObservableCollection<Section> { new("A", rows.Take(3)), new("B", [.. rows.Skip(3), rows[0]]) };
        var list = List(sections, SkUiSelectionMode.Single);
        list.IsGrouped = true;
        Layout(list);

        list.SelectedItem = rows[0];
        Layout(list);
        Assert.IsType<Detail>(list.GetRealizedView(0));
        Assert.IsType<Detail>(list.GetRealizedView(6)); // its second appearance
        Assert.IsType<Plain>(list.GetRealizedView(1));
    }

    [Fact]
    public void ASelectorMayChooseNoneAndAnUnsetTemplateChangesNothing()
    {
        var rows = Rows(10);
        var list = List(rows, SkUiSelectionMode.Multiple);
        list.SelectedItemTemplate = new EvenOnly();
        Layout(list);
        list.SelectAll();
        Layout(list);
        Assert.IsType<Detail>(list.GetRealizedView(2));
        Assert.IsType<Plain>(list.GetRealizedView(3)); // the item template for what the selector leaves

        var unset = List(rows, SkUiSelectionMode.Single, selected: false);
        var view = unset.GetRealizedView(2);
        unset.SelectedItem = rows[2];
        Layout(unset);
        Assert.Same(view, unset.GetRealizedView(2));
        Assert.Equal(50, OnScreen(unset, 2).Height);

        // Set later: every row is made again with it.
        unset.SelectedItemTemplate = new DataTemplate(() => new Detail());
        Layout(unset);
        Assert.IsType<Detail>(unset.GetRealizedView(2));
    }
}
