using System.Collections.ObjectModel;
using System.Windows.Input;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// FR-22 <see cref="SkUiCollectionView"/> (Phase B2): items on the FR-21 engine, header and footer (scrolled and sticky), the
/// empty view, single selection and its visual state, item taps versus tappable views inside items, the load-more threshold,
/// scrolling to an item, and pull-to-refresh.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class CollectionViewTests
{
    private static long _pointer = 120_000;

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

    private static ObservableCollection<Row> Rows(int count) => new(Enumerable.Range(0, count).Select(index => new Row(index)));

    private static SkUiCollectionView List(IEnumerable<Row>? rows, Func<SkUiView>? template = null)
    {
        var list = new SkUiCollectionView
        {
            ItemsSource = rows,
            ItemTemplate = new DataTemplate(() => template?.Invoke() ?? new RowView()),
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Overscroll = SkUiOverscrollMode.None
        };
        list.ItemsLayout.PrefetchBudget = TimeSpan.FromSeconds(10);
        return list;
    }

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    private static void Drag(SkUiView root, Point from, Point to, bool release = true, int steps = 6)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.FromMilliseconds(0)));
        for (var step = 1; step <= steps; step++)
        {
            var point = new Point(from.X + (to.X - from.X) * step / steps, from.Y + (to.Y - from.Y) * step / steps);
            root.Touch(new(id, SkUiTouchAction.Moved, point, TimeSpan.FromMilliseconds(step * 100)));
        }
        if (release)
            root.Touch(new(id, SkUiTouchAction.Released, to, TimeSpan.FromMilliseconds(steps * 100 + 300)));
    }

    /// <summary>Where a view shows in the list (sticky parts, scrolled content).</summary>
    private static Rect OnScreen(SkUiCollectionView list, View view)
    {
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

    private sealed class Command(Action<object?> execute, Func<bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => execute(parameter);
    }

    #region Items, header, footer, empty view

    [Fact]
    public void ItemsAreVirtualizedBelowAScrolledHeaderAndAboveTheFooter()
    {
        var header = new SkUiBox { HeightRequest = 80 };
        var footer = new SkUiBox { HeightRequest = 40 };
        var list = List(Rows(10_000));
        list.Header = header;
        list.Footer = footer;
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(new Rect(0, 0, 300, 80), OnScreen(list, header));
        Assert.Equal(new Rect(0, 80, 300, 50), OnScreen(list, (View)list.GetItemView(0)!));
        Assert.Equal(0, list.FirstVisibleIndex);
        Assert.Equal(8, list.LastVisibleIndex); // 80 + 9 · 50 > 500
        var (_, last) = list.ItemsLayout.RealizedRange;
        Assert.InRange(last, 9, 40);
        Assert.Equal(80 + 10_000 * 50 + 40, list.ScrollView.ContentSize.Height);

        // The header scrolls away with the items; the footer follows the last item.
        list.ScrollView.ScrollTo(0, 80 + 100 * 50);
        Assert.Equal(-80 - 100 * 50, OnScreen(list, header).Y);
        Assert.Equal(100, list.FirstVisibleIndex);
        list.ScrollView.ScrollTo(0, list.ScrollView.ContentSize.Height);
        Assert.Equal(new Rect(0, 460, 300, 40), OnScreen(list, footer));
    }

    [Fact]
    public void StickyHeaderAndFooterStayOutsideTheScrolledItems()
    {
        var header = new SkUiBox { HeightRequest = 60 };
        var footer = new SkUiBox { HeightRequest = 40 };
        var list = List(Rows(1000));
        list.SetHeader(header, sticky: true).SetFooter(footer, sticky: true);
        SkUiTestHelpers.Arrange(list, 300, 500);

        Assert.Equal(new Rect(0, 0, 300, 60), OnScreen(list, header));
        Assert.Equal(new Rect(0, 460, 300, 40), OnScreen(list, footer));
        Assert.Equal(new Rect(0, 60, 300, 400), list.ScrollView.Frame);
        Assert.Equal(1000 * 50, list.ScrollView.ContentSize.Height);
        Assert.Equal(7, list.LastVisibleIndex); // a 400 DIP viewport

        // Scrolling moves the items only: the sticky parts are not inside the scroller, so nothing re-records them.
        list.ScrollView.ScrollTo(0, 5000);
        Assert.Equal(new Rect(0, 0, 300, 60), OnScreen(list, header));
        Assert.Equal(100, list.FirstVisibleIndex);
        Assert.Same(list, ((Element)header).Parent.Parent);

        // Switching to scrolled moves the same header into the scroller.
        list.IsStickyHeader = false;
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.IsType<SkUiContentView>(((Element)header).Parent);
        Assert.NotSame(list, ((Element)header).Parent.Parent);
        Assert.Equal(60 + 1000 * 50, list.ScrollView.ContentSize.Height);
    }

    [Fact]
    public void TheEmptyViewFillsTheListWhileThereAreNoItems()
    {
        var rows = new ObservableCollection<Row>();
        var created = 0;
        var list = List(rows);
        list.Header = new SkUiBox { HeightRequest = 50 };
        list.EmptyViewTemplate = new DataTemplate(() => { created++; return new SkUiLabel { Text = "Nothing here" }; });
        var page = new SkUiContentView { Content = list };
        SkUiTestHelpers.Arrange(page, 300, 500);

        Assert.True(list.IsEmpty);
        Assert.Equal(1, created);
        var empty = (View)list.EmptyView!;
        Assert.Equal(new Rect(0, 50, 300, 450), OnScreen(list, empty));

        rows.Add(new Row(0));
        SkUiTestHelpers.Arrange(page, 300, 500);
        Assert.False(list.IsEmpty);
        Assert.False(((View)empty.Parent).IsVisible);
        Assert.Equal(new Rect(0, 50, 300, 50), OnScreen(list, (View)list.GetItemView(0)!));

        // The template's view is kept for the next time the list is empty; a null source is empty too.
        list.ItemsSource = null;
        SkUiTestHelpers.Arrange(page, 300, 500);
        Assert.True(list.IsEmpty);
        Assert.Same(empty, list.EmptyView);
        Assert.Equal(1, created);
    }

    [Fact]
    public void HeaderTemplatesBindToTheListsContext()
    {
        var list = List(Rows(3));
        list.HeaderTemplate = new DataTemplate(() =>
        {
            var label = new SkUiLabel();
            label.SetBinding(SkUiLabel.TextProperty, ".");
            return label;
        });
        var page = new SkUiContentView { Content = list, BindingContext = "Orders" };
        SkUiTestHelpers.Arrange(page, 300, 500);
        Assert.Equal("Orders", ((SkUiLabel)list.Header!).Text);

        // Making it sticky moves the template's header; a new template still replaces it.
        var header = list.Header;
        list.IsStickyHeader = true;
        Assert.Same(header, list.Header);
        list.HeaderTemplate = new DataTemplate(() => new SkUiBox());
        Assert.IsType<SkUiBox>(list.Header);
    }

    #endregion

    #region Selection and taps

    [Fact]
    public void TapsSelectAnItemAndRaiseTheEventsInOrder()
    {
        var rows = Rows(100);
        var list = List(rows);
        list.SelectionMode = SkUiSelectionMode.Single;
        var log = new List<string>();
        list.SelectionChanging += (_, args) => log.Add($"changing {(args.PreviousItem as Row)?.Index} -> {(args.CurrentItem as Row)?.Index}");
        list.SelectionChangedCommand = new Command(_ => log.Add("command"));
        list.SelectionChanged += (_, args) => log.Add($"changed {(args.PreviousItem as Row)?.Index} -> {(args.CurrentItem as Row)?.Index}");
        list.ItemTapped += (_, args) => log.Add($"tapped {args.Index}");
        list.ItemTappedCommand = new Command(parameter => log.Add($"tap command {((Row)parameter!).Index}"));
        SkUiTestHelpers.Arrange(list, 300, 500);

        Tap(list, new Point(100, 125)); // item 2
        Assert.Same(rows[2], list.SelectedItem);
        Assert.Equal(["changing  -> 2", "command", "changed  -> 2", "tapped 2", "tap command 2"], log);

        // Single: tapping the selected item keeps it.
        log.Clear();
        Tap(list, new Point(100, 125));
        Assert.Same(rows[2], list.SelectedItem);
        Assert.Equal(["tapped 2", "tap command 2"], log);

        // SingleDeselect: it clears it.
        list.SelectionMode = SkUiSelectionMode.SingleDeselect;
        log.Clear();
        Tap(list, new Point(100, 125));
        Assert.Null(list.SelectedItem);
        Assert.Equal(["changing 2 -> ", "command", "changed 2 -> ", "tapped 2", "tap command 2"], log);
    }

    [Fact]
    public void SelectionChangingCanCancelATapsSelection()
    {
        var rows = Rows(10);
        var list = List(rows);
        list.SelectionMode = SkUiSelectionMode.Single;
        list.SelectedItem = rows[0];
        list.SelectionChanging += (_, args) => args.Cancel = true;
        var tapped = 0;
        list.ItemTapped += (_, _) => tapped++;
        SkUiTestHelpers.Arrange(list, 300, 500);

        Tap(list, new Point(100, 75));
        Assert.Same(rows[0], list.SelectedItem);
        Assert.Equal(1, tapped);
    }

    [Fact]
    public void WithoutSelectionItemsStillReportTapsAndModeNoneClearsTheSelection()
    {
        var rows = Rows(10);
        var list = List(rows);
        object? tapped = null;
        list.ItemTapped += (_, args) => tapped = args.Item;
        SkUiTestHelpers.Arrange(list, 300, 500);

        Tap(list, new Point(100, 175));
        Assert.Same(rows[3], tapped);
        Assert.Null(list.SelectedItem);

        list.SelectionMode = SkUiSelectionMode.Single;
        list.SelectedItem = rows[1];
        list.SelectionMode = SkUiSelectionMode.None;
        Assert.Null(list.SelectedItem);
    }

    [Fact]
    public void ATappableViewInsideAnItemKeepsItsTap()
    {
        var clicked = 0;
        var list = List(Rows(10), () =>
        {
            var row = new SkUiHorizontalStackLayout { HeightRequest = 50 };
            row.Children.Add(new SkUiBox { WidthRequest = 200 });
            var button = new SkUiButton { Text = "", WidthRequest = 100 };
            button.Clicked += (_, _) => clicked++;
            row.Children.Add(button);
            return row;
        });
        list.SelectionMode = SkUiSelectionMode.Single;
        var tapped = 0;
        list.ItemTapped += (_, _) => tapped++;
        SkUiTestHelpers.Arrange(list, 300, 500);

        Tap(list, new Point(250, 25)); // the button
        Assert.Equal(1, clicked);
        Assert.Equal(0, tapped);
        Assert.Null(list.SelectedItem);

        Tap(list, new Point(50, 25)); // the rest of the item
        Assert.Equal(1, tapped);
        Assert.NotNull(list.SelectedItem);
    }

    [Fact]
    public void TheSelectedItemsRootGoesToTheSelectedStateAndOnlyChangedItemsRepaint()
    {
        var rows = Rows(100);
        var list = List(rows, () =>
        {
            var view = new RowView();
            VisualStateManager.SetVisualStateGroups(view, [new VisualStateGroup
            {
                Name = "CommonStates",
                States = { new VisualState { Name = "Normal" }, new VisualState { Name = "Selected" }, new VisualState { Name = "PointerOver" } }
            }]);
            return view;
        });
        list.SelectionMode = SkUiSelectionMode.Single;
        SkUiTestHelpers.Arrange(list, 300, 500);
        string State(int index) => VisualStateManager.GetVisualStateGroups((VisualElement)list.GetItemView(index)!)[0].CurrentState?.Name ?? "-";

        list.SelectedItem = rows[1];
        Assert.Equal("Selected", State(1));
        Assert.Equal("Normal", State(0));

        var repainted = new HashSet<object>();
        foreach (var view in list.ItemsLayout.RealizedViews)
            ((SkUiView)view).PaintInvalidated += (sender, _) => repainted.Add(sender!);
        list.SelectedItem = rows[4];
        Assert.Equal("Normal", State(1));
        Assert.Equal("Selected", State(4));
        Assert.Equal(2, repainted.Count);

        // A recycled view shows the selection of the item it is bound to now.
        list.ScrollView.ScrollTo(0, 3000);
        list.ScrollView.ScrollTo(0, 0);
        list.ScrollView.ScrollTo(0, 2000);
        foreach (var index in Enumerable.Range(list.FirstVisibleIndex, list.LastVisibleIndex - list.FirstVisibleIndex + 1))
            Assert.Equal("Normal", State(index));
        list.ScrollView.ScrollTo(0, 0);
        Assert.Equal("Selected", State(4));
    }

    [Fact]
    public void RemovingTheSelectedItemClearsTheSelection()
    {
        var rows = Rows(10);
        var list = List(rows);
        list.SelectionMode = SkUiSelectionMode.Single;
        SkUiTestHelpers.Arrange(list, 300, 500);
        list.SelectedItem = rows[3];
        var changed = 0;
        list.SelectionChanged += (_, _) => changed++;

        rows.RemoveAt(5);
        Assert.Same(rows[3], list.SelectedItem);
        rows.RemoveAt(3);
        Assert.Null(list.SelectedItem);
        Assert.Equal(1, changed);

        list.SelectedItem = rows[0];
        list.ItemsSource = new ObservableCollection<Row>(Enumerable.Range(10, 5).Select(index => new Row(index))); // no equal row
        Assert.Null(list.SelectedItem);
    }

    #endregion

    #region Scrolling and loading

    [Fact]
    public void ScrollToItemAndTheThresholdReachTheItemsLayout()
    {
        var rows = Rows(200);
        var list = List(rows);
        list.Header = new SkUiBox { HeightRequest = 100 };
        list.RemainingItemsThreshold = 5;
        var reached = 0;
        list.RemainingItemsThresholdReached += (_, _) => reached++;
        SkUiTestHelpers.Arrange(list, 300, 500);

        list.ScrollToItem(rows[150], ScrollToPosition.Start, animated: false);
        Assert.Equal(100 + 150 * 50, list.ScrollY);
        Assert.Equal(150, list.FirstVisibleIndex);
        list.ScrollToItem(new Row(-1), ScrollToPosition.Start, animated: false); // not in the list: ignored
        Assert.Equal(150, list.FirstVisibleIndex);

        Assert.Equal(0, reached);
        list.ScrollToIndex(199, ScrollToPosition.End, animated: false);
        Assert.Equal(1, reached);
    }

    #endregion

    #region Pull to refresh

    [Fact]
    public void PullingTheTopPastTheTriggerStartsARefreshEvenOnAShortList()
    {
        var list = List(Rows(3)); // the items do not fill the list, and overscroll is off
        list.IsPullToRefreshEnabled = true;
        var log = new List<string>();
        list.Refreshing += (_, _) => log.Add("refreshing");
        list.RefreshCommand = new Command(parameter => log.Add($"command {parameter}"));
        list.RefreshCommandParameter = "p";
        using var surface = new SkUiTestSurface(list, 300, 500);
        surface.Frame(0);

        // A short pull shows the indicator coming down, but a release before the trigger does nothing.
        Drag(list, new Point(150, 100), new Point(150, 160), release: false);
        Assert.InRange(list.RefreshIndicator.Opacity, 0.05, 0.99);
        Assert.True(list.RefreshIndicator.TranslationY < 0);
        list.Touch(new(_pointer, SkUiTouchAction.Released, new Point(150, 160), TimeSpan.FromSeconds(1)));
        Assert.False(list.IsRefreshing);
        Assert.Empty(log);

        Drag(list, new Point(150, 50), new Point(150, 450));
        Assert.True(list.IsRefreshing);
        Assert.Equal(["refreshing", "command p"], log);
        Assert.Equal(1, list.RefreshIndicator.Opacity);
        Assert.Equal(SkUiCollectionView.RefreshTriggerDistance - 40, list.RefreshIndicator.TranslationY);

        // A pull while refreshing does not start another one; the app ends it once the list has sprung back.
        Drag(list, new Point(150, 50), new Point(150, 450));
        Assert.Equal(2, log.Count);
        for (var frame = 1; frame <= 60; frame++)
            surface.Frame(frame * 16);
        Assert.Equal(1, list.RefreshIndicator.Opacity);
        list.IsRefreshing = false;
        Assert.Equal(0, list.RefreshIndicator.Opacity);
    }

    [Fact]
    public void WithoutPullToRefreshAShortListCannotBePulled()
    {
        var list = List(Rows(3));
        SkUiTestHelpers.Arrange(list, 300, 500);
        Drag(list, new Point(150, 50), new Point(150, 450));
        Assert.False(list.IsRefreshing);
        Assert.Equal(0, list.RefreshIndicator.Opacity);
    }

    [Fact]
    public void SettingIsRefreshingRunsTheCommandAsMauisRefreshView()
    {
        var list = List(Rows(3));
        var ran = 0;
        list.RefreshCommand = new Command(_ => ran++);
        list.IsRefreshing = true;
        Assert.Equal(1, ran);
        Assert.Equal(1, list.RefreshIndicator.Opacity);
    }

    #endregion
}
