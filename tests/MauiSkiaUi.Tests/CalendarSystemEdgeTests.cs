using System.Globalization;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Calendar systems at the ends of their ranges, and what the 1.15 branch review found there: screen readers on the first and
/// last pages, the Gregorian-month calendars' December 9999, week pages at a calendar's first day, months a partly supported year
/// does not have, the Japanese calendar's first era, a Hebrew year step across a leap year, the first day of the week, and the
/// date and time picker's default popup.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class CalendarSystemEdgeTests : IDisposable
{
    private const int Width = 280;
    private const int Height = 48 + 28 + 6 * 44;

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly IDisposable _dispatcher = SkUiTestHelpers.UseTestDispatcher();

    public CalendarSystemEdgeTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        _dispatcher.Dispose();
    }

    /// <summary>The calendar's elements for screen readers (the calendar is the test surface's root, already arranged).</summary>
    private static List<SkUiSemanticsNode> Elements(SkUiCalendar calendar) =>
        SkUiSemanticsTree.Build(calendar).Flatten().Where(node => ReferenceEquals(node.Source, calendar) && node.PartIndex >= 0).ToList();

    public static TheoryData<string, DateTime> Ends => new()
    {
        { "UmAlQura", new DateTime(2077, 11, 10) },
        { "UmAlQura", new DateTime(1900, 5, 2) },
        { "Hebrew", new DateTime(2239, 9, 20) },
        { "Hebrew", new DateTime(1583, 1, 3) },
        { "Japanese", new DateTime(1868, 1, 1) },
        { "Hijri", new DateTime(9999, 12, 20) },
        { "Persian", new DateTime(9999, 12, 20) },
        { "ThaiBuddhist", new DateTime(9999, 12, 15) },
        { "Korean", new DateTime(9999, 12, 15) },
    };

    private static Calendar Create(string name) => name switch
    {
        "UmAlQura" => new UmAlQuraCalendar(),
        "Hebrew" => new HebrewCalendar(),
        "Japanese" => new JapaneseCalendar(),
        "Hijri" => new HijriCalendar(),
        "Persian" => new PersianCalendar(),
        "ThaiBuddhist" => new ThaiBuddhistCalendar(),
        _ => new KoreanCalendar()
    };

    [Theory]
    [MemberData(nameof(Ends))]
    public void TheFirstAndLastPagesDrawAndAreReadOnEveryMode(string name, DateTime date)
    {
        foreach (var weeks in new[] { 6, 1 })
        {
            var calendar = new SkUiCalendar { CalendarSystem = Create(name), DisplayDate = date, VisibleWeeks = weeks };
            using var surface = new SkUiTestSurface(calendar, Width, Height);
            surface.Frame(0);
            var days = Elements(calendar);
            Assert.NotEmpty(days);
            var system = SkUiCalendarSystem.For(calendar.CalendarSystem)!;
            // Days outside the calendar's range are neither drawn nor read; the rest are.
            Assert.All(days.Where(part => part.PartIndex >= 3), part => Assert.False(string.IsNullOrEmpty(part.Label)));
            Assert.True(calendar.Engine.PageStart <= system.Maximum);
            foreach (var mode in new[] { SkUiCalendarDisplayMode.Year, SkUiCalendarDisplayMode.Decade, SkUiCalendarDisplayMode.Century })
            {
                calendar.DisplayMode = mode;
                surface.Frame(16);
                Assert.NotEmpty(Elements(calendar));
            }
        }
    }

    [Theory]
    [InlineData("Hijri")]
    [InlineData("Persian")]
    [InlineData("ThaiBuddhist")]
    public void TheLastMonthPageEndsTheCalendar(string name)
    {
        // The calendar's last day (a lunar month may start in the last days of December 9999: the page holding the 20th is not the last).
        var last = DateTime.MaxValue.Date;
        var calendar = new SkUiCalendar { CalendarSystem = Create(name), DisplayDate = last };
        using var surface = new SkUiTestSurface(calendar, Width, Height);
        surface.Frame(0);
        Assert.False(calendar.CanGoToNext);
        var engine = calendar.Engine;
        var system = SkUiCalendarSystem.For(calendar.CalendarSystem)!;
        Assert.True(SkUiCalendarGrid.PageEnd(engine.PageStart, SkUiCalendarDisplayMode.Month, 6, system) >= engine.PageStart);
        Assert.True(engine.PageContains(last));
        Assert.True(calendar.CanGoToPrevious);
        // The calendar's last day is its month's day, not the next month's first (December 9999 has nothing after it).
        var days = new Dictionary<DateTime, SkUiCalendarDayContext>();
        calendar.DayPainter = (SKCanvas _, in SkUiCalendarDayContext day) =>
        {
            days[day.Date] = day;
            return false;
        };
        surface.Frame(16);
        Assert.False(days[last].IsAdjacent);
        Assert.Equal(system.Day(last), engine.DayNumberOf(engine.PageStart, last));
    }

    [Fact]
    public void AYearOnlyPartlySupportedHasCellsForItsSupportedMonthsOnly()
    {
        var japanese = new JapaneseCalendar();
        var system = SkUiCalendarSystem.For(japanese)!;
        var first = system.Minimum;
        Assert.Equal(1868, first.Year);
        var year = SkUiCalendarGrid.PageStart(first, SkUiCalendarDisplayMode.Year, 6, DayOfWeek.Sunday, system);
        var cells = Enumerable.Range(0, 12).Select(index => SkUiCalendarGrid.PeriodCell(year, SkUiCalendarDisplayMode.Year, index, system)).ToList();
        Assert.All(cells.Take(first.Month - 1), cell => Assert.Null(cell));
        Assert.Equal(first, cells[first.Month - 1]);
        Assert.Equal(new DateTime(1868, 12, 1), cells[11]);
        // The first month page has nothing before it.
        var page = SkUiCalendarGrid.PageStart(first, SkUiCalendarDisplayMode.Month, 6, DayOfWeek.Sunday, system);
        Assert.Null(SkUiCalendarGrid.Step(page, SkUiCalendarDisplayMode.Month, 6, -1, system));
        var calendar = new SkUiCalendar { CalendarSystem = japanese, DisplayDate = first };
        Assert.False(calendar.CanGoToPrevious);
    }

    [Fact]
    public void AWeekPageReachesTheCalendarsFirstDay()
    {
        var umAlQura = new UmAlQuraCalendar();
        var first = umAlQura.MinSupportedDateTime.Date; // Monday 30 April 1900; en-US weeks start on Sunday
        var calendar = new SkUiCalendar { CalendarSystem = umAlQura, VisibleWeeks = 1, DisplayDate = first.AddDays(8) };
        using var surface = new SkUiTestSurface(calendar, Width, Height);
        surface.Frame(0);
        Assert.True(calendar.GoToPrevious(animated: false));
        surface.Frame(16);
        Assert.True(calendar.Engine.PageStart < first && calendar.Engine.PageStart.AddDays(6) >= first);
        Assert.False(calendar.CanGoToPrevious);
        Assert.False(calendar.Engine.IsChoosable(first.AddDays(-1)));
    }

    [Fact]
    public void AHebrewYearStepKeepsTheMonthAcrossALeapYear()
    {
        var hebrew = new HebrewCalendar();
        var system = SkUiCalendarSystem.For(hebrew)!;
        var nisan = new DateTime(2024, 4, 23); // 15 Nisan 5784 (a leap year: Nisan is its 8th month)
        Assert.Equal(15, hebrew.GetDayOfMonth(nisan));
        var next = SkUiCalendarGrid.AddYears(nisan, 1, system)!.Value;
        Assert.Equal(5785, hebrew.GetYear(next));
        Assert.Equal(7, hebrew.GetMonth(next)); // Nisan is the 7th month of a common year
        Assert.Equal(15, hebrew.GetDayOfMonth(next));
        Assert.Equal(nisan, SkUiCalendarGrid.AddYears(next, -1, system));
        // Adar I and Adar II of a leap year are Adar of a common one; Adar of a common year is Adar II of a leap one.
        Assert.Equal(6, system.SameMonth(6, 5784, 5785));
        Assert.Equal(6, system.SameMonth(7, 5784, 5785));
        Assert.Equal(7, system.SameMonth(6, 5785, 5787));
    }

    [Theory]
    [InlineData("UmAlQura")]
    [InlineData("Japanese")]
    [InlineData("Hebrew")]
    public void TheFirstMonthNumbersItsDaysFromTheMonthNotFromTheCalendarsFirstDay(string name)
    {
        // The first supported day is mid-month (Umm al-Qura's 30 April 1900 is in its month 1318/1); the page starts there.
        var calendar = Create(name);
        var system = SkUiCalendarSystem.For(calendar)!;
        foreach (var weeks in new[] { 6, 2 })
        {
            var control = new SkUiCalendar { CalendarSystem = calendar, DisplayDate = system.Minimum.AddDays(3), VisibleWeeks = weeks };
            var engine = control.Engine;
            var start = engine.PageStart;
            for (var day = system.Minimum; day < system.Minimum.AddDays(10); day = day.AddDays(1))
                Assert.Equal(calendar.GetDayOfMonth(day), engine.DayNumberOf(start, day));
        }
    }

    [Fact]
    public void KeysMoveIntoAPartlySupportedFirstYearAsFarAsTheCalendarGoes()
    {
        var japanese = new JapaneseCalendar();
        var system = SkUiCalendarSystem.For(japanese)!;
        // A year back from March 1869 is March 1868, before the calendar's first day: the focus lands on that day.
        Assert.Equal(system.Minimum, SkUiCalendarGrid.AddYears(new DateTime(1869, 3, 10), -1, system));
        var yearPage = SkUiCalendarGrid.PageStart(new DateTime(1869, 3, 10), SkUiCalendarDisplayMode.Year, 6, DayOfWeek.Sunday, system);
        Assert.Equal(system.MonthStart(system.Minimum), SkUiCalendarGrid.MoveCells(new DateTime(1869, 1, 1), SkUiCalendarDisplayMode.Year, -4, system)); // September 1868 is before it
        Assert.Null(SkUiCalendarGrid.MoveCells(system.Minimum, SkUiCalendarDisplayMode.Year, -3, system)); // already there
        Assert.Equal(1869, yearPage.Year);
    }

    [Fact]
    public void TheFirstDayOfTheWeekIsTheUsersWhateverCultureWritesTheCalendar()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB"); // Monday weeks; it cannot write the Korean calendar
        var calendar = new SkUiCalendar { CalendarSystem = new KoreanCalendar() };
        Assert.Equal(DayOfWeek.Monday, calendar.Engine.EffectiveFirstDayOfWeek);
        calendar.CalendarSystem = new PersianCalendar();
        Assert.Equal(DayOfWeek.Monday, calendar.Engine.EffectiveFirstDayOfWeek);
    }

    [Fact]
    public void AYearPageSlideAllocatesNothingWithACalendarSystem()
    {
        var calendar = new SkUiCalendar { CalendarSystem = new HebrewCalendar(), DisplayDate = new DateTime(2026, 11, 19), DisplayMode = SkUiCalendarDisplayMode.Year };
        using var surface = new SkUiTestSurface(calendar, Width, Height);
        surface.Frame(0);
        var engine = calendar.Engine;
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, Width, Height));
        engine.BeginDrag();
        void Step(int i)
        {
            engine.Drag(-60 + i % 12 * 10, Width, Height);
            engine.Draw(canvas, Width, Height);
        }
        for (var i = 0; i < 30; i++) Step(i);
        var fewest = long.MaxValue;
        for (var batch = 0; batch < 3 && fewest > 0; batch++)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 60; i++) Step(i);
            fewest = Math.Min(fewest, GC.GetAllocatedBytesForCurrentThread() - allocated);
        }
        engine.CancelDrag();
        recorder.EndRecording().Dispose();
        Assert.Equal(0, fewest);
    }

    [Fact]
    public void TheCoreCalendarGivesTodayStyleAndSelectionShapeBackToTheLook()
    {
        var calendar = new SkUiCoreCalendar().SetTodayStyle(SkUiCalendarTodayStyle.Filled).SetSelectionShape(SkUiCalendarSelectionShape.Square);
        ISkUiCalendarHost host = calendar;
        Assert.Equal(SkUiCalendarTodayStyle.Filled, host.CalendarTodayStyle);
        calendar.ClearTodayStyle().ClearSelectionShape();
        Assert.Null(host.CalendarTodayStyle);
        Assert.Null(host.CalendarSelectionShape);
        Assert.Equal(SkUiCalendarTodayStyle.Ring, calendar.TodayStyle);
    }

    private sealed class CalendarDatesLook : DefaultSkUiLook
    {
        public SkUiDatePickerPopupStyle DateTime { get; init; } = SkUiDatePickerPopupStyle.Wheels;
        public override SkUiDatePickerPopupStyle DatePickerPopupStyle => SkUiDatePickerPopupStyle.Calendar;
        public override SkUiDatePickerPopupStyle DateTimePickerPopupStyle => DateTime;
    }

    [Fact]
    public void ALookThatGivesDatePickersCalendarsLeavesDateAndTimePickersOnWheels()
    {
        using var ui = TestUiContext.Install();
        var presenter = new RecordingPopupPresenter();
        var previous = (SkUiPopups.Presenter, SkUiLook.Current);
        SkUiPopups.Presenter = presenter;
        try
        {
            SkUiLook.Current = new CalendarDatesLook();
            var picker = new SkUiDateTimePicker { Date = new DateTime(2026, 11, 10, 9, 0, 0) };
            var stack = new SkUiVerticalStackLayout();
            stack.Children.Add(picker);
            SkUiTestHelpers.Arrange(stack, 360, 700);
            picker.IsOpen = true;
            Assert.IsType<SkUiColumnsPopup>(presenter.Current!.Content);
            picker.IsOpen = false;
            ui.RunPending();
            SkUiLook.Current = new CalendarDatesLook { DateTime = SkUiDatePickerPopupStyle.Calendar };
            picker.IsOpen = true;
            Assert.IsType<SkUiDateTimeCalendarPopup>(presenter.Current!.Content);
        }
        finally
        {
            (SkUiPopups.Presenter, SkUiLook.Current) = previous;
        }
    }
}
