using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Pinch, pan, double tap and wheel on <see cref="SkUiZoomView"/>.</summary>
public sealed class ZoomViewDemoPage : ComponentDemoPage
{
    public ZoomViewDemoPage() : base(nameof(SkUiZoomView), new SkUiZoomView { HeightRequest = 280, BackgroundColor = Colors.White }, widthRange: (160, 500, 320))
    {
        var zoom = (SkUiZoomView)SkiaControl;
        var content = new SkUiVerticalStackLayout { Spacing = 6, Padding = new Thickness(16) };
        content.Children.Add(new SkUiLabel { Text = "Pinch, double tap or use the wheel", FontSize = 20, TextColor = Ink });
        content.Children.Add(new SkUiLabel { Text = "The content's scale and offset change at composite time, so nothing is recorded again while it moves.", TextColor = Ink });
        content.Children.Add(new SkUiBox { Color = Accent, HeightRequest = 60, CornerRadius = 12 });
        zoom.Content = content;
        zoom.ZoomChanged += (_, _) => Feedback($"Scale: {zoom.ZoomScale:0.00}");

        Number(nameof(SkUiZoomView.MaximumZoomScale), 1, 10, 5, value => zoom.MaximumZoomScale = value, () => zoom.MaximumZoomScale);
        Number(nameof(SkUiZoomView.DoubleTapZoomScale), 1, 10, 2.5, value => zoom.DoubleTapZoomScale = value, () => zoom.DoubleTapZoomScale);
        ActionButton("Reset", zoom.ResetZoom);
    }
}
