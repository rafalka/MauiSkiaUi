using System.Collections.ObjectModel;
using MauiSkiaUi.Core;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// A presenter for tests: records each request, shows nothing, and completes when the popup must close (the token) or when
/// the test dismisses it, as a real presenter's popup does.
/// </summary>
internal sealed class RecordingPopupPresenter : ISkUiPopupPresenter
{
    private TaskCompletionSource? _shown;

    public List<SkUiPopupRequest> Requests { get; } = [];

    public SkUiPopupRequest? Current { get; private set; }

    /// <summary>How many popups were closed (their task completed).</summary>
    public int Closes { get; private set; }

    public Task ShowAsync(SkUiPopupRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Current = request;
        var shown = new TaskCompletionSource();
        _shown = shown;
        cancellationToken.Register(() =>
        {
            if (shown.TrySetResult())
                Closes++;
        });
        return shown.Task;
    }

    /// <summary>The user dismisses the popup (a tap outside): it closes without a result.</summary>
    public void Dismiss()
    {
        if (_shown?.TrySetResult() == true)
            Closes++;
    }
}

/// <summary>
/// Pickers (C6, FR-29): MAUI's <c>Picker</c> API on <see cref="SkUiPicker"/> and <see cref="SkUiCorePicker"/>, the popup
/// request (anchor, placement, content, typed data), its lifetime (choose, dismiss, close by the control) and events in MAUI's
/// order, keyboard and screen readers, through a recording presenter. In the global collection: the app-wide presenter and the
/// keyboard's last-input state are process-wide.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerTests : IDisposable
{
    private static readonly string[] Monkeys = ["Baboon", "Capuchin Monkey", "Blue Monkey", "Squirrel Monkey", "Golden Lion Tamarin", "Howler Monkey"];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public PickerTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    private static SkUiVerticalStackLayout Root(params ISkUiView[] children)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        foreach (var child in children)
            stack.Children.Add(child);
        SkUiTestHelpers.Arrange(stack, 300, 600);
        return stack;
    }

    private static SkUiPicker Picker(params string[] items)
    {
        var picker = new SkUiPicker { Title = "Select a monkey" };
        foreach (var item in items)
            picker.Items.Add(item);
        return picker;
    }

    private SkUiListPopupRequest Shown() => Assert.IsType<SkUiListPopupRequest>(_presenter.Current);

    [Fact]
    public void OpeningHandsTheAppsPresenterAListRequest()
    {
        var picker = Picker(Monkeys);
        picker.SelectedIndex = 2;
        picker.PopupPlacement = SkUiPopupPlacement.Anchored;
        var root = Root(picker);

        picker.IsOpen = true;

        var request = Shown();
        Assert.Same(picker, request.Anchor);
        Assert.Same(root, request.PlatformAnchor);
        Assert.Equal(Monkeys, request.Items);
        Assert.Equal(2, request.SelectedIndex);
        Assert.Equal("Select a monkey", request.Title);
        Assert.Equal(SkUiPopupPlacement.Anchored, request.Placement);
        Assert.Null(request.OverlayColor);
        // The anchor's rectangle: in the surface, and (headless: the surface at the window's origin) in the window.
        Assert.Equal(new Rect(10, 20, 280, picker.Height), request.AnchorBoundsInPlatformAnchor);
        Assert.Equal(request.AnchorBoundsInPlatformAnchor, request.AnchorBounds);
        // Below the anchor, at least its width, start-aligned.
        Assert.Equal(10, request.Bounds.Left);
        Assert.True(request.Bounds.Width >= 280);
        Assert.Equal(request.AnchorBounds.Bottom + SkUiLook.Current.PopupAnchorGap, request.Bounds.Top);
        Assert.False(request.OpensAbove);
        var content = Assert.IsType<SkUiPickerPopup>(request.Content);
        Assert.Equal(2, content.HighlightedIndex);
        Assert.Equal(request.Bounds.Width, content.WidthRequest);
        Assert.True(picker.IsPopupShown);
    }

    [Fact]
    public void AChoiceSelectsThenClosesInMauisOrder()
    {
        var picker = Picker(Monkeys);
        Root(picker);
        var events = new List<string>();
        picker.Opened += (_, _) => events.Add("Opened");
        picker.Closed += (_, _) => events.Add("Closed");
        picker.SelectedIndexChanged += (_, _) => events.Add($"SelectedIndexChanged {picker.SelectedIndex}");

        picker.IsOpen = true;
        var request = Shown();
        Assert.True(request.Complete(3));
        Assert.True(request.CloseRequested.IsCancellationRequested);
        _ui.RunPending();

        Assert.Equal(["Opened", "SelectedIndexChanged 3", "Closed"], events);
        Assert.Equal(3, picker.SelectedIndex);
        Assert.Equal("Squirrel Monkey", picker.SelectedItem);
        Assert.False(picker.IsOpen);
        Assert.False(picker.IsPopupShown);
        Assert.Equal(1, _presenter.Closes);
        // Completed: the request lets go of the picker, its surface, window and content.
        Assert.True(request.IsCompleted);
        Assert.Null(request.Anchor);
        Assert.Null(request.PlatformAnchor);
        Assert.Null(request.Content);
        Assert.Empty(request.Items);
        Assert.False(request.Complete(1));
    }

    [Fact]
    public void ADismissalClosesWithoutChanging()
    {
        var picker = Picker(Monkeys);
        picker.SelectedIndex = 1;
        Root(picker);
        var closed = 0;
        var changed = 0;
        picker.Closed += (_, _) => closed++;
        picker.SelectedIndexChanged += (_, _) => changed++;

        picker.IsOpen = true;
        _presenter.Dismiss();
        _ui.RunPending();

        Assert.False(picker.IsOpen);
        Assert.Equal(1, picker.SelectedIndex);
        Assert.Equal((1, 0), (closed, changed));
        Assert.False(Shown().HasResult);
    }

    [Fact]
    public void TheControlClosesThePopupWhenItMustGo()
    {
        var picker = Picker(Monkeys);
        var root = Root(picker);
        var closed = 0;
        picker.Closed += (_, _) => closed++;

        // IsOpen = false.
        picker.IsOpen = true;
        var first = Shown();
        picker.IsOpen = false;
        Assert.True(first.CloseRequested.IsCancellationRequested);
        _ui.RunPending();
        Assert.Equal(1, closed);
        Assert.False(picker.IsPopupShown);

        // Disabled, hidden, removed.
        foreach (var close in new Action[] { () => picker.IsEnabled = false, () => picker.IsVisible = false, () => root.Children.Remove(picker) })
        {
            picker.IsEnabled = true;
            picker.IsVisible = true;
            if (!root.Children.Contains(picker))
                root.Children.Add(picker);
            picker.IsOpen = true;
            var request = Shown();
            Assert.True(picker.IsPopupShown);
            close();
            Assert.True(request.CloseRequested.IsCancellationRequested);
            _ui.RunPending();
            Assert.False(picker.IsOpen);
        }
        Assert.Equal(4, closed);
        Assert.Equal(4, _presenter.Closes);
    }

    [Fact]
    public void APickerThatCannotOpenStaysClosedWithoutEvents()
    {
        var picker = Picker(Monkeys);
        var events = 0;
        picker.Opened += (_, _) => events++;
        picker.Closed += (_, _) => events++;

        Root(picker);
        picker.IsVisible = false;
        picker.IsOpen = true;
        Assert.False(picker.IsOpen);
        picker.IsVisible = true;
        picker.IsEnabled = false;
        picker.IsOpen = true;
        Assert.False(picker.IsOpen);
        Assert.Equal(0, events);
        Assert.Empty(_presenter.Requests);
    }

    [Fact]
    public void AnchoredPopupsCloseWhenTheirScrollerMoves()
    {
        var picker = Picker(Monkeys);
        picker.PopupPlacement = SkUiPopupPlacement.Anchored;
        var inner = new SkUiVerticalStackLayout();
        inner.Children.Add(picker);
        for (var index = 0; index < 20; index++)
            inner.Children.Add(new SkUiBox { HeightRequest = 60 });
        var scroller = new SkUiScrollView { Content = inner };
        SkUiTestHelpers.Arrange(scroller, 300, 400);

        picker.IsOpen = true;
        var request = Shown();
        _ = scroller.ScrollToAsync(0, 200, animated: false);
        Assert.True(request.CloseRequested.IsCancellationRequested);
        _ui.RunPending();
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void ThePopupListChoosesByKeyboard()
    {
        var picker = Picker(Monkeys);
        picker.SelectedIndex = 0;
        Root(picker);
        picker.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(Shown().Content);
        SkUiTestHelpers.Arrange(content, 300, 300);

        Key(content, SkUiKey.Down);
        Key(content, SkUiKey.Down);
        Assert.Equal(2, content.HighlightedIndex);
        Assert.Equal(0, picker.SelectedIndex); // highlighted, not chosen
        Key(content, SkUiKey.End);
        Assert.Equal(5, content.HighlightedIndex);
        Key(content, SkUiKey.Character, 'g');
        Assert.Equal(4, content.HighlightedIndex); // "Golden Lion Tamarin"
        Key(content, SkUiKey.Enter);
        _ui.RunPending();

        Assert.Equal(4, picker.SelectedIndex);
        Assert.False(picker.IsOpen);

        // Escape closes without a choice; the list had keyboard focus, so the field takes it back.
        picker.IsOpen = true;
        content = Assert.IsType<SkUiPickerPopup>(Shown().Content);
        SkUiTestHelpers.Arrange(content, 300, 300);
        Assert.True(content.Focus());
        Assert.False(content.IsFocusRingVisible);
        Assert.True(content.FocusManager.KeyDown(SkUiKey.Up));
        Assert.Equal(3, content.HighlightedIndex);
        Assert.True(content.FocusManager.KeyDown(SkUiKey.Escape));
        _ui.RunPending();
        Assert.Equal(4, picker.SelectedIndex);
        Assert.False(picker.IsOpen);
        Assert.True(picker.IsFocused);
    }

    private static void Key(SkUiPickerPopup content, SkUiKey key, char character = '\0') =>
        Assert.True(((ISkUiKeyHandler)content).OnKeyDown(new SkUiKeyEvent(key, SkUiKeyModifiers.None, false, character)));

    [Fact]
    public void ATappedRowCompletesTheRequest()
    {
        var picker = Picker(Monkeys);
        Root(picker);
        picker.IsOpen = true;
        var request = Shown();
        var content = Assert.IsType<SkUiPickerPopup>(request.Content);
        using var surface = new SkUiTestSurface(content, 300, 300);
        surface.Frame();
        var row = Assert.IsAssignableFrom<SkUiView>(content.List.GetRealizedView(1));

        Assert.NotNull(SkUiDiagnostics.SimulateTap(row));
        _ui.RunPending();

        Assert.Equal(1, picker.SelectedIndex);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void TheClosedFieldTakesTheKeysOfAComboBox()
    {
        var picker = Picker(Monkeys);
        var root = Root(picker);
        Assert.True(picker.Focus());

        Assert.True(root.FocusManager.KeyDown(SkUiKey.Down));
        Assert.Equal(0, picker.SelectedIndex);
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Down));
        Assert.True(root.FocusManager.KeyDown(SkUiKey.End));
        Assert.Equal(5, picker.SelectedIndex);
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Character, character: 'b'));
        Assert.Equal(0, picker.SelectedIndex); // "Baboon", the first match after the last item
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Character, character: 'b'));
        Assert.Equal(2, picker.SelectedIndex); // the same letter again: "Blue Monkey"
        Assert.False(picker.IsOpen);

        Assert.True(root.FocusManager.KeyDown(SkUiKey.Down, SkUiKeyModifiers.Alt));
        Assert.True(picker.IsOpen);
        picker.IsOpen = false;
        _ui.RunPending();
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Enter));
        Assert.True(picker.IsOpen);
    }

    [Fact]
    public void ScreenReadersSeeAComboBox()
    {
        var picker = Picker(Monkeys);
        var root = Root(picker);

        var element = SkUiSemanticsTree.Build(root).Flatten().Single(node => ReferenceEquals(node.Source, picker));
        Assert.Equal(SkUiSemanticsRole.ComboBox, element.Role);
        Assert.Equal("Select a monkey", element.Label);
        Assert.Null(element.Value);
        Assert.False(element.IsExpanded);
        Assert.True((element.Actions & SkUiSemanticsActions.Activate) != 0);

        picker.SelectedIndex = 1;
        Assert.True(((ISkUiAccessibleNode)picker).PerformSemanticsAction(SkUiSemanticsActions.Activate));
        element = SkUiSemanticsTree.Build(root).Flatten().Single(node => ReferenceEquals(node.Source, picker));
        Assert.Equal("Capuchin Monkey", element.Value);
        Assert.True(element.IsExpanded);
    }

    [Fact]
    public void SelectionFollowsMauisRules()
    {
        var monkeys = new ObservableCollection<Monkey>(Monkeys.Select(name => new Monkey(name)));
        var picker = new SkUiPicker { ItemDisplayBinding = new Binding(nameof(Monkey.Name)) };
        var changes = 0;
        picker.SelectedIndexChanged += (_, _) => changes++;

        // An index before the items applies when they come, without the event.
        picker.SelectedIndex = 2;
        Assert.Equal(-1, picker.SelectedIndex);
        picker.ItemsSource = monkeys;
        Assert.Equal(2, picker.SelectedIndex);
        Assert.Same(monkeys[2], picker.SelectedItem);
        Assert.Equal(Monkeys, picker.Items);
        Assert.Equal(0, changes);
        Assert.Throws<InvalidOperationException>(() => picker.Items.Add("Gorilla"));

        // The selected item is kept as the collection changes.
        monkeys.Insert(0, new Monkey("Gorilla"));
        Assert.Equal(3, picker.SelectedIndex);
        Assert.Equal("Gorilla", picker.Items[0]);
        monkeys.RemoveAt(3);
        Assert.Equal(-1, picker.SelectedIndex); // the selected one was removed
        Assert.Null(picker.SelectedItem);

        // SelectedItem selects its index; an out-of-range index is clamped.
        picker.SelectedItem = monkeys[1];
        Assert.Equal(1, picker.SelectedIndex);
        picker.SelectedIndex = 50;
        Assert.Equal(monkeys.Count - 1, picker.SelectedIndex);

        // Clearing the source clears the items.
        picker.ItemsSource = null;
        Assert.Empty(picker.Items);
        Assert.Equal(-1, picker.SelectedIndex);
        picker.Items.Add("Again");
        Assert.Single(picker.Items);
    }

    private sealed record Monkey(string Name);

    [Fact]
    public void TheFieldShowsTheSelectionOrTheTitle()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var picker = Picker(Monkeys);
        picker.FontFamily = SkUiTestHelpers.BundledFontFamily;
        var titleSize = picker.Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.True(titleSize.Height >= SkUiLook.Current.DefaultPickerMinimumHeight);
        picker.SelectedIndex = 4; // "Golden Lion Tamarin" is longer than the title
        var selectedSize = picker.Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.True(selectedSize.Width > titleSize.Width);
        picker.MinimumHeightRequest = 10;
        Assert.True(picker.Measure(double.PositiveInfinity, double.PositiveInfinity).Height < SkUiLook.Current.DefaultPickerMinimumHeight);
    }

    [Fact]
    public void BothLayersDrawTheSameField()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var picker = Picker(Monkeys);
        picker.FontFamily = SkUiTestHelpers.BundledFontFamily;
        picker.SelectedIndex = 1;
        SkUiTestHelpers.Arrange(picker, 200, 44);
        var core = new SkUiCorePicker { Title = "Select a monkey", FontFamily = SkUiTestHelpers.BundledFontFamily };
        foreach (var monkey in Monkeys)
            core.Items.Add(monkey);
        core.SetSelectedIndex(1);
        core.Measure(200, 44);
        core.Arrange(new Rect(0, 0, 200, 44));

        using var skia = Render(picker.Paint);
        using var coreBitmap = Render(core.Paint);
        Assert.Equal(skia.Bytes, coreBitmap.Bytes);
        Assert.Contains(skia.Pixels, pixel => pixel.Alpha > 0);
    }

    private static SKBitmap Render(Action<SKCanvas> paint)
    {
        var bitmap = new SKBitmap(200, 44);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        paint(canvas);
        return bitmap;
    }

    [Fact]
    public void TheCorePickerOpensFromItsHost()
    {
        var core = new SkUiCorePicker { Title = "Fruit" };
        core.Items.Add("Apple");
        core.Items.Add("Pear");
        var host = new SkUiCoreHost().SetContent(core);
        var root = Root(host);
        var events = new List<string>();
        core.Opened += (_, _) => events.Add("Opened");
        core.Closed += (_, _) => events.Add("Closed");
        core.SelectedIndexChanged += (_, _) => events.Add($"SelectedIndexChanged {core.SelectedIndex}");

        Assert.True(((ISkUiAccessibleNode)core).PerformSemanticsAction(SkUiSemanticsActions.Activate));
        var request = Shown();
        Assert.Same(host, request.Anchor);
        Assert.Same(root, request.PlatformAnchor);
        Assert.Equal(["Apple", "Pear"], request.Items);
        request.Complete(1);
        _ui.RunPending();

        Assert.Equal(["Opened", "SelectedIndexChanged 1", "Closed"], events);
        Assert.Equal("Pear", core.SelectedItem);
        Assert.False(core.IsOpen);

        // Keys on the closed field, an index before the items.
        Assert.True(core.Focus());
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Up));
        Assert.Equal(0, core.SelectedIndex);
        var empty = new SkUiCorePicker().SetSelectedIndex(1);
        empty.ItemsSource = new List<string> { "A", "B", "C" };
        Assert.Equal(1, empty.SelectedIndex);
        Assert.Equal("B", empty.SelectedItem);
    }

    [Fact]
    public void APerControlPresenterWinsAndTheFallbackIsTheActionSheet()
    {
        var own = new RecordingPopupPresenter();
        var picker = Picker(Monkeys);
        picker.PopupPresenter = own;
        Root(picker);
        picker.IsOpen = true;
        Assert.Single(own.Requests);
        Assert.Empty(_presenter.Requests);

        SkUiPopups.Presenter = null;
        Assert.Same(SkUiActionSheetPopupPresenter.Instance, SkUiPopups.Resolve(null));
        Assert.Same(own, SkUiPopups.Resolve(own));
    }

    [Fact]
    public void WindowPopupsShowTheTitleAndDimThePage()
    {
        var picker = Picker(Monkeys);
        picker.PopupPlacement = SkUiPopupPlacement.Window;
        var window = new Window(new ContentPage { Content = new SkUiContentView { Content = Root(picker) } });
        ((IWindow)window).FrameChanged(new Rect(0, 0, 400, 800));

        picker.IsOpen = true;
        var request = Shown();
        Assert.Equal(SkUiPopupPlacement.Window, request.Placement);
        Assert.Same(window, request.Window);
        Assert.IsType<ContentPage>(request.Page);
        Assert.NotNull(request.OverlayColor);
        Assert.Equal(new Size(400, 800), request.WindowSize);
        // Centered in the window (the look's default), the width the look prefers.
        Assert.Equal(200, request.Bounds.Center.X, 3);
        Assert.Equal(400, request.Bounds.Center.Y, 3);
        Assert.Equal(SkUiLook.Current.PopupPreferredWidth, request.Bounds.Width, 3);
        Assert.IsType<SkUiGrid>(Assert.IsType<SkUiPickerPopup>(request.Content).Content); // a title above the list
    }

    [Fact]
    public void TemplatedRowsBindToTheItems()
    {
        var monkeys = Monkeys.Select(name => new Monkey(name)).ToList();
        var picker = new SkUiPicker
        {
            ItemsSource = monkeys,
            ItemDisplayBinding = new Binding(nameof(Monkey.Name)),
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new SkUiLabel { HeightRequest = 60 };
                label.SetBinding(SkUiLabel.TextProperty, new Binding(nameof(Monkey.Name), stringFormat: "🐒 {0}"));
                return label;
            })
        };
        Root(picker);
        picker.IsOpen = true;
        var request = Shown();
        var content = Assert.IsType<SkUiPickerPopup>(request.Content);
        Assert.Equal(60 * monkeys.Count, request.PreferredSize.Height, 3);
        using var surface = new SkUiTestSurface(content, 300, 300);
        surface.Frame();
        var row = Assert.IsType<SkUiLabel>(((SkUiContentView)content.List.GetRealizedView(1)!).Content);
        Assert.Equal("🐒 Capuchin Monkey", row.Text);
    }

    [Fact]
    public void TemplatedRowsWiderThanTheTextsWidenThePopup()
    {
        var picker = new SkUiPicker
        {
            ItemsSource = new List<string> { "A", "B" },
            PopupPlacement = SkUiPopupPlacement.Anchored,
            ItemTemplate = new DataTemplate(() => new SkUiBox { WidthRequest = 420, HeightRequest = 30 })
        };
        var root = new SkUiVerticalStackLayout { Children = { new SkUiHorizontalStackLayout { Children = { picker } } } };
        SkUiTestHelpers.Arrange(root, 600, 600);

        picker.IsOpen = true;

        Assert.Equal(420, Shown().PreferredSize.Width, 3);
    }

    /// <summary>A presenter whose popup goes only when the test says so (a choice can be made before the items change).</summary>
    private sealed class ManualPopupPresenter : ISkUiPopupPresenter
    {
        private TaskCompletionSource? _shown;

        public SkUiListPopupRequest? Current { get; private set; }

        public Task ShowAsync(SkUiPopupRequest request, CancellationToken cancellationToken)
        {
            Current = (SkUiListPopupRequest)request;
            _shown = new TaskCompletionSource();
            return _shown.Task;
        }

        public void Gone() => _shown?.TrySetResult();
    }

    [Fact]
    public void ItemsChangingWhileOpenCloseThePopupAndAChoiceKeepsItsItem()
    {
        var monkeys = new ObservableCollection<Monkey>(Monkeys.Select(name => new Monkey(name)));
        var manual = new ManualPopupPresenter();
        var picker = new SkUiPicker { ItemsSource = monkeys, ItemDisplayBinding = new Binding(nameof(Monkey.Name)), PopupPresenter = manual };
        Root(picker);

        // A choice made just before the items changed selects the item the popup showed, where it is now.
        picker.IsOpen = true;
        var request = manual.Current!;
        request.Complete(1);
        monkeys.Insert(0, new Monkey("Gorilla"));
        Assert.True(request.CloseRequested.IsCancellationRequested);
        manual.Gone();
        _ui.RunPending();
        Assert.Same(monkeys[2], picker.SelectedItem);
        Assert.False(picker.IsOpen);

        // An item gone meanwhile is not chosen.
        picker.IsOpen = true;
        manual.Current!.Complete(0);
        monkeys.RemoveAt(0);
        manual.Gone();
        _ui.RunPending();
        Assert.Same(monkeys[1], picker.SelectedItem);

        // Items changing while the popup is open close it (it shows the items it opened with).
        picker.IsOpen = true;
        request = manual.Current!;
        monkeys.Add(new Monkey("Gibbon"));
        Assert.True(request.CloseRequested.IsCancellationRequested);
        manual.Gone();
        _ui.RunPending();
        Assert.False(picker.IsOpen);
        Assert.Same(monkeys[1], picker.SelectedItem);
    }

    [Fact]
    public void ASelectionSetWhileOpenMovesTheHighlight()
    {
        var picker = Picker(Monkeys);
        picker.SelectedIndex = 1;
        Root(picker);
        picker.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(Shown().Content);
        Assert.Equal(1, content.HighlightedIndex);

        picker.SelectedIndex = 4;
        Assert.Equal(4, content.HighlightedIndex);
        // Enter chooses what the field shows, not the selection the popup opened with.
        Assert.True(content.OnKeyDown(new SkUiKeyEvent(SkUiKey.Enter, SkUiKeyModifiers.None, false)));
        _ui.RunPending();
        Assert.Equal(4, picker.SelectedIndex);

        picker.IsOpen = true;
        content = Assert.IsType<SkUiPickerPopup>(Shown().Content);
        picker.SelectedIndex = -1;
        Assert.Equal(-1, content.HighlightedIndex);
    }

    [Fact]
    public void APopupThatFailsToBuildLeavesThePickerClosed()
    {
        var picker = Picker(Monkeys);
        picker.ItemTemplate = new DataTemplate(() => throw new InvalidOperationException("Broken template"));
        Root(picker);

        picker.IsOpen = true;
        _ui.RunPending();
        Assert.False(picker.IsPopupShown);
        Assert.False(picker.IsOpen);
        Assert.Empty(_presenter.Requests);

        // It opens again once it can.
        picker.ItemTemplate = null;
        picker.IsOpen = true;
        Assert.True(picker.IsPopupShown);
        Assert.Single(_presenter.Requests);
    }

    [Fact]
    public void ACorePickerKeepsAPendingIndexUntilTheItemsReachIt()
    {
        var core = new SkUiCorePicker().SetSelectedIndex(3);
        for (var index = 0; index < 3; index++)
        {
            core.Items.Add($"Item {index}");
            Assert.Equal(-1, core.SelectedIndex);
        }
        core.Items.Add("Item 3");
        Assert.Equal(3, core.SelectedIndex);
        Assert.Equal("Item 3", core.SelectedItem);

        // A new source applies it at once, clamped (as MAUI's).
        var clamped = new SkUiCorePicker().SetSelectedIndex(9);
        clamped.ItemsSource = new List<string> { "A", "B" };
        Assert.Equal(1, clamped.SelectedIndex);

        // SelectedItem is settable, as SelectedIndex.
        clamped.SelectedItem = "A";
        Assert.Equal(0, clamped.SelectedIndex);
        clamped.SelectedItem = "Missing";
        Assert.Equal(-1, clamped.SelectedIndex);
    }
}

/// <summary>Placing popups (<see cref="SkUiPopupGeometry"/>): anchored, flipped, mirrored, window-aligned, absolute, clamped.</summary>
public class PopupGeometryTests
{
    private static SkUiPopupLayout Layout(Rect anchor, Size preferred, SkUiPopupPlacement placement = SkUiPopupPlacement.Anchored, bool rtl = false,
        LayoutOptions? horizontal = null, LayoutOptions? vertical = null, double maximumHeight = double.PositiveInfinity, Rect absolute = default) =>
        SkUiPopupGeometry.Layout(new SkUiPopupPlacementInput(anchor, anchor, new Size(400, 800), new Thickness(0, 40, 0, 20), new Thickness(8), 4, placement,
            horizontal ?? LayoutOptions.Center, vertical ?? LayoutOptions.Center, preferred, maximumHeight, absolute, rtl));

    [Fact]
    public void AnchoredPopupsOpenBelowAtLeastAsWideAsTheAnchor()
    {
        var layout = Layout(new Rect(20, 100, 200, 40), new Size(120, 300));
        Assert.Equal(new Rect(20, 144, 200, 300), layout.Bounds);
        Assert.False(layout.OpensAbove);
        Assert.Equal(100 - 4 - 48, layout.SpaceAbove);
        Assert.Equal(800 - 20 - 8 - 140 - 4, layout.SpaceBelow);
    }

    [Fact]
    public void AnchoredPopupsFlipAboveWhenThereIsMoreRoom()
    {
        var layout = Layout(new Rect(20, 600, 200, 40), new Size(200, 500));
        Assert.True(layout.OpensAbove);
        Assert.Equal(new Rect(20, 96, 200, 500), layout.Bounds);
        var tall = Layout(new Rect(20, 600, 200, 40), new Size(200, 900));
        Assert.True(tall.OpensAbove);
        Assert.Equal(600 - 4, tall.Bounds.Bottom, 3);
        Assert.Equal(48, tall.Bounds.Top, 3); // shortened to the room there is (the safe area plus the margin)
        // Fits below: stays below even with more room above.
        Assert.False(Layout(new Rect(20, 600, 200, 40), new Size(200, 100)).OpensAbove);
    }

    [Fact]
    public void AnchoredPopupsWithoutRoomBesideTheAnchorStayInTheSafeArea()
    {
        // An anchor filling the safe area: no room above (40 + 8) or below (800 - 20 - 8); the popup keeps the minimum
        // extent, inside the safe area less the margin, over the anchor.
        var layout = Layout(new Rect(20, 50, 200, 720), new Size(200, 300));
        Assert.Equal(48, layout.Bounds.Height, 3);
        Assert.InRange(layout.Bounds.Top, 48, 772 - 48);
        Assert.True(layout.Bounds.Bottom <= 772 + 0.001);
        // Never taller than the safe area.
        var short_ = SkUiPopupGeometry.Layout(new SkUiPopupPlacementInput(new Rect(0, 10, 100, 20), new Rect(0, 10, 100, 20), new Size(200, 60),
            new Thickness(0), new Thickness(8), 4, SkUiPopupPlacement.Anchored, LayoutOptions.Center, LayoutOptions.Center, new Size(100, 300),
            double.PositiveInfinity, default, false));
        Assert.Equal(44, short_.Bounds.Height, 3);
        Assert.Equal(8, short_.Bounds.Top, 3);
    }

    [Fact]
    public void AnchoredPopupsAlignToTheEndInRightToLeftAndStayInTheWindow()
    {
        var rtl = Layout(new Rect(180, 100, 200, 40), new Size(300, 100), rtl: true);
        Assert.Equal(380, rtl.Bounds.Right, 3);
        var wide = Layout(new Rect(300, 100, 80, 40), new Size(300, 100));
        Assert.Equal(392, wide.Bounds.Right, 3); // pushed back inside the margin
        Assert.Equal(384, Layout(new Rect(0, 100, 600, 40), new Size(600, 100)).Bounds.Width, 3);
    }

    [Fact]
    public void WindowPopupsAlignInTheSafeArea()
    {
        var centered = Layout(new Rect(20, 100, 200, 40), new Size(300, 200), SkUiPopupPlacement.Window);
        Assert.Equal(new Rect(50, 40 + 8 + (800 - 60 - 16 - 200) / 2d, 300, 200), centered.Bounds);
        var sheet = Layout(new Rect(20, 100, 200, 40), new Size(300, 200), SkUiPopupPlacement.Window, horizontal: LayoutOptions.Fill, vertical: LayoutOptions.End);
        Assert.Equal(new Rect(8, 800 - 20 - 8 - 200, 384, 200), sheet.Bounds);
        var tall = Layout(new Rect(20, 100, 200, 40), new Size(300, 2000), SkUiPopupPlacement.Window, maximumHeight: 250);
        Assert.Equal(250, tall.Bounds.Height, 3);
        Assert.Equal(250, tall.MaximumSize.Height, 3);
        var start = Layout(new Rect(20, 100, 200, 40), new Size(300, 200), SkUiPopupPlacement.Window, rtl: true, horizontal: LayoutOptions.Start);
        Assert.Equal(392, start.Bounds.Right, 3); // Start is the right in right-to-left
    }

    [Fact]
    public void AbsolutePopupsUseTheAppsRectangleElseTheWindow()
    {
        var absolute = Layout(new Rect(20, 100, 200, 40), new Size(300, 200), SkUiPopupPlacement.Absolute, absolute: new Rect(5, 6, 70, 80));
        Assert.Equal(new Rect(5, 6, 70, 80), absolute.Bounds);
        Assert.Equal(SkUiPopupPlacement.Absolute, absolute.Placement);
        Assert.Equal(SkUiPopupPlacement.Window, Layout(new Rect(20, 100, 200, 40), new Size(300, 200), SkUiPopupPlacement.Absolute).Placement);
    }
}

/// <summary>MAUI's <c>Picker</c> documentation samples with the prefix changed load, bind and select (XAML ports by prefix).</summary>
[Collection(RuntimeXamlCollection.Name)]
public class PickerXamlTests
{
    [Fact]
    public void TheDocumentedItemsLoad()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiPicker x:Name="picker" Title="Select a monkey" TitleColor="Red" SelectedIndex="1"
                             HorizontalTextAlignment="Center" FontSize="18" FontAttributes="Bold" CharacterSpacing="1"
                             PopupPlacement="Window" MaxPopupHeight="300">
                <sk:SkUiPicker.ItemsSource>
                  <x:Array Type="{x:Type x:String}">
                    <x:String>Baboon</x:String>
                    <x:String>Capuchin Monkey</x:String>
                    <x:String>Blue Monkey</x:String>
                    <x:String>Squirrel Monkey</x:String>
                    <x:String>Golden Lion Tamarin</x:String>
                    <x:String>Howler Monkey</x:String>
                    <x:String>Japanese Macaque</x:String>
                  </x:Array>
                </sk:SkUiPicker.ItemsSource>
              </sk:SkUiPicker>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var picker = Assert.IsType<SkUiPicker>(root.Content);

        Assert.Equal(7, picker.Items.Count);
        Assert.Equal(1, picker.SelectedIndex);
        Assert.Equal("Capuchin Monkey", picker.SelectedItem);
        Assert.Equal(Colors.Red, picker.TitleColor);
        Assert.Equal((TextAlignment.Center, 18d, FontAttributes.Bold), (picker.HorizontalTextAlignment, picker.FontSize, picker.FontAttributes));
        Assert.Equal((SkUiPopupPlacement.Window, 300d), (picker.PopupPlacement, picker.MaxPopupHeight));
    }

    [Fact]
    public void TheDocumentedBindingsWorkBothWays()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiPicker Title="Select a monkey"
                             ItemsSource="{Binding Monkeys}"
                             ItemDisplayBinding="{Binding Name}"
                             SelectedItem="{Binding SelectedMonkey}"
                             IsOpen="{Binding IsPickerOpen}" />
            </ContentView>
            """;
        var model = new MonkeysModel();
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var picker = Assert.IsType<SkUiPicker>(root.Content);

        Assert.Equal(["Baboon", "Capuchin Monkey", "Blue Monkey"], picker.Items);
        picker.SelectedIndex = 2;
        Assert.Same(model.Monkeys[2], model.SelectedMonkey);
        model.SelectedMonkey = model.Monkeys[0];
        Assert.Equal(0, picker.SelectedIndex);
        Assert.False(model.IsPickerOpen);
    }

    public sealed class MonkeysModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public List<Monkey> Monkeys { get; } = [new("Baboon"), new("Capuchin Monkey"), new("Blue Monkey")];

        public Monkey? SelectedMonkey
        {
            get;
            set
            {
                field = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(SelectedMonkey)));
            }
        }

        public bool IsPickerOpen { get; set; }
    }

    public sealed record Monkey(string Name);
}

/// <summary>
/// SkiaUi.Maui.CommunityToolkit: the toolkit popup a request becomes (placed at the request's rectangle, or aligned in the
/// window's safe area) and the app-wide registration. Showing it needs the toolkit's modal pages (device runs).
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class ToolkitPopupPresenterTests : IDisposable
{
    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;
    private readonly RecordingPopupPresenter _recorder = new();

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    private SkUiListPopupRequest Request(SkUiPopupPlacement placement)
    {
        var picker = new SkUiPicker { Title = "Fruit", ItemsSource = new[] { "Apple", "Pear" }, PopupPlacement = placement, PopupPresenter = _recorder };
        var window = new Window(new ContentPage { Content = new SkUiContentView { Content = picker } });
        ((IWindow)window).FrameChanged(new Rect(0, 0, 400, 800));
        SkUiTestHelpers.Arrange((SkUiContentView)((ContentPage)window.Page!).Content, 400, 800);
        picker.IsOpen = true;
        return Assert.IsType<SkUiListPopupRequest>(_recorder.Current);
    }

    [Fact]
    public void AnchoredPopupsSitAtTheRequestsRectangleOverAClearPage()
    {
        var request = Request(SkUiPopupPlacement.Anchored);
        var (popup, options) = MauiSkiaUi.CommunityToolkit.SkUiToolkitPopupPresenter.Instance.Create(request, request.Content!);

        Assert.Same(request.Content, popup.Content);
        Assert.Equal(new Thickness(request.Bounds.Left, request.Bounds.Top, 0, 0), popup.Margin);
        Assert.Equal((LayoutOptions.Start, LayoutOptions.Start), (popup.HorizontalOptions, popup.VerticalOptions));
        Assert.Equal(new Thickness(0), popup.Padding);
        Assert.True(popup.CanBeDismissedByTappingOutsideOfPopup);
        Assert.Equal(Colors.Transparent, options.PageOverlayColor);
    }

    [Fact]
    public void WindowPopupsAlignInTheSafeAreaOverADimmedPage()
    {
        var request = Request(SkUiPopupPlacement.Window);
        var (popup, options) = MauiSkiaUi.CommunityToolkit.SkUiToolkitPopupPresenter.Instance.Create(request, request.Content!);

        Assert.Equal((request.HorizontalOptions, request.VerticalOptions), (popup.HorizontalOptions, popup.VerticalOptions));
        var margin = request.Margin;
        var safe = request.SafeAreaInsets;
        Assert.Equal(new Thickness(margin.Left + safe.Left, margin.Top + safe.Top, margin.Right + safe.Right, margin.Bottom + safe.Bottom), popup.Margin);
        Assert.Equal(request.OverlayColor, options.PageOverlayColor);
    }

    [Fact]
    public void PopupsTakeTheLooksCornersAndShadowAndThePresenterFollowsThem()
    {
        var previous = SkUiLook.Current;
        try
        {
            SkUiLook.Current = AndroidMaterial3SkUiLook.Instance;
            var window = Request(SkUiPopupPlacement.Window);
            Assert.Equal(new CornerRadius(28), window.CornerRadius);
            Assert.NotNull(window.Shadow);
            var clip = Assert.IsType<RoundRectangleGeometry>(window.Content!.Clip);
            Assert.Equal(new CornerRadius(28), clip.CornerRadius);
            Assert.Equal(new Rect(0, 0, window.Bounds.Width, window.Bounds.Height), clip.Rect);
            var (_, options) = MauiSkiaUi.CommunityToolkit.SkUiToolkitPopupPresenter.Instance.Create(window, window.Content);
            Assert.Equal(new CornerRadius(28), ((RoundRectangle)options.Shape!).CornerRadius);
            Assert.Same(window.Shadow, options.Shadow);

            var anchored = Request(SkUiPopupPlacement.Anchored);
            Assert.Equal(new CornerRadius(4), anchored.CornerRadius);
            Assert.NotSame(window.Shadow, anchored.Shadow); // a new shadow for each popup

            // The presenter's own values win over the look's.
            var own = new MauiSkiaUi.CommunityToolkit.SkUiToolkitPopupPresenter { CornerRadius = 12, Shadow = null };
            (_, options) = own.Create(anchored, anchored.Content!);
            Assert.Equal(new CornerRadius(12), ((RoundRectangle)options.Shape!).CornerRadius);
            Assert.Null(options.Shadow);

            SkUiLook.Current = DefaultSkUiLook.Instance;
            var pane = Request(SkUiPopupPlacement.BottomPane);
            Assert.Equal(default, pane.CornerRadius);
            Assert.Null(pane.Content!.Clip);
            Assert.Equal(new CornerRadius(10), Request(SkUiPopupPlacement.Window).CornerRadius);
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    [Fact]
    public void TheBuilderExtensionRegistersThePresenterAppWide()
    {
        SkUiPopups.Presenter = null;
        MauiSkiaUi.CommunityToolkit.SkUiCommunityToolkitAppBuilderExtensions.UseSkiaUiCommunityToolkit(MauiApp.CreateBuilder(useDefaults: false));
        Assert.Same(MauiSkiaUi.CommunityToolkit.SkUiToolkitPopupPresenter.Instance, SkUiPopups.Presenter);
    }
}

public class ToolkitPopupOffsetTests
{
    [Fact]
    public void MovingAPopupShiftsBothOppositeMargins()
    {
        // A bottom sheet shown 34 too high (the page inset by the bottom safe area): down by 34 whatever the alignment.
        Assert.Equal(new Thickness(8, 104, 8, 8), MauiSkiaUi.CommunityToolkit.SkUiToolkitPopupPresenter.Moved(new Thickness(8, 70, 8, 42), 0, 34));
        Assert.Equal(new Thickness(15, 20, -5, 0), MauiSkiaUi.CommunityToolkit.SkUiToolkitPopupPresenter.Moved(new Thickness(10, 20, 0, 0), 5, 0));
    }
}
