using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// The calendar's date math, shared by both layers and holding no state: where a page starts, how a page steps, which date a
/// cell holds, the span a month / year / decade cell covers, titles and week numbers. Gregorian dates without times; every
/// step stays inside <see cref="DateTime"/>'s range and reports when it would leave it.
/// </summary>
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

    /// <summary>The first day of the week: the set one, or the culture's.</summary>
    public static DayOfWeek FirstDayOfWeek(DayOfWeek? set, CultureInfo culture) => set ?? culture.DateTimeFormat.FirstDayOfWeek;

    /// <summary><paramref name="date"/> plus <paramref name="days"/>, or <c>null</c> outside <see cref="DateTime"/>'s range.</summary>
    public static DateTime? AddDays(DateTime date, long days)
    {
        var limit = days < 0 ? (date - First).Days : (Last - date).Days;
        return Math.Abs(days) > limit ? null : date.AddDays(days);
    }

    /// <summary><paramref name="date"/> plus <paramref name="months"/>, or <c>null</c> outside <see cref="DateTime"/>'s range.</summary>
    public static DateTime? AddMonths(DateTime date, long months)
    {
        var index = date.Year * 12L + date.Month - 1 + months;
        if (index < 12 || index > 9999 * 12L + 11)
            return null;
        return date.AddMonths((int)months);
    }

    /// <summary>The last day of the week holding <paramref name="date"/>'s first day of week, clamped to <see cref="DateTime"/>'s range.</summary>
    public static DateTime WeekStart(DateTime date, DayOfWeek first)
    {
        var back = ((int)date.DayOfWeek - (int)first + 7) % 7;
        return AddDays(date.Date, -back) ?? First;
    }

    /// <summary>The first day of the page of <paramref name="mode"/> that holds <paramref name="date"/>.</summary>
    public static DateTime PageStart(DateTime date, SkUiCalendarDisplayMode mode, int weeks, DayOfWeek first) => mode switch
    {
        SkUiCalendarDisplayMode.Month when weeks >= MaxWeeks => new DateTime(date.Year, date.Month, 1),
        SkUiCalendarDisplayMode.Month => WeekStart(date, first),
        SkUiCalendarDisplayMode.Year => new DateTime(date.Year, 1, 1),
        SkUiCalendarDisplayMode.Decade => new DateTime(Math.Max(1, date.Year / 10 * 10), 1, 1),
        _ => new DateTime(Math.Max(1, date.Year / 100 * 100), 1, 1)
    };

    /// <summary>The first day of the page <paramref name="pages"/> pages from the one starting at <paramref name="start"/>, or <c>null</c> outside the range of dates.</summary>
    public static DateTime? Step(DateTime start, SkUiCalendarDisplayMode mode, int weeks, int pages)
    {
        if (pages == 0)
            return start;
        var stepped = mode switch
        {
            SkUiCalendarDisplayMode.Month when weeks >= MaxWeeks => AddMonths(start, pages),
            SkUiCalendarDisplayMode.Month => AddDays(start, 7L * weeks * pages),
            SkUiCalendarDisplayMode.Year => AddMonths(start, 12L * pages),
            SkUiCalendarDisplayMode.Decade => AddMonths(start, 120L * pages),
            _ => AddMonths(start, 1200L * pages)
        };
        // Decade and century pages start on round years; the first ones start in year 1 (there is no year 0).
        return stepped is { } date && mode is SkUiCalendarDisplayMode.Decade or SkUiCalendarDisplayMode.Century
            ? PageStart(date, mode, weeks, DayOfWeek.Sunday)
            : stepped;
    }

    /// <summary>The last day of the page starting at <paramref name="start"/>.</summary>
    public static DateTime PageEnd(DateTime start, SkUiCalendarDisplayMode mode, int weeks) => mode switch
    {
        SkUiCalendarDisplayMode.Month when weeks >= MaxWeeks => AddMonths(start, 1)?.AddDays(-1) ?? Last,
        SkUiCalendarDisplayMode.Month => AddDays(start, 7L * weeks - 1) ?? Last,
        SkUiCalendarDisplayMode.Year => new DateTime(start.Year, 12, 31),
        SkUiCalendarDisplayMode.Decade => YearEnd(start.Year / 10 * 10 + 9),
        _ => YearEnd(start.Year / 100 * 100 + 99)
    };

    private static DateTime YearEnd(int year) => year >= 9999 ? Last : new DateTime(year, 12, 31);

    /// <summary>The first cell's date of a month page starting at <paramref name="start"/> (the week holding the month's first day).</summary>
    public static DateTime GridStart(DateTime start, int weeks, DayOfWeek first) => weeks >= MaxWeeks ? WeekStart(start, first) : start;

    /// <summary>The first day of a year / decade / century page's cell <paramref name="index"/> (0–11), or <c>null</c> outside the range of dates.</summary>
    public static DateTime? PeriodCell(DateTime start, SkUiCalendarDisplayMode mode, int index)
    {
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
    public static int PeriodIndex(DateTime start, SkUiCalendarDisplayMode mode, DateTime date)
    {
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
    public static DateTime PeriodEnd(DateTime cell, SkUiCalendarDisplayMode mode) => mode switch
    {
        SkUiCalendarDisplayMode.Year => AddMonths(cell, 1)?.AddDays(-1) ?? Last,
        SkUiCalendarDisplayMode.Decade => YearEnd(cell.Year),
        _ => YearEnd(cell.Year / 10 * 10 + 9)
    };

    /// <summary>The text of a year / decade / century page's cell starting at <paramref name="cell"/>.</summary>
    public static string PeriodText(DateTime cell, SkUiCalendarDisplayMode mode, CultureInfo culture) => mode switch
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
    public static string Title(DateTime start, SkUiCalendarDisplayMode mode, int weeks, string? format, CultureInfo culture)
    {
        switch (mode)
        {
            case SkUiCalendarDisplayMode.Month:
                var shown = weeks >= MaxWeeks ? start : AddDays(start, 7L * weeks / 2) ?? start;
                return shown.ToString(string.IsNullOrEmpty(format) ? culture.DateTimeFormat.YearMonthPattern : format, culture);
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
        return culture.Calendar.GetWeekOfYear(weekStart, rule, first);
    }
}
