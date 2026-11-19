using System.Globalization;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// A month view with events (1.15 B5, <see cref="SkUiCalendarDayMarkerLayout.Lines"/>): the number at the top of a day, its
/// markers one per line under it, a "+N" line for what does not fit; screen readers read a day's event captions.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class CalendarEventLinesTests : IDisposable
{
    private const int Width = 280;
    private static readonly DateTime November = new(2026, 11, 1);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly IDisposable _dispatcher = SkUiTestHelpers.UseTestDispatcher();
    private readonly IDisposable _font = SkUiTestHelpers.UseBundledFont();

    public CalendarEventLinesTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        _font.Dispose();
        _dispatcher.Dispose();
        SkUiStrings.Current = new SkUiStrings();
    }

    private static readonly SkUiCalendarDayStyle Busy = new()
    {
        Markers =
        [
            SkUiCalendarMarker.Caption("Standup", Colors.Blue), SkUiCalendarMarker.Caption("Lunch", Colors.Orange),
            SkUiCalendarMarker.Bar(Colors.Green), SkUiCalendarMarker.Caption("Review", Colors.Purple), SkUiCalendarMarker.Dot(Colors.Red),
        ]
    };

    private static readonly SkUiCalendarDayStyle Light = new()
    {
        Markers = [SkUiCalendarMarker.Caption("Gym", Colors.Blue), SkUiCalendarMarker.Dot(Colors.Red)]
    };

    private static SkUiCalendarDayStyle? Style(DateTime day) => day.Day == 10 ? Busy : day.Day == 11 ? Light : null;

    [Fact]
    public void ARowHoldsTheNumberAndThreeLinesWhenTheCalendarSizesItself()
    {
        var calendar = new SkUiCalendar { DisplayDate = November, DayStyle = Style };
        var look = SkUiLook.Current;
        var row = look.CalendarRowHeight + look.CalendarMarkerAreaHeight;
        var chrome = look.CalendarHeaderHeight + look.CalendarDayNamesHeight;
        Assert.Equal(chrome + 6 * row, ((IView)calendar).Measure(Width, double.PositiveInfinity).Height, 3);

        calendar.DayMarkerLayout = SkUiCalendarDayMarkerLayout.Lines;
        Assert.Equal(chrome + 6 * (look.CalendarEventNumberHeight + 3 * look.CalendarMarkerLineHeight),
            ((IView)calendar).Measure(Width, double.PositiveInfinity).Height, 3);
        calendar.DayMarkerLineHeight = 20;
        Assert.Equal(chrome + 6 * (look.CalendarEventNumberHeight + 60), ((IView)calendar).Measure(Width, double.PositiveInfinity).Height, 3);
        calendar.DayMarkerAreaHeight = 30;
        Assert.Equal(chrome + 6 * (look.CalendarEventNumberHeight + 30), ((IView)calendar).Measure(Width, double.PositiveInfinity).Height, 3);
        calendar.DayRowHeight = 100; // the whole day
        Assert.Equal(chrome + 6 * 100, ((IView)calendar).Measure(Width, double.PositiveInfinity).Height, 3);

        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.SetDayMarkerLineHeight(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.SetDayMarkerLayout((SkUiCalendarDayMarkerLayout)7));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EventsAreLinesUnderTheNumberAndWhatDoesNotFitIsCounted(bool core)
    {
        // 76 + 6 × 76: each day 28 for the number and 48 for three 16-tall lines.
        const int height = 76 + 6 * 76;
        var contexts = new Dictionary<DateTime, SkUiCalendarDayContext>();
        var cells = new Dictionary<SKRect, bool>();
        bool Painter(SKCanvas _, in SkUiCalendarDayContext day)
        {
            contexts[day.Date] = day;
            return false;
        }
        var texts = new List<string>();
        SkUiTestSurface surface;
        SkUiCalendarModel engine;
        if (core)
        {
            var calendar = new SkUiCoreCalendar().SetDisplayDate(November).SetFirstDayOfWeek(DayOfWeek.Monday).SetFontFamily(SkUiTestHelpers.BundledFontFamily)
                .SetDayStyle(Style).SetDayPainter(Painter).SetDayMarkerLayout(SkUiCalendarDayMarkerLayout.Lines);
            surface = new SkUiTestSurface(new SkUiCoreHost().SetContent(calendar), Width, height);
            engine = calendar.Engine;
        }
        else
        {
            var calendar = new SkUiCalendar
            {
                DisplayDate = November, FirstDayOfWeek = DayOfWeek.Monday, FontFamily = SkUiTestHelpers.BundledFontFamily, DayStyle = Style,
                DayPainter = Painter, DayMarkerLayout = SkUiCalendarDayMarkerLayout.Lines,
            };
            surface = new SkUiTestSurface(calendar, Width, height);
            engine = calendar.Engine;
        }
        using var _ = surface;
        SkUiLook.Current.CalendarCellPainter = (_, cell) => cells[cell.Bounds] = cell.IsToday;
        try
        {
            surface.Frame(0);
        }
        finally
        {
            SkUiLook.Current.CalendarCellPainter = null;
        }

        var tenth = contexts[new DateTime(2026, 11, 10)];
        Assert.Equal(28, tenth.NumberBounds.Height, 3);
        Assert.Equal(48, tenth.MarkerBounds.Height, 3);
        Assert.Contains(tenth.NumberBounds, cells.Keys); // the look's chrome takes the number's part
        var bitmap = surface.Bitmap;
        // Five markers in three lines: Standup, Lunch, then "+3" (the bar, Review and the dot).
        var area = tenth.MarkerBounds;
        Assert.Equal(SKColors.Blue, bitmap.GetPixel((int)area.Left + 3, (int)area.Top + 8));
        Assert.Equal(SKColors.Orange, bitmap.GetPixel((int)area.Left + 3, (int)area.Top + 24));
        Assert.NotEqual(SKColors.Purple, bitmap.GetPixel((int)area.Left + 3, (int)area.Top + 40));
        Assert.True(InkIn(bitmap, SKRect.Create(area.Left, area.Top + 32, area.Width, 16)), "the +3 line is drawn");
        Assert.Contains(engine.CachedTexts, text => text == "+3");
        // Two markers fit: the caption and, on the next line, the dot.
        var eleventh = contexts[new DateTime(2026, 11, 11)].MarkerBounds;
        Assert.Equal(SKColors.Blue, bitmap.GetPixel((int)eleventh.Left + 3, (int)eleventh.Top + 8));
        Assert.Equal(SKColors.Red, bitmap.GetPixel((int)eleventh.MidX, (int)(eleventh.Top + 16 + 16 * 0.4f)));
        Assert.DoesNotContain(engine.CachedTexts, text => text == "+1");
    }

    [Fact]
    public void ADayTooShortForItsEventsSaysHowManyThereAre()
    {
        SkUiStrings.Current = new SkUiStrings { MoreEventsFormat = "{0} more" };
        var calendar = new SkUiCalendar
        {
            DisplayDate = November, FirstDayOfWeek = DayOfWeek.Monday, FontFamily = SkUiTestHelpers.BundledFontFamily, DayStyle = Style,
            DayMarkerLayout = SkUiCalendarDayMarkerLayout.Lines, DayMarkerAreaHeight = 16,
        };
        using var surface = new SkUiTestSurface(calendar, Width, 76 + 6 * 44);
        surface.Frame(0);
        Assert.Contains(calendar.Engine.CachedTexts, text => text == "5 more");
        Assert.Contains(calendar.Engine.CachedTexts, text => text == "2 more");
    }

    [Fact]
    public void ScreenReadersReadADaysEvents()
    {
        var calendar = new SkUiCalendar
        {
            DisplayDate = November, FirstDayOfWeek = DayOfWeek.Monday, SelectedDate = new DateTime(2026, 11, 10), DayStyle = Style,
        };
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(calendar);
        SkUiTestHelpers.Arrange(stack, Width, 600);
        var parts = SkUiSemanticsTree.Build(stack).Flatten().Where(node => ReferenceEquals(node.Source, calendar) && node.PartIndex >= 0).ToList();
        Assert.Equal("Selected, Standup, Lunch, Review", parts.Single(part => part.Label == "Tuesday, November 10, 2026").Value);
        Assert.Equal("Gym", parts.Single(part => part.Label == "Wednesday, November 11, 2026").Value);
        Assert.Null(parts.Single(part => part.Label == "Thursday, November 12, 2026").Value);
    }

    private static bool InkIn(SKBitmap bitmap, SKRect rect)
    {
        var background = bitmap.GetPixel((int)rect.Right - 1, (int)rect.Bottom - 1);
        for (var y = (int)rect.Top; y < (int)rect.Bottom; y++)
            for (var x = (int)rect.Left; x < (int)rect.MidX; x++)
                if (bitmap.GetPixel(x, y) != background)
                    return true;
        return false;
    }
}
