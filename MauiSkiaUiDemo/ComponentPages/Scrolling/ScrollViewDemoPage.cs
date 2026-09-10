using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiScrollView"/>.</summary>
public sealed class ScrollViewDemoPage : ComponentDemoPage
{
    private readonly SkUiScrollView _skia;
    private readonly ScrollView _native;

    public ScrollViewDemoPage() : base(nameof(SkUiScrollView), new SkUiScrollView(), new ScrollView())
    {
        _skia = (SkUiScrollView)SkiaControl;
        _native = (ScrollView)NativeControl!;
        var drawn = new SkUiGrid { WidthRequest = 400, HeightRequest = 640, RowSpacing = 8 };
        var standard = new Grid { WidthRequest = 400, HeightRequest = 640, RowSpacing = 8 };
        var skiaClicks = 0;
        var nativeClicks = 0;
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
            standard.Add(counterpart, 0, index);
        }
        _skia.Content = drawn;
        _native.Content = standard;
        _skia.Scrolled += (_, _) => Status();
        _native.Scrolled += (_, _) => Status();
        Choice(nameof(SkUiScrollView.Orientation), Enum.GetValues<ScrollOrientation>(), ScrollOrientation.Vertical, value => { _skia.Orientation = value; _native.Orientation = value; }, () => _skia.Orientation, () => _native.Orientation);
        // Content size is distinct from the host WidthRequest/HeightRequest editors.
        Number("ContentHeight", 200, 1200, 640, value => { drawn.HeightRequest = value; standard.HeightRequest = value; }, () => drawn.HeightRequest, () => standard.HeightRequest);
        Number("ContentWidth", 200, 800, 400, value => { drawn.WidthRequest = value; standard.WidthRequest = value; }, () => drawn.WidthRequest, () => standard.WidthRequest);
        Number(nameof(SkUiScrollView.Padding), 0, 24, 0, value => { _skia.Padding = value; _native.Padding = value; }, () => _skia.Padding.Left, () => _native.Padding.Left);
        ActionButton("Scroll to middle", () => ScrollBoth(100, 250));
        ActionButton("Scroll to start", () => ScrollBoth(0, 0));
        OnReset(() => { skiaClicks = nativeClicks = 0; ScrollBoth(0, 0); });
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
