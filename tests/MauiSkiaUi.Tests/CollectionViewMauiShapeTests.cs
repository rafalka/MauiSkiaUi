using System.Collections.ObjectModel;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The collection view's and carousel's MAUI shape (steps V1–V3 and V6 in docs/design/ImplementationPlan-1.2.md):
/// <c>ItemsLayout</c>, <c>ItemSizingStrategy</c>, MAUI's <c>ScrollTo</c> with <c>ScrollToRequested</c>, and text in
/// <c>Header</c> / <c>Footer</c> / <c>EmptyView</c>, in MAUI's XAML with only the prefix changed.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class CollectionViewMauiShapeTests
{
    private const string ContactsTitle = "Contacts";
    private const string NoContacts = "No contacts";

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

    private sealed record HeaderInfo(string Title);

    private static ObservableCollection<Row> Rows(int count, Func<int, double>? height = null) =>
        new(Enumerable.Range(0, count).Select(index => new Row(index, height?.Invoke(index) ?? 50)));

    private static SkUiCollectionView List(System.Collections.IEnumerable? rows) => new()
    {
        ItemsSource = rows,
        ItemTemplate = new DataTemplate(() => new RowView()),
        VerticalScrollBarVisibility = ScrollBarVisibility.Never,
        Overscroll = SkUiOverscrollMode.None
    };

    /// <summary>The height of a row's slot (the item view in it keeps its own size request).</summary>
    private static double RowHeight(SkUiCollectionView list, int row) => ((View)list.RowsLayout.GetRealizedView(row)!).Height;

    private static T Load<T>(string xaml) where T : View
    {
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, $"""
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              {xaml}
            </ContentView>
            """);
        return (T)root.Content;
    }

    [Fact]
    public void ItemsLayoutStringsAndElementsDecideTheListsLayout()
    {
        var grid = Load<SkUiCollectionView>("""<sk:SkUiCollectionView ItemsLayout="VerticalGrid, 3" />""");
        Assert.Equal((false, 3), (grid.LayoutInUse.Horizontal, grid.LayoutInUse.Span));
        Assert.Equal(1, grid.Span); // read, not written: the list's own properties keep their values

        var horizontal = Load<SkUiCollectionView>("""
            <sk:SkUiCollectionView>
              <sk:SkUiCollectionView.ItemsLayout>
                <GridItemsLayout Orientation="Horizontal" Span="2" HorizontalItemSpacing="8" VerticalItemSpacing="4" />
              </sk:SkUiCollectionView.ItemsLayout>
            </sk:SkUiCollectionView>
            """);
        // Horizontal: the rows follow each other horizontally, so the horizontal spacing is the gap between them.
        Assert.Equal((true, 2, 8d, 4d), horizontal.LayoutInUse);

        var list = Load<SkUiCollectionView>("""<sk:SkUiCollectionView ItemsLayout="HorizontalList" />""");
        Assert.Equal((true, 1), (list.LayoutInUse.Horizontal, list.LayoutInUse.Span));
    }

    [Fact]
    public void ASetItemsLayoutDecidesWhateverTheAttributeOrder()
    {
        foreach (var xaml in new[]
                 {
                     """<sk:SkUiCollectionView ItemSpacing="8" Span="2" ItemsLayout="VerticalList" />""",
                     """<sk:SkUiCollectionView ItemsLayout="VerticalList" ItemSpacing="8" Span="2" />"""
                 })
        {
            var list = Load<SkUiCollectionView>(xaml);
            Assert.Equal((false, 1, 0d, 0d), list.LayoutInUse);
            Assert.Equal((8d, 2), (list.ItemSpacing, list.Span));
        }
    }

    [Fact]
    public void TheListFollowsItsItemsLayoutUntilItIsCleared()
    {
        var layout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 6 };
        var list = new SkUiCollectionView { ItemsLayout = layout, ItemSpacing = 2, Orientation = ItemsLayoutOrientation.Horizontal };
        Assert.Equal((false, 6d), (list.LayoutInUse.Horizontal, list.LayoutInUse.ItemSpacing));

        layout.ItemSpacing = 10;
        Assert.Equal(10, list.LayoutInUse.ItemSpacing);

        list.ClearValue(SkUiCollectionView.ItemsLayoutProperty);
        Assert.Equal((true, 2d), (list.LayoutInUse.Horizontal, list.LayoutInUse.ItemSpacing));
        layout.ItemSpacing = 12; // no longer followed
        Assert.Equal(2, list.LayoutInUse.ItemSpacing);
        Assert.IsType<LinearItemsLayout>(new SkUiCollectionView().ItemsLayout); // MAUI's default, a vertical list
    }

    [Fact]
    public void TheCarouselTakesALinearItemsLayout()
    {
        var carousel = Load<SkUiCarouselView>("""
            <sk:SkUiCarouselView ItemSpacing="3">
              <sk:SkUiCarouselView.ItemsLayout>
                <LinearItemsLayout Orientation="Vertical" ItemSpacing="10" SnapPointsType="Mandatory" SnapPointsAlignment="Start" />
              </sk:SkUiCarouselView.ItemsLayout>
            </sk:SkUiCarouselView>
            """);
        Assert.Equal((false, 10d, SnapPointsType.Mandatory, SnapPointsAlignment.Start), carousel.LayoutInUse);
        Assert.Equal((ItemsLayoutOrientation.Horizontal, 3d), (carousel.Orientation, carousel.ItemSpacing));

        carousel.ClearValue(SkUiCarouselView.ItemsLayoutProperty);
        Assert.Equal((true, 3d, SnapPointsType.MandatorySingle, SnapPointsAlignment.Center), carousel.LayoutInUse);

        var vertical = Load<SkUiCarouselView>("""<sk:SkUiCarouselView ItemsLayout="VerticalList" />""");
        Assert.False(vertical.LayoutInUse.Horizontal);
        Assert.Equal(SnapPointsType.MandatorySingle, new SkUiCarouselView().ItemsLayout.SnapPointsType);
    }

    [Fact]
    public void MeasureFirstItemGivesEveryRowTheFirstRowsSize()
    {
        var rows = Rows(20, index => index == 0 ? 80 : 40);
        var list = List(rows);
        list.ItemSizingStrategy = ItemSizingStrategy.MeasureFirstItem;
        SkUiTestHelpers.Arrange(list, 300, 400);
        Assert.Equal(80, RowHeight(list, 1));

        list.ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems;
        SkUiTestHelpers.Arrange(list, 300, 400);
        Assert.Equal(40, RowHeight(list, 1));

        // ItemExtent wins over the first item's size.
        list.ItemSizingStrategy = ItemSizingStrategy.MeasureFirstItem;
        list.ItemExtent = 30;
        SkUiTestHelpers.Arrange(list, 300, 400);
        Assert.Equal(30, RowHeight(list, 1));
    }

    [Fact]
    public void MauisScrollToRaisesScrollToRequestedThenScrolls()
    {
        var rows = Rows(40);
        var list = List(rows);
        SkUiTestHelpers.Arrange(list, 300, 400);
        var requests = new List<ScrollToRequestEventArgs>();
        list.ScrollToRequested += (_, args) => requests.Add(args);

        list.ScrollTo(20, position: ScrollToPosition.Start, animate: false);
        SkUiTestHelpers.Arrange(list, 300, 400);
        Assert.Equal(20 * 50, list.ScrollY, 1);
        list.ScrollTo(rows[30], position: ScrollToPosition.Start, animate: false);
        SkUiTestHelpers.Arrange(list, 300, 400);
        Assert.Equal(30 * 50, list.ScrollY, 1);
        list.ScrollTo(99, animate: false); // not an item: ignored, as on MAUI
        Assert.Equal(30 * 50, list.ScrollY, 1);

        Assert.Equal((20, -1, ScrollToPosition.Start, false), (requests[0].Index, requests[0].GroupIndex, requests[0].ScrollToPosition, requests[0].IsAnimated));
        Assert.Same(rows[30], requests[1].Item);
        Assert.Equal(3, requests.Count);
    }

    [Fact]
    public void MauisScrollToTakesAnIndexInAGroup()
    {
        List<List<Row>> groups = [[.. Rows(10)], [.. Rows(10)]];
        var list = List(groups);
        list.IsGrouped = true;
        SkUiTestHelpers.Arrange(list, 300, 400);

        list.ScrollTo(2, groupIndex: 1, position: ScrollToPosition.Start, animate: false);
        SkUiTestHelpers.Arrange(list, 300, 400);
        Assert.Equal(12 * 50, list.ScrollY, 1); // no group headers: the twelfth row
    }

    [Fact]
    public void StringHeadersFootersAndEmptyViewsBecomeLabels()
    {
        var list = Load<SkUiCollectionView>($"""<sk:SkUiCollectionView Header="{ContactsTitle}" Footer="End" EmptyView="{NoContacts}" />""");
        SkUiTestHelpers.Arrange(list, 300, 400);

        Assert.Equal(ContactsTitle, Assert.IsType<SkUiLabel>(list.Header).Text);
        Assert.Equal("End", Assert.IsType<SkUiLabel>(list.Footer).Text);
        var empty = Assert.IsType<SkUiLabel>(list.EmptyView);
        Assert.Equal((NoContacts, TextAlignment.Center, TextAlignment.Center), (empty.Text, empty.HorizontalTextAlignment, empty.VerticalTextAlignment));

        var carousel = Load<SkUiCarouselView>($"""<sk:SkUiCarouselView EmptyView="{NoContacts}" />""");
        SkUiTestHelpers.Arrange(carousel, 300, 400);
        Assert.Equal(NoContacts, Assert.IsType<SkUiLabel>(carousel.EmptyView).Text);
    }

    [Fact]
    public void DataIsRefusedAsBefore()
    {
        // Data headers convert to a template bound through the list's binding context (Migration.md).
        var list = new SkUiCollectionView();
        list.SetValue(SkUiCollectionView.HeaderProperty, new HeaderInfo(ContactsTitle));
        Assert.Null(list.Header);
        list.SetValue(SkUiCollectionView.HeaderProperty, new Label());
        Assert.Null(list.Header);
    }
}
