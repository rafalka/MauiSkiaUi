using System.Collections.ObjectModel;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiChipGroup"/> / <see cref="SkUiCoreChipGroup"/> (1.18 K, #130): chips from items, selection modes, removal, wrapping, keys.</summary>
public class ChipGroupTests
{
    private static long _pointer = 190_000;

    private static void Tap(SkUiView root, SkUiView chip)
    {
        var at = Center(root, chip);
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    /// <summary>A point a little left of the chip's middle, in root coordinates.</summary>
    private static Point Center(SkUiView root, SkUiView chip)
    {
        double x = chip.Width / 2 - 4, y = chip.Height / 2;
        for (Element? current = chip; current is SkUiView view && !ReferenceEquals(view, root); current = current.Parent)
        {
            x += view.Frame.X;
            y += view.Frame.Y;
        }
        return new Point(x, y);
    }

    private static SkUiChipGroup Group(IEnumerable<string> items, double width = 300)
    {
        var group = new SkUiChipGroup { ItemsSource = items.ToList() };
        SkUiTestHelpers.Arrange(group, width, 400);
        return group;
    }

    [Fact]
    public void OneChipPerItemFollowingTheSource()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var items = new ObservableCollection<string> { "Mon", "Tue" };
        var group = new SkUiChipGroup { ItemsSource = items, ChipKind = SkUiChipKind.Input };
        Assert.Equal(["Mon", "Tue"], group.Chips.Select(chip => chip.Text));
        Assert.All(group.Chips, chip => Assert.Equal(SkUiChipKind.Input, chip.Kind));
        Assert.Equal("Tue", group.Chips[1].BindingContext);

        items.Add("Wed");
        items.RemoveAt(0);
        Assert.Equal(["Tue", "Wed"], group.Chips.Select(chip => chip.Text));

        group.ItemDisplayBinding = new Binding(nameof(string.Length));
        Assert.Equal(["3", "3"], group.Chips.Select(chip => chip.Text));
    }

    [Fact]
    public void MultipleSelectionAddsAndRemovesItems()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var group = Group(["Approved", "Pending", "Rejected"]);
        var changes = new List<SkUiSelectionChangedEventArgs>();
        group.SelectionChanged += (_, args) => changes.Add(args);

        Tap(group, group.Chips[0]);
        Tap(group, group.Chips[2]);
        Assert.Equal(["Approved", "Rejected"], group.SelectedItems);
        Assert.Equal([true, false, true], group.Chips.Select(chip => chip.IsSelected));
        Tap(group, group.Chips[0]);
        Assert.Equal(["Rejected"], group.SelectedItems);
        Assert.Equal(3, changes.Count);
        Assert.Equal(["Rejected"], changes[^1].CurrentSelection);

        group.SelectedItems.Add("Pending"); // the app
        Assert.True(group.Chips[1].IsSelected);
        group.Chips[2].IsSelected = false; // a chip set by the app
        Assert.Equal(["Pending"], group.SelectedItems);
    }

    [Fact]
    public void SingleSelectionKeepsOneAndClearsOnASecondTap()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var group = Group(["Today", "This week", "This month"]);
        group.SelectionMode = SkUiChipSelectionMode.Single;
        var changes = new List<SkUiSelectionChangedEventArgs>();
        group.SelectionChanged += (_, args) => changes.Add(args);

        Tap(group, group.Chips[1]);
        Assert.Equal("This week", group.SelectedItem);
        Tap(group, group.Chips[2]);
        Assert.Equal("This month", group.SelectedItem);
        Assert.Equal([false, false, true], group.Chips.Select(chip => chip.IsSelected));
        Tap(group, group.Chips[2]);
        Assert.Null(group.SelectedItem);
        Assert.Equal(3, changes.Count);
        Assert.Equal("This month", changes[1].CurrentItem);

        group.SelectedItem = "Today";
        Assert.True(group.Chips[0].IsSelected);

        group.SelectionMode = SkUiChipSelectionMode.None;
        var clicks = 0;
        group.Chips[1].Clicked += (_, _) => clicks++;
        Tap(group, group.Chips[1]);
        Assert.Equal(1, clicks);
        Assert.False(group.Chips[1].IsSelected);
        Assert.Equal(SkUiSemanticsRole.Button, SkUiSemanticsTree.Build(group).Flatten().First().Role);
    }

    [Fact]
    public void TheGroupsSyncKeepsAChipsBinding()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var group = Group(["A", "B"]);
        group.SelectionMode = SkUiChipSelectionMode.Single;
        var source = new SkUiSwitch { IsToggled = false };
        group.Chips[0].SetBinding(SkUiChip.IsSelectedProperty, new Binding(nameof(SkUiSwitch.IsToggled), BindingMode.OneWay, source: source));
        Tap(group, group.Chips[1]); // the group clears chip A
        Assert.False(group.Chips[0].IsSelected);
        source.IsToggled = true;
        Assert.True(group.Chips[0].IsSelected); // the binding still runs
    }

    [Fact]
    public void ChangingTheModeClearsOrResizesTheChips()
    {
        // Widths compared are the check's room, not text: no font needed (Linux CI measures text 0 wide).
        var group = Group(["Approved", "Pending"]);
        Tap(group, group.Chips[0]);
        SkUiTestHelpers.Arrange(group, 300, 400);
        var checkedWidth = group.Chips[0].Width;
        var plainWidth = group.Chips[1].Width;
        Assert.True(checkedWidth > plainWidth);

        group.SelectionMode = SkUiChipSelectionMode.None; // no selection: no check, no fill, read as buttons
        SkUiTestHelpers.Arrange(group, 300, 400);
        Assert.False(group.Chips[0].IsSelected);
        Assert.True(group.Chips[0].Width < checkedWidth, $"{checkedWidth} → {group.Chips[0].Width}");
        Assert.All(SkUiSemanticsTree.Build(group).Flatten(), node => Assert.Equal(SkUiSemanticsRole.Button, node.Role));
        group.Chips[1].IsSelected = true; // the app, while the group selects nothing: kept across the group's changes
        group.SelectedItem = "Pending";
        Assert.True(group.Chips[1].IsSelected);
        group.Chips[1].IsSelected = false;

        var changes = new List<SkUiSelectionChangedEventArgs>();
        group.SelectionChanged += (_, args) => changes.Add(args);
        group.SelectionMode = SkUiChipSelectionMode.Multiple; // "Approved" is still in SelectedItems
        SkUiTestHelpers.Arrange(group, 300, 400);
        Assert.True(group.Chips[0].IsSelected);
        Assert.Equal(checkedWidth, group.Chips[0].Width, 1); // the check's room is back
        Tap(group, group.Chips[1]);
        Assert.Equal(["Approved"], changes.Single().PreviousSelection);

        var core = new SkUiCoreChipGroup().SetItemsSource(new[] { "A", "B" }).SetSelectedItems(["A"]);
        var host = new SkUiCoreHost().SetContent(core);
        SkUiTestHelpers.Arrange(host, 300, 200);
        var coreChecked = core.Chips[0].Frame.Width;
        core.SetSelectionMode(SkUiChipSelectionMode.None);
        SkUiTestHelpers.Arrange(host, 300, 200);
        Assert.False(core.Chips[0].IsSelected);
        Assert.True(core.Chips[0].Frame.Width < coreChecked);
    }

    [Fact]
    public void ACommandThatRemovesTheItemStillReportsIt()
    {
        var items = new ObservableCollection<string> { "Anna", "Ben", "Chiara" };
        var group = new SkUiChipGroup { ItemsSource = items, ChipKind = SkUiChipKind.Input, SelectionMode = SkUiChipSelectionMode.None };
        var commanded = new List<object?>();
        var removed = new List<(object? Item, int Index)>();
        var command = new Command<object>(parameter =>
        {
            commanded.Add(parameter);
            items.Remove((string)parameter); // the group rebuilds before the chip is done
        });
        foreach (var chip in group.Chips)
            chip.RemoveCommand = command;
        group.ChipRemoved += (_, args) => removed.Add((args.Item, args.Index));

        group.Chips[1].Remove();
        group.Chips[^1].Remove(); // the last chip: the rebuild takes it out of the group
        Assert.Equal(["Ben", "Chiara"], commanded);
        Assert.Equal([("Ben", 1), ("Chiara", 1)], removed);
        Assert.Equal(["Anna"], items);

        var coreItems = new ObservableCollection<string> { "A", "B" };
        var core = new SkUiCoreChipGroup().SetItemsSource(coreItems).SetChipKind(SkUiChipKind.Input);
        var coreRemoved = new List<object?>();
        core.ChipRemoved += (_, args) => coreRemoved.Add(args.Item);
        core.Chips[1].SetRemoveCommand(new Command<object>(parameter => coreItems.Remove((string)parameter)));
        core.Chips[1].Remove();
        Assert.Equal(["B"], coreRemoved);
    }

    [Fact]
    public void ArrowKeysPassOverDisabledChips()
    {
        var group = Group(["A", "B", "C"]);
        group.Chips[1].IsEnabled = false;
        var focus = group.FocusManager;
        Assert.True(group.Chips[0].Focus());
        Assert.True(focus.KeyDown(SkUiKey.Right));
        Assert.True(group.Chips[2].IsFocused);
        Assert.True(focus.KeyDown(SkUiKey.Left));
        Assert.True(group.Chips[0].IsFocused);
        group.Chips[0].IsEnabled = false;
        group.Chips[1].IsEnabled = true;
        Assert.True(group.Chips[2].Focus());
        Assert.True(focus.KeyDown(SkUiKey.Home)); // the first chip that takes focus
        Assert.True(group.Chips[1].IsFocused);
    }

    [Fact]
    public void RemovingAChipReportsItsItem()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var items = new ObservableCollection<string> { "Anna", "Ben" };
        var group = new SkUiChipGroup { ItemsSource = items, ChipKind = SkUiChipKind.Input, SelectionMode = SkUiChipSelectionMode.None };
        group.ChipRemoved += (_, args) => items.Remove((string)args.Item!);
        group.Chips[1].Remove();
        Assert.Equal(["Anna"], items);
        Assert.Single(group.Chips);
    }

    [Fact]
    public void ChipsWrapOrScrollInOneRowWithThePadding()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var words = Enumerable.Range(1, 12).Select(index => $"Option {index}").ToList();
        var group = new SkUiChipGroup { ItemsSource = words, Padding = new Thickness(16, 0) };
        var wrapped = group.Measure(300, double.PositiveInfinity);
        Assert.True(wrapped.Height > 3 * SkUiLook.Current.DefaultChipHeight, $"rows: {wrapped.Height}");

        group.Wraps = false;
        var row = group.Measure(300, double.PositiveInfinity);
        Assert.True(row.Height < 2 * SkUiLook.Current.DefaultChipHeight);
        SkUiTestHelpers.Arrange(group, 300, row.Height);
        Assert.Equal(16, group.Chips[0].Frame.X, 1); // the padding is inside the scrolled row
        var scroller = Assert.IsType<SkUiScrollView>(group.SkiaChildren.Single());
        Assert.True(group.Chips[^1].Frame.Right > 300, $"last chip ends at {group.Chips[^1].Frame.Right}, row {scroller.Content!.Width}");
    }

    [Fact]
    public void ArrowKeysMoveTheFocusBetweenChips()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var group = Group(["A", "B", "C"]);
        var focus = group.FocusManager;
        Assert.True(group.Chips[0].Focus());
        Assert.True(focus.KeyDown(SkUiKey.Right));
        Assert.True(group.Chips[1].IsFocused);
        Assert.True(focus.KeyDown(SkUiKey.End));
        Assert.True(group.Chips[2].IsFocused);
        Assert.True(focus.KeyDown(SkUiKey.Right)); // at the end: stays
        Assert.True(group.Chips[2].IsFocused);
        Assert.True(focus.KeyDown(SkUiKey.Home));
        Assert.True(group.Chips[0].IsFocused);
    }

    [Fact]
    public void CoreGroupsSelectAndWrap()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var group = new SkUiCoreChipGroup().SetItemsSource(new[] { "Vacation", "Sick", "Unpaid" }).SetSelectionMode(SkUiChipSelectionMode.Single);
        var changes = 0;
        group.SelectionChanged += (_, _) => changes++;
        var host = new SkUiCoreHost().SetContent(group);
        SkUiTestHelpers.Arrange(host, 300, 200);
        Assert.Equal(3, group.Chips.Count);

        var chip = group.Chips[1];
        var at = new Point(chip.Frame.X + chip.Frame.Width / 2 - 4, chip.Frame.Y + chip.Frame.Height / 2);
        var id = ++_pointer;
        host.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        host.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
        Assert.Equal("Sick", group.SelectedItem);
        Assert.Equal(1, changes);

        group.SetSelectionMode(SkUiChipSelectionMode.Multiple).SetSelectedItems(["Vacation", "Unpaid"]);
        Assert.Equal([true, false, true], group.Chips.Select(c => c.IsSelected));
        group.SetItemDisplay(item => ((string)item!).ToUpperInvariant());
        Assert.Equal("SICK", group.Chips[1].Text);
        group.SetWraps(false);
        SkUiTestHelpers.Arrange(host, 300, 200);
        Assert.Equal(3, group.Chips.Count);
    }
}
