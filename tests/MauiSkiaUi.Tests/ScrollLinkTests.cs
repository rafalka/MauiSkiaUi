using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using Microsoft.Maui.Layouts;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.16 A1 / A2: scroll-linked channels (<see cref="SkUiRenderLink"/>): evaluation, how channels combine with the node's own
/// values, and the compositor drawing a node from its source's current offset (a sibling of the scroller, through a
/// render-thread fling) without re-recording.
/// </summary>
public class ScrollLinkTests
{
    private const float Tolerance = 1e-4f;

    /// <summary>A box whose render props carry the given channels on its scroller, evaluated at the scroller's UI offset.</summary>
    private sealed class LinkedBox(SkUiScrollView scroll, Func<SkUiRenderNode, SkUiRenderLink[]> build) : SkUiCoreBox
    {
        private SkUiRenderLink[]? _links;

        internal override void OnGetRenderProps(ref SkUiRenderProps props)
        {
            base.OnGetRenderProps(ref props);
            var links = _links ??= build(((ISkUiRenderable)scroll).RenderState.Node);
            props.Links = links;
            var input = ((ISkUiScrollHost)scroll).Scroller.LinkInput;
            props.LinkValues = SkUiRenderLinkValues.Evaluate(links, input.X, input.Y);
        }
    }

    private static SkUiRenderNode Node() => new();

    private static SkUiRenderProps Props(object node)
    {
        var props = SkUiRenderProps.Default;
        ((ISkUiRenderable)node).GetRenderProps(ref props);
        return props;
    }

    /// <summary>A 100 × 100 scroller over 4,000 DIPs of content, and a red 20 × 20 box at the top of a column beside it, linked to it.</summary>
    private static (SkUiScrollView Scroll, LinkedBox Box, SkUiGrid Root) Page(Func<SkUiRenderNode, SkUiRenderLink[]> build,
        double width = 20, double height = 20, double column = 40)
    {
        var scroll = new SkUiScrollView { Content = new SkUiBox { Color = Colors.White, HeightRequest = 4000 } };
        var box = new LinkedBox(scroll, build);
        box.SetColor(Colors.Red).SetWidth(width).SetHeight(height).SetHorizontalAlignment(LayoutAlignment.Start).SetVerticalAlignment(LayoutAlignment.Start);
        var root = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(column), new ColumnDefinition(100)] };
        // The box's host comes first: it is drawn before the scroller that drives it.
        root.Children.Add(new SkUiCoreHost().SetContent(box));
        Grid.SetColumn(scroll, 1);
        root.Children.Add(scroll);
        return (scroll, box, root);
    }

    private static int TopOfRed(SKBitmap bitmap) => Enumerable.Range(0, 100).First(y => bitmap.GetPixel(10, y).Red > 200 && bitmap.GetPixel(10, y).Green < 50);

    [Fact]
    public void AChannelMapsItsInputRangeOntoItsOutputAndHoldsTheEndsOutside()
    {
        var link = new SkUiRenderLink(Node(), horizontal: false, SkUiRenderLinkTarget.TranslationY, 100, 300, 0, -50);
        Assert.Equal(0, link.Evaluate(-20), Tolerance);
        Assert.Equal(0, link.Evaluate(100), Tolerance);
        Assert.Equal(-25, link.Evaluate(200), Tolerance);
        Assert.Equal(-50, link.Evaluate(300), Tolerance);
        Assert.Equal(-50, link.Evaluate(5000), Tolerance);
    }

    [Fact]
    public void EqualInputEndsMakeAStep()
    {
        var link = new SkUiRenderLink(Node(), horizontal: true, SkUiRenderLinkTarget.Opacity, 200, 200, 1, 0);
        Assert.Equal(1, link.Evaluate(199.9f), Tolerance);
        Assert.Equal(0, link.Evaluate(200), Tolerance);
    }

    [Fact]
    public void ACurveShapesTheProgress()
    {
        // An ease-in sampled at 0, 0.25, 1 (three points): halfway in is the middle sample.
        var link = new SkUiRenderLink(Node(), horizontal: false, SkUiRenderLinkTarget.ScaleX, 0, 100, 1, 2, [0, 0.25f, 1]);
        Assert.Equal(1.25f, link.Evaluate(50), Tolerance);
        Assert.Equal(1.125f, link.Evaluate(25), Tolerance);
        Assert.Equal(2, link.Evaluate(100), Tolerance);
    }

    [Fact]
    public void ChannelsCombineTranslationsAddScalesAndOpacitiesMultiply()
    {
        var source = Node();
        SkUiRenderLink[] links =
        [
            new(source, false, SkUiRenderLinkTarget.TranslationY, 0, 100, 0, -10),
            new(source, false, SkUiRenderLinkTarget.TranslationY, 0, 100, 0, -20),
            new(source, false, SkUiRenderLinkTarget.Scale, 0, 100, 1, 2),
            new(source, false, SkUiRenderLinkTarget.ScaleY, 0, 100, 1, 0.5f),
            new(source, false, SkUiRenderLinkTarget.Opacity, 0, 100, 1, 0.5f),
            new(source, false, SkUiRenderLinkTarget.Opacity, 0, 100, 1, 0.5f)
        ];
        var values = SkUiRenderLinkValues.Evaluate(links, 0, 100);
        Assert.Equal(-30, values.TranslationY, Tolerance);
        Assert.Equal(2, values.ScaleX, Tolerance);
        Assert.Equal(1, values.ScaleY, Tolerance);
        Assert.Equal(0.25f, values.Opacity, Tolerance);
        // On the render thread, the same at the source's offset.
        source.Props = SkUiRenderProps.Default with { ChildrenOffsetY = 100 };
        Assert.Equal(values, SkUiRenderLinkValues.Evaluate(links, SkUiRenderLinkValues.Identity));
    }

    [Fact]
    public void ChannelsAddToTheNodesOwnValuesInItsMatrix()
    {
        var props = SkUiRenderProps.Default with { X = 10, Y = 20, Width = 40, Height = 40, TranslationY = 5 };
        var plain = props.Matrix;
        props.Links = [new SkUiRenderLink(Node(), false, SkUiRenderLinkTarget.TranslationY, 0, 100, 0, -30)];
        props.LinkValues = SkUiRenderLinkValues.Evaluate(props.Links, 0, 100);
        // The own translation stays; the matrix (what hit-testing and the immediate painter use) adds the channel.
        Assert.Equal(5, props.TranslationY);
        Assert.Equal(-25, props.EffectiveTranslationY, Tolerance);
        Assert.Equal(plain.TransY - 30, props.Matrix.TransY, Tolerance);
    }

    [Fact]
    public void AGoneSourceLeavesWhatTheUiSideLastComputed()
    {
        var source = Node();
        SkUiRenderLink[] links = [new(source, false, SkUiRenderLinkTarget.TranslationY, 0, 100, 0, -30)];
        var committed = SkUiRenderLinkValues.Evaluate(links, 0, 50);
        source.Disposed = true;
        Assert.Equal(committed, SkUiRenderLinkValues.Evaluate(links, committed));
    }

    [Fact]
    public void ALinkedSiblingFollowsARenderThreadFlingWithoutReRecording()
    {
        // 50 DIPs down over the first 1,000 of scroll.
        var (scroll, box, root) = Page(source => [new SkUiRenderLink(source, false, SkUiRenderLinkTarget.TranslationY, 0, 1000, 0, 50)]);
        using var surface = new SkUiTestSurface(root, 140, 100);
        Assert.Equal(0, TopOfRed(surface.Frame(0)));
        var recorded = surface.RecordedPictures;
        Assert.True(((ISkUiScrollHost)scroll).Scroller.StartFling(new Point(0, 3000)));
        surface.Frame(10);
        var first = TopOfRed(surface.Frame(40));
        var later = TopOfRed(surface.Frame(200));
        Assert.True(later > first, $"{later} > {first}");
        // Drawn where the channel puts it for the offset the render thread shows.
        var offset = ((ISkUiRenderable)scroll).RenderState.Node.Props.ChildrenOffsetY;
        Assert.InRange(later, offset / 20 - 1, offset / 20 + 1);
        Assert.Equal(recorded, surface.RecordedPictures);
        GC.KeepAlive(box);
    }

    [Fact]
    public void ALinkedOpacityFadesTheNodeAndHidesItAtZero()
    {
        var (scroll, _, root) = Page(source => [new SkUiRenderLink(source, false, SkUiRenderLinkTarget.Opacity, 0, 100, 1, 0)]);
        using var surface = new SkUiTestSurface(root, 140, 100);
        Assert.Equal(255, surface.Frame(0).GetPixel(10, 10).Red);
        scroll.ScrollTo(0, 50);
        var half = surface.Frame(10).GetPixel(10, 10);
        // Red at half opacity over the surface's transparent background.
        Assert.InRange(half.Alpha, 100, 160);
        scroll.ScrollTo(0, 100);
        Assert.Equal(0, surface.Frame(20).GetPixel(10, 10).Alpha);
    }

    [Fact]
    public void ALinkedScaleGrowsTheNodeAboutItsAnchor()
    {
        var (scroll, box, root) = Page(source => [new SkUiRenderLink(source, false, SkUiRenderLinkTarget.Scale, 0, 100, 1, 2)]);
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        scroll.ScrollTo(0, 100);
        var bitmap = surface.Frame(10);
        // 20 DIPs about its center (10, 10) doubled: from -10 to 30 (clipped by the host's column at 0).
        Assert.Equal(255, bitmap.GetPixel(28, 28).Red);
        Assert.Equal(0, bitmap.GetPixel(28, 28).Green);
        Assert.Equal(2, Props(box).Matrix.ScaleX, Tolerance);
    }

    #region Clips

    private static bool IsRed(SKColor color) => color.Alpha > 200 && color.Red > 200 && color.Green < 60;

    private static SkUiRenderLink Clip(SkUiRenderNode source, SkUiRenderLinkTarget target, float from, float to,
        SkUiRenderLinkClipEdge edge = SkUiRenderLinkClipEdge.Start, SkUiRenderLinkClipShape shape = SkUiRenderLinkClipShape.Rectangle, float radius = 0) =>
        new(source, false, target, 0, 100, from, to, clipEdge: edge, clipShape: shape, clipCornerRadius: radius);

    [Theory]
    [InlineData(0, 0f)] // Start
    [InlineData(1, 60f)] // End
    [InlineData(2, 30f)] // Center
    public void AClipKeepsTheEdgeItNames(int edge, float left)
    {
        SkUiRenderLink[] links = [Clip(Node(), SkUiRenderLinkTarget.ClipWidth, 100, 40, (SkUiRenderLinkClipEdge)edge)];
        var values = SkUiRenderLinkValues.Evaluate(links, 0, 100);
        Assert.True(SkUiRenderLinkClip.TryGet(links, values, 100, 50, out var clip));
        Assert.Equal(left, clip.Rect.Left, Tolerance);
        Assert.Equal(left + 40, clip.Rect.Right, Tolerance);
        // No channel clips the other axis: what the node draws above or below it stays.
        Assert.True(clip.Rect.Top < -1000 && clip.Rect.Bottom > 1000);
        Assert.Equal(0, clip.Radius);
    }

    [Fact]
    public void AClipLargerThanTheNodeDoesNotCutItsOverflow()
    {
        SkUiRenderLink[] links = [Clip(Node(), SkUiRenderLinkTarget.ClipHeight, 400, 400)];
        Assert.True(SkUiRenderLinkClip.TryGet(links, SkUiRenderLinkValues.Evaluate(links, 0, 0), 100, 200, out var clip));
        Assert.Equal(400, clip.Rect.Height, Tolerance); // a header growing while pulled is not cut at its 200
    }

    [Fact]
    public void AnOutlineShadowKeepsItsShapeWhereTheClipDoesNotCutIt()
    {
        using var outline = new SKPath();
        outline.AddRoundRect(new SKRect(0, 0, 100, 50), 16, 16);
        SkUiRenderLink[] links = [Clip(Node(), SkUiRenderLinkTarget.ClipHeight, 50, 20)];
        Assert.True(SkUiRenderLinkClip.TryGet(links, SkUiRenderLinkValues.Evaluate(links, 0, 0), 100, 50, out var whole));
        var own = SkUiRenderShadow.CornerRadiusOf(outline);
        Assert.Equal(16, own, Tolerance);
        Assert.False(whole.TryGetOutlineShadow(outline.Bounds, own, out _, out _, out var hidden)); // at rest: the outline's own shadow
        Assert.False(hidden);
        Assert.True(SkUiRenderLinkClip.TryGet(links, SkUiRenderLinkValues.Evaluate(links, 0, 100), 100, 50, out var cut));
        Assert.True(cut.TryGetOutlineShadow(outline.Bounds, own, out var rect, out var radius, out _));
        Assert.Equal(20, rect.Height, Tolerance);
        Assert.Equal(10, radius, Tolerance); // the card's 16, capped at half the 20 left
    }

    [Fact]
    public void ClipsOnBothAxesTakeTheSmallerAndTheFirstShape()
    {
        var source = Node();
        SkUiRenderLink[] links =
        [
            Clip(source, SkUiRenderLinkTarget.ClipHeight, 50, 30, SkUiRenderLinkClipEdge.End),
            Clip(source, SkUiRenderLinkTarget.ClipHeight, 50, 20, SkUiRenderLinkClipEdge.Start),
            Clip(source, SkUiRenderLinkTarget.ClipWidth, 100, 56, SkUiRenderLinkClipEdge.End, SkUiRenderLinkClipShape.Capsule),
            Clip(source, SkUiRenderLinkTarget.ClipWidth, 100, 80, shape: SkUiRenderLinkClipShape.RoundRectangle, radius: 4)
        ];
        var values = SkUiRenderLinkValues.Evaluate(links, 0, 100);
        Assert.True(SkUiRenderLinkClip.TryGet(links, values, 100, 50, out var clip));
        // Height 20 kept at the bottom (the first height channel's edge), width 56 at the right, capsule corners.
        Assert.Equal(new SKRect(44, 30, 100, 50), clip.Rect);
        Assert.Equal(10, clip.Radius, Tolerance);
    }

    [Fact]
    public void ARoundedClipNeverRoundsPastHalfItsShorterSide()
    {
        SkUiRenderLink[] links = [Clip(Node(), SkUiRenderLinkTarget.ClipHeight, 50, 10, shape: SkUiRenderLinkClipShape.RoundRectangle, radius: 12)];
        Assert.True(SkUiRenderLinkClip.TryGet(links, SkUiRenderLinkValues.Evaluate(links, 0, 100), 100, 50, out var clip));
        Assert.Equal(5, clip.Radius, Tolerance);
        Assert.True(SkUiRenderLinkClip.TryGet(links, SkUiRenderLinkValues.Evaluate(links, 0, 0), 100, 50, out clip));
        Assert.Equal(12, clip.Radius, Tolerance);
    }

    [Fact]
    public void NoClipChannelMeansNoClipAndAClipToZeroIsEmpty()
    {
        SkUiRenderLink[] none = [new(Node(), false, SkUiRenderLinkTarget.TranslationY, 0, 100, 0, 10)];
        Assert.False(SkUiRenderLinkClip.TryGet(none, SkUiRenderLinkValues.Evaluate(none, 0, 100), 100, 50, out _));
        SkUiRenderLink[] away = [Clip(Node(), SkUiRenderLinkTarget.ClipHeight, 50, 0)];
        Assert.True(SkUiRenderLinkClip.TryGet(away, SkUiRenderLinkValues.Evaluate(away, 0, 100), 100, 50, out var clip));
        Assert.True(clip.IsEmpty);
    }

    [Theory]
    [InlineData(0, 2, 12)] // Start
    [InlineData(1, 17, 8)] // End
    public void TheCompositorDrawsTheClippedPartOnly(int edge, int shown, int hidden)
    {
        // 20 DIPs tall, clipped to 5 over the first 100 DIPs of scroll.
        var (scroll, _, root) = Page(source => [Clip(source, SkUiRenderLinkTarget.ClipHeight, 20, 5, (SkUiRenderLinkClipEdge)edge)]);
        using var surface = new SkUiTestSurface(root, 140, 100);
        Assert.True(IsRed(surface.Frame(0).GetPixel(10, hidden)));
        var recorded = surface.RecordedPictures;
        scroll.ScrollTo(0, 100);
        var bitmap = surface.Frame(10);
        Assert.True(IsRed(bitmap.GetPixel(10, shown)));
        Assert.Equal(0, bitmap.GetPixel(10, hidden).Alpha);
        // A clip is composite-time: nothing is recorded again.
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    [Fact]
    public void ACapsuleClipRoundsItsEnds()
    {
        // 80 × 20, clipped across to 40 at its right end, capsule corners (radius 10).
        var (scroll, _, root) = Page(source => [Clip(source, SkUiRenderLinkTarget.ClipWidth, 80, 40, SkUiRenderLinkClipEdge.End, SkUiRenderLinkClipShape.Capsule)],
            width: 80, column: 90);
        using var surface = new SkUiTestSurface(root, 190, 100);
        surface.Frame(0);
        scroll.ScrollTo(0, 100);
        var bitmap = surface.Frame(10);
        Assert.Equal(0, bitmap.GetPixel(20, 10).Alpha);    // left of the clip
        Assert.True(IsRed(bitmap.GetPixel(60, 10)));       // its middle
        Assert.True(bitmap.GetPixel(41, 1).Alpha < 60);    // a rounded corner
        Assert.True(IsRed(bitmap.GetPixel(50, 1)));        // past the corner, the straight edge
    }

    [Fact]
    public void AnOutlineShadowFollowsTheClip()
    {
        var (scroll, box, root) = Page(source => [Clip(source, SkUiRenderLinkTarget.ClipHeight, 40, 20)], width: 40, height: 40);
        box.SetShadow(new SkUiCoreShadow(Colors.Black, new Point(0, 10), 0));
        using var surface = new SkUiTestSurface(root, 140, 100);
        var bitmap = surface.Frame(0);
        Assert.Equal(255, bitmap.GetPixel(20, 45).Alpha); // the whole box's shadow, below it
        scroll.ScrollTo(0, 100);
        bitmap = surface.Frame(10);
        Assert.True(IsRed(bitmap.GetPixel(20, 10)));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(20, 25)); // the clipped box's shadow, 10 below its 20
        Assert.Equal(0, bitmap.GetPixel(20, 45).Alpha);       // not the whole box's
    }

    [Fact]
    public void TheImmediatePainterDrawsLinkedClipsAndOpacityAsTheCompositorDoes()
    {
        var (scroll, box, root) = Page(source =>
        [
            Clip(source, SkUiRenderLinkTarget.ClipWidth, 60, 30, SkUiRenderLinkClipEdge.End, SkUiRenderLinkClipShape.Capsule),
            new SkUiRenderLink(source, false, SkUiRenderLinkTarget.Opacity, 0, 100, 1, 0.5f),
            new SkUiRenderLink(source, false, SkUiRenderLinkTarget.TranslationY, 0, 100, 0, 15)
        ], width: 60, column: 70);
        box.SetShadow(new SkUiCoreShadow(Colors.Black, new Point(0, 6), 2));
        using var surface = new SkUiTestSurface(root, 170, 100);
        surface.Frame(0);
        scroll.ScrollTo(0, 60);
        var composited = surface.Frame(10);
        using var painted = new SKBitmap(170, 100);
        using (var canvas = new SKCanvas(painted))
        {
            canvas.Clear(SKColors.Transparent);
            SkUiImmediatePainter.Paint(root, canvas, applyOffset: false);
        }
        var different = 0;
        for (var x = 0; x < 70; x++)
            for (var y = 0; y < 60; y++)
            {
                var a = composited.GetPixel(x, y);
                var b = painted.GetPixel(x, y);
                if (Math.Abs(a.Alpha - b.Alpha) > 2 || Math.Abs(a.Red - b.Red) > 2)
                    different++;
            }
        Assert.Equal(0, different);
    }

    #endregion

    #region Overscroll as input

    [Fact]
    public void AStretchCountsAsTheDistanceItShowsPastThePulledEdge()
    {
        // A 100 DIP viewport stretched by an 80 DIP overscroll: 1 + 0.3 · 80 / 100.
        var scale = 1 + SkUiOverscroll.StretchFactor * 80 / 100f;
        var atStart = SkUiRenderProps.Default with
        {
            Width = 100, Height = 100, ChildrenOffsetY = 0, ChildrenScaleY = scale, ChildrenClipRect = new SKRect(0, 0, 100, 100)
        };
        Assert.Equal(-80, atStart.ScrollLinkInputY, 0.01f);
        Assert.Equal(0, atStart.ScrollLinkInputX, Tolerance);
        var atEnd = atStart with { ChildrenOffsetY = 900, ChildrenScaleOrigin = new SKPoint(0, 100) };
        Assert.Equal(980, atEnd.ScrollLinkInputY, 0.01f);
        // A bounce is already in the offset.
        var bounced = SkUiRenderProps.Default with { Width = 100, Height = 100, ChildrenOffsetY = -80 };
        Assert.Equal(-80, bounced.ScrollLinkInputY, Tolerance);
    }

    [Theory]
    [InlineData(SkUiOverscrollMode.Bounce)]
    [InlineData(SkUiOverscrollMode.Stretch)]
    public void APullPastTheTopIsTheSameInputOnBothThreadsInEitherMode(SkUiOverscrollMode mode)
    {
        var scroll = new SkUiScrollView { Content = new SkUiBox { Color = Colors.White, HeightRequest = 4000 }, Overscroll = mode };
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        // Pulled down 120 DIPs from the top, held.
        scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(50, 10), TimeSpan.Zero));
        for (var step = 1; step <= 4; step++)
            scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(50, 10 + 30 * step), TimeSpan.FromMilliseconds(step * 100)));
        surface.Frame(500);
        var scroller = ((ISkUiScrollHost)scroll).Scroller;
        var shown = (float)scroller.OverscrollY;
        Assert.True(shown < -10, $"overscroll {shown}");
        Assert.Equal(shown, scroller.LinkInput.Y, Tolerance);
        var render = ((ISkUiRenderable)scroll).RenderState.Node.Props;
        Assert.Equal(shown, render.ScrollLinkInputY, 0.05f);
        // A header that grows when pulled: the same output from the render thread's input and the UI side's.
        var link = new SkUiRenderLink(Node(), false, SkUiRenderLinkTarget.Scale, -200, 0, 1.5f, 1);
        Assert.Equal(link.Evaluate(scroller.LinkInput.Y), link.Evaluate(link.Input(render)), 0.001f);
        Assert.True(link.Evaluate(link.Input(render)) > 1);
    }

    [Fact]
    public void APullThatIsNotShownDoesNotCount()
    {
        var scroll = new SkUiScrollView { Content = new SkUiBox { Color = Colors.White, HeightRequest = 4000 }, Overscroll = SkUiOverscrollMode.None };
        using var surface = new SkUiTestSurface(scroll, 100, 100);
        surface.Frame(0);
        scroll.Touch(new(1, SkUiTouchAction.Pressed, new Point(50, 10), TimeSpan.Zero));
        scroll.Touch(new(1, SkUiTouchAction.Moved, new Point(50, 90), TimeSpan.FromMilliseconds(100)));
        surface.Frame(200);
        Assert.Equal(0, ((ISkUiScrollHost)scroll).Scroller.LinkInput.Y, Tolerance);
        Assert.Equal(0, ((ISkUiRenderable)scroll).RenderState.Node.Props.ScrollLinkInputY, Tolerance);
    }

    #endregion

    #region Hit-testing and semantics

    private static bool Taps(SkUiView root, LinkedBox box, Point point)
    {
        var tapped = false;
        void OnTapped(object? sender, SkUiTappedEventArgs e) => tapped = true;
        box.Tapped += OnTapped;
        root.Touch(new(7, SkUiTouchAction.Pressed, point, TimeSpan.FromSeconds(10)));
        root.Touch(new(7, SkUiTouchAction.Released, point, TimeSpan.FromSeconds(10.05)));
        box.Tapped -= OnTapped;
        return tapped;
    }

    [Fact]
    public void APointerReachesOnlyThePartAClipShows()
    {
        var (scroll, box, root) = Page(source => [Clip(source, SkUiRenderLinkTarget.ClipHeight, 40, 20)], width: 40, height: 40);
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        Assert.True(Taps(root, box, new Point(20, 30)));
        scroll.ScrollTo(0, 100);
        surface.Frame(10);
        Assert.True(Taps(root, box, new Point(20, 10)));
        Assert.False(Taps(root, box, new Point(20, 30)));
    }

    [Fact]
    public void APointerMissesARoundedClipsCornersAndAFadedOutNode()
    {
        var (scroll, box, root) = Page(source =>
        [
            Clip(source, SkUiRenderLinkTarget.ClipWidth, 80, 40, SkUiRenderLinkClipEdge.End, SkUiRenderLinkClipShape.Capsule),
            new SkUiRenderLink(source, false, SkUiRenderLinkTarget.Opacity, 100, 200, 1, 0)
        ], width: 80, column: 90);
        using var surface = new SkUiTestSurface(root, 190, 100);
        surface.Frame(0);
        scroll.ScrollTo(0, 100);
        surface.Frame(10);
        Assert.True(Taps(root, box, new Point(60, 10)));
        Assert.False(Taps(root, box, new Point(41, 1)));  // the capsule's corner
        Assert.False(Taps(root, box, new Point(20, 10))); // clipped away
        scroll.ScrollTo(0, 200);
        surface.Frame(20);
        Assert.False(Taps(root, box, new Point(60, 10))); // faded out
    }

    [Fact]
    public void ScreenReadersSeeThePartAClipShowsAndNothingFadedOrClippedAway()
    {
        var (scroll, box, root) = Page(source =>
        [
            Clip(source, SkUiRenderLinkTarget.ClipHeight, 40, 0),
            new SkUiRenderLink(source, false, SkUiRenderLinkTarget.Opacity, 0, 50, 1, 0)
        ], width: 40, height: 40);
        box.SetSemanticDescription("Header");
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        var header = Assert.Single(SkUiSemanticsTree.Build(root).Flatten(), node => node.Label == "Header");
        Assert.Equal(40, header.Bounds.Height, 0.01);
        scroll.ScrollTo(0, 25); // clipped to 30, half faded
        surface.Frame(10);
        header = Assert.Single(SkUiSemanticsTree.Build(root).Flatten(), node => node.Label == "Header");
        Assert.Equal(30, header.Bounds.Height, 0.01);
        scroll.ScrollTo(0, 50); // faded out
        surface.Frame(20);
        Assert.DoesNotContain(SkUiSemanticsTree.Build(root).Flatten(), node => node.Label == "Header");
    }

    #endregion
}
