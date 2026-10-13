using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls.Pickers;

/// <summary>
/// HOWTO: pickers in XAML over a view model. The page's XAML (PizzaOrderSample.xaml) shows:
/// <list type="number">
/// <item>A size picker drawn by templates: <c>SelectedItemTemplate</c> for the field, <c>ItemTemplate</c> for the rows, with a
/// <c>Selected</c> visual state on the row's root.</item>
/// <item>A crust picker with a <c>Placeholder</c> and a clear button (<c>IsClearButtonVisible</c>).</item>
/// <item>A toppings picker with several choices (<c>SelectionMode="Multiple"</c>, <c>SelectedItems</c> bound to the view model's
/// list) shown as tokens, an "All toppings" row, toppings out of stock that cannot be chosen (<c>ItemEnabledBinding</c>),
/// a footer of the app's in the popup, and a draft until Add (<c>UpdateMode="WhenFinished"</c>).</item>
/// <item>The total, recomputed by <c>SelectionChangedCommand</c>.</item>
/// </list>
/// </summary>
public sealed partial class PizzaOrderSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Pizza order (pickers in XAML)",
        Summary: "Pickers in XAML over a view model: a field and rows drawn by templates, a placeholder and a clear button, and " +
                 "several choices shown as tokens, with \"select all\", items out of stock and OK / Cancel.",
        HowTo:
        [
            "Draw the field and the rows: `<sk:SkUiPicker.SelectedItemTemplate>` and `<sk:SkUiPicker.ItemTemplate>` with " +
                "`DataTemplate`s of drawn views; style the selected row with a `Selected` state and `SelectionBackground=\"Transparent\"`.",
            "Several choices: `SelectionMode=\"Multiple\"` with `SelectedItems=\"{Binding Chosen}\"` (an `ObservableCollection<object>`), " +
                "`SelectedItemsDisplay=\"Tokens\"`, `IsSelectAllVisible=\"True\"`.",
            "Items that cannot be chosen: `<sk:SkUiPicker.ItemEnabledBinding><Binding Path=\"InStock\" x:DataType=\"local:Topping\" /></…>`.",
            "Wait for OK: `UpdateMode=\"WhenFinished\"`; name the buttons with `AcceptText` / `CancelText` (or translate them app-wide).",
            "Recompute on every change with `SelectionChangedCommand` (it runs before `SelectionChanged` / `SelectedIndexChanged`)."
        ],
        ThingsToKnow:
        [
            "Bindings inside `ItemDisplayBinding` and `ItemEnabledBinding` read the items: give them the item's `x:DataType`, as here, " +
                "or they are compiled against the page's.",
            "A token's × removes its topping at once (Backspace removes the last); in the popup, changes wait for Add.",
            "The app may still choose an item out of stock from code; the user cannot, and \"All toppings\" leaves it out.",
            "The popup's footer is the app's (`PopupFooterTemplate`), bound to the picker's binding context."
        ]);

    public PizzaOrderSample() : base(Info)
    {
        InitializeComponent();
        BindingContext = new PizzaOrderModel();
    }
}

/// <summary>A pizza size with its price.</summary>
public sealed record PizzaSize(string Name, int Centimetres, decimal Price)
{
    public string Description => $"{Centimetres} cm";

    public string PriceText => Price.ToString("C", CultureInfo.CurrentCulture);
}

/// <summary>A topping with its price, which may be out of stock.</summary>
public sealed record Topping(string Name, decimal Price, bool InStock = true)
{
    public string PriceText => "+" + Price.ToString("C", CultureInfo.CurrentCulture);
}

public sealed class PizzaOrderModel : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs SizeChanged = new(nameof(Size));
    private static readonly PropertyChangedEventArgs CrustChanged = new(nameof(Crust));
    private static readonly PropertyChangedEventArgs TotalChanged = new(nameof(Total));
    private static readonly PropertyChangedEventArgs SummaryChanged = new(nameof(Summary));

    private PizzaSize? _size;
    private string? _crust;

    public PizzaOrderModel()
    {
        _size = Sizes[1];
        ChosenToppings.Add(Toppings[0]);
        ChosenToppings.Add(Toppings[2]);
        UpdateCommand = new Command(Update);
        Update();
    }

    public IReadOnlyList<PizzaSize> Sizes { get; } = [new("Small", 26, 7.50m), new("Medium", 32, 9.90m), new("Large", 40, 12.90m), new("Family", 50, 17.50m)];

    public IReadOnlyList<string> Crusts { get; } = ["Thin", "Classic", "Wholemeal", "Gluten-free", "Stuffed"];

    public IReadOnlyList<Topping> Toppings { get; } =
    [
        new("Mozzarella", 1.20m), new("Basil", 0.50m), new("Mushrooms", 1.00m), new("Olives", 1.10m), new("Ham", 1.80m),
        new("Salami", 1.80m, InStock: false), new("Peppers", 0.90m), new("Onion", 0.60m), new("Pineapple", 1.20m, InStock: false),
        new("Anchovies", 2.00m), new("Rocket", 0.80m), new("Parmesan", 1.40m)
    ];

    /// <summary>The chosen toppings (the picker's <c>SelectedItems</c>).</summary>
    public ObservableCollection<object> ChosenToppings { get; } = [];

    public PizzaSize? Size
    {
        get => _size;
        set
        {
            if (Equals(_size, value)) return;
            _size = value;
            PropertyChanged?.Invoke(this, SizeChanged);
            Update();
        }
    }

    public string? Crust
    {
        get => _crust;
        set
        {
            if (_crust == value) return;
            _crust = value;
            PropertyChanged?.Invoke(this, CrustChanged);
            Update();
        }
    }

    /// <summary>Recomputes the total (the pickers' <c>SelectionChangedCommand</c>).</summary>
    public ICommand UpdateCommand { get; }

    public string Total { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Update()
    {
        var toppings = ChosenToppings.OfType<Topping>().ToList();
        var total = (_size?.Price ?? 0) + toppings.Sum(topping => topping.Price);
        Total = "Total " + total.ToString("C", CultureInfo.CurrentCulture);
        Summary = $"{_size?.Name ?? "No size"} · {_crust ?? "crust not chosen"} · " +
                  (toppings.Count == 0 ? "no toppings" : string.Join(", ", toppings.Select(topping => topping.Name)));
        PropertyChanged?.Invoke(this, TotalChanged);
        PropertyChanged?.Invoke(this, SummaryChanged);
    }
}
