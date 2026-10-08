namespace MauiSkiaUi;

/// <summary>How the items of an <see cref="SkUiCollectionView"/> are selected by taps.</summary>
public enum SkUiSelectionMode
{
    /// <summary>Taps do not select (default); <see cref="SkUiCollectionView.SelectedItem"/> stays <c>null</c>.</summary>
    None,
    /// <summary>A tap selects its item; tapping the selected item keeps it selected.</summary>
    Single,
    /// <summary>A tap selects its item; tapping the selected item clears the selection.</summary>
    SingleDeselect
}

/// <summary>Data of <see cref="SkUiCollectionView.ItemTapped"/>.</summary>
public sealed class SkUiItemTappedEventArgs(object? item, int index) : EventArgs
{
    /// <summary>The tapped item (of <see cref="SkUiCollectionView.ItemsSource"/>).</summary>
    public object? Item { get; } = item;

    /// <summary>The item's index in <see cref="SkUiCollectionView.ItemsSource"/>.</summary>
    public int Index { get; } = index;
}

/// <summary>Data of <see cref="SkUiCollectionView.SelectionChanging"/>: set <see cref="Cancel"/> to keep the selection.</summary>
public sealed class SkUiSelectionChangingEventArgs(object? previousItem, object? currentItem) : EventArgs
{
    /// <summary>The selected item now (<c>null</c>: none).</summary>
    public object? PreviousItem { get; } = previousItem;

    /// <summary>The item the tap selects (<c>null</c>: the selection is cleared).</summary>
    public object? CurrentItem { get; } = currentItem;

    /// <summary>Set to <c>true</c> to keep the current selection.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Data of <see cref="SkUiCollectionView.SelectionChanged"/>.</summary>
public sealed class SkUiSelectionChangedEventArgs(object? previousItem, object? currentItem) : EventArgs
{
    /// <summary>The item selected before (<c>null</c>: none).</summary>
    public object? PreviousItem { get; } = previousItem;

    /// <summary>The selected item now (<c>null</c>: none).</summary>
    public object? CurrentItem { get; } = currentItem;
}
