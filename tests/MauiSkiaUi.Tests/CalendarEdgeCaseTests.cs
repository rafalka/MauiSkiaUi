using System.Globalization;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The calendar at its edges (1.15 A3): the first and last dates .NET has, settings changed while a page slides, a culture or the
/// app's strings changed while a page is shown, and range targets against the range rules. November 2026 in en-US, Monday weeks.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class CalendarEdgeCaseTests : IDisposable
{
    private const int Width = 280;
    private const int Height = 48 + 28 + 6 * 44;
    private static readonly DateTime November = new(2026, 11, 1);
    private static long _pointer = 1_150_000;

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
    private readonly IDisposable _dispatcher = SkUiTestHelpers.UseTestDispatcher();

    public CalendarEdgeCaseTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
        _dispatcher.Dispose();
    }

    private static Point Day(int day)
    {
        var index = (new DateTime(2026, 11, day) - new DateTime(2026, 10, 26)).Days;
        return new Point(20 + 40 * (index % 7), 48 + 28 + 22 + 44 * (index / 7));
    }

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        var start = TimeSpan.FromSeconds(id);
        root.Touch(new(id, SkUiTouchAction.Pressed, at, start));
        root.Touch(new(id, SkUiTouchAction.Released, at, start + TimeSpan.FromMilliseconds(30)));
    }

    private static SkUiDateRange Range(int first, int last) => new(new DateTime(2026, 11, first), new DateTime(2026, 11, last));

    private static SkUiCalendar Calendar(out SkUiTestSurface surface, DateTime? display = null)
    {
        var calendar = new SkUiCalendar { DisplayDate = display ?? November, FirstDayOfWeek = DayOfWeek.Monday };
        surface = new SkUiTestSurface(calendar, Width, Height);
        surface.Frame(0);
        return calendar;
    }

    [Theory]
    [InlineData(SkUiCalendarDisplayMode.Month)]
    [InlineData(SkUiCalendarDisplayMode.Year)]
    [InlineData(SkUiCalendarDisplayMode.Decade)]
    [InlineData(SkUiCalendarDisplayMode.Century)]
    public void TheFirstAndLastDatesNetHasAreDrawnAndThePagesStopThere(SkUiCalendarDisplayMode mode)
    {
        var calendar = Calendar(out var surface, DateTime.MaxValue);
        using var _ = surface;
        calendar.DisplayMode = mode;
        surface.Frame(16);
        Assert.False(calendar.CanGoToNext);
        Assert.False(calendar.GoToNext(animated: false));
        Assert.Equal(DateTime.MaxValue.Date.Year, calendar.DisplayDate.Year);
        calendar.Engine.OnKey(new SkUiKeyEvent(SkUiKey.PageDown, SkUiKeyModifiers.Shift, false));
        surface.Frame(32);

        calendar.DisplayDate = DateTime.MinValue;
        surface.Frame(48);
        Assert.False(calendar.CanGoToPrevious);
        Assert.False(calendar.GoToPrevious(animated: false));
        Assert.Equal(1, calendar.DisplayDate.Year);
        calendar.Engine.OnKey(new SkUiKeyEvent(SkUiKey.PageUp, SkUiKeyModifiers.Shift, false));
        calendar.Engine.OnKey(new SkUiKeyEvent(SkUiKey.Left, SkUiKeyModifiers.None, false));
        surface.Frame(64);

        // Explicit limits at the ends behave as no limits.
        calendar.MinimumDate = DateTime.MinValue;
        calendar.MaximumDate = DateTime.MaxValue;
        calendar.GoToDate(DateTime.MaxValue, animated: false);
        surface.Frame(80);
        Assert.False(calendar.CanGoToNext);
    }

    [Fact]
    public void GoingToADateOutsideTheLimitsShowsTheNearestLimit()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        calendar.MinimumDate = new DateTime(2026, 6, 10);
        calendar.MaximumDate = new DateTime(2027, 2, 20);
        calendar.GoToDate(new DateTime(2030, 1, 1), animated: false);
        Assert.Equal(new DateTime(2027, 2, 1), new DateTime(calendar.DisplayDate.Year, calendar.DisplayDate.Month, 1));
        calendar.GoToDate(new DateTime(2020, 1, 1), animated: false);
        Assert.Equal(new DateTime(2026, 6, 1), new DateTime(calendar.DisplayDate.Year, calendar.DisplayDate.Month, 1));
        Assert.False(calendar.CanGoToPrevious);
    }

    [Fact]
    public void ACultureChangeWhileAPageIsShownChangesItsTexts()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        calendar.FirstDayOfWeek = null;
        Assert.Equal("November 2026", calendar.Title);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        calendar.GoToNext(animated: false);
        surface.Frame(16);
        Assert.Equal("Dezember 2026", calendar.Title);
        Assert.Equal(DayOfWeek.Monday, calendar.Engine.EffectiveFirstDayOfWeek);
    }

    [Fact]
    public void SettingsChangedMidSlideEndOnAPageThatIsShown()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        var engine = calendar.Engine;

        engine.Step(1, animated: true);
        Assert.NotEqual(0, engine.SlideOffset);
        calendar.FlowDirection = FlowDirection.RightToLeft;
        surface.Frame(16);
        calendar.VisibleWeeks = 2;
        surface.Frame(32);
        engine.StopAnimations();
        surface.Frame(48);
        Assert.Equal(0, engine.SlideOffset);
        Assert.Equal(2026, calendar.DisplayDate.Year);

        // A swipe caught by a change of the display mode's limits: the mode follows the limits and the page settles.
        engine.BeginDrag();
        engine.Drag(-40, Width, Height);
        calendar.MinimumDisplayMode = SkUiCalendarDisplayMode.Year;
        Assert.Equal(SkUiCalendarDisplayMode.Year, calendar.DisplayMode);
        engine.EndDrag(0, Width, Height);
        engine.StopAnimations();
        surface.Frame(64);
        Assert.Equal(0, engine.SlideOffset);
        Assert.False(engine.IsAnimating);
    }

    [Fact]
    public void TheAppsStringsChangedDuringASlideAreDrawn()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        var strings = SkUiStrings.Current;
        try
        {
            calendar.Engine.Step(1, animated: true);
            SkUiStrings.Current = new SkUiStrings { ShortestDayNames = ["S", "M", "T", "W", "T", "F", "S"] };
            surface.Frame(16);
            Assert.Equal("M", calendar.Engine.DayNames(DayOfWeek.Monday, CultureInfo.CurrentCulture)[0]);
        }
        finally
        {
            SkUiStrings.Current = strings;
        }
    }

    [Fact]
    public void AnEndTargetCannotRunARangeOverAnUnavailableDay()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        calendar.SelectionMode = SkUiCalendarSelectionMode.Range;
        calendar.AllowsDisabledDaysInRange = false;
        calendar.BlackoutDates!.Add(new DateTime(2026, 11, 13));
        calendar.SelectedRange = Range(10, 12);
        calendar.RangeSelectionTarget = SkUiCalendarRangeTarget.End;
        Tap(calendar, Day(16)); // over the 13th: not taken
        Assert.Equal(Range(10, 12), calendar.SelectedRange);
        Tap(calendar, Day(11));
        Assert.Equal(Range(10, 11), calendar.SelectedRange);
        // An end before the start becomes the start; the old start, the end, may not run over the 13th either.
        Tap(calendar, Day(5));
        Assert.Equal(Range(5, 10), calendar.SelectedRange);
        // A length rule: an end that would make the range longer than three days is not taken.
        calendar.MaximumRangeDays = 3;
        Tap(calendar, Day(9));
        Assert.Equal(Range(5, 10), calendar.SelectedRange);
        Tap(calendar, Day(7));
        Assert.Equal(Range(5, 7), calendar.SelectedRange);
    }

    [Fact]
    public void MultipleRangesJoinAcrossAMonthThePagesSkip()
    {
        var calendar = Calendar(out var surface);
        using var _ = surface;
        calendar.SelectionMode = SkUiCalendarSelectionMode.MultipleRanges;
        calendar.SelectedRanges =
        [
            new SkUiDateRange(new DateTime(2026, 10, 20), new DateTime(2026, 12, 5)),
            Range(3, 4),
            new SkUiDateRange(new DateTime(2026, 12, 6), new DateTime(2026, 12, 8)),
        ];
        // Ordered and joined: the long range takes in the one inside it and the one that touches its end.
        Assert.Equal([new SkUiDateRange(new DateTime(2026, 10, 20), new DateTime(2026, 12, 8))], calendar.SelectedRanges);
        // A tap on it removes it, though most of it is on pages not shown.
        Tap(calendar, Day(18));
        Assert.Empty(calendar.SelectedRanges!);
    }

    [Fact]
    public void TheCoreCalendarHandlesTheLastDate()
    {
        var calendar = new SkUiCoreCalendar().SetDisplayDate(DateTime.MaxValue).SetDisplayMode(SkUiCalendarDisplayMode.Century);
        var host = new SkUiCoreHost().SetContent(calendar);
        using var surface = new SkUiTestSurface(host, Width, Height);
        surface.Frame(0);
        Assert.False(calendar.GoToNext(animated: false));
        calendar.SetDisplayDate(DateTime.MinValue).SetDisplayMode(SkUiCalendarDisplayMode.Month);
        surface.Frame(16);
        Assert.False(calendar.GoToPrevious(animated: false));
    }
}
