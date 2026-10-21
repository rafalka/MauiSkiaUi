using System.Collections;

namespace MauiSkiaUi;

/// <summary>
/// What a picker's selector chooses from, for a picker that shows its own UI (<c>SkUiPicker.SelectItemsAsync</c>,
/// <c>SkUiCorePicker.SelectItemsAsync</c>): the items as they were when it opened, their texts, which can be chosen and which are
/// chosen. The selector returns the chosen items, taken from <see cref="Items"/>.
/// </summary>
/// <remarks>Built for one opening; it holds the items (not the picker) until the selector returns.</remarks>
public sealed class SkUiPickerSelection
{
    private readonly object[] _items;
    private readonly IReadOnlyList<bool>? _itemsEnabled;

    private SkUiPickerSelection(SkUiPopupRequest request, object[] items, IReadOnlyList<string> texts, IReadOnlyList<bool>? itemsEnabled,
        IReadOnlyList<int> selected, bool allowsMultiple)
    {
        PopupRequest = request;
        _items = items;
        Texts = texts;
        _itemsEnabled = itemsEnabled;
        AllowsMultiple = allowsMultiple;
        SelectedItems = [.. selected.Where(index => index >= 0 && index < items.Length).Select(index => items[index])];
    }

    /// <summary>
    /// The items, in order: the picker's <c>ItemsSource</c> objects, else the texts of its <c>Items</c> (a <c>null</c> object of the
    /// source stands as its text).
    /// </summary>
    public IReadOnlyList<object> Items => _items;

    /// <summary>The items' display texts, in order (what the drawn list shows: <c>ItemDisplayBinding</c>, else <c>ToString()</c>).</summary>
    public IReadOnlyList<string> Texts { get; }

    /// <summary>The chosen items when the selector opened (one at most unless <see cref="AllowsMultiple"/>).</summary>
    public IReadOnlyList<object> SelectedItems { get; }

    /// <summary>Whether several items may be chosen (the picker's <c>SelectionMode</c> is <see cref="SkUiSelectionMode.Multiple"/>).</summary>
    public bool AllowsMultiple { get; }

    /// <summary>The picker's title, for a selector that shows one; <c>null</c> for none.</summary>
    public string? Title => PopupRequest.Title;

    /// <summary>
    /// The popup request underneath (where SkiaUi would place a popup, the anchor, its page and window, the texts of OK / Cancel),
    /// for a selector that needs more than the items. Not completed by the selector: the picker completes it with what it returns.
    /// </summary>
    public SkUiPopupRequest PopupRequest { get; }

    /// <summary>Whether the item at <paramref name="index"/> can be chosen (the picker's <c>ItemEnabledBinding</c>): disabled items show but cannot be chosen.</summary>
    public bool IsItemEnabled(int index) => SkUiItemStates.IsEnabled(_itemsEnabled, index);

    /// <summary>The selection a list request of a picker with <paramref name="source"/> carries; <c>null</c> for other requests (wheels, dates).</summary>
    internal static SkUiPickerSelection? From(SkUiPopupRequest request, IList? source) => request switch
    {
        SkUiListPopupRequest list => new SkUiPickerSelection(request, ItemsOf(list.Items, source), list.Items, list.ItemsEnabled, [list.SelectedIndex], false),
        SkUiMultiListPopupRequest multiple => new SkUiPickerSelection(request, ItemsOf(multiple.Items, source), multiple.Items, multiple.ItemsEnabled,
            multiple.SelectedIndices, true),
        _ => null
    };

    private static object[] ItemsOf(IReadOnlyList<string> texts, IList? source)
    {
        var items = new object[texts.Count];
        for (var index = 0; index < items.Length; index++)
            items[index] = source is not null && index < source.Count && source[index] is { } item ? item : texts[index];
        return items;
    }

    /// <summary>
    /// The request's choice as items, after the drawn popup closed (the default selector): <c>null</c> without one (a dismissal, a close),
    /// else the chosen items (none: the choice was cleared).
    /// </summary>
    internal IReadOnlyList<object>? Chosen() => PopupRequest switch
    {
        SkUiListPopupRequest { HasResult: true, Result: var index } => index >= 0 && index < _items.Length ? [_items[index]] : [],
        SkUiMultiListPopupRequest { HasResult: true, Result: { } indices } => [.. indices.Where(index => index < _items.Length).Select(index => _items[index])],
        _ => null
    };

    /// <summary>
    /// Completes the request with <paramref name="chosen"/> (the selector's answer): nothing for <c>null</c> (dismissed) or when the
    /// request already has a result (the drawn popup chose); none clears the choice; a list of one choice takes the first item;
    /// items that are not in <see cref="Items"/> are ignored, and disabled items keep their state.
    /// </summary>
    internal void Apply(IReadOnlyList<object>? chosen)
    {
        if (chosen is null || PopupRequest.HasResult || PopupRequest.IsCompleted || PopupRequest.CloseRequested.IsCancellationRequested)
            return;
        switch (PopupRequest)
        {
            case SkUiListPopupRequest list:
                var index = chosen.Count == 0 ? -1 : IndexOf(chosen[0]);
                if (chosen.Count == 0 || index >= 0)
                    list.Complete(index);
                break;
            case SkUiMultiListPopupRequest multiple:
                var current = multiple.SelectedIndices;
                var indices = new SortedSet<int>();
                foreach (var item in chosen)
                    if (IndexOf(item) is >= 0 and var found && IsItemEnabled(found))
                        indices.Add(found);
                // Disabled items keep their state: chosen ones stay chosen, others cannot be chosen.
                foreach (var kept in current)
                    if (!IsItemEnabled(kept))
                        indices.Add(kept);
                multiple.Complete([.. indices]);
                break;
        }
    }

    /// <summary>Where <paramref name="item"/> is in <see cref="Items"/>: the same object first, else an equal one; -1 when it is not there.</summary>
    private int IndexOf(object item)
    {
        for (var index = 0; index < _items.Length; index++)
            if (ReferenceEquals(_items[index], item))
                return index;
        for (var index = 0; index < _items.Length; index++)
            if (Equals(_items[index], item))
                return index;
        return -1;
    }
}
