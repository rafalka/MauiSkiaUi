using System.Collections;
using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>
/// The members every virtualized list has, with the same names, types and defaults: <see cref="SkUiVirtualVerticalStackLayout"/>
/// (which implements them), and <see cref="SkUiVirtualScrollView"/> and <see cref="SkUiCollectionView"/> (which forward them to
/// the layout inside, declaring their bindable properties from the layout's). Code that only configures or follows a list
/// (its items, sizes, prefetch, loading more, what shows, scrolling to an item) can take any of them through this interface.
/// </summary>
/// <remarks>
/// <see cref="ISkUiVirtualList"/> adds what only the plain lists have (item factories, realized / released item events).
/// A test checks that every settable member reaches the layout from each list, so the three cannot drift apart.
/// </remarks>
public interface ISkUiItemsView
{
    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ItemsSource" />
    IEnumerable? ItemsSource { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ItemTemplate" />
    DataTemplate? ItemTemplate { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ItemSpacing" />
    double ItemSpacing { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ItemExtent" />
    double ItemExtent { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.EstimatedItemSize" />
    double EstimatedItemSize { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.PrefetchFactor" />
    double PrefetchFactor { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.PrefetchBehindFactor" />
    double PrefetchBehindFactor { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ReleaseFactor" />
    double ReleaseFactor { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.PrefetchBudget" />
    TimeSpan? PrefetchBudget { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemainingItemsThreshold" />
    int RemainingItemsThreshold { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdReachedCommand" />
    ICommand? RemainingItemsThresholdReachedCommand { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdReachedCommandParameter" />
    object? RemainingItemsThresholdReachedCommandParameter { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ItemCount" />
    int ItemCount { get; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.FirstVisibleIndex" />
    int FirstVisibleIndex { get; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.LastVisibleIndex" />
    int LastVisibleIndex { get; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.VisibleRangeChanged" />
    event EventHandler<SkUiVisibleRangeChangedEventArgs>? VisibleRangeChanged;

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdReached" />
    event EventHandler? RemainingItemsThresholdReached;

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ScrollToIndex" />
    Task ScrollToIndex(int index, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true);

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ScrollToItem" />
    Task ScrollToItem(object? item, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true);

    /// <summary>
    /// The view the item template (or factory) created for the item at <paramref name="index"/>, while it is realized; else
    /// <c>null</c>.
    /// </summary>
    ISkUiView? GetRealizedView(int index);

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemeasureItem" />
    void RemeasureItem(int index);
}
