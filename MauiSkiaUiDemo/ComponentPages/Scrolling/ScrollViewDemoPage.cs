using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiScrollView"/>.</summary>
public sealed class ScrollViewDemoPage : ComponentDemoPage
{
    private readonly SkUiScrollView _skia;
    private readonly ScrollView _native;

    // Explicit sizes: a scroller's natural size is its whole content, which would make the preview as tall as the 640 DIP
    // rows (and centered in twice that), so nothing would scroll. 140 DIPs fit the default panel, also stacked on a phone.
    public ScrollViewDemoPage() : base(nameof(SkUiScrollView), new SkUiScrollView(), new ScrollView(),
        widthRange: (160, 400, 300), heightRange: (100, 480, 140))
    {
        _skia = (SkUiScrollView)SkiaControl;
        _native = (ScrollView)NativeControl!;
        var drawn = new SkUiGrid { WidthRequest = 400, HeightRequest = 640, RowSpacing = 8 };
        var standard = new Grid { WidthRequest = 400, HeightRequest = 640, RowSpacing = 8 };
        var skiaClicks = 0;
        var nativeClicks = 0;
        var skiaRows = new List<SkUiButton>();
        var nativeRows = new List<Button>();
        void Status() => Feedback($"Offset {_skia.ScrollX:F0}, {_skia.ScrollY:F0}; taps {skiaClicks}", $"Offset {_native.ScrollX:F0}, {_native.ScrollY:F0}; taps {nativeClicks}");
        for (var index = 0; index < 10; index++)
        {
            drawn.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            standard.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            var button = new SkUiButton { Text = $"Row {index + 1}", FillColor = Accent, FontSize = 14 };
            var counterpart = new Button { Text = button.Text, Background = Accent, TextColor = Colors.White, FontSize = 14 };
            button.Clicked += (_, _) => { skiaClicks++; Status(); };
            counterpart.Clicked += (_, _) => { nativeClicks++; Status(); };
            Grid.SetRow(button, index);
            drawn.Children.Add(button);
            skiaRows.Add(button);
            nativeRows.Add(counterpart);
            standard.Add(counterpart, 0, index);
        }
        _skia.Content = drawn;
        _native.Content = standard;
        _skia.Scrolled += (_, _) => Status();
        _native.Scrolled += (_, _) => Status();
        Choice(nameof(SkUiScrollView.Orientation), Enum.GetValues<ScrollOrientation>(), ScrollOrientation.Both, value => { _skia.Orientation = value; _native.Orientation = value; }, () => _skia.Orientation, () => _native.Orientation);
        // Content size is distinct from the host WidthRequest/HeightRequest editors.
        Number("ContentHeight", 200, 1200, 640, value => { drawn.HeightRequest = value; standard.HeightRequest = value; }, () => drawn.HeightRequest, () => standard.HeightRequest);
        Number("ContentWidth", 200, 800, 400, value => { drawn.WidthRequest = value; standard.WidthRequest = value; }, () => drawn.WidthRequest, () => standard.WidthRequest);
        Number(nameof(SkUiScrollView.Padding), 0, 24, 0, value => { _skia.Padding = value; _native.Padding = value; }, () => _skia.Padding.Left, () => _native.Padding.Left);
        var bars = Enum.GetValues<ScrollBarVisibility>();
        Choice(nameof(SkUiScrollView.VerticalScrollBarVisibility), bars, ScrollBarVisibility.Default,
            value => { _skia.VerticalScrollBarVisibility = value; _native.VerticalScrollBarVisibility = value; },
            () => _skia.VerticalScrollBarVisibility, () => _native.VerticalScrollBarVisibility);
        Choice(nameof(SkUiScrollView.HorizontalScrollBarVisibility), bars, ScrollBarVisibility.Default,
            value => { _skia.HorizontalScrollBarVisibility = value; _native.HorizontalScrollBarVisibility = value; },
            () => _skia.HorizontalScrollBarVisibility, () => _native.HorizontalScrollBarVisibility);
        // SkiaUi only: MAUI's ScrollView always uses the platform's.
        Choice(nameof(SkUiScrollView.Overscroll), Enum.GetValues<SkUiOverscrollMode>(), SkUiOverscrollMode.Default,
            value => _skia.Overscroll = value, () => _skia.Overscroll);
        // SkiaUi only: snap the rows to the viewport (MAUI has snap points on CollectionView only).
        Choice(nameof(SkUiScrollView.SnapPointsType), Enum.GetValues<SnapPointsType>(), SnapPointsType.None,
            value => _skia.SnapPointsType = value, () => _skia.SnapPointsType);
        Choice(nameof(SkUiScrollView.SnapPointsAlignment), Enum.GetValues<SnapPointsAlignment>(), SnapPointsAlignment.Start,
            value => _skia.SnapPointsAlignment = value, () => _skia.SnapPointsAlignment);
        Choice("Thumb color", ["Default", "Accent"], "Default",
            value => { _skia.VerticalScrollBar.ThumbColor = _skia.HorizontalScrollBar.ThumbColor = value == "Accent" ? Accent : null; },
            () => _skia.VerticalScrollBar.ThumbColor is null ? "Default" : "Accent");
        var position = ScrollToPosition.MakeVisible;
        Choice(nameof(ScrollToPosition), Enum.GetValues<ScrollToPosition>(), ScrollToPosition.MakeVisible, value => position = value, () => position);
        ActionButton("Scroll to row 8", () => ScrollBothTo(skiaRows[7], nativeRows[7], position));
        ActionButton("Scroll to middle", () => ScrollBoth(100, 250));
        ActionButton("Scroll to start", () => ScrollBoth(0, 0));
        OnReset(() => { skiaClicks = nativeClicks = 0; ScrollBoth(0, 0); });
    }

    private async void ScrollBothTo(SkUiButton skiaRow, Button nativeRow, ScrollToPosition position)
    {
        try
        {
            var drawn = _skia.ScrollToAsync(skiaRow, position, animated: true);
            if (_native.Handler is not null)
                await _native.ScrollToAsync(nativeRow, position, true);
            await drawn;
        }
        catch (OperationCanceledException)
        {
            // A drag or another scroll took over.
        }
    }

    private async void ScrollBoth(double horizontal, double vertical)
    {
        _skia.ScrollTo(horizontal, vertical);
        if (_native.Handler is not null) await _native.ScrollToAsync(horizontal, vertical, false);
    }

    protected override void OnDisappearing()
    {
        _skia.ScrollTo(_skia.ScrollX, _skia.ScrollY);
        base.OnDisappearing();
    }
}
