using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Picker popups and the room their presenter reports (<see cref="SkUiPopupRequest.SetAvailableArea"/>: the window less the soft
/// keyboard): an anchored searchable list stays inside it (shorter below its field, flipped above it, or on the room's bottom), a
/// bottom pane sits on its bottom, a window popup fits its height; each returns to its bounds when the room is the whole window
/// again; a window resize closes an anchored popup (MAUI's window does not resize for the keyboard).
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
        SkUiPopups.Presenter = _previousPresenter;
        SkUiLook.Current = _previousLook;
        foreach (var surface in _surfaces)
            surface.Dispose();
        _ui.Dispose();
    }

    private static double Margin => SkUiLook.Current.PopupMargin.Bottom;

    private static SkUiPicker Picker(LayoutOptions? vertical = null, SkUiPopupPlacement placement = SkUiPopupPlacement.Anchored)
    {
        var picker = new SkUiPicker { Title = "City", IsSearchEnabled = true, PopupPlacement = placement, VerticalOptions = vertical ?? LayoutOptions.Start };
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

    /// <summary>The window above a keyboard whose top is at <paramref name="keyboardTop"/>.</summary>
    private static Rect Above(double keyboardTop) => new(0, 0, 400, keyboardTop);

    [Fact]
    public void BelowItsFieldThePopupGetsShorterAboveTheKeyboardAndGrowsBackWhenItHides()
    {
        var (content, request) = Open(Picker());
        var opened = request.Bounds;
        Assert.False(request.OpensAbove);

        Assert.Equal(new Rect(0, 0, 400, 800), request.AvailableArea);
        var keyboardTop = opened.Bottom - 100;
        request.SetAvailableArea(Above(keyboardTop));
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

        request.SetAvailableArea(Rect.Zero);
        Assert.Equal(new Rect(0, 0, 400, 800), request.AvailableArea);
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
        request.SetAvailableArea(Above(keyboardTop));
        var bounds = request.Bounds;
        Assert.Equal(keyboardTop - Margin, bounds.Bottom, Tolerance);
        Assert.True(bounds.Top >= SkUiLook.Current.PopupMargin.Top - Tolerance);
        Assert.True(bounds.Height > 0);
        // Resizing against its bottom edge: the search field is under the rows, next to the keyboard.
        Assert.True(SearchUnderRows(content));

        request.SetAvailableArea(Rect.Zero);
        Assert.Equal(opened, request.Bounds);
        Assert.False(SearchUnderRows(content));
    }

    [Fact]
    public void APopupAboveItsFieldTheKeyboardCoversMovesUpOntoIt()
    {
        var (content, request) = Open(Picker(LayoutOptions.End));
        var opened = request.Bounds;
        Assert.True(request.OpensAbove);
        var keyboardTop = opened.Bottom - 50;
        request.SetAvailableArea(Above(keyboardTop));
        var bounds = request.Bounds;
        Assert.Equal(keyboardTop - Margin, bounds.Bottom, Tolerance);
        Assert.Equal(opened.Height, bounds.Height, Tolerance);
        Assert.True(SearchUnderRows(content));

        request.SetAvailableArea(Rect.Zero);
        Assert.Equal(opened, request.Bounds);
        Assert.True(SearchUnderRows(content));
    }

    [Fact]
    public void AKeyboardBelowThePopupMovesNothing()
    {
        var (content, request) = Open(Picker());
        var changes = 0;
        request.BoundsChanged += (_, _) => changes++;
        request.SetAvailableArea(Above(request.Bounds.Bottom + 100));
        request.SetAvailableArea(Rect.Zero);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void WithoutAFollowingPresenterTheKeyboardMovesNothing()
    {
        Picker().IsOpen = true;
        var request = _presenter.Current!;
        var content = Assert.IsType<SkUiPickerPopup>(request.Content);
        var opened = request.Bounds;
        request.SetAvailableArea(Above(opened.Top + 50));
        Assert.Equal(opened, request.Bounds);
        Assert.Equal(opened.Height, content.HeightRequest, Tolerance);
    }

    [Fact]
    public void AWindowResizedClosesTheAnchoredPopup()
    {
        var picker = Picker();
        var (_, request) = Open(picker);
        ((IWindow)picker.Window!).FrameChanged(new Rect(0, 0, 400, 500));
        _ui.RunPending();
        Assert.True(request.CloseRequested.IsCancellationRequested);
    }

    [Fact]
    public void ABottomPaneSitsOnTheKeyboard()
    {
        var (content, request) = Open(Picker(placement: SkUiPopupPlacement.BottomPane));
        var opened = request.Bounds;
        Assert.Equal(800, opened.Bottom, Tolerance);
        request.SetAvailableArea(Above(500));
        var bounds = request.Bounds;
        Assert.Equal(500, bounds.Bottom, Tolerance);
        Assert.Equal(Math.Min(opened.Height, 500 - SkUiLook.Current.PopupMargin.Top), bounds.Height, Tolerance);
        Assert.Equal(opened.Width, bounds.Width, Tolerance);
        Assert.Equal(bounds.Height, content.HeightRequest, Tolerance);

        request.SetAvailableArea(Rect.Zero);
        Assert.Equal(opened, request.Bounds);
    }

    [Fact]
    public void AWindowPopupFitsTheRoomTheKeyboardLeaves()
    {
        var (_, request) = Open(Picker(placement: SkUiPopupPlacement.Window));
        var opened = request.Bounds;
        var margin = SkUiLook.Current.PopupMargin;
        var keyboardTop = opened.Top + opened.Height / 2;
        request.SetAvailableArea(Above(keyboardTop));
        var bounds = request.Bounds;
        Assert.True(bounds.Top >= margin.Top - Tolerance);
        Assert.True(bounds.Bottom <= keyboardTop - margin.Bottom + Tolerance);
        Assert.Equal(opened.Width, bounds.Width, Tolerance);

        request.SetAvailableArea(Rect.Zero);
        Assert.Equal(opened, request.Bounds);
    }

    [Fact]
    public void FittedShortensFlipsOrSitsOnTheRoomsBottom()
    {
        // A field in the middle of a 400 × 800 window, a popup of 300 below it (344 to 644).
        var layout = SkUiPopupGeometry.Layout(new SkUiPopupPlacementInput(new Rect(10, 300, 200, 40), new Rect(10, 300, 200, 40), new Size(400, 800),
            default, new Thickness(8), 4, SkUiPopupPlacement.Anchored, LayoutOptions.Start, LayoutOptions.Start, new Size(200, 300),
            double.PositiveInfinity, Rect.Zero, false));
        Assert.Equal(new Rect(10, 344, 200, 300), layout.Bounds);

        // The whole window, or a room that holds it: where it opened.
        Assert.Equal(layout.Bounds, SkUiPopupGeometry.Fitted(layout, 300, Rect.Zero, 100, out var bottomStays));
        Assert.False(bottomStays);
        Assert.Equal(layout.Bounds, SkUiPopupGeometry.Fitted(layout, 300, new Rect(0, 0, 400, 700), 100, out _));

        // Room for the minimum below the field: shorter, its top kept.
        Assert.Equal(new Rect(10, 344, 200, 148), SkUiPopupGeometry.Fitted(layout, 300, new Rect(0, 0, 400, 500), 100, out bottomStays));
        Assert.False(bottomStays);

        // Too little below: above the field (296 down to the margin at 8).
        Assert.Equal(new Rect(10, 8, 200, 288), SkUiPopupGeometry.Fitted(layout, 300, new Rect(0, 0, 400, 400), 100, out bottomStays));
        Assert.True(bottomStays);
        Assert.Equal(new Rect(10, 196, 200, 100), SkUiPopupGeometry.Fitted(layout, 100, new Rect(0, 0, 400, 400), 100, out _));

        // The room's bottom over the field itself: on the room's bottom, over the field.
        Assert.Equal(new Rect(10, 8, 200, 284), SkUiPopupGeometry.Fitted(layout, 300, new Rect(0, 0, 400, 300), 100, out bottomStays));
        Assert.True(bottomStays);
    }

    [Fact]
    public void ABottomPaneKeepsOnlyTheSafeAreaTheKeyboardLeaves()
    {
        var safe = new Thickness(0, 50, 0, 34);
        var window = new Size(400, 800);
        Assert.Equal(new Thickness(0, 0, 0, 34), SkUiPopupGeometry.PaneContentPadding(safe, window, Rect.Zero));
        Assert.Equal(new Thickness(0, 0, 0, 14), SkUiPopupGeometry.PaneContentPadding(safe, window, new Rect(0, 0, 400, 780)));
        Assert.Equal(new Thickness(0, 0, 0, 0), SkUiPopupGeometry.PaneContentPadding(safe, window, new Rect(0, 0, 400, 500)));
    }
}
