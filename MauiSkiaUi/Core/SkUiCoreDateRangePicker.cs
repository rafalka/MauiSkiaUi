namespace MauiSkiaUi.Core;

/// <summary>
/// The Core twin of <see cref="SkUiDateRangePicker"/>: a drawn picker of a run of days whose popup is a month calendar in range
/// selection (the first tap starts the range, the second ends it). <see cref="SkUiCoreValuePicker{T}.Value"/> is the range,
/// <c>null</c> for none. Fluent <c>Set*</c> and <c>PropertyChanged</c>, as Core nodes; the rules are read when the popup opens.
/// </summary>
public class SkUiCoreDateRangePicker : SkUiCoreValuePicker<SkUiDateRange?>
{
    private DateTime? _minimumDate;
    private DateTime? _maximumDate;
    private IList<DateTime> _blackoutDates = [];
    private Func<DateTime, bool>? _dateEnabled;
    private int _minimumRangeDays;
    private int _maximumRangeDays;
    private bool _allowsDisabledDaysInRange = true;
    private string _displayFormat = SkUiDateRangePicker.DefaultDisplayFormat;
    private Style? _calendarStyle;

    /// <inheritdoc cref="SkUiDateRangePicker.MinimumDate" />
    public DateTime? MinimumDate { get => _minimumDate; set => SetMinimumDate(value); }

    /// <inheritdoc cref="SkUiDateRangePicker.MaximumDate" />
    public DateTime? MaximumDate { get => _maximumDate; set => SetMaximumDate(value); }

    /// <inheritdoc cref="SkUiDateRangePicker.BlackoutDates" />
    public IList<DateTime> BlackoutDates { get => _blackoutDates; set => SetBlackoutDates(value); }

    /// <inheritdoc cref="SkUiDateRangePicker.DateEnabled" />
    public Func<DateTime, bool>? DateEnabled { get => _dateEnabled; set => SetDateEnabled(value); }

    /// <inheritdoc cref="SkUiCalendar.MinimumRangeDays" />
    public int MinimumRangeDays { get => _minimumRangeDays; set => SetMinimumRangeDays(value); }

    /// <inheritdoc cref="SkUiCalendar.MaximumRangeDays" />
    public int MaximumRangeDays { get => _maximumRangeDays; set => SetMaximumRangeDays(value); }

    /// <inheritdoc cref="SkUiCalendar.AllowsDisabledDaysInRange" />
    public bool AllowsDisabledDaysInRange { get => _allowsDisabledDaysInRange; set => SetAllowsDisabledDaysInRange(value); }

    /// <inheritdoc cref="SkUiDateRangePicker.DisplayFormat" />
    public string DisplayFormat { get => _displayFormat; set => SetDisplayFormat(value); }

    /// <inheritdoc cref="SkUiDatePicker.CalendarStyle" />
    public Style? CalendarStyle { get => _calendarStyle; set => SetCalendarStyle(value); }

    /// <summary>Sets <see cref="MinimumDate"/> (its day).</summary>
    public SkUiCoreDateRangePicker SetMinimumDate(DateTime? value)
    {
        SetProperty(ref _minimumDate, value?.Date, nameof(MinimumDate));
        return this;
    }

    /// <summary>Sets <see cref="MaximumDate"/> (its day).</summary>
    public SkUiCoreDateRangePicker SetMaximumDate(DateTime? value)
    {
        SetProperty(ref _maximumDate, value?.Date, nameof(MaximumDate));
        return this;
    }

    /// <summary>Sets <see cref="BlackoutDates"/>.</summary>
    public SkUiCoreDateRangePicker SetBlackoutDates(IList<DateTime> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        SetProperty(ref _blackoutDates, value, nameof(BlackoutDates));
        return this;
    }

    /// <summary>Sets <see cref="DateEnabled"/>.</summary>
    public SkUiCoreDateRangePicker SetDateEnabled(Func<DateTime, bool>? value)
    {
        SetProperty(ref _dateEnabled, value, nameof(DateEnabled));
        return this;
    }

    /// <summary>Sets <see cref="MinimumRangeDays"/>.</summary>
    public SkUiCoreDateRangePicker SetMinimumRangeDays(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        SetProperty(ref _minimumRangeDays, value, nameof(MinimumRangeDays));
        return this;
    }

    /// <summary>Sets <see cref="MaximumRangeDays"/>.</summary>
    public SkUiCoreDateRangePicker SetMaximumRangeDays(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        SetProperty(ref _maximumRangeDays, value, nameof(MaximumRangeDays));
        return this;
    }

    /// <summary>Sets <see cref="AllowsDisabledDaysInRange"/>.</summary>
    public SkUiCoreDateRangePicker SetAllowsDisabledDaysInRange(bool value)
    {
        SetProperty(ref _allowsDisabledDaysInRange, value, nameof(AllowsDisabledDaysInRange));
        return this;
    }

    /// <summary>Sets <see cref="DisplayFormat"/>.</summary>
    public SkUiCoreDateRangePicker SetDisplayFormat(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref _displayFormat, value, nameof(DisplayFormat)))
            RefreshFieldText();
        return this;
    }

    /// <summary>Sets <see cref="CalendarStyle"/>.</summary>
    public SkUiCoreDateRangePicker SetCalendarStyle(Style? value)
    {
        SetProperty(ref _calendarStyle, value, nameof(CalendarStyle));
        return this;
    }

    /// <inheritdoc />
    protected override SkUiValuePopupContent<SkUiDateRange?> CreateValueContent(SkUiPickerPopupContext context) =>
        new SkUiDateRangePopupContent(new SkUiDateRangeRules(_minimumDate, _maximumDate, _blackoutDates, _dateEnabled, _minimumRangeDays, _maximumRangeDays,
            _allowsDisabledDaysInRange, _calendarStyle));

    /// <inheritdoc cref="SkUiDateRangePicker.FormatValue" />
    protected override string? FormatValue(SkUiDateRange? value) => value is { } range ? SkUiDateRangePicker.FormatRange(range, _displayFormat) : null;
}
