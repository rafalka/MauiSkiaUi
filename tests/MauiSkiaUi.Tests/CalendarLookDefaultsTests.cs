using System.Globalization;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Platform looks' calendars (1.15 B3): what marks today, the selection's shape, fill and number come from the look while the app
/// sets none (<see cref="SkUiLook.CalendarTodayStyle"/>, <see cref="SkUiLook.CalendarSelectionShape"/>,
/// <see cref="SkUiLook.CalendarSelectionColor"/>, <see cref="SkUiLook.CalendarSelectedTextColor"/>), the app's values win, and the
/// platform looks' sizes and drawing. November 2026 in en-US; the selected day is the 10th.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class CalendarLookDefaultsTests : IDisposable
{
    private const int Width = 280;
    private static readonly DateTime November = new(2026, 11, 1);
    private static readonly DateTime Tenth = new(2026, 11, 10);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly SkUiLook _look = SkUiLook.Current;
    private readonly SkUiColorScheme _scheme = SkUiColorScheme.Current;
    private readonly IDisposable _dispatcher = SkUiTestHelpers.UseTestDispatcher();

    public CalendarLookDefaultsTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose()
    {
        SkUiLook.Current = _look;
        SkUiColorScheme.Current = _scheme;
        CultureInfo.CurrentCulture = _culture;
        _dispatcher.Dispose();
    }

    private static void Use(DefaultSkUiLook look)
    {
        SkUiLook.Current = look;
        SkUiColorScheme.Current = look.GetColorScheme(false);
    }

    private static SKColor Accent => SkUiShapePainter.ToSkColor(SkUiColorScheme.Current.Accent);

    /// <summary>Draws <paramref name="root"/> once and returns the look's cell paint and the day painter's context of the 10th.</summary>
    private static (SkUiCalendarCellPaint Cell, SkUiCalendarDayContext Day, SKBitmap Bitmap) DrawTenth(SkUiView root, Action<SkUiCalendarDayPainter> setPainter,
        out SkUiTestSurface surface, int height = 420)
    {
        SkUiCalendarDayContext day = default;
        setPainter((SKCanvas _, in SkUiCalendarDayContext context) =>
        {
            if (context.Date == Tenth)
                day = context;
            return false;
        });
        var cells = new List<SkUiCalendarCellPaint>();
        var look = SkUiLook.Current;
        look.CalendarCellPainter = (canvas, cell) => cells.Add(cell);
        try
        {
            surface = new SkUiTestSurface(root, Width, height);
            surface.Frame(0);
        }
        finally
        {
            look.CalendarCellPainter = null;
        }
        var tenth = day;
        return (cells.Single(cell => cell.Bounds == tenth.NumberBounds), tenth, surface.Bitmap);
    }

    private static SkUiCalendar Calendar() => new() { DisplayDate = November, SelectedDate = Tenth };

    [Fact]
    public void TheDefaultLookKeepsTheMarksOf113()
    {
        Use(new DefaultSkUiLook());
        var calendar = Calendar();
        var (cell, day, _) = DrawTenth(calendar, painter => calendar.DayPainter = painter, out var surface);
        using var surfaceScope = surface;
        Assert.Equal(SkUiCalendarTodayStyle.Ring, cell.TodayStyle);
        Assert.Equal(SkUiCalendarSelectionShape.Circle, cell.Shape);
        Assert.Equal(Accent, cell.SelectionColor);
        Assert.Equal(SKColors.White, day.TextColor); // on the solid accent
        Assert.Equal(SkUiCalendarTodayStyle.Ring, calendar.TodayStyle);
    }

    [Fact]
    public void OnIosTodayIsTheAccentNumberAndTheSelectionALightTint()
    {
        Use(new IosSkUiLook());
        var calendar = Calendar();
        var (cell, day, bitmap) = DrawTenth(calendar, painter => calendar.DayPainter = painter, out var surface);
        using var surfaceScope = surface;
        Assert.Equal(SkUiCalendarTodayStyle.Text, cell.TodayStyle);
        Assert.Equal(Accent.WithAlpha((byte)Math.Round(Accent.Alpha * 0.14)), cell.SelectionColor);
        Assert.Equal(Accent, day.TextColor);
        Assert.Equal(SkUiCalendarTodayStyle.Ring, calendar.TodayStyle); // the getter reads the property's default while it is not set
        Assert.NotEqual(Accent, bitmap.GetPixel((int)cell.Bounds.MidX - 12, (int)cell.Bounds.MidY)); // a tint, not the solid accent

        // The app's values win; cleared, the look's again (ClearValue of an unchanged value raises no change).
        calendar.TodayStyle = SkUiCalendarTodayStyle.Ring;
        calendar.SelectionColor = Colors.DarkRed;
        (cell, day, _) = DrawTenth(calendar, painter => calendar.DayPainter = painter, out var again);
        using var againScope = again;
        Assert.Equal(SkUiCalendarTodayStyle.Ring, cell.TodayStyle);
        Assert.Equal(SkUiShapePainter.ToSkColor(Colors.DarkRed), cell.SelectionColor);
        Assert.Equal(SKColors.White, day.TextColor); // a solid selection of the app's: white or near black
        calendar.ClearValue(SkUiCalendar.TodayStyleProperty);
        calendar.ClearValue(SkUiCalendar.SelectionColorProperty);
        (cell, _, _) = DrawTenth(calendar, painter => calendar.DayPainter = painter, out var cleared);
        using var clearedScope = cleared;
        Assert.Equal(SkUiCalendarTodayStyle.Text, cell.TodayStyle);
    }

    [Fact]
    public void OnTheMacTheSelectionIsASolidRoundedSquareOnCompactRows()
    {
        Use(new MacSkUiLook());
        var calendar = new SkUiCoreCalendar().SetDisplayDate(November).SetSelectedDate(Tenth);
        var host = new SkUiCoreHost().SetContent(calendar);
        var (cell, day, _) = DrawTenth(host, painter => calendar.SetDayPainter(painter), out var surface);
        using var surfaceScope = surface;
        Assert.Equal(SkUiCalendarSelectionShape.RoundedSquare, cell.Shape);
        Assert.Equal(SkUiCalendarTodayStyle.Text, cell.TodayStyle); // as iOS
        Assert.Equal(Accent, cell.SelectionColor);
        Assert.Equal(SKColors.White, day.TextColor);
        // With a day painter, rows grow by the look's marker area (14).
        Assert.Equal(36 + 24 + 6 * (32 + 14), calendar.Measure(Width, double.PositiveInfinity).Height, 3);

        // The Core twin's set values win.
        calendar.SetSelectionShape(SkUiCalendarSelectionShape.Circle).SetTodayStyle(SkUiCalendarTodayStyle.Filled);
        (cell, _, _) = DrawTenth(host, painter => calendar.SetDayPainter(painter), out var again);
        using var againScope = again;
        Assert.Equal(SkUiCalendarSelectionShape.Circle, cell.Shape);
        Assert.Equal(SkUiCalendarTodayStyle.Filled, cell.TodayStyle);
    }

    [Fact]
    public void OnWindowsTodayIsFilledAndTheSelectionARing()
    {
        Use(new WindowsSkUiLook());
        var calendar = Calendar();
        SkUiCalendarDayContext tenth = default;
        calendar.DayPainter = (SKCanvas _, in SkUiCalendarDayContext context) =>
        {
            if (context.Date == Tenth)
                tenth = context;
            return true; // no numbers: only the look's chrome
        };
        using var surface = new SkUiTestSurface(calendar, Width, 420);
        surface.Frame(0);
        Assert.Equal(SkUiShapePainter.ToSkColor(SkUiColorScheme.Current.DefaultForeground), tenth.TextColor); // the days' own color
        var bounds = tenth.NumberBounds;
        var bitmap = surface.Bitmap;
        // A ring: the accent at the shape's edge (2 in from the cell, 1 into the 2-wide stroke), the background inside.
        Assert.Equal(Accent, bitmap.GetPixel((int)bounds.MidX, (int)(bounds.MidY - Math.Min(bounds.Width, bounds.Height) / 2 + 3)));
        Assert.NotEqual(Accent, bitmap.GetPixel((int)bounds.MidX, (int)bounds.MidY));
        Assert.Equal(44 + 32 + 6 * (40 + 14), ((IView)calendar).Measure(Width, double.PositiveInfinity).Height, 3); // with the painter's marker area
    }

    [Fact]
    public void Material3SizesTheGridAsItsDatePicker()
    {
        Use(new AndroidMaterial3SkUiLook());
        var calendar = Calendar();
        Assert.Equal(56 + 40 + 6 * 48, ((IView)calendar).Measure(Width, double.PositiveInfinity).Height, 3);
        var (cell, _, _) = DrawTenth(calendar, painter => calendar.DayPainter = painter, out var surface, height: 56 + 40 + 6 * 48);
        using var surfaceScope = surface;
        Assert.Equal(SkUiCalendarTodayStyle.Ring, cell.TodayStyle);
        Assert.Equal(Accent, cell.SelectionColor);
    }

    [Fact]
    public void ASwipeFrameOnAPlatformLookAllocatesNothing()
    {
        Use(new IosSkUiLook());
        var calendar = Calendar();
        using var surface = new SkUiTestSurface(calendar, Width, 420);
        surface.Frame(0);
        var engine = calendar.Engine;
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, Width, 420));
        engine.BeginDrag();
        for (var i = 0; i < 30; i++)
        {
            engine.Drag(-60 + i % 12 * 10, Width, 420);
            engine.Draw(canvas, Width, 420);
        }
        var fewest = long.MaxValue;
        for (var batch = 0; batch < 3 && fewest > 0; batch++)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 60; i++)
            {
                engine.Drag(-60 + i % 12 * 10, Width, 420);
                engine.Draw(canvas, Width, 420);
            }
            fewest = Math.Min(fewest, GC.GetAllocatedBytesForCurrentThread() - allocated);
        }
        engine.CancelDrag();
        recorder.EndRecording().Dispose();
        Assert.Equal(0, fewest);
    }
}
