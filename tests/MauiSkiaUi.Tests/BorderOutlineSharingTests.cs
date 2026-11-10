using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.9 R3: borders with the same outline share its path operations (the content clip, the shadow silhouette), so a list
/// of equal shaped cards computes them once; each border commits a copy the compositor owns.
/// </summary>
[Collection(GlobalStateCollection.Name)] // the cache and its counter are process-wide
public class BorderOutlineSharingTests
{
    private const string Ticket = "M 0,8 A 8,8 0 0 0 8,0 L 192,0 A 8,8 0 0 0 200,8 L 200,26 A 10,10 0 0 0 200,46 L 200,64 " +
        "A 8,8 0 0 0 192,72 L 8,72 A 8,8 0 0 0 0,64 L 0,46 A 10,10 0 0 0 0,26 Z";

    private static SkUiCoreBorder CoreTicket(double height = 40, double thickness = 2)
    {
        var border = new SkUiCoreBorder()
            .SetStrokeShape(new SkUiCorePath(Ticket).SetAspect(SkUiCoreStretch.Fill))
            .SetStroke(Colors.Black)
            .SetStrokeThickness(thickness)
            .SetContent(new SkUiCoreBox().SetColor(Colors.Red));
        border.SetBackground(Colors.White).SetHeight(height);
        return border;
    }

    private static SKPath? ChildrenClip(ISkUiRenderable node)
    {
        var props = default(SkUiRenderProps);
        node.GetRenderProps(ref props);
        return props.ChildrenClipPath;
    }

    [Fact]
    public void EqualShapedBordersComputeTheirContentClipOnce()
    {
        SkUiStrokedOutlines.Clear();
        var borders = Enumerable.Range(0, 5).Select(_ => CoreTicket()).ToArray();
        var stack = new SkUiCoreVerticalStackLayout();
        foreach (var border in borders)
            stack.Add(border);
        using var surface = new SkUiTestSurface(new SkUiCoreHost().SetContent(stack), 200, 220);
        surface.Frame();

        Assert.Equal(1, SkUiStrokedOutlines.Computed);
        var clips = borders.Select(border => ChildrenClip(border)!).ToArray();
        Assert.Equal(clips.Length, clips.Distinct(ReferenceEqualityComparer.Instance).Count()); // a copy each: the compositor owns it
        Assert.All(clips, clip => Assert.Equal(clips[0].Bounds, clip.Bounds));
    }

    [Fact]
    public void BordersOfBothLayersShareAndAnotherSizeOrStrokeComputesItsOwn()
    {
        SkUiStrokedOutlines.Clear();
        var drawn = Enumerable.Range(0, 3).Select(_ => new SkUiBorder
        {
            HeightRequest = 40, StrokeThickness = 2, Stroke = Colors.Black,
            StrokeShape = new SkUiPath { Aspect = Stretch.Fill }.SetData(Ticket), Content = new SkUiBox { Color = Colors.Red },
        }).ToArray();
        var root = new SkUiVerticalStackLayout();
        foreach (var border in drawn)
            root.Children.Add(border);
        using var surface = new SkUiTestSurface(root, 200, 200);
        surface.Frame();
        Assert.Equal(1, SkUiStrokedOutlines.Computed);

        drawn[0].HeightRequest = 50;
        drawn[1].StrokeThickness = 3;
        SkUiTestHelpers.Arrange(root, 200, 200);
        surface.Frame();
        Assert.Equal(3, SkUiStrokedOutlines.Computed);

        drawn[0].HeightRequest = 40; // back to the first outline: found again
        SkUiTestHelpers.Arrange(root, 200, 200);
        surface.Frame();
        Assert.Equal(3, SkUiStrokedOutlines.Computed);
    }

    [Fact]
    public void ShadowSilhouettesOfEqualBordersAreSharedToo()
    {
        SkUiStrokedOutlines.Clear();
        var stack = new SkUiCoreVerticalStackLayout().SetSpacing(8);
        for (var index = 0; index < 3; index++)
        {
            var border = CoreTicket();
            border.SetShadow(new SkUiCoreShadow(Colors.Black, new Point(0, 2), 4));
            stack.Add(border);
        }
        using var surface = new SkUiTestSurface(new SkUiCoreHost().SetContent(stack), 200, 160);
        surface.Frame();

        Assert.Equal(2, SkUiStrokedOutlines.Computed); // the content clip and the silhouette, once each
    }

    [Fact]
    public void TheCacheKeepsAtMostItsCapacityAndCopiesOutliveIt()
    {
        SkUiStrokedOutlines.Clear();
        using var first = new SKPath();
        first.AddCircle(10, 10, 8);
        var kept = SkUiStrokedOutlines.Inside(first, 2);
        for (var index = 0; index < SkUiStrokedOutlines.Capacity + 4; index++)
        {
            using var outline = new SKPath();
            outline.AddCircle(10, 10, 9 + index);
            SkUiStrokedOutlines.Inside(outline, 2).Dispose();
        }
        Assert.Equal(SkUiStrokedOutlines.Capacity, SkUiStrokedOutlines.Count);

        SkUiStrokedOutlines.Clear(); // disposes the cached paths, not the copies handed out
        using (kept)
        {
            Assert.False(kept.IsEmpty);
            // Radius 8 − 1 (half the stroke).
            Assert.Equal(3, kept.Bounds.Left, 3);
            Assert.Equal(17, kept.Bounds.Right, 3);
            using var bitmap = new SKBitmap(20, 20);
            using var canvas = new SKCanvas(bitmap);
            canvas.ClipPath(kept, antialias: true);
            canvas.Clear(SKColors.Red);
            Assert.Equal(SKColors.Red, bitmap.GetPixel(10, 10));
            Assert.Equal(new SKColor(0, 0, 0, 0), bitmap.GetPixel(1, 1));
        }
    }

    [Fact]
    public void AnOutlineWithOtherPointsIsNotTakenForACachedOne()
    {
        SkUiStrokedOutlines.Clear();
        using var square = new SKPath();
        square.AddRect(new SKRect(0, 0, 20, 20));
        using var wide = new SKPath();
        wide.AddRect(new SKRect(0, 0, 30, 20));
        using var a = SkUiStrokedOutlines.Inside(square, 2);
        using var b = SkUiStrokedOutlines.Inside(wide, 2);
        using var c = SkUiStrokedOutlines.Grown(square, 2);
        Assert.Equal(3, SkUiStrokedOutlines.Computed);
        Assert.NotEqual(a.Bounds, b.Bounds);
        Assert.True(c.Bounds.Width > a.Bounds.Width);
    }
}
