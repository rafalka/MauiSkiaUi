using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Picker options of every kind of picker: the placeholder, the popup's width, the selection command, closing after a toggle.</summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerOptionsTests : IDisposable
{
    private static readonly string[] Fruits = ["Apple", "Pear", "Plum", "Fig"];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public PickerOptionsTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    private static T Arranged<T>(T view) where T : SkUiView
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(view);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        return view;
    }

    [Fact]
    public void ThePlaceholderIsTheFieldsHintAndTheTitleThePopups()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var picker = new SkUiPicker { ItemsSource = Fruits, Title = "Fruit", FontFamily = SkUiTestHelpers.BundledFontFamily, HorizontalOptions = LayoutOptions.Start };
        Arranged(picker);
        var withTitle = picker.Width;

        picker.Placeholder = "Choose the fruit of the day";
        Arranged((SkUiVerticalStackLayout)picker.Parent!);
        Assert.True(picker.Width > withTitle); // the field measures the placeholder

        picker.PopupPlacement = SkUiPopupPlacement.Window;
        picker.IsOpen = true;
        Assert.Equal("Fruit", _presenter.Current!.Title);
        var element = SkUiSemanticsTree.Build((SkUiView)picker.Parent!).Flatten().Single(node => ReferenceEquals(node.Source, picker));
        Assert.Equal("Fruit", element.Label);
    }

    [Fact]
    public void ThePopupWidthIsTheAppsWithinTheWindow()
    {
        var picker = Arranged(new SkUiPicker { ItemsSource = Fruits, PopupWidth = 180, PopupPlacement = SkUiPopupPlacement.Anchored });

        picker.IsOpen = true;

        Assert.Equal(180, _presenter.Current!.Bounds.Width);
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SetPopupWidth(-5));
    }

    [Fact]
    public void TheSelectionCommandRunsBeforeTheEvent()
    {
        var log = new List<string>();
        var command = new Command<object?>(parameter => log.Add($"command {parameter}"));
        var single = new SkUiPicker { ItemsSource = Fruits, SelectionChangedCommand = command, SelectionChangedCommandParameter = "single" };
        single.SelectedIndexChanged += (_, _) => log.Add("event");
        single.SelectedIndex = 2;
        Assert.Equal(["command single", "event"], log);

        log.Clear();
        var multiple = new SkUiPicker { ItemsSource = Fruits, SelectionMode = SkUiSelectionMode.Multiple, SelectionChangedCommand = command };
        multiple.SelectionChanged += (_, _) => log.Add("event");
        multiple.SelectedItems.Add("Fig");
        Assert.Equal(["command ", "event"], log);

        log.Clear();
        var columns = new SkUiMultiColumnPicker { SelectionChangedCommand = command, SelectionChangedCommandParameter = 7 }
            .AddColumn(new SkUiPickerColumn { ItemsSource = Fruits });
        columns.SelectionChanged += (_, _) => log.Add("event");
        columns.Columns[0].SelectedIndex = 1;
        Assert.Equal(["command 7", "event"], log);

        log.Clear();
        var core = new SkUiCorePicker().SetSelectionChangedCommand(command).SetSelectionChangedCommandParameter("core");
        foreach (var fruit in Fruits)
            core.Items.Add(fruit);
        core.SelectedIndexChanged += (_, _) => log.Add("event");
        core.SetSelectedIndex(3);
        Assert.Equal(["command core", "event"], log);

        // A command that cannot execute is skipped.
        log.Clear();
        single.SelectionChangedCommand = new Command(() => log.Add("never"), () => false);
        single.SelectedIndex = 0;
        Assert.Equal(["event"], log);
    }

    [Fact]
    public void SeveralChoicesCanCloseAfterEachToggle()
    {
        var picker = Arranged(new SkUiPicker { ItemsSource = Fruits, SelectionMode = SkUiSelectionMode.Multiple, IsPopupClosedOnSelection = true });
        picker.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        Assert.NotNull(SkUiDiagnostics.SimulateTap(Assert.IsAssignableFrom<SkUiView>(content.List.GetRealizedView(1))));
        _ui.RunPending();

        Assert.False(picker.IsOpen);
        Assert.Equal([1], picker.SelectedIndices);
    }
}
