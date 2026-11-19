using System.Globalization;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace MauiSkiaUi.Tests;

/// <summary>
/// What a frame of a moving calendar allocates once warm (1.15 A2, NFR-2). A swipe re-records the calendar's one node every frame
/// with two pages at an offset (1.13 D2), so the texts, day styles, markers and the day painter's text are kept between frames:
/// a frame draws cached text blobs and a few shapes.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class CalendarAllocationTests : IDisposable
{
    private const int Width = 280;
    private const int Height = 48 + 28 + 6 * 58;
    private static readonly DateTime November = new(2026, 11, 1);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly IDisposable _dispatcher = SkUiTestHelpers.UseTestDispatcher();
    private readonly IDisposable _font = SkUiTestHelpers.UseBundledFont();
    private readonly ITestOutputHelper _output;

    public CalendarAllocationTests(ITestOutputHelper output)
    {
        _output = output;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        _font.Dispose();
        _dispatcher.Dispose();
    }

    private static readonly SkUiCalendarDayStyle Marked = new()
    {
        Markers = [SkUiCalendarMarker.Dot(Colors.Red), SkUiCalendarMarker.Bar(Colors.Blue), SkUiCalendarMarker.Caption("Off", Colors.Green)],
    };

    private static readonly SkUiCalendarDayStyle Bold = new() { FontAttributes = FontAttributes.Bold, IsStrikethrough = true };

    private static SkUiCalendarDayStyle? Style(DateTime day) => day.Day % 3 == 0 ? Bold : Marked;

    private static bool Price(SKCanvas canvas, in SkUiCalendarDayContext day)
    {
        day.DrawText(canvas, day.Date.Day % 2 == 0 ? "$90" : "$120", day.MarkerBounds, day.TextColor, fontSize: 10);
        return false;
    }

    /// <summary>
    /// The fewest bytes per draw of three batches of 60 draws of the calendar mid-swipe into a recording canvas, after a warm-up:
    /// what recording the calendar's content costs, without the frame pipeline around it.
    /// </summary>
    private static long BytesPerSwipeDraw(SkUiCalendarModel engine)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, Width, Height));
        var frame = 0;
        engine.BeginDrag();
        void Step()
        {
            // Back and forth within the page, so both neighbors are drawn and the drag never settles.
            engine.Drag(-60 + frame % 12 * 10, Width, Height);
            engine.Draw(canvas, Width, Height);
            frame++;
        }
        for (var i = 0; i < 30; i++) Step();
        var fewest = long.MaxValue;
        for (var batch = 0; batch < 3 && fewest > 0; batch++)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 60; i++) Step();
            fewest = Math.Min(fewest, GC.GetAllocatedBytesForCurrentThread() - allocated);
        }
        engine.CancelDrag();
        recorder.EndRecording().Dispose();
        return fewest / 60;
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void ASwipeFrameAllocatesNothing(bool styled, bool painter, bool lines)
    {
        var calendar = new SkUiCalendar
        {
            DisplayDate = November, FirstDayOfWeek = DayOfWeek.Monday, FontFamily = SkUiTestHelpers.BundledFontFamily,
            AreWeekNumbersVisible = true, SelectionMode = SkUiCalendarSelectionMode.Range,
            SelectedRange = new SkUiDateRange(new DateTime(2026, 11, 10), new DateTime(2026, 11, 20)),
        };
        if (styled) calendar.DayStyle = Style;
        if (painter) calendar.DayPainter = Price;
        if (lines)
        {
            // Event lines with an overflow line: Off fits, "+N" for the rest.
            calendar.DayMarkerLayout = SkUiCalendarDayMarkerLayout.Lines;
            calendar.DayMarkerAreaHeight = 30;
        }
        using var surface = new SkUiTestSurface(calendar, Width, Height);
        surface.Frame();

        var bytes = BytesPerSwipeDraw(calendar.Engine);

        _output.WriteLine($"SkUi swipe (styled {styled}, painter {painter}, lines {lines}): {bytes} bytes per frame");
        Assert.Equal(0, bytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ACoreSwipeFrameAllocatesNothing(bool styled, bool painter)
    {
        var calendar = new SkUiCoreCalendar()
            .SetDisplayDate(November).SetFirstDayOfWeek(DayOfWeek.Monday).SetFontFamily(SkUiTestHelpers.BundledFontFamily)
            .SetAreWeekNumbersVisible(true).SetSelectionMode(SkUiCalendarSelectionMode.Range)
            .SetSelectedRange(new SkUiDateRange(new DateTime(2026, 11, 10), new DateTime(2026, 11, 20)));
        if (styled) calendar.SetDayStyle(Style);
        if (painter) calendar.SetDayPainter(Price);
        var host = new SkUiCoreHost().SetContent(calendar);
        using var surface = new SkUiTestSurface(host, Width, Height);
        surface.Frame();

        var bytes = BytesPerSwipeDraw(calendar.Engine);

        _output.WriteLine($"Core swipe (styled {styled}, painter {painter}): {bytes} bytes per frame");
        Assert.Equal(0, bytes);
    }

    /// <summary>
    /// A whole frame of a swipe through the pipeline: the calendar's draw (nothing, above) plus what a re-recorded node costs the
    /// pipeline (its new picture's wrapper and handle). Measured at 1.15; a regression in the frame pipeline shows here.
    /// </summary>
    [Fact]
    public void ASwipeFrameThroughThePipelineAllocatesOnlyItsPicture()
    {
        var calendar = new SkUiCalendar
        {
            DisplayDate = November, FirstDayOfWeek = DayOfWeek.Monday, FontFamily = SkUiTestHelpers.BundledFontFamily, DayStyle = Style,
        };
        using var surface = new SkUiTestSurface(calendar, Width, Height);
        surface.Frame();
        var engine = calendar.Engine;
        var frame = 0;
        engine.BeginDrag();
        void Step()
        {
            engine.Drag(-60 + frame % 12 * 10, Width, Height);
            surface.Frame(frame * 16.0);
            frame++;
        }
        for (var i = 0; i < 30; i++) Step();
        var recorded = surface.RecordedPictures;
        var fewest = long.MaxValue;
        for (var batch = 0; batch < 3; batch++)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 60; i++) Step();
            fewest = Math.Min(fewest, GC.GetAllocatedBytesForCurrentThread() - allocated);
        }
        engine.CancelDrag();
        var bytes = fewest / 60;
        _output.WriteLine($"swipe frame through the pipeline: {bytes} bytes, {(surface.RecordedPictures - recorded) / 180.0:0.##} pictures per frame");
        Assert.True(surface.RecordedPictures > recorded); // every frame re-records the calendar (1.13 D2)
        Assert.True(bytes <= BytesPerRecordedFrame, $"{bytes} bytes per frame");
    }

    /// <summary>What a frame that re-records one node may allocate in the pipeline (the picture's wrapper): 104 bytes at 1.15 on net10.</summary>
    private const long BytesPerRecordedFrame = 256;
}
