using System.Collections.ObjectModel;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiVirtualVerticalStackLayout"/> as one section of a scrolling page: a header and a horizontal carousel above a feed
/// whose items are created only near the viewport. The feed loads page after page (<see cref="SkUiVirtualVerticalStackLayoutBase.RemainingItemsThreshold"/>),
/// or comes from an endless <see cref="SkUiVirtualVerticalStackLayout.ItemFactory"/>; inserting at the top keeps what shows in place.
/// </summary>
public sealed class VirtualVerticalStackLayoutDemoPage : ComponentDemoPage
{
    private const int PageSize = 40;
    private readonly SkUiVirtualVerticalStackLayout _feed;
    private readonly ObservableCollection<FeedItem> _items = [];
    private int _created;
    private int _pages;
    private int _inserted;

    public VirtualVerticalStackLayoutDemoPage() : base(nameof(SkUiVirtualVerticalStackLayout), new SkUiScrollView(), widthRange: (160, 400, 300), heightRange: (160, 600, 360))
    {
        var page = (SkUiScrollView)SkiaControl;
        _feed = new SkUiVirtualVerticalStackLayout { ItemSpacing = 6, Padding = new Thickness(0, 6) };
        _feed.ItemTemplate = new DataTemplate(() => { _created++; return new FeedCard(); });
        _feed.RemainingItemsThresholdReached += (_, _) => LoadPage();
        _feed.VisibleRangeChanged += (_, _) => Status();
        var carousel = new SkUiHorizontalStackLayout { Spacing = 8, Padding = new Thickness(8) };
        for (var index = 0; index < 12; index++)
            carousel.Children.Add(new SkUiLabel
            {
                Text = $"Story {index + 1}", TextColor = Colors.White, FontSize = 13, WidthRequest = 84, HeightRequest = 84,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, CornerRadius = 42,
                Background = index % 2 == 0 ? Accent : DemoColors.SampleB
            });
        page.Content = new SkUiVerticalStackLayout
        {
            Children =
            {
                new SkUiLabel { Text = "Feed", FontSize = 22, TextColor = Ink, Padding = new Thickness(12, 12, 12, 0) },
                new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = carousel },
                _feed
            }
        };

        Choice("Source", ["Paged collection", "Endless factory"], "Paged collection", UseSource,
            () => _feed.ItemFactory is null ? "Paged collection" : "Endless factory");
        Number(nameof(SkUiVirtualVerticalStackLayout.RemainingItemsThreshold), 0, 20, 5, value => _feed.RemainingItemsThreshold = (int)value,
            () => _feed.RemainingItemsThreshold, whole: true);
        Number(nameof(SkUiVirtualVerticalStackLayout.EstimatedItemSize), 0, 200, 0, value => _feed.EstimatedItemSize = value, () => _feed.EstimatedItemSize);
        Number(nameof(SkUiVirtualVerticalStackLayout.PrefetchFactor), 0, 3, 1, value => _feed.PrefetchFactor = value, () => _feed.PrefetchFactor);
        // Automatic by default; the fixed values are there to compare (the status line shows the budget in use).
        Choice(nameof(SkUiVirtualVerticalStackLayout.PrefetchBudget), ["Automatic", "1 ms", "4 ms", "16 ms"], "Automatic",
            value => _feed.PrefetchBudget = value == "Automatic" ? null : TimeSpan.FromMilliseconds(int.Parse(value.Split(' ')[0])),
            () => _feed.PrefetchBudget is { } budget ? $"{budget.TotalMilliseconds:0} ms" : "Automatic");
        ActionButton("Insert at top", () =>
        {
            if (_feed.ItemFactory is null)
                _items.Insert(0, new FeedItem(--_inserted, "New post: what shows stays in place unless the top shows", 2));
        });
        ActionButton("Scroll to item 200", async () =>
        {
            while (_feed.ItemFactory is null && _items.Count <= 200)
                LoadPage();
            // An endless factory knows only the items scrolled near so far.
            if (_feed.ItemCount <= 200)
            {
                Feedback($"Item 200 is not known yet ({_feed.ItemCount} items): scroll down first.");
                return;
            }
            try
            {
                await _feed.ScrollToIndex(200, ScrollToPosition.Start, animated: true);
            }
            catch (OperationCanceledException)
            {
                // A drag or another scroll took over.
            }
        });
        OnReset(() => { UseSource("Paged collection"); page.ScrollTo(0, 0); });
    }

    private void UseSource(string source)
    {
        _items.Clear();
        _pages = 0;
        if (source == "Endless factory")
        {
            _feed.ItemsSource = null;
            _feed.SetItemFactory(index => new FeedCard { BindingContext = Item(index) });
            return;
        }
        _feed.ItemFactory = null;
        LoadPage();
        _feed.ItemsSource = _items;
    }

    private void LoadPage()
    {
        if (_feed.ItemFactory is not null)
            return;
        for (var index = 0; index < PageSize; index++)
            _items.Add(Item(_pages * PageSize + index));
        _pages++;
        Status();
    }

    private static FeedItem Item(int index) => new(index, (index % 5) switch
    {
        0 => "A short post.",
        1 => "A post with a little more text, which wraps onto a second line on a phone.",
        2 => "A long post: items have different heights, measured as they come into view; the ones further on are estimated from the average until then.",
        3 => "Photo post",
        _ => "Another post."
    }, index % 3);

    private void Status()
    {
        var (first, last) = _feed.RealizedRange;
        Feedback($"{_feed.ItemCount} items · visible {_feed.FirstVisibleIndex}–{_feed.LastVisibleIndex} · realized {first}–{last} · {_created} cards created · "
            + $"{_feed.ItemCost:0.0} ms each · budget {_feed.LastPrefetchBudget.TotalMilliseconds:0.0} ms of {_feed.AnimationClock.FrameInterval.TotalMilliseconds:0.0} ms frames");
    }

    internal sealed record FeedItem(int Index, string Text, int Kind);

    /// <summary>A recycled card: it takes its item from the binding context.</summary>
    private sealed class FeedCard : SkUiBorder
    {
        private readonly SkUiLabel _title = new() { FontSize = 13, TextColor = DemoColors.Caption };
        private readonly SkUiLabel _text = new() { FontSize = 15, TextColor = Ink, LineBreakMode = LineBreakMode.WordWrap };
        private readonly SkUiBox _photo = new() { HeightRequest = 120, CornerRadius = 8 };

        public FeedCard()
        {
            Margin = new Thickness(12, 0);
            Padding = new Thickness(12, 10);
            Stroke = DemoColors.Border;
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 };
            Background = Colors.White;
            Content = new SkUiVerticalStackLayout { Spacing = 6, Children = { _title, _text, _photo } };
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not FeedItem item)
                return;
            _title.Text = item.Index < 0 ? "Just now" : $"Post {item.Index + 1}";
            _text.Text = item.Text;
            _photo.IsVisible = item.Kind == 0;
            _photo.Color = item.Index % 2 == 0 ? DemoColors.SoftSurface : DemoColors.StressAlt;
        }
    }
}
