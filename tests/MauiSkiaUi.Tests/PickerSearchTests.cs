using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// A picker's search (<see cref="SkUiPicker.IsSearchEnabled"/>): the field shows for enough items, filters the rows with the
/// default or the app's filter, keeps checked rows the search hides, takes typing, and moves the highlight from the field.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerSearchTests : IDisposable
{
    private static readonly string[] Cities = ["Zürich", "Berlin", "Bern", "Paris", "Prague", "Rome"];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previousPresenter = SkUiPopups.Presenter;
    private readonly SkUiLook _previousLook = SkUiLook.Current;
    private readonly SkUiStrings _previousStrings = SkUiStrings.Current;
    private readonly List<SkUiTestSurface> _surfaces = [];

    public PickerSearchTests()
    {
        SkUiLook.Current = new DefaultSkUiLook();
        SkUiPopups.Presenter = _presenter;
    }

    public void Dispose()
    {
        SkUiPopups.Presenter = _previousPresenter;
        SkUiLook.Current = _previousLook;
        SkUiStrings.Current = _previousStrings;
        foreach (var surface in _surfaces)
            surface.Dispose();
        _ui.Dispose();
    }

    private const double Tolerance = 0.01;

    private static SkUiPicker Picker(SkUiSelectionMode mode = SkUiSelectionMode.Single, SkUiPopupPlacement placement = SkUiPopupPlacement.Anchored,
        LayoutOptions? vertical = null)
    {
        var picker = new SkUiPicker
        {
            Title = "City", IsSearchEnabled = true, SelectionMode = mode, PopupPlacement = placement, VerticalOptions = vertical ?? LayoutOptions.Start
        };
        foreach (var city in Cities)
            picker.Items.Add(city);
        var root = new SkUiContentView { Content = picker };
        var window = new Window(new ContentPage { Content = root });
        ((IWindow)window).FrameChanged(new Rect(0, 0, 400, 800));
        SkUiTestHelpers.Arrange(root, 400, 800);
        return picker;
    }

    /// <summary>Opens the picker; its content laid out at the request's bounds (as a presenter shows it).</summary>
    private SkUiPickerPopup Open(SkUiPicker picker)
    {
        picker.IsOpen = true;
        var request = _presenter.Current!;
        var content = Assert.IsType<SkUiPickerPopup>(request.Content);
        var surface = new SkUiTestSurface(content, (int)request.Bounds.Width, (int)request.Bounds.Height);
        surface.Frame();
        _surfaces.Add(surface);
        return content;
    }

    private static bool Press(ISkUiKeyHandler handler, SkUiKey key, char character = '\0') =>
        handler.OnKeyDown(new SkUiKeyEvent(key, SkUiKeyModifiers.None, false, character));

    [Fact]
    public void TheSearchFieldShowsOnlyForEnoughItems()
    {
        var picker = Picker();
        picker.SearchMinimumItemCount = Cities.Length + 1;
        Assert.Null(Open(picker).SearchBar);
        picker.IsOpen = false;
        _ui.RunPending();

        picker.SearchMinimumItemCount = Cities.Length;
        picker.SearchPlaceholder = "Find a city";
        var search = Open(picker).SearchBar;
        Assert.NotNull(search);
        Assert.Equal("Find a city", search.Placeholder);
        picker.IsOpen = false;
        _ui.RunPending();

        picker.IsSearchEnabled = false;
        Assert.Null(Open(picker).SearchBar);
    }

    [Fact]
    public void ThePlaceholderIsTheStringsByDefault()
    {
        SkUiStrings.Current = new SkUiStrings { SearchPlaceholderText = "Suchen" };
        Assert.Equal("Suchen", Open(Picker()).SearchBar!.Placeholder);
    }

    [Fact]
    public void TypingFiltersIgnoringCaseAndAccents()
    {
        var picker = Picker();
        var content = Open(picker);

        content.SearchBar!.Text = "BER";
        Assert.Equal([1, 2], content.ShownIndices);
        content.SearchBar.Text = "zurich";
        Assert.Equal([0], content.ShownIndices);
        content.SearchBar.Text = "  ";
        Assert.Equal(Enumerable.Range(0, Cities.Length), content.ShownIndices);
        content.SearchBar.Text = "xyz";
        Assert.Empty(content.ShownIndices);
    }

    [Fact]
    public void TheAppsFilterDecides()
    {
        var picker = Picker();
        var calls = new List<(object? Item, string Phrase)>();
        picker.SearchFilter = (item, phrase) =>
        {
            calls.Add((item, phrase));
            return ((string)item!).StartsWith(phrase, StringComparison.Ordinal);
        };
        var content = Open(picker);

        content.SearchBar!.Text = " P ";
        Assert.Equal([3, 4], content.ShownIndices);
        Assert.Equal(Cities.Length, calls.Count);
        Assert.All(calls, call => Assert.Equal("P", call.Phrase));
    }

    [Fact]
    public void TheFilterGetsTheSourcesItems()
    {
        var people = new[] { new Person("Ann", "Accounting"), new Person("Bob", "Sales"), new Person("Cid", "Accounting") };
        var picker = new SkUiPicker
        {
            IsSearchEnabled = true, ItemsSource = people, ItemDisplayBinding = new Binding(nameof(Person.Name)),
            SearchFilter = (item, phrase) => ((Person)item!).Team.Contains(phrase, StringComparison.OrdinalIgnoreCase)
        };
        SkUiTestHelpers.Arrange(new SkUiContentView { Content = picker }, 400, 800);
        var content = Open(picker);

        content.SearchBar!.Text = "acc";
        Assert.Equal([0, 2], content.ShownIndices);
    }

    [Fact]
    public void ATapOnAFoundRowChoosesThatItem()
    {
        var picker = Picker();
        var content = Open(picker);
        content.SearchBar!.Text = "pra";

        var bounds = _presenter.Current!.Bounds;
        SkUiTestHelpers.Arrange(content, bounds.Width, bounds.Height);
        Assert.NotNull(SkUiDiagnostics.SimulateTap(Assert.IsAssignableFrom<SkUiView>(content.List.GetRealizedView(0))));
        _ui.RunPending();
        Assert.Equal(4, picker.SelectedIndex);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void EnterInTheFieldChoosesTheFirstMatch()
    {
        var picker = Picker();
        var content = Open(picker);
        content.SearchBar!.Text = "r";

        Press(content.SearchBar, SkUiKey.Enter);
        _ui.RunPending();

        Assert.Equal(0, picker.SelectedIndex); // "Zürich"
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void UpAndDownInTheFieldMoveThroughTheMatches()
    {
        var picker = Picker();
        var content = Open(picker);
        var field = (ISkUiKeyHandler)content.SearchBar!;
        content.SearchBar!.Text = "p";

        Assert.True(Press(field, SkUiKey.Down));
        Assert.Equal(3, content.HighlightedIndex);
        Assert.True(Press(field, SkUiKey.Down));
        Assert.Equal(4, content.HighlightedIndex);
        Assert.True(Press(field, SkUiKey.Down)); // the last match
        Assert.Equal(4, content.HighlightedIndex);
        Assert.True(Press(field, SkUiKey.Up));
        Assert.Equal(3, content.HighlightedIndex);
        Assert.True(picker.IsOpen);
    }

    [Fact]
    public void TypingInTheListGoesToTheSearchField()
    {
        var picker = Picker();
        var content = Open(picker);

        Assert.True(Press(content, SkUiKey.Character, 'r'));
        Assert.True(Press(content, SkUiKey.Character, 'o'));

        Assert.Equal("ro", content.SearchBar!.Text);
        Assert.Equal([5], content.ShownIndices);
    }

    [Fact]
    public void CheckedRowsTheSearchHidesStayChosen()
    {
        var picker = Picker(SkUiSelectionMode.Multiple);
        picker.SelectedItems.Add("Bern");
        var content = Open(picker);

        content.SearchBar!.Text = "pa";
        var paris = content.List.ItemsSource!.Cast<object>().Single();
        content.List.SelectedItems.Add(paris);

        Assert.Equal([2, 3], picker.SelectedIndices);
        content.SearchBar.Text = string.Empty;
        Assert.Equal(2, content.List.SelectedItems.Count); // both show as chosen again
        Assert.Equal([2, 3], picker.SelectedIndices);
    }

    [Fact]
    public void SelectAllChoosesTheMatches()
    {
        var picker = Picker(SkUiSelectionMode.Multiple);
        picker.IsSelectAllVisible = true;
        picker.SelectedItems.Add("Rome");
        var content = Open(picker);

        content.SearchBar!.Text = "ber";
        Assert.Equal(SkUiCheckState.Unchecked, content.SelectAllCheckState);
        content.TapSelectAll();

        Assert.Equal([1, 2, 5], picker.SelectedIndices);
        Assert.Equal(SkUiCheckState.Checked, content.SelectAllCheckState);
    }

    [Fact]
    public void ClearClearsHiddenChoicesToo()
    {
        var picker = Picker(SkUiSelectionMode.Multiple);
        picker.IsPopupClearButtonVisible = true;
        picker.SelectedItems.Add("Bern");
        picker.SelectedItems.Add("Rome");
        var content = Open(picker);

        content.SearchBar!.Text = "zzz";
        content.ClearChoice();

        Assert.Empty(picker.SelectedIndices);
    }

    [Fact]
    public void ASearchableListStaysAListWhereTheLookShowsAWheel()
    {
        SkUiLook.Current = new IosSkUiLook();
        var picker = Picker(placement: SkUiPopupPlacement.BottomPane);

        Assert.NotNull(Open(picker).SearchBar);
        picker.IsOpen = false;
        _ui.RunPending();

        picker.SearchMinimumItemCount = 100;
        picker.IsOpen = true;
        Assert.IsType<SkUiColumnsPopup>(_presenter.Current!.Content);
    }

    [Fact]
    public void TheDefaultMatchIgnoresCaseAndAccents()
    {
        Assert.True(SkUiPickerPopup.Contains("Zürich", "ZUR"));
        Assert.True(SkUiPickerPopup.Contains("Crème brûlée", "creme brulee"));
        Assert.False(SkUiPickerPopup.Contains("Paris", "rome"));
    }

    [Fact]
    public void InvalidCountsAreRejected()
    {
        var picker = new SkUiPicker { SearchMinimumItemCount = 3 };
        picker.SearchMinimumItemCount = -1; // ignored, as MAUI's validation
        Assert.Equal(3, picker.SearchMinimumItemCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SetSearchMinimumItemCount(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCorePicker().SetSearchMinimumItemCount(-1));
    }

    [Fact]
    public void ACorePickerSearchesToo()
    {
        var core = new SkUiCorePicker().SetItemsSource(Cities).SetIsSearchEnabled(true).SetSearchMinimumItemCount(4)
            .SetSearchFilter((item, phrase) => ((string)item!).EndsWith(phrase, StringComparison.Ordinal)).SetPopupPlacement(SkUiPopupPlacement.Anchored);
        var changed = new List<string?>();
        core.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        core.SetSearchPlaceholder("Find");
        Assert.Equal([nameof(SkUiCorePicker.SearchPlaceholder)], changed);

        var root = new SkUiContentView { Content = new SkUiCoreHost().SetContent(core) };
        var window = new Window(new ContentPage { Content = root });
        ((IWindow)window).FrameChanged(new Rect(0, 0, 400, 800));
        SkUiTestHelpers.Arrange(root, 400, 800);
        core.SetIsOpen(true);
        var content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);

        Assert.Equal("Find", content.SearchBar!.Placeholder);
        content.SearchBar.Text = "e";
        Assert.Equal([4, 5], content.ShownIndices);
    }

    /// <summary>The popup's height for <paramref name="rows"/> plain rows under its search field.</summary>
    private static double HeightFor(SkUiPickerPopup content, int rows, double width) =>
        rows * SkUiLook.Current.PopupItemHeight + content.SearchBar!.Measure(width, double.PositiveInfinity).Height;

    [Fact]
    public void AnAnchoredPopupShrinksToTheMatchesWhenThePresenterFollows()
    {
        var content = Open(Picker());
        var request = _presenter.Current!;
        var opened = request.Bounds;
        Assert.False(request.OpensAbove);
        var changes = 0;
        request.BoundsChanged += (_, _) => changes++;

        content.Filter("ber");
        var bounds = request.Bounds;
        Assert.Equal(1, changes);
        Assert.Equal(opened.Top, bounds.Top, Tolerance);
        Assert.Equal(opened.Left, bounds.Left, Tolerance);
        Assert.Equal(opened.Width, bounds.Width, Tolerance);
        Assert.Equal(HeightFor(content, 2, opened.Width), bounds.Height, Tolerance);
        Assert.Equal(bounds.Height, content.HeightRequest, Tolerance);
        Assert.Equal(bounds.Width, content.WidthRequest, Tolerance);

        // The same matches again: nothing moves.
        content.Filter("bEr");
        Assert.Equal(1, changes);

        // Back to every row: the size it opened with.
        content.Filter("");
        Assert.Equal(2, changes);
        Assert.Equal(opened, request.Bounds);
        Assert.Equal(opened.Height, content.HeightRequest, Tolerance);
    }

    [Fact]
    public void NothingFoundLeavesRoomForTheNoResultsText()
    {
        var content = Open(Picker());
        var request = _presenter.Current!;
        request.BoundsChanged += (_, _) => { };
        content.Filter("xyz");
        Assert.Empty(content.ShownIndices);
        var width = request.Bounds.Width;
        var noResults = Assert.IsType<SkUiLabel>(content.List.EmptyView);
        var expected = noResults.Measure(width, double.PositiveInfinity).Height + content.SearchBar!.Measure(width, double.PositiveInfinity).Height;
        Assert.Equal(expected, request.Bounds.Height, Tolerance);
        Assert.True(request.Bounds.Height > 0);
    }

    [Fact]
    public void WithoutAFollowingPresenterThePopupKeepsItsSize()
    {
        var content = Open(Picker());
        var request = _presenter.Current!;
        var opened = request.Bounds;
        content.Filter("ber");
        Assert.Equal(opened, request.Bounds);
        Assert.Equal(opened.Height, content.HeightRequest, Tolerance);
    }

    [Fact]
    public void AWindowPopupKeepsItsSize()
    {
        var content = Open(Picker(placement: SkUiPopupPlacement.Window));
        var request = _presenter.Current!;
        var opened = request.Bounds;
        var changes = 0;
        request.BoundsChanged += (_, _) => changes++;
        content.Filter("ber");
        Assert.Equal(0, changes);
        Assert.Equal(opened, request.Bounds);
    }

    [Fact]
    public void OpenedAboveTheSearchFieldIsUnderTheRowsAndThePopupShrinksTowardTheField()
    {
        var content = Open(Picker(vertical: LayoutOptions.End));
        var request = _presenter.Current!;
        Assert.True(request.OpensAbove);
        var search = content.SearchBar!;
        Assert.True(Grid.GetRow(search) > Grid.GetRow(content.List));
        // Drawn against its bottom edge while the surface is resized ahead of the new layout.
        Assert.True(content.SurfaceKeepsBottom);
        var opened = request.Bounds;
        request.BoundsChanged += (_, _) => { };

        content.Filter("ber");
        var bounds = request.Bounds;
        Assert.Equal(opened.Bottom, bounds.Bottom, Tolerance);
        Assert.Equal(HeightFor(content, 2, opened.Width), bounds.Height, Tolerance);
        Assert.True(bounds.Top > opened.Top);
        Assert.True(content.SurfaceKeepsBottom);
    }

    [Fact]
    public void OpenedBelowTheSearchFieldIsAboveTheRows()
    {
        var content = Open(Picker());
        Assert.True(Grid.GetRow(content.SearchBar!) < Grid.GetRow(content.List));
        Assert.False(content.SurfaceKeepsBottom);
        _presenter.Current!.BoundsChanged += (_, _) => { };
        content.Filter("ber");
        Assert.False(content.SurfaceKeepsBottom);
    }

    [Fact]
    public void TemplatedRowsShrinkToTheirMeasuredHeights()
    {
        var picker = Picker();
        picker.ItemTemplate = new DataTemplate(() =>
        {
            var label = new SkUiLabel { HeightRequest = 30 };
            label.SetBinding(SkUiLabel.TextProperty, new Binding("."));
            return label;
        });
        var content = Open(picker);
        var request = _presenter.Current!;
        request.BoundsChanged += (_, _) => { };
        content.Filter("ber");
        var probe = content.SearchBar!.Measure(request.Bounds.Width, double.PositiveInfinity).Height;
        Assert.Equal(2 * 30 + probe, request.Bounds.Height, 1);
    }

    [Fact]
    public void AResizedPopupStaysAgainstItsAnchor()
    {
        var opened = new Rect(10, 100, 200, 300);
        Assert.Equal(new Rect(10, 100, 200, 80), SkUiPopupGeometry.Resized(opened, opensAbove: false, 80));
        Assert.Equal(new Rect(10, 320, 200, 80), SkUiPopupGeometry.Resized(opened, opensAbove: true, 80));
        Assert.Equal(opened, SkUiPopupGeometry.Resized(opened, opensAbove: true, 500));
    }

    private sealed record Person(string Name, string Team);
}
