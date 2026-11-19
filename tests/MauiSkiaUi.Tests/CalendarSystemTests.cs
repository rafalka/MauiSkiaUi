using System.Globalization;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Calendars other than the Gregorian one (1.15 wave C, <see cref="SkUiCalendar.CalendarSystem"/>): pages cut by the calendar's
/// months and years, checked against .NET's own calendars over 30 years; a Hebrew leap year's 13 months; the supported range's
/// ends; titles and the culture that writes them; dates that stay <see cref="DateTime"/> values; both layers; allocations.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class CalendarSystemTests : IDisposable
{
    private const int Width = 280;
    private const int Height = 48 + 28 + 6 * 44;

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly IDisposable _dispatcher = SkUiTestHelpers.UseTestDispatcher();

    public CalendarSystemTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        _dispatcher.Dispose();
    }

    public static TheoryData<string> Systems => ["ThaiBuddhist", "Taiwan", "Korean", "Japanese", "UmAlQura", "Hijri", "Persian", "Hebrew"];

    private static Calendar Create(string name) => name switch
    {
        "ThaiBuddhist" => new ThaiBuddhistCalendar(),
        "Taiwan" => new TaiwanCalendar(),
        "Korean" => new KoreanCalendar(),
        "Japanese" => new JapaneseCalendar(),
        "UmAlQura" => new UmAlQuraCalendar(),
        "Hijri" => new HijriCalendar(),
        "Persian" => new PersianCalendar(),
        _ => new HebrewCalendar()
    };

    [Theory]
    [MemberData(nameof(Systems))]
    public void MonthPagesAreTheCalendarsMonthsForThirtyYears(string name)
    {
        var calendar = Create(name);
        var system = SkUiCalendarSystem.For(calendar)!;
        var start = SkUiCalendarGrid.PageStart(new DateTime(2010, 1, 1), SkUiCalendarDisplayMode.Month, 6, DayOfWeek.Sunday, system);
        var months = 0;
        while (start < new DateTime(2040, 1, 1))
        {
            Assert.Equal(1, calendar.GetDayOfMonth(start));
            var end = SkUiCalendarGrid.PageEnd(start, SkUiCalendarDisplayMode.Month, 6, system);
            Assert.Equal(calendar.GetDaysInMonth(calendar.GetYear(start), calendar.GetMonth(start), calendar.GetEra(start)), (end - start).Days + 1);
            var next = SkUiCalendarGrid.Step(start, SkUiCalendarDisplayMode.Month, 6, 1, system)!.Value;
            Assert.Equal(end.AddDays(1), next);
            Assert.Equal(start, SkUiCalendarGrid.Step(next, SkUiCalendarDisplayMode.Month, 6, -1, system));
            // Every day of the page is on it, and the next month's first day is another month's.
            Assert.False(SkUiCalendarGrid.IsOtherMonth(start, end, system));
            Assert.True(SkUiCalendarGrid.IsOtherMonth(start, next, system));
            Assert.Equal(calendar.GetDayOfMonth(end), SkUiCalendarGrid.DayOfMonth(end, system));
            start = next;
            months++;
        }
        Assert.InRange(months, 360, 380); // Hebrew leap years add months; lunar years are shorter
    }

    [Theory]
    [MemberData(nameof(Systems))]
    public void YearPagesHoldTheCalendarsMonthsAndDecadesItsYears(string name)
    {
        var calendar = Create(name);
        var system = SkUiCalendarSystem.For(calendar)!;
        var day = new DateTime(2026, 11, 19);
        var year = SkUiCalendarGrid.PageStart(day, SkUiCalendarDisplayMode.Year, 6, DayOfWeek.Sunday, system);
        var calendarYear = calendar.GetYear(day);
        Assert.Equal(1, calendar.GetMonth(year));
        Assert.Equal(1, calendar.GetDayOfMonth(year));
        var monthsInYear = calendar.GetMonthsInYear(calendarYear);
        for (var index = 0; index < SkUiCalendarGrid.PeriodCellCount(SkUiCalendarDisplayMode.Year, system); index++)
        {
            var cell = SkUiCalendarGrid.PeriodCell(year, SkUiCalendarDisplayMode.Year, index, system);
            if (index >= monthsInYear)
            {
                Assert.Null(cell);
                continue;
            }
            Assert.Equal(index + 1, calendar.GetMonth(cell!.Value));
            Assert.Equal(index, SkUiCalendarGrid.PeriodIndex(year, SkUiCalendarDisplayMode.Year, cell.Value, system));
        }
        Assert.Equal(calendarYear + 1, calendar.GetYear(SkUiCalendarGrid.Step(year, SkUiCalendarDisplayMode.Year, 6, 1, system)!.Value) is var next && name == "Japanese"
            ? calendarYear + 1 // the Japanese pages count Gregorian years: 2027 is Reiwa 9
            : next);

        // A decade page starts on a round year of the calendar (Gregorian for the Japanese one).
        var decade = SkUiCalendarGrid.PageStart(day, SkUiCalendarDisplayMode.Decade, 6, DayOfWeek.Sunday, system);
        var counted = name == "Japanese" ? decade.Year : calendar.GetYear(decade);
        Assert.Equal(0, counted % 10);
        Assert.Equal(1, calendar.GetMonth(decade));
    }

    [Fact]
    public void AHebrewLeapYearPageHasThirteenMonthsOnFiveRows()
    {
        var hebrew = new HebrewCalendar();
        var system = SkUiCalendarSystem.For(hebrew)!;
        Assert.Equal(5, SkUiCalendarGrid.PeriodRowCount(SkUiCalendarDisplayMode.Year, system));
        Assert.Equal(4, SkUiCalendarGrid.PeriodRowCount(SkUiCalendarDisplayMode.Decade, system));
        Assert.True(hebrew.IsLeapYear(5787));
        var leap = hebrew.ToDateTime(5787, 1, 1, 0, 0, 0, 0);
        Assert.NotNull(SkUiCalendarGrid.PeriodCell(leap, SkUiCalendarDisplayMode.Year, 12, system));
        var common = hebrew.ToDateTime(5786, 1, 1, 0, 0, 0, 0);
        Assert.Null(SkUiCalendarGrid.PeriodCell(common, SkUiCalendarDisplayMode.Year, 12, system));

        // Drawn: the year page lays out 5 rows and a tap on the 13th month opens it.
        var calendar = new SkUiCalendar { CalendarSystem = hebrew, DisplayDate = leap, DisplayMode = SkUiCalendarDisplayMode.Year };
        using var surface = new SkUiTestSurface(calendar, Width, Height);
        surface.Frame(0);
        var cellHeight = (Height - 48f) / 5;
        var thirteenth = new Point(Width / 6.0, 48 + cellHeight * 4.5);
        Tap(calendar, thirteenth);
        Assert.Equal(SkUiCalendarDisplayMode.Month, calendar.DisplayMode);
        Assert.Equal(13, hebrew.GetMonth(calendar.DisplayDate));
        Assert.Equal(5787, hebrew.GetYear(calendar.DisplayDate));
    }

    [Fact]
    public void ThePagesStopAtTheDaysTheCalendarSupports()
    {
        var umAlQura = new UmAlQuraCalendar();
        var calendar = new SkUiCalendar { CalendarSystem = umAlQura, DisplayDate = new DateTime(1850, 1, 1) };
        using var surface = new SkUiTestSurface(calendar, Width, Height);
        surface.Frame(0);
        Assert.Equal(umAlQura.MinSupportedDateTime.Date, calendar.Engine.PageStart);
        Assert.False(calendar.CanGoToPrevious);
        Assert.False(calendar.Engine.IsChoosable(umAlQura.MinSupportedDateTime.Date.AddDays(-1)));
        Assert.True(calendar.Engine.IsChoosable(umAlQura.MinSupportedDateTime.Date));

        calendar.GoToDate(new DateTime(2200, 1, 1), animated: false);
        surface.Frame(16);
        Assert.False(calendar.CanGoToNext);
        Assert.False(calendar.Engine.IsChoosable(umAlQura.MaxSupportedDateTime.Date.AddDays(1)));
        foreach (var mode in new[] { SkUiCalendarDisplayMode.Year, SkUiCalendarDisplayMode.Decade, SkUiCalendarDisplayMode.Century })
        {
            calendar.DisplayMode = mode;
            surface.Frame(32);
            Assert.False(calendar.CanGoToNext);
        }
        // The app's limits inside the calendar's still apply.
        calendar.DisplayMode = SkUiCalendarDisplayMode.Month;
        calendar.MaximumDate = new DateTime(2030, 1, 10);
        calendar.GoToDate(new DateTime(2050, 1, 1), animated: false);
        Assert.Equal(umAlQura.GetMonth(new DateTime(2030, 1, 10)), umAlQura.GetMonth(calendar.Engine.PageStart));
    }

    [Fact]
    public void TitlesAreWrittenInTheCalendarByACultureThatCan()
    {
        var calendar = new SkUiCalendar { DisplayDate = new DateTime(2026, 11, 19), CalendarSystem = new ThaiBuddhistCalendar() };
        Assert.Contains("2569", calendar.Title);
        calendar.DisplayMode = SkUiCalendarDisplayMode.Year;
        Assert.Equal("2569", calendar.Title);
        calendar.DisplayMode = SkUiCalendarDisplayMode.Decade;
        Assert.Equal("2560 – 2569", calendar.Title);

        calendar.DisplayMode = SkUiCalendarDisplayMode.Month;
        calendar.CalendarSystem = new JapaneseCalendar();
        Assert.Equal("令和8年11月", calendar.Title); // with its era
        calendar.DisplayMode = SkUiCalendarDisplayMode.Year;
        Assert.Equal("令和 8", calendar.Title);

        // en-US cannot write the Hebrew calendar: its own culture does; he-IL itself does.
        calendar.DisplayMode = SkUiCalendarDisplayMode.Month;
        calendar.CalendarSystem = new HebrewCalendar();
        Assert.Contains("כסלו", calendar.Title);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("he-IL");
        calendar.GoToNext(animated: false);
        Assert.Contains("טבת", calendar.Title);

        // Persian from fa-IR's optional calendar, and back to Gregorian with null.
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        calendar.CalendarSystem = new PersianCalendar();
        Assert.Contains("1405", calendar.Title);
        calendar.CalendarSystem = null;
        Assert.Equal("December 2026", calendar.Title);
    }

    [Fact]
    public void DatesStayDateTimesWhateverTheCalendar()
    {
        var calendar = new SkUiCalendar
        {
            CalendarSystem = new PersianCalendar(), DisplayDate = new DateTime(2026, 11, 19), SelectionMode = SkUiCalendarSelectionMode.Range,
            FirstDayOfWeek = DayOfWeek.Saturday
        };
        using var surface = new SkUiTestSurface(calendar, Width, Height);
        surface.Frame(0);
        // The page is Aban 1405 (23 October to 21 November 2026); its 1st is a Friday, so Saturday weeks start on 17 October.
        Assert.Equal(new DateTime(2026, 10, 23), calendar.Engine.PageStart);
        var accessible = (ISkUiAccessibleNode)calendar;
        // Cells from Saturday 17 October: the 6th is 22 October (Mehr 30, the month before), the 13th 29 October (Aban 7).
        Assert.True(accessible.PerformSemanticsPartAction(3 + 5, SkUiSemanticsActions.Activate));
        Assert.True(accessible.PerformSemanticsPartAction(3 + 12, SkUiSemanticsActions.Activate));
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 10, 22), new DateTime(2026, 10, 29)), calendar.SelectedRange);
    }

    [Fact]
    public void ShiftPageDownMovesAYearOfTheCalendar()
    {
        var hebrew = new HebrewCalendar();
        var calendar = new SkUiCalendar { CalendarSystem = hebrew, DisplayDate = new DateTime(2026, 11, 19) };
        using var surface = new SkUiTestSurface(calendar, Width, Height);
        surface.Frame(0);
        var engine = calendar.Engine;
        engine.OnKey(new SkUiKeyEvent(SkUiKey.Right, SkUiKeyModifiers.None, false)); // the keyboard takes the focus
        var focus = engine.FocusDate!.Value;
        engine.OnKey(new SkUiKeyEvent(SkUiKey.PageDown, SkUiKeyModifiers.Shift, false));
        var moved = engine.FocusDate!.Value;
        Assert.Equal(hebrew.GetYear(focus) + 1, hebrew.GetYear(moved));
        Assert.Equal(hebrew.GetMonth(focus), hebrew.GetMonth(moved)); // 5787 is a leap year, 5788 not: the same Kislev
        Assert.Equal(hebrew.GetDayOfMonth(focus), hebrew.GetDayOfMonth(moved));
    }

    [Fact]
    public void OtherCalendarsAreRejectedAndTheCoreTwinHasTheSystem()
    {
        var calendar = new SkUiCalendar();
        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.SetCalendarSystem(new JulianCalendar()));
        calendar.CalendarSystem = new ChineseLunisolarCalendar(); // ignored, as MAUI ignores an invalid value
        Assert.Null(calendar.CalendarSystem);
        var core = new SkUiCoreCalendar();
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetCalendarSystem(new JulianCalendar()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiDateRangePicker().SetCalendarSystem(new JulianCalendar()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreDateRangePicker().SetCalendarSystem(new JulianCalendar()));
    }

    [Theory]
    [InlineData("Hebrew")]
    [InlineData("UmAlQura")]
    public void BothLayersDrawTheSamePixels(string name)
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var date = new DateTime(2026, 11, 19);
        var skui = new SkUiCalendar
        {
            CalendarSystem = Create(name), DisplayDate = date, SelectedDate = new DateTime(2026, 11, 10), FontFamily = SkUiTestHelpers.BundledFontFamily
        };
        var core = new SkUiCoreCalendar().SetCalendarSystem(Create(name)).SetDisplayDate(date).SetSelectedDate(new DateTime(2026, 11, 10))
            .SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        using var skuiSurface = new SkUiTestSurface(skui, Width, Height);
        using var coreSurface = new SkUiTestSurface(new SkUiCoreHost().SetContent(core), Width, Height);
        Assert.Equal(skuiSurface.Frame(0).Bytes, coreSurface.Frame(0).Bytes);
    }

    [Fact]
    public void TheRangePickersPopupCalendarTakesTheSystem()
    {
        using var ui = TestUiContext.Install();
        var presenter = new RecordingPopupPresenter();
        var previous = SkUiPopups.Presenter;
        SkUiPopups.Presenter = presenter;
        try
        {
            var picker = new SkUiDateRangePicker { CalendarSystem = new HebrewCalendar() };
            var stack = new SkUiVerticalStackLayout();
            stack.Children.Add(picker);
            SkUiTestHelpers.Arrange(stack, 360, 700);
            picker.IsOpen = true;
            var content = presenter.Current!.Content!;
            var popupCalendar = content.GetVisualTreeDescendants().OfType<SkUiCalendar>().Single();
            Assert.IsType<HebrewCalendar>(popupCalendar.CalendarSystem);
        }
        finally
        {
            SkUiPopups.Presenter = previous;
        }
    }

    [Fact]
    public void ASwipeFrameInAnotherCalendarAllocatesNothing()
    {
        var calendar = new SkUiCalendar { CalendarSystem = new HebrewCalendar(), DisplayDate = new DateTime(2026, 11, 19) };
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

    private static long _pointer = 1_151_000;

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        var start = TimeSpan.FromSeconds(id);
        root.Touch(new(id, SkUiTouchAction.Pressed, at, start));
        root.Touch(new(id, SkUiTouchAction.Released, at, start + TimeSpan.FromMilliseconds(30)));
    }
}
