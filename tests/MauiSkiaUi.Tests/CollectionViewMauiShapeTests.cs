using System.Collections.ObjectModel;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The collection view's and carousel's MAUI shape (steps V1–V3 and V6 in docs/design/ImplementationPlan-1.2.md):
/// <c>ItemsLayout</c>, <c>ItemSizingStrategy</c>, MAUI's <c>ScrollTo</c> with <c>ScrollToRequested</c>, and data in
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
    public void ItemsLayoutStringsAndElementsSetTheListsLayout()
    {
        var grid = Load<SkUiCollectionView>("""<sk:SkUiCollectionView ItemsLayout="VerticalGrid, 3" />""");
        Assert.Equal((ItemsLayoutOrientation.Vertical, 3), (grid.Orientation, grid.Span));

        var horizontal = Load<SkUiCollectionView>("""
            <sk:SkUiCollectionView>
              <sk:SkUiCollectionView.ItemsLayout>
                <GridItemsLayout Orientation="Horizontal" Span="2" HorizontalItemSpacing="8" VerticalItemSpacing="4" />
              </sk:SkUiCollectionView.ItemsLayout>
            </sk:SkUiCollectionView>
            """);
        // Horizontal: the rows follow each other horizontally, so the horizontal spacing is the gap between them.
        Assert.Equal((ItemsLayoutOrientation.Horizontal, 2, 8d, 4d), (horizontal.Orientation, horizontal.Span, horizontal.ItemSpacing, horizontal.SpanSpacing));

        var list = Load<SkUiCollectionView>("""<sk:SkUiCollectionView ItemsLayout="HorizontalList" />""");
        Assert.Equal((ItemsLayoutOrientation.Horizontal, 1), (list.Orientation, list.Span));
    }

    [Fact]
    public void TheLastOfTheLayoutAndTheListsPropertiesWins()
    {
        var layout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 6 };
        var list = new SkUiCollectionView { ItemsLayout = layout };
        Assert.Equal(6, list.ItemSpacing);

        list.ItemSpacing = 2;
        Assert.Equal(2, list.ItemSpacing);
        layout.ItemSpacing = 10; // the layout changed after: it wins again
        Assert.Equal(10, list.ItemSpacing);
        Assert.IsType<LinearItemsLayout>(new SkUiCollectionView().ItemsLayout); // MAUI's default, a vertical list
    }

    [Fact]
    public void TheCarouselTakesALinearItemsLayout()
    {
        var carousel = Load<SkUiCarouselView>("""
            <sk:SkUiCarouselView>
              <sk:SkUiCarouselView.ItemsLayout>
                <LinearItemsLayout Orientation="Vertical" ItemSpacing="10" SnapPointsType="Mandatory" SnapPointsAlignment="Start" />
              </sk:SkUiCarouselView.ItemsLayout>
            </sk:SkUiCarouselView>
            """);
        Assert.Equal((ItemsLayoutOrientation.Vertical, 10d, SnapPointsType.Mandatory, SnapPointsAlignment.Start),
            (carousel.Orientation, carousel.ItemSpacing, carousel.SnapPointsType, carousel.SnapPointsAlignment));

        var vertical = Load<SkUiCarouselView>("""<sk:SkUiCarouselView ItemsLayout="VerticalList" />""");
        Assert.Equal(ItemsLayoutOrientation.Vertical, vertical.Orientation);
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
    public void StringHeadersFootersAndEmptyViewsShowAsLabels()
    {
        var list = Load<SkUiCollectionView>($"""<sk:SkUiCollectionView Header="{ContactsTitle}" Footer="End" EmptyView="{NoContacts}" />""");
        SkUiTestHelpers.Arrange(list, 300, 400);

        var (header, footer, empty) = list.Shown;
        Assert.Equal(ContactsTitle, Assert.IsType<SkUiLabel>(header).Text);
        Assert.Equal("End", Assert.IsType<SkUiLabel>(footer).Text);
        Assert.Equal(NoContacts, Assert.IsType<SkUiLabel>(empty).Text);
        Assert.Null(list.Header); // the data is in the bindable property
        Assert.Equal(ContactsTitle, list.GetValue(SkUiCollectionView.HeaderProperty));

        var carousel = Load<SkUiCarouselView>($"""<sk:SkUiCarouselView EmptyView="{NoContacts}" />""");
        SkUiTestHelpers.Arrange(carousel, 300, 400);
        Assert.Equal(NoContacts, Assert.IsType<SkUiLabel>(carousel.ShownEmptyView).Text);
    }

    [Fact]
    public void DataHeadersShowThroughTheTemplateWithTheDataAsBindingContext()
    {
        var list = List(Rows(3));
        list.HeaderTemplate = new DataTemplate(() =>
        {
            var label = new SkUiLabel();
            label.SetBinding(SkUiLabel.TextProperty, nameof(HeaderInfo.Title));
            return label;
        });
        list.SetValue(SkUiCollectionView.HeaderProperty, new HeaderInfo(ContactsTitle));
        SkUiTestHelpers.Arrange(new SkUiContentView { Content = list }, 300, 400);
        Assert.Equal(ContactsTitle, Assert.IsType<SkUiLabel>(list.Shown.Header).Text);

        // New data, then a drawn view: each replaces what shows.
        list.SetValue(SkUiCollectionView.HeaderProperty, new HeaderInfo("People"));
        Assert.Equal("People", Assert.IsType<SkUiLabel>(list.Shown.Header).Text);
        var view = new SkUiBox();
        list.Header = view;
        Assert.Same(view, list.Shown.Header);

        // A native view is refused, as before.
        list.SetValue(SkUiCollectionView.HeaderProperty, new Label());
        Assert.Same(view, list.Header);
    }
}
