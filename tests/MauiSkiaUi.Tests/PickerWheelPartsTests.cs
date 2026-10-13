using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The wheels' parts: column headers, item templates (the middle item <c>Selected</c>), <see cref="SkUiPickerColumn.Range"/> and
/// <see cref="SkUiPickerColumn.ItemDisplay"/>, and the look's wheel and footer settings.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerWheelPartsTests : IDisposable
{
    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;
    private readonly SkUiLook _look = SkUiLook.Current;

    public PickerWheelPartsTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        SkUiLook.Current = _look;
        _ui.Dispose();
    }

    private static SkUiColumnsPopup Open(SkUiMultiColumnPicker picker, RecordingPopupPresenter presenter)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(picker);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        picker.IsOpen = true;
        return Assert.IsType<SkUiColumnsPopup>(presenter.Current!.Content);
    }

    private static IEnumerable<ISkUiView> Descendants(ISkUiView view)
    {
        yield return view;
        if (view is SkUiView node)
            foreach (var child in node.SkiaChildren)
                foreach (var descendant in Descendants(child))
                    yield return descendant;
    }

    [Fact]
    public void RangeMakesSteppedNumbers()
    {
        var minutes = SkUiPickerColumn.Range(0, 55, 5, "00");

        Assert.Equal(12, minutes.Count);
        Assert.Equal("05", minutes.Texts[1]);
        Assert.Equal("55", minutes.Texts[^1]);
        minutes.SelectedItem = 30;
        Assert.Equal(6, minutes.SelectedIndex);
        Assert.Equal(30, minutes.SelectedItem);
        Assert.Throws<ArgumentOutOfRangeException>(() => SkUiPickerColumn.Range(0, 10, 0));

        var hours = new SkUiPickerColumn { ItemsSource = new[] { 1, 2 }, ItemDisplay = item => $"{item} h" };
        Assert.Equal(["1 h", "2 h"], hours.Texts);
    }

    [Fact]
    public void HeadersShowAboveTheWheels()
    {
        var picker = new SkUiMultiColumnPicker()
            .AddColumn(SkUiPickerColumn.Range(0, 23).SetHeader("Hour"))
            .AddColumn(SkUiPickerColumn.Range(0, 59).SetHeader("Minute"));

        var content = Open(picker, _presenter);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        Assert.Equal(["Hour", "Minute"], Assert.IsType<SkUiColumnsPopupRequest>(_presenter.Current).Headers);
        var headers = Descendants(content).OfType<SkUiLabel>().Where(label => label.Text is "Hour" or "Minute").ToList();
        Assert.Equal(2, headers.Count);
        // Above the wheels.
        Assert.True(SkUiDiagnostics.GetRootBounds(headers[0])!.Value.Bottom <= SkUiDiagnostics.GetRootBounds(content.Wheel(0))!.Value.Top + 0.5);
        Assert.True(_presenter.Current!.PreferredSize.Height > 5 * SkUiLook.Current.PickerWheelRowHeight + headers[0].Height - 1);
    }

    [Fact]
    public void TheMiddleItemOfATemplatedWheelIsSelected()
    {
        var column = SkUiPickerColumn.Range(1, 10);
        column.ItemTemplate = new DataTemplate(() =>
        {
            var view = new SkUiContentView { HeightRequest = 30, Content = new SkUiLabel() };
            view.BindingContextChanged += (_, _) => ((SkUiLabel)view.Content!).Text = view.BindingContext?.ToString();
            return view;
        });
        column.SelectedIndex = 3;
        var picker = new SkUiMultiColumnPicker().AddColumn(column);
        var content = Open(picker, _presenter);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        SkUiContentView Root(int index) => Descendants(Assert.IsAssignableFrom<SkUiView>(content.Wheel(0).GetRealizedView(index))).OfType<SkUiContentView>().Last();
        Assert.True(Root(3).IsSelectedItem);
        Assert.False(Root(2).IsSelectedItem);
        Assert.Equal(4, Root(3).BindingContext); // bound to the column's item

        content.Wheel(0).Position = 4;
        surface.Frame();
        Assert.True(Root(4).IsSelectedItem);
        Assert.False(Root(3).IsSelectedItem);
    }

    [Fact]
    public void TheLookSetsTheWheelsAndTheFootersButtons()
    {
        SkUiLook.Current = new CustomLook();
        var picker = new SkUiMultiColumnPicker { UpdateMode = SkUiPickerUpdateMode.WhenFinished }.AddColumn(SkUiPickerColumn.Range(1, 5));

        var content = Open(picker, _presenter);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        Assert.Equal(30, content.Wheel(0).ItemExtent);
        var buttons = Descendants(content).OfType<SkUiButton>().ToList();
        Assert.Equal(["OK", "Cancel"], buttons.Select(button => button.Text)); // OK first, as Windows'
        Assert.All(buttons, button => Assert.Equal(Colors.Purple, button.TextColor));
        Assert.Contains(Descendants(content).OfType<SkUiBox>(), box => box.Color == Colors.Orange);
    }

    private sealed class CustomLook : WindowsSkUiLook
    {
        public override double PickerWheelRowHeight => 30;

        public override Color PickerWheelBandColor => Colors.Orange;

        public override void StylePopupButton(SkUiButton button, SkUiPopupButton kind) => button.TextColor = Colors.Purple;
    }
}
