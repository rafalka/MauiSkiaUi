namespace MauiSkiaUi;

/// <summary>
/// A date and time picker's popup content as a month calendar over the time wheels (<c>PopupStyle</c>
/// <see cref="SkUiDatePickerPopupStyle.Calendar"/>, 1.15): a <see cref="SkUiCalendar"/> of one day, then the picker's time columns
/// (hour, minute, seconds, AM / PM) as wheels, under the title of window popups and the app's header, over the app's footer and the
/// buttons. As the wheel popup, it follows <c>UpdateMode</c>: each change (a day, a wheel) applies at once and the popup stays open,
/// or the changes are a draft that OK accepts. A tap on a day never closes it: the time is still to choose.
/// </summary>
/// <remarks>
/// The request is the date picker's own (<see cref="SkUiDatePopupRequest"/> with <see cref="SkUiDatePopupRequest.IncludesTime"/>),
/// built from all the picker's wheels, so presenters with their own UI work as with the wheels. The time wheels are built for the
/// chosen day from the same rules with only the time columns, and built again when the day changes, so the range's first and last
/// days offer only the times in the range. Keys: Tab moves between the calendar and the wheels; Enter or Space accept (waiting
/// for OK) or close; Escape cancels. Holds the dates it was opened with, never the picker.
/// </remarks>
internal sealed class SkUiDateTimeCalendarPopup : SkUiContentView, ISkUiKeyHandler, ISkUiPickerPopupContent
{
    /// <summary>The rows the time wheels show (fewer than the wheel popup's, under a calendar).</summary>
    private const int TimeRows = 3;

    private readonly SkUiDateTimeWheels _wheels;
    private readonly SkUiDateTimeSettings _timeSettings;
    private readonly Action<IReadOnlyList<int>> _apply;
    private readonly Func<DateTime?> _current;
    private readonly SkUiPickerRowStyle _style;
    private readonly bool _rightToLeft;
    private readonly SkUiCalendar _calendar;
    private readonly SkUiContentView _timeHost;
    private readonly SkUiLabel? _title;
    private readonly SkUiView? _footer;
    private readonly SkUiView? _appHeader;
    private readonly SkUiView? _appFooter;
    private SkUiDateTimeWheels _time = null!;
    private SkUiWheelsView? _timeView;
    private int[] _timeIndices = [];
    private DateTime _draft;
    private bool _showing;
    private bool _committing;
    private bool _timeFocused;
    private bool _focusedOnce;

    /// <param name="wheels">The picker's rules and value as all its wheels would show them (the request carries them).</param>
    /// <param name="settings">What <paramref name="wheels"/> were built from (the time wheels take the same rules).</param>
    /// <param name="setup">What the popup is built for.</param>
    /// <param name="calendarStyle">The app's style of the calendar (<c>CalendarStyle</c>), or <c>null</c>.</param>
    /// <param name="current">The picker's value now.</param>
    /// <param name="apply">Applies a selection of the wheels' indices to the picker.</param>
    public SkUiDateTimeCalendarPopup(SkUiDateTimeWheels wheels, in SkUiDateTimeSettings settings, in SkUiPickerPopupSetup setup, Style? calendarStyle,
        Func<DateTime?> current, Action<IReadOnlyList<int>> apply)
    {
        _wheels = wheels;
        _current = current;
        _apply = apply;
        _style = setup.RowStyle;
        _rightToLeft = setup.RightToLeft;
        // The same rules with only the time columns: the date of a value comes from the value itself (the chosen day).
        _timeSettings = settings with { WheelFormat = TimeFormat(wheels), BlackoutDates = null, DateEnabled = null, ShowsHeaders = false };
        FlowDirection = _rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        BackgroundColor = SkUiColors.DefaultBackground;
        AutomationProperties.SetIsInAccessibleTree(this, false);
        _calendar = new SkUiCalendar
        {
            SelectionMode = SkUiCalendarSelectionMode.Single,
            MinimumDate = wheels.Minimum.Date,
            MaximumDate = wheels.Maximum.Date,
            DateEnabled = wheels.IsDateEnabled,
            Margin = new Thickness(8, 4, 8, 0)
        };
        if (calendarStyle is not null)
            _calendar.Style = calendarStyle;
        else if (_style.FontFamily is { } family)
            _calendar.FontFamily = family;
        _timeHost = new SkUiContentView { Margin = new Thickness(8, 0, 8, 4) };
        Show(wheels.Value ?? wheels.Highlighted);
        _calendar.DisplayDate = _draft;
        _calendar.SelectionChanged += OnSelectionChanged;
        var body = new SkUiVerticalStackLayout { Spacing = 0 };
        body.Children.Add(_calendar);
        body.Children.Add(_timeHost);
        _title = setup.Title is null || setup.Chrome.IsToolbar ? null : SkUiPopupParts.Title(setup.Title, _style);
        _footer = SkUiPopupParts.Buttons(setup.Chrome, _style, setup.Title, () => Request, () => Request?.Clear());
        _appHeader = setup.Extras.CreateHeader();
        _appFooter = setup.Extras.CreateFooter();
        Content = SkUiPopupParts.Compose(_title, _appHeader, body, _appFooter, _footer, setup.Chrome.IsToolbar);
    }

    /// <summary>The request this content chooses through (set when it is created; cleared once it has gone).</summary>
    public SkUiDatePopupRequest? Request { get; private set; }

    /// <summary>The calendar (tests).</summary>
    internal SkUiCalendar Calendar => _calendar;

    /// <summary>The time wheels (tests).</summary>
    internal SkUiWheelsView? TimeWheels => _timeView;

    /// <summary>The time columns' texts (tests).</summary>
    internal string[][] TimeTexts => _time.Texts;

    /// <summary>What the popup shows chosen now, day and time (tests).</summary>
    internal DateTime Draft => _draft;

    /// <inheritdoc />
    public SkUiContentView Surface => this;

    /// <summary>
    /// The time columns of <paramref name="wheels"/>, in their order, as a wheel format: the 12- or 24-hour hour, minutes,
    /// seconds and AM / PM the picker shows.
    /// </summary>
    internal static string TimeFormat(SkUiDateTimeWheels wheels)
    {
        var parts = new List<string>(4);
        foreach (var column in wheels.Columns)
        {
            switch (column.Unit)
            {
                case SkUiDateTimeUnit.Hour:
                    parts.Add(wheels.Is24Hour ? "HH" : "h");
                    break;
                case SkUiDateTimeUnit.Minute:
                    parts.Add("mm");
                    break;
                case SkUiDateTimeUnit.Second:
                    parts.Add("ss");
                    break;
                case SkUiDateTimeUnit.Half:
                    parts.Add("tt");
                    break;
            }
        }
        // A wheel format without a time column (a date and time picker whose format shows only the date) keeps the culture's times.
        return parts.Count == 0 ? "t" : string.Join(' ', parts);
    }

    /// <inheritdoc />
    public SkUiPopupRequest CreateRequest(View anchor, View? platformAnchor, in SkUiPopupLayout layout, in SkUiPopupRequestOptions options)
    {
        var request = (SkUiDatePopupRequest)_wheels.CreateRequest(anchor, platformAnchor, this, layout, options, _wheels.IndicesOf(_wheels.Value), _apply);
        var selected = request.SelectedIndices;
        for (var column = 0; column < request.Columns.Count; column++)
            request.SetItemStates(column, _wheels.ItemStates(column, selected));
        // Waiting for OK: the draft is what the popup shows (OK chooses it), also with nothing chosen yet.
        if (request.RequiresAccept)
            request.Refresh(_wheels.IndicesOf(_draft));
        Request = request;
        return request;
    }

    /// <inheritdoc />
    /// <remarks>Whole: it neither scrolls nor shrinks, so with too little room beside its field it covers the field (issue #111).</remarks>
    public double MinimumHeight => double.PositiveInfinity;

    /// <inheritdoc />
    public Size PreferredSize(double minimumWidth, double maximumWidth, double maximumHeight)
    {
        var natural = _calendar.Measure(double.PositiveInfinity, double.PositiveInfinity);
        var timeWidth = (_timeView?.PreferredWidth() ?? 0) + _timeHost.Margin.HorizontalThickness;
        var width = Math.Clamp(Math.Ceiling(Math.Max(natural.Width, timeWidth)), Math.Min(minimumWidth, maximumWidth), maximumWidth);
        var timeHeight = (_timeView?.PreferredHeight(width - _timeHost.Margin.HorizontalThickness) ?? 0) + _timeHost.Margin.VerticalThickness;
        var height = _calendar.Measure(width, double.PositiveInfinity).Height + timeHeight
            + SkUiPopupParts.ExtraHeight(width, _title, _appHeader, _appFooter, _footer);
        return new Size(width, height);
    }

    /// <summary>
    /// Shows <paramref name="value"/> (kept in the range): its day chosen on the calendar and the time wheels built for that day at
    /// its time. Not the user's choice: nothing is applied.
    /// </summary>
    private void Show(DateTime value)
    {
        _time = SkUiDateTimeWheels.Create(_timeSettings with { Value = value, Highlighted = value }, System.Globalization.CultureInfo.CurrentCulture,
            SkUiStrings.Current);
        _draft = _time.Coerce(value);
        _timeIndices = _time.IndicesOf(_draft);
        _showing = true;
        try
        {
            _calendar.SelectedDate = _draft.Date;
        }
        finally
        {
            _showing = false;
        }
        _timeView?.Release();
        _timeView = new SkUiWheelsView(_time.Texts, _time.Loops, Widths(_time.Texts.Length), _timeIndices, TimeRows, _style, _rightToLeft,
            () => _timeIndices, OnTimeSelected, rules: _time);
        _timeView.ShowFocusRing(_timeFocused);
        _timeHost.Content = _timeView;
    }

    private static double[] Widths(int columns)
    {
        var widths = new double[columns];
        Array.Fill(widths, -1d);
        return widths;
    }

    /// <summary>The calendar's day changed: the time wheels are built for it, at the time chosen so far (kept in the range).</summary>
    private void OnSelectionChanged(object? sender, SkUiCalendarSelectionChangedEventArgs args)
    {
        if (_showing || _calendar.SelectedDate is not { } day)
            return;
        Show(day.Date + _draft.TimeOfDay);
        Commit();
    }

    /// <summary>A time wheel settled on an item.</summary>
    private void OnTimeSelected(int[] indices)
    {
        var coerced = _time.Coerce(indices);
        if (_time.ValueOf(coerced) is not { } value)
            return;
        _timeIndices = coerced;
        _draft = value;
        if (!coerced.AsSpan().SequenceEqual(indices))
            _timeView?.ShowChoice(coerced);
        Commit();
    }

    /// <summary>The draft goes to the request: applied at once, or kept for OK.</summary>
    private void Commit()
    {
        if (Request is not { } request || request.CloseRequested.IsCancellationRequested)
            return;
        // Applied at once, the picker's change comes back (SelectionChanged) while a wheel may still be in its own handler: the
        // popup already shows it, so nothing is built again then.
        _committing = true;
        bool taken;
        try
        {
            taken = request.SetDate(_draft);
        }
        finally
        {
            _committing = false;
        }
        // A moment the request will not take (the app's rule changed while open) goes back to the request's.
        if (!taken && request.CurrentDate is { } kept)
            Show(kept);
    }

    /// <inheritdoc />
    /// <remarks>The calendar asks the picker's rules as it draws: the days follow when it is drawn again.</remarks>
    public void ItemStatesChanged(int column, IReadOnlyList<bool>? states)
    {
        Request?.SetItemStates(column, states);
        _calendar.RefreshDayStyles();
    }

    /// <summary>The picker's value changed while the popup is open (the app set it): the popup shows it.</summary>
    public void SelectionChanged(int index)
    {
        if (_committing)
            return;
        var value = _current();
        Request?.Refresh(_wheels.IndicesOf(value));
        if (value is not { } shown)
            return;
        Show(shown);
        _calendar.GoToDate(shown.Date);
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
                // Between the calendar and the time wheels; the focus stays in the popup.
                _timeFocused = !_timeFocused;
                _timeView?.ShowFocusRing(_timeFocused);
                return true;
            case SkUiKey.Enter or SkUiKey.Space when !key.IsRepeat && (_timeFocused || _calendar.SelectedDate is { } chosen && (_calendar.FocusDate ?? chosen) == chosen):
                request.Accept();
                return true;
        }
        return _timeFocused ? _timeView?.OnKeyDown(key) == true : ((ISkUiKeyHandler)_calendar).OnKeyDown(key);
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
        _calendar.SelectionChanged -= OnSelectionChanged;
        _timeView?.Release();
    }
}
