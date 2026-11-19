using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// A calendar other than the Gregorian one, as the calendar's pages use it (1.15, <c>CalendarSystem</c>): the date parts, the months
/// a year has and the days a month has, the dates it supports and the culture that writes its texts. Dates stay
/// <see cref="DateTime"/> values; only the pages are cut by this calendar. Holds no control, and is shared per calendar object.
/// </summary>
/// <remarks>
/// <para>
/// Two kinds. The Thai Buddhist, Taiwan, Korean and Japanese calendars have the Gregorian months and number years differently
/// (<see cref="HasGregorianMonths"/>): the pages are the Gregorian ones, and decades and centuries count this calendar's years
/// (Gregorian years for the Japanese calendar, whose year numbers start again with each era). The Hijri, Umm al-Qura, Persian and
/// Hebrew calendars have months of their own: the pages follow them (13 months in a Hebrew leap year).
/// </para>
/// <para>
/// Texts come from the current culture when it can write this calendar (one of its optional calendars), else from the calendar's
/// own culture (Arabic for Hijri and Umm al-Qura, Persian, Hebrew, Thai, Chinese (Taiwan), Korean, Japanese): .NET formats a
/// calendar only in the cultures that offer it.
/// </para>
/// </remarks>
internal sealed class SkUiCalendarSystem
{
    private static readonly DateTime First = DateTime.MinValue.Date;
    private static readonly DateTime Last = DateTime.MaxValue.Date;

    private readonly int _yearOffset;
    private (CultureInfo Culture, CultureInfo Formatting)? _culture;

    private SkUiCalendarSystem(Calendar calendar)
    {
        Calendar = calendar;
        // The first whole supported day.
        var min = calendar.MinSupportedDateTime;
        Minimum = min.TimeOfDay > TimeSpan.Zero ? min.Date.AddDays(1) : min.Date;
        // The last supported day (its time may stop before midnight).
        Maximum = calendar.MaxSupportedDateTime.Date;
        if (Maximum > Last)
            Maximum = Last;
        HasGregorianMonths = calendar is ThaiBuddhistCalendar or TaiwanCalendar or KoreanCalendar or JapaneseCalendar;
        // Years of a calendar with one era and the Gregorian months are the Gregorian ones moved by a constant.
        IsEraCalendar = calendar is JapaneseCalendar;
        _yearOffset = HasGregorianMonths && !IsEraCalendar ? calendar.GetYear(new DateTime(2000, 6, 1)) - 2000 : 0;
        MinimumYear = Year(Minimum);
        MaximumYear = Year(Maximum);
        MaxMonthsInYear = calendar is HebrewCalendar ? 13 : 12;
        HomeCulture = calendar switch
        {
            ThaiBuddhistCalendar => "th-TH",
            TaiwanCalendar => "zh-TW",
            KoreanCalendar => "ko-KR",
            JapaneseCalendar => "ja-JP",
            UmAlQuraCalendar or HijriCalendar => "ar-SA",
            PersianCalendar => "fa-IR",
            HebrewCalendar => "he-IL",
            _ => "en-US"
        };
        // The Japanese calendar may report a first day before the eras its cultures write start (.NET 10 with ICU: 8 September 1868
        // against Meiji's 23 October): such days cannot be written with their era, so the pages start where the writing does.
        if (calendar is JapaneseCalendar && HomeCultureWith() is { } japanese)
        {
            Minimum = FirstWrittenDay(Minimum, japanese);
            MinimumYear = Year(Minimum);
        }
    }

    /// <summary>The calendar.</summary>
    public Calendar Calendar { get; }

    /// <summary>The first day the calendar supports (and <see cref="DateTime"/> holds).</summary>
    public DateTime Minimum { get; }

    /// <summary>The last day the calendar supports.</summary>
    public DateTime Maximum { get; }

    /// <summary>The calendar's year of <see cref="Minimum"/>.</summary>
    public int MinimumYear { get; }

    /// <summary>The calendar's year of <see cref="Maximum"/>.</summary>
    public int MaximumYear { get; }

    /// <summary>The Gregorian months with other year numbers (Thai Buddhist, Taiwan, Korean, Japanese).</summary>
    public bool HasGregorianMonths { get; }

    /// <summary>Year numbers that start again with each era (Japanese): decades and centuries count Gregorian years.</summary>
    public bool IsEraCalendar { get; }

    /// <summary>The most months a year has (13 for the Hebrew calendar).</summary>
    public int MaxMonthsInYear { get; }

    /// <summary>The culture that writes this calendar when the current one cannot.</summary>
    public string HomeCulture { get; }

    /// <summary>Whether the pages can be cut by <paramref name="calendar"/> (the calendars named in <see cref="SkUiCalendarSystem"/>, and the Gregorian one).</summary>
    public static bool IsSupported(Calendar? calendar) => calendar is null or GregorianCalendar or ThaiBuddhistCalendar or TaiwanCalendar or KoreanCalendar
        or JapaneseCalendar or UmAlQuraCalendar or HijriCalendar or PersianCalendar or HebrewCalendar;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Calendar, SkUiCalendarSystem> Systems = new();

    /// <summary>The system of <paramref name="calendar"/>, or <c>null</c> for the Gregorian calendar (the grid's own code) and calendars it does not support.</summary>
    public static SkUiCalendarSystem? For(Calendar? calendar) =>
        calendar is null or GregorianCalendar || !IsSupported(calendar) ? null : Systems.GetValue(calendar, static key => new SkUiCalendarSystem(key));

    /// <summary>The year of <paramref name="date"/> that decades and centuries count (Gregorian years for an era calendar).</summary>
    public int Year(DateTime date) => HasGregorianMonths ? date.Year + _yearOffset : Calendar.GetYear(date);

    /// <summary>The month of <paramref name="date"/> (1 to 13).</summary>
    public int Month(DateTime date) => HasGregorianMonths ? date.Month : Calendar.GetMonth(date);

    /// <summary>The day of <paramref name="date"/>'s month.</summary>
    public int Day(DateTime date) => HasGregorianMonths ? date.Day : Calendar.GetDayOfMonth(date);

    /// <summary>The months of <paramref name="year"/>.</summary>
    public int MonthsInYear(int year) => HasGregorianMonths ? 12 : Calendar.GetMonthsInYear(year);

    /// <summary>The days of <paramref name="month"/> of <paramref name="year"/>.</summary>
    public int DaysInMonth(int year, int month) => HasGregorianMonths ? DateTime.DaysInMonth(year - _yearOffset, month) : Calendar.GetDaysInMonth(year, month);

    /// <summary>The first day of <paramref name="date"/>'s month, or <see cref="Minimum"/> in the first, partly supported month.</summary>
    public DateTime MonthStart(DateTime date)
    {
        // A day outside the range has no parts this calendar can give (a week page may start before the first day).
        var day = Clamp(date);
        return Clamp(day.AddDays(1 - Day(day)));
    }

    /// <summary>The first day from <paramref name="from"/> that <paramref name="culture"/> can write with its era (at most a year on).</summary>
    private static DateTime FirstWrittenDay(DateTime from, CultureInfo culture)
    {
        for (var day = from; day < from.AddDays(400); day = day.AddDays(1))
        {
            try
            {
                _ = day.ToString("gg y M d", culture);
                return day;
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }
        return from;
    }

    /// <summary>
    /// The day of <paramref name="year"/>, <paramref name="month"/> and <paramref name="day"/> (the month's last day when it has fewer);
    /// in the month that holds <see cref="Minimum"/> or <see cref="Maximum"/>, kept within them; <c>null</c> for a month the calendar
    /// does not support at all (a year outside its years, a month before its first or after its last).
    /// </summary>
    public DateTime? Date(long year, int month, int day)
    {
        if (year < MinimumYear || year > MaximumYear)
            return null;
        var y = (int)year;
        // The first and last years are only partly supported: their months outside the range have no days here.
        if (y == MinimumYear && month < Month(Minimum) || y == MaximumYear && month > Month(Maximum))
            return null;
        if (y == MinimumYear && month == Month(Minimum) && day < Day(Minimum))
            return Minimum;
        if (y == MaximumYear && month == Month(Maximum) && day > Day(Maximum))
            return Maximum;
        month = Math.Clamp(month, 1, MonthsInYear(y));
        day = Math.Clamp(day, 1, DaysInMonth(y, month));
        return HasGregorianMonths ? new DateTime(y - _yearOffset, month, day) : Calendar.ToDateTime(y, month, day, 0, 0, 0, 0);
    }

    /// <summary><paramref name="date"/> plus <paramref name="months"/> of this calendar (the same day, or the month's last), or <c>null</c> outside its range.</summary>
    public DateTime? AddMonths(DateTime date, long months)
    {
        var year = (long)Year(date);
        var month = Month(date) + months;
        // Months of a year differ (Hebrew): step a year at a time.
        while (month > MonthsInYear((int)Math.Clamp(year, MinimumYear, MaximumYear)))
        {
            month -= MonthsInYear((int)Math.Clamp(year, MinimumYear, MaximumYear));
            if (++year > MaximumYear)
                return null;
        }
        while (month < 1)
        {
            if (--year < MinimumYear)
                return null;
            month += MonthsInYear((int)year);
        }
        return Date(year, (int)month, Day(date));
    }

    /// <summary>The first day of <paramref name="year"/> (or <see cref="Minimum"/> in the first year), or <c>null</c> outside the calendar's years.</summary>
    public DateTime? YearStart(long year) => year == MinimumYear ? Minimum : Date(year, 1, 1); // the first year starts where the calendar does

    /// <summary>
    /// <paramref name="month"/> of <paramref name="fromYear"/> as the same month of <paramref name="toYear"/>: the same number, but
    /// for the Hebrew calendar between a leap year and a common one (Adar I and II against Adar; the months after them a place apart).
    /// </summary>
    public int SameMonth(int month, int fromYear, int toYear)
    {
        if (Calendar is not HebrewCalendar || toYear < MinimumYear || toYear > MaximumYear)
            return month;
        var (fromLeap, toLeap) = (MonthsInYear(fromYear) == 13, MonthsInYear(toYear) == 13);
        if (fromLeap == toLeap || month <= 5)
            return month;
        // Leap: 6 Adar I, 7 Adar II, 8 Nisan ...; common: 6 Adar, 7 Nisan ... (Adar of a common year is Adar II's, as Purim is).
        return fromLeap ? (month <= 7 ? 6 : month - 1) : (month == 6 ? 7 : month + 1);
    }

    /// <summary><paramref name="date"/> within the calendar's range.</summary>
    public DateTime Clamp(DateTime date) => date < Minimum ? Minimum : date > Maximum ? Maximum : date;

    /// <summary>
    /// <paramref name="culture"/> writing this calendar: a read-only copy with it when the culture offers it, else the calendar's own
    /// culture with it (kept for the last culture asked).
    /// </summary>
    public CultureInfo Culture(CultureInfo culture)
    {
        if (_culture is { } cached && ReferenceEquals(cached.Culture, culture))
            return cached.Formatting;
        var formatting = With(culture) ?? HomeCultureWith() ?? CultureInfo.InvariantCulture;
        _culture = (culture, formatting);
        return formatting;
    }

    private CultureInfo? HomeCultureWith()
    {
        try
        {
            return With(CultureInfo.GetCultureInfo(HomeCulture));
        }
        catch (CultureNotFoundException)
        {
            return null; // invariant globalization: no culture to write it but the invariant one
        }
    }

    private CultureInfo? With(CultureInfo culture)
    {
        if (culture.DateTimeFormat.Calendar.GetType() == Calendar.GetType())
            return culture;
        if (!culture.OptionalCalendars.Any(optional => optional.GetType() == Calendar.GetType()))
            return null;
        var copy = (CultureInfo)culture.Clone();
        copy.DateTimeFormat.Calendar = Calendar;
        return CultureInfo.ReadOnly(copy);
    }

    /// <summary>The year of <paramref name="date"/> as the culture writes it in this calendar (Hebrew numerals, an era for Japanese).</summary>
    public string YearText(DateTime date, CultureInfo culture) => date.ToString(IsEraCalendar ? "gg y" : "yyyy", culture);
}
