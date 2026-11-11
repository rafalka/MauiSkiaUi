using System.Collections.ObjectModel;
using System.Windows.Input;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.10 C1: keyboard item navigation of <see cref="SkUiCollectionView"/>. The list is one tab stop with a current item that
/// the arrow keys, Home / End and the page keys move (lists and grids of both orientations, right to left, group headers as
/// stops), Space / Enter activate as a tap would, and the focus ring is drawn on the current item's container while focus
/// came from the keyboard. In the global collection: whether the keyboard or a pointer was used last is per thread and
/// read by every focus test.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class CollectionViewKeyboardTests
{
    private static long _pointer = 520_000;

    private sealed record Row(int Index, double Size = 50);

    /// <summary>A group: the list of its rows, with a name.</summary>
    private sealed class Section(string name, IEnumerable<Row> rows) : ObservableCollection<Row>(rows)
    {
        public string Name { get; } = name;
    }

    /// <summary>A row as long as its item's size along the list.</summary>
    private sealed class RowView : SkUiBox
    {
        public bool Horizontal { get; init; }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not Row row)
                return;
            if (Horizontal)
                WidthRequest = row.Size;
            else
                HeightRequest = row.Size;
        }
    }

    private sealed class Command(Action<object?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
    }

    private static ObservableCollection<Row> Rows(int count) => new(Enumerable.Range(0, count).Select(index => new Row(index)));

    private static SkUiCollectionView List(IEnumerable<object>? source, SkUiSelectionMode mode = SkUiSelectionMode.Single, bool horizontal = false) => new()
    {
        ItemsSource = source,
        ItemTemplate = new DataTemplate(() => new RowView { Horizontal = horizontal }),
        SelectionMode = mode,
        Orientation = horizontal ? ItemsLayoutOrientation.Horizontal : ItemsLayoutOrientation.Vertical,
        VerticalScrollBarVisibility = ScrollBarVisibility.Never,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
        Overscroll = SkUiOverscrollMode.None,
        PrefetchBudget = TimeSpan.FromSeconds(10)
    };

    /// <summary>The list laid out as a surface root, focused from the keyboard.</summary>
    private static SkUiCollectionView Focused(SkUiCollectionView list, double width = 300, double height = 500)
    {
        SkUiTestHelpers.Arrange(list, width, height);
        UseKeyboard(list);
        Assert.True(list.Focus());
        return list;
    }

    /// <summary>The keyboard was used last (focus rings show).</summary>
    private static void UseKeyboard(SkUiView root) => root.FocusManager.KeyDown(SkUiKey.None);

    private static bool Key(SkUiView root, SkUiKey key, bool repeat = false) => root.FocusManager.KeyDown(key, isRepeat: repeat);

    private static void Keys(SkUiView root, SkUiKey key, int times)
    {
        for (var press = 0; press < times; press++)
            Key(root, key);
    }

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    /// <summary>The container of the realized item at <paramref name="index"/>.</summary>
    private static SkUiView Host(SkUiCollectionView list, int index) => (SkUiView)((Element)list.GetRealizedView(index)!).Parent;

    /// <summary>Asserts the item at <paramref name="index"/> is current and its container alone draws the ring.</summary>
    private static void AssertCurrent(SkUiCollectionView list, int index)
    {
        Assert.Equal(index, list.CurrentItemIndex);
        Assert.Same(Host(list, index), list.FocusRingHost);
        Assert.True(Host(list, index).IsFocusRingVisible);
        Assert.Equal(1, RingCount(list));
    }

    /// <summary>The realized containers drawing a ring (item containers and group headers, also inside grid rows).</summary>
    private static int RingCount(SkUiCollectionView list)
    {
        var count = 0;
        foreach (var view in list.RowsLayout.RealizedViews)
        {
            if (view is SkUiView { IsFocusRingVisible: true })
                count++;
            if (view.GetType().Name == "GridRowPart")
                count += ((SkUiView)view).SkiaChildren.Count(cell => cell is SkUiView { IsFocusRingVisible: true });
        }
        return count;
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

    private static bool Shows(SkUiCollectionView list, int index, double top, double bottom) =>
        list.GetRealizedView(index) is View view && OnScreen(list, (View)view.Parent!) is var frame && frame.Y >= top - 0.5 && frame.Bottom <= bottom + 0.5;

    [Fact]
    public void TheListIsOneTabStopAndTheArrowsHomeAndEndMoveItsCurrentItem()
    {
        var list = List(Rows(100));
        var before = new SkUiButton { Text = "Before" };
        var after = new SkUiButton { Text = "After" };
        var root = new SkUiGrid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)] };
        root.Add(before);
        root.Add(list, 0, 1);
        root.Add(after, 0, 2);
        SkUiTestHelpers.Arrange(root, 300, 600);

        Assert.Equal<object>([before, list, after], root.FocusManager.GetTabOrder());
        // Without taps (no selection, no tap handlers) the list is not interactive and takes no focus, as a plain container.
        list.SelectionMode = SkUiSelectionMode.None;
        Assert.Equal<object>([before, after], root.FocusManager.GetTabOrder());
        list.ItemTapped += (_, _) => { };
        Assert.Equal<object>([before, list, after], root.FocusManager.GetTabOrder());

        // Tab in: the first item in view is current and shows the ring.
        UseKeyboard(root);
        Assert.True(before.Focus());
        Assert.True(Key(root, SkUiKey.Tab));
        Assert.True(list.IsFocused);
        AssertCurrent(list, 0);

        Keys(root, SkUiKey.Down, 3);
        AssertCurrent(list, 3);
        Assert.True(Key(root, SkUiKey.Up));
        AssertCurrent(list, 2);
        Assert.False(Key(root, SkUiKey.Left)); // nothing across a one-column list: the key goes on (to the scrollers)
        Assert.True(Key(root, SkUiKey.End));
        AssertCurrent(list, 99);
        Assert.True(Shows(list, 99, 0, list.Height));
        Assert.True(Key(root, SkUiKey.Home));
        AssertCurrent(list, 0);
        Assert.Equal(0, list.ScrollY, 1);
        Assert.False(Key(root, SkUiKey.Up)); // at the start the key goes on

        // Tab moves on out of the list; Shift+Tab comes back to the item it left.
        Keys(root, SkUiKey.Down, 5);
        Assert.True(Key(root, SkUiKey.Tab));
        Assert.True(after.IsFocused);
        Assert.Null(list.FocusRingHost);
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Tab, SkUiKeyModifiers.Shift));
        AssertCurrent(list, 5);
    }

    [Fact]
    public void ThePageKeysMoveByThePartTheStickyPartsLeaveUncovered()
    {
        var list = List(Rows(200));
        list.SetHeader(new SkUiBox { HeightRequest = 50 }, sticky: true);
        Focused(list); // 500 high, 450 uncovered under the header: 9 rows of 50

        Assert.True(Key(list, SkUiKey.PageDown));
        AssertCurrent(list, 8); // the last row that ends within a band from the current row's start: nothing scrolls
        Assert.Equal(0, list.ScrollY, 1);
        Assert.True(Key(list, SkUiKey.PageDown));
        AssertCurrent(list, 16);
        Assert.True(Shows(list, 16, 50, 500));
        Assert.True(Shows(list, 8, 50, 500)); // the band moved by a page: the row it left is still in view at the top
        Assert.True(Key(list, SkUiKey.PageUp));
        AssertCurrent(list, 8);
        Assert.True(Key(list, SkUiKey.PageUp));
        AssertCurrent(list, 0);
        Assert.True(Shows(list, 0, 50, 500));
        Assert.False(Key(list, SkUiKey.PageUp));

        Assert.True(Key(list, SkUiKey.End));
        Assert.True(Shows(list, 199, 50, 500));
        Assert.False(Key(list, SkUiKey.PageDown));
    }

    [Fact]
    public void SpaceAndEnterSelectAndTapTheCurrentItemAsATapWould()
    {
        var rows = Rows(20);
        var list = List(rows);
        var events = new List<string>();
        var cancel = true;
        list.SelectionChanging += (_, args) =>
        {
            events.Add($"changing {((Row?)args.CurrentItem)?.Index}");
            args.Cancel = cancel;
        };
        list.SelectionChanged += (_, _) => events.Add($"changed {((Row?)list.SelectedItem)?.Index}");
        list.ItemTapped += (_, args) => events.Add($"tapped {args.Index}");
        list.ItemTappedCommand = new Command(parameter => events.Add($"command {((Row)parameter!).Index}"));
        Focused(list);

        Key(list, SkUiKey.Down);
        Assert.True(Key(list, SkUiKey.Space));
        Assert.Equal(["changing 1", "tapped 1", "command 1"], events); // cancelled: still tapped, as a tap
        Assert.Null(list.SelectedItem);

        events.Clear();
        cancel = false;
        Assert.True(Key(list, SkUiKey.Enter));
        Assert.True(Key(list, SkUiKey.Enter, repeat: true)); // a held key does not activate again
        Assert.Equal(["changing 1", "changed 1", "tapped 1", "command 1"], events);
        Assert.Same(rows[1], list.SelectedItem);

        // Multiple: Space toggles the current item.
        list.SelectionMode = SkUiSelectionMode.Multiple;
        Key(list, SkUiKey.Space);
        Key(list, SkUiKey.Down);
        Key(list, SkUiKey.Space);
        Assert.Equal([rows[1], rows[2]], list.SelectedItems);
        Key(list, SkUiKey.Space);
        Assert.Equal([rows[1]], list.SelectedItems);
    }

    [Fact]
    public void FocusArrivingStartsAtTheSelectedItemAndShowsIt()
    {
        var rows = Rows(100);
        var list = List(rows);
        list.SelectedItem = rows[40];
        Focused(list);
        AssertCurrent(list, 40);
        Assert.True(Shows(list, 40, 0, 500));

        // Multiple: the item a tap selected last.
        var multiple = List(rows, SkUiSelectionMode.Multiple);
        SkUiTestHelpers.Arrange(multiple, 300, 500);
        SkUiFocusManager.NotePointerInput();
        Tap(multiple, new Point(150, 125)); // item 2
        Tap(multiple, new Point(150, 225)); // item 4
        Assert.Equal(4, multiple.CurrentItemIndex); // a tap makes its item current, also without focus
        Tap(multiple, new Point(150, 75)); // item 1
        multiple.ScrollView.ScrollTo(0, 2000);
        UseKeyboard(multiple);
        Assert.True(multiple.Focus());
        AssertCurrent(multiple, 1);
    }

    [Fact]
    public void TheRingShowsOnlyWhileFocusCameFromTheKeyboard()
    {
        var list = List(Rows(30));
        SkUiTestHelpers.Arrange(list, 300, 500);
        SkUiFocusManager.NotePointerInput();
        Assert.True(list.Focus());
        Assert.Equal(0, list.CurrentItemIndex);
        Assert.Null(list.FocusRingHost);
        Assert.Equal(0, RingCount(list));
        Assert.False(list.IsFocusRingVisible); // not on the list either

        Key(list, SkUiKey.Down);
        AssertCurrent(list, 1);
        SkUiFocusManager.NotePointerInput(); // a pointer press hides it, the item stays current
        Assert.Equal(0, RingCount(list));
        Assert.Equal(1, list.CurrentItemIndex);
        Key(list, SkUiKey.Down);
        AssertCurrent(list, 2);
        list.Unfocus();
        Assert.Equal(0, RingCount(list));

        // A press on Windows focuses the list and makes the pressed item current, without the ring.
        list.FocusManager.PointerPressFocuses = true;
        var id = ++_pointer;
        list.Touch(new(id, SkUiTouchAction.Pressed, new Point(150, 325), TimeSpan.FromSeconds(1)));
        Assert.True(list.IsFocused);
        Assert.Equal(6, list.CurrentItemIndex);
        Assert.Equal(0, RingCount(list));
        list.Touch(new(id, SkUiTouchAction.Released, new Point(150, 325), TimeSpan.FromSeconds(1.02)));
        Key(list, SkUiKey.Down);
        AssertCurrent(list, 7);

        // A UI Automation client focusing an item (it focuses before it acts) focuses the list with that item current.
        list.Unfocus();
        Assert.True(list.FocusManager.FocusOrOwner((ISkUiAccessibleNode)Host(list, 3)));
        Assert.True(list.IsFocused);
        AssertCurrent(list, 3);
    }

    [Fact]
    public void AnEmptyListDrawsTheRingOnItself()
    {
        var rows = new ObservableCollection<Row>();
        var list = Focused(List(rows));
        Assert.True(list.IsFocusRingVisible);
        Assert.False(Key(list, SkUiKey.Down));

        rows.Add(new Row(0));
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.False(list.IsFocusRingVisible);
        Assert.True(Key(list, SkUiKey.Down)); // the first key picks the first item
        AssertCurrent(list, 0);
    }

    [Fact]
    public void GridsMoveAcrossAndAlongTheirRowsKeepingTheColumn()
    {
        var list = List(Rows(10));
        list.Span = 3; // rows [0 1 2] [3 4 5] [6 7 8] [9]
        Focused(list);
        AssertCurrent(list, 0);

        Assert.False(Key(list, SkUiKey.Left));
        Keys(list, SkUiKey.Right, 3);
        AssertCurrent(list, 3); // on into the next row at the end of one
        Key(list, SkUiKey.Left);
        AssertCurrent(list, 2);
        Key(list, SkUiKey.Down);
        AssertCurrent(list, 5);
        Keys(list, SkUiKey.Down, 2);
        AssertCurrent(list, 9); // the short last row
        Key(list, SkUiKey.Up);
        AssertCurrent(list, 8); // back in the column it came from
        Assert.True(Key(list, SkUiKey.End));
        AssertCurrent(list, 9);
        Assert.True(Key(list, SkUiKey.Home));
        AssertCurrent(list, 0);

        // Right to left, cells run from the right: Left moves on.
        list.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(list, 300, 500);
        Key(list, SkUiKey.Left);
        AssertCurrent(list, 1);
        Assert.True(OnScreen(list, Host(list, 1)).X < OnScreen(list, Host(list, 0)).X);
        Key(list, SkUiKey.Right);
        AssertCurrent(list, 0);
    }

    [Fact]
    public void HorizontalListsMoveWithLeftAndRightMirroredRightToLeft()
    {
        var list = List(Rows(50), horizontal: true);
        Focused(list, 500, 200);
        Assert.False(Key(list, SkUiKey.Down));
        Keys(list, SkUiKey.Right, 12);
        AssertCurrent(list, 12);
        Assert.True(Shows(list, 12, 0, 200));
        Assert.True(OnScreen(list, Host(list, 12)) is { X: >= -0.5, Right: <= 500.5 });

        list.FlowDirection = FlowDirection.RightToLeft;
        SkUiTestHelpers.Arrange(list, 500, 200);
        Key(list, SkUiKey.Left);
        AssertCurrent(list, 13);
        Key(list, SkUiKey.Right);
        AssertCurrent(list, 12);

        // A horizontal grid moves across its columns with Up and Down.
        list.FlowDirection = FlowDirection.LeftToRight;
        list.Span = 2;
        SkUiTestHelpers.Arrange(list, 500, 200);
        Assert.True(Key(list, SkUiKey.Down));
        AssertCurrent(list, 13);
        Key(list, SkUiKey.Right);
        AssertCurrent(list, 15);
    }

    private static (SkUiCollectionView List, ObservableCollection<Section> Sections) Grouped(bool expandable)
    {
        var sections = new ObservableCollection<Section>
        {
            new("A", Enumerable.Range(0, 3).Select(index => new Row(index))),
            new("B", Enumerable.Range(3, 3).Select(index => new Row(index))),
            new("C", Enumerable.Range(6, 3).Select(index => new Row(index)))
        };
        var list = List(sections);
        list.IsGrouped = true;
        list.GroupHeaderTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = 30 });
        list.GroupFooterTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = 20 });
        list.AllowGroupExpandCollapse = expandable;
        return (list, sections);
    }

    [Fact]
    public void GroupHeadersAreStopsThatExpandAndCollapseTheirGroup()
    {
        var (list, sections) = Grouped(expandable: true);
        var events = new List<string>();
        list.GroupCollapsing += (_, args) => events.Add($"collapsing {((Section)args.Group!).Name}");
        Focused(list);
        Assert.Equal(0, list.CurrentHeaderGroup); // the first stop in view
        Assert.True(list.FocusRingHost is { IsFocusRingVisible: true });

        Key(list, SkUiKey.Down);
        AssertCurrent(list, 0);
        Key(list, SkUiKey.Left); // from an item to its group's header
        Assert.Equal(0, list.CurrentHeaderGroup);
        Key(list, SkUiKey.Left);
        Assert.Equal(["collapsing A"], events);
        Assert.False(list.IsGroupExpanded(sections[0]));
        Key(list, SkUiKey.Down); // the collapsed group's items are skipped (its footer is no stop)
        Assert.Equal(1, list.CurrentHeaderGroup);
        Key(list, SkUiKey.Up);
        Key(list, SkUiKey.Right);
        Assert.True(list.IsGroupExpanded(sections[0]));
        Key(list, SkUiKey.Right); // expanded: on into its first item
        AssertCurrent(list, 0);

        // Enter on a header toggles it; collapsing the group of the current item hands it to the header.
        Keys(list, SkUiKey.Down, 3);
        Assert.Equal(1, list.CurrentHeaderGroup);
        Key(list, SkUiKey.Enter);
        Assert.False(list.IsGroupExpanded(sections[1]));
        Key(list, SkUiKey.Space);
        Assert.True(list.IsGroupExpanded(sections[1]));
        Keys(list, SkUiKey.Down, 2);
        AssertCurrent(list, 4);
        list.CollapseGroup(sections[1]);
        Assert.Equal(1, list.CurrentHeaderGroup);

        // Without expanding and collapsing, headers are no stops.
        list.AllowGroupExpandCollapse = false;
        list.ExpandAll();
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.True(Key(list, SkUiKey.Down)); // the header left: the first key starts again from the first item in view
        AssertCurrent(list, 0);
        Keys(list, SkUiKey.Down, 3);
        AssertCurrent(list, 3); // past the footer and the header between the groups
        Assert.False(Key(list, SkUiKey.Left));
    }

    [Fact]
    public void TheCurrentItemIsKeptByItemAcrossChangesAndPassesToItsNeighborWhenRemoved()
    {
        var rows = Rows(30);
        var list = Focused(List(rows));
        Keys(list, SkUiKey.Down, 5);
        AssertCurrent(list, 5);

        rows.Insert(0, new Row(100));
        SkUiTestHelpers.Arrange(list, 300, 500);
        AssertCurrent(list, 6); // the same item, one further down
        rows.Move(6, 1);
        SkUiTestHelpers.Arrange(list, 300, 500);
        AssertCurrent(list, 1);
        Assert.Equal(5, ((Row)((BindableObject)list.GetRealizedView(1)!).BindingContext).Index);

        rows.RemoveAt(1);
        SkUiTestHelpers.Arrange(list, 300, 500);
        AssertCurrent(list, 1); // the item now at its place
        Assert.Equal(0, ((Row)((BindableObject)list.GetRealizedView(1)!).BindingContext).Index);
        rows.RemoveAt(rows.Count - 1);
        Key(list, SkUiKey.End);
        rows.RemoveAt(rows.Count - 1);
        SkUiTestHelpers.Arrange(list, 300, 500);
        AssertCurrent(list, rows.Count - 1); // the last one: the one before it

        rows.Clear();
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(-1, list.CurrentItemIndex);
        Assert.True(list.IsFocusRingVisible);
        foreach (var row in Rows(10))
            rows.Add(row);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.True(Key(list, SkUiKey.Down));
        AssertCurrent(list, 0);
    }

    [Fact]
    public void TheRingFollowsTheCurrentItemThroughRecycling()
    {
        var list = Focused(List(Rows(10_000)));
        Key(list, SkUiKey.End);
        AssertCurrent(list, 9_999);
        Assert.Null(list.GetRealizedView(0));
        Key(list, SkUiKey.Home);
        AssertCurrent(list, 0);

        // Scrolled out and back: its container may be another one now, which draws the ring; the others do not.
        Keys(list, SkUiKey.Down, 3);
        list.ScrollView.ScrollTo(0, 100_000);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.Equal(0, RingCount(list));
        Assert.Equal(3, list.CurrentItemIndex);
        list.ScrollView.ScrollTo(0, 0);
        SkUiTestHelpers.Arrange(list, 300, 500);
        AssertCurrent(list, 3);
        // A key while it is scrolled out moves on from it and shows the next one.
        list.ScrollView.ScrollTo(0, 100_000);
        Key(list, SkUiKey.Down);
        AssertCurrent(list, 4);
        Assert.True(Shows(list, 4, 0, 500));
    }

    [Fact]
    public void NavigationKeysFromAButtonInAnItemMoveOnFromItsItem()
    {
        var list = List(Rows(20));
        list.ItemTemplate = new DataTemplate(() =>
        {
            var row = new SkUiGrid { HeightRequest = 50 };
            row.Add(new SkUiButton { Text = "Open", HorizontalOptions = LayoutOptions.End });
            return row;
        });
        Focused(list);
        var order = list.FocusManager.GetTabOrder();
        Assert.Same(list, order[0]); // the list, then the buttons inside its items
        Assert.IsType<SkUiButton>(order[1]);

        var button = (SkUiButton)((SkUiGrid)list.GetRealizedView(2)!).Children[0];
        Assert.True(button.Focus());
        var tapped = false;
        button.Clicked += (_, _) => tapped = true;
        Assert.True(Key(list, SkUiKey.Enter)); // the button's own key
        Assert.True(tapped);
        Assert.True(button.IsFocused);
        Assert.True(Key(list, SkUiKey.Down));
        Assert.True(list.IsFocused);
        AssertCurrent(list, 3);
    }

    [Fact]
    public void TheRingFollowsTheRowToItsSelectedTemplate()
    {
        var list = List(Rows(20));
        list.SelectedItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = 120 });
        Focused(list);
        Key(list, SkUiKey.Down);
        var before = Host(list, 1);
        AssertCurrent(list, 1);

        Assert.True(Key(list, SkUiKey.Space)); // selects it: the row gets a container of the selected template
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.NotSame(before, Host(list, 1));
        AssertCurrent(list, 1);
        Assert.Equal(120, Host(list, 1).Height);
        Key(list, SkUiKey.Down);
        AssertCurrent(list, 2);
    }
}
