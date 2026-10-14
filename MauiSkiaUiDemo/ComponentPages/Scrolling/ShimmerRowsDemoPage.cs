using System.ComponentModel;
using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// A long list whose every row shimmers until it loads: an <see cref="SkUiShimmer"/> per row (only <c>IsLoading</c> bound) with
/// Core bones, in an <see cref="SkUiCollectionView"/>. With the surface-wide region one wave crosses all rows as if they
/// were one; every row is in phase whenever it was recycled. The frame time of a scrolling list of shimmering rows.
/// </summary>
public sealed class ShimmerRowsDemoPage : ComponentDemoPage
{
    private readonly LoadingState _state = new();

    public ShimmerRowsDemoPage()
        : base("SkUiShimmer rows", new SkUiCollectionView(), widthRange: (200, 400, 300), heightRange: (160, 600, 420))
    {
        var list = (SkUiCollectionView)SkiaControl;
        var region = SkUiShimmerWaveRegion.Surface;
        list.ItemsSource = Enumerable.Range(1, 300).Select(index => $"Row {index}").ToList();
        list.ItemTemplate = new DataTemplate(() => Row(_state, region));
        Toggle("Loading", true, value => _state.IsLoading = value, () => _state.IsLoading);
        Choice(nameof(SkUiShimmer.WaveRegion), [SkUiShimmerWaveRegion.Surface, SkUiShimmerWaveRegion.Shimmer], SkUiShimmerWaveRegion.Surface, value =>
        {
            region = value;
            list.ItemTemplate = new DataTemplate(() => Row(_state, region));
        }, () => region);
        Feedback("Scroll while loading: nothing is recorded per frame");
    }

    private static SkUiShimmer Row(LoadingState state, SkUiShimmerWaveRegion region)
    {
        var label = new SkUiLabel { FontSize = 15, TextColor = DemoColors.Ink, Padding = new Thickness(12, 14) };
        label.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath);
        var shimmer = new SkUiShimmer { Content = label, SkeletonContent = Bones(), WaveRegion = region, HeightRequest = 48 };
        shimmer.SetBinding(SkUiShimmer.IsLoadingProperty, new Binding(nameof(LoadingState.IsLoading), source: state));
        return shimmer;
    }

    private static SkUiCoreHost Bones()
    {
        var row = new SkUiCoreHorizontalStackLayout { Padding = new Thickness(12, 8), Spacing = 10 };
        row.Add(new SkUiCoreBox { Color = Colors.Black, CornerRadius = 16, Width = 32, Height = 32 });
        row.Add(new SkUiCoreBox { Color = Colors.Black, CornerRadius = 4, Width = 160, Height = 12, VerticalAlignment = LayoutAlignment.Center });
        return new SkUiCoreHost().SetContent(row);
    }

    private sealed class LoadingState : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs IsLoadingChanged = new(nameof(IsLoading));

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsLoading
        {
            get => field;
            set { field = value; PropertyChanged?.Invoke(this, IsLoadingChanged); }
        } = true;
    }
}
