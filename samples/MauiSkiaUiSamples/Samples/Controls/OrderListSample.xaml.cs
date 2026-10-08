using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>
/// HOWTO: <see cref="SkUiCollectionView"/> in XAML. The page's XAML (OrderListSample.xaml) shows:
/// <list type="number">
/// <item>Items: <c>ItemsSource</c> bound to an <see cref="ObservableCollection{T}"/> (changes are applied item by item),
/// the item template as the element's content; rows are created near the viewport and recycled.</item>
/// <item>Selection: <c>SelectionMode="SingleDeselect"</c> with a two-way <c>SelectedItem</c>; the row's root has a
/// <c>Selected</c> visual state, and <c>SelectionBackground="Transparent"</c> turns the default tint off.</item>
/// <item>Taps: <c>ItemTappedCommand</c> gets the tapped item; the Pay button in each row keeps its own taps.</item>
/// <item>A sticky header, an empty view, loading more near the end (<c>RemainingItemsThreshold</c>) and pull-to-refresh
/// (<c>IsRefreshing</c> two-way, <c>RefreshCommand</c>).</item>
/// </list>
/// </summary>
public sealed partial class OrderListSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Order list (collection view)",
        Summary: "SkUiCollectionView in XAML: a virtualized list with a sticky header, selection styled by a visual state, " +
                 "a button inside each row, loading more near the end and pull-to-refresh.",
        HowTo:
        [
            "Bind `ItemsSource` and put a `DataTemplate` of drawn views inside `<sk:SkUiCollectionView>`.",
            "Select with `SelectionMode=\"Single\"` (or `SingleDeselect`) and a two-way `SelectedItem`; style the row with a " +
                "`Selected` state in `CommonStates`, or keep the default tint (`SelectionBackground`).",
            "React to taps with `ItemTappedCommand` (its parameter is the tapped item).",
            "Add `<sk:SkUiCollectionView.Header>`, `.Footer` and `.EmptyView`; `IsStickyHeader=\"True\"` keeps the header at " +
                "the top.",
            "Load pages with `RemainingItemsThreshold` and its command; refresh with `IsPullToRefreshEnabled`, " +
                "`IsRefreshing` and `RefreshCommand`."
        ],
        ThingsToKnow:
        [
            "Give the list a bounded height (a grid row, a page). In a vertical stack or a scroll view it would be as tall as " +
                "all its items and create every one.",
            "Views inside a row that take taps (buttons) keep them: the row is not tapped or selected.",
            "Setting `IsRefreshing` to true, by a pull or from code, runs `RefreshCommand`; set it back to false when done.",
            "Not in this version: grouping, multiple selection, grid and horizontal layouts.",
            "Templates must create drawn views; put native ones inside `SkUiMauiContentView`."
        ]);

    public OrderListSample() : base(Info)
    {
        InitializeComponent();
        BindingContext = new OrderListModel();
    }
}

/// <summary>An order row's data, with the command of its own button.</summary>
public sealed class OrderItem(int number, OrderListModel owner)
{
    public string Title { get; } = $"Order {number}";

    public string Detail { get; } = $"{number % 5 + 1} items · {(number % 7 + 1) * 12.5:F2} €";

    public ICommand PayCommand { get; } = new Command(() => owner.Status = $"Paying order {number}");
}

public sealed class OrderListModel : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs SelectedChanged = new(nameof(Selected));
    private static readonly PropertyChangedEventArgs IsRefreshingChanged = new(nameof(IsRefreshing));
    private static readonly PropertyChangedEventArgs StatusChanged = new(nameof(Status));

    private int _next = 1000;
    private OrderItem? _selected;
    private bool _isRefreshing;
    private string _status = "Tap an order, pull down to refresh.";

    public OrderListModel()
    {
        Append(30);
        OpenCommand = new Command<OrderItem>(order => Status = $"Opened {order.Title}");
        LoadMoreCommand = new Command(() => { if (Orders.Count < 300) Append(20); });
        RefreshCommand = new Command(async () =>
        {
            await Task.Delay(1000); // a fetch
            Orders.Insert(0, new OrderItem(_next++, this));
            IsRefreshing = false;
            Status = "Refreshed: a new order on top.";
        });
    }

    public ObservableCollection<OrderItem> Orders { get; } = [];

    public ICommand OpenCommand { get; }

    public ICommand LoadMoreCommand { get; }

    public ICommand RefreshCommand { get; }

    public OrderItem? Selected { get => _selected; set => Set(ref _selected, value, SelectedChanged); }

    public bool IsRefreshing { get => _isRefreshing; set => Set(ref _isRefreshing, value, IsRefreshingChanged); }

    public string Status { get => _status; set => Set(ref _status, value, StatusChanged); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Append(int count)
    {
        for (var index = 0; index < count; index++)
            Orders.Add(new OrderItem(_next++, this));
    }

    private void Set<T>(ref T field, T value, PropertyChangedEventArgs changed)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, changed);
    }
}
