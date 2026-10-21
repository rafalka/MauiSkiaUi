using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The wheels of a picker's popup as screen readers read them: one adjustable element per wheel (named by its column, the middle
/// row's item and its place as the value, its items not elements), stepping and setting through the semantics owner, passing items
/// that cannot be chosen, the date and time pickers' wheels named also without headers, on both layers.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerWheelSemanticsTests : IDisposable
{
    private static readonly string[] Hours = ["09", "10", "11"];
    private static readonly string[] Minutes = ["00", "15", "30", "45"];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;
    private readonly SkUiStrings _strings = SkUiStrings.Current;
    private readonly IDisposable _culture = DateTimeWheelsTests.UseCulture("en-US");

    public PickerWheelSemanticsTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        SkUiStrings.Current = _strings;
        _culture.Dispose();
        _ui.Dispose();
    }

    private static void Arrange(SkUiView picker)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(picker);
        SkUiTestHelpers.Arrange(stack, 360, 700);
    }

    private SkUiColumnsPopup Shown() => Assert.IsType<SkUiColumnsPopup>(_presenter.Current!.Content);

    /// <summary>The popup's wheel elements, in reading order.</summary>
    private static List<SkUiSemanticsNode> Wheels(SkUiColumnsPopup content) =>
        [.. SkUiSemanticsTree.Build(content).Flatten().Where(node => node.Source is SkUiCarouselView)];

    private static SkUiMultiColumnPicker TimePicker()
    {
        var picker = new SkUiMultiColumnPicker { Title = "Time", Separator = ":" };
        picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Hours, Header = "Hour" });
        picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Minutes, Loop = true }.SetSemanticDescription("Minute").SetSemanticHint("Swipe to change"));
        Arrange(picker);
        return picker;
    }

    [Fact]
    public void EachWheelIsOneAdjustableElement()
    {
        var picker = TimePicker();
        picker.Columns[0].SelectedIndex = 1;
        picker.IsOpen = true;
        var content = Shown();
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        var tree = SkUiSemanticsTree.Build(content);
        var wheels = Wheels(content);
        Assert.Equal(2, wheels.Count);
        var (hour, minute) = (wheels[0], wheels[1]);
        Assert.All(wheels, wheel => Assert.Equal(SkUiSemanticsRole.Slider, wheel.Role));
        Assert.Equal("Hour", hour.Label);
        Assert.Equal("10, 2 of 3", hour.Value);
        Assert.Equal(new SkUiSemanticsRange(0, 2, 1), hour.Range);
        Assert.Equal(SkUiSemanticsActions.Increment | SkUiSemanticsActions.Decrement, hour.Actions);
        // A column without a header is named by its semantic description, with its hint.
        Assert.Equal("Minute", minute.Label);
        Assert.Equal("Swipe to change", minute.Hint);
        Assert.Equal("00, 1 of 4", minute.Value);
        // The items are not elements of their own, and the header is read as the wheel's name only.
        Assert.All(wheels, wheel => Assert.Empty(wheel.Children));
        Assert.DoesNotContain(tree.Flatten(), node => node.Label == "Hour" && node.Role == SkUiSemanticsRole.Text);
        Assert.DoesNotContain(tree.Flatten(), node => node.Label is "09" or "10" or "11");
        // Where the wheel is drawn.
        Assert.Equal(SkUiDiagnostics.GetRootBounds(content.Wheel(0))!.Value.Center.X, hour.Bounds.Center.X, 1);
        Assert.Equal(hour.Id, tree.HitTest(hour.Bounds.Center)?.Id);
    }

    [Fact]
    public void StepsAndValuesChooseItems()
    {
        var picker = TimePicker();
        picker.IsOpen = true;
        var content = Shown();
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();
        var owner = new SkUiSemanticsOwner(content);
        var hour = Wheels(content)[0];

        // The first item: no previous one on a wheel that stops at its ends.
        Assert.Equal(SkUiSemanticsActions.Increment, hour.Actions);
        Assert.False(owner.Perform(hour.Id, SkUiSemanticsActions.Decrement));
        Assert.True(owner.Perform(hour.Id, SkUiSemanticsActions.Increment));
        // Chosen at once, with the minutes the other wheel shows.
        Assert.Equal([1, 0], picker.SelectedIndices);
        Assert.Equal(1, content.Wheel(0).Position);
        Assert.Equal("10, 2 of 3", Wheels(content)[0].Value);

        Assert.True(owner.SetValue(hour.Id, 2));
        Assert.Equal([2, 0], picker.SelectedIndices);
        Assert.Equal(SkUiSemanticsActions.Decrement, Wheels(content)[0].Actions);
        Assert.False(owner.SetValue(hour.Id, 7)); // clamped to the last item, already shown

        // A looping wheel goes around its end.
        var minute = Wheels(content)[1];
        Assert.True(owner.Perform(minute.Id, SkUiSemanticsActions.Decrement));
        Assert.Equal([2, 3], picker.SelectedIndices);
        Assert.Equal("45, 4 of 4", Wheels(content)[1].Value);
    }

    [Fact]
    public void ItemsThatCannotBeChosenArePassed()
    {
        var products = PickerDisabledItemsTests.Products();
        var column = new SkUiPickerColumn
        {
            ItemsSource = products, SelectedIndex = 0,
            ItemEnabledBinding = Binding.Create(static (PickerDisabledItemsTests.Product product) => product.InStock),
            ItemDisplayBinding = Binding.Create(static (PickerDisabledItemsTests.Product product) => product.Name)
        };
        var picker = new SkUiMultiColumnPicker().AddColumn(column);
        Arrange(picker);
        picker.IsOpen = true;
        var content = Shown();
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();
        var owner = new SkUiSemanticsOwner(content);
        var wheel = Wheels(content)[0];
        var next = products.FindIndex(1, product => product.InStock);
        Assert.True(products[0].InStock);
        Assert.False(products[1].InStock); // the next item cannot be chosen: the step passes it

        Assert.True(owner.Perform(wheel.Id, SkUiSemanticsActions.Increment));
        Assert.Equal(next, column.SelectedIndex);
        // A value on an item that cannot be chosen moves on to the nearest one that can, towards the value.
        Assert.True(owner.SetValue(wheel.Id, 0));
        Assert.Equal(0, column.SelectedIndex);
        Assert.True(owner.SetValue(wheel.Id, 1));
        Assert.Equal(next, column.SelectedIndex);
    }

    [Fact]
    public void DateWheelsAreNamedWithoutHeadersAndFollowTheRules()
    {
        var picker = new SkUiDatePicker { WheelFormat = "MMMM d yyyy", Date = new DateTime(2026, 1, 31) };
        Arrange(picker);
        picker.IsOpen = true;
        var content = Shown();
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();
        var owner = new SkUiSemanticsOwner(content);

        var wheels = Wheels(content);
        Assert.Equal(["Month", "Day", "Year"], wheels.Select(wheel => wheel.Label));
        Assert.Equal("January, 1 of 12", wheels[0].Value);
        Assert.Equal("31, 31 of 31", wheels[1].Value);

        // February has no 31st: the day moves to the 28th, as when the wheel is turned.
        Assert.True(owner.Perform(wheels[0].Id, SkUiSemanticsActions.Increment));
        Assert.Equal(new DateTime(2026, 2, 28), picker.Date);
        wheels = Wheels(content);
        Assert.Equal("February, 2 of 12", wheels[0].Value);
        Assert.Equal("28, 28 of 31", wheels[1].Value);
    }

    [Fact]
    public void TheCorePickersWheelsAreTheSame()
    {
        var core = new SkUiCoreMultiColumnPicker { Title = "Time" };
        core.AddColumn(new SkUiPickerColumn { ItemsSource = Hours }.SetSemanticDescription("Hour"));
        var time = new SkUiCoreTimePicker().SetTime(new TimeSpan(7, 30, 0)).SetWheelFormat("HH:mm");
        var host = new SkUiCoreHost().SetContent(new SkUiCoreVerticalStackLayout().Add(core).Add(time));
        SkUiTestHelpers.Arrange(host, 360, 700);

        core.IsOpen = true;
        var content = Shown();
        using (var surface = new SkUiTestSurface(content, 360, 400))
        {
            surface.Frame();
            var wheel = Assert.Single(Wheels(content));
            Assert.Equal("Hour", wheel.Label);
            Assert.Equal("09, 1 of 3", wheel.Value);
            Assert.True(new SkUiSemanticsOwner(content).Perform(wheel.Id, SkUiSemanticsActions.Increment));
            Assert.Equal([1], core.SelectedIndices);
        }
        core.IsOpen = false;
        _ui.RunPending();

        time.SetIsOpen(true);
        content = Shown();
        using (var surface = new SkUiTestSurface(content, 360, 400))
        {
            surface.Frame();
            var wheels = Wheels(content);
            Assert.Equal(["Hour", "Minute"], wheels.Select(wheel => wheel.Label));
            Assert.True(new SkUiSemanticsOwner(content).Perform(wheels[1].Id, SkUiSemanticsActions.Increment));
            Assert.Equal(new TimeSpan(7, 31, 0), time.Time);
        }
    }

    [Fact]
    public void ThePlaceIsTranslated()
    {
        SkUiStrings.Current = new SkUiStrings { WheelPositionFormat = "{0} z {1}" };
        var picker = TimePicker();
        picker.IsOpen = true;
        var content = Shown();
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        Assert.Equal("09, 1 z 3", Wheels(content)[0].Value);
    }
}
