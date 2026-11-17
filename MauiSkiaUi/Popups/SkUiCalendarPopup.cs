namespace MauiSkiaUi;

/// <summary>
/// A date picker's popup content as a month calendar (<c>PopupStyle</c> <see cref="SkUiDatePickerPopupStyle.Calendar"/>): a
/// <see cref="SkUiCalendar"/> of one day under the title of window popups and the app's header, over the app's footer and the buttons.
/// A tap on a day chooses it and closes the popup; with <c>UpdateMode</c> <see cref="SkUiPickerUpdateMode.WhenFinished"/> it is a draft
/// that OK accepts. The request is the date picker's own (<see cref="SkUiDatePopupRequest"/>), so presenters with their own UI work as
/// with the wheels. With keyboard focus (it takes it when shown): the arrows, Page Up / Down and Home / End move the calendar's focus,
/// Enter or Space choose (and, waiting for OK, accept the day already chosen), Escape cancels. Holds the dates it was opened with,
/// never the picker.
/// </summary>
internal sealed class SkUiCalendarPopup : SkUiContentView, ISkUiKeyHandler, ISkUiPickerPopupContent
{
    private readonly SkUiDateTimeWheels _wheels;
    private readonly Action<IReadOnlyList<int>> _apply;
    private readonly Func<DateTime?> _current;
    private readonly SkUiCalendar _calendar;
    private readonly SkUiLabel? _title;
    private readonly SkUiView? _footer;
    private readonly SkUiView? _appHeader;
    private readonly SkUiView? _appFooter;
    private bool _showing;
    private bool _focusedOnce;

    /// <param name="wheels">The picker's date rules and value, as the wheels would show them (the request carries them).</param>
    /// <param name="setup">What the popup is built for.</param>
    /// <param name="calendarStyle">The app's style of the calendar (<c>CalendarStyle</c>), or <c>null</c>.</param>
    /// <param name="current">The picker's date now.</param>
    /// <param name="apply">Applies a selection of the wheels' indices to the picker.</param>
    public SkUiCalendarPopup(SkUiDateTimeWheels wheels, in SkUiPickerPopupSetup setup, Style? calendarStyle, Func<DateTime?> current,
        Action<IReadOnlyList<int>> apply)
    {
        _wheels = wheels;
        _current = current;
        _apply = apply;
        var style = setup.RowStyle;
        FlowDirection = setup.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        BackgroundColor = SkUiColors.DefaultBackground;
        AutomationProperties.SetIsInAccessibleTree(this, false);
        _calendar = new SkUiCalendar
        {
            SelectionMode = SkUiCalendarSelectionMode.Single,
            MinimumDate = wheels.Minimum,
            MaximumDate = wheels.Maximum,
            DateEnabled = wheels.IsDateEnabled,
            Margin = new Thickness(8, 4)
        };
        if (calendarStyle is not null)
            _calendar.Style = calendarStyle;
        else if (style.FontFamily is { } family)
            _calendar.FontFamily = family; // the picker's font, when the app does not style the calendar itself
        ShowValue(wheels.Value);
        _calendar.DisplayDate = wheels.Value ?? wheels.Highlighted;
        _calendar.DateTapped += OnDateTapped;
        _calendar.SelectionChanged += OnSelectionChanged;
        // A bottom pane's title is in its toolbar.
        _title = setup.Title is null || setup.Chrome.IsToolbar ? null : SkUiPopupParts.Title(setup.Title, style);
        _footer = SkUiPopupParts.Buttons(setup.Chrome, style, setup.Title, () => Request, () => Request?.Clear());
        _appHeader = setup.Extras.CreateHeader();
        _appFooter = setup.Extras.CreateFooter();
        Content = SkUiPopupParts.Compose(_title, _appHeader, _calendar, _appFooter, _footer, setup.Chrome.IsToolbar);
    }

    /// <summary>The request this content chooses through (set when it is created; cleared once it has gone).</summary>
    public SkUiDatePopupRequest? Request { get; private set; }

    /// <summary>The calendar (tests).</summary>
    internal SkUiCalendar Calendar => _calendar;

    /// <inheritdoc />
    public SkUiContentView Surface => this;

    /// <inheritdoc />
    public SkUiPopupRequest CreateRequest(View anchor, View? platformAnchor, in SkUiPopupLayout layout, in SkUiPopupRequestOptions options)
    {
        var request = (SkUiDatePopupRequest)_wheels.CreateRequest(anchor, platformAnchor, this, layout, options, _wheels.IndicesOf(_wheels.Value), _apply);
        var selected = request.SelectedIndices;
        for (var column = 0; column < request.Columns.Count; column++)
            request.SetItemStates(column, _wheels.ItemStates(column, selected));
        Request = request;
        return request;
    }

    /// <inheritdoc />
    public Size PreferredSize(double minimumWidth, double maximumWidth, double maximumHeight)
    {
        // As wide as the calendar wants (seven touch-sized columns and its margin), within the popup's widths.
        var natural = _calendar.Measure(double.PositiveInfinity, double.PositiveInfinity);
        var width = Math.Clamp(Math.Ceiling(natural.Width), Math.Min(minimumWidth, maximumWidth), maximumWidth);
        var height = _calendar.Measure(width, double.PositiveInfinity).Height + SkUiPopupParts.ExtraHeight(width, _title, _appHeader, _appFooter, _footer);
        return new Size(width, height);
    }

    /// <summary>A day was tapped (or chosen with the keyboard): without OK to wait for, the popup ends with it.</summary>
    private void OnDateTapped(object? sender, SkUiCalendarDateTappedEventArgs args)
    {
        if (_showing || !args.IsEnabled || args.DisplayMode != SkUiCalendarDisplayMode.Month || Request is not { RequiresAccept: false } request)
            return;
        request.CompleteDate(args.Date);
    }

    /// <summary>The calendar's day changed: waiting for OK, it is the draft.</summary>
    private void OnSelectionChanged(object? sender, SkUiCalendarSelectionChangedEventArgs args)
    {
        if (_showing || Request is not { RequiresAccept: true } request)
            return;
        // A day the request will not take (the app's rule changed while open) goes back to the draft.
        if (!request.SetDate(_calendar.SelectedDate))
            ShowValue(request.CurrentDate);
    }

    /// <summary>The calendar shows <paramref name="date"/> chosen, without that being the user's choice.</summary>
    private void ShowValue(DateTime? date)
    {
        _showing = true;
        try
        {
            _calendar.SelectedDate = date?.Date;
        }
        finally
        {
            _showing = false;
        }
    }

    /// <inheritdoc />
    /// <remarks>The calendar asks the picker's rules as it draws: the days follow when it is drawn again.</remarks>
    public void ItemStatesChanged(int column, IReadOnlyList<bool>? states)
    {
        Request?.SetItemStates(column, states);
        _calendar.RefreshDayStyles();
    }

    /// <summary>The picker's date changed while the popup is open (the app set it): the calendar shows it.</summary>
    public void SelectionChanged(int index)
    {
        var date = _current();
        Request?.Refresh(_wheels.IndicesOf(date));
        ShowValue(date);
        if (date is { } day)
            _calendar.GoToDate(day);
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
            case SkUiKey.Tab:
                return true; // focus stays in the popup
            // Waiting for OK: Enter on the day already chosen accepts it (the first Enter chose it).
            case SkUiKey.Enter or SkUiKey.Space when request.RequiresAccept && !key.IsRepeat && _calendar.SelectedDate is { } chosen
                && (_calendar.FocusDate ?? chosen) == chosen:
                request.Accept();
                return true;
        }
        return ((ISkUiKeyHandler)_calendar).OnKeyDown(key);
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
        // Shown: keys go to the calendar (the field's surface loses them).
        Dispatcher.Dispatch(() =>
        {
            if (Request is not null)
                Focus();
        });
    }

    /// <summary>The popup has gone: lets go of the request.</summary>
    public void Release()
    {
        Request = null;
        _calendar.DateTapped -= OnDateTapped;
        _calendar.SelectionChanged -= OnSelectionChanged;
    }
}
