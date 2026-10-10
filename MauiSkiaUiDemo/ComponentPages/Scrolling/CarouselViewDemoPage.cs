using System.Collections.ObjectModel;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCarouselView"/> (C4) with a linked <see cref="SkUiIndicatorView"/>, next to MAUI's <see cref="CarouselView"/>
/// and <see cref="IndicatorView"/> on the same destinations: swipe the cards (one per swipe), tap the dots. Editors for MAUI's
/// properties set both carousels (MAUI's get a new <see cref="LinearItemsLayout"/> for the items layout's settings: its handler
/// does not follow changes of the one it has); SkiaUi-only editors show
/// several cards at once (<see cref="SkUiCarouselView.ItemExtent"/>), item effects (cover flow, scaling) and loading more
/// cards as the end comes near. The indicator style is the look's (Look &amp; colors page).
/// </summary>
public sealed class CarouselViewDemoPage : ComponentDemoPage
{
    private const string NoEffect = "None";
    private const string CoverFlow = "Cover flow";
    private const string ScaleEffect = "Scale";
    private const string Previous = "Previous";
    private const string Next = "Next";

    private static readonly (string Name, string Country, Color Color)[] Destinations =
    [
        ("Moscow", "Russia", Color.FromArgb("#283593")),
        ("Algarve", "Portugal", Color.FromArgb("#00838F")),
        ("Athens", "Greece", Color.FromArgb("#6D4C41")),
        ("Dubai", "UAE", Color.FromArgb("#EF6C00")),
        ("Kyoto", "Japan", Color.FromArgb("#AD1457")),
        ("Reykjavik", "Iceland", Color.FromArgb("#37474F")),
        ("Cusco", "Peru", Color.FromArgb("#2E7D32")),
        ("Giza", "Egypt", Color.FromArgb("#C69214"))
    ];

    private readonly SkUiCarouselView _skia;
    private readonly SkUiIndicatorView _skiaIndicator;
    private readonly CarouselView _native;
    private readonly IndicatorView _nativeIndicator;
    private readonly ObservableCollection<Destination> _skiaItems = [];
    private readonly ObservableCollection<Destination> _nativeItems = [];
    private readonly SkUiCoverFlowEffect _coverFlow = new();
    private readonly SkUiScaleEffect _scale = new();
    private bool _endless;

    public CarouselViewDemoPage()
        : base(nameof(SkUiCarouselView), new SkUiGrid { RowDefinitions = [new(GridLength.Star), new(GridLength.Auto)], RowSpacing = 8 },
            new Grid { RowDefinitions = [new(GridLength.Star), new(GridLength.Auto)], RowSpacing = 8 },
            widthRange: (200, 600, 340), heightRange: (140, 300, 240))
    {
        // Room for the tallest preview (the default panel is shorter than the carousel and its indicator).
        SinglePanelHeight = 350;
        var skiaRoot = (SkUiGrid)SkiaControl;
        var nativeRoot = (Grid)NativeControl!;
        Fill(_skiaItems);
        Fill(_nativeItems);
        _skiaIndicator = new SkUiIndicatorView { HorizontalOptions = LayoutOptions.Center, HeightRequest = 16 };
        _skia = new SkUiCarouselView { ItemsSource = _skiaItems, ItemTemplate = new DataTemplate(SkiaCard), IndicatorView = _skiaIndicator };
        Grid.SetRow(_skiaIndicator, 1);
        skiaRoot.Children.Add(_skia);
        skiaRoot.Children.Add(_skiaIndicator);
        _nativeIndicator = new IndicatorView
        {
            HorizontalOptions = LayoutOptions.Center, HeightRequest = 16,
            IndicatorColor = SkUiColors.TrackOff, SelectedIndicatorColor = SkUiColors.Accent
        };
        _native = new CarouselView
        {
            ItemsSource = _nativeItems, ItemTemplate = new DataTemplate(NativeCard), IndicatorView = _nativeIndicator,
            ItemsLayout = NativeLayout(0, SnapPointsType.MandatorySingle, SnapPointsAlignment.Center)
        };
        nativeRoot.Add(_native);
        nativeRoot.Add(_nativeIndicator, 0, 1);
        _skia.PositionChanged += (_, _) => Status();
        _skia.Scrolled += (_, _) => Status();
        _native.PositionChanged += (_, _) => Status();
        _native.CurrentItemChanged += (_, _) => Status();
        _skia.RemainingItemsThresholdReached += (_, _) => LoadMore();
        Status();

        Toggle(nameof(SkUiCarouselView.Loop), true, value => { _skia.Loop = value; _native.Loop = value; }, () => _skia.Loop, () => _native.Loop);
        Number(nameof(SkUiCarouselView.Position), 0, Destinations.Length - 1, 0,
            value => { _skia.Position = (int)value; _native.Position = (int)value; },
            () => _skia.Position, () => _native.Position, whole: true);
        Number("PeekAreaInsets (left, right)", 0, 80, 0,
            value => { _skia.PeekAreaInsets = new Thickness(value, 0); _native.PeekAreaInsets = new Thickness(value, 0); },
            () => _skia.PeekAreaInsets.Left, () => _native.PeekAreaInsets.Left, whole: true);
        Number(nameof(LinearItemsLayout.ItemSpacing), 0, 40, 0,
            value => { _skia.ItemSpacing = value; ReplaceNativeLayout(spacing: value); },
            () => _skia.ItemSpacing, () => _native.ItemsLayout.ItemSpacing, whole: true);
        Choice(nameof(SkUiCarouselView.SnapPointsType), Enum.GetValues<SnapPointsType>(), SnapPointsType.MandatorySingle,
            value => { _skia.SnapPointsType = value; ReplaceNativeLayout(snapType: value); },
            () => _skia.SnapPointsType, () => _native.ItemsLayout.SnapPointsType);
        Choice(nameof(SkUiCarouselView.SnapPointsAlignment), Enum.GetValues<SnapPointsAlignment>(), SnapPointsAlignment.Center,
            value => { _skia.SnapPointsAlignment = value; ReplaceNativeLayout(alignment: value); },
            () => _skia.SnapPointsAlignment, () => _native.ItemsLayout.SnapPointsAlignment);
        Toggle(nameof(SkUiCarouselView.IsSwipeEnabled), true, value => { _skia.IsSwipeEnabled = value; _native.IsSwipeEnabled = value; },
            () => _skia.IsSwipeEnabled, () => _native.IsSwipeEnabled);
        Toggle(nameof(SkUiCarouselView.IsBounceEnabled), true, value => { _skia.IsBounceEnabled = value; _native.IsBounceEnabled = value; },
            () => _skia.IsBounceEnabled, () => _native.IsBounceEnabled);
        Toggle(nameof(SkUiCarouselView.IsScrollAnimated), true, value => { _skia.IsScrollAnimated = value; _native.IsScrollAnimated = value; },
            () => _skia.IsScrollAnimated, () => _native.IsScrollAnimated);
        Choice(nameof(IndicatorView.IndicatorsShape), Enum.GetValues<IndicatorShape>(), IndicatorShape.Circle,
            value => { _skiaIndicator.IndicatorsShape = value; _nativeIndicator.IndicatorsShape = value; },
            () => _skiaIndicator.IndicatorsShape, () => _nativeIndicator.IndicatorsShape);
        // SkiaUi only.
        Number(nameof(SkUiCarouselView.ItemExtent), 0, 300, 0, value => _skia.ItemExtent = value, () => _skia.ItemExtent, whole: true);
        Choice(nameof(SkUiCarouselView.ItemEffect), [NoEffect, CoverFlow, ScaleEffect], NoEffect, value => _skia.ItemEffect = value switch
        {
            CoverFlow => _coverFlow,
            ScaleEffect => _scale,
            _ => null
        }, () => _skia.ItemEffect switch
        {
            SkUiCoverFlowEffect => CoverFlow,
            SkUiScaleEffect => ScaleEffect,
            _ => NoEffect
        });
        Number("Cover flow " + nameof(SkUiCoverFlowEffect.RotationAngle), 0, 80, _coverFlow.RotationAngle,
            value => _coverFlow.RotationAngle = value, () => _coverFlow.RotationAngle, whole: true);
        Toggle("Load more at the end (RemainingItemsThreshold = 2)", false, value =>
        {
            _endless = value;
            _skia.RemainingItemsThreshold = value ? 2 : -1;
        }, () => _skia.RemainingItemsThreshold == 2);
        ActionButton(Previous, () => Step(-1));
        ActionButton(Next, () => Step(1));
        OnReset(() =>
        {
            ResetItems(_skiaItems);
            ResetItems(_nativeItems);
        });
    }

    private static LinearItemsLayout NativeLayout(double spacing, SnapPointsType snapType, SnapPointsAlignment alignment) =>
        new(ItemsLayoutOrientation.Horizontal) { ItemSpacing = spacing, SnapPointsType = snapType, SnapPointsAlignment = alignment };

    /// <summary>MAUI's carousel with a new items layout: its handler applies a new layout, not changes of the one it has.</summary>
    private void ReplaceNativeLayout(double? spacing = null, SnapPointsType? snapType = null, SnapPointsAlignment? alignment = null)
    {
        var layout = _native.ItemsLayout;
        _native.ItemsLayout = NativeLayout(spacing ?? layout.ItemSpacing, snapType ?? layout.SnapPointsType, alignment ?? layout.SnapPointsAlignment);
    }

    private void Step(int delta)
    {
        var count = _skiaItems.Count;
        var target = _skia.Loop ? (_skia.Position + delta + count) % count : Math.Clamp(_skia.Position + delta, 0, count - 1);
        _ = _skia.ScrollToIndex(target);
        if (target < _nativeItems.Count)
            _native.Position = target;
    }

    /// <summary>Appends the destinations again (the drawn carousel's threshold asks for more).</summary>
    private void LoadMore()
    {
        if (!_endless)
            return;
        var start = _skiaItems.Count;
        for (var index = 0; index < Destinations.Length; index++)
            _skiaItems.Add(new Destination(Destinations[index], start + index));
        Status();
    }

    private static void Fill(ObservableCollection<Destination> items)
    {
        for (var index = 0; index < Destinations.Length; index++)
            items.Add(new Destination(Destinations[index], index));
    }

    private static void ResetItems(ObservableCollection<Destination> items)
    {
        while (items.Count > Destinations.Length)
            items.RemoveAt(items.Count - 1);
    }

    private void Status() => Feedback(
        $"Position {_skia.Position} · {(_skia.CurrentItem as Destination)?.Name} · scroll {_skia.ScrollPosition:0.00} · {_skiaItems.Count} items",
        $"Position {_native.Position} · {(_native.CurrentItem as Destination)?.Name}");

    /// <summary>One destination card.</summary>
    /// <param name="Source">Its name, country and color.</param>
    /// <param name="Number">Its place in the list (cards loaded later count on).</param>
    internal sealed record Destination((string Name, string Country, Color Color) Source, int Number)
    {
        public string Name => Source.Name;

        public string Caption => $"{Source.Country} · #{Number + 1}";

        /// <summary>The card's fill: a brush, which MAUI's border clips to its rounded shape (a background color it does not).</summary>
        public Brush Fill { get; } = new SolidColorBrush(Source.Color);
    }

    private static SkUiBorder SkiaCard()
    {
        var title = new SkUiLabel { TextColor = Colors.White, FontSize = 22, FontAttributes = FontAttributes.Bold };
        title.SetBinding(SkUiLabel.TextProperty, static (Destination destination) => destination.Name);
        var caption = new SkUiLabel { TextColor = Colors.White.WithAlpha(0.8f), FontSize = 13 };
        caption.SetBinding(SkUiLabel.TextProperty, static (Destination destination) => destination.Caption);
        var card = new SkUiBorder
        {
            StrokeThickness = 0, Padding = new Thickness(16), StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Content = new SkUiVerticalStackLayout { VerticalOptions = LayoutOptions.End, Children = { title, caption } }
        };
        card.SetBinding(BackgroundProperty, static (Destination destination) => destination.Fill);
        return card;
    }

    private static Border NativeCard()
    {
        var title = new Label { TextColor = Colors.White, FontSize = 22, FontAttributes = FontAttributes.Bold };
        title.SetBinding(Label.TextProperty, static (Destination destination) => destination.Name);
        var caption = new Label { TextColor = Colors.White.WithAlpha(0.8f), FontSize = 13 };
        caption.SetBinding(Label.TextProperty, static (Destination destination) => destination.Caption);
        var card = new Border
        {
            StrokeThickness = 0, Padding = new Thickness(16), StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Content = new VerticalStackLayout { VerticalOptions = LayoutOptions.End, Children = { title, caption } }
        };
        card.SetBinding(BackgroundProperty, static (Destination destination) => destination.Fill);
        return card;
    }
}
