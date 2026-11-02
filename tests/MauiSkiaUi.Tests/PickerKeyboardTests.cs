using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// An anchored searchable picker list and the soft keyboard: it stays above the keyboard while it shows (shorter below its field,
/// flipped above it, or on the keyboard), returns to its bounds when it hides, and a window resized by the keyboard keeps it open.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerKeyboardTests : IDisposable
{
    private static readonly string[] Cities = ["Zürich", "Berlin", "Bern", "Paris", "Prague", "Rome"];

    private const double Tolerance = 0.01;

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previousPresenter = SkUiPopups.Presenter;
    private readonly SkUiLook _previousLook = SkUiLook.Current;
    private readonly List<SkUiTestSurface> _surfaces = [];

    public PickerKeyboardTests()
    {
        SkUiLook.Current = new DefaultSkUiLook();
        SkUiPopups.Presenter = _presenter;
    }

    public void Dispose()
    {
        SkUiWindowMetrics.SoftKeyboardOverride = null;
        SkUiPopups.Presenter = _previousPresenter;
        SkUiLook.Current = _previousLook;
        foreach (var surface in _surfaces)
            surface.Dispose();
        _ui.Dispose();
    }

    private static double Margin => SkUiLook.Current.PopupMargin.Bottom;

    private static SkUiPicker Picker(LayoutOptions? vertical = null)
    {
        var picker = new SkUiPicker { Title = "City", IsSearchEnabled = true, PopupPlacement = SkUiPopupPlacement.Anchored, VerticalOptions = vertical ?? LayoutOptions.Start };
        foreach (var city in Cities)
            picker.Items.Add(city);
        var root = new SkUiContentView { Content = picker };
        var window = new Window(new ContentPage { Content = root });
        ((IWindow)window).FrameChanged(new Rect(0, 0, 400, 800));
        SkUiTestHelpers.Arrange(root, 400, 800);
        return picker;
    }

    /// <summary>Opens the picker and follows its bounds, as the Toolkit presenter does.</summary>
    private (SkUiPickerPopup Content, SkUiPopupRequest Request) Open(SkUiPicker picker)
    {
        picker.IsOpen = true;
        var request = _presenter.Current!;
        request.BoundsChanged += (_, _) => { };
        var content = Assert.IsType<SkUiPickerPopup>(request.Content);
        var surface = new SkUiTestSurface(content, (int)request.Bounds.Width, (int)request.Bounds.Height);
        surface.Frame();
        _surfaces.Add(surface);
        return (content, request);
    }

    private static bool SearchUnderRows(SkUiPickerPopup content) => Grid.GetRow(content.SearchBar!) > Grid.GetRow(content.List);

    [Fact]
    public void BelowItsFieldThePopupGetsShorterAboveTheKeyboardAndGrowsBackWhenItHides()
    {
        var (content, request) = Open(Picker());
        var opened = request.Bounds;
        Assert.False(request.OpensAbove);

        // Reported by the platform's text input, in the popup surface's coordinates (headless: the window's).
        var keyboardTop = opened.Bottom - 100;
        content.FocusManager.KeyboardMoved(keyboardTop);
        var bounds = request.Bounds;
        Assert.Equal(opened.Top, bounds.Top, Tolerance);
        Assert.Equal(keyboardTop - Margin, bounds.Bottom, Tolerance);
        Assert.Equal(opened.Width, bounds.Width, Tolerance);
        Assert.Equal(bounds.Height, content.HeightRequest, Tolerance);
        Assert.False(SearchUnderRows(content));

        // A search while the keyboard shows: the matches, never under the keyboard.
        content.Filter("ber");
        var rows = 2 * SkUiLook.Current.PopupItemHeight + content.SearchBar!.Measure(opened.Width, double.PositiveInfinity).Height;
        Assert.Equal(Math.Min(rows, keyboardTop - Margin - opened.Top), request.Bounds.Height, Tolerance);

        content.FocusManager.KeyboardMoved(double.PositiveInfinity);
        Assert.Equal(rows, request.Bounds.Height, Tolerance);
        content.Filter("");
        Assert.Equal(opened, request.Bounds);
        Assert.Equal(opened.Height, content.HeightRequest, Tolerance);
    }

    [Fact]
    public void WithTooLittleRoomBelowItsFieldThePopupSitsOnTheKeyboard()
    {
        var (content, request) = Open(Picker());
        var opened = request.Bounds;
        // Room for less than the search field and a row between the field and the keyboard; none above the field (at the top).
        var keyboardTop = opened.Top + 20;
        content.KeyboardMoved(keyboardTop);
        var bounds = request.Bounds;
        Assert.Equal(keyboardTop - Margin, bounds.Bottom, Tolerance);
        Assert.True(bounds.Top >= SkUiLook.Current.PopupMargin.Top - Tolerance);
        Assert.True(bounds.Height > 0);
        // Resizing against its bottom edge: the search field is under the rows, next to the keyboard.
        Assert.True(SearchUnderRows(content));

        content.KeyboardMoved(double.PositiveInfinity);
        Assert.Equal(opened, request.Bounds);
        Assert.False(SearchUnderRows(content));
    }

    [Fact]
    public void AbovePopupTheKeyboardCoversMovesUpOntoIt()
    {
        var (content, request) = Open(Picker(LayoutOptions.End));
        var opened = request.Bounds;
        Assert.True(request.OpensAbove);
        var keyboardTop = opened.Bottom - 50;
        content.KeyboardMoved(keyboardTop);
        var bounds = request.Bounds;
        Assert.Equal(keyboardTop - Margin, bounds.Bottom, Tolerance);
        Assert.Equal(opened.Height, bounds.Height, Tolerance);
        Assert.True(SearchUnderRows(content));

        content.KeyboardMoved(double.PositiveInfinity);
        Assert.Equal(opened, request.Bounds);
        Assert.True(SearchUnderRows(content));
    }

    [Fact]
    public void AKeyboardBelowThePopupMovesNothing()
    {
        var (content, request) = Open(Picker());
        var changes = 0;
        request.BoundsChanged += (_, _) => changes++;
        content.KeyboardMoved(request.Bounds.Bottom + 100);
        content.KeyboardMoved(double.PositiveInfinity);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void WithoutAFollowingPresenterTheKeyboardMovesNothing()
    {
        Picker().IsOpen = true;
        var request = _presenter.Current!;
        var content = Assert.IsType<SkUiPickerPopup>(request.Content);
        var opened = request.Bounds;
        content.KeyboardMoved(opened.Top + 50);
        Assert.Equal(opened, request.Bounds);
        Assert.Equal(opened.Height, content.HeightRequest, Tolerance);
    }

    [Fact]
    public void AWindowResizedByTheKeyboardKeepsTheAnchoredPopupOpen()
    {
        var picker = Picker();
        var (_, request) = Open(picker);
        var window = (IWindow)picker.Window!;

        // Only the height, while the keyboard shows (Android's adjustResize): kept open.
        SkUiWindowMetrics.SoftKeyboardOverride = static () => true;
        window.FrameChanged(new Rect(0, 0, 400, 500));
        _ui.RunPending();
        Assert.False(request.CloseRequested.IsCancellationRequested);

        // The width (a turn, split view): closed, keyboard or not.
        window.FrameChanged(new Rect(0, 0, 600, 500));
        _ui.RunPending();
        Assert.True(request.CloseRequested.IsCancellationRequested);
    }

    [Fact]
    public void AWindowResizedWithoutTheKeyboardClosesTheAnchoredPopup()
    {
        var picker = Picker();
        var (_, request) = Open(picker);
        SkUiWindowMetrics.SoftKeyboardOverride = static () => false;
        ((IWindow)picker.Window!).FrameChanged(new Rect(0, 0, 400, 500));
        _ui.RunPending();
        Assert.True(request.CloseRequested.IsCancellationRequested);
    }

    [Fact]
    public void AboveKeyboardShortensFlipsOrSitsOnTheKeyboard()
    {
        // A field in the middle of a 400 × 800 window, a popup of 300 below it (344 to 644).
        var layout = SkUiPopupGeometry.Layout(new SkUiPopupPlacementInput(new Rect(10, 300, 200, 40), new Rect(10, 300, 200, 40), new Size(400, 800),
            default, new Thickness(8), 4, SkUiPopupPlacement.Anchored, LayoutOptions.Start, LayoutOptions.Start, new Size(200, 300),
            double.PositiveInfinity, Rect.Zero, false));
        Assert.Equal(new Rect(10, 344, 200, 300), layout.Bounds);

        // No keyboard, or one below the popup: where it opened.
        Assert.Equal(layout.Bounds, SkUiPopupGeometry.AboveKeyboard(layout, 300, double.PositiveInfinity, 100, out var bottomStays));
        Assert.False(bottomStays);
        Assert.Equal(layout.Bounds, SkUiPopupGeometry.AboveKeyboard(layout, 300, 700, 100, out _));

        // Room for the minimum below the field: shorter, its top kept.
        Assert.Equal(new Rect(10, 344, 200, 148), SkUiPopupGeometry.AboveKeyboard(layout, 300, 500, 100, out bottomStays));
        Assert.False(bottomStays);

        // Too little below: above the field (296 down to the margin at 8).
        Assert.Equal(new Rect(10, 8, 200, 288), SkUiPopupGeometry.AboveKeyboard(layout, 300, 400, 100, out bottomStays));
        Assert.True(bottomStays);
        Assert.Equal(new Rect(10, 196, 200, 100), SkUiPopupGeometry.AboveKeyboard(layout, 100, 400, 100, out _));

        // The keyboard over the field itself: on the keyboard, over the field.
        Assert.Equal(new Rect(10, 8, 200, 284), SkUiPopupGeometry.AboveKeyboard(layout, 300, 300, 100, out bottomStays));
        Assert.True(bottomStays);
    }
}
