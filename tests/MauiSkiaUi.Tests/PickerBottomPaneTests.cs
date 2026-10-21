using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <see cref="SkUiPopupPlacement.BottomPane"/>: docked to the window's bottom edge, edge to edge, with a toolbar on top (a close button ×, and
/// Cancel when the choice waits for OK); a picker's list as a wheel on looks that show one (<see cref="SkUiLook.PickerPaneShowsWheel"/>),
/// choosing as it turns (<see cref="SkUiListPopupRequest.SetSelectedIndex"/>). In the global collection: the look and the app-wide
/// presenter are process-wide.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerBottomPaneTests : IDisposable
{
    private const string Done = "Done";
    private const string Cancel = "Cancel";
    private const string Clear = "Clear";
    private static readonly string[] Monkeys = ["Baboon", "Capuchin Monkey", "Blue Monkey", "Squirrel Monkey"];

    public sealed record Monkey(string Name, bool IsAvailable);

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;
    private readonly SkUiLook _look = SkUiLook.Current;
    private readonly List<SkUiTestSurface> _surfaces = [];

    public PickerBottomPaneTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        SkUiLook.Current = _look;
        foreach (var surface in _surfaces)
            surface.Dispose();
        _ui.Dispose();
    }

    /// <summary>A picker in a 400 × 800 window.</summary>
    private static T InWindow<T>(T picker) where T : SkUiView
    {
        var root = new SkUiContentView { Content = picker };
        var window = new Window(new ContentPage { Content = root });
        ((IWindow)window).FrameChanged(new Rect(0, 0, 400, 800));
        SkUiTestHelpers.Arrange(root, 400, 800);
        return picker;
    }

    private static SkUiPicker Picker(SkUiPickerUpdateMode mode = SkUiPickerUpdateMode.Immediately, bool clear = false)
    {
        var picker = new SkUiPicker { Title = "Monkey", PopupPlacement = SkUiPopupPlacement.BottomPane, UpdateMode = mode, IsPopupClearButtonVisible = clear };
        foreach (var monkey in Monkeys)
            picker.Items.Add(monkey);
        return InWindow(picker);
    }

    private SkUiListPopupRequest Open(SkUiPicker picker)
    {
        picker.IsOpen = true;
        return Shown<SkUiListPopupRequest>();
    }

    /// <summary>The request shown now, its content laid out at its bounds (as a presenter shows it).</summary>
    private T Shown<T>() where T : SkUiPopupRequest
    {
        var request = Assert.IsType<T>(_presenter.Current);
        var surface = new SkUiTestSurface((SkUiContentView)request.Content!, (int)request.Bounds.Width, (int)request.Bounds.Height);
        surface.Frame();
        _surfaces.Add(surface);
        return request;
    }

    private static IEnumerable<ISkUiView> Descendants(ISkUiView view)
    {
        yield return view;
        if (view is SkUiView node)
            foreach (var child in node.SkiaChildren)
                foreach (var descendant in Descendants(child))
                    yield return descendant;
    }

    private static SkUiButton Button(View content, string text) => Descendants((ISkUiView)content).OfType<SkUiButton>().Single(button => button.Text == text);

    /// <summary>The toolbar's close button (×), the Done of iOS's picker.</summary>
    private static SkUiPopupCloseButton Close(View content) => Descendants((ISkUiView)content).OfType<SkUiPopupCloseButton>().Single();

    private void Tap(SkUiView view)
    {
        Assert.NotNull(SkUiDiagnostics.SimulateTap(view));
        _ui.RunPending();
    }

    /// <summary>The content's toolbar: the first row of its root.</summary>
    private static SkUiGrid Toolbar(SkUiContentView content)
    {
        var root = Assert.IsType<SkUiGrid>(content.Content);
        return Assert.IsType<SkUiGrid>(root.Children.Single(child => Grid.GetRow((BindableObject)child) == 0));
    }

    [Fact]
    public void PanesSpanTheWindowAtItsBottomEdge()
    {
        static SkUiPopupLayout Pane(Size preferred, Thickness safe, double maximumHeight = double.PositiveInfinity) =>
            SkUiPopupGeometry.Layout(new SkUiPopupPlacementInput(new Rect(20, 100, 200, 40), new Rect(20, 100, 200, 40), new Size(400, 800), safe,
                new Thickness(8), 4, SkUiPopupPlacement.BottomPane, LayoutOptions.Center, LayoutOptions.Center, preferred, maximumHeight, default, false, 100, 200));

        // Edge to edge (the app's widths and the margin do not apply), down to the window's bottom: the home indicator's inset is added.
        var pane = Pane(new Size(300, 260), new Thickness(0, 47, 0, 34));
        Assert.Equal(SkUiPopupPlacement.BottomPane, pane.Placement);
        Assert.Equal(new Rect(0, 800 - 294, 400, 294), pane.Bounds);
        Assert.Equal(new Size(400, 800 - 47 - 8), pane.MaximumSize);
        // The control's maximum limits the content, not the inset.
        Assert.Equal(200 + 34, Pane(new Size(300, 260), new Thickness(0, 47, 0, 34), maximumHeight: 200).Bounds.Height, 3);
        // Never above the safe area's top less the margin.
        Assert.Equal(47 + 8, Pane(new Size(300, 2000), new Thickness(0, 47, 0, 34)).Bounds.Top, 3);
        // The content pads itself by the sides and the bottom of the safe area.
        Assert.Equal(new Thickness(44, 0, 44, 21), SkUiPopupGeometry.PaneContentPadding(new Thickness(44, 0, 44, 21)));
    }

    [Fact]
    public void PanesSpanThePlatformWindowNotMauisFrame()
    {
        // Mac Catalyst's iPad idiom: MAUI's frame is in screen points (77 % of the app's), the platform window in the app's.
        var window = new Window(new ContentPage());
        ((IWindow)window).FrameChanged(new Rect(0, 0, 859, 907));
        Assert.Equal(new Size(1116, 1178), SkUiWindowMetrics.WindowSize(window, new Size(1116, 1178)));
        // Without a platform window (headless), the frame.
        Assert.Equal(new Size(859, 907), SkUiWindowMetrics.WindowSize(window, Size.Zero));
        Assert.Equal(Size.Zero, SkUiWindowMetrics.WindowSize(null, Size.Zero));
    }

    [Fact]
    public void APickersPaneDocksWithAToolbarOverAClearPage()
    {
        var picker = Picker();
        picker.SelectedIndex = 1;
        var request = Open(picker);

        Assert.Equal(SkUiPopupPlacement.BottomPane, request.Placement);
        Assert.Equal(400, request.Bounds.Width, 3);
        Assert.Equal(0, request.Bounds.Left, 3);
        Assert.Equal(800, request.Bounds.Bottom, 3);
        Assert.Null(request.OverlayColor);
        // The default look keeps the list; its title and the close button are in the toolbar on top.
        var content = Assert.IsType<SkUiPickerPopup>(request.Content);
        var toolbar = Toolbar(content);
        Assert.Contains(Descendants(toolbar).OfType<SkUiLabel>(), label => label.Text == "Monkey");
        Assert.Empty(Descendants(toolbar).OfType<SkUiButton>());
        // A close button (×) instead of Done, which screen readers read as Done.
        var close = Assert.Single(Descendants(toolbar).OfType<SkUiPopupCloseButton>());
        Assert.Equal(Done, SemanticProperties.GetDescription(close));

        Tap(Close(content));
        Assert.Equal(1, _presenter.Closes);
        Assert.Equal(1, picker.SelectedIndex);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void ThePanesToolbarHasCancelAndClearBesideTheCloseButton()
    {
        var picker = Picker(SkUiPickerUpdateMode.WhenFinished, clear: true);
        var request = Open(picker);
        var toolbar = Toolbar((SkUiContentView)request.Content!);
        Assert.Equal([Cancel, Clear], Descendants(toolbar).OfType<SkUiButton>().Select(button => button.Text));
        Assert.Single(Descendants(toolbar).OfType<SkUiPopupCloseButton>());
        // Too wide to center in the pane next to Cancel and Clear, the title moves just clear of them.
        var title = Descendants(toolbar).OfType<SkUiLabel>().Single(label => label.Text == "Monkey");
        Assert.Equal(Button(request.Content!, Clear).Frame.Right + 8, title.Frame.X, 0.5);
        // With Cancel only, the title is centered in the pane although only one side has buttons.
        request.Cancel();
        var cancelOnly = Toolbar((SkUiContentView)Open(Picker(SkUiPickerUpdateMode.WhenFinished)).Content!);
        var row = Assert.IsType<SkUiHorizontalAlignStackLayout>(cancelOnly.Children[0]);
        title = Descendants(cancelOnly).OfType<SkUiLabel>().Single(label => label.Text == "Monkey");
        Assert.Equal(cancelOnly.Width / 2, row.Frame.X + title.Frame.Center.X, 0.5);
        // The picker's own accept text names the close button.
        _presenter.Current!.Cancel();
        picker.AcceptText = "Use";
        Assert.Equal("Use", SemanticProperties.GetDescription(Close(Open(picker).Content!)));
    }

    [Fact]
    public void TheIosLooksPaneShowsAWheelThatChoosesAsItTurns()
    {
        SkUiLook.Current = new IosSkUiLook();
        var picker = Picker();
        var changes = new List<int>();
        picker.SelectedIndexChanged += (_, _) => changes.Add(picker.SelectedIndex);
        var request = Open(picker);

        var wheel = Assert.IsType<SkUiColumnsPopup>(request.Content);
        // Nothing chosen: the wheel shows the first item, the one closing takes.
        Assert.Equal(0, wheel.Wheel(0).Position);
        Assert.Equal(0, request.CurrentIndex);
        Assert.Equal(-1, picker.SelectedIndex);

        wheel.Wheel(0).Position = 2;
        Assert.Equal(2, picker.SelectedIndex);
        Assert.Equal(2, request.CurrentIndex);
        Assert.True(picker.IsOpen);
        Assert.Equal(0, _presenter.Closes);

        // A dismissal keeps what was chosen.
        _presenter.Dismiss();
        Assert.Equal(2, picker.SelectedIndex);
        Assert.Equal([2], changes);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void ClosingAWheelChoosesTheItemItShows()
    {
        SkUiLook.Current = new IosSkUiLook();
        var picker = Picker();
        var request = Open(picker);
        Tap(Close(request.Content!));
        Assert.Equal(0, picker.SelectedIndex);
        Assert.Equal(1, _presenter.Closes);
    }

    [Fact]
    public void AWheelThatWaitsForDoneAppliesNothingUntilThen()
    {
        SkUiLook.Current = new IosSkUiLook();
        var picker = Picker(SkUiPickerUpdateMode.WhenFinished);
        picker.SelectedIndex = 1;
        var request = Open(picker);
        var wheel = Assert.IsType<SkUiColumnsPopup>(request.Content);

        wheel.Wheel(0).Position = 3;
        Assert.Equal(1, picker.SelectedIndex);
        Assert.Equal(3, request.CurrentIndex);
        Tap(Button(wheel, Cancel));
        Assert.Equal(1, picker.SelectedIndex);

        request = Open(picker);
        wheel = Assert.IsType<SkUiColumnsPopup>(request.Content);
        wheel.Wheel(0).Position = 3;
        Tap(Close(wheel));
        Assert.Equal(3, picker.SelectedIndex);
    }

    [Fact]
    public void ClearOnAWheelClearsAndCloses()
    {
        SkUiLook.Current = new IosSkUiLook();
        foreach (var mode in new[] { SkUiPickerUpdateMode.Immediately, SkUiPickerUpdateMode.WhenFinished })
        {
            var picker = Picker(mode, clear: true);
            picker.SelectedIndex = 2;
            var request = Open(picker);
            Tap(Button(request.Content!, Clear));
            Assert.Equal(-1, picker.SelectedIndex);
            Assert.True(request.IsCompleted);
            Assert.False(picker.IsOpen);
        }
    }

    [Fact]
    public void AWheelSkipsDisabledItemsAndFollowsTheApp()
    {
        SkUiLook.Current = new IosSkUiLook();
        var picker = InWindow(new SkUiPicker
        {
            Title = "Monkey",
            PopupPlacement = SkUiPopupPlacement.BottomPane,
            ItemsSource = Monkeys.Select((name, index) => new Monkey(name, index != 2)).ToList(),
            ItemDisplayBinding = Binding.Create(static (Monkey monkey) => monkey.Name),
            ItemEnabledBinding = Binding.Create(static (Monkey monkey) => monkey.IsAvailable)
        });
        var request = Open(picker);
        var wheel = Assert.IsType<SkUiColumnsPopup>(request.Content);

        Assert.False(request.SetSelectedIndex(2));
        Assert.False(request.SetSelectedIndex(9));
        Assert.Equal(-1, picker.SelectedIndex);
        Assert.True(request.SetSelectedIndex(3));
        Assert.Equal(3, picker.SelectedIndex);

        picker.SelectedIndex = 1;
        Assert.Equal(1, wheel.Wheel(0).Position);
        Assert.Equal(1, request.CurrentIndex);
    }

    [Fact]
    public void ListsWithSeveralChoicesAndWheelsOfColumnsGetTheToolbarToo()
    {
        SkUiLook.Current = new IosSkUiLook();
        var multiple = new SkUiPicker { Title = "Monkeys", PopupPlacement = SkUiPopupPlacement.BottomPane, SelectionMode = SkUiSelectionMode.Multiple };
        foreach (var monkey in Monkeys)
            multiple.Items.Add(monkey);
        InWindow(multiple).IsOpen = true;
        var list = Shown<SkUiMultiListPopupRequest>();
        Assert.IsType<SkUiPickerPopup>(list.Content);
        Assert.Single(Descendants(Toolbar((SkUiContentView)list.Content!)).OfType<SkUiPopupCloseButton>());
        list.Cancel();

        var columns = new SkUiMultiColumnPicker { Title = "Time", PopupPlacement = SkUiPopupPlacement.BottomPane };
        columns.Columns.Add(new SkUiPickerColumn { ItemsSource = new[] { "09", "10" }, SelectedIndex = 0 });
        columns.Columns.Add(new SkUiPickerColumn { ItemsSource = new[] { "00", "30" }, SelectedIndex = 0 });
        InWindow(columns).IsOpen = true;
        var wheels = Shown<SkUiColumnsPopupRequest>();
        Assert.Equal(SkUiPopupPlacement.BottomPane, wheels.Placement);
        Tap(Close(wheels.Content!));
        Assert.True(wheels.IsCompleted);
    }

    [Fact]
    public void TheCorePickerOpensTheSameWheel()
    {
        SkUiLook.Current = new IosSkUiLook();
        var core = new Core.SkUiCorePicker { Title = "Monkey" }.SetPopupPlacement(SkUiPopupPlacement.BottomPane);
        foreach (var monkey in Monkeys)
            core.Items.Add(monkey);
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(new Core.SkUiCoreHost().SetContent(core));
        InWindow(stack);
        core.IsOpen = true;
        var request = Shown<SkUiListPopupRequest>();
        var wheel = Assert.IsType<SkUiColumnsPopup>(request.Content);
        Assert.Equal(SkUiPopupPlacement.BottomPane, request.Placement);

        wheel.Wheel(0).Position = 1;
        Assert.Equal(1, core.SelectedIndex);
        Assert.True(core.IsOpen);
        Tap(Close(wheel));
        Assert.False(core.IsOpen);
        Assert.Equal(1, core.SelectedIndex);
    }

    [Fact]
    public void TheToolkitShowsPanesSquareAtThePagesBottom()
    {
        var request = Open(Picker());
        var (popup, options) = MauiSkiaUi.CommunityToolkit.SkUiToolkitPopupPresenter.Instance.Create(request, request.Content!);
        // At the bottom of the popup page, whose inset (iOS: the safe area) leaves the pane's full height above it.
        // Never all zero: the toolkit replaces an empty margin with its default one.
        Assert.False(popup.Margin.IsEmpty);
        Assert.Equal(0, popup.Margin.Left, 3);
        Assert.Equal(0, popup.Margin.Top, 2);
        Assert.Equal((LayoutOptions.Start, LayoutOptions.End), (popup.HorizontalOptions, popup.VerticalOptions));
        Assert.Equal(new CornerRadius(0), Assert.IsType<Microsoft.Maui.Controls.Shapes.RoundRectangle>(options.Shape).CornerRadius);
        Assert.Equal(Colors.Transparent, options.PageOverlayColor);
    }
}
