using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// S2 <see cref="SkUiSheetEngine"/>: detent heights from the room, the content and the bottom inset; where a released sheet comes to
/// rest by position and velocity; which detent a position is nearest; and the detent text form.
/// </summary>
public class SheetEngineTests
{
    private static SkUiSheetEngine Resolved(double available, double fit, double inset, params SkUiSheetDetent[] detents)
    {
        var engine = new SkUiSheetEngine();
        engine.Resolve(detents, available, fit, inset);
        return engine;
    }

    [Fact]
    public void DetentsResolveToHeightsCappedAtTheRoom()
    {
        var engine = Resolved(800, 300, 34,
            SkUiSheetDetent.Fraction(0.5), SkUiSheetDetent.Fraction(1), SkUiSheetDetent.Height(200), SkUiSheetDetent.FitContent, SkUiSheetDetent.Height(5000));
        Assert.Equal(400, engine.HeightOf(0));
        Assert.Equal(800, engine.HeightOf(1));
        Assert.Equal(234, engine.HeightOf(2)); // the inset is added below a height
        Assert.Equal(300, engine.HeightOf(3)); // fit includes the inset already
        Assert.Equal(800, engine.HeightOf(4)); // capped
        Assert.Equal(800, engine.MaximumHeight);
        Assert.Equal(234, engine.LowestHeight);
        Assert.Equal(5, engine.Count);
        // Out of range indexes clamp.
        Assert.Equal(400, engine.HeightOf(-3));
        Assert.Equal(800, engine.HeightOf(9));
    }

    [Fact]
    public void NoDetentsIsOneFullDetentAndResolvingReportsChanges()
    {
        var engine = new SkUiSheetEngine();
        Assert.False(engine.HasGeometry);
        Assert.True(engine.Resolve([], 600, 0, 0));
        Assert.Equal(1, engine.Count);
        Assert.Equal(600, engine.MaximumHeight);
        Assert.False(engine.Resolve([], 600, 0, 0));
        Assert.True(engine.Resolve([], 500, 0, 0));
        Assert.True(engine.HasGeometry);
    }

    [Fact]
    public void NearestDetentKeepsThePreferredOneWhenTwoResolveAlike()
    {
        var engine = Resolved(800, 400, 0, SkUiSheetDetent.Fraction(0.5), SkUiSheetDetent.FitContent, SkUiSheetDetent.Fraction(1));
        Assert.Equal(2, engine.NearestIndex(700));
        Assert.Equal(0, engine.NearestIndex(390));
        Assert.Equal(1, engine.NearestIndex(400, preferred: 1));
        Assert.Equal(0, engine.NearestIndex(400, preferred: 0));
    }

    [Fact]
    public void SlowReleasesRestOnTheNearestDetentOrClose()
    {
        var engine = Resolved(800, 0, 0, SkUiSheetDetent.Fraction(0.5), SkUiSheetDetent.Fraction(1));
        Assert.Equal(400, engine.RestingPosition(500, 0, dismissible: true));
        Assert.Equal(800, engine.RestingPosition(650, 10, dismissible: true));
        // Nearer closed than the lowest detent: closes, unless it cannot.
        Assert.Equal(0, engine.RestingPosition(150, 0, dismissible: true));
        Assert.Equal(400, engine.RestingPosition(150, 0, dismissible: false));
        Assert.Equal(400, engine.RestingPosition(250, 0, dismissible: true));
    }

    [Fact]
    public void FlingsGoTheWayTheyMoveAndMayPassDetents()
    {
        var engine = Resolved(1000, 0, 0, SkUiSheetDetent.Fraction(0.25), SkUiSheetDetent.Fraction(0.5), SkUiSheetDetent.Fraction(1));
        // Just past a detent, a gentle fling up goes to the next one, not back.
        Assert.Equal(500, engine.RestingPosition(260, 200, dismissible: true));
        // A hard fling up passes the middle detent.
        Assert.Equal(1000, engine.RestingPosition(260, 8000, dismissible: true));
        // Down from the top: the next detent below, or further with speed.
        Assert.Equal(500, engine.RestingPosition(990, -200, dismissible: true));
        // Down past the lowest: closes when it may, else stays on the lowest.
        Assert.Equal(0, engine.RestingPosition(240, -3000, dismissible: true));
        Assert.Equal(250, engine.RestingPosition(240, -3000, dismissible: false));
        // Up at the top stays on top.
        Assert.Equal(1000, engine.RestingPosition(1000, 3000, dismissible: true));
    }

    [Fact]
    public void RestingPositionsAndStepsListTheDetentsOnce()
    {
        var engine = Resolved(800, 400, 0, SkUiSheetDetent.Fraction(1), SkUiSheetDetent.Fraction(0.5), SkUiSheetDetent.FitContent);
        var positions = new List<double>();
        engine.GetRestingPositions(positions, dismissible: true);
        Assert.Equal([0, 400, 800], positions);
        positions.Clear();
        engine.GetRestingPositions(positions, dismissible: false);
        Assert.Equal([400, 800], positions);
        Assert.Equal(800, engine.Step(400, up: true));
        Assert.Null(engine.Step(800, up: true));
        Assert.Equal(400, engine.Step(800, up: false));
        Assert.Null(engine.Step(400, up: false));
    }

    [Fact]
    public void DetentsReadAndWriteTheirTextForm()
    {
        Assert.Equal([SkUiSheetDetent.FitContent, SkUiSheetDetent.Fraction(0.5), SkUiSheetDetent.Fraction(1), SkUiSheetDetent.Height(320)],
            SkUiSheetDetent.ParseList("Fit, 50%, 100%, 320"));
        Assert.Equal("Fit, 50%, 100%, 320", new SkUiSheetDetentsTypeConverter().ConvertTo(null, null,
            new List<SkUiSheetDetent> { SkUiSheetDetent.FitContent, SkUiSheetDetent.Fraction(0.5), SkUiSheetDetent.Fraction(1), SkUiSheetDetent.Height(320) },
            typeof(string)));
        Assert.Equal(SkUiSheetDetent.Fraction(0.25), new SkUiSheetDetentTypeConverter().ConvertFrom(null, null, "25%"));
        Assert.Throws<FormatException>(() => SkUiSheetDetent.ParseList("half"));
        Assert.Throws<FormatException>(() => SkUiSheetDetent.ParseList("150%"));
        Assert.Throws<FormatException>(() => SkUiSheetDetent.ParseList("-3"));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkUiSheetDetent.Fraction(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkUiSheetDetent.Fraction(1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkUiSheetDetent.Height(double.PositiveInfinity));
        Assert.Equal(SkUiSheetDetentKind.Height, SkUiSheetDetent.Height(10).Kind);
        Assert.Equal(10, SkUiSheetDetent.Height(10).Value);
    }
}
