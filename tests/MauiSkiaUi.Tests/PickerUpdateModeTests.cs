using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <c>UpdateMode</c> <see cref="SkUiPickerUpdateMode.WhenFinished"/> on the three pickers: the choice made in the popup waits for
/// OK (the drawn footer, Enter, <see cref="SkUiPopupRequest.Accept"/>) and Cancel, Escape or a dismissal discard it; the
/// buttons' texts, app-wide and per picker.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerUpdateModeTests : IDisposable
{
    private static readonly string[] Monkeys = ["Baboon", "Capuchin Monkey", "Blue Monkey", "Squirrel Monkey"];
    private static readonly string[] Hours = ["09", "10", "11", "12"];
    private static readonly string[] Minutes = ["00", "15", "30", "45"];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;
    private readonly SkUiStrings _strings = SkUiStrings.Current;

    public PickerUpdateModeTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        SkUiStrings.Current = _strings;
        _ui.Dispose();
    }

    private static T Arranged<T>(T picker) where T : SkUiView
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(picker);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        return picker;
    }

    private static SkUiPicker Picker(SkUiSelectionMode mode = SkUiSelectionMode.Single)
    {
        var picker = new SkUiPicker { Title = "Monkey", SelectionMode = mode, UpdateMode = SkUiPickerUpdateMode.WhenFinished };
        foreach (var monkey in Monkeys)
            picker.Items.Add(monkey);
        return Arranged(picker);
    }

    private static SkUiMultiColumnPicker Columns()
    {
        var picker = new SkUiMultiColumnPicker { Title = "Time", Separator = ":", UpdateMode = SkUiPickerUpdateMode.WhenFinished };
        picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Hours, SelectedIndex = 0 });
        picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Minutes, SelectedIndex = 0 });
        return Arranged(picker);
    }

    private static IEnumerable<ISkUiView> Descendants(ISkUiView view)
    {
        yield return view;
        if (view is SkUiView node)
            foreach (var child in node.SkiaChildren)
                foreach (var descendant in Descendants(child))
                    yield return descendant;
    }

    private static SkUiButton[] Buttons(SkUiView content) => [.. Descendants(content).OfType<SkUiButton>()];

    private static void Tap(SkUiView view) => Assert.NotNull(SkUiDiagnostics.SimulateTap(view));

    [Fact]
    public void ATappedRowWaitsForOk()
    {
        var picker = Picker();
        var events = new List<string>();
        picker.SelectedIndexChanged += (_, _) => events.Add($"SelectedIndexChanged {picker.SelectedIndex}");
        picker.Closed += (_, _) => events.Add("Closed");

        picker.IsOpen = true;
        var request = Assert.IsType<SkUiListPopupRequest>(_presenter.Current);
        Assert.True(request.RequiresAccept);
        Assert.Equal(("OK", "Cancel"), (request.AcceptText, request.CancelText));
        var content = Assert.IsType<SkUiPickerPopup>(request.Content);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();
        var buttons = Buttons(content);
        Assert.Equal(["Cancel", "OK"], buttons.Select(button => button.Text));

        Tap(Assert.IsAssignableFrom<SkUiView>(content.List.GetRealizedView(2)));
        _ui.RunPending();
        Assert.True(picker.IsOpen);
        Assert.Equal(-1, picker.SelectedIndex);
        Assert.Equal(2, request.CurrentIndex);
        Assert.Equal(2, content.HighlightedIndex);

        Tap(buttons[1]);
        _ui.RunPending();
        Assert.False(picker.IsOpen);
        Assert.Equal(2, picker.SelectedIndex);
        Assert.Equal(["SelectedIndexChanged 2", "Closed"], events);
    }

    [Fact]
    public void CancelEscapeAndADismissalDiscardTheChoice()
    {
        var picker = Picker();
        picker.SelectedIndex = 1;

        // Cancel.
        picker.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);
        using (var surface = new SkUiTestSurface(content, 360, 400))
        {
            surface.Frame();
            Tap(Assert.IsAssignableFrom<SkUiView>(content.List.GetRealizedView(3)));
            Tap(Buttons(content)[0]);
            _ui.RunPending();
        }
        Assert.False(picker.IsOpen);
        Assert.Equal(1, picker.SelectedIndex);

        // Escape, a dismissal.
        picker.IsOpen = true;
        var keys = (ISkUiKeyHandler)_presenter.Current!.Content!;
        Assert.True(keys.OnKeyDown(new SkUiKeyEvent(SkUiKey.Down, SkUiKeyModifiers.None, false)));
        Assert.True(keys.OnKeyDown(new SkUiKeyEvent(SkUiKey.Escape, SkUiKeyModifiers.None, false)));
        _ui.RunPending();
        Assert.Equal(1, picker.SelectedIndex);
        picker.IsOpen = true;
        ((SkUiListPopupRequest)_presenter.Current!).SetCurrentIndex(0);
        _presenter.Dismiss();
        _ui.RunPending();
        Assert.Equal(1, picker.SelectedIndex);

        // Enter accepts the highlighted row.
        picker.IsOpen = true;
        keys = (ISkUiKeyHandler)_presenter.Current!.Content!;
        Assert.True(keys.OnKeyDown(new SkUiKeyEvent(SkUiKey.Down, SkUiKeyModifiers.None, false)));
        Assert.True(keys.OnKeyDown(new SkUiKeyEvent(SkUiKey.Enter, SkUiKeyModifiers.None, false)));
        _ui.RunPending();
        Assert.Equal(2, picker.SelectedIndex);
    }

    [Fact]
    public void SeveralChoicesAreADraftUntilAccepted()
    {
        var picker = Picker(SkUiSelectionMode.Multiple);
        picker.SelectedItems.Add("Baboon");
        var changes = 0;
        picker.SelectionChanged += (_, _) => changes++;

        picker.IsOpen = true;
        var request = Assert.IsType<SkUiMultiListPopupRequest>(_presenter.Current);
        Assert.True(request.SetSelected(2, true));
        Assert.True(request.SetSelected(0, false));
        Assert.Equal([2], request.SelectedIndices);
        Assert.Equal([0], picker.SelectedIndices); // not applied yet
        Assert.Equal(0, changes);

        _presenter.Dismiss();
        _ui.RunPending();
        Assert.Equal([0], picker.SelectedIndices);

        picker.IsOpen = true;
        request = Assert.IsType<SkUiMultiListPopupRequest>(_presenter.Current);
        request.SetSelected(3, true);
        Assert.True(request.Accept());
        _ui.RunPending();
        Assert.False(picker.IsOpen);
        Assert.Equal([0, 3], picker.SelectedIndices);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void TappedRowsWithSeveralChoicesWaitForOk()
    {
        var picker = Picker(SkUiSelectionMode.Multiple);
        picker.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        Tap(Assert.IsAssignableFrom<SkUiView>(content.List.GetRealizedView(1)));
        Tap(Assert.IsAssignableFrom<SkUiView>(content.List.GetRealizedView(3)));
        _ui.RunPending();
        Assert.Empty(picker.SelectedIndices);
        Tap(Buttons(content)[1]);
        _ui.RunPending();

        Assert.Equal([1, 3], picker.SelectedIndices);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void WheelsWaitForOk()
    {
        var picker = Columns();
        var changes = new List<int>();
        picker.SelectionChanged += (_, args) => changes.Add(args.ColumnIndex);

        picker.IsOpen = true;
        var request = Assert.IsType<SkUiColumnsPopupRequest>(_presenter.Current);
        var content = Assert.IsType<SkUiColumnsPopup>(request.Content);
        content.Wheel(0).Position = 2;
        content.Wheel(1).Position = 3;
        Assert.Equal([2, 3], request.SelectedIndices);
        Assert.Equal([0, 0], picker.SelectedIndices);

        Assert.True(((ISkUiKeyHandler)content).OnKeyDown(new SkUiKeyEvent(SkUiKey.Escape, SkUiKeyModifiers.None, false)));
        _ui.RunPending();
        Assert.Equal([0, 0], picker.SelectedIndices);
        Assert.Empty(changes);

        picker.IsOpen = true;
        content = Assert.IsType<SkUiColumnsPopup>(_presenter.Current!.Content);
        content.Wheel(1).Position = 2;
        Assert.True(((ISkUiKeyHandler)content).OnKeyDown(new SkUiKeyEvent(SkUiKey.Enter, SkUiKeyModifiers.None, false)));
        _ui.RunPending();
        Assert.Equal([0, 2], picker.SelectedIndices);
        Assert.Equal([1], changes);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void ImmediatelyHasNoButtons()
    {
        var picker = Picker();
        picker.UpdateMode = SkUiPickerUpdateMode.Immediately;

        picker.IsOpen = true;

        Assert.False(_presenter.Current!.RequiresAccept);
        Assert.Empty(Buttons((SkUiView)_presenter.Current.Content!));
    }

    [Fact]
    public void TheButtonsTextsAreTheAppsOrThePickers()
    {
        var strings = new CultureStrings();
        SkUiStrings.Current = strings;
        var picker = Picker();

        picker.IsOpen = true;
        Assert.Equal(("Apply", "Back"), (_presenter.Current!.AcceptText, _presenter.Current.CancelText));
        picker.IsOpen = false;
        _ui.RunPending();

        // Read at each opening: a culture switch applies to the next popup.
        strings.Culture = "de";
        picker.IsOpen = true;
        var content = (SkUiView)_presenter.Current!.Content!;
        Assert.Equal(["Abbrechen", "Übernehmen"], Buttons(content).Select(button => button.Text));
        picker.IsOpen = false;
        _ui.RunPending();

        picker.AcceptText = "Choose";
        picker.IsOpen = true;
        Assert.Equal(("Choose", "Abbrechen"), (_presenter.Current!.AcceptText, _presenter.Current.CancelText));
    }

    /// <summary>Texts read from the app's "resources" in the current culture, as a derived <see cref="SkUiStrings"/> does.</summary>
    private sealed class CultureStrings : SkUiStrings
    {
        public string Culture { get; set; } = "en";

        public override string AcceptText => Culture == "de" ? "Übernehmen" : "Apply";

        public override string CancelText => Culture == "de" ? "Abbrechen" : "Back";
    }

    [Fact]
    public void TheCorePickersWaitForOkToo()
    {
        var core = new SkUiCoreMultiColumnPicker { UpdateMode = SkUiPickerUpdateMode.WhenFinished }
            .AddColumn(new SkUiPickerColumn { ItemsSource = Hours, SelectedIndex = 1 });
        var list = new SkUiCorePicker().SetUpdateMode(SkUiPickerUpdateMode.WhenFinished).SetCancelText("Nope");
        foreach (var monkey in Monkeys)
            list.Items.Add(monkey);
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(new SkUiCoreHost().SetContent(core));
        stack.Children.Add(new SkUiCoreHost().SetContent(list));
        SkUiTestHelpers.Arrange(stack, 360, 700);

        core.IsOpen = true;
        var wheels = Assert.IsType<SkUiColumnsPopupRequest>(_presenter.Current);
        wheels.SetSelectedIndex(0, 3);
        Assert.Equal([1], core.SelectedIndices);
        wheels.Accept();
        _ui.RunPending();
        Assert.Equal([3], core.SelectedIndices);

        list.IsOpen = true;
        var rows = Assert.IsType<SkUiListPopupRequest>(_presenter.Current);
        Assert.Equal("Nope", rows.CancelText);
        rows.SetCurrentIndex(2);
        Assert.Equal(-1, list.SelectedIndex);
        rows.Accept();
        _ui.RunPending();
        Assert.Equal(2, list.SelectedIndex);
    }
}
