using System.Globalization;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The calendar's date math (<see cref="SkUiCalendarGrid"/>): page starts by culture and first day, steps, decade and century
/// cells, titles and week numbers.
/// </summary>
public class CalendarGridTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void AMonthPageStartsOnTheWeekHoldingItsFirstDay()
    {
        var november = new DateTime(2026, 11, 17);
        var start = SkUiCalendarGrid.PageStart(november, SkUiCalendarDisplayMode.Month, 6, DayOfWeek.Monday);
        Assert.Equal(new DateTime(2026, 11, 1), start);
        // 1 November 2026 is a Sunday: a Monday grid starts on 26 October, a Sunday grid on the 1st.
        Assert.Equal(new DateTime(2026, 10, 26), SkUiCalendarGrid.GridStart(start, 6, DayOfWeek.Monday));
        Assert.Equal(new DateTime(2026, 11, 1), SkUiCalendarGrid.GridStart(start, 6, DayOfWeek.Sunday));
        Assert.Equal(new DateTime(2026, 11, 30), SkUiCalendarGrid.PageEnd(start, SkUiCalendarDisplayMode.Month, 6));
        Assert.Equal(DayOfWeek.Sunday, SkUiCalendarGrid.FirstDayOfWeek(null, English));
        Assert.Equal(DayOfWeek.Monday, SkUiCalendarGrid.FirstDayOfWeek(null, CultureInfo.GetCultureInfo("de-DE")));
    }

    [Fact]
    public void PagesOfWeeksStepByTheirWeeks()
    {
        var start = SkUiCalendarGrid.PageStart(new DateTime(2026, 11, 19), SkUiCalendarDisplayMode.Month, 2, DayOfWeek.Monday);
        Assert.Equal(new DateTime(2026, 11, 16), start);
        Assert.Equal(new DateTime(2026, 11, 30), SkUiCalendarGrid.Step(start, SkUiCalendarDisplayMode.Month, 2, 1));
        Assert.Equal(new DateTime(2026, 11, 29), SkUiCalendarGrid.PageEnd(start, SkUiCalendarDisplayMode.Month, 2));
        // Named after the month most of its days are in.
        Assert.Equal("November 2026", SkUiCalendarGrid.Title(start, SkUiCalendarDisplayMode.Month, 2, null, English));
        Assert.Equal("December 2026", SkUiCalendarGrid.Title(new DateTime(2026, 11, 30), SkUiCalendarDisplayMode.Month, 2, null, English));
    }

    [Fact]
    public void YearDecadeAndCenturyPagesHaveTwelveCells()
    {
        var day = new DateTime(2026, 11, 17);
        var year = SkUiCalendarGrid.PageStart(day, SkUiCalendarDisplayMode.Year, 6, DayOfWeek.Monday);
        Assert.Equal(new DateTime(2026, 3, 1), SkUiCalendarGrid.PeriodCell(year, SkUiCalendarDisplayMode.Year, 2));
        Assert.Equal("Mar", SkUiCalendarGrid.PeriodText(new DateTime(2026, 3, 1), SkUiCalendarDisplayMode.Year, English));
        Assert.Equal(10, SkUiCalendarGrid.PeriodIndex(year, SkUiCalendarDisplayMode.Year, day));

        var decade = SkUiCalendarGrid.PageStart(day, SkUiCalendarDisplayMode.Decade, 6, DayOfWeek.Monday);
        Assert.Equal(new DateTime(2020, 1, 1), decade);
        Assert.Equal(new DateTime(2019, 1, 1), SkUiCalendarGrid.PeriodCell(decade, SkUiCalendarDisplayMode.Decade, 0));
        Assert.Equal(new DateTime(2030, 1, 1), SkUiCalendarGrid.PeriodCell(decade, SkUiCalendarDisplayMode.Decade, 11));
        Assert.True(SkUiCalendarGrid.IsAdjacentPeriodCell(SkUiCalendarDisplayMode.Decade, 11));
        Assert.Equal(7, SkUiCalendarGrid.PeriodIndex(decade, SkUiCalendarDisplayMode.Decade, day));
        Assert.Equal("2020 – 2029", SkUiCalendarGrid.Title(decade, SkUiCalendarDisplayMode.Decade, 6, null, English));
        Assert.Equal(new DateTime(2029, 12, 31), SkUiCalendarGrid.PageEnd(decade, SkUiCalendarDisplayMode.Decade, 6));

        var century = SkUiCalendarGrid.PageStart(day, SkUiCalendarDisplayMode.Century, 6, DayOfWeek.Monday);
        Assert.Equal(new DateTime(2000, 1, 1), century);
        Assert.Equal(new DateTime(1990, 1, 1), SkUiCalendarGrid.PeriodCell(century, SkUiCalendarDisplayMode.Century, 0));
        Assert.Equal(3, SkUiCalendarGrid.PeriodIndex(century, SkUiCalendarDisplayMode.Century, day));
        Assert.Equal("2020", SkUiCalendarGrid.PeriodText(new DateTime(2020, 1, 1), SkUiCalendarDisplayMode.Century, English));
        Assert.Equal(new DateTime(2029, 12, 31), SkUiCalendarGrid.PeriodEnd(new DateTime(2020, 1, 1), SkUiCalendarDisplayMode.Century));
        Assert.Equal("2000 – 2099", SkUiCalendarGrid.Title(century, SkUiCalendarDisplayMode.Century, 6, null, English));
    }

    [Fact]
    public void StepsStayInsideTheRangeOfDates()
    {
        Assert.Null(SkUiCalendarGrid.Step(new DateTime(9999, 12, 1), SkUiCalendarDisplayMode.Month, 6, 1));
        Assert.Null(SkUiCalendarGrid.Step(new DateTime(1, 1, 1), SkUiCalendarDisplayMode.Month, 6, -1));
        Assert.Null(SkUiCalendarGrid.Step(new DateTime(1, 1, 1), SkUiCalendarDisplayMode.Decade, 6, -1));
        Assert.Equal(new DateTime(10, 1, 1), SkUiCalendarGrid.Step(new DateTime(1, 1, 1), SkUiCalendarDisplayMode.Decade, 6, 1));
        // The first decade and century (from year 1) are a page back from the next ones; nothing past year 9999.
        Assert.Equal(new DateTime(1, 1, 1), SkUiCalendarGrid.Step(new DateTime(10, 1, 1), SkUiCalendarDisplayMode.Decade, 6, -1));
        Assert.Equal(new DateTime(1, 1, 1), SkUiCalendarGrid.Step(new DateTime(100, 1, 1), SkUiCalendarDisplayMode.Century, 6, -1));
        Assert.Equal(new DateTime(1990, 1, 1), SkUiCalendarGrid.Step(new DateTime(2026, 1, 1), SkUiCalendarDisplayMode.Decade, 6, -3));
        Assert.Null(SkUiCalendarGrid.Step(new DateTime(9990, 1, 1), SkUiCalendarDisplayMode.Decade, 6, 1));
        Assert.Null(SkUiCalendarGrid.Step(new DateTime(9900, 1, 1), SkUiCalendarDisplayMode.Century, 6, 1));
        Assert.Equal(DateTime.MinValue.Date, SkUiCalendarGrid.WeekStart(DateTime.MinValue, DayOfWeek.Sunday));
        Assert.Null(SkUiCalendarGrid.PeriodCell(new DateTime(1, 1, 1), SkUiCalendarDisplayMode.Decade, 0));
    }

    [Fact]
    public void WeekNumbersFollowIsoForMondayWeeksAndTheCultureOtherwise()
    {
        var german = CultureInfo.GetCultureInfo("de-DE");
        Assert.Equal(53, SkUiCalendarGrid.WeekNumber(new DateTime(2026, 12, 28), DayOfWeek.Monday, german));
        Assert.Equal(1, SkUiCalendarGrid.WeekNumber(new DateTime(2024, 12, 30), DayOfWeek.Monday, german));
        Assert.Equal(47, SkUiCalendarGrid.WeekNumber(new DateTime(2026, 11, 15), DayOfWeek.Sunday, English));
        // The first-day rule numbers 54 weeks in a leap year that starts on a Saturday.
        Assert.Equal(54, SkUiCalendarGrid.WeekNumber(new DateTime(2028, 12, 31), DayOfWeek.Sunday, English));
    }

    [Fact]
    public void TextsUseTheGregorianCalendarThePagesAreLaidOutIn()
    {
        Assert.Same(English, SkUiCalendarGrid.Gregorian(English));
        var thai = SkUiCalendarGrid.Gregorian(CultureInfo.GetCultureInfo("th-TH"));
        Assert.IsType<GregorianCalendar>(thai.DateTimeFormat.Calendar);
        Assert.Same(thai, SkUiCalendarGrid.Gregorian(CultureInfo.GetCultureInfo("th-TH")));
        Assert.Contains("2026", SkUiCalendarGrid.Title(new DateTime(2026, 11, 1), SkUiCalendarDisplayMode.Month, 6, null, thai));
        // Past the Hijri calendar's range the Gregorian copy still formats.
        var arabic = SkUiCalendarGrid.Gregorian(CultureInfo.GetCultureInfo("ar-SA"));
        Assert.IsType<GregorianCalendar>(arabic.DateTimeFormat.Calendar);
        Assert.NotEmpty(SkUiCalendarGrid.Title(new DateTime(2100, 1, 1), SkUiCalendarDisplayMode.Month, 6, null, arabic));
        Assert.Equal(1, SkUiCalendarGrid.WeekNumber(new DateTime(2100, 1, 1), DayOfWeek.Sunday, arabic));
    }

    [Fact]
    public void DayNamesComeFromTheCulture()
    {
        Assert.Equal("Mo", SkUiCalendarGrid.DayName(DayOfWeek.Monday, SkUiCalendarDayNameFormat.Shortest, English));
        Assert.Equal("Mon", SkUiCalendarGrid.DayName(DayOfWeek.Monday, SkUiCalendarDayNameFormat.Abbreviated, English));
        Assert.Equal("M", SkUiCalendarGrid.DayName(DayOfWeek.Monday, SkUiCalendarDayNameFormat.FirstLetter, English));
    }

    [Fact]
    public void ARangeIsOrderedAndHoldsItsEnds()
    {
        var range = new SkUiDateRange(new DateTime(2026, 11, 13, 15, 0, 0), new DateTime(2026, 11, 10));
        Assert.Equal(new DateTime(2026, 11, 10), range.Start);
        Assert.Equal(new DateTime(2026, 11, 13), range.End);
        Assert.Equal(4, range.Days);
        Assert.True(range.Contains(new DateTime(2026, 11, 13, 23, 0, 0)));
        Assert.False(range.Contains(new DateTime(2026, 11, 14)));
    }
}

/// <summary>
/// <see cref="SkUiCalendar"/> and <see cref="SkUiCoreCalendar"/>: taps on days, buttons and the title, selection modes and
/// their write-back, day rules, swipes, display modes, keys, screen readers, what the look is asked to draw, and both layers
/// drawing the same pixels. November 2026 in en-US with Monday weeks: the grid starts on Monday 26 October.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class CalendarTests : IDisposable
{
    // 280 wide: seven 40-wide columns. Header 48, day names 28, six rows of 44 (the look's sizes).
    private const int Width = 280;
    private const int Height = 48 + 28 + 6 * 44;
    private static readonly DateTime November = new(2026, 11, 1);
    private static long _pointer = 1_130_000;

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly IDisposable _dispatcher = SkUiTestHelpers.UseTestDispatcher();

    public CalendarTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        _dispatcher.Dispose();
        SkUiLook.Current.CalendarCellPainter = null;
    }

    /// <summary>The center of the cell of <paramref name="day"/> on the November page (Monday weeks from 26 October).</summary>
    private static Point DayCenter(DateTime day)
    {
        var index = (day - new DateTime(2026, 10, 26)).Days;
        return new Point(20 + 40 * (index % 7), 48 + 28 + 22 + 44 * (index / 7));
    }

    private static Point Day(int day) => DayCenter(new DateTime(2026, 11, day));

    private static readonly Point TitlePoint = new(60, 24);
    // The title-first header: the next button at the end (280 - 8 - 36), the previous one before it.
    private static readonly Point NextButton = new(254, 24);
    private static readonly Point PreviousButton = new(214, 24);

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        var start = TimeSpan.FromSeconds(id);
        root.Touch(new(id, SkUiTouchAction.Pressed, at, start));
        root.Touch(new(id, SkUiTouchAction.Released, at, start + TimeSpan.FromMilliseconds(30)));
    }

    private static void Swipe(SkUiView root, Point from, double dx, double dy, double milliseconds = 300)
    {
        var id = ++_pointer;
        var start = TimeSpan.FromSeconds(id);
        root.Touch(new(id, SkUiTouchAction.Pressed, from, start));
        const int steps = 10;
        for (var step = 1; step <= steps; step++)
            root.Touch(new(id, SkUiTouchAction.Moved, new Point(from.X + dx * step / steps, from.Y + dy * step / steps),
                start + TimeSpan.FromMilliseconds(milliseconds * step / steps)));
        root.Touch(new(id, SkUiTouchAction.Released, new Point(from.X + dx, from.Y + dy), start + TimeSpan.FromMilliseconds(milliseconds + 10)));
    }

    private static SkUiCalendar Calendar(out SkUiTestSurface surface)
    {
        var calendar = new SkUiCalendar { DisplayDate = November, FirstDayOfWeek = DayOfWeek.Monday };
        surface = new SkUiTestSurface(calendar, Width, Height);
        surface.Frame(0);
        return calendar;
    }

    private static SkUiCoreCalendar CoreCalendar(out SkUiCoreHost host, out SkUiTestSurface surface)
    {
        var calendar = new SkUiCoreCalendar().SetDisplayDate(November).SetFirstDayOfWeek(DayOfWeek.Monday);
        host = new SkUiCoreHost().SetContent(calendar);
        surface = new SkUiTestSurface(host, Width, Height);
        surface.Frame(0);
        return calendar;
    }

    [Fact]
    public void DefaultsAndSize()
    {
        var calendar = new SkUiCalendar();
        Assert.Equal(DateTime.Today, calendar.DisplayDate);
        Assert.Equal(SkUiCalendarDisplayMode.Month, calendar.DisplayMode);
        Assert.Equal(SkUiCalendarSelectionMode.Single, calendar.SelectionMode);
        Assert.Null(calendar.SelectedDate);
        Assert.Empty(calendar.SelectedDates);
        Assert.NotNull(calendar.BlackoutDates);
        Assert.Equal(BindingMode.TwoWay, SkUiCalendar.DisplayDateProperty.DefaultBindingMode);
        Assert.Equal(BindingMode.TwoWay, SkUiCalendar.SelectedDateProperty.DefaultBindingMode);
        Assert.Equal(BindingMode.TwoWay, SkUiCalendar.SelectedRangeProperty.DefaultBindingMode);
        Assert.Equal(new Size(Width, Height), ((IView)calendar).Measure(Width, double.PositiveInfinity));
        Assert.Equal(new Size(7 * 44, Height), new SkUiCoreCalendar().Measure(double.PositiveInfinity, double.PositiveInfinity));
        calendar.VisibleWeeks = 1;
        calendar.IsHeaderVisible = false;
        Assert.Equal(new Size(Width, 28 + 44), ((IView)calendar).Measure(Width, double.PositiveInfinity));
        calendar.AreWeekNumbersVisible = true;
        Assert.Equal(new Size(7 * 44 + 32, 28 + 44), ((IView)calendar).Measure(double.PositiveInfinity, double.PositiveInfinity));
    }

    [Fact]
    public void ATapChoosesADayAndIsWrittenBackWithoutCuttingAOneWayBinding()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        var source = BindingSource<DateTime?>.Bind(calendar, SkUiCalendar.SelectedDateProperty, null);
        var changes = new List<SkUiCalendarSelectionChangedEventArgs>();
        calendar.SelectionChanged += (_, args) => changes.Add(args);
        DateTime? tapped = null;
        calendar.DateTapped += (_, args) => tapped = args.Date;

        Tap(calendar, Day(10));
        Assert.Equal(new DateTime(2026, 11, 10), calendar.SelectedDate);
        Assert.Equal(new DateTime(2026, 11, 10), tapped);
        Assert.Null(source.Value); // one-way: the source keeps its value
        source.Value = new DateTime(2026, 11, 20);
        Assert.Equal(new DateTime(2026, 11, 20), calendar.SelectedDate);
        Assert.Equal(2, changes.Count);
        Assert.Equal([new DateTime(2026, 11, 20)], changes[1].AddedDates);
        Assert.Equal([new DateTime(2026, 11, 10)], changes[1].RemovedDates);

        // A second tap on the selected day keeps it, unless deselection is allowed.
        Tap(calendar, Day(20));
        Assert.Equal(new DateTime(2026, 11, 20), calendar.SelectedDate);
        calendar.AllowsDeselection = true;
        Tap(calendar, Day(20));
        Assert.Null(calendar.SelectedDate);
    }

    [Fact]
    public void MultipleSelectionTogglesDaysInDateOrder()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        calendar.SelectionMode = SkUiCalendarSelectionMode.Multiple;
        Tap(calendar, Day(12));
        Tap(calendar, Day(10));
        Tap(calendar, Day(14));
        Assert.Equal([new DateTime(2026, 11, 10), new DateTime(2026, 11, 12), new DateTime(2026, 11, 14)], calendar.SelectedDates);
        Assert.Equal(new DateTime(2026, 11, 14), calendar.SelectedDate);
        var before = calendar.SelectedDates;
        Tap(calendar, Day(12));
        Assert.Equal([new DateTime(2026, 11, 10), new DateTime(2026, 11, 14)], calendar.SelectedDates);
        Assert.NotSame(before, calendar.SelectedDates); // a new list per change, so a binding sees it

        calendar.SelectedDates = [new DateTime(2026, 11, 3, 10, 0, 0), new DateTime(2026, 11, 1), new DateTime(2026, 11, 3)];
        Assert.Equal([new DateTime(2026, 11, 1), new DateTime(2026, 11, 3)], calendar.SelectedDates);
        calendar.ClearSelection();
        Assert.Empty(calendar.SelectedDates);
    }

    [Fact]
    public void RangeSelectionTakesTwoTapsAndTheLookDrawsItsBand()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        calendar.SelectionMode = SkUiCalendarSelectionMode.Range;
        var ranges = new List<SkUiDateRange?>();
        calendar.SelectionChanged += (_, args) => ranges.Add(args.NewRange);
        Tap(calendar, Day(13));
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 11, 13)), calendar.SelectedRange);
        Assert.False(calendar.SelectedRange!.Value.IsComplete);
        Tap(calendar, Day(10));
        var range = new SkUiDateRange(new DateTime(2026, 11, 10), new DateTime(2026, 11, 13));
        Assert.Equal(range, calendar.SelectedRange);
        Assert.Equal(2, ranges.Count);

        var cells = new Dictionary<SKRect, SkUiCalendarCellPaint>();
        SkUiLook.Current.CalendarCellPainter = (_, cell) => cells[cell.Bounds] = cell;
        calendar.SelectionColor = Colors.Red; // re-records
        surface.Frame(16);
        SkUiCalendarCellPaint At(int day)
        {
            var center = Day(day);
            return cells.Single(entry => entry.Key.Contains((float)center.X, (float)center.Y)).Value;
        }
        Assert.Equal((SkUiCalendarRangePart.Start, true, false, true), (At(10).RangePart, At(10).IsSelected, At(10).BandToLeft, At(10).BandToRight));
        Assert.Equal((SkUiCalendarRangePart.Middle, false, true, true), (At(11).RangePart, At(11).IsSelected, At(11).BandToLeft, At(11).BandToRight));
        Assert.Equal((SkUiCalendarRangePart.End, true, true, false), (At(13).RangePart, At(13).IsSelected, At(13).BandToLeft, At(13).BandToRight));
        Assert.Equal(SKColors.Red, At(10).SelectionColor);

        // A third tap starts a new range.
        Tap(calendar, Day(20));
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 11, 20)), calendar.SelectedRange);
    }

    [Fact]
    public void ARangeRunsOnIntoTheNextMonthsDays()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        calendar.SelectionMode = SkUiCalendarSelectionMode.Range;
        calendar.SelectedRange = new SkUiDateRange(new DateTime(2026, 11, 28), new DateTime(2026, 12, 2));
        var cells = new Dictionary<SKRect, SkUiCalendarCellPaint>();
        SkUiLook.Current.CalendarCellPainter = (_, cell) => cells[cell.Bounds] = cell;
        calendar.SelectionColor = Colors.Red; // re-records
        surface.Frame(16);
        SkUiCalendarCellPaint At(DateTime day)
        {
            var center = DayCenter(day);
            return cells.Single(entry => entry.Key.Contains((float)center.X, (float)center.Y)).Value;
        }
        // The neighbor month's days shown on the page carry the band and the end, as November's do.
        Assert.Equal((SkUiCalendarRangePart.Middle, true, true), (At(new DateTime(2026, 11, 30)).RangePart, At(new DateTime(2026, 11, 30)).BandToLeft,
            At(new DateTime(2026, 11, 30)).BandToRight));
        Assert.Equal((SkUiCalendarRangePart.Middle, false), (At(new DateTime(2026, 12, 1)).RangePart, At(new DateTime(2026, 12, 1)).IsSelected));
        Assert.Equal((SkUiCalendarRangePart.End, true, false), (At(new DateTime(2026, 12, 2)).RangePart, At(new DateTime(2026, 12, 2)).IsSelected,
            At(new DateTime(2026, 12, 2)).BandToRight));
        Assert.Equal(SkUiCalendarRangePart.None, At(new DateTime(2026, 12, 3)).RangePart);
    }

    [Fact]
    public void TodaysDayNameIsHighlightedWhileThePageShowsToday()
    {
        var calendar = new SkUiCoreCalendar().SetFirstDayOfWeek(DayOfWeek.Monday).SetDisplayDate(DateTime.Today);
        var model = calendar.Model;
        var column = ((int)DateTime.Today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        Assert.Equal(column, model.TodayColumn(model.PageStart, DayOfWeek.Monday));
        // Two months on, today is not among the page's days.
        calendar.GoToNext(animated: false);
        calendar.GoToNext(animated: false);
        Assert.Equal(-1, model.TodayColumn(model.PageStart, DayOfWeek.Monday));
        calendar.GoToToday(animated: false);
        calendar.SetIsTodayDayNameHighlighted(false);
        Assert.Equal(-1, model.TodayColumn(model.PageStart, DayOfWeek.Monday));
    }

    [Fact]
    public void DayNamesComeFromTheAppsStrings()
    {
        var previous = SkUiStrings.Current;
        try
        {
            var english = CultureInfo.GetCultureInfo("en-US");
            var strings = new SkUiStrings { FirstLetterDayNames = ["N", "P", "W", "Ś", "C", "P", "S"] };
            Assert.Equal("W", strings.GetDayName(DayOfWeek.Tuesday, SkUiCalendarDayNameFormat.FirstLetter, english));
            Assert.Equal("Tue", strings.GetDayName(DayOfWeek.Tuesday, SkUiCalendarDayNameFormat.Abbreviated, english)); // the culture's
            Assert.Throws<ArgumentException>(() => strings.AbbreviatedDayNames = ["Mon"]);

            var calendar = Calendar(out var surface);
            using var _ = surface;
            var recorded = surface.RecordedPictures;
            var version = SkUiStrings.Version;
            SkUiStrings.Current = strings; // the calendar redraws with the new names
            Assert.True(SkUiStrings.Version > version);
            surface.Frame(16);
            Assert.True(surface.RecordedPictures > recorded);
        }
        finally
        {
            SkUiStrings.Current = previous;
        }
    }

    [Fact]
    public void ATapOnAWeekNumberReportsTheWeekToSelectIt()
    {
        var calendar = new SkUiCalendar
        {
            DisplayDate = November, FirstDayOfWeek = DayOfWeek.Monday, AreWeekNumbersVisible = true,
            SelectionMode = SkUiCalendarSelectionMode.Range, MinimumDate = new DateTime(2026, 11, 11)
        };
        using var surface = new SkUiTestSurface(calendar, Width + 32, Height);
        surface.Frame(0);
        var weeks = new List<SkUiCalendarWeekNumberTappedEventArgs>();
        calendar.WeekNumberTapped += (_, e) =>
        {
            weeks.Add(e);
            calendar.SelectedRange = e.EnabledRange;
        };
        // The week numbers take the first 32 DIPs; the third row is the week of Monday 9 November (ISO week 46).
        Tap(calendar, new Point(16, 48 + 28 + 22 + 44 * 2));
        var week = Assert.Single(weeks);
        Assert.Equal((new DateTime(2026, 11, 9), new DateTime(2026, 11, 15), 46), (week.WeekStart, week.WeekEnd, week.WeekNumber));
        Assert.Equal(new DateTime(2026, 11, 11), week.FirstEnabledDay); // the minimum date
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 11, 11), new DateTime(2026, 11, 15)), calendar.SelectedRange);
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 11, 9), new DateTime(2026, 11, 15)), week.Range);
        // A week before the minimum has no day to choose.
        Tap(calendar, new Point(16, 48 + 28 + 22 + 44 * 1));
        Assert.Null(weeks[1].EnabledRange);
        // Days still take taps beside the column.
        var day = Day(20);
        Tap(calendar, new Point(day.X + 32, day.Y));
        Assert.Equal(new DateTime(2026, 11, 20), calendar.SelectedDate);

        // The Core twin reports the same.
        var core = new SkUiCoreCalendar().SetDisplayDate(November).SetFirstDayOfWeek(DayOfWeek.Monday).SetAreWeekNumbersVisible(true);
        DateTime? coreWeek = null;
        core.WeekNumberTapped += (_, e) => coreWeek = e.WeekStart;
        var host = new SkUiCoreHost().SetContent(core);
        using var coreSurface = new SkUiTestSurface(host, Width + 32, Height);
        coreSurface.Frame(0);
        Tap(host, new Point(16, 48 + 28 + 22 + 44 * 4));
        Assert.Equal(new DateTime(2026, 11, 23), coreWeek);
    }

    [Fact]
    public void WeekFiftyFourAndNonGregorianCulturesAreDrawn()
    {
        // en-US numbers the week of 31 December 2028 as 54.
        var calendar = new SkUiCalendar { DisplayDate = new DateTime(2028, 12, 1), FirstDayOfWeek = DayOfWeek.Sunday, AreWeekNumbersVisible = true };
        using var surface = new SkUiTestSurface(calendar, Width + 32, Height);
        surface.Frame(0);
        // A Hijri culture past its calendar's range: the texts are Gregorian.
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
        calendar.DisplayDate = new DateTime(2100, 1, 1);
        surface.Frame(16);
        Assert.Equal(SkUiCalendarGrid.Title(new DateTime(2100, 1, 1), SkUiCalendarDisplayMode.Month, 6, null,
            SkUiCalendarGrid.Gregorian(CultureInfo.CurrentCulture)), calendar.Title);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");
        calendar.DisplayDate = November;
        surface.Frame(32);
        Assert.Contains("2026", calendar.Title);
    }

    [Fact]
    public void AKeyTheCalendarDoesNotHandleShowsNoFocusAndASwipeCatchesASlide()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        var focused = 0;
        SkUiLook.Current.CalendarCellPainter = (_, paint) => focused += paint.IsFocused ? 1 : 0;
        Assert.False(calendar.Engine.OnKey(new SkUiKeyEvent(SkUiKey.Escape, SkUiKeyModifiers.None, false)));
        calendar.Engine.Step(1, animated: false);
        surface.Frame(16);
        Assert.Equal(0, focused);

        // A swipe caught mid-slide starts where the page is.
        var engine = calendar.Engine;
        engine.Step(1, animated: true);
        var offset = engine.SlideOffset;
        Assert.NotEqual(0, offset);
        engine.BeginDrag();
        engine.Drag(0, Width, Height);
        Assert.True(Math.Abs(engine.SlideOffset - offset) < 0.001f);
        // A tenth of the page toward the middle.
        var back = -Math.Sign(offset);
        engine.Drag(28 * back, Width, Height);
        Assert.True(Math.Abs(engine.SlideOffset - (offset + 0.1f * back)) < 0.001f);
        // Released there, the swipe's own short move settles back on the page that was sliding in.
        engine.EndDrag(0, Width, Height);
        Assert.Equal(new DateTime(2027, 1, 1), calendar.DisplayDate);

        // A forward swipe that catches a forward slide goes on forward (the slide's offset is not counted as a swipe back).
        engine.Step(1, animated: true);
        Assert.True(Math.Abs(engine.SlideOffset - 1) < 0.001f);
        engine.BeginDrag();
        engine.Drag(-0.4 * Width, Width, Height);
        engine.EndDrag(0, Width, Height);
        Assert.Equal(new DateTime(2027, 3, 1), calendar.DisplayDate);

        // On the last page the rubber band starts only past the middle.
        calendar.MaximumDate = new DateTime(2027, 4, 30);
        engine.Step(1, animated: true);
        engine.BeginDrag();
        engine.Drag(-0.2 * Width, Width, Height);
        Assert.True(Math.Abs(engine.SlideOffset - 0.8f) < 0.001f);
        engine.Drag(-1.2 * Width, Width, Height);
        Assert.True(Math.Abs(engine.SlideOffset - -0.2f * 0.25f) < 0.001f);
        engine.CancelDrag();

        // Page Up reaches the first decade (from year 1) with the keyboard too.
        calendar.MaximumDate = null;
        calendar.DisplayMode = SkUiCalendarDisplayMode.Decade;
        calendar.DisplayDate = new DateTime(15, 1, 1);
        bool Key(SkUiKey key) => engine.OnKey(new SkUiKeyEvent(key, SkUiKeyModifiers.None, false));
        Assert.True(Key(SkUiKey.PageUp)); // from the display date's year
        Assert.Equal(new DateTime(5, 1, 1), engine.FocusDate);
        Assert.Equal(new DateTime(1, 1, 1), engine.PageStart);
        Assert.True(Key(SkUiKey.PageDown));
        for (var year = 15; year > 10; year--)
            Assert.True(Key(SkUiKey.Left));
        Assert.Equal(new DateTime(10, 1, 1), engine.FocusDate);
        Assert.True(Key(SkUiKey.PageUp)); // year 0 does not exist: year 1
        Assert.Equal(new DateTime(1, 1, 1), engine.FocusDate);
        Assert.False(Key(SkUiKey.PageUp));
    }

    [Fact]
    public void ARangeBandIsPaintedOnceUnderATranslucentColor()
    {
        // A wide cell (a stretched popup): the start day's band runs from its circle to the cell's right edge.
        var look = new DefaultSkUiLook();
        using var bitmap = new SKBitmap(200, 40);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        var range = new SKColor(0, 0, 255, 0x40);
        look.DrawCalendarCell(canvas, new SkUiCalendarCellPaint(new SKRect(0, 0, 200, 40), SkUiCalendarCellKind.Day, SkUiCalendarSelectionShape.Circle,
            false, SkUiCalendarRangePart.Start, false, true, false, SkUiCalendarTodayStyle.None, false, false, true, SKColors.Blue, range,
            SKColors.Red, SKColors.Transparent, SKColors.Black));
        canvas.Flush();
        var band = bitmap.GetPixel(150, 20);
        Assert.NotEqual(SKColors.White, band);
        // Next to the cell's edge, where the rounded end and its squared-off side used to overlap, the band has the same shade.
        Assert.Equal(band, bitmap.GetPixel(197, 20));
        Assert.Equal(band, bitmap.GetPixel(199, 3));
        Assert.Equal(band, bitmap.GetPixel(105, 20));
        Assert.Equal(SKColors.White, bitmap.GetPixel(81, 3)); // the start keeps its round end
    }

    [Fact]
    public void TheHeaderAndItsButtonsCanBeStyled()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        var buttons = new List<SkUiCalendarNavigationButtonPaint>();
        SkUiLook.Current.CalendarNavigationButtonPainter = (_, button) => buttons.Add(button);
        try
        {
            calendar.HeaderPadding = new Thickness(24, 0);
            calendar.NavigationButtonSize = 32;
            calendar.NavigationButtonSpacing = 16;
            calendar.NavigationButtonCornerRadius = 8;
            calendar.NavigationButtonBorderColor = Colors.Gray;
            calendar.NavigationButtonBorderWidth = 1;
            calendar.NextButtonGlyph = "\ue5cc";
            surface.Frame(16);
            var (previous, next) = (buttons[^2], buttons[^1]);
            // Inside the padding, 32 wide, 16 apart, centered in the 48 header.
            Assert.Equal(new SKRect(176, 8, 208, 40), previous.Bounds);
            Assert.Equal(new SKRect(224, 8, 256, 40), next.Bounds);
            Assert.Equal((8f, 1f, SKColors.Gray), (next.CornerRadius, next.BorderWidth, next.BorderColor));
            Assert.True(previous.ShowsChevron);
            Assert.False(next.ShowsChevron); // the calendar draws the app's glyph
            Assert.Equal((SkUiCalendarChevronDirection.Left, SkUiCalendarChevronDirection.Right), (previous.Direction, next.Direction));
            // Pages moving vertically: the chevrons point up and down, right-to-left too.
            calendar.NavigationOrientation = StackOrientation.Vertical;
            calendar.FlowDirection = FlowDirection.RightToLeft;
            surface.Frame(16);
            Assert.Equal((SkUiCalendarChevronDirection.Up, SkUiCalendarChevronDirection.Down), (buttons[^2].Direction, buttons[^1].Direction));
            // Sideways right-to-left: mirrored.
            calendar.NavigationOrientation = StackOrientation.Horizontal;
            surface.Frame(16);
            Assert.Equal((SkUiCalendarChevronDirection.Right, SkUiCalendarChevronDirection.Left), (buttons[^2].Direction, buttons[^1].Direction));
            calendar.FlowDirection = FlowDirection.LeftToRight;
            surface.Frame(16);
            Tap(calendar, new Point(192, 24));
            Assert.Equal(new DateTime(2026, 10, 1), calendar.DisplayDate);

            calendar.HeaderHeight = 60;
            Assert.Equal(Height + 12, ((IView)calendar).Measure(Width, double.PositiveInfinity).Height);
            Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreCalendar().SetNavigationButtonBorderWidth(-1));
            Assert.Equal(Height + 12, new SkUiCoreCalendar().SetHeaderHeight(60).Measure(Width, double.PositiveInfinity).Height);
        }
        finally
        {
            SkUiLook.Current.CalendarNavigationButtonPainter = null;
        }
    }

    [Fact]
    public async Task ButtonsTakeImageIcons()
    {
        var png = ImageLoadingTests.Png(SKColors.Red, 24, 24);
        var calendar = Calendar(out var surface);
        using var _ = surface;
        var buttons = new List<SkUiCalendarNavigationButtonPaint>();
        SkUiLook.Current.CalendarNavigationButtonPainter = (_, button) => buttons.Add(button);
        try
        {
            calendar.NextButtonIcon = ImageSource.FromStream(() => new MemoryStream(png));
            await calendar.Engine.ButtonIconsLoading;
            TestDispatcherProvider.RunDispatched();
            Assert.True(calendar.Engine.HasButtonImage(next: true));
            calendar.NavigationButtonGlyphSize = 20; // re-records
            surface.Frame(16);
            var next = buttons[^1];
            Assert.False(next.ShowsChevron);
            Assert.True(buttons[^2].ShowsChevron);
            // The icon is drawn, centered, 20 wide in the next button (224…260 at the header's end).
            var pixel = surface.Bitmap.GetPixel((int)next.Bounds.MidX, (int)next.Bounds.MidY);
            Assert.Equal(SKColors.Red, pixel);

            var core = new SkUiCoreCalendar().SetPreviousButtonIcon(SkUiImageSource.FromStream(_ => Task.FromResult<Stream>(new MemoryStream(png))));
            await core.Model.ButtonIconsLoading;
            TestDispatcherProvider.RunDispatched();
            Assert.True(core.Model.HasButtonImage(next: false));
        }
        finally
        {
            SkUiLook.Current.CalendarNavigationButtonPainter = null;
        }
    }

    private sealed class OwnHeaderCalendar : SkUiCoreCalendar
    {
        public readonly List<bool> Buttons = [];
        public string? DrawnTitle;

        protected override bool OnDrawNavigationButton(SKCanvas canvas, SkUiCalendarNavigationButtonPaint button, bool isNext)
        {
            Buttons.Add(isNext);
            DrawCalendarText(canvas, isNext ? "→" : "←", button.Bounds, button.Color);
            return true;
        }

        protected override bool OnDrawTitle(SKCanvas canvas, SKRect bounds, string title, SKColor color, bool isPressed)
        {
            DrawnTitle = title;
            return false; // the calendar draws it
        }
    }

    [Fact]
    public void ASubclassCanDrawTheButtonsAndTheTitle()
    {
        var calendar = new OwnHeaderCalendar();
        calendar.SetDisplayDate(November).SetFirstDayOfWeek(DayOfWeek.Monday);
        var looked = 0;
        SkUiLook.Current.CalendarNavigationButtonPainter = (_, _) => looked++;
        try
        {
            var host = new SkUiCoreHost().SetContent(calendar);
            using var surface = new SkUiTestSurface(host, Width, Height);
            surface.Frame(0);
            Assert.Equal([false, true], calendar.Buttons);
            Assert.Equal(0, looked); // the subclass drew them
            Assert.Equal("November 2026", calendar.DrawnTitle);
            Tap(host, NextButton); // taps still work
            Assert.Equal(new DateTime(2026, 12, 1), calendar.DisplayDate);
        }
        finally
        {
            SkUiLook.Current.CalendarNavigationButtonPainter = null;
        }
    }

    [Fact]
    public void TheAppCanSayWhichEndOfTheRangeATapSets()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        calendar.SelectionMode = SkUiCalendarSelectionMode.Range;
        var target = BindingSource<SkUiCalendarRangeTarget>.Bind(calendar, SkUiCalendar.RangeSelectionTargetProperty, SkUiCalendarRangeTarget.Start,
            BindingMode.TwoWay);
        // The start: then the calendar moves the target to the end (written back).
        Tap(calendar, Day(10));
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 11, 10)), calendar.SelectedRange);
        Assert.Equal(SkUiCalendarRangeTarget.End, target.Value);
        Tap(calendar, Day(14));
        Tap(calendar, Day(18)); // the end moves, the target stays
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 11, 10), new DateTime(2026, 11, 18)), calendar.SelectedRange);
        Assert.Equal(SkUiCalendarRangeTarget.End, calendar.RangeSelectionTarget);
        // An end before the start: it becomes the start, the old start the end.
        Tap(calendar, Day(5));
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 11, 5), new DateTime(2026, 11, 10)), calendar.SelectedRange);
        // A start after the end clears the end.
        target.Value = SkUiCalendarRangeTarget.Start;
        Tap(calendar, Day(20));
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 11, 20)), calendar.SelectedRange);
        target.Value = SkUiCalendarRangeTarget.Start;
        calendar.SelectedRange = new SkUiDateRange(new DateTime(2026, 11, 3), new DateTime(2026, 11, 12));
        Tap(calendar, Day(6)); // a start before the end keeps the end
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 11, 6), new DateTime(2026, 11, 12)), calendar.SelectedRange);
        // The start tapped again: the range stays, the target still moves to the end (written back, no selection change).
        target.Value = SkUiCalendarRangeTarget.Start;
        var changes = 0;
        calendar.SelectionChanged += (_, _) => changes++;
        Tap(calendar, Day(6));
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 11, 6), new DateTime(2026, 11, 12)), calendar.SelectedRange);
        Assert.Equal(SkUiCalendarRangeTarget.End, target.Value);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void MarkersSitInAnAreaUnderTheNumber()
    {
        var calendar = new SkUiCalendar { DisplayDate = November, FirstDayOfWeek = DayOfWeek.Monday };
        Assert.Equal(Height, ((IView)calendar).Measure(Width, double.PositiveInfinity).Height);
        calendar.DayStyle = day => day.Day == 10
            ? new SkUiCalendarDayStyle { Markers = [SkUiCalendarMarker.Bar(Colors.Blue), SkUiCalendarMarker.Dot(Colors.Red), SkUiCalendarMarker.Caption("Off")] }
            : null;
        // Rows grow by the look's marker area (14) when the app styles days.
        Assert.Equal(Height + 6 * 14, ((IView)calendar).Measure(Width, double.PositiveInfinity).Height);
        calendar.DayMarkerAreaHeight = 20;
        Assert.Equal(Height + 6 * 20, ((IView)calendar).Measure(Width, double.PositiveInfinity).Height);

        using var surface = new SkUiTestSurface(calendar, Width, Height + 6 * 20);
        var cells = new List<SKRect>();
        SkUiLook.Current.CalendarCellPainter = (_, cell) => cells.Add(cell.Bounds);
        var painted = new List<SkUiCalendarDayContext>();
        calendar.DayPainter = (SKCanvas _, in SkUiCalendarDayContext day) =>
        {
            painted.Add(day);
            return false;
        };
        surface.Frame(0);
        Assert.Equal(42, painted.Count);
        var tenth = painted.Single(day => day.Date == new DateTime(2026, 11, 10));
        // The chrome takes the cell above the marker area: 64 tall rows, 44 for the number.
        Assert.Equal(64, tenth.Bounds.Height, 3);
        Assert.Equal(44, tenth.NumberBounds.Height, 3);
        Assert.Equal(new SKRect(tenth.Bounds.Left, tenth.NumberBounds.Bottom, tenth.Bounds.Right, tenth.Bounds.Bottom), tenth.MarkerBounds);
        Assert.Contains(tenth.NumberBounds, cells);
        Assert.NotNull(tenth.Style);
        Assert.True(painted.Single(day => day.Date == new DateTime(2026, 10, 30)).IsAdjacent);
    }

    [Fact]
    public void ADayPainterCanDrawTheWholeDay()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var calendar = new SkUiCoreCalendar().SetDisplayDate(November).SetFirstDayOfWeek(DayOfWeek.Monday).SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        calendar.SetBackground(Colors.White);
        using var plain = new SkUiTestSurface(new SkUiCoreHost().SetContent(calendar), Width, Height + 6 * 14);
        calendar.SetDayPainter((SKCanvas _, in SkUiCalendarDayContext _) => false);
        var withNumbers = plain.Frame(0).Copy();
        // A painter that draws the day itself: the calendar draws no number, and the painter's text goes through the engine.
        calendar.SetDayPainter((SKCanvas canvas, in SkUiCalendarDayContext day) =>
        {
            if (day.Date.Day != 10 || day.IsAdjacent)
                return true;
            day.DrawText(canvas, "X", day.NumberBounds, SKColors.Black);
            return true;
        });
        var custom = plain.Frame(16);
        // Rows are 58 tall (44 + the marker area): the numbers sit in the upper 44.
        static bool Blank(SKBitmap bitmap, int day)
        {
            var index = (new DateTime(2026, 11, day) - new DateTime(2026, 10, 26)).Days;
            var (left, top) = (40 * (index % 7), 48 + 28 + 58 * (index / 7));
            for (var y = top + 12; y < top + 32; y++)
                for (var x = left + 10; x < left + 30; x++)
                    if (bitmap.GetPixel(x, y) != SKColors.White)
                        return false;
            return true;
        }
        Assert.False(Blank(withNumbers, 11));
        Assert.True(Blank(custom, 11), "The calendar drew a number the painter took over.");
        Assert.False(Blank(custom, 10), "The painter's text was not drawn.");
        withNumbers.Dispose();
    }

    [Fact]
    public void DaysOutsideTheRulesCannotBeChosenButAreReported()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        calendar.MinimumDate = new DateTime(2026, 11, 5);
        calendar.BlackoutDates!.Add(new DateTime(2026, 11, 12));
        calendar.DateEnabled = date => date.DayOfWeek != DayOfWeek.Sunday;
        var reported = new List<(DateTime, bool)>();
        calendar.DateTapped += (_, args) => reported.Add((args.Date, args.IsEnabled));

        Tap(calendar, Day(3));
        Tap(calendar, Day(12));
        Tap(calendar, Day(15)); // a Sunday
        Assert.Null(calendar.SelectedDate);
        Tap(calendar, Day(13));
        Assert.Equal(new DateTime(2026, 11, 13), calendar.SelectedDate);
        Assert.Equal([(new DateTime(2026, 11, 3), false), (new DateTime(2026, 11, 12), false), (new DateTime(2026, 11, 15), false),
            (new DateTime(2026, 11, 13), true)], reported);

        // The page cannot move before the minimum's month, and the display date is kept in the range.
        Assert.False(calendar.CanGoToPrevious);
        Tap(calendar, PreviousButton);
        Assert.Equal(new DateTime(2026, 11, 5), calendar.DisplayDate);
        calendar.DisplayDate = new DateTime(2026, 1, 1);
        Assert.Equal(new DateTime(2026, 11, 5), calendar.DisplayDate);
    }

    [Fact]
    public void TheButtonsMoveThePageAndTheDisplayDateIsWrittenBack()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        var source = BindingSource<DateTime>.Bind(calendar, SkUiCalendar.DisplayDateProperty, November);
        var titles = new List<string>();
        calendar.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiCalendar.Title))
                titles.Add(calendar.Title);
        };
        Assert.Equal("November 2026", calendar.Title);
        Tap(calendar, NextButton);
        Assert.Equal(new DateTime(2026, 12, 1), calendar.DisplayDate);
        Assert.Equal(["December 2026"], titles);
        Assert.True(calendar.Engine.IsAnimating); // the page slides in
        Tap(calendar, PreviousButton);
        Tap(calendar, PreviousButton);
        Assert.Equal(new DateTime(2026, 10, 1), calendar.DisplayDate);
        source.Value = new DateTime(2027, 3, 9); // the binding still applies
        Assert.Equal(new DateTime(2027, 3, 9), calendar.DisplayDate);
        Assert.Equal("March 2027", calendar.Title);
        Assert.True(calendar.GoToNext(animated: false));
        Assert.Equal(new DateTime(2027, 4, 1), calendar.DisplayDate);
        Assert.False(calendar.Engine.IsAnimating);
    }

    [Fact]
    public void ASwipeSettlesOnTheNextPageOrBack()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        // A short slow drag goes back.
        Swipe(calendar, Day(18), -40, 0, milliseconds: 600);
        Assert.Equal(November, calendar.DisplayDate);
        Assert.Null(calendar.SelectedDate); // a drag is not a tap
        // Past a third of the page, the next month.
        Swipe(calendar, Day(18), -120, 0);
        Assert.Equal(new DateTime(2026, 12, 1), calendar.DisplayDate);
        // A fling settles however short it is; right to left, back.
        Swipe(calendar, Day(18), 50, 0, milliseconds: 60);
        Assert.Equal(November, calendar.DisplayDate);
        // A drag across the navigation axis is left to scrollers around the calendar.
        Swipe(calendar, Day(18), 0, -120);
        Assert.Equal(November, calendar.DisplayDate);
        calendar.NavigationOrientation = StackOrientation.Vertical;
        Swipe(calendar, Day(18), 0, -150);
        Assert.Equal(new DateTime(2026, 12, 1), calendar.DisplayDate);
        calendar.IsSwipeEnabled = false;
        Swipe(calendar, Day(18), 0, -150);
        Assert.Equal(new DateTime(2026, 12, 1), calendar.DisplayDate);
    }

    [Fact]
    public void TheTitleStepsUpAModeAndACellStepsDown()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        Tap(calendar, TitlePoint);
        Assert.Equal(SkUiCalendarDisplayMode.Year, calendar.DisplayMode);
        Assert.Equal("2026", calendar.Title);
        // Year page cells: 4 rows of 3 in the area below the header, 93.3 × 73 each; March is the third.
        Tap(calendar, new Point(233, 48 + 36));
        Assert.Equal(SkUiCalendarDisplayMode.Month, calendar.DisplayMode);
        Assert.Equal(new DateTime(2026, 3, 1), calendar.DisplayDate);
        Assert.Null(calendar.SelectedDate);

        Tap(calendar, TitlePoint);
        Tap(calendar, TitlePoint);
        Tap(calendar, TitlePoint);
        Assert.Equal(SkUiCalendarDisplayMode.Century, calendar.DisplayMode);
        Tap(calendar, TitlePoint); // from the highest mode back to the days
        Assert.Equal(SkUiCalendarDisplayMode.Month, calendar.DisplayMode);

        calendar.MaximumDisplayMode = SkUiCalendarDisplayMode.Month;
        Tap(calendar, TitlePoint);
        Assert.Equal(SkUiCalendarDisplayMode.Month, calendar.DisplayMode);
    }

    [Fact]
    public void AMonthPickerChoosesMonths()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        calendar.MinimumDisplayMode = SkUiCalendarDisplayMode.Year;
        Assert.Equal(SkUiCalendarDisplayMode.Year, calendar.DisplayMode); // kept within the limits, written back
        Tap(calendar, new Point(233, 48 + 36));
        Assert.Equal(new DateTime(2026, 3, 1), calendar.SelectedDate);
        Assert.Equal(SkUiCalendarDisplayMode.Year, calendar.DisplayMode);
    }

    [Fact]
    public void ATapOnANeighborMonthsDayShowsItsMonth()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        Tap(calendar, DayCenter(new DateTime(2026, 10, 27)));
        Assert.Equal(new DateTime(2026, 10, 27), calendar.SelectedDate);
        Assert.Equal(10, calendar.DisplayDate.Month);
        calendar.AreAdjacentDaysVisible = false;
        calendar.DisplayDate = November;
        Tap(calendar, DayCenter(new DateTime(2026, 10, 27)));
        Assert.Equal(new DateTime(2026, 10, 27), calendar.SelectedDate);
        Assert.Equal(11, calendar.DisplayDate.Month);
    }

    [Fact]
    public void KeysMoveTheFocusAndChoose()
    {
        var calendar = new SkUiCalendar { DisplayDate = November, FirstDayOfWeek = DayOfWeek.Monday, SelectedDate = new DateTime(2026, 11, 10) };
        var root = new SkUiVerticalStackLayout { Children = { calendar } };
        SkUiTestHelpers.Arrange(root, Width, Height);
        calendar.Focus();
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Right));
        Assert.Equal(new DateTime(2026, 11, 11), calendar.Engine.FocusDate);
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Down));
        Assert.Equal(new DateTime(2026, 11, 18), calendar.Engine.FocusDate);
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Enter));
        Assert.Equal(new DateTime(2026, 11, 18), calendar.SelectedDate);
        Assert.True(root.FocusManager.KeyDown(SkUiKey.PageDown));
        Assert.Equal(new DateTime(2026, 12, 18), calendar.Engine.FocusDate);
        Assert.Equal(12, calendar.DisplayDate.Month); // the focus's page slid in
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Home));
        Assert.Equal(new DateTime(2026, 12, 14), calendar.Engine.FocusDate);

        calendar.FlowDirection = FlowDirection.RightToLeft;
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Left)); // mirrored: the next day
        Assert.Equal(new DateTime(2026, 12, 15), calendar.Engine.FocusDate);
    }

    [Fact]
    public void ScreenReadersReadTheFocusedDayAndMoveIt()
    {
        var calendar = new SkUiCalendar { DisplayDate = November, SelectedDate = new DateTime(2026, 11, 10) };
        SkUiTestHelpers.Arrange(calendar, Width, Height);
        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)calendar).GetSemantics(info);
        Assert.Equal(SkUiSemanticsRole.Slider, info.Role);
        Assert.Equal("November 2026", info.Text);
        Assert.Equal("Tuesday, November 10, 2026, Selected", info.Value);
        Assert.True(((ISkUiAccessibleNode)calendar).PerformSemanticsAction(SkUiSemanticsActions.Increment));
        Assert.True(((ISkUiAccessibleNode)calendar).PerformSemanticsAction(SkUiSemanticsActions.Activate));
        Assert.Equal(new DateTime(2026, 11, 11), calendar.SelectedDate);
    }

    [Fact]
    public void TheCoreCalendarChoosesAndNavigatesAsTheSkUiOne()
    {
        var calendar = CoreCalendar(out var host, out var surface);
        using var _ = surface;
        var notified = new List<string?>();
        calendar.PropertyChanged += (_, args) => notified.Add(args.PropertyName);
        Tap(host, Day(10));
        Assert.Equal(new DateTime(2026, 11, 10), calendar.SelectedDate);
        Assert.Contains(nameof(SkUiCoreCalendar.SelectedDate), notified);
        Tap(host, NextButton);
        Assert.Equal(new DateTime(2026, 12, 1), calendar.DisplayDate);
        Assert.Contains(nameof(SkUiCoreCalendar.DisplayDate), notified);
        Assert.Equal("December 2026", calendar.Title);
        calendar.SetSelectionMode(SkUiCalendarSelectionMode.Range).SetSelectedRange(new SkUiDateRange(new DateTime(2026, 12, 1), new DateTime(2026, 12, 3)));
        Assert.Equal(3, calendar.SelectedRange!.Value.Days);
        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.SetVisibleWeeks(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.SetDisplayMode((SkUiCalendarDisplayMode)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.SetFontSize(-1));

        // The start tapped again while it is the target: the target moves to the end, the selection does not change.
        calendar.SetDisplayDate(November).SetSelectedRange(new SkUiDateRange(new DateTime(2026, 11, 6), new DateTime(2026, 11, 12)))
            .SetRangeSelectionTarget(SkUiCalendarRangeTarget.Start);
        surface.Frame(16);
        notified.Clear();
        var changes = 0;
        calendar.SelectionChanged += (_, _) => changes++;
        Tap(host, Day(6));
        Assert.Equal(SkUiCalendarRangeTarget.End, calendar.RangeSelectionTarget);
        Assert.Contains(nameof(SkUiCoreCalendar.RangeSelectionTarget), notified);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void AWeeklyStripPagesByWeeks()
    {
        var calendar = new SkUiCoreCalendar().SetDisplayDate(new DateTime(2026, 11, 19)).SetFirstDayOfWeek(DayOfWeek.Monday).SetVisibleWeeks(1);
        Assert.Equal(new DateTime(2026, 11, 16), calendar.Model.PageStart);
        Assert.True(calendar.GoToNext(animated: false));
        Assert.Equal(new DateTime(2026, 11, 23), calendar.DisplayDate);
        Assert.Equal(new Size(Width, 48 + 28 + 44), calendar.Measure(Width, double.PositiveInfinity));
    }

    [Fact]
    public void BothLayersDrawTheSamePixels()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var calendar = new SkUiCalendar
        {
            DisplayDate = November, FirstDayOfWeek = DayOfWeek.Monday, FontFamily = SkUiTestHelpers.BundledFontFamily,
            SelectionMode = SkUiCalendarSelectionMode.Range, SelectedRange = new SkUiDateRange(new DateTime(2026, 11, 10), new DateTime(2026, 11, 13)),
            AreWeekNumbersVisible = true, DayStyle = Marked, BackgroundColor = Colors.White
        };
        var core = new SkUiCoreCalendar().SetDisplayDate(November).SetFirstDayOfWeek(DayOfWeek.Monday).SetFontFamily(SkUiTestHelpers.BundledFontFamily)
            .SetSelectionMode(SkUiCalendarSelectionMode.Range).SetSelectedRange(new SkUiDateRange(new DateTime(2026, 11, 10), new DateTime(2026, 11, 13)))
            .SetAreWeekNumbersVisible(true).SetDayStyle(Marked);
        core.SetBackground(Colors.White);
        using var skui = new SkUiTestSurface(calendar, Width + 32, Height + 6 * 14);
        using var coreSurface = new SkUiTestSurface(new SkUiCoreHost().SetContent(core), Width + 32, Height + 6 * 14);
        var expected = skui.Frame(0).Bytes;
        var actual = coreSurface.Frame(0).Bytes;
        Assert.True(expected.AsSpan().SequenceEqual(actual), "The SkUi and Core calendars draw different pixels.");
        Assert.Contains(expected, value => value != 0xFF); // something was drawn on the white

        static SkUiCalendarDayStyle? Marked(DateTime day) => day.Day == 20 ? new SkUiCalendarDayStyle { Markers = [SkUiCalendarMarker.Dot(Colors.Green), SkUiCalendarMarker.Bar(), SkUiCalendarMarker.Caption("Hi")], IsStrikethrough = true } : null;
    }

    [Fact]
    public void TextsAreKeptPerTextAndReleasedWithTheDrawing()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        var kept = calendar.Engine.CachedTextCount;
        Assert.InRange(kept, 30, 60); // day numbers, day names, the title
        Tap(calendar, NextButton);
        for (var ms = 16; ms <= 400; ms += 16)
        {
            surface.Root.AnimationClock.Tick(TimeSpan.FromMilliseconds(ms));
            surface.Frame(ms);
        }
        Assert.InRange(calendar.Engine.CachedTextCount, kept, kept + 4); // the next month's numbers are the same texts
        Assert.False(calendar.Engine.IsAnimating);
        calendar.Engine.Release();
        Assert.Equal(0, calendar.Engine.CachedTextCount);
    }
    private static SkUiDateRange Range(int first, int last) => new(new DateTime(2026, 11, first), new DateTime(2026, 11, last));

    [Fact]
    public void RangeLengthRulesLimitWhereARangeCanEnd()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        calendar.SelectionMode = SkUiCalendarSelectionMode.Range;
        calendar.MinimumRangeDays = 3;
        calendar.MaximumRangeDays = 7;
        Tap(calendar, Day(10));
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 11, 10)), calendar.SelectedRange);
        // While the range waits for its end, days that would end it too short or too long cannot be chosen.
        Assert.False(calendar.Engine.IsChoosable(new DateTime(2026, 11, 11)));
        Assert.False(calendar.Engine.IsChoosable(new DateTime(2026, 11, 17)));
        Assert.True(calendar.Engine.IsChoosable(new DateTime(2026, 11, 16)));
        Assert.True(calendar.Engine.IsChoosable(new DateTime(2026, 11, 4))); // backwards too: 4 to 10 is seven days
        Tap(calendar, Day(11));
        Tap(calendar, Day(10)); // its own first day: still open
        Tap(calendar, Day(20));
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 11, 10)), calendar.SelectedRange);
        Tap(calendar, Day(16));
        Assert.Equal(Range(10, 16), calendar.SelectedRange);
        // A whole range: the next tap starts one anywhere.
        Assert.True(calendar.Engine.IsChoosable(new DateTime(2026, 11, 11)));
        // The app's ranges are not checked.
        calendar.SelectedRange = Range(1, 30);
        Assert.Equal(30, calendar.SelectedRange!.Value.Days);
        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.SetMaximumRangeDays(-1));
    }

    [Fact]
    public void ARangeCanBeKeptFromRunningOverUnavailableDays()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        calendar.SelectionMode = SkUiCalendarSelectionMode.Range;
        calendar.AllowsDisabledDaysInRange = false;
        calendar.BlackoutDates!.Add(new DateTime(2026, 11, 13));
        Tap(calendar, Day(10));
        Tap(calendar, Day(16)); // over the 13th: not taken
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 11, 10)), calendar.SelectedRange);
        Tap(calendar, Day(12));
        Assert.Equal(Range(10, 12), calendar.SelectedRange);
        Tap(calendar, Day(14));
        Assert.False(calendar.Engine.IsChoosable(new DateTime(2026, 11, 8)));
        Assert.True(calendar.Engine.IsChoosable(new DateTime(2026, 11, 28)));
        // A rule of the app's that changes is read again after RefreshDayStyles.
        calendar.DateEnabled = date => date.Day != 20;
        Assert.False(calendar.Engine.IsChoosable(new DateTime(2026, 11, 28)));
        Tap(calendar, Day(19));
        Assert.Equal(Range(14, 19), calendar.SelectedRange);
        // Allowed again: a range runs over the blackout day (which is still not chosen by itself).
        calendar.AllowsDisabledDaysInRange = true;
        Tap(calendar, Day(10));
        Tap(calendar, Day(16));
        Assert.Equal(Range(10, 16), calendar.SelectedRange);
    }

    [Fact]
    public void MultipleRangesJoinAndATapOnOneRemovesIt()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        calendar.SelectionMode = SkUiCalendarSelectionMode.MultipleRanges;
        var bound = BindingSource<IReadOnlyList<SkUiDateRange>>.Bind(calendar, SkUiCalendar.SelectedRangesProperty, [], BindingMode.TwoWay);
        var changes = 0;
        calendar.SelectionChanged += (_, _) => changes++;
        Tap(calendar, Day(3));
        Tap(calendar, Day(5));
        Tap(calendar, Day(12));
        Assert.Equal([Range(3, 5), new SkUiDateRange(new DateTime(2026, 11, 12))], calendar.SelectedRanges);
        Tap(calendar, Day(10)); // ends before its start: ordered
        Assert.Equal([Range(3, 5), Range(10, 12)], bound.Value);
        Assert.Equal(Range(10, 12), calendar.SelectedRange); // the range changed last
        Assert.Equal(4, changes);

        // A range that reaches the others joins them.
        Tap(calendar, Day(6));
        Tap(calendar, Day(9));
        Assert.Equal([Range(3, 12)], calendar.SelectedRanges);
        // A tap on a chosen range removes it.
        Tap(calendar, Day(7));
        Assert.Empty(calendar.SelectedRanges);
        Assert.Null(calendar.SelectedRange);

        // The app's ranges are ordered and joined.
        bound.Value = [Range(20, 22), Range(1, 2), Range(2, 4), Range(23, 23)];
        Assert.Equal([Range(1, 4), Range(20, 23)], calendar.SelectedRanges);
        Assert.Equal([Range(1, 4), Range(20, 23)], bound.Value);
        bound.Value = [Range(1, 10), Range(2, 3), Range(5, 6), Range(11, 12)];
        Assert.Equal([Range(1, 12)], calendar.SelectedRanges);
        // A range tapped over two others past a gap takes in both.
        bound.Value = [Range(2, 3), Range(5, 6)];
        Tap(calendar, Day(1));
        Tap(calendar, Day(10));
        Assert.Equal([Range(1, 10)], calendar.SelectedRanges);
        calendar.ClearSelection();
        Assert.Empty(bound.Value);

        // The band is drawn for each range.
        var paints = new List<SkUiCalendarCellPaint>();
        SkUiLook.Current.CalendarCellPainter = (_, paint) => paints.Add(paint);
        calendar.SelectedRanges = [Range(2, 3), Range(9, 10)];
        surface.Frame(16);
        Assert.Equal(4, paints.Count(paint => paint.RangePart is SkUiCalendarRangePart.Start or SkUiCalendarRangePart.End));
    }

    [Fact]
    public void TheCoreCalendarHasTheRangeRulesAndMultipleRanges()
    {
        var calendar = CoreCalendar(out var host, out var surface);
        using var _ = surface;
        calendar.SetSelectionMode(SkUiCalendarSelectionMode.MultipleRanges).SetMaximumRangeDays(3);
        var notified = new List<string?>();
        calendar.PropertyChanged += (_, args) => notified.Add(args.PropertyName);
        Tap(host, Day(3));
        Tap(host, Day(9)); // seven days: too long
        Tap(host, Day(5));
        Assert.Equal([Range(3, 5)], calendar.SelectedRanges);
        Assert.Contains(nameof(SkUiCoreCalendar.SelectedRanges), notified);
        calendar.SetSelectedRanges([Range(1, 1), Range(2, 2)]);
        Assert.Equal([Range(1, 2)], calendar.SelectedRanges);
        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.SetMinimumRangeDays(-2));
        calendar.SetAllowsDisabledDaysInRange(false);
        Assert.False(calendar.AllowsDisabledDaysInRange);
    }

    [Fact]
    public void EveryDayAndTheHeaderAreElementsOfTheirOwnForScreenReaders()
    {
        var calendar = new SkUiCalendar
        {
            DisplayDate = November,
            FirstDayOfWeek = DayOfWeek.Monday,
            SelectedDate = new DateTime(2026, 11, 10),
            MinimumDate = new DateTime(2026, 11, 3),
            AutomationId = "Cal"
        };
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(calendar);
        SkUiTestHelpers.Arrange(stack, Width, Height);
        var nodes = SkUiSemanticsTree.Build(stack, exposeAutomationIds: true).Flatten().ToList();
        var element = nodes.Single(node => ReferenceEquals(node.Source, calendar) && node.PartIndex < 0);
        var parts = nodes.Where(node => ReferenceEquals(node.Source, calendar) && node.PartIndex >= 0).ToList();
        Assert.All(parts, part => Assert.Same(element, part.Parent));
        Assert.Equal(3 + 42, parts.Count); // title, previous, next and six weeks of days

        var tenth = parts.Single(part => part.Label == "Tuesday, November 10, 2026");
        Assert.Equal("Selected", tenth.Value);
        Assert.Equal("Cal_2026-11-10", tenth.AutomationId);
        Assert.Equal(new Rect(40, 48 + 28 + 2 * 44, 40, 44), tenth.Bounds);
        var second = parts.Single(part => part.Label == "Monday, November 2, 2026");
        Assert.False(second.IsEnabled); // before the minimum
        Assert.False(parts.Single(part => part.Label == "Previous").IsEnabled);
        Assert.True(parts.Single(part => part.Label == "Next").IsEnabled);
        Assert.Equal("November 2026", parts.Single(part => part.AutomationId == "Cal_Title").Label);

        // Activating a day chooses it; the buttons move the page.
        var accessible = (ISkUiAccessibleNode)calendar;
        var twelfth = parts.Single(part => part.Label == "Thursday, November 12, 2026");
        Assert.True(accessible.PerformSemanticsPartAction(twelfth.PartIndex, SkUiSemanticsActions.Activate));
        Assert.Equal(new DateTime(2026, 11, 12), calendar.SelectedDate);
        Assert.True(accessible.PerformSemanticsPartAction(parts.Single(part => part.Label == "Next").PartIndex, SkUiSemanticsActions.Activate));
        Assert.Equal(new DateTime(2026, 12, 1), calendar.DisplayDate);
        var again = SkUiSemanticsTree.Build(stack).Flatten().Where(node => ReferenceEquals(node.Source, calendar) && node.PartIndex >= 0).ToList();
        // A cell's element keeps its id while its date changes with the page.
        Assert.Equal(twelfth.Id, again.Single(part => part.PartIndex == twelfth.PartIndex).Id);
        Assert.Contains(again, part => part.Label == "Thursday, December 10, 2026");
    }
}
