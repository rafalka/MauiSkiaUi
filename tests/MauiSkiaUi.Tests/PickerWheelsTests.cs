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
