using MauiSkiaUi;
using MauiSkiaUi.Core;
using SkiaSharp;

namespace MauiSkiaUiDemo;

/// <summary>
/// Core scroller with nested scrolling and gestures: a horizontal carousel inside the vertical scroller, rows that
/// react to swipe / long press / double tap, and a pinch-to-scale tile. The feedback line reports each gesture.
/// </summary>
public sealed class CoreScrollViewDemoPage : ComponentDemoPage
{
    private readonly SkUiCoreScrollView _scroll;

    public CoreScrollViewDemoPage()
        : base(nameof(SkUiCoreScrollView), CreateHost(out var scroll, out var carousel, out var rows, out var tile), native: null,
            widthRange: (200, 400, 320), heightRange: (200, 560, 420))
    {
        _scroll = scroll;
        void Report(string message) => Feedback($"{message} · offset {_scroll.ScrollY:F0}, carousel {carousel.ScrollX:F0}");
        _scroll.Scrolled += (_, _) => Report("Scrolled");
        carousel.Scrolled += (_, _) => Report("Carousel scrolled");
        foreach (var (row, index) in rows.Select((row, index) => (row, index)))
        {
            row.Swiped += (_, args) => Report($"Row {index + 1} swiped {args.Direction}");
            row.LongPressed += (_, _) => Report($"Row {index + 1} long press");
            row.DoubleTapped += (_, _) => Report($"Row {index + 1} double tap");
        }
        foreach (var button in carousel.Content is SkUiCoreHorizontalStackLayout stack ? stack.Children.OfType<SkUiCoreButton>() : [])
            button.Clicked += (_, _) => Report($"Tapped {button.Text}");
        var scale = 1.0;
        tile.PinchUpdated += (_, args) =>
        {
            if (args.Status == GestureStatus.Running)
            {
                scale = Math.Clamp(scale * args.Scale, 0.5, 3);
                tile.SetScale(scale);
            }
            Report($"Pinch {args.Status} ×{scale:F2}");
        };
        ActionButton("Scroll to top", () => _scroll.ScrollToAsync(0, 0));
        ActionButton("Reset tile", () => { scale = 1; tile.SetScale(1); });
        OnReset(() => { _scroll.ScrollTo(0, 0); carousel.ScrollTo(0, 0); scale = 1; tile.SetScale(1); });
    }

    private static SkUiCoreHost CreateHost(out SkUiCoreScrollView scroll, out SkUiCoreScrollView carousel, out List<SkUiCoreLabel> rows, out SkUiCoreBox tile)
    {
        var cards = new SkUiCoreHorizontalStackLayout().SetSpacing(8).SetPadding(new Thickness(8));
        for (var i = 1; i <= 8; i++)
        {
            var button = new SkUiCoreButton();
            button.SetText($"Card {i}").SetWidth(110).SetHeight(64);
            cards.Add(button);
        }
        carousel = new SkUiCoreScrollView().SetOrientation(ScrollOrientation.Horizontal);
        carousel.SetContent(cards);

        var content = new SkUiCoreVerticalStackLayout().SetSpacing(6).SetPadding(new Thickness(8));
        content.Add(Row("Horizontal carousel (nested scroller)", "#E5E7EB", fixedHeight: 28));
        content.Add(carousel);
        rows = [];
        for (var i = 1; i <= 12; i++)
        {
            var row = Row($"Row {i} — swipe, long press or double tap", i % 2 == 0 ? "#DBEAFE" : "#DCFCE7");
            row.SetSwipeDirections(SwipeDirection.Left | SwipeDirection.Right);
            rows.Add(row);
            content.Add(row);
        }
        content.Add(Row("Pinch the tile below", "#E5E7EB", fixedHeight: 28));
        tile = new SkUiCoreBox();
        tile.SetColor(DemoColors.Accent).SetWidth(160).SetHeight(160).SetHorizontalAlignment(LayoutAlignment.Center);
        content.Add(tile);

        scroll = new SkUiCoreScrollView();
        scroll.SetContent(content);
        return new SkUiCoreHost().SetContent(scroll);
    }

    private static SkUiCoreLabel Row(string text, string color, double fixedHeight = 56)
    {
        var background = Color.FromArgb(color);
        var fill = new SKColor((byte)(background.Red * 255), (byte)(background.Green * 255), (byte)(background.Blue * 255));
        var label = new SkUiCoreLabel();
        label.SetText(text).SetTextColor(DemoColors.Ink).SetFontSize(14).SetPadding(new Thickness(10, 0))
            .SetVerticalTextAlignment(TextAlignment.Center);
        label.SetHeight(fixedHeight);
        label.SetPaintBackground(canvas =>
        {
            using var paint = new SKPaint { Color = fill, IsAntialias = true };
            canvas.DrawRoundRect(0, 0, (float)label.Frame.Width, (float)label.Frame.Height, 8, 8, paint);
        });
        return label;
    }
}
