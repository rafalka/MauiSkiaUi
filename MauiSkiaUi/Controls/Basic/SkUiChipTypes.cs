namespace MauiSkiaUi;

/// <summary>What a chip is for (<see cref="SkUiChip.Kind"/>); the look draws them alike, the behavior differs.</summary>
public enum SkUiChipKind
{
    /// <summary>Runs an action when tapped (Material's assist chip): "Add break", "Share".</summary>
    Action,
    /// <summary>Selected and cleared by taps, with a check while selected: a filter of a list ("Approved", "This week").</summary>
    Filter,
    /// <summary>A value the user entered or picked (a recipient, a tag), removed with its × (shown by default).</summary>
    Input,
    /// <summary>A suggested reply or value the app offers; tapping it uses it.</summary>
    Suggestion
}

/// <summary>How many chips of a <see cref="SkUiChipGroup"/> can be selected (<see cref="SkUiChipGroup.SelectionMode"/>).</summary>
public enum SkUiChipSelectionMode
{
    /// <summary>Taps select nothing: the chips are actions or suggestions.</summary>
    None,
    /// <summary>One chip at most: a tap selects its chip and clears the others; tapping the selected chip clears it.</summary>
    Single,
    /// <summary>A tap selects or clears its chip (<see cref="SkUiChipGroup.SelectedItems"/>).</summary>
    Multiple
}

/// <summary>A chip's item was removed with its × (or a screen reader's remove action, or Delete): the app takes it out of its items.</summary>
/// <param name="item">The chip's item.</param>
/// <param name="index">Its index in the items.</param>
public sealed class SkUiChipRemovedEventArgs(object? item, int index) : EventArgs
{
    /// <summary>The removed chip's item.</summary>
    public object? Item { get; } = item;

    /// <summary>The item's index in the group's items.</summary>
    public int Index { get; } = index;
}
