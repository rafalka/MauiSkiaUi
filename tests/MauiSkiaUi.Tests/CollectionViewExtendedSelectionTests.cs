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
    public void KeysMoveTheSelectionShiftExtendsItAndCtrlMovesWithoutSelecting()
    {
        var list = List(Rows(20));
        var tapped = new List<int>();
        list.ItemTapped += (_, args) => tapped.Add(args.Index);
        var counter = new Counter(list);
        var focus = list.FocusManager;
        focus.KeyDown(SkUiKey.None);
        Assert.True(list.Focus());
        Assert.Equal(0, list.CurrentItemIndex);

        Assert.True(focus.KeyDown(SkUiKey.Down));
        Assert.Equal([1], Selected(list));
        counter.AssertOne();
        focus.KeyDown(SkUiKey.Down, SkUiKeyModifiers.Shift);
        focus.KeyDown(SkUiKey.Down, SkUiKeyModifiers.Shift);
        Assert.Equal([1, 2, 3], Selected(list));
        counter.AssertOne(2, 2);
        Keys(focus, SkUiKey.Up, SkUiKeyModifiers.Shift, 3);
        Assert.Equal([0, 1], Selected(list)); // from the anchor, the other way
        counter.Changing = counter.Changed = counter.Commands = 0;

        // Ctrl moves the current item alone; Ctrl+Space adds it and anchors there.
        Keys(focus, SkUiKey.Down, SkUiKeyModifiers.Control, 3);
        Assert.Equal(3, list.CurrentItemIndex);
        Assert.Equal([0, 1], Selected(list));
        counter.AssertOne(0, 0);
        Assert.True(focus.KeyDown(SkUiKey.Space, SkUiKeyModifiers.Control));
        Assert.Equal([0, 1, 3], Selected(list));
        Assert.True(focus.KeyDown(SkUiKey.End, SkUiKeyModifiers.Shift));
        Assert.Equal(Enumerable.Range(3, 17), Selected(list));
        Assert.True(focus.KeyDown(SkUiKey.Home, SkUiKeyModifiers.Shift));
        Assert.Equal([0, 1, 2, 3], Selected(list));
        counter.AssertOne(3, 3);

        // Space adds or removes the current item; Shift+Space selects the range to it; Enter taps without selecting.
        Assert.True(focus.KeyDown(SkUiKey.Space));
        Assert.Equal([1, 2, 3], Selected(list));
        Keys(focus, SkUiKey.Down, SkUiKeyModifiers.Control, 5);
        Assert.True(focus.KeyDown(SkUiKey.Space, SkUiKeyModifiers.Shift));
        Assert.Equal([0, 1, 2, 3, 4, 5], Selected(list)); // from the anchor Space left at item 0
        counter.AssertOne(2, 2);
        Assert.True(focus.KeyDown(SkUiKey.Enter));
        Assert.Equal([0, 1, 2, 3, 4, 5], Selected(list));
        counter.AssertOne(0, 0);
        Assert.Equal([3, 0, 5, 5], tapped); // Space (with Ctrl or Shift too) and Enter tap as clicks do

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
