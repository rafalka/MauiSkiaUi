namespace MauiSkiaUi;

/// <summary>
/// A multi-column picker's popup content: the wheels (<see cref="SkUiWheelsView"/>: one per column side by side behind a band that
/// marks the middle row), an optional title above (window popups), the app's header and footer and the buttons. Scrolling or tapping
/// a wheel chooses its item (<see cref="SkUiSelectionPopupRequest.SetSelection"/>: at once, or as a draft that the OK button below the
/// wheels accepts with <c>UpdateMode</c> <see cref="SkUiPickerUpdateMode.WhenFinished"/>); the popup stays open. With keyboard focus
/// (it takes it when shown): Enter or Space accept, Escape cancels, the other keys move the wheels. Holds the texts it was opened
/// with, never the picker. As a list's wheel (one column, <c>singleList</c>: a picker's list in a bottom pane on a look that shows it
/// as a wheel) it chooses through a <see cref="SkUiListPopupRequest"/> instead (<see cref="SkUiListPopupRequest.SetSelectedIndex"/>),
/// whose close button (×) takes the item in the middle row.
/// </summary>
internal sealed class SkUiColumnsPopup : SkUiContentView, ISkUiKeyHandler, ISkUiPickerPopupContent
{
    private readonly string[][] _texts;
    private readonly bool[] _loops;
    private readonly int[] _initial;
    private readonly Action<IReadOnlyList<int>> _apply;
    private readonly Func<IReadOnlyList<int>> _current;
    private readonly SkUiLabel? _title;
    private readonly SkUiView? _footer;
    private readonly SkUiView? _appHeader;
    private readonly SkUiView? _appFooter;
    private readonly SkUiWheelsView.ColumnParts[] _parts;
    private readonly ISkUiColumnsRules? _rules;
    private readonly bool _singleList;
    private readonly SkUiWheelsView _wheels;
    private bool _focusedOnce;

    public SkUiColumnsPopup(string[][] texts, bool[] loops, double[] widths, IReadOnlyList<int> selected, int rows, SkUiPickerRowStyle style, string? title,
        bool rightToLeft, SkUiPickerPopupChrome chrome, Action<IReadOnlyList<int>> apply, Func<IReadOnlyList<int>> current,
        IReadOnlyList<bool>?[]? states = null, SkUiWheelsView.ColumnParts[]? parts = null, SkUiPopupExtras extras = default, ISkUiColumnsRules? rules = null,
        IReadOnlyList<int>? positions = null, bool singleList = false)
    {
        _singleList = singleList && texts.Length == 1 && rules is null;
        _texts = texts;
        _loops = loops;
        _rules = rules;
        _parts = parts ?? new SkUiWheelsView.ColumnParts[texts.Length];
        _initial = [.. selected];
        _apply = apply;
        _current = current;
        FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        BackgroundColor = SkUiColors.DefaultBackground;
        AutomationProperties.SetIsInAccessibleTree(this, false);
        _wheels = new SkUiWheelsView(texts, loops, widths, selected, rows, style, rightToLeft, Chosen, Select, states, _parts, rules, positions,
            (column, columnStates) => (Request as SkUiColumnsPopupRequest)?.SetItemStates(column, columnStates));
        // A bottom pane's title is in its toolbar.
        _title = title is null || chrome.IsToolbar ? null : SkUiPopupParts.Title(title, style);
        _footer = SkUiPopupParts.Buttons(chrome, style, title, () => Request, ClearChoice);
        _appHeader = extras.CreateHeader();
        _appFooter = extras.CreateFooter();
        Content = SkUiPopupParts.Compose(_title, _appHeader, _wheels, _appFooter, _footer, chrome.IsToolbar);
    }

    /// <summary>The request this content changes the selection of (set when it is created; cleared once it has gone).</summary>
    public SkUiPopupRequest? Request { get; private set; }

    /// <summary>The wheel of a column (tests).</summary>
    internal SkUiCarouselView Wheel(int column) => _wheels.Wheel(column);

    /// <summary>The column the keyboard moves (tests).</summary>
    internal int ActiveColumn => _wheels.ActiveColumn;

    /// <inheritdoc />
    public SkUiContentView Surface => this;

    /// <inheritdoc />
    public SkUiPopupRequest CreateRequest(View anchor, View? platformAnchor, in SkUiPopupLayout layout, in SkUiPopupRequestOptions options)
    {
        var chrome = options.Chrome;
        if (_singleList)
        {
            var list = new SkUiListPopupRequest(anchor, platformAnchor, this, layout, _texts[0], _initial[0], _wheels.StatesOf(0), index => _apply([index]))
            {
                Title = options.Title,
                OverlayColor = options.OverlayColor,
                IsAnimated = options.IsAnimated,
                RequiresAccept = chrome.RequiresAccept,
                AcceptText = chrome.AcceptText,
                CancelText = chrome.CancelText
            };
            Request = list;
            // A wheel always shows an item in its middle row: the one the close button chooses, also when the picker has none chosen yet.
            list.SetCurrentIndex(_wheels.Shown(_initial)[0]);
            return list;
        }
        var request = _rules?.CreateRequest(anchor, platformAnchor, this, layout, options, _initial, indices => _apply(indices))
            ?? new SkUiColumnsPopupRequest(anchor, platformAnchor, this, layout, _texts, _loops, _initial, indices => _apply(indices),
                [.. _parts.Select(part => part.Header)])
            {
                Title = options.Title,
                OverlayColor = options.OverlayColor,
                IsAnimated = options.IsAnimated,
                RequiresAccept = chrome.RequiresAccept,
                AcceptText = chrome.AcceptText,
                CancelText = chrome.CancelText
            };
        for (var column = 0; column < _texts.Length; column++)
            request.SetItemStates(column, _wheels.StatesOf(column));
        // Waiting for OK: the draft is what the wheels show (OK chooses it), also in columns with nothing chosen yet.
        if (request.RequiresAccept)
            request.Refresh(_wheels.Shown(_initial));
        Request = request;
        return request;
    }

    /// <inheritdoc />
    /// <remarks>Whole: it neither scrolls nor shrinks, so with too little room beside its field it covers the field (issue #111).</remarks>
    public double MinimumHeight => double.PositiveInfinity;

    /// <inheritdoc />
    public Size PreferredSize(double minimumWidth, double maximumWidth, double maximumHeight)
    {
        var width = Math.Clamp(Math.Ceiling(_wheels.PreferredWidth()), Math.Min(minimumWidth, maximumWidth), maximumWidth);
        var height = _wheels.PreferredHeight(width) + SkUiPopupParts.ExtraHeight(width, _title, _appHeader, _appFooter, _footer);
        return new Size(width, height);
    }

    /// <summary>
    /// What the wheels have chosen, one index per column: the request's selection; for a list's wheel, the picker's selection while
    /// each choice applies at once, else the draft (the request's current item). <c>null</c> without a request.
    /// </summary>
    private IReadOnlyList<int>? Chosen() => Request switch
    {
        SkUiSelectionPopupRequest selection => selection.SelectedIndices,
        SkUiListPopupRequest list => list.RequiresAccept ? [list.CurrentIndex] : _current(),
        _ => null
    };

    /// <summary>Chooses <paramref name="indices"/> through the request (a list's wheel: its one index).</summary>
    private void Select(int[] indices)
    {
        if (Request is SkUiSelectionPopupRequest selection)
            selection.SetSelection(indices);
        else if (Request is SkUiListPopupRequest list && indices.Length == 1)
            list.SetSelectedIndex(indices[0]);
    }

    /// <summary>
    /// The Clear button. Wheels cannot show that nothing is chosen, so a list's wheel clears and closes, also when the popup waits
    /// for OK (as <see cref="SkUiColumnsPopupRequest"/>'s Clear).
    /// </summary>
    private void ClearChoice()
    {
        if (Request is SkUiListPopupRequest list)
        {
            if (list.Clear() && !list.CloseRequested.IsCancellationRequested)
                list.Accept();
            return;
        }
        Request?.Clear();
    }

    /// <inheritdoc />
    /// <remarks>A wheel whose item became disabled moves to the nearest earlier one that can be chosen (as a date's day in a shorter month).</remarks>
    public void ItemStatesChanged(int column, IReadOnlyList<bool>? states)
    {
        if (column < 0 || column >= _texts.Length)
            return;
        (Request as SkUiColumnsPopupRequest)?.SetItemStates(column, states);
        (Request as SkUiListPopupRequest)?.SetItemStates(states);
        _wheels.ItemStatesChanged(column, states);
    }

    /// <summary>The picker's selection changed while the popup is open (the app set it): the wheels follow.</summary>
    public void SelectionChanged(int index)
    {
        var chosen = _current();
        _wheels.ShowChoice(chosen);
        (Request as SkUiSelectionPopupRequest)?.Refresh(chosen);
        if (Request is SkUiListPopupRequest list && chosen.Count == 1 && chosen[0] >= 0)
            list.SetCurrentIndex(chosen[0]);
        if (_rules is not null && chosen.Count == _wheels.ColumnCount)
            _wheels.ApplyStates(_wheels.Shown(chosen));
    }

    /// <inheritdoc />
    public bool OnKeyDown(in SkUiKeyEvent key)
    {
        if (Request is not { } request || request.CloseRequested.IsCancellationRequested)
            return false;
        switch (key.Key)
        {
            case SkUiKey.Escape:
                request.Cancel();
                return true;
            case SkUiKey.Enter:
            case SkUiKey.Space when !key.IsRepeat:
                request.Accept();
                return true;
            case SkUiKey.Tab:
                return true; // focus stays in the popup
        }
        return _wheels.OnKeyDown(key);
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
        // Shown: keys go to the wheels (the field's surface loses them).
        Dispatcher.Dispatch(() =>
        {
            if (Request is not null)
                Focus();
        });
    }

    /// <summary>The popup has gone: lets go of the request and the wheels' items.</summary>
    public void Release()
    {
        Request = null;
        _wheels.Release();
    }
}
