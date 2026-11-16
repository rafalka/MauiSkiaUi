using System.ComponentModel;
using System.Runtime.CompilerServices;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <see cref="SkUiPickerWheels"/>: the popup's wheels inline in a page. Columns bind two-way, the wheels write once they settle
/// (<see cref="SkUiPickerWheels.CommitDelay"/>), the app's changes move them, and they are built again when the columns change.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerWheelsTests : IDisposable
{
    private static readonly string[] Numbers = ["1", "2", "3", "4", "5"];
    private static readonly string[] Units = ["days", "weeks", "months", "years"];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly ManualGestureClock _clock = new();

    public void Dispose()
    {
        _clock.Dispose();
        _ui.Dispose();
    }

    private sealed class Schedule : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Count { get; set { field = value; Raise(); } }

        public int Unit { get; set { field = value; Raise(); } }

        private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private static SkUiPickerWheels Create(params int[] selected)
    {
        var wheels = new SkUiPickerWheels()
            .AddColumn(new SkUiPickerColumn { ItemsSource = Numbers, Header = "Every" })
            .AddColumn(new SkUiPickerColumn { ItemsSource = Units });
        for (var column = 0; column < selected.Length; column++)
            wheels.Columns[column].SelectedIndex = selected[column];
        return wheels;
    }

    private static SkUiVerticalStackLayout Place(SkUiPickerWheels wheels)
    {
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(wheels);
        SkUiTestHelpers.Arrange(stack, 360, 600);
        return stack;
    }

    private static bool Key(SkUiPickerWheels wheels, SkUiKey key) =>
        ((ISkUiKeyHandler)wheels).OnKeyDown(new SkUiKeyEvent(key, SkUiKeyModifiers.None, false));

    [Fact]
    public void WritesOnceTheWheelsHaveSettled()
    {
        var wheels = Create(0, 1);
        Place(wheels);
        var changes = new List<SkUiColumnSelectionChangedEventArgs>();
        wheels.SelectionChanged += (_, args) => changes.Add(args);

        Assert.True(Key(wheels, SkUiKey.Down));
        Assert.True(Key(wheels, SkUiKey.Down));
        // Rows passed on the way are not written.
        Assert.Equal(0, wheels.Columns[0].SelectedIndex);
        Assert.True(wheels.HasPendingChoice);
        _clock.Advance(TimeSpan.FromMilliseconds(60));
        Assert.Equal(0, wheels.Columns[0].SelectedIndex);

        _clock.Advance(TimeSpan.FromMilliseconds(60));
        Assert.Equal(2, wheels.Columns[0].SelectedIndex);
        Assert.Equal("3", wheels.Columns[0].SelectedItem);
        Assert.False(wheels.HasPendingChoice);
        var change = Assert.Single(changes);
        Assert.Equal((0, 0, 2), (change.ColumnIndex, change.PreviousIndex, change.CurrentIndex));
        Assert.Equal([2, 1], wheels.SelectedIndices);
    }

    [Fact]
    public void AChoiceWaitingForTheDelayIsKeptWhenTheWheelsAreBuiltAgain()
    {
        var wheels = Create(0, 1);
        Place(wheels);

        Assert.True(Key(wheels, SkUiKey.Down));
        Assert.True(wheels.HasPendingChoice);
        wheels.TextColor = Colors.Red; // a theme switch builds the wheels again

        Assert.False(wheels.HasPendingChoice);
        Assert.Equal(1, wheels.Columns[0].SelectedIndex);
        Assert.Equal(1, wheels.Wheels.Wheel(0).Position);
    }

    [Fact]
    public void AWaitingChoiceIsDroppedWhenTheColumnsChange()
    {
        var wheels = Create(2, 1);
        Place(wheels);

        // A column inserted in front: the waiting [3, 1] was made for the old columns and is not written into the new ones.
        Assert.True(Key(wheels, SkUiKey.Down));
        wheels.Columns.Insert(0, new SkUiPickerColumn { ItemsSource = new[] { "AM", "PM" }, SelectedIndex = 0 });
        Assert.False(wheels.HasPendingChoice);
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal([0, 2, 1], wheels.SelectedIndices);

        // A column's items replaced: the waiting choice goes too (its index named an item the user no longer sees).
        Assert.True(Key(wheels, SkUiKey.Right));
        Assert.True(Key(wheels, SkUiKey.Down));
        Assert.True(wheels.HasPendingChoice);
        wheels.Columns[1].ItemsSource = new[] { "10", "20", "30", "40", "50" };
        Assert.False(wheels.HasPendingChoice);
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, wheels.Columns[1].SelectedIndex);
    }

    [Fact]
    public void AHandlerThatThrowsStillLeavesTheChoiceReported()
    {
        var wheels = Create(0, 0);
        Place(wheels);
        var changes = new List<int>();
        wheels.SelectionChanged += (_, args) => changes.Add(args.ColumnIndex);
        wheels.Columns[1].PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiPickerColumn.SelectedIndex))
                throw new InvalidOperationException("the app's handler");
        };

        Assert.True(Key(wheels, SkUiKey.Down));
        Assert.True(Key(wheels, SkUiKey.Right));
        Assert.True(Key(wheels, SkUiKey.Down));
        Assert.Throws<InvalidOperationException>(wheels.CommitNow);

        // The first column was written before the throw: it is reported, and the picker's indices are not stale.
        Assert.Equal(1, wheels.SelectedIndices[0]);
        Assert.Contains(0, changes);
    }

    [Fact]
    public void HandlersSeeTheWholeChoice()
    {
        var wheels = Create(0, 0);
        Place(wheels);
        var seen = new List<string>();
        wheels.SelectionChanged += (_, _) => seen.Add(string.Join(",", wheels.Columns.Select(column => column.SelectedIndex)));

        Assert.True(Key(wheels, SkUiKey.Down));
        Assert.True(Key(wheels, SkUiKey.Right));
        Assert.True(Key(wheels, SkUiKey.Down));
        wheels.CommitNow();

        // Two columns changed in one commit: both events see both new indices, never "1,0".
        Assert.Equal(["1,1", "1,1"], seen);
    }

    [Fact]
    public void ZeroDelayWritesAtOnce()
    {
        var wheels = Create(0, 0).SetCommitDelay(TimeSpan.Zero);
        Place(wheels);

        Assert.True(Key(wheels, SkUiKey.Right));
        Assert.True(Key(wheels, SkUiKey.End));

        Assert.Equal(3, wheels.Columns[1].SelectedIndex);
    }

    [Fact]
    public void ColumnsBindTwoWayAndOneWayBindingsSurvive()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new Schedule { Count = 1, Unit = 0 };
        var wheels = Create();
        wheels.Columns[0].SetBinding(SkUiPickerColumn.SelectedIndexProperty, nameof(Schedule.Count));
        wheels.Columns[1].SetBinding(SkUiPickerColumn.SelectedIndexProperty, new Binding(nameof(Schedule.Unit), BindingMode.OneWay));
        wheels.BindingContext = model;
        Place(wheels);
        Assert.Equal(1, wheels.Wheels.Wheel(0).Position);

        Assert.True(Key(wheels, SkUiKey.Down));
        Assert.True(Key(wheels, SkUiKey.Right));
        Assert.True(Key(wheels, SkUiKey.Down));
        wheels.CommitNow();

        Assert.Equal(2, model.Count);
        // One-way: the model keeps its value, and the binding still works after the wheels wrote the column.
        Assert.Equal(0, model.Unit);
        Assert.Equal(1, wheels.Columns[1].SelectedIndex);
        model.Unit = 3;
        Assert.Equal(3, wheels.Columns[1].SelectedIndex);
        Assert.Equal(3, wheels.Wheels.Wheel(1).Position);
    }

    [Fact]
    public void TheAppsChoiceMovesTheWheelsAndWinsOverAWaitingOne()
    {
        var wheels = Create(0, 0);
        Place(wheels);

        Assert.True(Key(wheels, SkUiKey.Down));
        Assert.True(wheels.HasPendingChoice);
        wheels.Columns[0].SelectedIndex = 4;

        Assert.False(wheels.HasPendingChoice);
        Assert.Equal(4, wheels.Wheels.Wheel(0).Position);
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(4, wheels.Columns[0].SelectedIndex);
    }

    [Fact]
    public void ColumnsChangedBuildTheWheelsAgain()
    {
        var wheels = Create(1, 1);
        Place(wheels);
        var first = wheels.Wheels;
        Assert.Equal(2, first.ColumnCount);

        wheels.AddColumn(new SkUiPickerColumn { ItemsSource = new[] { "before start", "before due" }, SelectedIndex = 1 });

        var second = wheels.Wheels;
        Assert.NotSame(first, second);
        Assert.Equal(3, second.ColumnCount);
        Assert.Equal(1, second.Wheel(2).Position);
        Assert.Same(second, Assert.Single(wheels.SkiaChildren));

        // A column's items changing builds them again too.
        wheels.Columns[1].ItemsSource = new[] { "hours", "days" };
        Assert.NotSame(second, wheels.Wheels);
    }

    [Fact]
    public void ColumnsSetBeforeTheViewIsPlacedBuildOnce()
    {
        var wheels = Create(2, 0);
        wheels.VisibleRows = 3;
        wheels.FontSize = 20;

        Place(wheels);
        var built = wheels.Wheels;
        SkUiTestHelpers.Arrange((IView)wheels.Parent!, 360, 600);

        Assert.Same(built, wheels.Wheels);
        Assert.Equal(2, built.Wheel(0).Position);
    }

    [Fact]
    public void DisabledItemsArePassedAndLeft()
    {
        var wheels = Create(0, 0).SetCommitDelay(TimeSpan.Zero);
        wheels.Columns[0].ItemEnabledBinding = new Binding(".", converter: new FuncConverter(item => (string)item! != "2"));
        Place(wheels);

        Assert.True(Key(wheels, SkUiKey.Down));
        Assert.Equal(2, wheels.Columns[0].SelectedIndex);
    }

    [Fact]
    public void MeasuresItsRows()
    {
        var wheels = Create(0, 0);
        wheels.Columns[0].Header = null;
        Place(wheels);
        Assert.Equal(5 * SkUiLook.Current.PickerWheelRowHeight, wheels.Height, 3);

        wheels.SetRowHeight(40).SetVisibleRows(3);
        SkUiTestHelpers.Arrange((IView)wheels.Parent!, 360, 600);

        Assert.Equal(120, wheels.Height, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => wheels.SetRowHeight(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => wheels.SetVisibleRows(10));
    }

    [Fact]
    public void EachColumnIsOneAdjustableElement()
    {
        var wheels = Create(1, 2).SetCommitDelay(TimeSpan.Zero);
        using var surface = new SkUiTestSurface(wheels, 360, 400);
        surface.Frame();

        var nodes = SkUiSemanticsTree.Build(wheels).Flatten().Where(node => node.Source is SkUiCarouselView).ToList();
        Assert.Equal(2, nodes.Count);
        Assert.All(nodes, node => Assert.Equal(SkUiSemanticsRole.Slider, node.Role));
        Assert.Equal("Every", nodes[0].Label);
        Assert.Equal("2, 2 of 5", nodes[0].Value);

        Assert.True(new SkUiSemanticsOwner(wheels).Perform(nodes[1].Id, SkUiSemanticsActions.Increment));
        Assert.Equal(3, wheels.Columns[1].SelectedIndex);
    }

    [Fact]
    public void RunsTheCommandBeforeTheEvent()
    {
        var order = new List<string>();
        var wheels = Create(0, 0).SetCommitDelay(TimeSpan.Zero)
            .SetSelectionChangedCommand(new Command<string>(parameter => order.Add($"command {parameter}")))
            .SetSelectionChangedCommandParameter("p");
        wheels.SelectionChanged += (_, _) => order.Add("event");
        Place(wheels);

        Assert.True(Key(wheels, SkUiKey.Down));

        Assert.Equal(["command p", "event"], order);
    }

    [Fact]
    public void RightToLeftSwapsLeftAndRight()
    {
        var wheels = Create(0, 0);
        wheels.FlowDirection = FlowDirection.RightToLeft;
        Place(wheels);

        Assert.True(Key(wheels, SkUiKey.Left));

        Assert.Equal(1, wheels.Wheels.ActiveColumn);
    }

    /// <summary>Press, move in steps, release — 10 ms per sample.</summary>
    private static void Drag(ISkUiView root, Point from, Point to, long id = 1, int steps = 10)
    {
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.Zero));
        for (var step = 1; step <= steps; step++)
        {
            var t = (double)step / steps;
            root.Touch(new(id, SkUiTouchAction.Moved, new Point(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t),
                TimeSpan.FromMilliseconds(step * 10)));
        }
        root.Touch(new(id, SkUiTouchAction.Released, to, TimeSpan.FromMilliseconds(steps * 10 + 500)));
    }

    /// <summary>The wheels at the top of a page that scrolls (as a sheet), the first column without a header.</summary>
    private static (SkUiScrollView Page, SkUiPickerWheels Wheels) InScrollingPage(params int[] selected)
    {
        var wheels = Create(selected).SetCommitDelay(TimeSpan.Zero);
        wheels.Columns[0].Header = null;
        var column = new SkUiVerticalStackLayout();
        column.Children.Add(wheels);
        column.Children.Add(new SkUiBox { HeightRequest = 1000 });
        var page = new SkUiScrollView { Content = column };
        SkUiTestHelpers.Arrange(page, 300, 400);
        return (page, wheels);
    }

    [Fact]
    public void DragsOnAWheelAtItsEndDoNotScrollThePage()
    {
        var (page, wheels) = InScrollingPage(4, 0);
        using var surface = new SkUiTestSurface(page, 300, 400);
        surface.Frame();
        var middle = wheels.Height / 2;

        // Up on the first column's wheel, which shows its last item: the page stays.
        Drag(page, new Point(40, middle), new Point(40, middle - 120));
        surface.Frame();
        Assert.Equal(0, page.ScrollY);
        Assert.Equal(4, wheels.Columns[0].SelectedIndex);

        // A long drag from the start: the wheel turns to its end, and the rest of the drag does not go to the page.
        wheels.Columns[0].SelectedIndex = 1;
        surface.Frame();
        Drag(page, new Point(40, middle), new Point(40, middle - 380));
        surface.Frame();
        Assert.Equal(0, page.ScrollY);

        // Below the wheels, the page scrolls as before.
        Drag(page, new Point(150, wheels.Height + 100), new Point(150, wheels.Height));
        Assert.Equal(100, page.ScrollY);
    }

    [Fact]
    public void TicksWhileTurningWhenHapticsAreOn()
    {
        var ticks = 0;
        SkUiHaptics.TestHook = _ => ticks++;
        try
        {
            var (page, wheels) = InScrollingPage(0, 0);
            using var surface = new SkUiTestSurface(page, 300, 400);
            surface.Frame();
            var middle = wheels.Height / 2;

            // Off by default (the app-wide setting).
            SkUiHaptics.ResetForTests();
            Drag(page, new Point(40, middle), new Point(40, middle - 100));
            surface.Frame();
            Assert.Equal(0, ticks);

            wheels.IsHapticFeedbackEnabled = true;
            SkUiHaptics.ResetForTests();
            Drag(page, new Point(40, middle), new Point(40, middle + 100));
            surface.Frame();
            Assert.True(ticks >= 1);

            // Keys move the wheel at once and give no tick.
            ticks = 0;
            SkUiHaptics.ResetForTests();
            Assert.True(Key(wheels, SkUiKey.Down));
            Assert.Equal(0, ticks);
        }
        finally
        {
            SkUiHaptics.TestHook = null;
        }
    }

    [Fact]
    public void TheAppWideSettingTurnsTicksOn()
    {
        var ticks = 0;
        SkUiHaptics.TestHook = _ => ticks++;
        SkUiGestureSettings.IsWheelHapticFeedbackEnabled = true;
        try
        {
            var (page, wheels) = InScrollingPage(0, 0);
            using var surface = new SkUiTestSurface(page, 300, 400);
            surface.Frame();
            SkUiHaptics.ResetForTests();
            Drag(page, new Point(40, wheels.Height / 2), new Point(40, wheels.Height / 2 - 100));
            surface.Frame();
            Assert.True(ticks >= 1);

            // The view's own setting wins.
            ticks = 0;
            wheels.IsHapticFeedbackEnabled = false;
            SkUiHaptics.ResetForTests();
            Drag(page, new Point(40, wheels.Height / 2), new Point(40, wheels.Height / 2 + 100));
            surface.Frame();
            Assert.Equal(0, ticks);
        }
        finally
        {
            SkUiGestureSettings.IsWheelHapticFeedbackEnabled = false;
            SkUiHaptics.TestHook = null;
        }
    }

    [Fact]
    public void TheFocusRingIsOnTheActiveColumn()
    {
        var wheels = Create(0, 0);
        Place(wheels);
        var focus = (ISkUiAccessibleNode)wheels;

        focus.SetKeyboardFocus(true, true);
        Assert.False(wheels.IsFocusRingVisible);
        Assert.True(wheels.Wheels.Wheel(0).IsFocusRingVisible);
        Assert.False(wheels.Wheels.Wheel(1).IsFocusRingVisible);

        Assert.True(Key(wheels, SkUiKey.Right));
        Assert.False(wheels.Wheels.Wheel(0).IsFocusRingVisible);
        Assert.True(wheels.Wheels.Wheel(1).IsFocusRingVisible);

        // Built again (a column added): the ring stays on the active column.
        wheels.AddColumn(new SkUiPickerColumn { ItemsSource = new[] { "a", "b" } });
        Assert.True(wheels.Wheels.Wheel(wheels.Wheels.ActiveColumn).IsFocusRingVisible);

        // Focus from a pointer shows no ring; losing focus hides it.
        focus.SetKeyboardFocus(true, false);
        Assert.All(Enumerable.Range(0, 3), column => Assert.False(wheels.Wheels.Wheel(column).IsFocusRingVisible));
        focus.SetKeyboardFocus(true, true);
        focus.SetKeyboardFocus(false, false);
        Assert.All(Enumerable.Range(0, 3), column => Assert.False(wheels.Wheels.Wheel(column).IsFocusRingVisible));
    }

    [Fact]
    public void ScreenReadersSeeOnlyTheWheels()
    {
        // The readout needs a font that measures on Linux CI (no system fonts there: a 0-wide label is no element).
        using var font = SkUiTestHelpers.UseBundledFont();
        var wheels = Create(1, 0);
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(new SkUiLabel { Text = "Every 2 days", FontFamily = SkUiTestHelpers.BundledFontFamily });
        stack.Children.Add(wheels);
        using var surface = new SkUiTestSurface(stack, 360, 400);
        surface.Frame();

        // The view itself is no element: the readout, then one adjustable element per column.
        var nodes = SkUiSemanticsTree.Build(stack).Flatten().Where(node => node.Source is not null).ToList();
        Assert.Equal([SkUiSemanticsRole.Text, SkUiSemanticsRole.Slider, SkUiSemanticsRole.Slider], nodes.Select(node => node.Role));
        Assert.Equal("days, 1 of 4", nodes[2].Value);

        // Disabled: read as disabled, with no adjustment offered, and an adjustment does nothing.
        wheels.IsEnabled = false;
        surface.Frame();
        var hour = SkUiSemanticsTree.Build(stack).Flatten().First(node => node.Source is SkUiCarouselView);
        Assert.False(hour.IsEnabled);
        Assert.Equal(SkUiSemanticsActions.None, hour.EnabledActions);
        Assert.False(new SkUiSemanticsOwner(stack).Perform(hour.Id, SkUiSemanticsActions.Increment));
        Assert.False(Key(wheels, SkUiKey.Down));
        Assert.Equal(1, wheels.Columns[0].SelectedIndex);
    }

    [Fact]
    public void PageKeysMoveByTheVisibleRowsLessOne()
    {
        var wheels = Create(0, 0).SetCommitDelay(TimeSpan.Zero);
        wheels.Columns[0].ItemsSource = Enumerable.Range(1, 20).Select(number => number.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        wheels.Columns[0].SelectedIndex = 0;
        Place(wheels);

        Assert.True(Key(wheels, SkUiKey.PageDown));
        Assert.Equal(4, wheels.Columns[0].SelectedIndex);
        Assert.True(Key(wheels, SkUiKey.End));
        Assert.Equal(19, wheels.Columns[0].SelectedIndex);
        Assert.True(Key(wheels, SkUiKey.PageUp));
        Assert.Equal(15, wheels.Columns[0].SelectedIndex);
        Assert.True(Key(wheels, SkUiKey.Home));
        Assert.Equal(0, wheels.Columns[0].SelectedIndex);
    }

    private sealed class FuncConverter(Func<object?, bool> convert) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => convert(value);

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }
}

/// <summary><see cref="SkUiPickerWheels"/> in XAML: columns as content, bound to the view model.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class PickerWheelsXamlTests
{
    public sealed class Model
    {
        public int Count { get; set; } = 2;
    }

    [Fact]
    public void ColumnsAreTheContent()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        const string xaml = """
            <VerticalStackLayout xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                                 xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiPickerWheels VisibleRows="3" RowHeight="40" CommitDelay="0:0:0.2">
                <sk:SkUiPickerColumn SelectedIndex="{Binding Count}">
                  <sk:SkUiPickerColumn.ItemsSource>
                    <x:Array Type="{x:Type x:String}">
                      <x:String>1</x:String>
                      <x:String>2</x:String>
                      <x:String>3</x:String>
                    </x:Array>
                  </sk:SkUiPickerColumn.ItemsSource>
                </sk:SkUiPickerColumn>
              </sk:SkUiPickerWheels>
            </VerticalStackLayout>
            """;
        var root = new VerticalStackLayout { BindingContext = new Model() };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var wheels = Assert.IsType<SkUiPickerWheels>(root.Children[0]);

        Assert.Equal((3, 40d, TimeSpan.FromMilliseconds(200)), (wheels.VisibleRows, wheels.RowHeight, wheels.CommitDelay));
        Assert.Equal(2, wheels.Columns[0].SelectedIndex);
        Assert.Equal(2, wheels.Wheels.Wheel(0).Position);
    }
}
