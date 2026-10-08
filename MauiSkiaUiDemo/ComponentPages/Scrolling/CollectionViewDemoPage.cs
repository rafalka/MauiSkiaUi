using System.Collections.ObjectModel;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCollectionView"/> (B2) next to MAUI's <see cref="CollectionView"/> in a <see cref="RefreshView"/>, on the
/// same items: selection, item taps, a header and footer (sticky on the drawn list only), the empty view, loading more
/// near the end, and pull-to-refresh.
/// </summary>
public sealed class CollectionViewDemoPage : ComponentDemoPage
{
    private readonly SkUiCollectionView _skia;
    private readonly RefreshView _refresh;
    private readonly CollectionView _native;
    private readonly ObservableCollection<Order> _orders = [];
    private readonly ObservableCollection<Order> _none = [];
    private int _next;
    private bool _empty;
    private bool _loadMore = true;
    private string _lastTap = "";

    public CollectionViewDemoPage() : base(nameof(SkUiCollectionView), new SkUiCollectionView(), new RefreshView { Content = new CollectionView() },
        widthRange: (160, 400, 300), heightRange: (160, 600, 360))
    {
        _skia = (SkUiCollectionView)SkiaControl;
        _refresh = (RefreshView)NativeControl!;
        _native = (CollectionView)_refresh.Content;
        Append(60);

        _skia.ItemTemplate = new DataTemplate(() => new SkiaRow());
        // Floating cards narrower than the list, translucent: when sticky, the rows scroll behind them and show around them.
        _skia.Header = Card(new SkUiLabel { Text = "Orders", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Ink }, new Thickness(16, 8, 16, 4));
        _skia.Footer = Card(new SkUiLabel { Text = "Pull down to refresh · scroll to load more", FontSize = 12, TextColor = DemoColors.Caption }, new Thickness(16, 4, 16, 8));
        _skia.IsStickyHeader = true;
        _skia.IsStickyFooter = true;
        _skia.EmptyView = new SkUiLabel { Text = "No orders", FontSize = 16, TextColor = DemoColors.Caption, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        _skia.IsPullToRefreshEnabled = true;
        _skia.RemainingItemsThreshold = 5;
        _skia.RemainingItemsThresholdReached += (_, _) => LoadMore();
        _skia.Refreshing += (_, _) => Refresh(() => _skia.IsRefreshing = false);
        _skia.ItemTapped += (_, args) => { _lastTap = $"tapped {((Order)args.Item!).Number}"; Status(); };
        _skia.SelectionChanged += (_, _) => Status();

        _native.ItemTemplate = new DataTemplate(() => new NativeRow());
        _native.Header = new Label { Text = "Orders", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Ink, Padding = new Thickness(12, 10) };
        _native.Footer = new Label { Text = "Pull down to refresh · scroll to load more", FontSize = 12, TextColor = DemoColors.Caption, Padding = new Thickness(12, 8) };
        _native.EmptyView = new Label { Text = "No orders", FontSize = 16, TextColor = DemoColors.Caption, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        _native.RemainingItemsThreshold = 5;
        _native.RemainingItemsThresholdReached += (_, _) => LoadMore();
        _refresh.Refreshing += (_, _) => Refresh(() => _refresh.IsRefreshing = false);
        ShowItems();

        Choice(nameof(SkUiCollectionView.SelectionMode), Enum.GetValues<SkUiSelectionMode>(), SkUiSelectionMode.None,
            value =>
            {
                _skia.SelectionMode = value;
                _native.SelectionMode = value == SkUiSelectionMode.None ? SelectionMode.None : SelectionMode.Single;
            },
            () => _skia.SelectionMode);
        // SkiaUi only: MAUI's header and footer always scroll.
        Toggle(nameof(SkUiCollectionView.IsStickyHeader), true, value => _skia.IsStickyHeader = value, () => _skia.IsStickyHeader);
        Toggle(nameof(SkUiCollectionView.IsStickyFooter), true, value => _skia.IsStickyFooter = value, () => _skia.IsStickyFooter);
        Toggle("No items (empty view)", false, value => { _empty = value; ShowItems(); }, () => _empty);
        Toggle("Load more near the end", true, value => _loadMore = value, () => _loadMore);
        Toggle(nameof(SkUiCollectionView.IsPullToRefreshEnabled), true,
            value => { _skia.IsPullToRefreshEnabled = value; _refresh.IsRefreshEnabled = value; },
            () => _skia.IsPullToRefreshEnabled, () => _refresh.IsRefreshEnabled);
        Toggle(nameof(SkUiCollectionView.KeepSelectionVisible), false, value => _skia.KeepSelectionVisible = value, () => _skia.KeepSelectionVisible);
        Toggle(nameof(SkUiCollectionView.ShowsItemPressEffect), false, value => _skia.ShowsItemPressEffect = value, () => _skia.ShowsItemPressEffect);
        Number(nameof(SkUiCollectionView.ItemSpacing), 0, 16, 0,
            value => { _skia.ItemSpacing = value; ((LinearItemsLayout)_native.ItemsLayout).ItemSpacing = value; },
            () => _skia.ItemSpacing, () => ((LinearItemsLayout)_native.ItemsLayout).ItemSpacing);
        ActionButton("Refresh", () => { _skia.IsRefreshing = true; _refresh.IsRefreshing = true; });
        ActionButton("Scroll to the selected order", () =>
        {
            if (_skia.SelectedItem is { } selected)
            {
                _ = _skia.ScrollToItem(selected, ScrollToPosition.Center);
                _native.ScrollTo(selected, position: ScrollToPosition.Center);
            }
        });
        OnReset(() =>
        {
            _skia.SelectedItem = null;
            _native.SelectedItem = null;
            _ = _skia.ScrollToAsync(0, animated: false);
        });
    }

    /// <summary>A rounded, translucent card with a shadow, inset from the list's edges by <paramref name="margin"/>.</summary>
    private static SkUiBorder Card(SkUiLabel text, Thickness margin) => new()
    {
        Content = text,
        Margin = margin,
        Padding = new Thickness(14, 8),
        StrokeThickness = 0,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
        Background = Colors.White.WithAlpha(0.85f),
        Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.18f, Radius = 8, Offset = new Point(0, 2) }
    };

    private void ShowItems()
    {
        var items = _empty ? _none : _orders;
        _skia.ItemsSource = items;
        _native.ItemsSource = items;
        Status();
    }

    private void Append(int count)
    {
        for (var index = 0; index < count; index++, _next++)
            _orders.Add(new Order(1000 + _next, _next % 4 == 0 ? 2 : 1, (_next % 3) switch { 0 => Accent, 1 => DemoColors.SampleA, _ => DemoColors.SampleB }));
    }

    private void LoadMore()
    {
        if (!_loadMore || _empty || _orders.Count >= 2000)
            return;
        // As if a page arrived from a service: on the next UI turn.
        Dispatcher.Dispatch(() => { Append(40); Status(); });
    }

    /// <summary>A simulated fetch: a new order on top after a second.</summary>
    private void Refresh(Action done) => Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), () =>
    {
        if (!_empty)
            _orders.Insert(0, new Order(1000 + _next++, 1, DemoColors.SampleB));
        done();
        Status();
    });

    private void Status() =>
        Feedback($"{_skia.ItemCount} orders · selected {(_skia.SelectedItem as Order)?.Number.ToString() ?? "none"}"
            + (_lastTap.Length > 0 ? $" · {_lastTap}" : "") + (_skia.IsRefreshing ? " · refreshing" : ""),
            $"{((IList<Order>)_native.ItemsSource).Count} orders · selected {(_native.SelectedItem as Order)?.Number.ToString() ?? "none"}");

    internal sealed record Order(int Number, int Lines, Color Color);

    /// <summary>A recycled row; the <c>Selected</c> visual state bolds the order number.</summary>
    private sealed class SkiaRow : SkUiHorizontalStackLayout
    {
        private readonly SkUiBox _swatch = new() { WidthRequest = 6 };
        private readonly SkUiLabel _text = new() { FontSize = 14, TextColor = Ink, VerticalTextAlignment = TextAlignment.Center, Margin = new Thickness(12, 0) };

        public SkiaRow()
        {
            Children.Add(_swatch);
            Children.Add(_text);
            VisualStateManager.SetVisualStateGroups(this, [new VisualStateGroup
            {
                Name = "CommonStates",
                States =
                {
                    new VisualState { Name = "Normal" },
                    new VisualState { Name = "PointerOver" },
                    new VisualState { Name = "Selected" }
                }
            }]);
        }

        protected override void ChangeVisualState()
        {
            base.ChangeVisualState();
            _text.FontAttributes = VisualStateManager.GetVisualStateGroups(this)[0].CurrentState?.Name == "Selected" ? FontAttributes.Bold : FontAttributes.None;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not Order order)
                return;
            HeightRequest = 28 + order.Lines * 22;
            _swatch.Color = order.Color;
            _text.Text = order.Lines > 1 ? $"Order {order.Number}\n{order.Lines} items" : $"Order {order.Number}";
        }
    }

    private sealed class NativeRow : ContentView
    {
        private readonly BoxView _swatch = new() { WidthRequest = 6 };
        private readonly Label _text = new() { FontSize = 14, TextColor = Ink, VerticalTextAlignment = TextAlignment.Center, Margin = new Thickness(12, 0) };

        public NativeRow() => Content = new HorizontalStackLayout { Children = { _swatch, _text } };

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not Order order)
                return;
            HeightRequest = 28 + order.Lines * 22;
            _swatch.Color = order.Color;
            _text.Text = order.Lines > 1 ? $"Order {order.Number}\n{order.Lines} items" : $"Order {order.Number}";
        }
    }
}
