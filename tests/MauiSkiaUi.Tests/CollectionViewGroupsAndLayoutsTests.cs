using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// FR-22 <see cref="SkUiCollectionView"/>, Phase B3: grouped lists (group headers and footers, group and item changes),
/// expandable groups, sticky group headers, grids (<see cref="SkUiCollectionView.Span"/>), horizontal lists, multiple
/// selection, and loading more (manual, automatic, at the start).
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class CollectionViewGroupsAndLayoutsTests
{
    private static long _pointer = 220_000;

    private sealed record Row(int Index, double Size = 50);

    /// <summary>A group: the list of its rows, with a name (MAUI's grouped source shape).</summary>
    private sealed class Section(string name, IEnumerable<Row> rows) : ObservableCollection<Row>(rows)
    {
        public string Name { get; } = name;

        public override string ToString() => Name;
    }

    /// <summary>A group that keeps its own expanded state.</summary>
    private sealed class ExpandableSection(string name, IEnumerable<Row> rows, bool expanded) : ObservableCollection<Row>(rows), ISkUiExpandableGroup
    {
        private static readonly PropertyChangedEventArgs IsExpandedChanged = new(nameof(IsExpanded));

        private bool _expanded = expanded;

        public string Name { get; } = name;

        public bool IsExpanded
        {
            get => _expanded;
            set
            {
                if (_expanded == value)
                    return;
                _expanded = value;
                OnPropertyChanged(IsExpandedChanged);
            }
        }
    }

    /// <summary>A row as tall as its item's size (vertical lists).</summary>
    private sealed class RowView : SkUiBox
    {
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is Row row)
                HeightRequest = row.Size;
        }
    }

    /// <summary>A row as wide as its item's size (horizontal lists).</summary>
    private sealed class ColumnView : SkUiBox
    {
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is Row row)
                WidthRequest = row.Size;
        }
    }

    /// <summary>A 30 DIP group header with Expanded / Collapsed visual states.</summary>
    private static DataTemplate HeaderTemplate() => new(() =>
    {
        var header = new SkUiBox { HeightRequest = 30 };
        VisualStateManager.SetVisualStateGroups(header, [new VisualStateGroup
        {
            Name = "ExpandStates",
            States = { new VisualState { Name = "Expanded" }, new VisualState { Name = "Collapsed" } }
        }]);
        return header;
    });

    private static string? ExpandState(ISkUiView? header) =>
        header is null ? null : VisualStateManager.GetVisualStateGroups((VisualElement)header)[0].CurrentState?.Name;

    private static IEnumerable<Row> Rows(int from, int count, double size = 50) => Enumerable.Range(from, count).Select(index => new Row(index, size));

    /// <summary>Three groups of 10 rows (items 0–29), 30 DIP headers, 20 DIP footers.</summary>
    private static (SkUiCollectionView List, ObservableCollection<Section> Sections) Grouped(bool footers = true)
    {
        var sections = new ObservableCollection<Section>
        {
            new("A", Rows(0, 10)),
            new("B", Rows(10, 10)),
            new("C", Rows(20, 10))
        };
        var list = new SkUiCollectionView
        {
            IsGrouped = true,
            ItemsSource = sections,
            ItemTemplate = new DataTemplate(() => new RowView()),
            GroupHeaderTemplate = HeaderTemplate(),
            GroupFooterTemplate = footers ? new DataTemplate(() => new SkUiBox { HeightRequest = 20 }) : null,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Overscroll = SkUiOverscrollMode.None,
            PrefetchBudget = TimeSpan.FromSeconds(10)
        };
        return (list, sections);
    }

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    private static void Drag(SkUiView root, Point from, Point to, int steps = 6)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.FromMilliseconds(0)));
        for (var step = 1; step <= steps; step++)
            root.Touch(new(id, SkUiTouchAction.Moved, new Point(from.X + (to.X - from.X) * step / steps, from.Y + (to.Y - from.Y) * step / steps),
                TimeSpan.FromMilliseconds(step * 100)));
        root.Touch(new(id, SkUiTouchAction.Released, to, TimeSpan.FromMilliseconds(steps * 100 + 300)));
    }

    /// <summary>Where a view shows in the list (scroll offsets of the scrollers on the way subtracted).</summary>
    private static Rect OnScreen(SkUiCollectionView list, View view)
    {
        var x = view.Frame.X;
        var y = view.Frame.Y;
        for (var parent = view.Parent as View; parent is not null && !ReferenceEquals(parent, list); parent = parent.Parent as View)
        {
            x += parent.Frame.X;
            y += parent.Frame.Y;
            if (parent is SkUiScrollView scroller)
            {
                x -= scroller.ScrollX;
                y -= scroller.ScrollY;
            }
        }
        return new Rect(x, y, view.Frame.Width, view.Frame.Height);
    }

    private static Rect ItemOnScreen(SkUiCollectionView list, int index) => OnScreen(list, (View)list.GetRealizedView(index)!);

    private sealed class Command(Action<object?> execute, Func<bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => execute(parameter);
        public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    #region Grouping

    [Fact]
    public void GroupsShowTheirHeaderItemsAndFooterAndItemIndicesCountItemsOnly()
    {
        var (list, sections) = Grouped();
        object? tappedGroup = null;
        var tappedIndex = -1;
        list.ItemTapped += (_, args) => (tappedGroup, tappedIndex) = (args.Group, args.Index);
        SkUiTestHelpers.Arrange(list, 300, 600);

        Assert.Equal(30, list.ItemCount);
        // A: header 0..30, items 30..530, footer 530..550; B's header 550..580, its first item at 580.
        Assert.Equal(new Rect(0, 30, 300, 50), ItemOnScreen(list, 0));
        Assert.Equal(new Rect(0, 580, 300, 50), ItemOnScreen(list, 10));
        Assert.Equal(0, list.FirstVisibleIndex);
        Assert.Equal(10, list.LastVisibleIndex);
        Assert.Equal(3 * 12, list.ItemsLayout.ItemCount); // a header, 10 items and a footer per group

        Tap(list, new Point(150, 595));
        Assert.Same(sections[1], tappedGroup);
        Assert.Equal(10, tappedIndex);

        // Past A's header the first visible item is still A's first, then items of B.
        list.ScrollView.ScrollTo(0, 10);
        Assert.Equal(0, list.FirstVisibleIndex);
        list.ScrollView.ScrollTo(0, 560);
        Assert.Equal(10, list.FirstVisibleIndex);
    }

    [Fact]
    public void ItemAndGroupChangesTouchOnlyTheirRows()
    {
        var (list, sections) = Grouped();
        SkUiTestHelpers.Arrange(list, 300, 600);
        var first = list.GetRealizedView(0);

        sections[0].Insert(1, new Row(100));
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal(31, list.ItemCount);
        Assert.Same(first, list.GetRealizedView(0)); // kept its view
        Assert.Equal(100, ((Row)((BindableObject)list.GetRealizedView(1)!).BindingContext).Index);
        Assert.Equal(new Rect(0, 80, 300, 50), ItemOnScreen(list, 1));

        sections.Insert(0, new Section("Z", Rows(200, 2)));
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal(33, list.ItemCount);
        Assert.Equal(200, ((Row)((BindableObject)list.GetRealizedView(0)!).BindingContext).Index);
        Assert.Equal(new Rect(0, 30, 300, 50), ItemOnScreen(list, 0));
        Assert.Same(first, list.GetRealizedView(2)); // A's first item, after Z's 2 items

        sections.RemoveAt(0);
        sections[0].RemoveAt(1);
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal(30, list.ItemCount);
        Assert.Same(first, list.GetRealizedView(0));
        Assert.Equal(new Rect(0, 30, 300, 50), ItemOnScreen(list, 0));
    }

    [Fact]
    public void NewTemplatesReplaceTheRowsAndKeepNoOldViews()
    {
        var (list, _) = Grouped();
        SkUiTestHelpers.Arrange(list, 300, 600);
        list.ScrollView.ScrollTo(0, 800);
        SkUiTestHelpers.Arrange(list, 300, 600);

        list.GroupHeaderTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = 40, Color = Colors.Red });
        Assert.Equal(0, list.ItemsLayout.PooledViews); // views of the old templates are not kept for reuse
        list.ScrollView.ScrollTo(0, 0);
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal(Colors.Red, ((SkUiBox)HeaderView(list, 0)!).Color);
        Assert.Equal(new Rect(0, 40, 300, 50), ItemOnScreen(list, 0));

        list.ScrollView.ScrollTo(0, 800);
        SkUiTestHelpers.Arrange(list, 300, 600);
        list.ItemTemplate = new DataTemplate(() => new RowView { Color = Colors.Blue });
        Assert.Equal(0, list.ItemsLayout.PooledViews);
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal(Colors.Blue, ((SkUiBox)list.GetRealizedView(list.FirstVisibleIndex)!).Color);
    }

    [Fact]
    public void RemovingAGroupClearsTheSelectionOfItsItems()
    {
        var (list, sections) = Grouped();
        list.SelectionMode = SkUiSelectionMode.Single;
        list.SelectedItem = sections[1][3];
        SkUiTestHelpers.Arrange(list, 300, 600);
        sections.RemoveAt(0);
        Assert.Same(sections[0][3], list.SelectedItem);
        sections.RemoveAt(0);
        Assert.Null(list.SelectedItem);
    }

    #endregion

    #region Expandable groups

    [Fact]
    public void TappingAGroupHeaderCollapsesTheGroupWithoutRealizingItsItems()
    {
        var (list, sections) = Grouped();
        list.AllowGroupExpandCollapse = true;
        var log = new List<string>();
        list.GroupCollapsing += (_, args) => log.Add($"collapsing {((Section)args.Group!).Name}");
        list.GroupCollapsed += (_, args) => log.Add($"collapsed {((Section)args.Group!).Name} {args.GroupIndex}");
        list.GroupExpanded += (_, args) => log.Add($"expanded {((Section)args.Group!).Name}");
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal("Expanded", ExpandState(HeaderView(list, 0)));

        Tap(list, new Point(150, 15)); // A's header
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal(["collapsing A", "collapsed A 0"], log);
        Assert.False(list.IsGroupExpanded(sections[0]));
        Assert.Equal("Collapsed", ExpandState(HeaderView(list, 0)));
        // A's items (and footer) are gone from the layout, not just hidden; the indices still count them.
        Assert.Equal(30, list.ItemCount);
        Assert.Null(list.GetRealizedView(0));
        Assert.Equal(new Rect(0, 60, 300, 50), ItemOnScreen(list, 10)); // A's header, then B's header
        Assert.Equal(10, list.FirstVisibleIndex);

        Tap(list, new Point(150, 15));
        Assert.True(list.IsGroupExpanded(sections[0]));
        Assert.Equal("expanded A", log[^1]);
    }

    private static ISkUiView? HeaderView(SkUiCollectionView list, int rowIndex) =>
        ((SkUiContentView?)list.ItemsLayout.GetRealizedView(rowIndex))?.Content;

    [Fact]
    public void ExpansionCanBeCanceledDrivenFromCodeAndFromTheGroupAndKeepsWhatShows()
    {
        var sections = new ObservableCollection<ExpandableSection>
        {
            new("A", Rows(0, 10), expanded: true),
            new("B", Rows(10, 10), expanded: false),
            new("C", Rows(20, 10), expanded: true)
        };
        var list = new SkUiCollectionView
        {
            IsGrouped = true, ItemsSource = sections, ItemTemplate = new DataTemplate(() => new RowView()),
            GroupHeaderTemplate = HeaderTemplate(), Overscroll = SkUiOverscrollMode.None, PrefetchBudget = TimeSpan.FromSeconds(10)
        };
        SkUiTestHelpers.Arrange(list, 300, 600);
        // B starts collapsed, as the group says.
        Assert.False(list.IsGroupExpanded(sections[1]));
        Assert.Equal(30 + 500 + 30 + 30 + 500, list.ScrollView.ContentSize.Height);

        // Canceled.
        list.GroupExpanding += Cancel;
        Assert.False(list.ExpandGroup(sections[1]));
        Assert.False(sections[1].IsExpanded);
        list.GroupExpanding -= Cancel;

        // From code: the group's own state follows.
        Assert.True(list.ExpandGroup(sections[1]));
        Assert.True(sections[1].IsExpanded);
        SkUiTestHelpers.Arrange(list, 300, 600);

        // Collapsing a group above what shows keeps it in place.
        list.ScrollView.ScrollTo(0, 30 + 500 + 30 + 250); // B's 6th item at the top
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal(0, ItemOnScreen(list, 15).Y, 1);
        list.CollapseGroup(sections[0]);
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal(0, ItemOnScreen(list, 15).Y, 1);

        // From the group: the list follows.
        sections[2].IsExpanded = false;
        Assert.False(list.IsGroupExpanded(sections[2]));

        // Scrolling to an item of a collapsed group expands it.
        list.ScrollToIndex(3, ScrollToPosition.Start, animated: false);
        Assert.True(sections[0].IsExpanded);
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal(0, ItemOnScreen(list, 3).Y, 1);

        static void Cancel(object? sender, SkUiGroupChangingEventArgs args) => args.Cancel = true;
    }

    [Fact]
    public void GroupsStartCollapsedWithoutAutoExpandAndExpandAllShowsThem()
    {
        var (list, sections) = Grouped(footers: false);
        list.AutoExpandGroups = false;
        list.ItemsSource = new ObservableCollection<Section>(sections); // read again with the setting
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal(3 * 30, list.ScrollView.ContentSize.Height);
        Assert.Equal(3, list.ItemsLayout.ItemCount); // the headers
        list.ExpandAll();
        Assert.Equal(3 + 30, list.ItemsLayout.ItemCount);
        list.CollapseAll();
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal(3 * 30, list.ScrollView.ContentSize.Height);
    }

    #endregion

    #region Sticky group headers

    [Fact]
    public void TheCurrentGroupsHeaderStaysAtTheStartAndIsPushedByTheNextOne()
    {
        var (list, sections) = Grouped(footers: false);
        sections.Add(new Section("D", Rows(30, 10))); // long enough to bring any header to the start
        list.IsStickyGroupHeader = true;
        list.AllowGroupExpandCollapse = true;
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal(-1, list.StickyGroupShown); // A's own header shows

        list.ScrollView.ScrollTo(0, 200);
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal(0, list.StickyGroupShown);
        Assert.Same(sections[0], ((SkUiContentView)((SkUiContentView)list.StickyGroupHost!).Content!).BindingContext);
        Assert.Equal(new Rect(0, 0, 300, 30), list.StickyGroupHost!.Frame);
        Assert.Equal(0, list.StickyGroupHost.TranslationY);

        // B's header (at 530) comes within 30 DIPs of the start: A's is pushed up by the overlap.
        list.ScrollView.ScrollTo(0, 510);
        Assert.Equal(0, list.StickyGroupShown);
        Assert.Equal(-10, list.StickyGroupHost.TranslationY, 1);

        list.ScrollView.ScrollTo(0, 600);
        Assert.Equal(1, list.StickyGroupShown);
        Assert.Equal(0, list.StickyGroupHost.TranslationY);

        // Scrolling to an item places it below the sticky header.
        list.ScrollToIndex(15, ScrollToPosition.Start, animated: false);
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.Equal(30, ItemOnScreen(list, 15).Y, 1);

        // A tap on the sticky header collapses its group, whose header comes to the start.
        Tap(list, new Point(150, 15));
        SkUiTestHelpers.Arrange(list, 300, 600);
        Assert.False(list.IsGroupExpanded(sections[1]));
        Assert.Equal(-1, list.StickyGroupShown);
        Assert.Equal(0, OnScreen(list, (View)list.ItemsLayout.GetRealizedView(sections[0].Count + 1)!).Y, 1);
    }

    #endregion

    #region Grid

    [Fact]
    public void ItemsShareRowsInAGridAsTallAsTheirTallestItem()
    {
        var rows = new ObservableCollection<Row>(Rows(0, 10).Select(row => row with { Size = 40 + row.Index % 3 * 10 }));
        var list = new SkUiCollectionView
        {
            ItemsSource = rows, ItemTemplate = new DataTemplate(() => new RowView()),
            Span = 3, SpanSpacing = 6, ItemSpacing = 4, PrefetchBudget = TimeSpan.FromSeconds(10), Overscroll = SkUiOverscrollMode.None
        };
        list.SelectionMode = SkUiSelectionMode.Single;
        var tapped = -1;
        list.ItemTapped += (_, args) => tapped = args.Index;
        SkUiTestHelpers.Arrange(list, 306, 600);

        // (306 − 2 · 6) / 3 = 98 DIPs per cell.
        Assert.Equal(new Rect(0, 0, 98, 60), OnScreen(list, ((SkUiView)list.GetRealizedView(0)!.Parent!)));
        Assert.Equal(new Rect(104, 0, 98, 60), OnScreen(list, ((SkUiView)list.GetRealizedView(1)!.Parent!)).Round());
        Assert.Equal(new Rect(208, 64, 98, 60), OnScreen(list, ((SkUiView)list.GetRealizedView(5)!.Parent!)).Round());
        Assert.Equal(3 * 60 + 40 + 3 * 4, list.ScrollView.ContentSize.Height); // 4 rows, the last one with one 40 DIP item
        Assert.Equal(0, list.FirstVisibleIndex);
        Assert.Equal(9, list.LastVisibleIndex);

        Tap(list, new Point(250, 90)); // row 1, column 2: item 5
        Assert.Equal(5, tapped);
        Assert.Same(rows[5], list.SelectedItem);

        // An insert moves the items after it to the next cells.
        rows.Insert(0, new Row(100, 40));
        SkUiTestHelpers.Arrange(list, 306, 600);
        Assert.Equal(100, ((Row)((BindableObject)list.GetRealizedView(0)!).BindingContext).Index);
        Assert.Equal(0, ((Row)((BindableObject)list.GetRealizedView(1)!).BindingContext).Index);
        Assert.Equal(11, list.ItemCount);
        Assert.True(((SkUiCollectionView)list).ItemsLayout.ItemCount == 4);
    }

    [Fact]
    public void GridCellsFollowTheirItemsTemplates()
    {
        var created = new Dictionary<string, int> { ["even"] = 0, ["odd"] = 0 };
        var selector = new ParitySelector(
            new DataTemplate(() => { created["even"]++; return new RowView { Color = Colors.Red }; }),
            new DataTemplate(() => { created["odd"]++; return new RowView { Color = Colors.Blue }; }));
        var rows = new ObservableCollection<Row>(Rows(0, 6));
        var list = new SkUiCollectionView { ItemsSource = rows, ItemTemplate = selector, Span = 2, PrefetchBudget = TimeSpan.FromSeconds(10) };
        SkUiTestHelpers.Arrange(list, 200, 600);
        Assert.Equal(Colors.Red, ((SkUiBox)list.GetRealizedView(0)!).Color);
        Assert.Equal(Colors.Blue, ((SkUiBox)list.GetRealizedView(1)!).Color);

        // Shifted by one: every cell now needs the other template; cells are swapped, not re-created beyond the first.
        var total = created["even"] + created["odd"];
        rows.Insert(0, new Row(1001));
        SkUiTestHelpers.Arrange(list, 200, 600);
        Assert.Equal(Colors.Blue, ((SkUiBox)list.GetRealizedView(0)!).Color);
        Assert.Equal(Colors.Red, ((SkUiBox)list.GetRealizedView(1)!).Color);
        Assert.InRange(created["even"] + created["odd"] - total, 0, 2);
    }

    private sealed class ParitySelector(DataTemplate even, DataTemplate odd) : DataTemplateSelector
    {
        protected override DataTemplate OnSelectTemplate(object item, BindableObject container) => ((Row)item).Index % 2 == 0 ? even : odd;
    }

    [Fact]
    public void GroupedGridsStartEachGroupOnANewRow()
    {
        var (list, sections) = Grouped(footers: false);
        list.Span = 4;
        SkUiTestHelpers.Arrange(list, 400, 600);
        // A: header, 3 rows (4 + 4 + 2 items); B's header after them.
        Assert.Equal(new Rect(0, 30, 100, 50), OnScreen(list, (View)list.GetRealizedView(0)!.Parent!));
        Assert.Equal(new Rect(0, 30 + 150 + 30, 100, 50), OnScreen(list, (View)list.GetRealizedView(10)!.Parent!));
        Assert.Equal(3 * 30 + 9 * 50, list.ScrollView.ContentSize.Height);
    }

    #endregion

    #region Horizontal

    [Fact]
    public void AHorizontalListLaysItsItemsAlongXAndScrollsThem()
    {
        var list = new SkUiCollectionView
        {
            Orientation = ItemsLayoutOrientation.Horizontal,
            ItemsSource = Rows(0, 1000, 80).ToList(),
            ItemTemplate = new DataTemplate(() => new ColumnView()),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Overscroll = SkUiOverscrollMode.None,
            PrefetchBudget = TimeSpan.FromSeconds(10)
        };
        list.SetHeader(new SkUiBox { WidthRequest = 40 }, sticky: true);
        SkUiTestHelpers.Arrange(list, 400, 100);

        Assert.Equal(new Rect(40, 0, 80, 100), ItemOnScreen(list, 0));
        Assert.Equal(new Rect(120, 0, 80, 100), ItemOnScreen(list, 1));
        Assert.Equal(40 + 1000 * 80, list.ScrollView.ContentSize.Width);
        Assert.Equal(4, list.LastVisibleIndex);
        var (_, last) = list.ItemsLayout.RealizedRange;
        Assert.InRange(last, 4, 20); // only near the viewport

        list.ScrollToIndex(500, ScrollToPosition.Start, animated: false);
        Assert.Equal(40, ItemOnScreen(list, 500).X, 1); // after the sticky header
        Assert.Equal(499, list.FirstVisibleIndex); // behind the sticky header: it shows through
    }

    [Fact]
    public void RightToLeftHorizontalListsStartAtTheRight()
    {
        var list = new SkUiCollectionView
        {
            Orientation = ItemsLayoutOrientation.Horizontal,
            FlowDirection = FlowDirection.RightToLeft,
            ItemsSource = Rows(0, 100, 80).ToList(),
            ItemTemplate = new DataTemplate(() => new ColumnView()),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Overscroll = SkUiOverscrollMode.None,
            PrefetchBudget = TimeSpan.FromSeconds(10)
        };
        SkUiTestHelpers.Arrange(list, 400, 100);
        SkUiTestHelpers.Arrange(list, 400, 100);
        Assert.Equal(new Rect(320, 0, 80, 100), ItemOnScreen(list, 0));
        Assert.Equal(new Rect(240, 0, 80, 100), ItemOnScreen(list, 1));
        Assert.Equal(0, list.FirstVisibleIndex);

        list.ScrollToIndex(50, ScrollToPosition.Start, animated: false);
        SkUiTestHelpers.Arrange(list, 400, 100);
        Assert.Equal(400, ItemOnScreen(list, 50).Right, 1);
        Assert.Equal(50, list.FirstVisibleIndex);
    }

    #endregion

    #region Multiple selection

    [Fact]
    public void TapsToggleItemsInAMultipleSelection()
    {
        var rows = new ObservableCollection<Row>(Rows(0, 20));
        var list = new SkUiCollectionView
        {
            ItemsSource = rows, ItemTemplate = new DataTemplate(() => new RowView()),
            SelectionMode = SkUiSelectionMode.Multiple, PrefetchBudget = TimeSpan.FromSeconds(10)
        };
        var changes = new List<(int Before, int After)>();
        list.SelectionChanged += (_, args) => changes.Add((args.PreviousSelection.Count, args.CurrentSelection.Count));
        SkUiTestHelpers.Arrange(list, 300, 500);
        bool Selected(int index) => ((SkUiView)list.GetRealizedView(index)!).IsSelectedItem;

        Tap(list, new Point(100, 25));
        Tap(list, new Point(100, 125));
        Assert.Equal([rows[0], rows[2]], list.SelectedItems);
        Assert.True(Selected(0) && Selected(2) && !Selected(1));
        Tap(list, new Point(100, 25));
        Assert.Equal([rows[2]], list.SelectedItems);
        Assert.Equal([(0, 1), (1, 2), (2, 1)], changes);

        // Canceled.
        list.SelectionChanging += Cancel;
        Tap(list, new Point(100, 75));
        Assert.Equal([rows[2]], list.SelectedItems);
        list.SelectionChanging -= Cancel;

        // Changes made to the list show.
        list.SelectedItems.Add(rows[3]);
        Assert.True(Selected(3));
        Assert.Equal((1, 2), changes[^1]);

        // One change each for SelectAll and ClearSelection.
        changes.Clear();
        list.SelectAll();
        Assert.Equal(20, list.SelectedItems.Count);
        list.ClearSelection();
        Assert.Empty(list.SelectedItems);
        Assert.Equal([(2, 20), (20, 0)], changes);

        // Removed items leave the selection; another mode clears it.
        list.SelectedItems.Add(rows[5]);
        list.SelectedItems.Add(rows[6]);
        rows.RemoveAt(5);
        Assert.Equal([rows[5]], list.SelectedItems); // the former item 6
        list.SelectionMode = SkUiSelectionMode.Single;
        Assert.Empty(list.SelectedItems);

        static void Cancel(object? sender, SkUiSelectionChangingEventArgs args) => args.Cancel = true;
    }

    [Fact]
    public void AnAppListSetAsSelectedItemsIsFollowed()
    {
        var rows = Rows(0, 5).ToList();
        var mine = new ObservableCollection<object> { rows[1] };
        var list = new SkUiCollectionView { ItemsSource = rows, SelectionMode = SkUiSelectionMode.Multiple, SelectedItems = mine };
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.True(((SkUiView)list.GetRealizedView(1)!).IsSelectedItem);
        mine.Add(rows[4]);
        Assert.True(((SkUiView)list.GetRealizedView(4)!).IsSelectedItem);
    }

    #endregion

    #region Keeping the selection visible

    private static SkUiCollectionView KeepingList(IEnumerable<Row> rows, SkUiSelectionMode mode) => new()
    {
        ItemsSource = rows.ToList(), ItemTemplate = new DataTemplate(() => new RowView()), SelectionMode = mode,
        KeepSelectionVisible = true, Overscroll = SkUiOverscrollMode.None, PrefetchBudget = TimeSpan.FromSeconds(10)
    };

    /// <summary>Runs render frames until animated scrolls (selection reveals are animated) have landed.</summary>
    private static void Settle(SkUiTestSurface surface, ref double time)
    {
        for (var frame = 0; frame < 60; frame++)
            surface.Frame(time += 16);
    }

    private static bool FullyShown(SkUiCollectionView list, int index, double height) =>
        list.GetRealizedView(index) is View view && OnScreen(list, (View)view.Parent!) is var frame && frame.Y >= -0.5 && frame.Bottom <= height + 0.5;

    [Fact]
    public void ASingleSelectionScrollsIntoViewAndStaysThereWhenTheListShrinks()
    {
        var rows = Rows(0, 100).ToList();
        var list = KeepingList(rows, SkUiSelectionMode.Single);
        using var surface = new SkUiTestSurface(list, 300, 500);
        var time = 0d;
        Settle(surface, ref time);

        list.SelectedItem = rows[60]; // from the app: scrolled to (animated), only as far as needed
        Settle(surface, ref time);
        Assert.True(FullyShown(list, 60, 500));
        Assert.Equal(60 * 50 + 50 - 500, list.ScrollY, 1);

        // A smaller list (a rotation): the item stays in view, at once.
        SkUiTestHelpers.Arrange(list, 500, 200);
        Assert.True(FullyShown(list, 60, 200));

        // Off: nothing moves.
        list.KeepSelectionVisible = false;
        list.SelectedItem = rows[5];
        Settle(surface, ref time);
        Assert.False(FullyShown(list, 5, 200));
    }

    [Fact]
    public void ASelectionSetBeforeTheFirstLayoutShowsOnceLaidOut()
    {
        var rows = Rows(0, 100).ToList();
        var list = KeepingList(rows, SkUiSelectionMode.Single);
        list.SelectedItem = rows[40];
        SkUiTestHelpers.Arrange(list, 300, 500);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.True(FullyShown(list, 40, 500));
    }

    [Fact]
    public void AMultipleSelectionFromTheAppShowsFromItsStartWithAsManyAsFit()
    {
        var rows = Rows(0, 100).ToList();
        var list = KeepingList(rows, SkUiSelectionMode.Multiple);
        using var surface = new SkUiTestSurface(list, 300, 500);
        var time = 0d;
        Settle(surface, ref time);

        // Items 30, 33, 36 fit in one screen with 30 first; 80 does not.
        foreach (var index in new[] { 80, 30, 36, 33 })
            list.SelectedItems.Add(rows[index]);
        Settle(surface, ref time);
        // Each change scrolled only as far as needed: all three show (the last addition, 36, at the end).
        Assert.True(FullyShown(list, 30, 500) && FullyShown(list, 33, 500) && FullyShown(list, 36, 500));
        Assert.False(FullyShown(list, 80, 500));

        // Already showing as many: the next change does not move the list.
        var offset = list.ScrollY;
        list.SelectedItems.Add(rows[34]);
        Settle(surface, ref time);
        Assert.Equal(offset, list.ScrollY, 1);

        // Set in one change (SelectAll-like replacement from the app): it shows from its start.
        list.ScrollToAsync(0, animated: false);
        list.ClearSelection();
        foreach (var index in new[] { 60, 62 })
            list.SelectedItems.Add(rows[index]);
        Settle(surface, ref time);
        Assert.True(FullyShown(list, 60, 500) && FullyShown(list, 62, 500));
    }

    [Fact]
    public void AfterAResizeTheTappedItemStaysInViewWithAsManySelectedItemsAsFitAroundIt()
    {
        var rows = Rows(0, 100).ToList();
        var list = KeepingList(rows, SkUiSelectionMode.Multiple);
        using var surface = new SkUiTestSurface(list, 300, 500);
        var time = 0d;
        Settle(surface, ref time);
        list.KeepSelectionVisible = false;
        list.SelectedItems.Add(rows[2]);
        list.SelectedItems.Add(rows[24]);
        list.ScrollToIndex(20, ScrollToPosition.Start, animated: false);
        list.KeepSelectionVisible = true; // 2 and 24 do not fit together: the selection's start (2) shows
        Settle(surface, ref time);
        Assert.True(FullyShown(list, 2, 500));
        list.ScrollToIndex(20, ScrollToPosition.Start, animated: false);
        Settle(surface, ref time);

        // A tap on 27 selects it: it shows already, so nothing moves under the finger.
        Tap(list, new Point(150, 7 * 50 + 25));
        Settle(surface, ref time);
        Assert.Contains(rows[27], list.SelectedItems);
        Assert.Equal(20 * 50, list.ScrollY, 1);

        // A short list: the tapped item stays in view, with 24 (both fit), not 2.
        SkUiTestHelpers.Arrange(list, 300, 200);
        Assert.True(FullyShown(list, 27, 200));
        Assert.True(FullyShown(list, 24, 200));
    }

    [Fact]
    public void RemovingSelectedItemsDoesNotScroll()
    {
        var rows = new ObservableCollection<Row>(Rows(0, 100));
        var list = new SkUiCollectionView
        {
            ItemsSource = rows, ItemTemplate = new DataTemplate(() => new RowView()), SelectionMode = SkUiSelectionMode.Multiple,
            Overscroll = SkUiOverscrollMode.None, PrefetchBudget = TimeSpan.FromSeconds(10)
        };
        using var surface = new SkUiTestSurface(list, 300, 500);
        var time = 0d;
        list.SelectedItems.Add(rows[1]);
        list.SelectedItems.Add(rows[90]);
        list.ScrollToIndex(50, ScrollToPosition.Start, animated: false);
        list.KeepSelectionVisible = true;
        list.ScrollToIndex(50, ScrollToPosition.Start, animated: false);
        Settle(surface, ref time);
        var offset = list.ScrollY;
        rows.RemoveAt(90);
        Settle(surface, ref time);
        Assert.Equal(offset, list.ScrollY, 1);
    }

    #endregion

    #region Load more

    [Fact]
    public void ManualLoadMoreShowsAButtonRowWhileMoreCanLoad()
    {
        var rows = new ObservableCollection<Row>(Rows(0, 3));
        var more = true;
        Command? command = null;
        command = new Command(_ => { }, () => more);
        var list = new SkUiCollectionView
        {
            ItemsSource = rows, ItemTemplate = new DataTemplate(() => new RowView()),
            LoadMoreMode = SkUiLoadMoreMode.Manual, LoadMoreCommand = command, Overscroll = SkUiOverscrollMode.None
        };
        var loads = 0;
        list.LoadingMore += (_, _) => loads++;
        SkUiTestHelpers.Arrange(list, 300, 500);
        var row = list.LoadMoreRow!;
        Assert.True(row.IsVisible);
        Assert.Equal(150, OnScreen(list, row).Y); // after the items

        Tap(list, new Point(150, 150 + row.Height / 2)); // the button
        Assert.Equal(1, loads);
        Assert.True(list.IsLoadMoreActive);
        Tap(list, new Point(150, 150 + row.Height / 2)); // loading: nothing
        Assert.Equal(1, loads);

        rows.Add(new Row(3));
        list.IsLoadMoreActive = false;
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(200, OnScreen(list, row).Y);

        more = false;
        command.Raise();
        Assert.False(row.IsVisible);
    }

    [Fact]
    public void AutomaticLoadMoreRunsWhenTheEndShowsAndOnUserScrollOnlyAfterAScroll()
    {
        var rows = new ObservableCollection<Row>(Rows(0, 20));
        var loads = 0;
        var list = new SkUiCollectionView
        {
            ItemsSource = rows, ItemTemplate = new DataTemplate(() => new RowView()),
            LoadMoreMode = SkUiLoadMoreMode.AutoOnUserScroll, LoadMoreCommand = new Command(_ => loads++),
            Overscroll = SkUiOverscrollMode.None, PrefetchBudget = TimeSpan.FromSeconds(10)
        };
        SkUiTestHelpers.Arrange(list, 300, 500);
        list.ScrollToAsync(10_000, animated: false); // programmatic: not the user
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(0, loads);
        list.ScrollToAsync(300, animated: false);
        SkUiTestHelpers.Arrange(list, 300, 500);

        Drag(list, new Point(150, 450), new Point(150, 50)); // to the end
        Assert.Equal(1, loads);
        Assert.True(list.IsLoadMoreActive);

        // Auto: also when the items do not fill the list; again once the app ended the load and the end still shows.
        var shortRows = new ObservableCollection<Row>(Rows(0, 2));
        var autoLoads = 0;
        var auto = new SkUiCollectionView
        {
            ItemsSource = shortRows, ItemTemplate = new DataTemplate(() => new RowView()),
            LoadMoreMode = SkUiLoadMoreMode.Auto, LoadMoreCommand = new Command(_ => autoLoads++)
        };
        SkUiTestHelpers.Arrange(auto, 300, 500);
        Assert.Equal(1, autoLoads);
        shortRows.Add(new Row(2));
        auto.IsLoadMoreActive = false;
        SkUiTestHelpers.Arrange(auto, 300, 500);
        Assert.Equal(2, autoLoads);
    }

    [Fact]
    public void LoadingMoreAtTheStartKeepsWhatShows()
    {
        var rows = new ObservableCollection<Row>(Rows(100, 20));
        var list = new SkUiCollectionView
        {
            ItemsSource = rows, ItemTemplate = new DataTemplate(() => new RowView()),
            LoadMoreMode = SkUiLoadMoreMode.Manual, LoadMorePosition = SkUiLoadMorePosition.Start,
            LoadMoreCommand = new Command(_ => { }), Overscroll = SkUiOverscrollMode.None, PrefetchBudget = TimeSpan.FromSeconds(10)
        };
        SkUiTestHelpers.Arrange(list, 300, 500);
        var row = list.LoadMoreRow!;
        Assert.Equal(0, OnScreen(list, row).Y); // before the items
        var first = ItemOnScreen(list, 0).Y;
        Assert.Equal(row.Height, first);

        for (var index = 0; index < 10; index++)
            rows.Insert(0, new Row(99 - index));
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(first, ItemOnScreen(list, 10).Y, 1); // the former first item stayed
        Assert.Equal(10 * 50, list.ScrollY, 1);
    }

    #endregion
}

internal static class RectRounding
{
    public static Rect Round(this Rect rect) => new(Math.Round(rect.X), Math.Round(rect.Y), Math.Round(rect.Width), Math.Round(rect.Height));
}
