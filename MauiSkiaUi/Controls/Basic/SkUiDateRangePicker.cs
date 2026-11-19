using System.Collections.ObjectModel;
using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// A drawn picker of a run of days (a stay, a leave, a report's period): the field shows the range ("3/11/2026 – 3/15/2026", by
/// <see cref="DisplayFormat"/>), and the popup holds a month calendar in range selection (<see cref="SkUiCalendar"/>): the first tap starts the range, the second ends it
/// and the popup closes. <see cref="SkUiValuePicker{T}.Value"/> (two-way) is the range, <c>null</c> for none. The days that can be
/// chosen follow the date picker's rules (<see cref="MinimumDate"/>, <see cref="MaximumDate"/>, <see cref="BlackoutDates"/>,
/// <see cref="DateEnabled"/>) and the calendar's range rules (<see cref="MinimumRangeDays"/>, <see cref="MaximumRangeDays"/>,
/// <see cref="AllowsDisabledDaysInRange"/>). Core twin: <see cref="Core.SkUiCoreDateRangePicker"/>. SkiaUi control.
/// </summary>
/// <remarks>
/// Everything else is the picker's (<see cref="SkUiPickerBase"/>): <c>Title</c>, <c>Placeholder</c>, <c>UpdateMode</c> (waiting for OK,
/// the range is a draft: one day while only its start is chosen), the popup's placement and presenter, the app's popup header and
/// footer, the clear buttons, <c>IsOpen</c>, keyboard and screen readers. A presenter that shows its own UI handles
/// <see cref="SkUiValuePopupRequest{T}"/> of <see cref="SkUiDateRange"/>. The rules are read when the popup opens.
/// </remarks>
public class SkUiDateRangePicker : SkUiValuePicker<SkUiDateRange?>
{
    /// <summary>The default for <see cref="DisplayFormat"/>: both days in the culture's short date format, with a dash between.</summary>
    public const string DefaultDisplayFormat = "{0:d} – {1:d}";

    /// <summary>Bindable property for <see cref="MinimumDate"/>.</summary>
    public static readonly BindableProperty MinimumDateProperty = BindableProperty.Create(nameof(MinimumDate), typeof(DateTime?), typeof(SkUiDateRangePicker), null,
        coerceValue: (_, value) => ((DateTime?)value)?.Date);

    /// <summary>Bindable property for <see cref="MaximumDate"/>.</summary>
    public static readonly BindableProperty MaximumDateProperty = BindableProperty.Create(nameof(MaximumDate), typeof(DateTime?), typeof(SkUiDateRangePicker), null,
        coerceValue: (_, value) => ((DateTime?)value)?.Date);

    /// <summary>Bindable property for <see cref="BlackoutDates"/>.</summary>
    public static readonly BindableProperty BlackoutDatesProperty = BindableProperty.Create(nameof(BlackoutDates), typeof(IList<DateTime>), typeof(SkUiDateRangePicker),
        null, defaultValueCreator: _ => new ObservableCollection<DateTime>());

    /// <summary>Bindable property for <see cref="MinimumRangeDays"/>.</summary>
    public static readonly BindableProperty MinimumRangeDaysProperty = BindableProperty.Create(nameof(MinimumRangeDays), typeof(int), typeof(SkUiDateRangePicker), 0,
        validateValue: (_, value) => (int)value >= 0);

    /// <summary>Bindable property for <see cref="MaximumRangeDays"/>.</summary>
    public static readonly BindableProperty MaximumRangeDaysProperty = BindableProperty.Create(nameof(MaximumRangeDays), typeof(int), typeof(SkUiDateRangePicker), 0,
        validateValue: (_, value) => (int)value >= 0);

    /// <summary>Bindable property for <see cref="AllowsDisabledDaysInRange"/>.</summary>
    public static readonly BindableProperty AllowsDisabledDaysInRangeProperty = BindableProperty.Create(nameof(AllowsDisabledDaysInRange), typeof(bool),
        typeof(SkUiDateRangePicker), true);

    /// <summary>Bindable property for <see cref="CalendarSystem"/>.</summary>
    public static readonly BindableProperty CalendarSystemProperty = BindableProperty.Create(nameof(CalendarSystem), typeof(System.Globalization.Calendar),
        typeof(SkUiDateRangePicker), null, validateValue: (_, value) => SkUiCalendarSystem.IsSupported((System.Globalization.Calendar?)value));

    /// <summary>The popup calendar's calendar system (1.15; <see cref="SkUiCalendar.CalendarSystem"/>); <c>null</c>: Gregorian. The field's text follows <see cref="DisplayFormat"/> and the culture.</summary>
    public System.Globalization.Calendar? CalendarSystem
    {
        get => (System.Globalization.Calendar?)GetValue(CalendarSystemProperty);
        set => SetValue(CalendarSystemProperty, value);
    }

    /// <summary>Sets <see cref="CalendarSystem"/>; throws for a calendar the pages cannot be cut by.</summary>
    public SkUiDateRangePicker SetCalendarSystem(System.Globalization.Calendar? value)
    {
        if (!SkUiCalendarSystem.IsSupported(value))
            throw new ArgumentOutOfRangeException(nameof(value), value?.GetType().Name, "The calendar's pages cannot be cut by this calendar.");
        CalendarSystem = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="DisplayFormat"/>.</summary>
    public static readonly BindableProperty DisplayFormatProperty = BindableProperty.Create(nameof(DisplayFormat), typeof(string), typeof(SkUiDateRangePicker),
        DefaultDisplayFormat, validateValue: SkUiValidate.NotNull,
        propertyChanged: (picker, _, _) => ((SkUiDateRangePicker)picker).RefreshFieldText());

    /// <summary>Bindable property for <see cref="CalendarStyle"/>.</summary>
    public static readonly BindableProperty CalendarStyleProperty = BindableProperty.Create(nameof(CalendarStyle), typeof(Style), typeof(SkUiDateRangePicker), null);

    /// <summary>The earliest day that can be chosen; <c>null</c> (default): none.</summary>
    public DateTime? MinimumDate { get => (DateTime?)GetValue(MinimumDateProperty); set => SetValue(MinimumDateProperty, value); }

    /// <summary>The latest day that can be chosen; <c>null</c> (default): none.</summary>
    public DateTime? MaximumDate { get => (DateTime?)GetValue(MaximumDateProperty); set => SetValue(MaximumDateProperty, value); }

    /// <summary>Days that cannot be chosen (holidays, booked nights): an empty list by default (add to it, or set your own).</summary>
    public IList<DateTime>? BlackoutDates { get => (IList<DateTime>?)GetValue(BlackoutDatesProperty); set => SetValue(BlackoutDatesProperty, value); }

    /// <summary>Whether a day can be chosen, a rule of the app's; <c>null</c> (default): every day in the range. Not bindable.</summary>
    public Func<DateTime, bool>? DateEnabled
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
                return;
            OnPropertyChanging();
            field = value;
            OnPropertyChanged();
        }
    }

    /// <inheritdoc cref="SkUiCalendar.MinimumRangeDays" />
    public int MinimumRangeDays { get => (int)GetValue(MinimumRangeDaysProperty); set => SetValue(MinimumRangeDaysProperty, value); }

    /// <inheritdoc cref="SkUiCalendar.MaximumRangeDays" />
    public int MaximumRangeDays { get => (int)GetValue(MaximumRangeDaysProperty); set => SetValue(MaximumRangeDaysProperty, value); }

    /// <inheritdoc cref="SkUiCalendar.AllowsDisabledDaysInRange" />
    public bool AllowsDisabledDaysInRange { get => (bool)GetValue(AllowsDisabledDaysInRangeProperty); set => SetValue(AllowsDisabledDaysInRangeProperty, value); }

    /// <summary>
    /// How the field shows the range: a <c>string.Format</c> composite format in the current culture, with the first day as
    /// <c>{0}</c>, the last day as <c>{1}</c> and the number of days as <c>{2}</c> (<c>"{0:MMM d} – {1:MMM d}"</c>,
    /// <c>"{0:d} ({2} days)"</c>). Default <see cref="DefaultDisplayFormat"/> (<c>"{0:d} – {1:d}"</c>). An open range (only its first
    /// day, set by the app) has no last day or count: they format as empty, and the text's trailing spaces are dropped. A format that
    /// cannot be used falls back to the default. It replaces the inherited <see cref="SkUiValuePicker{T}.ValueFormat"/>, which this
    /// picker does not use.
    /// </summary>
    public string DisplayFormat { get => (string)GetValue(DisplayFormatProperty); set => SetValue(DisplayFormatProperty, value); }

    /// <inheritdoc cref="SkUiDatePicker.CalendarStyle" />
    public Style? CalendarStyle { get => (Style?)GetValue(CalendarStyleProperty); set => SetValue(CalendarStyleProperty, value); }

    /// <summary>Sets <see cref="MinimumDate"/> (same as the property setter).</summary>
    public SkUiDateRangePicker SetMinimumDate(DateTime? value) { MinimumDate = value; return this; }

    /// <summary>Sets <see cref="MaximumDate"/> (same as the property setter).</summary>
    public SkUiDateRangePicker SetMaximumDate(DateTime? value) { MaximumDate = value; return this; }

    /// <summary>Sets <see cref="BlackoutDates"/> (same as the property setter).</summary>
    public SkUiDateRangePicker SetBlackoutDates(IList<DateTime>? value) { BlackoutDates = value; return this; }

    /// <summary>Sets <see cref="DateEnabled"/> (same as the property setter).</summary>
    public SkUiDateRangePicker SetDateEnabled(Func<DateTime, bool>? value) { DateEnabled = value; return this; }

    /// <summary>Sets <see cref="MinimumRangeDays"/> (same as the property setter).</summary>
    public SkUiDateRangePicker SetMinimumRangeDays(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); MinimumRangeDays = value; return this; }

    /// <summary>Sets <see cref="MaximumRangeDays"/> (same as the property setter).</summary>
    public SkUiDateRangePicker SetMaximumRangeDays(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); MaximumRangeDays = value; return this; }

    /// <summary>Sets <see cref="AllowsDisabledDaysInRange"/> (same as the property setter).</summary>
    public SkUiDateRangePicker SetAllowsDisabledDaysInRange(bool value) { AllowsDisabledDaysInRange = value; return this; }

    /// <summary>Sets <see cref="DisplayFormat"/> (same as the property setter).</summary>
    public SkUiDateRangePicker SetDisplayFormat(string value) { ArgumentNullException.ThrowIfNull(value); DisplayFormat = value; return this; }

    /// <summary>Sets <see cref="CalendarStyle"/> (same as the property setter).</summary>
    public SkUiDateRangePicker SetCalendarStyle(Style? value) { CalendarStyle = value; return this; }

    /// <inheritdoc />
    protected override SkUiValuePopupContent<SkUiDateRange?> CreateValueContent(SkUiPickerPopupContext context) =>
        new SkUiDateRangePopupContent(new SkUiDateRangeRules(MinimumDate, MaximumDate, BlackoutDates, DateEnabled, MinimumRangeDays, MaximumRangeDays,
            AllowsDisabledDaysInRange, CalendarStyle, CalendarSystem));

    /// <inheritdoc />
    /// <remarks>The range by <see cref="DisplayFormat"/>.</remarks>
    protected override string? FormatValue(SkUiDateRange? value) => value is { } range ? FormatRange(range, DisplayFormat) : null;

    /// <summary>
    /// A range's field text: <paramref name="format"/> with the first day as <c>{0}</c>, the last day as <c>{1}</c> and the number of
    /// days as <c>{2}</c> (an open range: none, trailing spaces dropped); a format that cannot be used: <see cref="DefaultDisplayFormat"/>.
    /// </summary>
    internal static string FormatRange(SkUiDateRange range, string? format)
    {
        // In the Gregorian calendar the popup's pages and titles use (Thai Buddhist and Hijri cultures).
        var culture = SkUiCalendarGrid.Gregorian(CultureInfo.CurrentCulture);
        object?[] parts = range.IsComplete ? [range.Start, range.LastDay, range.Days] : [range.Start, null, null];
        string text;
        try
        {
            text = string.Format(culture, string.IsNullOrEmpty(format) ? DefaultDisplayFormat : format, parts);
        }
        catch (FormatException)
        {
            text = string.Format(culture, DefaultDisplayFormat, parts);
        }
        return range.IsComplete ? text : text.TrimEnd();
    }
}
