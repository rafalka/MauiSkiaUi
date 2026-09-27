using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// Drawn surfaces inside a native MAUI <see cref="ScrollView"/>, to check how drawn gestures coordinate with a native
/// ancestor (Android RequestDisallowInterceptTouchEvent, iOS gate recognizer):
/// <list type="bullet">
/// <item>Same axis: a vertical drawn list scrolls first; at its top / bottom the native page takes over.</item>
/// <item>Orthogonal: horizontal drags scroll the drawn carousel, vertical drags on it scroll the page.</item>
/// <item>No drawn scroller: taps and swipes work, vertical drags scroll the page.</item>
/// <item>A Core scroll view behaves like the drawn list.</item>
/// </list>
/// The switch rebuilds every surface in software (SKCanvasView path) to check the second touch path.
/// </summary>
public sealed class NativeNestingPage : ContentPage
{
    private readonly ScrollView _page = new();
    private readonly Label _status = new() { FontSize = 13, TextColor = DemoColors.Ink, Padding = new Thickness(12, 6), BackgroundColor = Color.FromArgb("#E0F2F1") };
    private SkUiScrollView? _list;
    private SkUiScrollView? _carousel;
    private SkUiCoreScrollView? _core;
    private string _lastGesture = "–";

    public NativeNestingPage()
    {
        Title = "Native nesting";
        BackgroundColor = DemoColors.PageBackground;
        var software = new Switch { IsToggled = false, AutomationId = "SoftwareSurfaces" };
        software.Toggled += (_, args) => Build(hwAccelerated: !args.Value);
        var header = new HorizontalStackLayout
        {
            Padding = new Thickness(12, 6), Spacing = 8,
            Children = { new Label { Text = "Software surfaces", VerticalOptions = LayoutOptions.Center, TextColor = DemoColors.Ink }, software }
        };
        _page.Scrolled += (_, _) => Status();
        Content = new Grid
        {
            RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star)],
            Children = { header, WithRow(_status, 1), WithRow(_page, 2) }
        };
        Build(hwAccelerated: true);
    }

    private void Build(bool hwAccelerated)
    {
        var stack = new VerticalStackLayout { Padding = new Thickness(12), Spacing = 12 };
        stack.Children.Add(Native("This page is a native MAUI ScrollView. Each teal-framed area below is a separate drawn surface."));

        stack.Children.Add(Caption("1 · Drawn vertical list (same axis): scrolls first, the page takes over at its ends"));
        _list = new SkUiScrollView { Content = Rows("List row", 15, "#DBEAFE", "#DCFCE7") };
        _list.Scrolled += (_, _) => Status();
        stack.Children.Add(Surface(_list, 220, hwAccelerated));

        stack.Children.Add(Native("Native filler — drag here to scroll the page."));
        stack.Children.Add(Caption("2 · Drawn carousel (orthogonal): horizontal drags scroll it, vertical drags scroll the page"));
        var cards = new SkUiHorizontalStackLayout { Spacing = 8, Padding = new Thickness(8) };
        for (var i = 1; i <= 10; i++)
        {
            var card = new SkUiButton { Text = $"Card {i}", WidthRequest = 120, FillColor = DemoColors.Accent, FontSize = 14 };
            card.Clicked += (_, _) => Report($"Tapped {card.Text}");
            cards.Children.Add(card);
        }
        _carousel = new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, Content = cards };
        _carousel.Scrolled += (_, _) => Status();
        stack.Children.Add(Surface(_carousel, 80, hwAccelerated));

        stack.Children.Add(Caption("3 · Drawn content without a scroller: tap / swipe it; vertical drags scroll the page"));
        var swipeRow = new SkUiLabel
        {
            Text = "Swipe me left / right, or tap", FontSize = 15, TextColor = DemoColors.Ink, Padding = new Thickness(12),
            Background = Color.FromArgb("#FEF3C7"), VerticalTextAlignment = TextAlignment.Center,
            SwipeDirections = SwipeDirection.Left | SwipeDirection.Right
        };
        swipeRow.Swiped += (_, args) => Report($"Swiped {args.Direction}");
        swipeRow.Tapped += (_, _) => Report("Tapped the swipe row");
        stack.Children.Add(Surface(swipeRow, 70, hwAccelerated));

        stack.Children.Add(Native("More native filler."));
        stack.Children.Add(Caption("4 · Core scroll view (same axis) inside a SkUiCoreHost"));
        var coreRows = new SkUiCoreVerticalStackLayout().SetSpacing(4).SetPadding(new Thickness(8));
        for (var i = 1; i <= 12; i++)
        {
            var row = new SkUiCoreButton();
            row.SetText($"Core row {i}").SetHeight(40);
            var index = i;
            row.Clicked += (_, _) => Report($"Tapped Core row {index}");
            coreRows.Add(row);
        }
        _core = new SkUiCoreScrollView();
        _core.SetContent(coreRows);
        _core.Scrolled += (_, _) => Status();
        stack.Children.Add(Surface(new SkUiCoreHost().SetContent(_core), 200, hwAccelerated));

        for (var i = 1; i <= 6; i++)
            stack.Children.Add(Native($"Native filler {i} — the page continues so it can scroll past the drawn areas."));
        _page.Content = stack;
        _lastGesture = hwAccelerated ? "GPU surfaces" : "software surfaces";
        Status();
    }

    private void Report(string gesture)
    {
        _lastGesture = gesture;
        Status();
    }

    private void Status()
    {
        static string Of(double value, double max) => $"{value:F0}/{Math.Max(0, max):F0}";
        var list = _list is null ? "–" : Of(_list.ScrollY, _list.ContentSize.Height - _list.Height);
        var carousel = _carousel is null ? "–" : Of(_carousel.ScrollX, _carousel.ContentSize.Width - _carousel.Width);
        var core = _core is null ? "–" : Of(_core.ScrollY, _core.ContentSize.Height - _core.ViewportSize.Height);
        _status.Text = $"page {_page.ScrollY:F0} · list {list} · carousel {carousel} · core {core} · {_lastGesture}";
    }

    /// <summary>A standalone drawn surface (its own platform view) framed in teal.</summary>
    private static Border Surface(ISkUiView content, double height, bool hwAccelerated)
    {
        var surface = new SkUiContentView { HeightRequest = height, Background = Colors.White };
        surface.HwAccelerated = hwAccelerated;
        surface.Content = content;
        return new Border { Stroke = DemoColors.Accent, StrokeThickness = 2, Content = surface };
    }

    private static SkUiVerticalStackLayout Rows(string prefix, int count, string even, string odd)
    {
        var rows = new SkUiVerticalStackLayout { Spacing = 4, Padding = new Thickness(6) };
        for (var i = 1; i <= count; i++)
            rows.Children.Add(new SkUiLabel
            {
                Text = $"{prefix} {i} of {count}", FontSize = 14, TextColor = DemoColors.Ink, Padding = new Thickness(10, 8),
                Background = Color.FromArgb(i % 2 == 0 ? even : odd)
            });
        return rows;
    }

    private static Label Caption(string text) => new() { Text = text, FontAttributes = FontAttributes.Bold, FontSize = 13, TextColor = DemoColors.Ink };

    private static Border Native(string text) => new()
    {
        Stroke = Color.FromArgb("#CBD5E1"), Padding = new Thickness(12), BackgroundColor = Colors.White,
        Content = new Label { Text = text, TextColor = DemoColors.Ink, FontSize = 14 },
        HeightRequest = 90
    };

    private static View WithRow(View view, int row)
    {
        Grid.SetRow(view, row);
        return view;
    }
}
