namespace MauiSkiaUi;

/// <summary>
/// The members of a plain virtualized list: those of every list (<see cref="ISkUiItemsView"/>) plus item factories and the
/// realized / released item events. Shared by <see cref="SkUiVirtualVerticalStackLayout"/> (which implements them) and
/// <see cref="SkUiVirtualScrollView"/> (which forwards them to its <see cref="SkUiVirtualScrollView.Items"/>).
/// <see cref="SkUiCollectionView"/> implements <see cref="ISkUiItemsView"/> only: its items come from templates, and its
/// item views are hosted in item containers.
/// </summary>
public interface ISkUiVirtualList : ISkUiItemsView
{
    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ItemFactory" />
    Func<int, ISkUiView?>? ItemFactory { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ItemFactoryCount" />
    int? ItemFactoryCount { get; set; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ItemRealized" />
    event EventHandler<SkUiVirtualItemEventArgs>? ItemRealized;

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ItemReleased" />
    event EventHandler<SkUiVirtualItemEventArgs>? ItemReleased;
}
