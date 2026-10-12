using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiPicker"/>s in the rows of an <see cref="SkUiCollectionView"/> (a drawn scroller), next to MAUI's pickers in a
/// <see cref="CollectionView"/>: each row's picker is bound two-way to its order line, rows recycle as the list scrolls, and an
/// anchored list closes when the list under it scrolls (as a combo box's does).
/// </summary>
public sealed class PickerRowsDemoPage : ComponentDemoPage
{
    private static readonly string[] Sizes = ["XS", "S", "M", "L", "XL", "XXL"];
    private static readonly string[] Products = ["T-shirt", "Hoodie", "Cap", "Jacket", "Socks", "Scarf", "Jeans", "Sweater"];

    private readonly ObservableCollection<OrderLine> _lines = [];

    public PickerRowsDemoPage()
        : base("SkUiPicker rows", new SkUiCollectionView(), new CollectionView(), widthRange: (200, 400, 300), heightRange: (160, 600, 360))
    {
        var skia = (SkUiCollectionView)SkiaControl;
        var native = (CollectionView)NativeControl!;
        for (var index = 0; index < 40; index++)
            _lines.Add(new OrderLine($"{Products[index % Products.Length]} #{index + 1}", index % Sizes.Length));
        skia.ItemsSource = _lines;
        native.ItemsSource = _lines;
        skia.ItemTemplate = new DataTemplate(SkiaRow);
        native.ItemTemplate = new DataTemplate(NativeRow);
        Choice(nameof(SkUiPicker.PopupPlacement), [SkUiPopupPlacement.Anchored, SkUiPopupPlacement.Window], SkUiPopupPlacement.Anchored,
            value => _placement = value, () => _placement);
        ActionButton("All medium", () =>
        {
            foreach (var line in _lines)
                line.Size = 2;
        });
        Feedback("Pick sizes, scroll while a list is open");
    }

    private SkUiPopupPlacement _placement = SkUiPopupPlacement.Anchored;

    private SkUiGrid SkiaRow()
    {
        var name = new SkUiLabel { FontSize = 15, TextColor = DemoColors.Ink, VerticalOptions = LayoutOptions.Center };
        name.SetBinding(SkUiLabel.TextProperty, static (OrderLine line) => line.Product);
        var picker = new SkUiPicker { Title = "Size", ItemsSource = Sizes, WidthRequest = 110, TextColor = DemoColors.Ink };
        picker.SetBinding(SkUiPicker.SelectedIndexProperty, static (OrderLine line) => line.Size, BindingMode.TwoWay);
        picker.Opened += (_, _) => picker.PopupPlacement = _placement;
        var grid = new SkUiGrid { Padding = new Thickness(12, 6), ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)] };
        grid.Children.Add(name);
        grid.Children.Add(picker);
        Grid.SetColumn(picker, 1);
        return grid;
    }

    private static Grid NativeRow()
    {
        var name = new Label { FontSize = 15, TextColor = DemoColors.Ink, VerticalOptions = LayoutOptions.Center };
        name.SetBinding(Label.TextProperty, static (OrderLine line) => line.Product);
        var picker = new Picker { Title = "Size", ItemsSource = Sizes, WidthRequest = 110, TextColor = DemoColors.Ink };
        picker.SetBinding(Picker.SelectedIndexProperty, static (OrderLine line) => line.Size, BindingMode.TwoWay);
        var grid = new Grid { Padding = new Thickness(12, 6), ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)] };
        grid.Add(name);
        grid.Add(picker, 1);
        return grid;
    }

    /// <summary>One order line: a product and its size (an index of <see cref="Sizes"/>).</summary>
    public sealed class OrderLine(string product, int size) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Product { get; } = product;

        public int Size
        {
            get => size;
            set
            {
                if (size == value) return;
                size = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Size)));
            }
        }
    }
}
