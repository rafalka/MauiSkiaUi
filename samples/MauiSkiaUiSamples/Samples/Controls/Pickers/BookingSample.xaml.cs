using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

namespace MauiSkiaUiSamples.Samples.Controls.Pickers;

/// <summary>
/// HOWTO: date and time pickers in XAML over a view model. The page's XAML (BookingSample.xaml) shows:
/// <list type="number">
/// <item>A day (<c>SkUiDatePicker</c>, MAUI's <c>DatePicker</c> with the prefix changed) from today on, without holidays
/// (<c>BlackoutDates</c>) or weekends (<c>DateEnabled</c>, set in code), with headers on its wheels.</item>
/// <item>A time (<c>SkUiTimePicker</c>) within opening hours (<c>MinimumTime</c> / <c>MaximumTime</c>) in quarter hours
/// (<c>MinuteInterval</c>), chosen with OK, starting empty with the wheels on 9:00 (<c>DefaultHighlightedTime</c>).</item>
/// <item>A reminder (<c>SkUiDateTimePicker</c>): a day and a time on one popup, in half hours.</item>
/// <item>A card's expiry on two wheels, month and year (<c>WheelFormat="MM/yy"</c>).</item>
/// </list>
/// </summary>
public sealed partial class BookingSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Booking (date and time pickers in XAML)",
        Summary: "MAUI's DatePicker and TimePicker with the prefix changed, plus a range, holidays and weekends that cannot be chosen, " +
                 "opening hours in quarter hours, a reminder with a day and a time on one popup, and a card's expiry on month and year wheels.",
        HowTo:
        [
            "Change the prefix: `<DatePicker Date=\"{Binding Day}\" …/>` becomes `<sk:SkUiDatePicker …/>`; `MinimumDate`, `MaximumDate`, " +
                "`Format`, `DateSelected` and `IsOpen` work as MAUI's (also `TimePicker`'s `Time`, `Format`, `TimeSelected`).",
            "Days that cannot be chosen: `BlackoutDates=\"{Binding Holidays}\"`, and a rule in code: `Day.DateEnabled = date => …`.",
            "Steps and a range of times: `MinuteInterval=\"15\" MinimumTime=\"08:00\" MaximumTime=\"17:30\"`.",
            "Which wheels show: `WheelFormat` (a .NET date format: `\"MM/yy\"` is a month and a year); the field's text is `Format`'s.",
            "Start empty with the wheels somewhere useful: `Time=\"{x:Null}\"` (or a null binding) and `DefaultHighlightedTime=\"09:00\"`."
        ],
        ThingsToKnow:
        [
            "Wheels depend on each other: a day the month lacks, a weekend or a holiday is dimmed, and a choice of one moves to the nearest " +
                "day that can be chosen.",
            "Without a `WheelFormat`, the wheels follow the current culture: its order of day, month and year, and its clock.",
            "A part without a wheel is the least when it is below the wheels (the expiry's day is the 1st); above them it keeps the " +
                "value's (a picker without a year wheel stays in the year it shows).",
            "The pickers' texts (OK, Cancel, Clear, the headers \"Day\", \"Month\", \"Year\") come from `SkUiStrings`."
        ]);

    public BookingSample() : base(Info)
    {
        InitializeComponent();
        BindingContext = new BookingModel();
        Day.DateEnabled = date => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    }
}

public sealed class BookingModel : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs DayChanged = new(nameof(Day));
    private static readonly PropertyChangedEventArgs TimeChanged = new(nameof(Time));
    private static readonly PropertyChangedEventArgs ReminderChanged = new(nameof(Reminder));
    private static readonly PropertyChangedEventArgs ExpiryChanged = new(nameof(Expiry));
    private static readonly PropertyChangedEventArgs SummaryChanged = new(nameof(Summary));

    public BookingModel()
    {
        var today = DateTime.Today;
        FirstDay = today;
        LastDay = today.AddMonths(3);
        LastExpiry = new DateTime(today.Year + 10, 12, 31);
        Holidays = [new DateTime(today.Year, 12, 24), new DateTime(today.Year, 12, 25), new DateTime(today.Year, 12, 26), new DateTime(today.Year + 1, 1, 1)];
        // The next weekday that is not a holiday.
        var day = today.AddDays(1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || Holidays.Contains(day))
            day = day.AddDays(1);
        Day = day;
    }

    public DateTime FirstDay { get; }

    public DateTime LastDay { get; }

    public DateTime LastExpiry { get; }

    public ObservableCollection<DateTime> Holidays { get; }

    public DateTime? Day { get; set => Set(ref field, value, DayChanged); }

    public TimeSpan? Time { get; set => Set(ref field, value, TimeChanged); }

    public DateTime? Reminder { get; set => Set(ref field, value, ReminderChanged); }

    public DateTime? Expiry { get; set => Set(ref field, value, ExpiryChanged); }

    public string Summary
    {
        get
        {
            var culture = CultureInfo.CurrentCulture;
            var when = Day is { } day ? day.ToString("D", culture) : "no day";
            var at = Time is { } time ? (DateTime.Today + time).ToString("t", culture) : "no time yet";
            var reminder = Reminder is { } remind ? $"reminder {remind.ToString("g", culture)}" : "no reminder";
            var card = Expiry is { } expiry ? $"card valid to {expiry:MM/yy}" : "no card";
            return $"{when}, {at} · {reminder} · {card}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, PropertyChangedEventArgs changed)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, changed);
        PropertyChanged?.Invoke(this, SummaryChanged);
    }
}
