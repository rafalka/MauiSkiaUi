using System.Collections.ObjectModel;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// MAUI's items layouts on <see cref="SkUiCollectionView"/> next to MAUI's <see cref="CollectionView"/> (steps V1, V4 and V5
/// of the 1.2 plan): one <see cref="LinearItemsLayout"/> or <see cref="GridItemsLayout"/> object set on both lists, with its
/// snap points, and <see cref="ItemsUpdatingScrollMode"/> as items are inserted at the start or added at the end.
/// </summary>
public sealed class ItemsLayoutDemoPage : ComponentDemoPage
{
    private const string VerticalList = "VerticalList";
    private const string HorizontalList = "HorizontalList";
    private const string VerticalGrid = "VerticalGrid, 2";
    private const string HorizontalGrid = "HorizontalGrid, 2";
    private const double CardSize = 96;
    private const double Spacing = 8;

    private static readonly Color[] Colors3 = [Accent, DemoColors.SampleA, DemoColors.SampleB];

    private readonly SkUiCollectionView _skia;
    private readonly CollectionView _native;
    private readonly ObservableCollection<Card> _cards = [];
    private string _layout = VerticalList;
    private SnapPointsType _snapType = SnapPointsType.MandatorySingle;
    private SnapPointsAlignment _snapAlignment = SnapPointsAlignment.Start;
    private int _next;

    public ItemsLayoutDemoPage() : base("SkUiCollectionView (items layouts and snap points)", new SkUiCollectionView(), new CollectionView(),
        widthRange: (160, 400, 300), heightRange: (160, 600, 360))
    {
        _skia = (SkUiCollectionView)SkiaControl;
        _native = (CollectionView)NativeControl!;
        DocumentTypes(typeof(ItemsLayout));
        for (var index = 0; index < 40; index++)
            _cards.Add(NewCard());
        _skia.ItemTemplate = new DataTemplate(() => new SkiaCard());
        _skia.ItemsSource = _cards;
        _skia.Scrolled += (_, _) => Status();
        _native.ItemTemplate = new DataTemplate(() => new NativeCard());
        _native.ItemsSource = _cards;
        Apply();

        Choice(nameof(SkUiCollectionView.ItemsLayout), [VerticalList, HorizontalList, VerticalGrid, HorizontalGrid], VerticalList,
            value => { _layout = value; Apply(); }, () => _layout);
        Choice(nameof(ItemsLayout.SnapPointsType), Enum.GetValues<SnapPointsType>(), SnapPointsType.MandatorySingle,
            value => { _snapType = value; Apply(); }, () => ((ItemsLayout)_skia.ItemsLayout).SnapPointsType, () => ((ItemsLayout)_native.ItemsLayout).SnapPointsType);
        Choice(nameof(ItemsLayout.SnapPointsAlignment), Enum.GetValues<SnapPointsAlignment>(), SnapPointsAlignment.Start,
            value => { _snapAlignment = value; Apply(); }, () => ((ItemsLayout)_skia.ItemsLayout).SnapPointsAlignment, () => ((ItemsLayout)_native.ItemsLayout).SnapPointsAlignment);
        Choice(nameof(SkUiCollectionView.ItemsUpdatingScrollMode), Enum.GetValues<ItemsUpdatingScrollMode>(), ItemsUpdatingScrollMode.KeepItemsInView,
            value => { _skia.ItemsUpdatingScrollMode = value; _native.ItemsUpdatingScrollMode = value; },
            () => _skia.ItemsUpdatingScrollMode, () => _native.ItemsUpdatingScrollMode);
        ActionButton("Insert 3 at the start", () =>
        {
            for (var count = 0; count < 3; count++)
                _cards.Insert(0, NewCard());
            Status();
        });
        ActionButton("Add one at the end", () => { _cards.Add(NewCard()); Status(); });
        ActionButton("Scroll to 20", () =>
        {
            _skia.ScrollTo(20, position: ScrollToPosition.Start);
            _native.ScrollTo(20, position: ScrollToPosition.Start);
        });
    }

    private Card NewCard()
    {
        var index = _next++;
        return new Card($"Card {index}", Colors3[index % Colors3.Length]);
    }

    /// <summary>One layout object for both lists, as a page of MAUI's would set it.</summary>
    private void Apply()
    {
        ItemsLayout layout = _layout switch
        {
            HorizontalList => new LinearItemsLayout(ItemsLayoutOrientation.Horizontal) { ItemSpacing = Spacing },
            VerticalGrid => new GridItemsLayout(2, ItemsLayoutOrientation.Vertical) { HorizontalItemSpacing = Spacing, VerticalItemSpacing = Spacing },
            HorizontalGrid => new GridItemsLayout(2, ItemsLayoutOrientation.Horizontal) { HorizontalItemSpacing = Spacing, VerticalItemSpacing = Spacing },
            _ => new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = Spacing }
        };
        layout.SnapPointsType = _snapType;
        layout.SnapPointsAlignment = _snapAlignment;
        _skia.ItemsLayout = layout;
        _native.ItemsLayout = layout;
        Status();
    }

    private void Status()
    {
        var offset = _skia.ItemsLayout is { } layout && IsHorizontal(layout) ? _skia.ScrollX : _skia.ScrollY;
        Feedback($"{_cards.Count} cards · offset {offset:0} · first visible {_skia.FirstVisibleIndex}");
    }

    private static bool IsHorizontal(IItemsLayout layout) =>
        layout is LinearItemsLayout { Orientation: ItemsLayoutOrientation.Horizontal } or GridItemsLayout { Orientation: ItemsLayoutOrientation.Horizontal };

    private sealed record Card(string Title, Color Color);

    /// <summary>A square card: a colored band and the title.</summary>
    private sealed class SkiaCard : SkUiBorder
    {
        private readonly SkUiLabel _title = new() { FontSize = 14, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };

        public SkiaCard()
        {
            WidthRequest = CardSize;
            HeightRequest = CardSize;
            StrokeThickness = 0;
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 };
            Content = _title;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not Card card)
                return;
            Background = card.Color;
            _title.Text = card.Title;
        }
    }

    private sealed class NativeCard : Border
    {
        private readonly Label _title = new() { FontSize = 14, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };

        public NativeCard()
        {
            WidthRequest = CardSize;
            HeightRequest = CardSize;
            StrokeThickness = 0;
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 };
            Content = _title;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not Card card)
                return;
            Background = card.Color;
            _title.Text = card.Title;
        }
    }
}
