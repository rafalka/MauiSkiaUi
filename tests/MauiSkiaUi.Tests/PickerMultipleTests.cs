using System.Collections.ObjectModel;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// A picker with several choices (<c>SelectionMode</c> <c>Multiple</c>): <c>SelectedItems</c> and <c>SelectedIndices</c>, the
/// field's text, the checked rows that stay open, the typed request, the keyboard and what happens when the items change.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerMultipleTests : IDisposable
{
    private static readonly string[] Monkeys = ["Baboon", "Capuchin Monkey", "Blue Monkey", "Squirrel Monkey", "Golden Lion Tamarin", "Howler Monkey"];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public PickerMultipleTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    private static SkUiPicker Picker(params string[] items)
    {
        var picker = new SkUiPicker { Title = "Select monkeys", SelectionMode = SkUiSelectionMode.Multiple };
        foreach (var item in items)
            picker.Items.Add(item);
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(picker);
        SkUiTestHelpers.Arrange(stack, 300, 600);
        return picker;
    }

    private static string? FieldText(SkUiPicker picker) =>
        SkUiSemanticsTree.Build((SkUiView)picker.Parent!).Flatten().Single(node => ReferenceEquals(node.Source, picker)).Value;

    private SkUiMultiListPopupRequest Shown() => Assert.IsType<SkUiMultiListPopupRequest>(_presenter.Current);

    [Fact]
    public void SelectedItemsAreTheChoiceAndTheFieldJoinsThem()
    {
        var picker = Picker(Monkeys);
        var changes = new List<(int Before, int After)>();
        picker.SelectionChanged += (_, args) => changes.Add((args.PreviousSelection.Count, args.CurrentSelection.Count));

        picker.SelectedItems.Add("Blue Monkey");
        picker.SelectedItems.Add("Baboon");

        Assert.Equal([0, 2], picker.SelectedIndices);
        Assert.Equal([(0, 1), (1, 2)], changes);
        Assert.Equal(-1, picker.SelectedIndex);
        Assert.Null(picker.SelectedItem);
        picker.SelectedTextSeparator = " + ";
        Assert.Equal("Baboon + Blue Monkey", FieldText(picker));
        picker.SelectedCountFormat = "{0} chosen";
        Assert.Equal("2 chosen", FieldText(picker));
        picker.SelectedItems.Remove("Baboon");
        Assert.Equal("Blue Monkey", FieldText(picker)); // one item: its own text
    }

    [Fact]
    public void ATapTogglesARowAndTheListStaysOpen()
    {
        var picker = Picker(Monkeys);
        picker.SelectedItems.Add("Squirrel Monkey");
        var events = new List<string>();
        picker.Opened += (_, _) => events.Add("Opened");
        picker.Closed += (_, _) => events.Add("Closed");
        picker.SelectionChanged += (_, args) => events.Add($"Changed {args.CurrentSelection.Count}");

        picker.IsOpen = true;
        var request = Shown();
        Assert.Equal(Monkeys, request.Items);
        Assert.Equal([3], request.SelectedIndices);
        var content = Assert.IsType<SkUiPickerPopup>(request.Content);
        using var surface = new SkUiTestSurface(content, 300, 300);
        surface.Frame();

        Assert.NotNull(SkUiDiagnostics.SimulateTap(Assert.IsAssignableFrom<SkUiView>(content.List.GetRealizedView(1))));
        _ui.RunPending();
        Assert.NotNull(SkUiDiagnostics.SimulateTap(Assert.IsAssignableFrom<SkUiView>(content.List.GetRealizedView(3))));
        _ui.RunPending();

        Assert.True(picker.IsOpen);
        Assert.Equal([1], picker.SelectedIndices);
        Assert.Equal([1], request.SelectedIndices);
        Assert.Equal(["Opened", "Changed 2", "Changed 1"], events);
        Assert.Equal(["Capuchin Monkey"], picker.SelectedItems.Cast<string>());

        // Dismissing keeps what was chosen.
        _presenter.Dismiss();
        _ui.RunPending();
        Assert.False(picker.IsOpen);
        Assert.Equal([1], picker.SelectedIndices);
        Assert.Equal(["Opened", "Changed 2", "Changed 1", "Closed"], events);
    }

    [Fact]
    public void APresenterOfItsOwnSetsTheSelectionThroughTheRequest()
    {
        var picker = Picker(Monkeys);
        picker.IsOpen = true;
        var request = Shown();

        Assert.True(request.SetSelected(4, true));
        Assert.True(request.SetSelected(0, true));
        Assert.Equal([0, 4], picker.SelectedIndices);
        Assert.False(request.SetSelected(9, true));
        Assert.False(request.SetSelection([4, 0])); // not ascending
        Assert.True(request.Complete([2, 3]));
        _ui.RunPending();

        Assert.Equal([2, 3], picker.SelectedIndices);
        Assert.False(picker.IsOpen);
        Assert.False(request.SetSelected(1, true)); // closed
    }

    [Fact]
    public void ChangesFromTheAppReachTheOpenList()
    {
        var picker = Picker(Monkeys);
        picker.IsOpen = true;
        var request = Shown();
        var content = Assert.IsType<SkUiPickerPopup>(request.Content);
        using var surface = new SkUiTestSurface(content, 300, 300);
        surface.Frame();

        picker.SelectedItems.Add("Howler Monkey");
        picker.SelectedItems.Add("Baboon");

        Assert.Equal([0, 5], request.SelectedIndices);
        Assert.Equal(2, content.List.SelectedItems.Count);
        picker.SelectAll();
        Assert.Equal(6, request.SelectedIndices.Count);
        picker.ClearSelection();
        Assert.Empty(request.SelectedIndices);
        Assert.Empty(content.List.SelectedItems);
    }

    [Fact]
    public void TheKeyboardMovesACursorAndSpaceToggles()
    {
        var picker = Picker(Monkeys);
        picker.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(Shown().Content);
        var handler = (ISkUiKeyHandler)content;
        bool Press(SkUiKey key) => handler.OnKeyDown(new SkUiKeyEvent(key, SkUiKeyModifiers.None, false));

        Assert.True(Press(SkUiKey.Down));
        Assert.True(Press(SkUiKey.Down));
        Assert.Equal(1, content.HighlightedIndex);
        Assert.True(Press(SkUiKey.Space));
        Assert.Equal([1], picker.SelectedIndices);
        Assert.True(Press(SkUiKey.Space));
        Assert.Empty(picker.SelectedIndices);
        Assert.True(Press(SkUiKey.End));
        Assert.True(Press(SkUiKey.Space));
        Assert.Equal([5], picker.SelectedIndices);
        Assert.True(picker.IsOpen);

        Assert.True(Press(SkUiKey.Enter)); // closes, keeping the choice
        _ui.RunPending();
        Assert.False(picker.IsOpen);
        Assert.Equal([5], picker.SelectedIndices);
    }

    [Fact]
    public void TheCursorStartsOnTheFirstCheckedRowAndSpaceWithoutOneShowsIt()
    {
        var picker = Picker(Monkeys);
        picker.SelectedItems.Add(Monkeys[2]);
        picker.SelectedItems.Add(Monkeys[4]);
        picker.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(Shown().Content);
        var handler = (ISkUiKeyHandler)content;
        bool Press(SkUiKey key) => handler.OnKeyDown(new SkUiKeyEvent(key, SkUiKeyModifiers.None, false));

        Assert.Equal(2, content.HighlightedIndex);
        Assert.True(Press(SkUiKey.Space)); // toggles the row the cursor is on
        Assert.Equal([4], picker.SelectedIndices);
        Assert.True(Press(SkUiKey.Down));
        Assert.Equal(3, content.HighlightedIndex);
        picker.IsOpen = false;
        _ui.RunPending();

        // Nothing checked: no cursor; Space puts it on the first row and toggles nothing.
        picker.ClearSelection();
        picker.IsOpen = true;
        content = Assert.IsType<SkUiPickerPopup>(Shown().Content);
        handler = content;
        Assert.Equal(-1, content.HighlightedIndex);
        Assert.True(Press(SkUiKey.Space));
        Assert.Equal(0, content.HighlightedIndex);
        Assert.Empty(picker.SelectedIndices);
    }

    [Fact]
    public void ADraftWaitingForOkSurvivesChangesOfTheFieldsText()
    {
        var picker = Picker(Monkeys);
        picker.UpdateMode = SkUiPickerUpdateMode.WhenFinished;
        picker.SelectedItems.Add(Monkeys[0]);
        picker.IsOpen = true;
        var request = Shown();
        Assert.True(request.SetSelected(3, true));
        Assert.Equal([0, 3], request.SelectedIndices);

        // Only the field's text changes: the draft stays.
        picker.SelectedTextSeparator = " + ";
        picker.SelectedCountFormat = "{0} monkeys";
        picker.SelectedItemsDisplay = SkUiSelectedItemsDisplay.Tokens;
        picker.ItemEnabledBinding = new Binding(".", converter: new NotHowler());
        Assert.Equal([0, 3], request.SelectedIndices);
        Assert.Equal([0], picker.SelectedIndices);

        // A change of the chosen items still reaches the popup.
        picker.SelectedItems.Add(Monkeys[1]);
        Assert.Equal([0, 1], request.SelectedIndices);
    }

    private sealed class NotHowler : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value as string != "Howler Monkey";

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }

    [Fact]
    public void TheClosedFieldDoesNotChangeTheChoiceWithArrowKeys()
    {
        var picker = Picker(Monkeys);
        Assert.True(picker.Focus());
        var root = (SkUiView)picker.Parent!;

        Assert.False(root.FocusManager.KeyDown(SkUiKey.Down));
        Assert.Empty(picker.SelectedIndices);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void ItemsThatLeaveTheListLeaveTheChoice()
    {
        var source = new ObservableCollection<string>(Monkeys);
        var picker = Picker();
        picker.ItemsSource = source;
        picker.SelectedItems.Add("Baboon");
        picker.SelectedItems.Add("Blue Monkey");
        picker.SelectedItems.Add("Howler Monkey");
        Assert.Equal([0, 2, 5], picker.SelectedIndices);

        source.Remove("Capuchin Monkey"); // the others move up
        Assert.Equal([0, 1, 4], picker.SelectedIndices);
        source.Remove("Blue Monkey");
        Assert.Equal([0, 3], picker.SelectedIndices);
        Assert.Equal(["Baboon", "Howler Monkey"], picker.SelectedItems.Cast<string>());
    }

    [Fact]
    public void AChoiceMadeBeforeTheItemsComeStays()
    {
        var picker = new SkUiPicker { SelectionMode = SkUiSelectionMode.Multiple };
        picker.SelectedItems.Add("Blue Monkey");

        picker.ItemsSource = Monkeys.ToList();

        Assert.Equal([2], picker.SelectedIndices);
    }

    [Fact]
    public void SwitchingTheModeKeepsTheFirstChoice()
    {
        var picker = new SkUiPicker();
        foreach (var monkey in Monkeys)
            picker.Items.Add(monkey);
        picker.SelectedIndex = 3;

        picker.SelectionMode = SkUiSelectionMode.Multiple;
        Assert.Equal(-1, picker.SelectedIndex);
        Assert.Equal([3], picker.SelectedIndices);
        picker.SelectedItems.Add("Baboon");
        picker.SelectedIndex = 2; // does nothing with several choices
        Assert.Equal(-1, picker.SelectedIndex);

        picker.SelectionMode = SkUiSelectionMode.Single;
        Assert.Equal(0, picker.SelectedIndex);
        Assert.Equal("Baboon", picker.SelectedItem);
        Assert.Empty(picker.SelectedItems);
        Assert.Empty(picker.SelectedIndices);
    }

    [Fact]
    public void ChangingTheModeClosesTheOpenList()
    {
        var picker = Picker(Monkeys);
        picker.IsOpen = true;

        picker.SelectionMode = SkUiSelectionMode.Single;
        _ui.RunPending();

        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void OnlySingleAndMultipleAreModes()
    {
        var picker = new SkUiPicker();

        picker.SelectionMode = SkUiSelectionMode.SingleDeselect; // ignored, as an invalid value

        Assert.Equal(SkUiSelectionMode.Single, picker.SelectionMode);
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SetSelectionMode(SkUiSelectionMode.None));
    }
}

/// <summary>The selected rows' look is the app's: the <c>Selected</c> visual state of the item template's root, and <c>SelectionBackground</c>.</summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerSelectedRowLookTests : IDisposable
{
    private static readonly string[] Fruits = ["Apple", "Pear", "Plum", "Fig"];
    private static readonly Color SelectedColor = Colors.Gold;

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public PickerSelectedRowLookTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    /// <summary>A row whose root turns gold in the <c>Selected</c> state.</summary>
    private static SkUiHorizontalStackLayout Row()
    {
        var label = new SkUiLabel { Padding = new Thickness(12) };
        var row = new SkUiHorizontalStackLayout { Children = { label } };
        row.BindingContextChanged += (_, _) => label.Text = row.BindingContext as string ?? string.Empty;
        var selected = new VisualState { Name = VisualStateManager.CommonStates.Selected };
        selected.Setters.Add(new Setter { Property = VisualElement.BackgroundColorProperty, Value = SelectedColor });
        var group = new VisualStateGroup { Name = "CommonStates" };
        group.States.Add(new VisualState { Name = VisualStateManager.CommonStates.Normal });
        group.States.Add(selected);
        VisualStateManager.GetVisualStateGroups(row).Add(group);
        return row;
    }

    private SkUiPickerPopup Open(SkUiPicker picker, out SkUiTestSurface surface)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(picker);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        picker.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);
        surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();
        return content;
    }

    private static Color? RowColor(SkUiPickerPopup content, int index)
    {
        var row = Assert.IsAssignableFrom<SkUiView>(content.List.GetRealizedView(index));
        var root = Descendants(row).OfType<SkUiHorizontalStackLayout>().First();
        return root.BackgroundColor;
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
    public void TheTemplatesRootIsSelectedWithTheHighlight()
    {
        var picker = new SkUiPicker { ItemsSource = Fruits, SelectedIndex = 1, ItemTemplate = new DataTemplate(Row), SelectionBackground = Brush.Transparent };
        var content = Open(picker, out var surface);
        using (surface)
        {
            Assert.Equal(SelectedColor, RowColor(content, 1));
            Assert.NotEqual(SelectedColor, RowColor(content, 0));
            Assert.Same(Brush.Transparent, content.List.SelectionBackground);

            // The keyboard moves the highlight: the state follows.
            Assert.True(((ISkUiKeyHandler)content).OnKeyDown(new SkUiKeyEvent(SkUiKey.Down, SkUiKeyModifiers.None, false)));
            Assert.Equal(SelectedColor, RowColor(content, 2));
            Assert.NotEqual(SelectedColor, RowColor(content, 1));
        }
    }

    [Fact]
    public void ChosenRowsOfSeveralChoicesAreSelected()
    {
        var picker = new SkUiPicker { ItemsSource = Fruits, SelectionMode = SkUiSelectionMode.Multiple, ItemTemplate = new DataTemplate(Row) };
        picker.SelectedItems.Add("Plum");
        var content = Open(picker, out var surface);
        using (surface)
        {
            Assert.Equal(SelectedColor, RowColor(content, 2));
            Assert.NotEqual(SelectedColor, RowColor(content, 0));
            Assert.NotNull(SkUiDiagnostics.SimulateTap(Assert.IsAssignableFrom<SkUiView>(content.List.GetRealizedView(0))));
            Assert.Equal(SelectedColor, RowColor(content, 0));
        }
    }

    [Fact]
    public void TheSelectionBackgroundIsThePickersElseTheLooks()
    {
        var picker = new SkUiPicker { ItemsSource = Fruits };
        var content = Open(picker, out var surface);
        surface.Dispose();
        Assert.Equal(SkUiLook.Current.PopupHighlightColor, Assert.IsType<SolidColorBrush>(content.List.SelectionBackground).Color);
        picker.IsOpen = false;
        _ui.RunPending();

        picker.SelectionBackground = Brush.Red;
        picker.IsOpen = true;
        content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);
        Assert.Same(Brush.Red, content.List.SelectionBackground);
    }
}
