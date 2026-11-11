using System.Collections.ObjectModel;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCollectionView.SelectedItemTemplate"/>: a selected order's row shows its details and actions, from a template
/// of its own; the row is measured again and the rows after it make room (gliding, with animated item changes). SkiaUi's own
/// API: MAUI's list swaps the template in the view model or with triggers.
/// </summary>
public sealed class CollectionViewSelectedTemplateDemoPage : ComponentDemoPage
{
    private const string Single = "Single";
    private const string Multiple = "Multiple";
    private const string List = "List";
    private const string GridLayout = "Grid, 2";

    private static readonly string[] Customers = ["Ada", "Grace", "Linus", "Margaret", "Alan", "Barbara", "Dennis", "Frances"];

    private readonly SkUiCollectionView _skia;
    private readonly DataTemplate _details = new(() => new OrderDetails());
    private string _mode = Single;
    private string _layout = List;

    public CollectionViewSelectedTemplateDemoPage() : base("SkUiCollectionView (selected item template)", new SkUiCollectionView(),
        widthRange: (160, 400, 300), heightRange: (200, 640, 420))
    {
        _skia = (SkUiCollectionView)SkiaControl;
        var orders = new ObservableCollection<Order>();
        for (var index = 0; index < 40; index++)
            orders.Add(new Order(1000 + index, Customers[index % Customers.Length], 12.5m + index * 3.25m));
        _skia.ItemsSource = orders;
        _skia.ItemTemplate = new DataTemplate(() => new OrderRow());
        _skia.SelectedItemTemplate = _details;
        _skia.SelectionMode = SkUiSelectionMode.Single;
        _skia.AnimatesItemChanges = true;
        _skia.SelectionChanged += (_, _) => Feedback(_skia.SelectedItem is Order order ? $"Order {order.Number} selected"
            : $"{_skia.SelectedItems.Count} selected");

        Choice(nameof(SkUiCollectionView.SelectionMode), [Single, Multiple], Single, value =>
        {
            _mode = value;
            _skia.SelectionMode = value == Single ? SkUiSelectionMode.Single : SkUiSelectionMode.Multiple;
        }, () => _mode);
        Toggle(nameof(SkUiCollectionView.SelectedItemTemplate), true, value => _skia.SelectedItemTemplate = value ? _details : null,
            () => _skia.SelectedItemTemplate is not null);
        Toggle(nameof(SkUiCollectionView.AnimatesItemChanges), true, value => _skia.AnimatesItemChanges = value, () => _skia.AnimatesItemChanges == true);
        Choice(nameof(SkUiCollectionView.ItemsLayout), [List, GridLayout], List, value =>
        {
            _layout = value;
            _skia.ItemsLayout = value == GridLayout
                ? new GridItemsLayout(2, ItemsLayoutOrientation.Vertical) { HorizontalItemSpacing = 4, VerticalItemSpacing = 4 }
                : new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 2 };
        }, () => _layout);
        ActionButton("Select all (multiple)", () => _skia.SelectAll());
        ActionButton("Clear the selection", () => _skia.ClearSelection());
    }

    private sealed record Order(int Number, string Customer, decimal Total);

    /// <summary>An order's row: number and customer.</summary>
    private sealed class OrderRow : SkUiGrid
    {
        private readonly SkUiLabel _title = new() { FontSize = 15, TextColor = DemoColors.Ink, VerticalTextAlignment = TextAlignment.Center, Margin = new Thickness(12, 0) };

        public OrderRow()
        {
            HeightRequest = 48;
            Background = Colors.White;
            Children.Add(_title);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is Order order)
                _title.Text = $"#{order.Number} · {order.Customer}";
        }
    }

    /// <summary>A selected order's row: its details and actions.</summary>
    private sealed class OrderDetails : SkUiVerticalStackLayout
    {
        private readonly SkUiLabel _title = new() { FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = DemoColors.Ink };
        private readonly SkUiLabel _total = new() { FontSize = 14, TextColor = DemoColors.Caption };

        public OrderDetails()
        {
            Padding = new Thickness(12, 10);
            Spacing = 6;
            Background = Colors.White;
            Children.Add(_title);
            Children.Add(_total);
            // The buttons share the width, so they fit a grid column too.
            var actions = new SkUiGrid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 8 };
            actions.Add(new SkUiButton { Text = "Ship" }, 0);
            actions.Add(new SkUiButton { Text = "Refund" }, 1);
            Children.Add(actions);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not Order order)
                return;
            _title.Text = $"#{order.Number} · {order.Customer}";
            _total.Text = $"Total {order.Total:0.00} · 3 items · paid";
        }
    }
}
