namespace MauiSkiaUi;

public partial class SkUiCollectionView
{
    private object? _tapSelecting;   // the item a tap is selecting, while its selection change runs
    private int _tapSelectingIndex;  // its index (the appearance tapped, when the item shows in several groups)
    private object? _tappedSelection; // the last item a tap selected (the focus of a multiple selection)
    private int _tappedIndex = -1;    // the index it was tapped at
    private bool _revealPending;
    private bool _selectionFromSource; // selected items left the source: the selection shrank, nothing to reveal
    private Size _revealSize;

    /// <summary>Bindable property for <see cref="KeepSelectionVisible"/>.</summary>
    public static readonly BindableProperty KeepSelectionVisibleProperty = BindableProperty.Create(nameof(KeepSelectionVisible), typeof(bool), typeof(SkUiCollectionView), false,
        propertyChanged: (view, _, value) =>
        {
            if ((bool)value)
                ((SkUiCollectionView)view).RevealSelection(animated: true);
        });

    /// <summary>
    /// Keeps the selection in view (default <c>false</c>): when the selection changes, when the list's size changes (a
    /// rotation, a resized window) and when it is first laid out, the list scrolls as little as needed.
    /// <list type="bullet">
    /// <item>A single selection: the selected item shows fully.</item>
    /// <item>A multiple selection: the part of the list with the most selected items that fits, around a focus item: the
    /// item last selected by a tap (while it stays selected), else the first selected item in the list (a selection set by
    /// the app or a binding shows from its start). Nothing moves while what shows already holds as many.</item>
    /// <item>A tap only makes its item show fully (the list does not move away from the finger to gather other selected
    /// items); after a size change the tapped item stays in view with as many selected items as fit around it.</item>
    /// </list>
    /// Items of collapsed groups are not revealed (their groups stay collapsed). Scrolls are animated for selection changes
    /// and immediate for size changes.
    /// </summary>
    public bool KeepSelectionVisible { get => (bool)GetValue(KeepSelectionVisibleProperty); set => SetValue(KeepSelectionVisibleProperty, value); }

    /// <summary>The selection changed (<see cref="RaiseSelectionChanged"/>): a tap's item, or the selection's start, comes into view.</summary>
    private void OnSelectionChangedKeepVisible()
    {
        var tapped = _tapSelecting;
        if (tapped is not null && IsSelected(tapped))
            (_tappedSelection, _tappedIndex) = (tapped, _tapSelectingIndex);
        else if (tapped is null)
            (_tappedSelection, _tappedIndex) = (null, -1); // the app changed the selection: it shows from its start
        if (!KeepSelectionVisible || _selectionFromSource)
            return;
        if (tapped is not null)
        {
            if (IsSelected(tapped) && RowOfTapped() is >= 0 and var row)
                _items.ScrollToIndex(row, ScrollToPosition.MakeVisible, animated: true);
            return;
        }
        RevealSelection(animated: true);
    }

    /// <summary>After an arrange: a reveal that waited for the first layout, or the list's size changed.</summary>
    private void KeepSelectionVisibleAfterArrange(Size size)
    {
        var resized = size != _revealSize;
        _revealSize = size;
        if (!KeepSelectionVisible || !(resized || _revealPending))
            return;
        RevealSelection(animated: false);
    }

    private bool IsSelected(object item) => _selectionMode == SkUiSelectionMode.Multiple ? _selectedSet.Contains(item) : Equals(item, _selectedItem);

    /// <summary>
    /// The row of the appearance of <see cref="_tappedSelection"/> that was tapped, while it is still at the index it was
    /// tapped at; else (items moved since) the row of its first appearance.
    /// </summary>
    private int RowOfTapped()
    {
        if (_tappedSelection is not { } item)
            return -1;
        if (_tappedIndex >= 0 && _tappedIndex < _model.ItemCount)
        {
            var (group, index) = _model.Locate(_tappedIndex);
            if (Equals(_model.ItemAt(group, index), item))
                return _model.RowOfItem(group, index);
        }
        return RowOfItem(item);
    }

    private int RowOfItem(object item)
    {
        var index = _model.IndexOfItem(item);
        if (index < 0)
            return -1;
        var (group, inGroup) = _model.Locate(index);
        return _model.RowOfItem(group, inGroup);
    }

    /// <summary>Scrolls the selection into view (see <see cref="KeepSelectionVisible"/>); waits for the first layout.</summary>
    private void RevealSelection(bool animated)
    {
        if (!_items.HasWindow)
        {
            _revealPending = true;
            return;
        }
        _revealPending = false;
        if (_selectionMode is SkUiSelectionMode.Single or SkUiSelectionMode.SingleDeselect)
        {
            if (_selectedItem is { } selected && (Equals(selected, _tappedSelection) ? RowOfTapped() : RowOfItem(selected)) is >= 0 and var row)
                _items.ScrollToIndex(row, ScrollToPosition.MakeVisible, animated);
            return;
        }
        if (_selectionMode != SkUiSelectionMode.Multiple || _selectedSet.Count == 0)
            return;

        // The rows showing selected items, in order (one scan of the items: a large selection costs no lookups per item).
        var rows = new List<int>();
        for (var row = 0; row < _model.RowCount; row++)
        {
            var current = _model.RowAt(row);
            if (current.Kind != RowKind.Items)
                continue;
            for (var slot = 0; slot < current.Count; slot++)
                if (_model.ItemAt(current.Group, current.Start + slot) is { } item && _selectedSet.Contains(item))
                {
                    rows.Add(row);
                    break;
                }
        }
        if (rows.Count == 0)
            return;
        var focusRow = _tappedSelection is { } tapped && _selectedSet.Contains(tapped) ? RowOfTapped() : -1;
        var focus = focusRow >= 0 ? Math.Max(0, rows.IndexOf(focusRow)) : 0;

        // The band the sticky parts leave uncovered, in item coordinates: a sticky group header covers the start too.
        var (insetStart, insetEnd) = _scroller.Insets;
        insetStart += StickyGroupInset(rows[focus]);
        var band = Math.Max(0, (_horizontal ? Controller.Viewport.Width : Controller.Viewport.Height) - insetStart - insetEnd);
        var visibleStart = _items.VisibleStart + insetStart;
        var visibleEnd = visibleStart + band;
        double Start(int index) => _items.RowOffset(rows[index]);
        double End(int index) => _items.RowOffset(rows[index]) + _items.RowSize(rows[index]);

        // The window of selected rows that fits the band, contains the focus and holds the most of them.
        // Ties keep the window that starts at the focus (a selection set by the app shows from its start).
        int bestFirst = focus, bestLast = focus;
        for (var first = focus; first >= 0; first--)
        {
            if (first < focus && End(focus) - Start(first) > band)
                break;
            var last = focus;
            while (last + 1 < rows.Count && End(last + 1) - Start(first) <= band)
                last++;
            if (last - first > bestLast - bestFirst)
                (bestFirst, bestLast) = (first, last);
        }
        // What shows already holds as many (and the focus): nothing moves.
        var shown = 0;
        var focusShown = Start(focus) >= visibleStart && End(focus) <= visibleEnd;
        for (var index = 0; index < rows.Count; index++)
            if (Start(index) >= visibleStart && End(index) <= visibleEnd)
                shown++;
        if (focusShown && shown >= bestLast - bestFirst + 1)
            return;
        if (Start(bestFirst) < visibleStart)
            _items.ScrollToIndex(rows[bestFirst], ScrollToPosition.Start, animated);
        else
            _items.ScrollToIndex(rows[bestLast], ScrollToPosition.End, animated);
    }
}
