using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// The calendar's date math, shared by both layers and holding no state: where a page starts, how a page steps, which date a
/// cell holds, the span a month / year / decade cell covers, titles and week numbers. Dates without times; every step stays
/// inside <see cref="DateTime"/>'s range (and a calendar system's) and reports when it would leave it.
/// </summary>
/// <remarks>
/// The methods that depend on months and years take an optional <see cref="SkUiCalendarSystem"/> (1.15): <c>null</c> is the
/// Gregorian calendar, on the code 1.13 shipped; a system cuts the pages by its months and years. Weeks are days either way.
/// </remarks>
internal static class SkUiCalendarGrid
{
    /// <summary>Cells on a year, decade or century page (4 rows of 3).</summary>
    public const int PeriodCells = 12;

    /// <summary>Columns of a year, decade or century page.</summary>
    public const int PeriodColumns = 3;

    /// <summary>Rows of a year, decade or century page.</summary>
    public const int PeriodRows = 4;

    /// <summary>The most weeks a month page shows.</summary>
    public const int MaxWeeks = 6;

    private static readonly DateTime First = DateTime.MinValue.Date;
    private static readonly DateTime Last = DateTime.MaxValue.Date;

    private static GregorianCulture? _gregorian;

    private sealed record GregorianCulture(CultureInfo Culture, CultureInfo Gregorian);

    /// <summary>
    /// <paramref name="culture"/> formatting dates in the Gregorian calendar the grid is laid out in: itself when it already does,
    /// else a read-only copy with its Gregorian calendar (Thai Buddhist and Hijri cultures), or the invariant culture without one.
    /// </summary>
    public static CultureInfo Gregorian(CultureInfo culture)
    {
        if (culture.DateTimeFormat.Calendar is GregorianCalendar)
            return culture;
        if (_gregorian is { } cached && ReferenceEquals(cached.Culture, culture))
            return cached.Gregorian;
        var gregorian = CultureInfo.InvariantCulture;
        if (culture.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault() is { } calendar)
        {
            var copy = (CultureInfo)culture.Clone();
            copy.DateTimeFormat.Calendar = calendar;
            gregorian = CultureInfo.ReadOnly(copy);
        }
        _gregorian = new GregorianCulture(culture, gregorian);
        return gregorian;
    }

    /// <summary>The first day of the week: the set one, or the culture's.</summary>
    public static DayOfWeek FirstDayOfWeek(DayOfWeek? set, CultureInfo culture) => set ?? culture.DateTimeFormat.FirstDayOfWeek;

    /// <summary><paramref name="date"/> plus <paramref name="days"/>, or <c>null</c> outside <see cref="DateTime"/>'s range.</summary>
    public static DateTime? AddDays(DateTime date, long days)
    {
        var limit = days < 0 ? (date - First).Days : (Last - date).Days;
        return Math.Abs(days) > limit ? null : date.AddDays(days);
    }

    /// <summary><paramref name="date"/> plus <paramref name="months"/>, or <c>null</c> outside <see cref="DateTime"/>'s range (or the system's).</summary>
    public static DateTime? AddMonths(DateTime date, long months, SkUiCalendarSystem? system = null)
    {
        if (system is not null)
            return system.AddMonths(date, months);
        var index = date.Year * 12L + date.Month - 1 + months;
        if (index < 12 || index > 9999 * 12L + 11)
            return null;
        return date.AddMonths((int)months);
    }

    /// <summary>
    /// <paramref name="date"/> <paramref name="years"/> years away, the same month and day (or the month's last), or <c>null</c>
    /// outside the range: by twelve months on the Gregorian calendar, by the calendar's years on a system (a Hebrew year has 12 or 13 months).
    /// </summary>
    public static DateTime? AddYears(DateTime date, long years, SkUiCalendarSystem? system = null)
    {
        if (system is null)
            return AddMonths(date, 12 * years);
        var year = system.Year(date) + years;
        if (year < system.MinimumYear || year > system.MaximumYear)
            return null;
        // A month the first or last year does not have: as far as the calendar goes (its first or last day).
        return system.Date(year, system.SameMonth(system.Month(date), system.Year(date), (int)year), system.Day(date))
            ?? (years < 0 ? system.Minimum : system.Maximum);
    }

    /// <summary>The last day of the week holding <paramref name="date"/>'s first day of week, clamped to <see cref="DateTime"/>'s range.</summary>
    public static DateTime WeekStart(DateTime date, DayOfWeek first)
    {
        var back = ((int)date.DayOfWeek - (int)first + 7) % 7;
        return AddDays(date.Date, -back) ?? First;
    }

    /// <summary>The first day of the page of <paramref name="mode"/> that holds <paramref name="date"/>.</summary>
    public static DateTime PageStart(DateTime date, SkUiCalendarDisplayMode mode, int weeks, DayOfWeek first, SkUiCalendarSystem? system = null) => system is not null
        ? mode switch
        {
            SkUiCalendarDisplayMode.Month when weeks >= MaxWeeks => system.MonthStart(system.Clamp(date)),
            SkUiCalendarDisplayMode.Month => WeekStart(system.Clamp(date), first),
            SkUiCalendarDisplayMode.Year => system.YearStart(system.Year(system.Clamp(date))) ?? system.Minimum,
            SkUiCalendarDisplayMode.Decade => system.YearStart(Math.Max(system.MinimumYear, system.Year(system.Clamp(date)) / 10 * 10)) ?? system.Minimum,
            _ => system.YearStart(Math.Max(system.MinimumYear, system.Year(system.Clamp(date)) / 100 * 100)) ?? system.Minimum
        }
        : mode switch
    {
        SkUiCalendarDisplayMode.Month when weeks >= MaxWeeks => new DateTime(date.Year, date.Month, 1),
        SkUiCalendarDisplayMode.Month => WeekStart(date, first),
        SkUiCalendarDisplayMode.Year => new DateTime(date.Year, 1, 1),
        SkUiCalendarDisplayMode.Decade => new DateTime(Math.Max(1, date.Year / 10 * 10), 1, 1),
        _ => new DateTime(Math.Max(1, date.Year / 100 * 100), 1, 1)
    };

    /// <summary>The first day of the page <paramref name="pages"/> pages from the one starting at <paramref name="start"/>, or <c>null</c> outside the range of dates.</summary>
    public static DateTime? Step(DateTime start, SkUiCalendarDisplayMode mode, int weeks, int pages, SkUiCalendarSystem? system = null)
    {
        if (pages == 0)
            return start;
        if (system is not null)
        {
            var step = mode switch
            {
                SkUiCalendarDisplayMode.Month when weeks >= MaxWeeks => system.AddMonths(system.MonthStart(start), pages) is { } month
                    ? system.MonthStart(month) : null,
                SkUiCalendarDisplayMode.Month => AddDays(start, 7L * weeks * pages),
                SkUiCalendarDisplayMode.Year => system.YearStart(system.Year(start) + (long)pages),
                SkUiCalendarDisplayMode.Decade => SystemStepYears(system, start, 10, pages),
                _ => SystemStepYears(system, start, 100, pages)
            };
            // A week page may start before the calendar's first day (its first week): it is a page while it reaches that day.
            var reaches = mode == SkUiCalendarDisplayMode.Month && weeks < MaxWeeks && step is { } week && AddDays(week, 7L * weeks - 1) >= system.Minimum;
            return step is { } date && (date >= system.Minimum || reaches) && date <= system.Maximum ? date : null;
        }
        return mode switch
        {
            SkUiCalendarDisplayMode.Month when weeks >= MaxWeeks => AddMonths(start, pages),
            SkUiCalendarDisplayMode.Month => AddDays(start, 7L * weeks * pages),
            SkUiCalendarDisplayMode.Year => AddMonths(start, 12L * pages),
            SkUiCalendarDisplayMode.Decade => StepYears(start, 10, pages),
            _ => StepYears(start, 100, pages)
        };
    }

    /// <summary>
    /// A decade or century page <paramref name="pages"/> pages away. Pages start on round years, counted from year 0; the first page
    /// starts in year 1 (there is no year 0), so it is reached from the next one too.
    /// </summary>
    private static DateTime? StepYears(DateTime start, int span, int pages)
    {
        var year = start.Year / span * span + (long)span * pages;
        return year + span - 1 < 1 || year > 9999 ? null : new DateTime((int)Math.Max(1, year), 1, 1);
    }

    /// <summary>A decade or century page of a calendar system <paramref name="pages"/> pages away (its first page starts at its first year).</summary>
    private static DateTime? SystemStepYears(SkUiCalendarSystem system, DateTime start, int span, int pages)
    {
        var year = (long)system.Year(start) / span * span + (long)span * pages;
        return year + span - 1 < system.MinimumYear ? null : system.YearStart(Math.Max(system.MinimumYear, year));
    }

    /// <summary>The last day of the page starting at <paramref name="start"/>.</summary>
    public static DateTime PageEnd(DateTime start, SkUiCalendarDisplayMode mode, int weeks, SkUiCalendarSystem? system = null) => system is not null
        ? mode switch
        {
            SkUiCalendarDisplayMode.Month when weeks >= MaxWeeks => system.AddMonths(system.MonthStart(start), 1) is { } next
                ? system.MonthStart(next).AddDays(-1) : system.Maximum,
            SkUiCalendarDisplayMode.Month => AddDays(start, 7L * weeks - 1) ?? Last,
            SkUiCalendarDisplayMode.Year => SystemYearEnd(system, system.Year(start)),
            SkUiCalendarDisplayMode.Decade => SystemYearEnd(system, system.Year(start) / 10 * 10 + 9),
            _ => SystemYearEnd(system, system.Year(start) / 100 * 100 + 99)
        }
        : mode switch
    {
        SkUiCalendarDisplayMode.Month when weeks >= MaxWeeks => AddMonths(start, 1)?.AddDays(-1) ?? Last,
        SkUiCalendarDisplayMode.Month => AddDays(start, 7L * weeks - 1) ?? Last,
        SkUiCalendarDisplayMode.Year => new DateTime(start.Year, 12, 31),
        SkUiCalendarDisplayMode.Decade => YearEnd(start.Year / 10 * 10 + 9),
        _ => YearEnd(start.Year / 100 * 100 + 99)
    };

    private static DateTime YearEnd(int year) => year >= 9999 ? Last : new DateTime(year, 12, 31);

    /// <summary>The last day of a calendar system's <paramref name="year"/>, or its last supported day.</summary>
    private static DateTime SystemYearEnd(SkUiCalendarSystem system, long year) =>
        year >= system.MaximumYear ? system.Maximum : system.YearStart(year + 1) is { } next ? next.AddDays(-1) : system.Maximum;

    /// <summary>The cells of a year, decade or century page: 12, or 15 (5 rows of 3) on the year page of a calendar with 13 months.</summary>
    public static int PeriodCellCount(SkUiCalendarDisplayMode mode, SkUiCalendarSystem? system = null) => PeriodRowCount(mode, system) * PeriodColumns;

    /// <summary>The rows of a year, decade or century page: 4, or 5 on the year page of a calendar with 13 months (Hebrew).</summary>
    public static int PeriodRowCount(SkUiCalendarDisplayMode mode, SkUiCalendarSystem? system = null) =>
        mode == SkUiCalendarDisplayMode.Year && system is { MaxMonthsInYear: > PeriodCells } ? PeriodRows + 1 : PeriodRows;

    /// <summary>Whether <paramref name="day"/> is in another month than the month page starting at <paramref name="start"/>.</summary>
    public static bool IsOtherMonth(DateTime start, DateTime day, SkUiCalendarSystem? system = null) => system is null
        ? day.Month != start.Month || day.Year != start.Year
        : system.Month(day) != system.Month(start) || system.Year(day) != system.Year(start);

    /// <summary>The day of <paramref name="date"/>'s month, the number its cell shows.</summary>
    public static int DayOfMonth(DateTime date, SkUiCalendarSystem? system = null) => system?.Day(date) ?? date.Day;

    /// <summary><paramref name="focus"/> moved by <paramref name="cells"/> cells of a page of <paramref name="mode"/>: days, months, years or decades.</summary>
    public static DateTime? MoveCells(DateTime focus, SkUiCalendarDisplayMode mode, int cells, SkUiCalendarSystem? system = null)
    {
        if (system is null)
            return mode switch
            {
                SkUiCalendarDisplayMode.Month => AddDays(focus, cells),
                SkUiCalendarDisplayMode.Year => AddMonths(new DateTime(focus.Year, focus.Month, 1), cells),
                SkUiCalendarDisplayMode.Decade => AddMonths(new DateTime(focus.Year, 1, 1), 12L * cells),
                _ => AddMonths(new DateTime(Math.Max(1, focus.Year / 10 * 10), 1, 1), 120L * cells)
            };
        return mode switch
        {
            SkUiCalendarDisplayMode.Month => AddDays(focus, cells) is { } day && day >= system.Minimum && day <= system.Maximum ? day : null,
            SkUiCalendarDisplayMode.Year => system.AddMonths(system.MonthStart(focus), cells) is { } month ? system.MonthStart(month) : EdgeMonth(focus, cells, system),
            SkUiCalendarDisplayMode.Decade => system.YearStart(system.Year(focus) + (long)cells),
            _ => system.YearStart(Math.Max(system.MinimumYear, (long)system.Year(focus) / 10 * 10 + 10L * cells))
        };
    }

    /// <summary>
    /// The calendar's first or last month (by the direction of <paramref name="cells"/>) when a move of the focus by months runs past
    /// it, unless the focus is already in it: a year page's cells move up to a partly supported first or last year.
    /// </summary>
    private static DateTime? EdgeMonth(DateTime focus, int cells, SkUiCalendarSystem system)
    {
        var edge = system.MonthStart(cells < 0 ? system.Minimum : system.Maximum);
        return edge == system.MonthStart(focus) ? null : edge;
    }

    /// <summary>
    /// A decade or century page's cell <paramref name="years"/> years away, as <see cref="Step"/> pages: a cell before the first year
    /// lands on the first year while it is on the first page, and nothing comes before that page.
    /// </summary>
    public static DateTime? MoveYears(DateTime focus, long years, SkUiCalendarSystem? system = null)
    {
        if (system is null)
        {
            var year = focus.Year + years;
            if (year > 9999)
                return null;
            if (year >= 1)
                return new DateTime((int)year, focus.Month, 1);
            return focus.Year < -years ? null : new DateTime(1, 1, 1);
        }
        var target = system.Year(focus) + years;
        if (target > system.MaximumYear)
            return null;
        if (target >= system.MinimumYear)
            return system.YearStart(target);
        return system.Year(focus) - system.MinimumYear < -years ? null : system.Minimum;
    }

    /// <summary>The first cell's date of a month page starting at <paramref name="start"/> (the week holding the month's first day).</summary>
    public static DateTime GridStart(DateTime start, int weeks, DayOfWeek first) => weeks >= MaxWeeks ? WeekStart(start, first) : start;

    /// <summary>The first day of a year / decade / century page's cell <paramref name="index"/> (0–11), or <c>null</c> outside the range of dates.</summary>
    public static DateTime? PeriodCell(DateTime start, SkUiCalendarDisplayMode mode, int index, SkUiCalendarSystem? system = null)
    {
        if (system is not null)
        {
            var year = system.Year(start);
            switch (mode)
            {
                case SkUiCalendarDisplayMode.Year:
                    // A month the calendar does not support at all (before its first day or after its last) has no cell.
                    if (index >= system.MonthsInYear(year) || system.Date(year, index + 1, 1) is not { } month)
                        return null;
                    return SystemPeriodEnd(system, month, mode) < system.Minimum || month > system.Maximum ? null : month;
                case SkUiCalendarDisplayMode.Decade:
                    return system.YearStart(year / 10 * 10 - 1 + index);
                default:
                {
                    var decade = year / 100 * 100 - 10 + 10 * index;
                    return decade + 9 < system.MinimumYear ? null : system.YearStart(Math.Max(system.MinimumYear, decade));
                }
            }
        }
        switch (mode)
        {
            case SkUiCalendarDisplayMode.Year:
                return new DateTime(start.Year, index + 1, 1);
            case SkUiCalendarDisplayMode.Decade:
            {
                // The decade's ten years with the year before and the year after it.
                var year = start.Year / 10 * 10 - 1 + index;
                return year is >= 1 and <= 9999 ? new DateTime(year, 1, 1) : null;
            }
            default:
            {
                var year = start.Year / 100 * 100 - 10 + 10 * index;
                return year is >= 0 and <= 9999 ? new DateTime(Math.Max(1, year), 1, 1) : null;
            }
        }
    }

    /// <summary>Whether a decade / century page's cell <paramref name="index"/> is outside the decade or century (the dimmed first and last cells).</summary>
    public static bool IsAdjacentPeriodCell(SkUiCalendarDisplayMode mode, int index) =>
        mode is SkUiCalendarDisplayMode.Decade or SkUiCalendarDisplayMode.Century && index is 0 or PeriodCells - 1;

    /// <summary>The page index (0–11) of the cell holding <paramref name="date"/> on the page of <paramref name="mode"/> starting at <paramref name="start"/>, or -1.</summary>
    public static int PeriodIndex(DateTime start, SkUiCalendarDisplayMode mode, DateTime date, SkUiCalendarSystem? system = null)
    {
        if (system is not null)
        {
            var (startYear, year) = (system.Year(start), system.Year(date));
            var at = mode switch
            {
                SkUiCalendarDisplayMode.Year => year == startYear ? system.Month(date) - 1 : -1,
                SkUiCalendarDisplayMode.Decade => year - (startYear / 10 * 10 - 1),
                _ => year - (startYear / 100 * 100 - 10) is var offset && offset >= 0 ? offset / 10 : -1
            };
            return at >= 0 && at < PeriodCellCount(mode, system) ? at : -1;
        }
        var index = mode switch
        {
            SkUiCalendarDisplayMode.Year => date.Year == start.Year ? date.Month - 1 : -1,
            SkUiCalendarDisplayMode.Decade => date.Year - (start.Year / 10 * 10 - 1),
            // The century's first cell is the decade before it (its base year may be -10 for the first century).
            _ => (date.Year - (start.Year / 100 * 100 - 10)) / 10
        };
        return index is >= 0 and < PeriodCells ? index : -1;
    }

    /// <summary>The last day of the period a year / decade / century page's cell starting at <paramref name="cell"/> covers.</summary>
    public static DateTime PeriodEnd(DateTime cell, SkUiCalendarDisplayMode mode, SkUiCalendarSystem? system = null) => system is not null
        ? SystemPeriodEnd(system, cell, mode)
        : mode switch
    {
        SkUiCalendarDisplayMode.Year => AddMonths(cell, 1)?.AddDays(-1) ?? Last,
        SkUiCalendarDisplayMode.Decade => YearEnd(cell.Year),
        _ => YearEnd(cell.Year / 10 * 10 + 9)
    };

    private static DateTime SystemPeriodEnd(SkUiCalendarSystem system, DateTime cell, SkUiCalendarDisplayMode mode) => mode switch
    {
        SkUiCalendarDisplayMode.Year => system.AddMonths(system.MonthStart(cell), 1) is { } next ? system.MonthStart(next).AddDays(-1) : system.Maximum,
        SkUiCalendarDisplayMode.Decade => SystemYearEnd(system, system.Year(cell)),
        _ => SystemYearEnd(system, system.Year(cell) / 10 * 10 + 9)
    };

    /// <summary>The text of a year / decade / century page's cell starting at <paramref name="cell"/>.</summary>
    public static string PeriodText(DateTime cell, SkUiCalendarDisplayMode mode, CultureInfo culture, SkUiCalendarSystem? system = null) => system is not null
        ? mode switch
        {
            // The month's name as the calendar writes it (a Hebrew leap year's Adar I and II, from the date).
            SkUiCalendarDisplayMode.Year => cell.ToString("MMM", culture),
            SkUiCalendarDisplayMode.Decade => system.YearText(cell, culture),
            _ => system.YearText(system.YearStart(system.Year(cell) / 10 * 10) ?? cell, culture)
        }
        : mode switch
    {
        SkUiCalendarDisplayMode.Year => culture.DateTimeFormat.GetAbbreviatedMonthName(cell.Month),
        SkUiCalendarDisplayMode.Decade => cell.Year.ToString(culture),
        _ => (cell.Year / 10 * 10).ToString(culture) // the first decade starts in year 1 but is named 0
    };

    /// <summary>
    /// The header's title for the page of <paramref name="mode"/> starting at <paramref name="start"/>: the month and year (the
    /// culture's year-month pattern, or <paramref name="format"/>), the year, or the decade's or century's first and last years.
    /// A page of weeks is named after the month most of its days are in.
    /// </summary>
    public static string Title(DateTime start, SkUiCalendarDisplayMode mode, int weeks, string? format, CultureInfo culture, SkUiCalendarSystem? system = null)
    {
        if (system is not null && mode != SkUiCalendarDisplayMode.Month)
        {
            var span = mode switch { SkUiCalendarDisplayMode.Year => 1, SkUiCalendarDisplayMode.Decade => 10, _ => 100 };
            if (span == 1)
                return system.YearText(start, culture);
            var first = system.Year(start) / span * span;
            var firstDay = system.YearStart(Math.Max(system.MinimumYear, first)) ?? start;
            var lastDay = system.YearStart(Math.Min(system.MaximumYear, first + span - 1)) ?? system.Maximum;
            return string.Concat(system.YearText(firstDay, culture), " – ", system.YearText(lastDay, culture));
        }
        switch (mode)
        {
            case SkUiCalendarDisplayMode.Month:
                var shown = weeks >= MaxWeeks ? start : AddDays(start, 7L * weeks / 2) ?? start;
                if (system is not null)
                    shown = system.Clamp(shown); // a first week may start before the calendar's first day, which it cannot write
                return shown.ToString(string.IsNullOrEmpty(format) ? YearMonthPattern(culture, system) : format, culture);
            case SkUiCalendarDisplayMode.Year:
                return start.Year.ToString(culture);
            case SkUiCalendarDisplayMode.Decade:
            {
                var first = start.Year / 10 * 10;
                return string.Concat(Math.Max(1, first).ToString(culture), " – ", (first + 9).ToString(culture));
            }
            default:
            {
                var first = start.Year / 100 * 100;
                return string.Concat(Math.Max(1, first).ToString(culture), " – ", (first + 99).ToString(culture));
            }
        }
    }

    /// <summary>
    /// The culture's year-month pattern; with an era calendar (Japanese) whose pattern names no era, the era before the year, since
    /// its years start again with each era ("令和8年11月", not "08年11月").
    /// </summary>
    internal static string YearMonthPattern(CultureInfo culture, SkUiCalendarSystem? system) => WithEra(culture.DateTimeFormat.YearMonthPattern, system);

    /// <summary>The culture's long date pattern ("D"), with the era for an era calendar (as <see cref="YearMonthPattern"/>).</summary>
    internal static string LongDatePattern(CultureInfo culture, SkUiCalendarSystem? system) => WithEra(culture.DateTimeFormat.LongDatePattern, system);

    private static string WithEra(string pattern, SkUiCalendarSystem? system) =>
        system is not { IsEraCalendar: true } || pattern.Contains('g') ? pattern : "gg" + pattern.Replace("yyyy", "y").Replace("yy", "y");

    /// <summary>The name of a day of the week above its column.</summary>
    public static string DayName(DayOfWeek day, SkUiCalendarDayNameFormat format, CultureInfo culture)
    {
        var names = culture.DateTimeFormat;
        return format switch
        {
            SkUiCalendarDayNameFormat.Abbreviated => names.GetAbbreviatedDayName(day),
            SkUiCalendarDayNameFormat.FirstLetter => FirstLetter(names.GetShortestDayName(day)),
            _ => names.GetShortestDayName(day)
        };
    }

    private static string FirstLetter(string name)
    {
        if (name.Length == 0)
            return name;
        var length = char.IsSurrogatePair(name, 0) ? 2 : 1;
        return name.Length == length ? name : name[..length];
    }

    /// <summary>
    /// The week number of the week starting at <paramref name="weekStart"/>: ISO 8601 for Monday weeks under the culture's
    /// "first four-day week" rule (the culture's calendar gets the last days of December wrong there), else the culture's rule.
    /// </summary>
    public static int WeekNumber(DateTime weekStart, DayOfWeek first, CultureInfo culture)
    {
        var rule = culture.DateTimeFormat.CalendarWeekRule;
        if (first == DayOfWeek.Monday && rule == CalendarWeekRule.FirstFourDayWeek)
            return ISOWeek.GetWeekOfYear(weekStart);
        // The week's number is the one of its last day that is still in the same year as the first: GetWeekOfYear numbers by date.
        return culture.DateTimeFormat.Calendar.GetWeekOfYear(weekStart, rule, first);
    }
}
