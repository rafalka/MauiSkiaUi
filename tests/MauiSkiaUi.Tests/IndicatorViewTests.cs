using System.Collections.ObjectModel;
using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// C4 <see cref="SkUiIndicatorView"/> and <see cref="SkUiCoreIndicatorView"/>: MAUI's <c>IndicatorView</c> API drawn by the look
/// (sizes, a pill that takes its room along the row, a painter of the app's), taps, the position transition, a window of
/// the items, the link to a carousel (count, position, the selection following the scrolling and its wrap), the keyboard
/// and screen readers; and carousel item effects placed by the compositor.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class IndicatorViewTests
{
    private static long _pointer = 170_000;

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    /// <summary>Ticks the UI clock and renders frames until <paramref name="untilMs"/> (16 ms apart) from <paramref name="fromMs"/>.</summary>
    private static void Run(SkUiTestSurface surface, double fromMs, double untilMs)
    {
        for (var ms = fromMs; ms <= untilMs; ms += 16)
        {
            surface.Root.AnimationClock.Tick(TimeSpan.FromMilliseconds(ms));
            surface.Frame(ms);
        }
    }

    private static ObservableCollection<string> Items(int count) => [.. Enumerable.Range(0, count).Select(index => $"Item {index}")];

    #region Indicator view

    [Fact]
    public void DefaultsMatchMaui()
    {
        var indicator = new SkUiIndicatorView();
        Assert.Equal(0, indicator.Position);
        Assert.Equal(0, indicator.Count);
        Assert.Equal(6, indicator.IndicatorSize);
        Assert.Equal(IndicatorShape.Circle, indicator.IndicatorsShape);
        Assert.Equal(int.MaxValue, indicator.MaximumVisible);
        Assert.True(indicator.HideSingle);
        Assert.Equal(-1, indicator.IndicatorSpacing);
        Assert.Equal(StackOrientation.Horizontal, indicator.Orientation);
        Assert.Equal(BindingMode.TwoWay, SkUiIndicatorView.PositionProperty.DefaultBindingMode);
    }

    [Fact]
    public void TheRowIsAsLongAsItsIndicatorsAndTheirSpacing()
    {
        var indicator = new SkUiIndicatorView { Count = 5 };
        SkUiTestHelpers.Arrange(indicator, 300, 100);
        Assert.Equal(new Size(5 * 6 + 4 * 8, 6), indicator.DesiredSize);
        indicator.IndicatorSpacing = 4;
        indicator.IndicatorSize = 10;
        indicator.MaximumVisible = 3;
        SkUiTestHelpers.Arrange(indicator, 300, 100);
        Assert.Equal(new Size(3 * 10 + 2 * 4, 10), indicator.DesiredSize);
        indicator.Orientation = StackOrientation.Vertical;
        SkUiTestHelpers.Arrange(indicator, 300, 300);
        Assert.Equal(new Size(10, 38), indicator.DesiredSize);
        Assert.Equal(Size.Zero, new SkUiCoreIndicatorView().Measure(100, 100));
    }

    [Fact]
    public void APillTakesItsRoomAlongTheRowAndTheLookDrawsWhatIsHit()
    {
        var look = DefaultSkUiLook.Instance;
        var previous = look.IndicatorStyle;
        try
        {
            look.IndicatorStyle = SkUiIndicatorStyle.Pill;
            var indicator = new SkUiIndicatorView { Count = 4, Position = 1, HeightRequest = 20 };
            using var surface = new SkUiTestSurface(indicator, 200, 20);
            // 4 × 6 + 3 × 8 + a pill 12 longer.
            Assert.Equal(new Size(60, 20), ((IView)indicator).Measure(200, 20));
            SkUiIndicatorPaint? drawn = null;
            look.IndicatorPainter = (_, paint) => drawn = paint;
            surface.Frame(0);
            var paint = Assert.NotNull(drawn);
            Assert.Equal(new SKRect(70, 7, 130, 13), paint.Bounds);
            Assert.Equal(new SKRect(70, 7, 76, 13), paint.GetIndicatorBounds(0));
            Assert.Equal(new SKRect(84, 7, 102, 13), paint.GetIndicatorBounds(1));
            Assert.Equal(1, paint.GetSelection(1));
            Assert.Equal(paint.SelectedColor, paint.GetColor(1));
            Assert.Equal(paint.Color, paint.GetColor(2));

            // A tap selects the nearest indicator; the selection moves there with the look's transition.
            Tap(indicator, new Point(126, 10));
            Assert.Equal(3, indicator.Position);
            Run(surface, 16, 80);
            Assert.InRange(indicator.DisplayedPosition, 1.01, 2.99);
            Run(surface, 96, 600);
            Assert.Equal(3, indicator.DisplayedPosition);
        }
        finally
        {
            look.IndicatorStyle = previous;
            look.IndicatorPainter = null;
        }
    }

    [Fact]
    public void ManyItemsShowAWindowAroundTheSelectedOne()
    {
        SkUiIndicatorPaint? drawn = null;
        var look = SkUiLook.Current;
        look.IndicatorPainter = (_, paint) => drawn = paint;
        try
        {
            var indicator = new SkUiIndicatorView { Count = 10, MaximumVisible = 5, Position = 8 };
            using var surface = new SkUiTestSurface(indicator, 200, 20);
            surface.Frame(0);
            Assert.Equal((5, 5), (drawn!.Value.Count, drawn.Value.First));
            Assert.Equal(1, drawn.Value.GetSelection(3));

            // A single item: nothing is drawn, and taps go through.
            drawn = null;
            indicator.Count = 1;
            surface.Frame(16);
            Assert.Null(drawn);
        }
        finally
        {
            look.IndicatorPainter = null;
        }
    }

    [Fact]
    public void ItemsSourceCountsTheItems()
    {
        var items = Items(3);
        var indicator = new SkUiIndicatorView { ItemsSource = items };
        Assert.Equal(3, indicator.Count);
        items.Add("More");
        Assert.Equal(4, indicator.Count);
        indicator.ItemsSource = new[] { 1, 2 };
        Assert.Equal(2, indicator.Count);
    }

    [Fact]
    public void ScreenReadersAndTheKeyboardAdjustThePosition()
    {
        var indicator = new SkUiIndicatorView { Count = 3 };
        var root = new SkUiVerticalStackLayout { Children = { indicator } };
        SkUiTestHelpers.Arrange(root, 300, 100);
        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)indicator).GetSemantics(info);
        Assert.Equal(SkUiSemanticsRole.Slider, info.Role);
        Assert.Equal(new SkUiSemanticsRange(0, 2, 0), info.Range);
        Assert.True(((ISkUiAccessibleNode)indicator).PerformSemanticsAction(SkUiSemanticsActions.Increment));
        Assert.Equal(1, indicator.Position);

        indicator.Focus();
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Right));
        Assert.Equal(2, indicator.Position);
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Home));
        Assert.Equal(0, indicator.Position);

        var core = new SkUiCoreIndicatorView().SetCount(3);
        Assert.True(((ISkUiAccessibleNode)core).PerformSemanticsAction(SkUiSemanticsActions.Increment));
        Assert.Equal(1, core.Position);
    }

    [Fact]
    public void ACoreIndicatorSelectsTheTappedIndicator()
    {
        var core = new SkUiCoreIndicatorView().SetCount(3).SetIndicatorSize(10).SetIndicatorSpacing(10);
        var host = new SkUiCoreHost().SetContent(core);
        using var surface = new SkUiTestSurface(host, 100, 10);
        surface.Frame(0);
        // Indicators at 25, 45 and 65 (centered in 100).
        Tap(host, new Point(70, 5));
        Assert.Equal(2, core.Position);
    }

    #endregion

    #region Carousel link

    [Fact]
    public void ALinkedIndicatorFollowsTheCarouselAndATapScrollsIt()
    {
        var carousel = new SkUiCarouselView { ItemsSource = Items(4), Loop = false, HeightRequest = 180 };
        var indicator = new SkUiIndicatorView { HeightRequest = 20 };
        carousel.IndicatorView = indicator;
        var column = new SkUiVerticalStackLayout { Children = { carousel, indicator } };
        using var surface = new SkUiTestSurface(column, 300, 200);
        surface.Frame(0);
        Assert.Equal(4, indicator.Count);
        Assert.Same(carousel, indicator.Carousel);

        // Past half an item in a drag, the selection is that far too.
        var id = ++_pointer;
        column.Touch(new(id, SkUiTouchAction.Pressed, new Point(250, 90), TimeSpan.FromMilliseconds(0)));
        column.Touch(new(id, SkUiTouchAction.Moved, new Point(200, 90), TimeSpan.FromMilliseconds(100)));
        column.Touch(new(id, SkUiTouchAction.Moved, new Point(90, 90), TimeSpan.FromMilliseconds(200)));
        Assert.Equal(160 / 300d, indicator.DisplayedPosition, 3);
        column.Touch(new(id, SkUiTouchAction.Released, new Point(90, 90), TimeSpan.FromMilliseconds(500)));
        Run(surface, 500, 2500);
        Assert.Equal(1, carousel.Position);
        Assert.Equal(1, indicator.Position);
        Assert.Equal(1, indicator.DisplayedPosition);

        // A tap on the last indicator scrolls the carousel there.
        var last = indicator.Bounds.Center.X + (6 + 8) * 1.5;
        Tap(column, new Point(last, 190));
        Assert.Equal(3, carousel.Position);
        Run(surface, 2600, 3600);
        Assert.Equal(900, carousel.ScrollView.ScrollX);

        carousel.IndicatorView = null;
        Assert.Null(indicator.Carousel);
    }

    [Fact]
    public void TheSelectionOfALoopingCarouselsIndicatorWrapsFromTheLastToTheFirst()
    {
        SkUiIndicatorPaint? drawn = null;
        SkUiLook.Current.IndicatorPainter = (_, paint) => drawn = paint;
        try
        {
            var carousel = new SkUiCarouselView { ItemsSource = Items(3), HeightRequest = 180, Position = 2 };
            var indicator = new SkUiIndicatorView { HeightRequest = 20 };
            carousel.IndicatorView = indicator;
            var column = new SkUiVerticalStackLayout { Children = { carousel, indicator } };
            using var surface = new SkUiTestSurface(column, 300, 200);
            surface.Frame(0);
            var id = ++_pointer;
            column.Touch(new(id, SkUiTouchAction.Pressed, new Point(250, 90), TimeSpan.FromMilliseconds(0)));
            column.Touch(new(id, SkUiTouchAction.Moved, new Point(200, 90), TimeSpan.FromMilliseconds(100)));
            column.Touch(new(id, SkUiTouchAction.Moved, new Point(100, 90), TimeSpan.FromMilliseconds(200)));
            surface.Frame(250);
            var paint = drawn!.Value;
            Assert.True(paint.Wraps);
            Assert.Equal(2.5f, paint.Position, 2);
            Assert.Equal(0.5f, paint.GetSelection(2), 2);
            Assert.Equal(0.5f, paint.GetSelection(0), 2);
            Assert.Equal(0, paint.GetSelection(1), 2);
        }
        finally
        {
            SkUiLook.Current.IndicatorPainter = null;
        }
    }

    #endregion

    #region Keyboard

    [Fact]
    public void ArrowKeysStepTheCarouselItemByItem()
    {
        var button = new SkUiButton { Text = "Open" };
        var carousel = new SkUiCarouselView
        {
            ItemsSource = Items(3),
            HeightRequest = 200,
            ItemTemplate = new DataTemplate(() => new SkUiButton { Text = "Card" })
        };
        var root = new SkUiVerticalStackLayout { Children = { carousel, button } };
        using var surface = new SkUiTestSurface(root, 300, 300);
        surface.Frame(0);
        ((SkUiView)carousel.GetRealizedView(0)!).Focus();
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Right));
        Assert.Equal(1, carousel.Position);
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Left));
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Left));
        Assert.Equal(2, carousel.Position); // looped
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Home));
        Assert.Equal(0, carousel.Position);

        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)carousel.ScrollView).GetSemantics(info);
        Assert.Equal(SkUiSemanticsActions.ScrollForward | SkUiSemanticsActions.ScrollBackward, info.Actions & (SkUiSemanticsActions.ScrollForward | SkUiSemanticsActions.ScrollBackward));
        carousel.Loop = false;
        info.Reset();
        ((ISkUiAccessibleNode)carousel.ScrollView).GetSemantics(info);
        Assert.Equal(SkUiSemanticsActions.ScrollForward, info.Actions & (SkUiSemanticsActions.ScrollForward | SkUiSemanticsActions.ScrollBackward));
    }

    #endregion

    #region Effects

    private static SkUiCarouselView EffectCarousel(SkUiCarouselEffect effect, out List<int> taps)
    {
        var tapped = new List<int>();
        taps = tapped;
        return new SkUiCarouselView
        {
            Loop = false,
            ItemExtent = 200,
            ItemsSource = Enumerable.Range(0, 7).ToList(),
            Position = 3,
            IsScrollAnimated = false,
            ItemEffect = effect,
            ItemTemplate = new DataTemplate(() =>
            {
                var box = new SkUiBox { Color = Colors.Red };
                box.Tapped += (sender, _) => tapped.Add((int)((SkUiBox)sender!).BindingContext);
                return box;
            })
        };
    }

    private static SkUiRenderProps PropsOf(SkUiCarouselView carousel, int index)
    {
        var props = SkUiRenderProps.Default;
        ((ISkUiRenderable)((SkUiView)carousel.GetRealizedView(index)!).Parent!).GetRenderProps(ref props);
        return props;
    }

    [Fact]
    public void CoverFlowTurnsTheSideItemsAndStacksThemNearestOnTop()
    {
        var carousel = EffectCarousel(new SkUiCoverFlowEffect(), out var taps);
        using var surface = new SkUiTestSurface(carousel, 400, 200);
        surface.Frame(0);
        var center = PropsOf(carousel, 3);
        Assert.Equal((0f, 1f, 0f), (center.RotationY, center.ScaleX, center.TranslationX));
        var next = PropsOf(carousel, 4);
        Assert.Equal(50, next.RotationY);
        Assert.Equal(0.8f, next.ScaleX, 3);
        // Its center 0.6 item lengths from the current item's: 120 instead of 200.
        Assert.Equal(-80, next.TranslationX, 3);
        var previous = PropsOf(carousel, 2);
        Assert.Equal(-50, previous.RotationY);
        // Further items: 0.18 item lengths apart, over three items.
        Assert.Equal(-(400 - 120 - 36), PropsOf(carousel, 5).TranslationX, 3);
        Assert.NotNull(next.ItemEffect);

        // The current item is drawn (and hit) over its neighbors where they overlap.
        Tap(carousel, new Point(285, 100));
        Assert.Equal([3], taps);
        Tap(carousel, new Point(318, 100));
        Assert.Equal([3, 4], taps);
    }

    [Fact]
    public void EffectsFollowTheScrollingOnTheRenderThreadWithoutRecording()
    {
        var carousel = EffectCarousel(new SkUiScaleEffect(), out _);
        carousel.SnapPointsType = SnapPointsType.None;
        using var surface = new SkUiTestSurface(carousel, 400, 200);
        surface.Frame(0);
        surface.Frame(16);
        var recorded = surface.RecordedPictures;
        var scroller = ((ISkUiScrollHost)carousel.ScrollView).Scroller;
        Assert.True(scroller.StartFling(new Point(400, 0)));
        Run(surface, 32, 600);
        Assert.Equal(recorded, surface.RecordedPictures);

        // The compositor's placement is the UI side's for the offset the render thread shows.
        var cell = (ISkUiRenderable)((SkUiView)carousel.GetRealizedView(4)!).Parent!;
        var link = cell.RenderState.Node.Props.ItemEffect!;
        var source = ((ISkUiRenderable)carousel.ScrollView).RenderState.Node.Props;
        var rendered = cell.RenderState.Node.Props;
        var opacity = link.Apply(ref rendered, source.ChildrenOffsetX, source.ChildrenOffsetY);
        var ui = PropsOf(carousel, 4);
        Assert.Equal(ui.ScaleX, rendered.ScaleX, 3);
        Assert.Equal(ui.TranslationX, rendered.TranslationX, 2);
        Assert.InRange(opacity, 0.6, 1);
    }

    [Fact]
    public void EffectSettingsAreValidatedAndRedrawTheCarousel()
    {
        var effect = new SkUiCoverFlowEffect();
        Assert.Throws<ArgumentOutOfRangeException>(() => effect.RotationAngle = 95);
        Assert.Throws<ArgumentOutOfRangeException>(() => effect.SideItemScale = double.NaN);
        var carousel = EffectCarousel(effect, out _);
        using var surface = new SkUiTestSurface(carousel, 400, 200);
        surface.Frame(0);
        effect.RotationAngle = 30;
        Assert.True(surface.NeedsFrame || surface.RenderRequests > 0);
        surface.Frame(16);
        Assert.Equal(30, PropsOf(carousel, 4).RotationY);
    }

    [Fact]
    public void TiltIsAPerspectiveThatShrinksTheFarEdge()
    {
        var props = SkUiRenderProps.Default with { Width = 100, Height = 100, RotationY = 45, CameraDistance = 300 };
        var matrix = props.Matrix;
        var near = matrix.MapPoint(new SKPoint(0, 0));
        var far = matrix.MapPoint(new SKPoint(100, 0));
        var farBottom = matrix.MapPoint(new SKPoint(100, 100));
        var nearBottom = matrix.MapPoint(new SKPoint(0, 100));
        Assert.True(farBottom.Y - far.Y < nearBottom.Y - near.Y);
        Assert.True(matrix.TryInvert(out var inverse));
        Assert.Equal(75, inverse.MapPoint(matrix.MapPoint(new SKPoint(75, 20))).X, 2);
    }

    #endregion
}
