using System.ComponentModel;

namespace MauiSkiaUi;

/// <summary>How the items of an <see cref="SkUiCollectionView"/> are selected by taps.</summary>
public enum SkUiSelectionMode
{
    /// <summary>Taps do not select (default); <see cref="SkUiCollectionView.SelectedItem"/> stays <c>null</c>.</summary>
    None,
    /// <summary>A tap selects its item; tapping the selected item keeps it selected.</summary>
    Single,
    /// <summary>A tap selects its item; tapping the selected item clears the selection.</summary>
    SingleDeselect,
    /// <summary>A tap adds its item to <see cref="SkUiCollectionView.SelectedItems"/>, or removes it when it is there.</summary>
    Multiple
}

/// <summary>When an <see cref="SkUiCollectionView"/> asks for more items (<see cref="SkUiCollectionView.LoadMoreCommand"/>).</summary>
public enum SkUiLoadMoreMode
{
    /// <summary>Never (default); no load-more row.</summary>
    None,
    /// <summary>When the load-more row (a "Load more" button by default) is tapped.</summary>
    Manual,
    /// <summary>When the scroll reaches the load-more end of the items (also when the items do not fill the list).</summary>
    Auto,
    /// <summary>As <see cref="Auto"/>, but only once the user has scrolled the list (nothing loads on the first layout).</summary>
    AutoOnUserScroll
}

/// <summary>Where an <see cref="SkUiCollectionView"/> loads more items.</summary>
public enum SkUiLoadMorePosition
{
    /// <summary>After the last item (default): feeds, search results.</summary>
    End,
    /// <summary>Before the first item: older messages of a chat. Items inserted at the start keep what shows in place.</summary>
    Start
}

/// <summary>
/// A group of an <see cref="SkUiCollectionView"/> (<see cref="SkUiCollectionView.IsGrouped"/>) that keeps its own expanded
/// state: the list reads <see cref="IsExpanded"/> for a new group, writes it when the group is expanded or collapsed, and
/// follows its changes when the group raises <see cref="INotifyPropertyChanged.PropertyChanged"/>.
/// </summary>
public interface ISkUiExpandableGroup
{
    /// <summary>Whether the group's items show.</summary>
    bool IsExpanded { get; set; }
}

/// <summary>Data of <see cref="SkUiCollectionView.ItemTapped"/>.</summary>
public sealed class SkUiItemTappedEventArgs(object? item, int index, object? group = null) : EventArgs
{
    /// <summary>The tapped item (of <see cref="SkUiCollectionView.ItemsSource"/>, or of its group).</summary>
    public object? Item { get; } = item;

    /// <summary>The item's index among all items of the list (in a grouped list, counted across the groups).</summary>
    public int Index { get; } = index;

    /// <summary>The item's group in a grouped list; else <c>null</c>.</summary>
    public object? Group { get; } = group;
}

/// <summary>Data of <see cref="SkUiCollectionView.SelectionChanging"/>: set <see cref="Cancel"/> to keep the selection.</summary>
public sealed class SkUiSelectionChangingEventArgs : EventArgs
{
    /// <summary>A change of a single selection.</summary>
    public SkUiSelectionChangingEventArgs(object? previousItem, object? currentItem)
        : this(previousItem, currentItem, Listed(previousItem), Listed(currentItem)) { }

    /// <summary>A change of a multiple selection (<see cref="SkUiSelectionMode.Multiple"/>).</summary>
    public SkUiSelectionChangingEventArgs(IReadOnlyList<object> previousSelection, IReadOnlyList<object> currentSelection)
        : this(null, null, previousSelection, currentSelection) { }

    private SkUiSelectionChangingEventArgs(object? previousItem, object? currentItem, IReadOnlyList<object> previousSelection, IReadOnlyList<object> currentSelection)
    {
        PreviousItem = previousItem;
        CurrentItem = currentItem;
        PreviousSelection = previousSelection;
        CurrentSelection = currentSelection;
    }

    /// <summary>The item selected before this change (<c>null</c>: none; always <c>null</c> for a multiple selection).</summary>
    public object? PreviousItem { get; }

    /// <summary>The item the tap selects (<c>null</c>: the selection is cleared; always <c>null</c> for a multiple selection).</summary>
    public object? CurrentItem { get; }

    /// <summary>The items selected before this change.</summary>
    public IReadOnlyList<object> PreviousSelection { get; }

    /// <summary>The selected items after the tap.</summary>
    public IReadOnlyList<object> CurrentSelection { get; }

    /// <summary>Set to <c>true</c> to keep the current selection.</summary>
    public bool Cancel { get; set; }

    internal static IReadOnlyList<object> Listed(object? item) => item is null ? [] : [item];
}

/// <summary>Data of <see cref="SkUiCollectionView.SelectionChanged"/>.</summary>
public sealed class SkUiSelectionChangedEventArgs : EventArgs
{
    /// <summary>A change of a single selection.</summary>
    public SkUiSelectionChangedEventArgs(object? previousItem, object? currentItem)
        : this(previousItem, currentItem, SkUiSelectionChangingEventArgs.Listed(previousItem), SkUiSelectionChangingEventArgs.Listed(currentItem)) { }

    /// <summary>A change of a multiple selection (<see cref="SkUiSelectionMode.Multiple"/>).</summary>
    public SkUiSelectionChangedEventArgs(IReadOnlyList<object> previousSelection, IReadOnlyList<object> currentSelection)
        : this(null, null, previousSelection, currentSelection) { }

    private SkUiSelectionChangedEventArgs(object? previousItem, object? currentItem, IReadOnlyList<object> previousSelection, IReadOnlyList<object> currentSelection)
    {
        PreviousItem = previousItem;
        CurrentItem = currentItem;
        PreviousSelection = previousSelection;
        CurrentSelection = currentSelection;
    }

    /// <summary>The item selected before (<c>null</c>: none; always <c>null</c> for a multiple selection).</summary>
    public object? PreviousItem { get; }

    /// <summary>The selected item now (<c>null</c>: none; always <c>null</c> for a multiple selection).</summary>
    public object? CurrentItem { get; }

    /// <summary>The items selected before.</summary>
    public IReadOnlyList<object> PreviousSelection { get; }

    /// <summary>The selected items now.</summary>
    public IReadOnlyList<object> CurrentSelection { get; }
}

/// <summary>Data of <see cref="SkUiCollectionView.GroupExpanded"/> and <see cref="SkUiCollectionView.GroupCollapsed"/>.</summary>
public class SkUiGroupEventArgs(object? group, int groupIndex) : EventArgs
{
    /// <summary>The group (an element of <see cref="SkUiCollectionView.ItemsSource"/>).</summary>
    public object? Group { get; } = group;

    /// <summary>The group's index in <see cref="SkUiCollectionView.ItemsSource"/>.</summary>
    public int GroupIndex { get; } = groupIndex;
}

/// <summary>Data of <see cref="SkUiCollectionView.GroupExpanding"/> and <see cref="SkUiCollectionView.GroupCollapsing"/>: set <see cref="Cancel"/> to keep the group as it is.</summary>
public sealed class SkUiGroupChangingEventArgs(object? group, int groupIndex) : SkUiGroupEventArgs(group, groupIndex)
{
    /// <summary>Set to <c>true</c> to keep the group expanded or collapsed.</summary>
    public bool Cancel { get; set; }
}
