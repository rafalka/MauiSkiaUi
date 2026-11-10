using System.Collections.ObjectModel;
using System.Windows.Input;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.10 C2: extended selection of <see cref="SkUiCollectionView"/> (<see cref="SkUiCollectionView.IsExtendedSelectionEnabled"/>
/// with <see cref="SkUiSelectionMode.Multiple"/>): clicks with and without modifier keys, the keyboard's extending moves,
/// ranges across groups, one cancelable change per gesture, touch's toggling tap; and the pointer's modifier keys reaching
/// gesture recognizers. In the global collection: keyboard focus tests read per-thread input state.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class CollectionViewExtendedSelectionTests
{
    private static long _pointer = 620_000;

    private sealed record Row(int Index);

    private sealed class Section(string name, IEnumerable<Row> rows) : ObservableCollection<Row>(rows)
    {
        public string Name { get; } = name;
    }

    private sealed class Command(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }

    private static ObservableCollection<Row> Rows(int count) => new(Enumerable.Range(0, count).Select(index => new Row(index)));

    private static SkUiCollectionView List(IEnumerable<object> source, bool extended = true)
    {
        var list = new SkUiCollectionView
        {
            ItemsSource = source,
            ItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = 50 }),
            SelectionMode = SkUiSelectionMode.Multiple,
            IsExtendedSelectionEnabled = extended,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Overscroll = SkUiOverscrollMode.None,
            PrefetchBudget = TimeSpan.FromSeconds(10)
        };
        SkUiTestHelpers.Arrange(list, 300, 1200);
        return list;
    }

    /// <summary>A click (mouse) or a tap (touch) on the row at <paramref name="row"/> (50 DIPs each, from the top).</summary>
    private static void Click(SkUiView root, int row, SkUiKeyModifiers modifiers = SkUiKeyModifiers.None, SkUiPointerDevice device = SkUiPointerDevice.Mouse)
    {
        var id = ++_pointer;
        var at = new Point(150, row * 50 + 25);
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1), Device: device) { Modifiers = modifiers });
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02), Device: device) { Modifiers = modifiers });
    }

    private static int[] Selected(SkUiCollectionView list) => [.. list.SelectedItems.Cast<Row>().Select(row => row.Index)];

    /// <summary>Counts the selection's events and command runs per gesture.</summary>
    private sealed class Counter
    {
        public int Changing, Changed, Commands;
        public bool Cancel;

        public Counter(SkUiCollectionView list)
        {
            list.SelectionChanging += (_, args) =>
            {
                Changing++;
                args.Cancel = Cancel;
            };
            list.SelectionChanged += (_, _) => Changed++;
            list.SelectionChangedCommand = new Command(() => Commands++);
        }

        /// <summary>Asserts one event of each since the last check (or none), then starts again.</summary>
        public void AssertOne(int changing = 1, int changed = 1)
        {
            Assert.Equal((changing, changed, changed), (Changing, Changed, Commands));
            Changing = Changed = Commands = 0;
        }
    }

    [Fact]
    public void ClicksSelectOnlyTheirItemCtrlTogglesAndShiftSelectsTheRange()
    {
        var list = List(Rows(20));
        var counter = new Counter(list);

        Click(list, 2);
        Assert.Equal([2], Selected(list));
        counter.AssertOne();
        Click(list, 5);
        Assert.Equal([5], Selected(list));
        counter.AssertOne();
        Click(list, 5); // the same selection: nothing changes, nothing is raised
        counter.AssertOne(0, 0);

        Click(list, 7, SkUiKeyModifiers.Control);
        Assert.Equal([5, 7], Selected(list));
        Click(list, 5, SkUiKeyModifiers.Command); // Command+click on Apple
        Assert.Equal([7], Selected(list));
        counter.AssertOne(2, 2);

        // Shift: the range from the anchor (the item toggled last), in source order, whichever way.
        Click(list, 10, SkUiKeyModifiers.Shift);
        Assert.Equal([5, 6, 7, 8, 9, 10], Selected(list));
        counter.AssertOne(); // one change for the whole range
        Click(list, 3, SkUiKeyModifiers.Shift);
        Assert.Equal([3, 4, 5], Selected(list)); // the anchor stays
        Click(list, 8, SkUiKeyModifiers.Shift | SkUiKeyModifiers.Control);
        Assert.Equal([3, 4, 5, 6, 7, 8], Selected(list)); // added to what was selected
        counter.AssertOne(2, 2);

        // A touch keeps the multiple selection's toggling tap.
        Click(list, 15, device: SkUiPointerDevice.Touch);
        Assert.Equal([3, 4, 5, 6, 7, 8, 15], Selected(list));
        Click(list, 4, device: SkUiPointerDevice.Touch);
        Assert.Equal([3, 5, 6, 7, 8, 15], Selected(list));
        counter.AssertOne(2, 2);

        // Cancelled: nothing changes.
        counter.Cancel = true;
        Click(list, 1);
        Assert.Equal([3, 5, 6, 7, 8, 15], Selected(list));
        counter.AssertOne(1, 0);
    }

    [Fact]
    public void WithoutExtendedSelectionClicksToggleAsTaps()
    {
        var list = List(Rows(10), extended: false);
        Click(list, 1);
        Click(list, 3, SkUiKeyModifiers.Shift);
        Assert.Equal([1, 3], Selected(list));
        Click(list, 1);
        Assert.Equal([3], Selected(list));

        // Only with Multiple: a single selection ignores the switch.
        list.SelectionMode = SkUiSelectionMode.Single;
        list.IsExtendedSelectionEnabled = true;
        Click(list, 4, SkUiKeyModifiers.Control);
        Assert.Equal(4, ((Row)list.SelectedItem!).Index);
    }

    [Fact]
    public void ArrowsMoveOnlyTheCurrentItemSpaceTogglesAndShiftAddsTheRange()
    {
        var list = List(Rows(20));
        var tapped = new List<int>();
        list.ItemTapped += (_, args) => tapped.Add(args.Index);
        var counter = new Counter(list);
        var focus = list.FocusManager;
        focus.KeyDown(SkUiKey.None);
        Assert.True(list.Focus());
        Assert.Equal(0, list.CurrentItemIndex);

        // Clicks select several; the arrow keys move the current item and keep them.
        Click(list, 2);
        Click(list, 4, SkUiKeyModifiers.Command);
        counter.AssertOne(2, 2);
        Assert.True(focus.KeyDown(SkUiKey.Down));
        Assert.True(focus.KeyDown(SkUiKey.Down));
        Assert.Equal(6, list.CurrentItemIndex);
        Assert.True(focus.KeyDown(SkUiKey.End));
        Assert.True(focus.KeyDown(SkUiKey.Up, SkUiKeyModifiers.Command)); // Ctrl / Command change nothing
        Assert.Equal(18, list.CurrentItemIndex);
        Assert.Equal([2, 4], Selected(list));
        counter.AssertOne(0, 0);

        // Space adds or removes the current item, without tapping it.
        Keys(focus, SkUiKey.Up, SkUiKeyModifiers.None, 10);
        Assert.True(focus.KeyDown(SkUiKey.Space));
        Assert.Equal([2, 4, 8], Selected(list));
        Assert.True(focus.KeyDown(SkUiKey.Space, isRepeat: true)); // held: once
        Assert.Equal([2, 4, 8], Selected(list));
        Keys(focus, SkUiKey.Up, SkUiKeyModifiers.None, 4);
        Assert.True(focus.KeyDown(SkUiKey.Space));
        Assert.Equal([2, 8], Selected(list));
        counter.AssertOne(2, 2);

        // Shift+arrows toggle the items they move onto past where the current item was when Shift was first pressed (it keeps
        // its state), in what was selected; moving back shrinks the range, past the start it runs the other way.
        Keys(focus, SkUiKey.Down, SkUiKeyModifiers.None, 6);
        Keys(focus, SkUiKey.Down, SkUiKeyModifiers.Shift, 3);
        Assert.Equal([2, 8, 11, 12, 13], Selected(list));
        counter.AssertOne(3, 3); // one change per key
        Keys(focus, SkUiKey.Up, SkUiKeyModifiers.Shift, 4);
        Assert.Equal([2, 8, 9], Selected(list));
        Assert.True(focus.KeyDown(SkUiKey.Home, SkUiKeyModifiers.Shift));
        Assert.Equal([0, 1, 3, 4, 5, 6, 7, 9], Selected(list)); // 2 and 8 were selected: toggled off
        counter.Changing = counter.Changed = counter.Commands = 0;

        // A plain move starts the next range where it ends; a selection change by anything else is kept by the next range.
        list.SelectedItems.Clear();
        Keys(focus, SkUiKey.Down, SkUiKeyModifiers.None, 15);
        list.SelectedItems.Add(list.ItemsSource!.Cast<Row>().ElementAt(1));
        Assert.True(focus.KeyDown(SkUiKey.Down, SkUiKeyModifiers.Shift));
        Assert.Equal([1, 16], Selected(list));
        // Shift+Space toggles the items past the anchor up to the current one; on the anchor, the current item alone.
        Keys(focus, SkUiKey.Down, SkUiKeyModifiers.None, 2);
        Keys(focus, SkUiKey.Up, SkUiKeyModifiers.Command, 1);
        Assert.Equal(17, list.CurrentItemIndex);
        Assert.True(focus.KeyDown(SkUiKey.Space)); // toggles 17 and anchors there
        Keys(focus, SkUiKey.Down, SkUiKeyModifiers.Command, 2);
        // A plain (or Ctrl) move anchors at the item it reaches: Shift+Space there toggles 19 alone.
        Assert.True(focus.KeyDown(SkUiKey.Space, SkUiKeyModifiers.Shift));
        Assert.Equal([1, 16, 17, 19], Selected(list));

        // Enter taps (opens) the current item without selecting; Space never tapped.
        counter.Changing = counter.Changed = counter.Commands = 0;
        Assert.True(focus.KeyDown(SkUiKey.Enter));
        Assert.Equal([1, 16, 17, 19], Selected(list));
        counter.AssertOne(0, 0);
        Assert.Equal([2, 19], tapped); // the plain click tapped too; the Command+click only selected

        // Ctrl / Command+A selects every item, once.
        Assert.True(focus.KeyDown(SkUiKey.Character, SkUiKeyModifiers.Command, character: 'a'));
        Assert.Equal(20, list.SelectedItems.Count);
        counter.AssertOne();
        Assert.True(focus.KeyDown(SkUiKey.Character, SkUiKeyModifiers.Control, character: 'a'));
        counter.AssertOne(0, 0);
        // Alt never takes part; without extended selection Ctrl+arrows are not the list's.
        Assert.False(focus.KeyDown(SkUiKey.Down, SkUiKeyModifiers.Alt));
        list.IsExtendedSelectionEnabled = false;
        Assert.False(focus.KeyDown(SkUiKey.Down, SkUiKeyModifiers.Control));
        Assert.False(focus.KeyDown(SkUiKey.Character, SkUiKeyModifiers.Control, character: 'a'));
    }

    [Fact]
    public void ShiftArrowsDeselectSelectedItemsAndMovingBackRestoresThem()
    {
        var list = List(Rows(10));
        var focus = list.FocusManager;
        focus.KeyDown(SkUiKey.None);
        Assert.True(list.Focus());
        Click(list, 2);
        Click(list, 3, SkUiKeyModifiers.Command);
        Click(list, 6, SkUiKeyModifiers.Command);
        int[] Sorted() => [.. Selected(list).Order()];

        focus.KeyDown(SkUiKey.Up);
        focus.KeyDown(SkUiKey.Up);
        focus.KeyDown(SkUiKey.Up); // at 3: the range starts here
        focus.KeyDown(SkUiKey.Up, SkUiKeyModifiers.Shift);
        Assert.Equal([3, 6], Sorted()); // 2 was selected: off; 3, where the range starts, keeps its state
        focus.KeyDown(SkUiKey.Down, SkUiKeyModifiers.Shift);
        Assert.Equal([2, 3, 6], Sorted()); // back at the start: as it was
        Keys(focus, SkUiKey.Down, SkUiKeyModifiers.Shift, 3);
        Assert.Equal([2, 3, 4, 5], Sorted()); // 6 off, 4 and 5 on
        Keys(focus, SkUiKey.Up, SkUiKeyModifiers.Shift, 3);
        Assert.Equal([2, 3, 6], Sorted());
    }

    [Fact]
    public void AClickedOrToggledItemStartsTheRangeOfShiftArrowsSelected()
    {
        var list = List(Rows(12));
        var focus = list.FocusManager;
        focus.KeyDown(SkUiKey.None);
        Assert.True(list.Focus());

        Click(list, 2);
        focus.KeyDown(SkUiKey.Down, SkUiKeyModifiers.Shift);
        Assert.Equal([2, 3], Selected(list)); // as every desktop list
        focus.KeyDown(SkUiKey.Down);
        focus.KeyDown(SkUiKey.Down);
        focus.KeyDown(SkUiKey.Space); // 5 on
        focus.KeyDown(SkUiKey.Down, SkUiKeyModifiers.Shift);
        Assert.Equal([2, 3, 5, 6], Selected(list));

        // A Shift+click's range goes on with Shift+arrows, and shrinks back.
        Click(list, 8);
        Click(list, 10, SkUiKeyModifiers.Shift);
        Assert.Equal([8, 9, 10], Selected(list));
        focus.KeyDown(SkUiKey.Down, SkUiKeyModifiers.Shift);
        Assert.Equal([8, 9, 10, 11], Selected(list));
        focus.KeyDown(SkUiKey.Up, SkUiKeyModifiers.Shift);
        focus.KeyDown(SkUiKey.Up, SkUiKeyModifiers.Shift);
        Assert.Equal([8, 9], Selected(list));
    }

    [Fact]
    public void ModifierClicksSelectWithoutTappingAndCtrlAKeepsTheCurrentItemInView()
    {
        var rows = Rows(200);
        var list = List(rows);
        var tapped = 0;
        list.ItemTapped += (_, _) => tapped++;
        Click(list, 1);
        Click(list, 3, SkUiKeyModifiers.Control);
        Click(list, 5, SkUiKeyModifiers.Shift);
        Click(list, 7, SkUiKeyModifiers.Shift | SkUiKeyModifiers.Command);
        Assert.Equal(1, tapped); // the plain click only
        Assert.Equal([3, 4, 5, 6, 7], Selected(list));

        // Ctrl+A far down the list: the current item stays in view, not the first selected one.
        list.KeepSelectionVisible = true;
        var focus = list.FocusManager;
        focus.KeyDown(SkUiKey.None);
        Assert.True(list.Focus());
        Assert.True(focus.KeyDown(SkUiKey.End));
        var offset = list.ScrollView.ScrollY;
        Assert.True(offset > 1000);
        Assert.True(focus.KeyDown(SkUiKey.Character, SkUiKeyModifiers.Control, character: 'a'));
        Assert.Equal(200, list.SelectedItems.Count);
        Assert.Equal(offset, list.ScrollView.ScrollY, 1);
    }

    [Fact]
    public void AResetForgetsTheAnchor()
    {
        var list = List(new ObservableCollection<Row>(Enumerable.Range(0, 10).Select(index => new Row(index))));
        Click(list, 2);
        // A new source of equal items: Shift+click ranges from where it is clicked, not from the old source's anchor.
        list.ItemsSource = new ObservableCollection<Row>(Enumerable.Range(0, 10).Select(index => new Row(index)));
        SkUiTestHelpers.Arrange(list, 300, 1200);
        list.SelectedItems.Clear();
        Click(list, 6, SkUiKeyModifiers.Shift);
        Assert.Equal([6], Selected(list));
    }

    [Fact]
    public void ClearingTheTapCommandOfAListWithoutSelectionGivesUpFocus()
    {
        var list = List(Rows(5));
        list.SelectionMode = SkUiSelectionMode.None;
        list.ItemTappedCommand = new Command(() => { });
        var focus = list.FocusManager;
        focus.KeyDown(SkUiKey.None);
        Assert.True(list.Focus());
        list.ItemTappedCommand = null;
        Assert.False(list.IsFocused);
        // An ItemTapped handler makes it focusable again; removing it gives focus up.
        EventHandler<SkUiItemTappedEventArgs> handler = (_, _) => { };
        list.ItemTapped += handler;
        Assert.True(list.Focus());
        list.ItemTapped -= handler;
        Assert.False(list.IsFocused);
    }

    [Fact]
    public void InsertTogglesTheCurrentItemAndMovesOn()
    {
        var list = List(Rows(10));
        var focus = list.FocusManager;
        focus.KeyDown(SkUiKey.None);
        Assert.True(list.Focus());

        Assert.True(focus.KeyDown(SkUiKey.Insert));
        Assert.True(focus.KeyDown(SkUiKey.Insert, isRepeat: true)); // held: item after item
        Assert.Equal([0, 1], Selected(list));
        Assert.Equal(2, list.CurrentItemIndex);
        focus.KeyDown(SkUiKey.Up);
        Assert.True(focus.KeyDown(SkUiKey.Insert));
        Assert.Equal([0], Selected(list));
        Assert.Equal(2, list.CurrentItemIndex);
        // Shift+arrows toggle from where Insert left the current item.
        focus.KeyDown(SkUiKey.Down, SkUiKeyModifiers.Shift);
        Assert.Equal([0, 3], Selected(list)); // 2, where the range starts, keeps its state
        // At the end it toggles and stays.
        focus.KeyDown(SkUiKey.End);
        Assert.True(focus.KeyDown(SkUiKey.Insert));
        Assert.Equal(9, list.CurrentItemIndex);
        Assert.Equal([0, 3, 9], Selected(list));

        // Only in an extended selection.
        list.IsExtendedSelectionEnabled = false;
        Assert.False(focus.KeyDown(SkUiKey.Insert));
    }

    [Fact]
    public void InsertMovesAcrossAGridRowThenToTheNextRow()
    {
        var list = List(Rows(7));
        list.Span = 3;
        SkUiTestHelpers.Arrange(list, 300, 1200);
        var focus = list.FocusManager;
        focus.KeyDown(SkUiKey.None);
        Assert.True(list.Focus());
        focus.KeyDown(SkUiKey.Right);
        Keys(focus, SkUiKey.Insert, SkUiKeyModifiers.None, 3);
        Assert.Equal([1, 2, 3], Selected(list));
        Assert.Equal(4, list.CurrentItemIndex);
    }

    private static void Keys(SkUiFocusManager focus, SkUiKey key, SkUiKeyModifiers modifiers, int times)
    {
        for (var press = 0; press < times; press++)
            focus.KeyDown(key, modifiers);
    }

    [Fact]
    public void RangesRunAcrossGroupsLeavingCollapsedGroupsOut()
    {
        var rows = Enumerable.Range(0, 9).Select(index => new Row(index)).ToList();
        var sections = new ObservableCollection<Section>
        {
            new("A", rows[0..3]),
            new("B", [.. rows[3..6], rows[0]]), // row 0 also shows in B
            new("C", rows[6..9])
        };
        var list = new SkUiCollectionView
        {
            ItemsSource = sections,
            IsGrouped = true,
            ItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = 50 }),
            SelectionMode = SkUiSelectionMode.Multiple,
            IsExtendedSelectionEnabled = true,
            PrefetchBudget = TimeSpan.FromSeconds(10)
        };
        SkUiTestHelpers.Arrange(list, 300, 1200);
        var counter = new Counter(list);

        Click(list, 1); // row 1 (no headers: rows are items)
        Click(list, 8, SkUiKeyModifiers.Shift); // row 8 = item 8: C's second
        Assert.Equal([1, 2, 3, 4, 5, 0, 6, 7], Selected(list)); // source order, row 0 once (its second appearance)
        counter.AssertOne(2, 2);

        list.CollapseGroup(sections[1]);
        SkUiTestHelpers.Arrange(list, 300, 1200);
        Click(list, 0);
        Click(list, 4, SkUiKeyModifiers.Shift); // A (3 rows), then C's second: B collapsed
        Assert.Equal([0, 1, 2, 6, 7], Selected(list));

        // An anchor that left the list: the range starts at the clicked item.
        Click(list, 2);
        sections[0].RemoveAt(2);
        SkUiTestHelpers.Arrange(list, 300, 1200);
        Click(list, 3, SkUiKeyModifiers.Shift);
        Assert.Equal([7], Selected(list));
    }

    /// <summary>Records the modifiers of the samples it sees.</summary>
    private sealed class ModifierProbe : SkUiGestureRecognizer
    {
        public List<SkUiKeyModifiers> Seen { get; } = [];

        protected internal override bool OnPointerPressed(SkUiPointer pointer)
        {
            Seen.Add(pointer.Modifiers);
            return true;
        }

        protected internal override void OnPointerMoved(SkUiPointer pointer) => Seen.Add(pointer.Modifiers);

        protected internal override void OnPointerReleased(SkUiPointer pointer) => Seen.Add(pointer.Modifiers);
    }

    [Fact]
    public void ThePointersModifierKeysReachGestureRecognizersPerSample()
    {
        var probe = new ModifierProbe();
        var box = new Core.SkUiCoreHost().SetContent(new Core.SkUiCoreBox().SetWidth(100).SetHeight(100).AddGestureRecognizer(probe));
        SkUiTestHelpers.Arrange(box, 100, 100);
        var id = ++_pointer;
        box.Touch(new(id, SkUiTouchAction.Pressed, new Point(10, 10), TimeSpan.FromSeconds(1), Device: SkUiPointerDevice.Mouse) { Modifiers = SkUiKeyModifiers.Shift });
        box.Touch(new(id, SkUiTouchAction.Moved, new Point(11, 10), TimeSpan.FromSeconds(1.01), Device: SkUiPointerDevice.Mouse));
        box.Touch(new(id, SkUiTouchAction.Released, new Point(11, 10), TimeSpan.FromSeconds(1.02), Device: SkUiPointerDevice.Mouse)
            { Modifiers = SkUiKeyModifiers.Control | SkUiKeyModifiers.Alt });
        Assert.Equal([SkUiKeyModifiers.Shift, SkUiKeyModifiers.None, SkUiKeyModifiers.Control | SkUiKeyModifiers.Alt], probe.Seen);
    }
}
