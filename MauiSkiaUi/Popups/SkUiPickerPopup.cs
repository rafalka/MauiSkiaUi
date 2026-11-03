using System.Collections;
using System.Globalization;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A picker popup's search field (the picker's <c>IsSearchEnabled</c>, shown for enough items): its placeholder and the app's
/// filter (the item and the phrase; <c>null</c>: the item's text contains the phrase, ignoring case and accents).
/// </summary>
internal sealed record SkUiPickerSearch(string Placeholder, Func<object?, string, bool>? Filter);

/// <summary>
/// How a picker's popup rows draw their text without an item template (the picker's text properties), and what is behind the
/// selected rows (<c>null</c>: the look's <see cref="SkUiLook.PopupHighlightColor"/>).
/// </summary>
internal readonly record struct SkUiPickerRowStyle(string? FontFamily, double FontSize, FontAttributes FontAttributes, bool FontAutoScalingEnabled,
    double CharacterSpacing, Color? TextColor, Brush? SelectionBackground = null);

/// <summary>
/// A picker's popup content: a drawn surface of its own with the items as a virtualized <see cref="SkUiCollectionView"/>
/// (plain rows, or the picker's <c>ItemTemplate</c>), the selection highlighted and scrolled into view, an optional title
/// above (window popups). A tap on a row chooses it. With keyboard focus (it takes it when shown): Up / Down, Page Up /
/// Page Down, Home / End move the highlight, typing jumps to the item starting with what was typed, Enter or Space chooses,
/// Escape closes without a choice. Holds the items it was opened with, never the picker.
/// </summary>
/// <remarks>
/// <para>
/// <b>Several choices</b> (a picker with <c>SelectionMode</c> <c>Multiple</c>): the list selects several rows (a check box in each),
/// every change goes to the request (<see cref="SkUiSelectionPopupRequest.SetSelection"/>), and the popup stays open. The
/// keyboard moves a cursor: Space toggles the row at it, Enter accepts, Escape cancels.
/// </para>
/// <para>
/// <b>Accepted choices</b> (<c>UpdateMode</c> <see cref="SkUiPickerUpdateMode.WhenFinished"/>): a tap only highlights (one choice)
/// or toggles a draft (several), and Cancel / OK buttons below the list cancel or accept.
/// </para>
/// </remarks>
internal sealed class SkUiPickerPopup : SkUiContentView, ISkUiKeyHandler, ISkUiPickerPopupContent
{
    /// <summary>Items whose texts are measured for the popup's width (more would cost too much on a long list).</summary>
    private const int MeasuredItems = 200;

    /// <summary>Templated rows measured for the popup's size, at most (the rest are taken as tall as their average).</summary>
    private const int MeasuredTemplatedRows = 50;

    private readonly SkUiCollectionView _list;
    private readonly SkUiLabel? _title;
    private readonly SkUiView? _footer;
    private readonly SkUiView? _appHeader;
    private readonly SkUiView? _appFooter;
    private readonly SelectAllRow? _selectAll;
    private readonly SkUiSearchBar? _search;
    private readonly SkUiLabel? _noResults;
    private readonly SkUiGrid? _column;
    private readonly Func<object?, string, bool>? _filter;
    private readonly SkUiPickerPopupChrome _chrome;
    private readonly IReadOnlyList<string> _texts;
    private readonly SkUiPickerRowStyle _style;
    private readonly DataTemplate? _template;
    private Row[] _rows;
    private List<Row> _shown;
    private string _phrase = string.Empty;
    private readonly bool _multiple;
    private readonly bool _closesOnToggle;
    private readonly Action<IReadOnlyList<int>>? _apply;
    private Action<int>? _choose;
    private readonly Func<IReadOnlyList<int>>? _currentSelection;
    private readonly int _initialSelected;
    private readonly int[] _initialChecked = [];
    private IReadOnlyList<bool>? _itemEnabled;
    private int _highlighted;
    private SkUiTypeAhead _typeAhead;
    private bool _focusedOnce;
    private bool _applying;
    private RowView? _probe;
    // Where the popup opened (it fits into the room its presenter reports, and an anchored one with a search resizes against its
    // anchor as the search filters), and whether the search field is under the rows.
    private SkUiPopupLayout _opened;
    private bool _filterResizes;
    private bool _searchUnderRows;

    /// <summary>Check box (24) and its gap (12) in rows of a list with several choices.</summary>
    private const double CheckColumnWidth = 36;

    /// <summary>
    /// Creates the content. <paramref name="apply"/> (with <paramref name="currentSelection"/>, the picker's chosen indices now)
    /// makes it a list of several choices starting with <paramref name="checkedIndices"/>; <paramref name="selected"/> is for single lists.
    /// <paramref name="chrome"/>: whether the choice waits to be accepted, and the buttons. <paramref name="choose"/> applies a single
    /// list's choice while the popup stays open (<see cref="SkUiListPopupRequest.SetSelectedIndex"/> from a presenter of one's own).
    /// <paramref name="search"/>: a search field above the rows that filters them as the user types.
    /// </summary>
    public SkUiPickerPopup(IReadOnlyList<string> texts, int selected, DataTemplate? template, IList? itemsSource, SkUiPickerRowStyle style, string? title,
        bool rightToLeft, SkUiPickerPopupChrome chrome, Action<IReadOnlyList<int>>? apply = null, Func<IReadOnlyList<int>>? currentSelection = null,
        IReadOnlyList<int>? checkedIndices = null, bool closesOnToggle = false, IReadOnlyList<bool>? itemEnabled = null, SkUiPopupExtras extras = default,
        string? selectAllText = null, Action<int>? choose = null, SkUiPickerSearch? search = null)
    {
        _choose = choose;
        _closesOnToggle = closesOnToggle;
        _itemEnabled = itemEnabled;
        _texts = texts;
        _chrome = chrome;
        _style = style;
        _template = template;
        _multiple = apply is not null;
        _apply = apply;
        _currentSelection = currentSelection;
        _initialSelected = selected;
        if (_multiple)
            _initialChecked = [.. checkedIndices ?? []];
        _highlighted = !_multiple && selected >= 0 && selected < texts.Count ? selected : -1;
        var look = SkUiLook.Current;
        FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        BackgroundColor = SkUiColors.DefaultBackground;
        AutomationProperties.SetIsInAccessibleTree(this, false);
        _rows = new Row[texts.Count];
        for (var index = 0; index < _rows.Length; index++)
            _rows[index] = new Row(index, texts[index], itemsSource is not null && index < itemsSource.Count ? itemsSource[index] : texts[index])
            {
                IsEnabled = SkUiItemStates.IsEnabled(itemEnabled, index),
                Position = index
            };
        _shown = [.. _rows];
        _list = new SkUiCollectionView
        {
            SelectionBackground = style.SelectionBackground ?? new SolidColorBrush(look.PopupHighlightColor),
            ItemTemplate = new DataTemplate(() => new RowView(template, style, _multiple)),
            ItemsSource = _shown
        };
        if (search is not null)
        {
            _filter = search.Filter;
            _search = CreateSearchField(search.Placeholder, style);
            _search.TextChanged += OnSearchTextChanged;
            _search.SearchButtonPressed += OnSearchButtonPressed;
            _search.KeyPreview = OnSearchKeyDown;
            _list.EmptyView = _noResults = NoResults(style);
        }
        if (_multiple)
        {
            _list.SelectionMode = SkUiSelectionMode.Multiple;
            foreach (var index in _initialChecked)
                if (index >= 0 && index < _rows.Length)
                {
                    _rows[index].IsChecked = true;
                    _list.SelectedItems.Add(_rows[index]);
                }
            _list.SelectionChanged += OnListSelectionChanged;
        }
        else
        {
            _list.SelectionMode = SkUiSelectionMode.Single;
        }
        if (template is null)
            _list.ItemExtent = look.PopupItemHeight;
        if (_highlighted >= 0)
            _list.SelectedItem = _rows[_highlighted];
        _list.ItemTapped += OnItemTapped;
        _list.SelectionChanging += OnListSelectionChanging;
        // A bottom pane's title is in its toolbar.
        _title = title is null || chrome.IsToolbar ? null : SkUiPopupParts.Title(title, style);
        _footer = SkUiPopupParts.Buttons(chrome, style, title, () => Request, ClearChoice);
        _appHeader = extras.CreateHeader();
        _appFooter = extras.CreateFooter();
        // Several choices: a "Select all" row above the rows (with the app's header, under it).
        if (_multiple && selectAllText is not null)
        {
            _selectAll = new SelectAllRow(this, selectAllText, style);
            _selectAll.Show(SelectAllState());
        }
        if (_title is null && _appHeader is null && _search is null && _selectAll is null && _appFooter is null && _footer is null)
        {
            Content = _list;
        }
        else
        {
            _column = new SkUiGrid();
            LayOutParts(searchUnderRows: false);
            Content = _column;
        }
        var first = _multiple ? (_initialChecked.Length > 0 ? _initialChecked[0] : -1) : _highlighted;
        if (first >= 0 && first < _rows.Length)
            _ = _list.ScrollToIndex(first, ScrollToPosition.Center, animated: false);
        // Several choices: the keyboard's cursor starts on the first checked row (the one scrolled to and focused).
        if (_multiple && first >= 0 && first < _rows.Length)
        {
            _highlighted = first;
            ShowCursor(first);
        }
    }

    /// <summary>The request this content completes (set before it is shown; cleared once it has gone).</summary>
    public SkUiPopupRequest? Request { get; private set; }

    /// <inheritdoc />
    public SkUiContentView Surface => this;

    /// <inheritdoc />
    public SkUiPopupRequest CreateRequest(View anchor, View? platformAnchor, in SkUiPopupLayout layout, in SkUiPopupRequestOptions options)
    {
        var chrome = options.Chrome;
        SkUiPopupRequest request = _multiple
            ? new SkUiMultiListPopupRequest(anchor, platformAnchor, this, layout, _texts, _initialChecked, indices => _apply!(indices), _itemEnabled)
            {
                Title = options.Title, OverlayColor = options.OverlayColor, IsAnimated = options.IsAnimated,
                RequiresAccept = chrome.RequiresAccept, AcceptText = chrome.AcceptText, CancelText = chrome.CancelText
            }
            : new SkUiListPopupRequest(anchor, platformAnchor, this, layout, _texts, _initialSelected, _itemEnabled, _choose)
            {
                Title = options.Title, OverlayColor = options.OverlayColor, IsAnimated = options.IsAnimated,
                RequiresAccept = chrome.RequiresAccept, AcceptText = chrome.AcceptText, CancelText = chrome.CancelText
            };
        Request = request;
        _opened = layout;
        request.AvailableAreaChanged = FitBounds;
        // An anchored popup with a search resizes as it filters, against its anchor: opened above, the search field goes under the
        // rows, next to the anchor, where it stays as the popup's top edge moves.
        if (layout.Placement == SkUiPopupPlacement.Anchored && _search is not null)
        {
            _filterResizes = true;
            if (layout.OpensAbove)
                LayOutParts(_searchUnderRows = true);
        }
        // Resized, the drawn content stays where the popup's edge does until it is laid out to the new size.
        SurfaceKeepsBottom = layout.OpensAbove || layout.Placement == SkUiPopupPlacement.BottomPane;
        // Handed to the request, which lets go of it when it completes: a presenter may keep this content, and it must not keep the picker.
        _choose = null;
        return request;
    }

    /// <summary>The list (tests).</summary>
    internal SkUiCollectionView List => _list;

    /// <summary>The highlighted item: the selection, then the keyboard's position (tests).</summary>
    internal int HighlightedIndex => _highlighted;

    /// <summary>
    /// The size the content wants: as wide as its widest text (the first items') or templated row, at least
    /// <paramref name="minimumWidth"/>, at most <paramref name="maximumWidth"/>, and as tall as all its rows and its title.
    /// Templated rows are measured one by one (at the popup's width) until they are taller than <paramref name="maximumHeight"/>
    /// (past it the list scrolls) or <see cref="MeasuredTemplatedRows"/> were; the rest count as tall as their average.
    /// </summary>
    public Size PreferredSize(double minimumWidth, double maximumWidth, double maximumHeight)
    {
        var look = SkUiLook.Current;
        var padding = look.PopupItemPadding;
        var widest = 0d;
        var measured = Math.Min(_texts.Count, MeasuredItems);
        if (measured > 0)
        {
            var layout = new SkUiTextLayout();
            var textStyle = new SkUiTextStyle(SkUiTypefaces.Resolve(_style.FontFamily, _style.FontAttributes),
                SkUiFontScaling.ScaleFontSize(_style.FontSize, _style.FontAutoScalingEnabled), LineBreakMode.NoWrap,
                CharacterSpacing: _style.CharacterSpacing, FontAttributes: _style.FontAttributes);
            for (var index = 0; index < measured; index++)
            {
                widest = Math.Max(widest, layout.Measure(_texts[index] ?? string.Empty, textStyle, default, double.PositiveInfinity).Width);
                layout.Invalidate();
            }
            layout.Release();
        }
        var width = Math.Ceiling(widest + padding.HorizontalThickness + (_multiple ? CheckColumnWidth : 0));
        double rowsHeight;
        if (_template is not null && _rows.Length > 0)
        {
            // Templated rows: each as tall as it is (a wrapped line, an optional one). At least as wide as the widest of the measured
            // rows wants (its own padding included), then as tall as they are at the popup's width (narrower rows wrap more).
            var probe = Probe();
            var rows = Math.Min(_rows.Length, MeasuredTemplatedRows);
            var tall = 0d;
            var count = 0;
            for (; count < rows && tall <= maximumHeight; count++)
            {
                probe.BindingContext = _rows[count];
                var size = probe.Measure(maximumWidth, double.PositiveInfinity);
                width = Math.Max(width, Math.Ceiling(size.Width));
                tall += size.Height;
            }
            probe.BindingContext = null;
            width = Math.Clamp(width, Math.Min(minimumWidth, maximumWidth), maximumWidth);
            rowsHeight = RowsHeight(_rows, width, maximumHeight, count);
        }
        else
        {
            width = Math.Clamp(width, Math.Min(minimumWidth, maximumWidth), maximumWidth);
            rowsHeight = RowsHeight(_rows, width, maximumHeight);
        }
        // As tall as every row: an anchored popup shrinks to the rows a search shows (FitBounds) when its presenter follows it.
        return new Size(width, rowsHeight + PartsHeight(width));
    }

    /// <summary>
    /// How tall <paramref name="rows"/> are at <paramref name="width"/>: plain rows the look's height each; templated ones measured
    /// one by one until they are taller than <paramref name="maximumHeight"/> or <paramref name="limit"/> were, the rest as tall as
    /// their average.
    /// </summary>
    private double RowsHeight(IReadOnlyList<Row> rows, double width, double maximumHeight, int limit = MeasuredTemplatedRows)
    {
        if (_template is null)
            return rows.Count * SkUiLook.Current.PopupItemHeight;
        if (rows.Count == 0)
            return 0;
        var probe = Probe();
        var tall = 0d;
        var measured = 0;
        for (var last = Math.Min(rows.Count, limit); measured < last && tall <= maximumHeight; measured++)
        {
            probe.BindingContext = rows[measured];
            tall += probe.Measure(width, double.PositiveInfinity).Height;
        }
        probe.BindingContext = null;
        return tall + (rows.Count - measured) * (tall / measured);
    }

    /// <summary>The row that measures templated rows (bound to each in turn).</summary>
    private RowView Probe() => _probe ??= new RowView(_template, _style, _multiple);

    /// <summary>The height of everything but the rows at <paramref name="width"/>: the title, header, search field, footer and buttons.</summary>
    private double PartsHeight(double width) => SkUiPopupParts.ExtraHeight(width, _title, _appHeader, _search, _selectAll, _appFooter, _footer);

    /// <summary>
    /// The parts in a column: a bottom pane's toolbar, the title, the app's header, the search field, then "Select all" (it chooses
    /// the rows the search shows), the rows, the app's footer and the buttons. <paramref name="searchUnderRows"/> (opened above the
    /// anchor): the search field goes right under the rows.
    /// </summary>
    private void LayOutParts(bool searchUnderRows)
    {
        var toolbar = _chrome.IsToolbar;
        SkUiPopupParts.Column(_column!, _list, toolbar ? _footer : null, _title, _appHeader, searchUnderRows ? null : _search, _selectAll, _list,
            searchUnderRows ? _search : null, _appFooter, toolbar ? null : _footer);
    }

    /// <summary>
    /// A popup whose presenter follows its bounds (<see cref="SkUiPopupRequest.BoundsChanged"/>) fits into the room the presenter
    /// reports (<see cref="SkUiPopupRequest.AvailableArea"/>: the window less the soft keyboard) with
    /// <see cref="SkUiPopupGeometry.Fitted"/>; an anchored one with a search is also as tall as the rows it shows (the "No results"
    /// text for none) and the other parts, never taller than it opened, against its anchor. Resizing against its bottom edge (above
    /// the anchor, on the room's bottom), the search field is under the rows, where it stays.
    /// </summary>
    private void FitBounds()
    {
        if (Request is not { FollowsBounds: true } request)
            return;
        var width = _opened.Bounds.Width;
        var parts = PartsHeight(width);
        var height = _opened.Bounds.Height;
        if (_filterResizes)
            height = parts + (_shown.Count > 0 ? RowsHeight(_shown, width, height) : _noResults!.Measure(width, double.PositiveInfinity).Height);
        var available = request.AvailableArea;
        var bounds = SkUiPopupGeometry.Fitted(_opened, height, available, parts + SkUiLook.Current.PopupItemHeight, out var bottomStays);
        if (_search is not null && _opened.Placement == SkUiPopupPlacement.Anchored && bottomStays != _searchUnderRows)
            LayOutParts(_searchUnderRows = bottomStays);
        // A bottom pane over the soft keyboard: no room kept for the home indicator the keyboard covers.
        if (_opened.Placement == SkUiPopupPlacement.BottomPane)
            Padding = SkUiPopupGeometry.PaneContentPadding(_opened.SafeAreaInsets, _opened.WindowSize, available);
        // Committed with the next frame, after a resize: set while the popup stays put, so it is in place for the next resize.
        SurfaceKeepsBottom = bottomStays || _opened.Placement == SkUiPopupPlacement.BottomPane;
        if (bounds == request.Bounds)
            return;
        SkUiPopupParts.Fit(this, bounds, request.CornerRadius);
        request.MoveTo(bounds);
    }

    /// <summary>A tap would select or toggle a disabled row: the list keeps its selection.</summary>
    private void OnListSelectionChanging(object? sender, SkUiSelectionChangingEventArgs args)
    {
        if (_applying)
            return;
        if (!_multiple)
        {
            if (args.CurrentItem is Row { IsEnabled: false })
                args.Cancel = true;
            return;
        }
        // The toggled row: in one selection and not in the other.
        foreach (var item in args.CurrentSelection.Except(args.PreviousSelection).Concat(args.PreviousSelection.Except(args.CurrentSelection)))
            if (item is Row { IsEnabled: false })
                args.Cancel = true;
    }

    private void OnItemTapped(object? sender, SkUiItemTappedEventArgs args)
    {
        if (args.Item is not Row { IsEnabled: true } row)
            return;
        if (_multiple)
            _highlighted = row.Index; // the list toggled the row; the keyboard goes on from it
        else if (_chrome.RequiresAccept)
            Highlight(row.Index); // the list selected it; OK (or Enter) chooses it
        else
            Choose(row.Index);
    }

    /// <summary>The list changed its chosen rows (a tap, or <see cref="ToggleRow"/>): the picker takes them at once.</summary>
    private void OnListSelectionChanged(object? sender, SkUiSelectionChangedEventArgs args)
    {
        if (!_applying)
            ApplyListSelection();
    }

    /// <summary>The list's chosen rows, and the checked rows the search hides, go to the request (applied at once, or the draft).</summary>
    private void ApplyListSelection()
    {
        if (Request is not SkUiSelectionPopupRequest request)
            return;
        var listed = _list.SelectedItems.OfType<Row>().ToHashSet();
        var chosen = new List<int>();
        foreach (var row in _rows)
            if (row.Position < 0 ? row.IsChecked : listed.Contains(row))
                chosen.Add(row.Index);
        ShowChecks(chosen);
        _applying = true;
        try
        {
            request.SetSelection(chosen);
        }
        finally
        {
            _applying = false;
        }
        // A combo box that adds one item at a time (each change already applied).
        if (_closesOnToggle && !request.RequiresAccept)
            request.Close();
    }

    /// <summary>"Select all": checked when every shown row that can be chosen is, unchecked when none is, else in between.</summary>
    private SkUiCheckState SelectAllState()
    {
        var enabled = 0;
        var checkedRows = 0;
        foreach (var row in _shown)
        {
            if (!row.IsEnabled)
                continue;
            enabled++;
            if (row.IsChecked)
                checkedRows++;
        }
        return checkedRows == 0 ? SkUiCheckState.Unchecked : checkedRows == enabled ? SkUiCheckState.Checked : SkUiCheckState.Indeterminate;
    }

    /// <summary>
    /// "Select all" tapped: every shown row (those the search found) that can be chosen is chosen, or (all were) none is; one
    /// change of the selection.
    /// </summary>
    private void ToggleAll()
    {
        if (Request is not { } request || request.CloseRequested.IsCancellationRequested)
            return;
        var choose = SelectAllState() != SkUiCheckState.Checked;
        _applying = true;
        try
        {
            foreach (var row in _shown)
            {
                if (!row.IsEnabled || row.IsChecked == choose)
                    continue;
                if (choose)
                    _list.SelectedItems.Add(row);
                else
                    _list.SelectedItems.Remove(row);
            }
        }
        finally
        {
            _applying = false;
        }
        ApplyListSelection();
    }

    /// <summary>Shows exactly <paramref name="chosen"/> (ascending) as checked.</summary>
    private void ShowChecks(IReadOnlyList<int> chosen)
    {
        var next = 0;
        for (var index = 0; index < _rows.Length; index++)
        {
            while (next < chosen.Count && chosen[next] < index)
                next++;
            _rows[index].IsChecked = next < chosen.Count && chosen[next] == index;
        }
        _selectAll?.Show(SelectAllState());
    }

    private void ToggleRow(int index)
    {
        if (index < 0 || index >= _rows.Length || _rows[index] is not { IsEnabled: true, Position: >= 0 } row)
            return;
        if (!_list.SelectedItems.Remove(row))
            _list.SelectedItems.Add(row);
    }

    /// <summary>Completes the request with <paramref name="index"/> (the presenter then closes the popup).</summary>
    private void Choose(int index)
    {
        if (index >= 0 && index < _rows.Length)
            (Request as SkUiListPopupRequest)?.Complete(index);
    }

    /// <inheritdoc />
    public bool OnKeyDown(in SkUiKeyEvent key)
    {
        if (Request is not { } request || request.CloseRequested.IsCancellationRequested)
            return false;
        // The keys move through the rows the list shows (the search's matches); the highlight stays an item's index.
        var count = _shown.Count;
        var at = HighlightedPosition;
        // Without a search the rows are the items, and their states the picker's.
        var states = _phrase.Length == 0 ? _itemEnabled : _shown.Select(static row => row.IsEnabled).ToArray();
        switch (key.Key)
        {
            case SkUiKey.Escape:
                request.Cancel();
                return true;
            case SkUiKey.Enter when _multiple:
                request.Accept();
                return true;
            case SkUiKey.Space when _multiple && !key.IsRepeat:
                // Without a cursor yet, Space puts it on the first row that can be chosen (Space again toggles it).
                if (at < 0)
                {
                    if (SkUiItemStates.Nearest(states, count, 0, 1) is var firstEnabled and >= 0)
                        Highlight(_shown[firstEnabled].Index);
                    return count > 0;
                }
                ToggleRow(_highlighted);
                return true;
            case SkUiKey.Enter or SkUiKey.Space when !key.IsRepeat:
                if (at >= 0)
                    Choose(_highlighted);
                else
                    request.Close();
                return true;
            case SkUiKey.Tab:
                return true; // focus stays in the popup
            case SkUiKey.Character when _search is not null && !key.HasCommandModifier:
                // Typing searches: the search field takes the character and the keys that follow.
                _search.Text = _search.Text + key.Character;
                _search.CursorPosition = _search.Text.Length;
                _search.Focus();
                return true;
        }
        if (count == 0 || key.HasCommandModifier)
            return false;
        var page = Math.Max(1, (int)(_list.Height / SkUiLook.Current.PopupItemHeight) - 1);
        var target = key.Key switch
        {
            SkUiKey.Down => at + 1,
            SkUiKey.Up => at < 0 ? 0 : at - 1,
            SkUiKey.PageDown => at + page,
            SkUiKey.PageUp => at - page,
            SkUiKey.Home => 0,
            SkUiKey.End => count - 1,
            SkUiKey.Character => SkUiTypeAhead.Next(_texts, _highlighted, key.Character, ref _typeAhead, _itemEnabled),
            _ => int.MinValue
        };
        if (target == int.MinValue)
            return false;
        // Disabled rows are passed by in the key's direction.
        var step = key.Key is SkUiKey.Up or SkUiKey.PageUp or SkUiKey.End ? -1 : 1;
        if (SkUiItemStates.Nearest(states, count, target, step) is var enabled and >= 0)
            Highlight(_shown[enabled].Index);
        return true;
    }

    /// <summary>Where the highlighted row is in the list; -1 without one, or while the search hides it.</summary>
    private int HighlightedPosition => _highlighted >= 0 && _highlighted < _rows.Length ? _rows[_highlighted].Position : -1;

    /// <summary>
    /// A key in the search field: Up / Down and Page Up / Page Down move the list's highlight (the field keeps the focus), and
    /// with several choices Enter accepts; other keys edit the phrase.
    /// </summary>
    private bool OnSearchKeyDown(in SkUiKeyEvent key) =>
        (key.Key is SkUiKey.Up or SkUiKey.Down or SkUiKey.PageUp or SkUiKey.PageDown || key.Key == SkUiKey.Enter && _multiple) && OnKeyDown(key);

    /// <summary>The search field's Enter (the keyboard's search key): one choice takes the highlighted row, else the first match.</summary>
    private void OnSearchButtonPressed(object? sender, EventArgs args)
    {
        if (_multiple || Request is not { } request || request.CloseRequested.IsCancellationRequested)
            return;
        if (HighlightedPosition >= 0)
            Choose(_highlighted);
        else if (_phrase.Length > 0 && _shown.FirstOrDefault(static row => row.IsEnabled) is { } first)
            Choose(first.Index);
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs args) => Filter(args.NewTextValue);

    /// <summary>
    /// Shows the rows that match <paramref name="phrase"/> (all of them for an empty one), in the items' order. The list's selection
    /// follows: the highlight while it shows, the checked rows that show (hidden ones stay checked).
    /// </summary>
    internal void Filter(string? phrase)
    {
        phrase = phrase?.Trim() ?? string.Empty;
        if (phrase == _phrase)
            return;
        _phrase = phrase;
        var shown = new List<Row>(phrase.Length == 0 ? _rows.Length : 16);
        foreach (var row in _rows)
        {
            var matches = phrase.Length == 0 || (_filter is { } filter ? filter(row.Item, phrase) : Contains(row.Text, phrase));
            row.Position = matches ? shown.Count : -1;
            if (matches)
                shown.Add(row);
        }
        _shown = shown;
        _applying = true;
        try
        {
            // A new source drops the selected rows it lacks; the checked rows that show again are selected again.
            _list.ItemsSource = shown;
            if (_multiple)
            {
                var listed = _list.SelectedItems.OfType<Row>().ToHashSet();
                foreach (var row in shown)
                    if (row.IsChecked && !listed.Contains(row))
                        _list.SelectedItems.Add(row);
            }
            else
            {
                _list.SelectedItem = HighlightedPosition >= 0 ? _rows[_highlighted] : null;
            }
        }
        finally
        {
            _applying = false;
        }
        _selectAll?.Show(SelectAllState());
        FitBounds();
        if (shown.Count > 0)
            _ = _list.ScrollToIndex(Math.Max(0, HighlightedPosition), ScrollToPosition.MakeVisible, animated: false);
    }

    /// <summary>The default match: <paramref name="text"/> contains <paramref name="phrase"/>, ignoring case and accents ("zurich" finds "Zürich").</summary>
    internal static bool Contains(string text, string phrase) =>
        CultureInfo.CurrentCulture.CompareInfo.IndexOf(text, phrase, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    /// <summary>The search field (tests).</summary>
    internal SkUiSearchBar? SearchBar => _search;

    /// <summary>The rows the list shows, as the items' indices (tests).</summary>
    internal IEnumerable<int> ShownIndices => _shown.Select(static row => row.Index);

    /// <summary>The Clear button: no row chosen (several choices: through the list, so the request follows), no highlight.</summary>
    internal void ClearChoice()
    {
        if (Request is not { } request || request.CloseRequested.IsCancellationRequested)
            return;
        if (_multiple)
        {
            // Disabled rows the app chose stay chosen. Rows the search hides are unchecked first, so they go with the first change.
            var hidden = false;
            foreach (var row in _rows)
                if (row is { Position: < 0, IsChecked: true, IsEnabled: true })
                    (row.IsChecked, hidden) = (false, true);
            var shown = _list.SelectedItems.OfType<Row>().Where(row => row.IsEnabled).ToList();
            foreach (var row in shown)
                _list.SelectedItems.Remove(row);
            if (hidden && shown.Count == 0)
                ApplyListSelection();
            return;
        }
        _highlighted = -1;
        _list.SelectedItem = null;
        request.Clear();
    }

    /// <summary>The row at the keyboard's cursor (several choices) shows it.</summary>
    private void ShowCursor(int index)
    {
        for (var row = 0; row < _rows.Length; row++)
            _rows[row].IsCursor = row == index;
    }

    /// <summary>Moves the highlight to <paramref name="index"/> and scrolls it into view (no choice yet).</summary>
    internal void Highlight(int index)
    {
        if (index < 0 || index >= _rows.Length || !_rows[index].IsEnabled)
            return;
        (Request as SkUiListPopupRequest)?.SetCurrentIndex(index);
        if (index == _highlighted)
            return;
        _highlighted = index;
        // A row the search hides is highlighted once it shows again.
        var position = _rows[index].Position;
        if (_multiple)
            ShowCursor(index);
        else
            _list.SelectedItem = position >= 0 ? _rows[index] : null;
        if (position >= 0)
            _ = _list.ScrollToIndex(position, ScrollToPosition.MakeVisible, animated: false);
    }

    /// <summary>
    /// The picker's selection changed while the popup is open (the app set it): the highlight follows, so Enter chooses what
    /// the field shows; -1 clears it.
    /// </summary>
    public void SelectionChanged(int index)
    {
        if (_multiple)
        {
            SyncChecks();
            return;
        }
        if (index >= 0 && index < _rows.Length)
        {
            Highlight(index);
            return;
        }
        _highlighted = -1;
        _list.SelectedItem = null;
        (Request as SkUiListPopupRequest)?.SetCurrentIndex(-1);
    }

    /// <inheritdoc />
    public void ItemStatesChanged(int column, IReadOnlyList<bool>? states)
    {
        _itemEnabled = states;
        for (var index = 0; index < _rows.Length; index++)
            _rows[index].IsEnabled = SkUiItemStates.IsEnabled(states, index);
        _selectAll?.Show(SelectAllState());
        switch (Request)
        {
            case SkUiListPopupRequest list:
                list.SetItemStates(states);
                break;
            case SkUiMultiListPopupRequest several:
                several.SetItemStates(states);
                break;
        }
    }

    /// <summary>The picker's chosen rows changed while open (the app set them): the checks follow.</summary>
    private void SyncChecks()
    {
        if (_applying || _currentSelection is null)
            return;
        var chosen = _currentSelection();
        _applying = true;
        try
        {
            ShowChecks(chosen);
            _list.SelectedItems.Clear();
            foreach (var index in chosen)
                if (index >= 0 && index < _rows.Length && _rows[index].Position >= 0)
                    _list.SelectedItems.Add(_rows[index]);
            (Request as SkUiSelectionPopupRequest)?.Refresh(chosen);
        }
        finally
        {
            _applying = false;
        }
    }

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    /// <inheritdoc />
    internal override bool ShowsFocusRing => false;

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is null || _focusedOnce)
            return;
        _focusedOnce = true;
        // Shown: keys go to the list (the field's surface loses them), and screen readers start at the selection.
        Dispatcher.Dispatch(() =>
        {
            if (Request is null)
                return;
            Focus();
            var focusRow = _multiple ? (_initialChecked.Length > 0 ? _initialChecked[0] : -1) : _highlighted;
            if (focusRow >= 0 && _list.GetRealizedView(focusRow) is SkUiView row)
                row.SetSemanticFocus();
        });
    }

    /// <summary>The popup has gone: lets go of the items and the request.</summary>
    public void Release()
    {
        Request = null;
        _list.ItemTapped -= OnItemTapped;
        _list.SelectionChanged -= OnListSelectionChanged;
        _list.SelectionChanging -= OnListSelectionChanging;
        _list.ItemsSource = null;
        _rows = [];
        _shown = [];
        if (_search is null)
            return;
        _search.TextChanged -= OnSearchTextChanged;
        _search.SearchButtonPressed -= OnSearchButtonPressed;
        _search.KeyPreview = null;
    }

    /// <summary>The search field above the rows (under them when the popup opened above its anchor): a search bar in the picker's font, inset as the rows' text.</summary>
    private static SkUiSearchBar CreateSearchField(string placeholder, in SkUiPickerRowStyle style)
    {
        var padding = SkUiLook.Current.PopupItemPadding;
        return new SkUiSearchBar
        {
            Placeholder = placeholder,
            FontFamily = style.FontFamily,
            FontSize = style.FontSize,
            FontAutoScalingEnabled = style.FontAutoScalingEnabled,
            TextColor = style.TextColor ?? SkUiColors.DefaultForeground,
            Margin = new Thickness(padding.Left, 8, padding.Right, 4)
        };
    }

    /// <summary>What the list shows when nothing matches the search.</summary>
    private static SkUiLabel NoResults(in SkUiPickerRowStyle style) => new()
    {
        Text = SkUiStrings.Current.NoResultsText,
        FontFamily = style.FontFamily,
        FontSize = style.FontSize,
        FontAutoScalingEnabled = style.FontAutoScalingEnabled,
        TextColor = SkUiColors.Muted,
        Padding = SkUiLook.Current.PopupItemPadding,
        HorizontalTextAlignment = TextAlignment.Center
    };

    /// <summary>
    /// The "Select all" row of a list with several choices: a check box (checked, unchecked, or in between when some rows are chosen)
    /// and its text; a tap chooses every row that can be chosen, or none when all are. A check box for screen readers.
    /// </summary>
    private sealed class SelectAllRow : SkUiContentView
    {
        private readonly SkUiPickerPopup _owner;
        private readonly SkUiCheckBox _check;
        private readonly string _text;

        public SelectAllRow(SkUiPickerPopup owner, string text, SkUiPickerRowStyle style)
        {
            _owner = owner;
            _text = text;
            var padding = SkUiLook.Current.PopupItemPadding;
            _check = new SkUiCheckBox { InputTransparent = true, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(padding.Left, 0, 12, 0) };
            AutomationProperties.SetIsInAccessibleTree(_check, false);
            var label = new SkUiLabel
            {
                Text = text,
                FontFamily = style.FontFamily,
                FontSize = style.FontSize,
                FontAttributes = style.FontAttributes,
                FontAutoScalingEnabled = style.FontAutoScalingEnabled,
                TextColor = style.TextColor ?? SkUiColors.DefaultForeground,
                Padding = new Thickness(0, padding.Top, padding.Right, padding.Bottom),
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
            };
            AutomationProperties.SetIsInAccessibleTree(label, false);
            var grid = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)], MinimumHeightRequest = SkUiLook.Current.PopupItemHeight };
            grid.Children.Add(_check);
            grid.Children.Add(label);
            Grid.SetColumn(label, 1);
            Content = grid;
        }

        /// <summary>The check box's state (tests).</summary>
        public SkUiCheckState State => _check.CheckState;

        public void Show(SkUiCheckState state)
        {
            _check.CheckState = state;
            InvalidateSemantics();
        }

        protected override bool HandlesTap => true;

        protected override void OnTapped(SkUiTappedEventArgs args)
        {
            RaiseTapped(args);
            _owner.ToggleAll();
        }

        protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
        {
            base.OnPopulateSemantics(info);
            info.Role = SkUiSemanticsRole.CheckBox;
            info.Text = _text;
            info.CheckState = _check.CheckState;
        }
    }

    /// <summary>
    /// Whether the app's visual states for a template's <paramref name="root"/> have a <c>Disabled</c> state: then that state is
    /// the look of items that cannot be chosen, otherwise the popup dims them.
    /// </summary>
    internal static bool HasDisabledState(VisualElement root)
    {
        foreach (var group in VisualStateManager.GetVisualStateGroups(root))
            foreach (var state in group.States)
                if (state.Name == VisualStateManager.CommonStates.Disabled)
                    return true;
        return false;
    }

    /// <summary>The "Select all" row's state (tests); <c>null</c> without one.</summary>
    internal SkUiCheckState? SelectAllCheckState => _selectAll?.State;

    /// <summary>Taps "Select all" (tests).</summary>
    internal void TapSelectAll() => ToggleAll();

    /// <summary>One item of the list: its index, text and the object its template binds to; whether it is checked or at the keyboard's cursor.</summary>
    internal sealed class Row(int index, string text, object? item)
    {
        private bool _isChecked;
        private bool _isCursor;
        private bool _isEnabled = true;

        public int Index { get; } = index;
        public string Text { get; } = text ?? string.Empty;
        public object? Item { get; } = item;

        /// <summary>Where the list shows the row: its index, or with a search its place among the matches; -1 while the search hides it.</summary>
        public int Position { get; set; }

        /// <summary>Raised when <see cref="IsChecked"/> or <see cref="IsCursor"/> changed (the row's view shows it).</summary>
        public event EventHandler? StateChanged;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                    return;
                _isChecked = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Whether the row can be chosen (the picker's item states); disabled rows show dimmed.</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                    return;
                _isEnabled = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsCursor
        {
            get => _isCursor;
            set
            {
                if (_isCursor == value)
                    return;
                _isCursor = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public override string ToString() => Text;
    }

    /// <summary>
    /// A row: the item's text in the picker's font, or the picker's item template bound to the item (not to the row, so the
    /// app's bindings read its own objects); with several choices a check box before it. The list puts the row in the
    /// <c>Selected</c> visual state while it is selected (the highlight, or a chosen row); the row passes it on to the template's
    /// root, so the app's <c>VisualStateGroups</c> style selected rows as in a collection view.
    /// </summary>
    private sealed class RowView : SkUiContentView
    {
        private readonly DataTemplate? _template;
        private readonly SkUiLabel? _label;
        private readonly SkUiContentView? _slot;
        private readonly SkUiCheckBox? _check;
        private Row? _row;
        private DataTemplate? _shownTemplate;

        public RowView(DataTemplate? template, SkUiPickerRowStyle style, bool multiple)
        {
            _template = template;
            var look = SkUiLook.Current;
            if (template is null)
            {
                _label = new SkUiLabel
                {
                    FontFamily = style.FontFamily,
                    FontSize = style.FontSize,
                    FontAttributes = style.FontAttributes,
                    FontAutoScalingEnabled = style.FontAutoScalingEnabled,
                    CharacterSpacing = style.CharacterSpacing,
                    TextColor = style.TextColor ?? SkUiColors.DefaultForeground,
                    Padding = multiple ? new Thickness(0, look.PopupItemPadding.Top, look.PopupItemPadding.Right, look.PopupItemPadding.Bottom) : look.PopupItemPadding,
                    VerticalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1
                };
            }
            if (!multiple)
            {
                if (_label is not null)
                    Content = _label;
                return;
            }
            // Several choices: a check box (it takes no taps: the row does), then the row's content.
            _check = new SkUiCheckBox { InputTransparent = true, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(look.PopupItemPadding.Left, 0, 12, 0) };
            AutomationProperties.SetIsInAccessibleTree(_check, false);
            _slot = new SkUiContentView { Content = _label };
            var grid = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] };
            grid.Children.Add(_check);
            grid.Children.Add(_slot);
            Grid.SetColumn(_slot, 1);
            Content = grid;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (_row is not null)
                _row.StateChanged -= OnRowStateChanged;
            _row = BindingContext as Row;
            if (_row is not null)
                _row.StateChanged += OnRowStateChanged;
            var row = _row;
            if (_label is not null)
            {
                _label.Text = row?.Text ?? string.Empty;
                ShowState();
                return;
            }
            // A template selector picks per item: a new view when it picks another template.
            var template = _template is DataTemplateSelector selector && row is not null ? selector.SelectTemplate(row.Item, this) : _template;
            var host = _slot ?? this;
            if (!ReferenceEquals(template, _shownTemplate) || host.Content is null)
            {
                _shownTemplate = template;
                host.Content = template?.CreateContent() as ISkUiView;
            }
            if (host.Content is BindableObject content)
                content.BindingContext = row?.Item;
            ShowState();
            ForwardSelected();
        }

        /// <inheritdoc />
        protected override void ChangeVisualState()
        {
            base.ChangeVisualState();
            ForwardSelected();
        }

        /// <summary>The template's root follows this row's <c>Selected</c> state.</summary>
        private void ForwardSelected()
        {
            if (_template is not null && (_slot ?? this).Content is SkUiView root)
                root.IsSelectedItem = IsSelectedItem;
        }

        private void OnRowStateChanged(object? sender, EventArgs args) => ShowState();


        /// <summary>How much a disabled row shows (Material's disabled text).</summary>
        private const double DisabledOpacity = 0.38;

        private void ShowState()
        {
            // A disabled row: plain text is dimmed; a template's root goes to the Disabled state, and is dimmed unless the
            // app styles that state itself.
            var enabled = _row?.IsEnabled != false;
            IsEnabled = enabled;
            if (_label is not null)
                _label.Opacity = enabled ? 1 : DisabledOpacity;
            else
            {
                var host = _slot ?? this;
                host.Opacity = enabled || (host.Content is VisualElement root && HasDisabledState(root)) ? 1 : DisabledOpacity;
            }
            if (_check is null)
                return;
            _check.IsChecked = _row?.IsChecked == true;
            BackgroundColor = _row?.IsCursor == true ? SkUiColors.DefaultForeground.WithAlpha(0.08f) : Colors.Transparent;
        }
    }
}
