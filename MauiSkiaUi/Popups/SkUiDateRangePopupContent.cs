namespace MauiSkiaUi;

/// <summary>What a date range picker's calendar is opened with (read when the popup opens).</summary>
internal readonly record struct SkUiDateRangeRules(DateTime? MinimumDate, DateTime? MaximumDate, IList<DateTime>? BlackoutDates,
    Func<DateTime, bool>? DateEnabled, int MinimumRangeDays, int MaximumRangeDays, bool AllowsDisabledDaysInRange, Style? CalendarStyle,
    System.Globalization.Calendar? CalendarSystem = null);

/// <summary>
/// The popup of a date range picker (<see cref="SkUiDateRangePicker"/>, <see cref="Core.SkUiCoreDateRangePicker"/>): a month calendar in
/// range selection. The first tap starts a range, the second ends it and the popup closes with it; waiting for OK
/// (<c>UpdateMode</c> <see cref="SkUiPickerUpdateMode.WhenFinished"/>), the range is the draft (one day while only its start is
/// chosen; when <c>MinimumRangeDays</c> asks for more, the last whole range stays the draft until the range ends). The request is <see cref="SkUiValuePopupRequest{T}"/> of <see cref="SkUiDateRange"/>. Keys go to the calendar; Enter
/// on the draft (waiting for OK) accepts it.
/// </summary>
internal sealed class SkUiDateRangePopupContent(SkUiDateRangeRules rules) : SkUiValuePopupContent<SkUiDateRange?>
{
    private SkUiCalendar? _calendar;
    private bool _showing;
    private bool _choosing;

    /// <summary>The calendar (tests); <c>null</c> before the body was built.</summary>
    internal SkUiCalendar? Calendar => _calendar;

    /// <inheritdoc />
    protected override SkUiView CreateBody()
    {
        // A calendar neither scrolls nor shrinks: anchored with too little room beside the field, it covers the field (issue #111).
        MinimumHeight = double.PositiveInfinity;
        var calendar = new SkUiCalendar
        {
            SelectionMode = SkUiCalendarSelectionMode.Range,
            MinimumDate = rules.MinimumDate,
            MaximumDate = rules.MaximumDate,
            DateEnabled = rules.DateEnabled,
            MinimumRangeDays = rules.MinimumRangeDays,
            MaximumRangeDays = rules.MaximumRangeDays,
            AllowsDisabledDaysInRange = rules.AllowsDisabledDaysInRange,
            Margin = new Thickness(8, 4)
        };
        if (rules.BlackoutDates is { Count: > 0 } blackout)
            calendar.BlackoutDates = blackout.ToList(); // as opened
        if (rules.CalendarStyle is { } style)
            calendar.Style = style;
        if (rules.CalendarSystem is { } system)
            calendar.CalendarSystem = system;
        else if (Context.FontFamily is { } family)
            calendar.FontFamily = family;
        _calendar = calendar;
        ShowValue();
        if (Value is { } range)
            calendar.DisplayDate = range.Start;
        calendar.SelectionChanged += OnSelectionChanged;
        return calendar;
    }

    /// <inheritdoc />
    /// <remarks>As wide as the calendar wants (seven touch-sized columns), within the popup's widths.</remarks>
    public override Size PreferredSize(double minimumWidth, double maximumWidth, double maximumHeight)
    {
        if (_calendar is not { } calendar)
            return Size.Zero;
        var natural = calendar.Measure(double.PositiveInfinity, double.PositiveInfinity);
        var width = Math.Clamp(Math.Ceiling(natural.Width), Math.Min(minimumWidth, maximumWidth), maximumWidth);
        return new Size(width, Math.Min(calendar.Measure(width, double.PositiveInfinity).Height, maximumHeight));
    }

    /// <summary>The user changed the calendar's range: a whole range ends the popup (or is the draft); a start alone is a one-day draft (or, under a minimum length, leaves the draft as it was).</summary>
    private void OnSelectionChanged(object? sender, SkUiCalendarSelectionChangedEventArgs args)
    {
        if (_showing || _calendar?.SelectedRange is not { } range)
            return;
        if (range.IsComplete && !Context.RequiresAccept)
        {
            Complete(range);
            return;
        }
        // A start alone is a one-day draft, unless a range must be longer: the draft stays the last whole range (or none).
        if (!Context.RequiresAccept || (!range.IsComplete && rules.MinimumRangeDays > 1))
            return;
        _choosing = true;
        try
        {
            SetValue(range.IsComplete ? range : new SkUiDateRange(range.Start, range.Start));
        }
        finally
        {
            _choosing = false;
        }
    }

    /// <inheritdoc />
    /// <remarks>The Clear button, or the app's change: the calendar shows it (not while the user's own draft is applied).</remarks>
    protected override void OnValueChanged()
    {
        if (!_choosing)
            ShowValue();
    }

    private void ShowValue()
    {
        if (_calendar is not { } calendar)
            return;
        _showing = true;
        try
        {
            calendar.SelectedRange = Value;
        }
        finally
        {
            _showing = false;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The calendar moves its focus and chooses with the keys; waiting for OK, Enter on a day of the whole draft accepts it (the
    /// popup does), else it chooses the focused day.
    /// </remarks>
    internal override bool OnKeyDownCore(in SkUiKeyEvent key)
    {
        if (_calendar is not { } calendar)
            return false;
        if (key.Key == SkUiKey.Enter && Context.RequiresAccept && calendar.SelectedRange is { IsComplete: true } range
            && (calendar.FocusDate is not { } focus || range.Contains(focus)))
            return false;
        return ((ISkUiKeyHandler)calendar).OnKeyDown(key);
    }

    /// <inheritdoc />
    /// <remarks>Waiting for OK, Clear empties the draft and the calendar; the popup stays open for another range (OK applies none).</remarks>
    internal override bool ClearsDraftCore => true;

    /// <inheritdoc />
    protected override void OnClosed()
    {
        if (_calendar is { } calendar)
            calendar.SelectionChanged -= OnSelectionChanged;
    }
}
