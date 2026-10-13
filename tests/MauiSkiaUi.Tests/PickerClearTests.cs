using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Clearing a picker's choice: the field's clear button (tap, Delete / Backspace), the popup's Clear button in both update modes,
/// <see cref="SkUiPopupRequest.Clear"/> for presenters, <see cref="SkUiPickerBase.ClearSelection"/>, on every kind of picker.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerClearTests : IDisposable
{
    private static readonly string[] Fruits = ["Apple", "Pear", "Plum", "Fig"];
    private static int _pointer = 9000;

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public PickerClearTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    private static SkUiVerticalStackLayout Root(SkUiView view)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(view);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        return stack;
    }

    /// <summary>A tap at a point of the picker (its coordinates).</summary>
    private static void TapAt(SkUiView root, SkUiView picker, double x, double y)
    {
        var bounds = SkUiDiagnostics.GetRootBounds(picker)!.Value;
        var at = new Point(bounds.X + x, bounds.Y + y);
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    /// <summary>The middle of the field's clear button: before the chevron, at the end.</summary>
    private static (double X, double Y) ClearButton(SkUiView picker)
    {
        var look = SkUiLook.Current;
        return (picker.Width - look.PickerChevronWidth - look.PickerClearButtonWidth / 2, picker.Height / 2);
    }

    private static IEnumerable<ISkUiView> Descendants(ISkUiView view)
    {
        yield return view;
        if (view is SkUiView node)
            foreach (var child in node.SkiaChildren)
                foreach (var descendant in Descendants(child))
                    yield return descendant;
    }

    private static SkUiButton FooterButton(SkUiView content, string text) =>
        Descendants(content).OfType<SkUiButton>().Single(button => button.Text == text);

    [Fact]
    public void TheFieldsClearButtonClearsWithoutOpening()
    {
        var picker = new SkUiPicker { ItemsSource = Fruits, IsClearButtonVisible = true };
        var root = Root(picker);
        var events = 0;
        picker.SelectedIndexChanged += (_, _) => events++;

        // Nothing chosen: no clear button, a tap there opens.
        var (x, y) = ClearButton(picker);
        TapAt(root, picker, x, y);
        Assert.True(picker.IsOpen);
        picker.IsOpen = false;
        _ui.RunPending();

        picker.SelectedIndex = 2;
        SkUiTestHelpers.Arrange(root, 360, 700);
        (x, y) = ClearButton(picker);
        TapAt(root, picker, x, y);
        _ui.RunPending();

        Assert.Equal(-1, picker.SelectedIndex);
        Assert.False(picker.IsOpen);
        Assert.Equal(2, events);

        // A tap on the text still opens.
        picker.SelectedIndex = 1;
        TapAt(root, picker, 20, picker.Height / 2);
        Assert.True(picker.IsOpen);
    }

    [Fact]
    public void DeleteAndBackspaceClearTheFocusedField()
    {
        var picker = new SkUiPicker { ItemsSource = Fruits, IsClearButtonVisible = true, SelectedIndex = 1 };
        var root = Root(picker);
        Assert.True(picker.Focus());

        Assert.True(root.FocusManager.KeyDown(SkUiKey.Delete));
        Assert.Equal(-1, picker.SelectedIndex);
        Assert.False(root.FocusManager.KeyDown(SkUiKey.Backspace)); // nothing left to clear

        picker.IsClearButtonVisible = false;
        picker.SelectedIndex = 2;
        Assert.False(root.FocusManager.KeyDown(SkUiKey.Backspace)); // no clear button: the key is not the picker's
        Assert.Equal(2, picker.SelectedIndex);
    }

    [Fact]
    public void TheClearButtonTakesRoomInTheField()
    {
        var picker = new SkUiPicker { ItemsSource = Fruits, SelectedIndex = 0, HorizontalOptions = LayoutOptions.Start };
        var root = Root(picker);
        var without = picker.Width;

        picker.IsClearButtonVisible = true;
        SkUiTestHelpers.Arrange(root, 360, 700);

        Assert.Equal(without + SkUiLook.Current.PickerClearButtonWidth, picker.Width, 0.5);
    }

    [Fact]
    public void ThePopupsClearClosesAListOfOneChoice()
    {
        var picker = new SkUiPicker { ItemsSource = Fruits, SelectedIndex = 3, IsPopupClearButtonVisible = true };
        Root(picker);
        var events = new List<string>();
        picker.SelectedIndexChanged += (_, _) => events.Add($"SelectedIndexChanged {picker.SelectedIndex}");
        picker.Closed += (_, _) => events.Add("Closed");

        picker.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();
        Assert.Equal(["Clear"], Descendants(content).OfType<SkUiButton>().Select(button => button.Text));
        Assert.NotNull(SkUiDiagnostics.SimulateTap(FooterButton(content, "Clear")));
        _ui.RunPending();

        Assert.Equal(-1, picker.SelectedIndex);
        Assert.Equal(["SelectedIndexChanged -1", "Closed"], events);
    }

    [Fact]
    public void ThePopupsClearIsADraftUntilOk()
    {
        var picker = new SkUiPicker
        {
            ItemsSource = Fruits, SelectedIndex = 1, IsPopupClearButtonVisible = true, UpdateMode = SkUiPickerUpdateMode.WhenFinished, ClearText = "None"
        };
        Root(picker);

        // Clear, then Cancel: kept.
        picker.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);
        using (var surface = new SkUiTestSurface(content, 360, 400))
        {
            surface.Frame();
            Assert.Equal(["None", "Cancel", "OK"], Descendants(content).OfType<SkUiButton>().Select(button => button.Text));
            Assert.NotNull(SkUiDiagnostics.SimulateTap(FooterButton(content, "None")));
            Assert.True(picker.IsOpen);
            Assert.Equal(-1, ((SkUiListPopupRequest)_presenter.Current).CurrentIndex);
            Assert.Equal(-1, content.HighlightedIndex);
            Assert.NotNull(SkUiDiagnostics.SimulateTap(FooterButton(content, "Cancel")));
            _ui.RunPending();
        }
        Assert.Equal(1, picker.SelectedIndex);

        // Clear, then OK: cleared.
        picker.IsOpen = true;
        content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);
        using (var surface = new SkUiTestSurface(content, 360, 400))
        {
            surface.Frame();
            Assert.NotNull(SkUiDiagnostics.SimulateTap(FooterButton(content, "None")));
            Assert.NotNull(SkUiDiagnostics.SimulateTap(FooterButton(content, "OK")));
            _ui.RunPending();
        }
        Assert.Equal(-1, picker.SelectedIndex);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void ClearingSeveralChoicesAndWheels()
    {
        var multiple = new SkUiPicker { ItemsSource = Fruits, SelectionMode = SkUiSelectionMode.Multiple, IsPopupClearButtonVisible = true };
        multiple.SelectedItems.Add("Pear");
        multiple.SelectedItems.Add("Fig");
        Root(multiple);
        multiple.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);
        using (var surface = new SkUiTestSurface(content, 360, 400))
        {
            surface.Frame();
            Assert.NotNull(SkUiDiagnostics.SimulateTap(FooterButton(content, "Clear")));
        }
        Assert.Empty(multiple.SelectedIndices); // applied at once, the list stays open
        Assert.True(multiple.IsOpen);
        Assert.Empty(content.List.SelectedItems);

        var wheels = new SkUiMultiColumnPicker { UpdateMode = SkUiPickerUpdateMode.WhenFinished, IsPopupClearButtonVisible = true }
            .AddColumn(new SkUiPickerColumn { ItemsSource = Fruits, SelectedIndex = 2 })
            .AddColumn(new SkUiPickerColumn { ItemsSource = Fruits, SelectedIndex = 0 });
        Root(wheels);
        wheels.IsOpen = true;
        var request = Assert.IsType<SkUiColumnsPopupRequest>(_presenter.Current);
        // Wheels cannot show that nothing is chosen: Clear chooses nothing and closes, also when waiting for OK.
        Assert.True(request.Clear());
        Assert.True(request.IsCompleted);
        _ui.RunPending();
        Assert.Equal([-1, -1], wheels.SelectedIndices);
        Assert.False(wheels.IsOpen);
    }

    [Fact]
    public void WheelsWithNothingChosenChooseWhatTheyShow()
    {
        string[] hours = ["00", "01", "02"];
        string[] minutes = ["00", "15", "30"];
        var wheels = new SkUiMultiColumnPicker { Format = "{0}:{1}" }
            .AddColumn(new SkUiPickerColumn { ItemsSource = hours })
            .AddColumn(new SkUiPickerColumn { ItemsSource = minutes });
        Root(wheels);

        // At once: moving the hours chooses the minutes the other wheel shows too, never "01:".
        wheels.IsOpen = true;
        var content = Assert.IsType<SkUiColumnsPopup>(_presenter.Current!.Content);
        using (var surface = new SkUiTestSurface(content, 360, 400))
        {
            surface.Frame();
            Assert.Equal([-1, -1], wheels.SelectedIndices); // opening chooses nothing
            content.Wheel(0).Position = 1;
            surface.Frame();
        }
        Assert.Equal([1, 0], wheels.SelectedIndices);
        Assert.Equal("01:00", SkUiSemanticsTree.Build((SkUiView)wheels.Parent!).Flatten().Single(node => ReferenceEquals(node.Source, wheels)).Value);
        wheels.IsOpen = false;
        _ui.RunPending();

        // Waiting for OK: OK chooses what the wheels show.
        wheels.ClearSelection();
        wheels.UpdateMode = SkUiPickerUpdateMode.WhenFinished;
        wheels.IsOpen = true;
        var request = Assert.IsType<SkUiColumnsPopupRequest>(_presenter.Current);
        Assert.Equal([0, 0], request.SelectedIndices);
        Assert.Equal([-1, -1], wheels.SelectedIndices); // a draft
        Assert.True(request.Accept());
        _ui.RunPending();
        Assert.Equal([0, 0], wheels.SelectedIndices);
    }

    [Fact]
    public void APresentersOwnListClearsWithMinusOne()
    {
        var picker = new SkUiPicker { ItemsSource = Fruits, SelectedIndex = 2 };
        Root(picker);
        picker.IsOpen = true;
        var request = Assert.IsType<SkUiListPopupRequest>(_presenter.Current);

        Assert.False(request.Complete(9));
        Assert.True(request.Complete(-1));
        _ui.RunPending();

        Assert.Equal(-1, picker.SelectedIndex);
    }

    [Fact]
    public void ClearSelectionClearsEveryKindOfPicker()
    {
        var wheels = new SkUiMultiColumnPicker().AddColumn(new SkUiPickerColumn { ItemsSource = Fruits, SelectedIndex = 1 });
        wheels.ClearSelection();
        Assert.Equal([-1], wheels.SelectedIndices);

        var core = new SkUiCorePicker().SetIsClearButtonVisible(true);
        foreach (var fruit in Fruits)
            core.Items.Add(fruit);
        core.SetSelectedIndex(2);
        var host = new SkUiCoreHost().SetContent(core);
        var root = Root(host);
        var (x, y) = (core.Frame.Width - SkUiLook.Current.PickerChevronWidth - SkUiLook.Current.PickerClearButtonWidth / 2, core.Frame.Height / 2);
        TapAt(root, host, x, y);
        Assert.Equal(-1, core.SelectedIndex);
        Assert.False(core.IsOpen);

        var coreWheels = new SkUiCoreMultiColumnPicker().AddColumn(new SkUiPickerColumn { ItemsSource = Fruits, SelectedIndex = 3 });
        coreWheels.ClearSelection();
        Assert.Equal([-1], coreWheels.SelectedIndices);
    }

    [Fact]
    public void TheClearTextIsTranslatable()
    {
        var original = SkUiStrings.Current;
        try
        {
            SkUiStrings.Current = new SkUiStrings { ClearText = "Wyczyść" };
            var picker = new SkUiPicker { ItemsSource = Fruits, IsPopupClearButtonVisible = true };
            Root(picker);
            picker.IsOpen = true;
            Assert.Equal(["Wyczyść"], Descendants((SkUiView)_presenter.Current!.Content!).OfType<SkUiButton>().Select(button => button.Text));
        }
        finally
        {
            SkUiStrings.Current = original;
        }
    }
}
