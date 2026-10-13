using System.Globalization;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The date and time pickers' wheels without a picker (<see cref="SkUiDateTimeWheels"/>): which wheels the culture and the wheel
/// format ask for, their texts, the grid of values (steps, parts without a wheel), the range on the grid, which items can be
/// chosen next to the others, and where a choice that cannot be made moves.
/// </summary>
public sealed class DateTimeWheelsTests
{
    /// <summary>Sets the current culture until disposed.</summary>
    internal static IDisposable UseCulture(string name)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        return new Restore(() => CultureInfo.CurrentCulture = previous);
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }

    private static SkUiDateTimeWheels Wheels(SkUiDateTimeKind kind, string? format = null, DateTime? minimum = null, DateTime? maximum = null,
        DateTime? value = null, DateTime? highlighted = null, int minuteInterval = 1, IEnumerable<DateTime>? blackout = null,
        Func<DateTime, bool>? enabled = null, string culture = "en-US", int hourInterval = 1) =>
        SkUiDateTimeWheels.Create(new SkUiDateTimeSettings(kind, format,
            minimum ?? (kind == SkUiDateTimeKind.Time ? SkUiDateTimeWheels.TimeDate : new DateTime(1900, 1, 1)),
            maximum ?? (kind == SkUiDateTimeKind.Time ? SkUiDateTimeWheels.TimeDate.AddDays(1).AddSeconds(-1) : new DateTime(2100, 12, 31, 23, 59, 59)),
            value, highlighted, hourInterval, minuteInterval, 1, blackout, enabled), new CultureInfo(culture), new SkUiStrings());

    private static SkUiDateTimeUnit[] Units(SkUiDateTimeWheels wheels) => [.. wheels.Columns.Select(column => column.Unit)];

    private static readonly DateTime March20 = new(2020, 3, 20);

    [Fact]
    public void TheCultureOrdersAndNamesTheDateWheels()
    {
        var english = Wheels(SkUiDateTimeKind.Date, culture: "en-US");
        Assert.Equal([SkUiDateTimeUnit.Month, SkUiDateTimeUnit.Day, SkUiDateTimeUnit.Year], Units(english));
        Assert.Equal("October", english.Texts[0][9]);
        Assert.Equal("1900", english.Texts[2][0]);
        Assert.Equal(201, english.Texts[2].Length);
        Assert.Equal([true, true, false], english.Loops); // years never loop

        Assert.Equal([SkUiDateTimeUnit.Day, SkUiDateTimeUnit.Month, SkUiDateTimeUnit.Year], Units(Wheels(SkUiDateTimeKind.Date, culture: "pl-PL")));
        Assert.Equal([SkUiDateTimeUnit.Year, SkUiDateTimeUnit.Month, SkUiDateTimeUnit.Day], Units(Wheels(SkUiDateTimeKind.Date, culture: "ja-JP")));

        // Next to a time, abbreviated month names.
        var both = Wheels(SkUiDateTimeKind.DateAndTime);
        Assert.Equal("Oct", both.Texts[0][9]);
    }

    [Fact]
    public void TheCultureChoosesTheClock()
    {
        var english = Wheels(SkUiDateTimeKind.Time, culture: "en-US");
        Assert.Equal([SkUiDateTimeUnit.Hour, SkUiDateTimeUnit.Minute, SkUiDateTimeUnit.Half], Units(english));
        Assert.Equal(["12", "1", "2"], english.Texts[0].Take(3));
        Assert.Equal(["AM", "PM"], english.Texts[2]);
        Assert.False(english.Is24Hour);

        var polish = Wheels(SkUiDateTimeKind.Time, culture: "pl-PL");
        Assert.Equal([SkUiDateTimeUnit.Hour, SkUiDateTimeUnit.Minute], Units(polish));
        Assert.Equal("00", polish.Texts[0][0]);
        Assert.True(polish.Is24Hour);

        // A 12-hour format without AM / PM still gets its wheel; a 24-hour one with it does not.
        Assert.Equal([SkUiDateTimeUnit.Hour, SkUiDateTimeUnit.Minute, SkUiDateTimeUnit.Half], Units(Wheels(SkUiDateTimeKind.Time, "h:mm")));
        Assert.Equal([SkUiDateTimeUnit.Hour, SkUiDateTimeUnit.Minute], Units(Wheels(SkUiDateTimeKind.Time, "HH:mm tt")));
    }

    [Fact]
    public void TheWheelFormatPicksTheWheels()
    {
        // Standard formats are the culture's patterns; a weekday and separators are no wheels.
        Assert.Equal([SkUiDateTimeUnit.Month, SkUiDateTimeUnit.Day, SkUiDateTimeUnit.Year], Units(Wheels(SkUiDateTimeKind.Date, "D")));
        Assert.Equal([SkUiDateTimeUnit.Hour, SkUiDateTimeUnit.Minute, SkUiDateTimeUnit.Second, SkUiDateTimeUnit.Half],
            Units(Wheels(SkUiDateTimeKind.Time, "T")));

        var expiry = Wheels(SkUiDateTimeKind.Date, "MM/yy", minimum: new DateTime(2026, 1, 1), maximum: new DateTime(2036, 12, 31));
        Assert.Equal([SkUiDateTimeUnit.Month, SkUiDateTimeUnit.Year], Units(expiry));
        Assert.Equal("03", expiry.Texts[0][2]);
        Assert.Equal("26", expiry.Texts[1][0]);
        // Parts below the smallest wheel are their least: the 1st of the month.
        Assert.Equal(new DateTime(2027, 3, 1), expiry.ValueOf([2, 1]));

        // A part the picker does not choose is no wheel: a time in a date picker; none left: the culture's.
        Assert.Equal(3, Wheels(SkUiDateTimeKind.Date, "HH:mm").Columns.Count);
        Assert.Equal([SkUiDateTimeUnit.Year], Units(Wheels(SkUiDateTimeKind.Date, "'Year' yyyy")));
    }

    [Fact]
    public void ATimePickersDateAndPartsAboveItsWheelsStay()
    {
        var time = Wheels(SkUiDateTimeKind.Time, "HH:mm", value: SkUiDateTimeWheels.TimeDate + new TimeSpan(7, 30, 45));
        Assert.Equal(SkUiDateTimeWheels.TimeDate + new TimeSpan(9, 5, 0), time.ValueOf([9, 5]));

        // A date and time picker without a year wheel keeps the value's year.
        var noYear = Wheels(SkUiDateTimeKind.DateAndTime, "dd MMM HH:mm", value: new DateTime(2026, 5, 1, 8, 0, 0));
        Assert.Equal(new DateTime(2026, 6, 3, 10, 15, 0), noYear.ValueOf([2, 5, 10, 15]));
    }

    [Fact]
    public void StepsMakeTheGrid()
    {
        var wheels = Wheels(SkUiDateTimeKind.Time, "HH:mm", minuteInterval: 15, hourInterval: 2);
        Assert.Equal(["00", "15", "30", "45"], wheels.Texts[1]);
        Assert.Equal(12, wheels.Texts[0].Length);
        // A time between the steps shows the step before it.
        Assert.Equal([5, 0], wheels.IndicesOf(SkUiDateTimeWheels.TimeDate + new TimeSpan(10, 7, 0)));

        // The range on the grid: 9:10 is not a step, 9:15 the first after it.
        var ranged = Wheels(SkUiDateTimeKind.Time, "HH:mm", minuteInterval: 15, minimum: SkUiDateTimeWheels.TimeDate + new TimeSpan(9, 10, 0),
            maximum: SkUiDateTimeWheels.TimeDate + new TimeSpan(17, 50, 0));
        Assert.Equal(new TimeSpan(9, 15, 0), ranged.Minimum.TimeOfDay);
        Assert.Equal(new TimeSpan(17, 45, 0), ranged.Maximum.TimeOfDay);
        Assert.Null(ranged.IndicesOfChoice(SkUiDateTimeWheels.TimeDate + new TimeSpan(8, 0, 0)));
        Assert.Equal([10, 2], ranged.IndicesOfChoice(SkUiDateTimeWheels.TimeDate + new TimeSpan(10, 40, 0))!); // rounded down
    }

    [Fact]
    public void DaysAMonthDoesNotHaveCannotBeChosen()
    {
        var wheels = Wheels(SkUiDateTimeKind.Date, "dd MM yyyy");
        int[] february = [30, 1, 2020 - 1900]; // the 31st, February 2020
        var days = wheels.ItemStates(0, february)!;
        Assert.True(days[28]); // the 29th of a leap year
        Assert.False(days[29]);
        Assert.False(days[30]);

        // The 31st of a month turned February: its last day.
        Assert.Equal([28, 1, 120], wheels.Coerce(february));
        Assert.Equal(new DateTime(2020, 2, 29), wheels.ValueOf(wheels.Coerce(february)));
    }

    [Fact]
    public void TheRangeDimsWhatIsOutsideIt()
    {
        var wheels = Wheels(SkUiDateTimeKind.Date, "dd MM yyyy", minimum: new DateTime(2020, 3, 15), maximum: new DateTime(2021, 6, 30));
        var march2020 = wheels.IndicesOf(March20);
        Assert.Equal([19, 2, 0], march2020); // the years start at the minimum's

        var months = wheels.ItemStates(1, march2020)!;
        Assert.Equal([false, false, true, true], months.Take(4));
        var days = wheels.ItemStates(0, march2020)!;
        Assert.False(days[13]);
        Assert.True(days[14]);
        Assert.Null(wheels.ItemStates(2, march2020)); // both years can be chosen

        // In June 2021 the days after the 30th are gone (the month's own) and July on is past the maximum.
        var june2021 = wheels.IndicesOf(new DateTime(2021, 6, 10));
        Assert.False(wheels.ItemStates(1, june2021)![6]);

        // A date before the minimum moves to it.
        Assert.Equal(new DateTime(2020, 3, 15), wheels.ValueOf(wheels.Coerce([9, 2, 0])));
    }

    [Fact]
    public void TheTimeOfTheRangesEndsCounts()
    {
        var wheels = Wheels(SkUiDateTimeKind.DateAndTime, "dd MM yyyy HH:mm", minimum: new DateTime(2026, 10, 13, 13, 30, 0),
            maximum: new DateTime(2026, 12, 31, 18, 0, 0));
        var first = wheels.IndicesOf(new DateTime(2026, 10, 13, 15, 0, 0));
        var hours = wheels.ItemStates(3, first)!;
        Assert.False(hours[12]);
        Assert.True(hours[13]);
        // The next day every hour.
        Assert.Null(wheels.ItemStates(3, wheels.IndicesOf(new DateTime(2026, 10, 14, 15, 0, 0))));
        // 13:00 on the first day is too early: 13:30.
        Assert.Equal(new DateTime(2026, 10, 13, 13, 30, 0), wheels.ValueOf(wheels.Coerce(wheels.IndicesOf(new DateTime(2026, 10, 13, 13, 0, 0)))));
    }

    [Fact]
    public void AmOrPmOutsideTheRangeIsDimmed()
    {
        var afternoon = Wheels(SkUiDateTimeKind.Time, "h:mm tt", minimum: SkUiDateTimeWheels.TimeDate + new TimeSpan(13, 0, 0));
        var states = afternoon.ItemStates(2, afternoon.IndicesOf(SkUiDateTimeWheels.TimeDate + new TimeSpan(15, 0, 0)))!;
        Assert.Equal([false, true], states);
        // 3 AM is before the minimum: 1 PM.
        Assert.Equal(new TimeSpan(13, 0, 0), afternoon.ValueOf(afternoon.Coerce([3, 0, 0]))!.Value.TimeOfDay);
    }

    [Fact]
    public void BlackoutDatesAndTheAppsRuleMoveAChoiceToTheNearestDay()
    {
        var saturday = new DateTime(2026, 10, 17);
        var weekdays = Wheels(SkUiDateTimeKind.Date, "dd MM yyyy", enabled: date => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
            blackout: [new DateTime(2026, 10, 16)]);
        var states = weekdays.ItemStates(0, weekdays.IndicesOf(saturday))!;
        Assert.False(states[15]); // Friday the 16th: a blackout date
        Assert.False(states[16]);
        Assert.True(states[18]); // Monday

        // Saturday: Friday is out, Thursday and Monday are as near (the earlier first); Sunday the 25th: Monday is nearest.
        Assert.Equal(new DateTime(2026, 10, 15), weekdays.ValueOf(weekdays.Coerce(weekdays.IndicesOf(saturday))));
        Assert.Equal(new DateTime(2026, 10, 26), weekdays.ValueOf(weekdays.Coerce(weekdays.IndicesOf(new DateTime(2026, 10, 25)))));
        Assert.False(weekdays.IsDateEnabled(saturday));
        Assert.Null(weekdays.IndicesOfChoice(saturday));
    }

    [Fact]
    public void WithNothingChosenTheWheelsShowTheHighlightedValueInTheRange()
    {
        var birth = Wheels(SkUiDateTimeKind.Date, highlighted: new DateTime(1990, 6, 15));
        Assert.Equal(new DateTime(1990, 6, 15), birth.Highlighted);
        Assert.Equal([-1, -1, -1], birth.IndicesOf(null));

        var future = Wheels(SkUiDateTimeKind.Date, minimum: new DateTime(2030, 1, 1), highlighted: new DateTime(2020, 1, 1));
        Assert.Equal(new DateTime(2030, 1, 1), future.Highlighted);

        var quarter = Wheels(SkUiDateTimeKind.Time, "HH:mm", minuteInterval: 15, highlighted: SkUiDateTimeWheels.TimeDate + new TimeSpan(10, 20, 0));
        Assert.Equal(new TimeSpan(10, 15, 0), quarter.Highlighted.TimeOfDay);
    }

    [Fact]
    public void HeadersComeFromTheStrings()
    {
        var strings = new SkUiStrings { DayText = "Tag", MonthText = "Monat", YearText = "Jahr" };
        var wheels = SkUiDateTimeWheels.Create(new SkUiDateTimeSettings(SkUiDateTimeKind.Date, "dd MM yyyy", new DateTime(1900, 1, 1),
            new DateTime(2100, 12, 31), null, null, ShowsHeaders: true), new CultureInfo("de-DE"), strings);
        Assert.Equal("Tag Monat Jahr", string.Join(' ', wheels.Headers));
        Assert.All(Wheels(SkUiDateTimeKind.Date).Headers, Assert.Null);
    }
}
