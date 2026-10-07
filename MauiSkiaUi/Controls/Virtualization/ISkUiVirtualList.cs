using System.Collections;
using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>
/// The members of a virtualized list, shared by <see cref="SkUiVirtualVerticalStackLayout"/> (which implements them) and
/// <see cref="SkUiVirtualScrollView"/> (which forwards them to its <see cref="SkUiVirtualScrollView.Items"/>). Code that only
/// configures or follows a list can take either through this interface.
/// </summary>
public interface ISkUiVirtualList
{
    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ItemsSource" />
    IEnumerable? ItemsSource { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ItemTemplate" />
    DataTemplate? ItemTemplate { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ItemFactory" />
    Func<int, ISkUiView?>? ItemFactory { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ItemFactoryCount" />
    int? ItemFactoryCount { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.Spacing" />
    double Spacing { get; set; }

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

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ItemRealized" />
    event EventHandler<SkUiVirtualItemEventArgs>? ItemRealized;

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ItemReleased" />
    event EventHandler<SkUiVirtualItemEventArgs>? ItemReleased;

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.VisibleRangeChanged" />
    event EventHandler<SkUiVisibleRangeChangedEventArgs>? VisibleRangeChanged;

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdReached" />
    event EventHandler? RemainingItemsThresholdReached;

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ScrollToIndex" />
    Task ScrollToIndex(int index, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true);

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.GetRealizedView" />
    ISkUiView? GetRealizedView(int index);

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemeasureItem" />
    void RemeasureItem(int index);
}
