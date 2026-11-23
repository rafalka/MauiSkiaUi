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
            var scroller = ((ISkUiScrollHost)scroll).Scroller;
            props.LinkValues = SkUiRenderLinkValues.Evaluate(links, (float)scroller.ChildrenOffset.X, (float)scroller.ChildrenOffset.Y);
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
    private static (SkUiScrollView Scroll, LinkedBox Box, SkUiGrid Root) Page(Func<SkUiRenderNode, SkUiRenderLink[]> build)
    {
        var scroll = new SkUiScrollView { Content = new SkUiBox { Color = Colors.White, HeightRequest = 4000 } };
        var box = new LinkedBox(scroll, build);
        box.SetColor(Colors.Red).SetWidth(20).SetHeight(20).SetHorizontalAlignment(LayoutAlignment.Start).SetVerticalAlignment(LayoutAlignment.Start);
        var root = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(40), new ColumnDefinition(100)] };
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
}
