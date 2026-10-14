using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The compositor's generic composite-time primitives, on plain nodes: blend modes, a children layer with its opacity,
/// clock-driven timelines (no recording per frame, frames only while drawn, every node in phase) and surface anchoring.
/// </summary>
public class RenderPrimitivesTests
{
    /// <summary>A box whose composite-time properties a test sets.</summary>
    private sealed class Box(Color color) : SkUiCoreBox
    {
        public SKBlendMode BlendMode = SKBlendMode.SrcOver;
        public SkUiRenderTimeline? Timeline;
        public bool SurfaceAnchored;

        public Box Init()
        {
            SetColor(color);
            return this;
        }

        internal override void OnGetRenderProps(ref SkUiRenderProps props)
        {
            base.OnGetRenderProps(ref props);
            props.BlendMode = BlendMode;
            props.Timeline = Timeline;
            props.SurfaceAnchored = SurfaceAnchored;
        }
    }

    /// <summary>A layout whose children layer a test sets.</summary>
    private sealed class Group : SkUiCoreAbsoluteLayout
    {
        public bool ChildrenLayer;
        public float ChildrenOpacity = 1;
        public SkUiRenderTimeline? Timeline;

        internal override void OnGetRenderProps(ref SkUiRenderProps props)
        {
            base.OnGetRenderProps(ref props);
            props.ChildrenLayer = ChildrenLayer;
            props.ChildrenOpacity = ChildrenOpacity;
            props.Timeline = Timeline;
        }
    }

    private static SkUiTestSurface Surface(SkUiCoreNode content, int width = 100, int height = 20) =>
        new(new SkUiContentView { Content = new SkUiCoreHost().SetContent(content), Background = Colors.White }, width, height);

    private static SKBitmap RenderAt(SkUiTestSurface surface, double seconds)
    {
        using var canvas = new SKCanvas(surface.Bitmap);
        surface.Renderer.Render(canvas, surface.Bitmap.Info, TimeSpan.FromSeconds(seconds));
        return surface.Bitmap;
    }

    [Fact]
    public void ATimelineMovesANodeFromTheClockWithoutRecording()
    {
        var box = new Box(Colors.Red).Init();
        box.Timeline = new SkUiRenderTimeline([SkUiRenderProperty.TranslationX], [0], [80], passSeconds: 1, reverse: false, easing: null);
        var group = new Group();
        group.Add(box, new Rect(0, 0, 20, 20));
        using var surface = Surface(group);
        surface.Frame();
        Assert.True(surface.NeedsFrame);
        var recorded = surface.RecordedPictures;
        Assert.Equal(SKColors.Red, RenderAt(surface, 10.25).GetPixel(30, 10)); // at x 20..40
        Assert.Equal(SKColors.White, surface.Bitmap.GetPixel(10, 10));
        Assert.Equal(SKColors.Red, RenderAt(surface, 10.75).GetPixel(70, 10)); // at x 60..80
        Assert.Equal(recorded, surface.RecordedPictures);

        // Reversing passes come back; the easing shapes each pass.
        box.Timeline = new SkUiRenderTimeline([SkUiRenderProperty.TranslationX], [0], [80], 1, reverse: true, Easing.Linear);
        box.InvalidatePaint();
        surface.Frame(16);
        Assert.Equal(SKColors.Red, RenderAt(surface, 11.75).GetPixel(30, 10)); // the second pass, backwards: x 20..40
    }

    [Fact]
    public void ATimelineRequestsFramesOnlyWhileItsParentIsDrawn()
    {
        var box = new Box(Colors.Red).Init();
        box.Timeline = new SkUiRenderTimeline([SkUiRenderProperty.Opacity], [0], [1], 1, true, null);
        var inner = new Group();
        inner.Add(box, new Rect(0, 0, 20, 20));
        var outer = new Group();
        outer.Add(inner, new Rect(0, 0, 20, 20));
        using var surface = Surface(outer);
        surface.Frame();
        Assert.True(surface.NeedsFrame);

        outer.SetLayoutBounds(inner, new Rect(500, 0, 20, 20)); // out of view: the parent is culled
        surface.Frame(16);
        surface.Frame(32);
        Assert.False(surface.NeedsFrame);
    }

    [Fact]
    public void BlendModesActWithinTheChildrenLayer()
    {
        // A red bone, then a blue rectangle over everything drawn source-in: blue only where the bone is.
        var bone = new Box(Colors.Red).Init();
        var tint = new Box(Colors.Blue).Init();
        tint.BlendMode = SKBlendMode.SrcIn;
        var group = new Group { ChildrenLayer = true };
        group.Add(bone, new Rect(0, 0, 50, 20));
        group.Add(tint, new Rect(0, 0, 100, 20));
        using var surface = Surface(group);
        var bitmap = surface.Frame();
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(25, 10));
        Assert.Equal(SKColors.White, bitmap.GetPixel(75, 10)); // the white background outside the layer is untouched

        // Without the layer the blend mode reaches what is under the group (the background turns blue too).
        group.ChildrenLayer = false;
        group.InvalidatePaint();
        group.SetLayoutBounds(tint, new Rect(0, 0, 100, 19));
        Assert.Equal(SKColors.Blue, surface.Frame(16).GetPixel(75, 10));
    }

    [Fact]
    public void TheChildrenLayerFadesTheGroupAndATimelineDrivesIt()
    {
        var group = new Group { ChildrenLayer = true, ChildrenOpacity = 0.5f };
        group.Add(new Box(Colors.Black).Init(), new Rect(0, 0, 100, 20));
        using var surface = Surface(group);
        Assert.InRange(surface.Frame().GetPixel(50, 10).Red, 125, 130);

        group.Timeline = new SkUiRenderTimeline([SkUiRenderProperty.ChildrenOpacity], [0], [1], 1, false, null);
        group.InvalidatePaint();
        surface.Frame(16);
        Assert.InRange(RenderAt(surface, 10.25).GetPixel(50, 10).Red, 189, 194); // 25 % black
    }

    [Fact]
    public void ASurfaceAnchoredNodeIsPlacedInSurfaceCoordinatesInsideItsParentsClip()
    {
        var anchored = new Box(Colors.Red).Init();
        anchored.SurfaceAnchored = true;
        var inner = new Group { ClipToBounds = true };
        inner.Add(anchored, new Rect(0, 0, 30, 20)); // surface x 0..30, whatever the parent's position
        var outer = new Group();
        outer.Add(inner, new Rect(20, 0, 60, 20)); // shows surface x 20..80
        using var surface = Surface(outer);
        var bitmap = surface.Frame();
        Assert.Equal(SKColors.White, bitmap.GetPixel(10, 10)); // outside the parent's clip
        Assert.Equal(SKColors.Red, bitmap.GetPixel(25, 10));
        Assert.Equal(SKColors.White, bitmap.GetPixel(40, 10)); // not at the parent's x + 30
    }

    [Fact]
    public void ATimelineDrivesOnlyPlacementAndOpacities() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SkUiRenderTimeline([SkUiRenderProperty.ChildrenOffsetY], [0], [1], 1, false, null));
}
