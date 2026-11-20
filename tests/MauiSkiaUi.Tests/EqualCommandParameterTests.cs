using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Issue #114: a command parameter replaced by another instance that <see cref="object.Equals(object)"/> the old one (a list row
/// view model replaced by a new instance with the same Id) reaches the command. MAUI raises no property change for an Equals
/// value, so the SkUi* controls read the parameter when the command runs; Core setters skip only the same object or an equal
/// plain value or string.
/// </summary>
public class EqualCommandParameterTests
{
    private sealed class Row(string id, string label)
    {
        private string Id { get; } = id;
        public string Label { get; } = label;
        public override bool Equals(object? obj) => obj is Row other && other.Id == Id;
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => Label;
    }

    private static readonly Row First = new("1", "first");
    private static readonly Row Second = new("1", "second");

    private static Command<Row> Recording(List<string> log) => new(row => log.Add(row.Label), row => row is not null);

    private static T OnSurface<T>(T view) where T : SkUiView
    {
        var root = new SkUiContentView { Content = view };
        SkUiTestHelpers.Arrange(root, 200, 100);
        return view;
    }

    [Fact]
    public void ButtonsRunTheirCommandWithTheCurrentParameter()
    {
        Assert.Equal(First, Second);
        var log = new List<string>();
        var button = OnSurface(new SkUiButton { Text = "Tap", Command = Recording(log), CommandParameter = First, WidthRequest = 80, HeightRequest = 40 });
        var image = new SkUiImageButton { Command = Recording(log), CommandParameter = First, WidthRequest = 40, HeightRequest = 40 };
        OnSurface(image);

        button.CommandParameter = Second;
        image.CommandParameter = Second;
        Assert.Same(Second, button.CommandParameter);
        Assert.NotNull(SkUiDiagnostics.SimulateTap(button));
        Assert.NotNull(SkUiDiagnostics.SimulateTap(image));

        Assert.Equal(["second", "second"], log);
    }

    [Fact]
    public void TheTappedCommandOfAViewRunsWithTheCurrentParameter()
    {
        var log = new List<string>();
        var box = OnSurface(new SkUiBox { WidthRequest = 50, HeightRequest = 50, TappedCommand = Recording(log), TappedCommandParameter = First });

        box.TappedCommandParameter = Second;
        Assert.NotNull(SkUiDiagnostics.SimulateTap(box));

        Assert.Equal(["second"], log);
    }

    [Fact]
    public void DoubleTapAndLongPressCommandsRunWithTheCurrentParameter()
    {
        using var clock = new ManualGestureClock();
        var log = new List<string>();
        var box = new SkUiBox
        {
            WidthRequest = 100, HeightRequest = 100, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            DoubleTappedCommand = Recording(log), DoubleTappedCommandParameter = First,
            LongPressedCommand = Recording(log), LongPressedCommandParameter = First,
        };
        var root = new SkUiContentView { Content = box };
        SkUiTestHelpers.Arrange(root, 200, 200);
        box.DoubleTappedCommandParameter = Second;
        box.LongPressedCommandParameter = new Row("1", "held");

        root.Touch(new(1, SkUiTouchAction.Pressed, new Point(10, 10), TimeSpan.Zero));
        root.Touch(new(1, SkUiTouchAction.Released, new Point(10, 10), TimeSpan.FromMilliseconds(30)));
        root.Touch(new(2, SkUiTouchAction.Pressed, new Point(12, 12), TimeSpan.FromMilliseconds(150)));
        root.Touch(new(2, SkUiTouchAction.Released, new Point(12, 12), TimeSpan.FromMilliseconds(180)));
        clock.Advance(TimeSpan.FromSeconds(1));

        root.Touch(new(3, SkUiTouchAction.Pressed, new Point(10, 10), TimeSpan.FromSeconds(5)));
        clock.Advance(SkUiGestureSettings.LongPressDuration);
        root.Touch(new(3, SkUiTouchAction.Released, new Point(10, 10), TimeSpan.FromSeconds(6)));

        Assert.Equal(["second", "held"], log);
    }

    [Fact]
    public void TheSwipeCommandRunsWithTheCurrentParameter()
    {
        var log = new List<string>();
        var box = new SkUiBox
        {
            WidthRequest = 300, HeightRequest = 50, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            SwipeDirections = SwipeDirection.Left, SwipedCommand = Recording(log), SwipedCommandParameter = First,
        };
        var root = new SkUiContentView { Content = box };
        SkUiTestHelpers.Arrange(root, 300, 100);
        box.SwipedCommandParameter = Second;

        root.Touch(new(1, SkUiTouchAction.Pressed, new Point(250, 25), TimeSpan.Zero));
        for (var step = 1; step <= 10; step++)
            root.Touch(new(1, SkUiTouchAction.Moved, new Point(250 - step * 20, 25), TimeSpan.FromMilliseconds(step * 10)));
        root.Touch(new(1, SkUiTouchAction.Released, new Point(50, 25), TimeSpan.FromMilliseconds(110)));

        Assert.Equal(["second"], log);
    }

    [Fact]
    public void CheckBoxesAndSwipeItemsRunWithTheCurrentParameter()
    {
        var log = new List<string>();
        var box = OnSurface(new SkUiCheckBox { Command = Recording(log), CommandParameter = First });
        box.CommandParameter = Second;
        box.IsChecked = true;

        var item = new SkUiSwipeItemView { Command = Recording(log), CommandParameter = First, Content = new SkUiBox() };
        item.CommandParameter = Second;
        item.OnInvoked();

        Assert.Equal(["second", "second"], log);
    }

    [Fact]
    public void PickersRunTheSelectionCommandWithTheCurrentParameter()
    {
        var log = new List<string>();
        var picker = new SkUiPicker { ItemsSource = new[] { "a", "b", "c" }, SelectionChangedCommand = Recording(log), SelectionChangedCommandParameter = First };
        picker.SelectionChangedCommandParameter = Second;
        picker.SelectedIndex = 1;

        var core = new SkUiCorePicker().SetSelectionChangedCommand(Recording(log)).SetSelectionChangedCommandParameter(First);
        foreach (var item in new[] { "a", "b", "c" })
            core.Items.Add(item);
        core.SetSelectionChangedCommandParameter(Second);
        Assert.Same(Second, core.SelectionChangedCommandParameter);
        core.SetSelectedIndex(2);

        Assert.Equal(["second", "second"], log);
    }

    [Fact]
    public void CoreSettersKeepTheNewInstanceAndRunTheCommandWithIt()
    {
        var log = new List<string>();
        var button = new SkUiCoreButton().SetCommand(Recording(log)).SetCommandParameter(First);
        var image = new SkUiCoreImageButton().SetCommand(Recording(log)).SetCommandParameter(First);
        var check = new SkUiCoreCheckBox().SetCommand(Recording(log)).SetCommandParameter(First);
        var node = new SkUiCoreBox().SetTappedCommand(Recording(log)).SetTappedCommandParameter(First);
        var stack = new SkUiCoreVerticalStackLayout();
        foreach (var child in new SkUiCoreNode[] { button, image, check, node })
            stack.Add(child.SetWidth(60).SetHeight(30));
        SkUiTestHelpers.Arrange(new SkUiCoreHost().SetContent(stack), 200, 200);

        var changed = new List<string?>();
        button.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        button.SetCommandParameter(Second);
        image.SetCommandParameter(Second);
        check.SetCommandParameter(Second);
        node.SetTappedCommandParameter(Second);
        Assert.Equal([nameof(SkUiCoreButton.CommandParameter)], changed);
        Assert.Same(Second, button.CommandParameter);

        foreach (var child in new SkUiCoreNode[] { button, image, check, node })
            Assert.NotNull(SkUiDiagnostics.SimulateTap(child));
        Assert.Equal(["second", "second", "second", "second"], log);

        // Parameters the command reads later.
        var expander = new SkUiCoreExpander().SetCommandParameter(First).SetCommandParameter(Second);
        var search = new SkUiCoreSearchBar().SetSearchCommandParameter(First).SetSearchCommandParameter(Second);
        var entry = new SkUiCoreEntry().SetReturnCommandParameter(First).SetReturnCommandParameter(Second);
        var tiles = new SkUiCoreTileLayout().SetOverflowCommandParameter(First).SetOverflowCommandParameter(Second);
        Assert.Same(Second, expander.CommandParameter);
        Assert.Same(Second, search.SearchCommandParameter);
        Assert.Same(Second, entry.ReturnCommandParameter);
        Assert.Same(Second, tiles.OverflowCommandParameter);
    }

    private readonly struct RowValue(int id, string label)
    {
        private int Id { get; } = id;
        public string Label { get; } = label;
        public override bool Equals(object? obj) => obj is RowValue other && other.Id == Id;
        public override int GetHashCode() => Id;
    }

    [Fact]
    public void CoreSettersKeepAStructThatOnlyEqualsTheOldOne()
    {
        var button = new SkUiCoreButton().SetCommandParameter(new RowValue(1, "first"));
        button.SetCommandParameter(new RowValue(1, "second"));
        Assert.Equal("second", ((RowValue)button.CommandParameter!).Label);
    }

    [Fact]
    public void CoreSettersSkipAnEqualValueOrString()
    {
        var button = new SkUiCoreButton().SetCommandParameter(5);
        var expander = new SkUiCoreExpander().SetCommandParameter(new string('x', 3));
        var changed = new List<string?>();
        button.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        expander.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        button.SetCommandParameter(5); // another box of an equal int
        expander.SetCommandParameter(new string('x', 3)); // another instance of an equal string

        Assert.Empty(changed);
    }

    [Fact]
    public void ACheckBoxAsksCanExecuteWithTheCurrentParameter()
    {
        var log = new List<string>();
        var box = OnSurface(new SkUiCheckBox { Command = new Command<Row>(row => log.Add(row.Label), row => row?.Label != "locked"), CommandParameter = First });
        box.CommandParameter = new Row("1", "locked");

        SkUiDiagnostics.SimulateTap(box);

        Assert.False(box.IsChecked);
        Assert.Empty(log);
    }
}
