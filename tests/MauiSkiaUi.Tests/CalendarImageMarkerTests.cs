using System.Globalization;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Image markers under a calendar's days (1.15 B4, <see cref="SkUiCalendarMarker.Image"/>): drawn once loaded, one load per source
/// however many days show it, let go when no day draws them any more and when the drawing is released.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class CalendarImageMarkerTests : IDisposable
{
    private const int Width = 280;
    private const int Height = 48 + 28 + 6 * (44 + 20);
    private static readonly DateTime November = new(2026, 11, 1);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly IDisposable _dispatcher = SkUiTestHelpers.UseTestDispatcher();

    public CalendarImageMarkerTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        _dispatcher.Dispose();
    }

    private static SkUiImageSource Source(SKColor color)
    {
        var png = ImageLoadingTests.Png(color, 24, 24);
        return SkUiImageSource.FromStream(_ => Task.FromResult<Stream>(new MemoryStream(png)), $"calendar-marker-{color}");
    }

    [Fact]
    public async Task ImageMarkersAreDrawnOnceLoadedAndLoadedOncePerSource()
    {
        var red = Source(SKColors.Red);
        var blue = Source(SKColors.Blue);
        var redMarker = new SkUiCalendarDayStyle { Markers = [SkUiCalendarMarker.Image(red)] };
        var blueMarker = new SkUiCalendarDayStyle { Markers = [SkUiCalendarMarker.Image(blue, size: 12)] };
        var showBlue = true;
        var calendar = new SkUiCalendar
        {
            DisplayDate = November, FirstDayOfWeek = DayOfWeek.Monday, DayMarkerAreaHeight = 20, AreAdjacentDaysVisible = false,
            DayStyle = day => day.Day is >= 10 and <= 12 ? redMarker : day.Day == 13 && showBlue ? blueMarker : null,
        };
        var markers = new Dictionary<DateTime, SKRect>();
        calendar.DayPainter = (SKCanvas _, in SkUiCalendarDayContext day) =>
        {
            markers[day.Date] = day.MarkerBounds;
            return false;
        };
        using var surface = new SkUiTestSurface(calendar, Width, Height);
        surface.Frame(0);
        var engine = calendar.Engine;
        Assert.Equal(2, engine.MarkerImageCount); // four days, two sources

        await engine.MarkerImagesLoading;
        TestDispatcherProvider.RunDispatched();
        surface.Frame(16);
        // Centered in the marker area, as tall as it (20) or the size asked for (12).
        foreach (var day in new[] { 10, 11, 12 })
        {
            var area = markers[new DateTime(2026, 11, day)];
            Assert.Equal(SKColors.Red, surface.Bitmap.GetPixel((int)area.MidX, (int)area.MidY));
            Assert.Equal(SKColors.Red, surface.Bitmap.GetPixel((int)area.MidX, (int)area.Top + 1));
        }
        var thirteenth = markers[new DateTime(2026, 11, 13)];
        Assert.Equal(SKColors.Blue, surface.Bitmap.GetPixel((int)thirteenth.MidX, (int)thirteenth.MidY));
        Assert.NotEqual(SKColors.Blue, surface.Bitmap.GetPixel((int)thirteenth.MidX, (int)thirteenth.Top + 2));

        // A source no day draws any more is let go at the next refresh after a frame without it; the others stay loaded.
        showBlue = false;
        calendar.RefreshDayStyles();
        surface.Frame(32);
        Assert.Equal(2, engine.MarkerImageCount);
        calendar.RefreshDayStyles();
        surface.Frame(48);
        Assert.Equal(1, engine.MarkerImageCount);
        Assert.Equal(SKColors.Red, surface.Bitmap.GetPixel((int)markers[new DateTime(2026, 11, 10)].MidX, (int)markers[new DateTime(2026, 11, 10)].MidY));

        // Released with the drawing; loaded again (from the memory cache) when drawn.
        engine.Release();
        Assert.Equal(0, engine.MarkerImageCount);
        calendar.RefreshDayStyles();
        surface.Frame(64);
        Assert.Equal(1, engine.MarkerImageCount);
    }

    [Fact]
    public async Task TheCoreCalendarDrawsImageMarkers()
    {
        var green = Source(SKColors.Lime);
        var style = new SkUiCalendarDayStyle { Markers = [SkUiCalendarMarker.Dot(Colors.Black), SkUiCalendarMarker.Image(green)] };
        SKRect area = default;
        var calendar = new SkUiCoreCalendar()
            .SetDisplayDate(November).SetFirstDayOfWeek(DayOfWeek.Monday).SetDayMarkerAreaHeight(20)
            .SetDayStyle(day => day.Day == 18 ? style : null)
            .SetDayPainter((SKCanvas _, in SkUiCalendarDayContext day) =>
            {
                if (day.Date == new DateTime(2026, 11, 18))
                    area = day.MarkerBounds;
                return false;
            });
        var host = new SkUiCoreHost().SetContent(calendar);
        using var surface = new SkUiTestSurface(host, Width, Height);
        surface.Frame(0);
        await calendar.Engine.MarkerImagesLoading;
        TestDispatcherProvider.RunDispatched();
        surface.Frame(16);
        // The dot (5) and the image (20) in one centered row, 3 apart: the image's middle is 4 right of the area's.
        Assert.Equal(SKColors.Lime, surface.Bitmap.GetPixel((int)(area.MidX + 4), (int)area.MidY));
    }

    [Fact]
    public void AnImageMarkerNeedsASourceAndASize()
    {
        Assert.Throws<ArgumentNullException>(() => SkUiCalendarMarker.Image(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkUiCalendarMarker.Image(SkUiImageSource.FromFile("a.png"), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkUiCalendarMarker.Image(SkUiImageSource.FromFile("a.png"), double.NaN));
        var marker = SkUiCalendarMarker.Image(SkUiImageSource.FromFile("a.png"), 16);
        Assert.Equal(SkUiCalendarMarkerKind.Image, marker.Kind);
        Assert.Equal(16, marker.Size);
    }
}
