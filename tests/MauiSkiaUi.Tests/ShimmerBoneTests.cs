using System.ComponentModel;
using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Bones (<see cref="SkUiShimmerBone"/> / <see cref="SkUiCoreShimmerBone"/>) in a shimmer's <see cref="SkUiShimmerMode.Bones"/>:
/// only they animate, the rest of the skeleton draws as it is, with no layer; one wave crosses every bone in the shimmer's
/// coordinates; they follow the shimmer's settings; a skeleton without content can hide itself after its fade-out.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class ShimmerBoneTests
{
    private static readonly SKColor Gray = new(0x80, 0x80, 0x80);
    private static readonly Color GrayColor = Color.FromRgb(0x80, 0x80, 0x80);

    /// <summary>A green 200 × 20 card with two bones on it: x 10–60 and 140–190, y 5–15.</summary>
    private static SkUiCoreAbsoluteLayout Card(out SkUiCoreShimmerBone left, out SkUiCoreShimmerBone right)
    {
        var card = new SkUiCoreAbsoluteLayout();
        card.Add(new SkUiCoreBox { Color = Colors.Green }, new Rect(0, 0, 200, 20));
        card.Add(left = new SkUiCoreShimmerBone(), new Rect(10, 5, 50, 10));
        card.Add(right = new SkUiCoreShimmerBone(), new Rect(140, 5, 50, 10));
        return card;
    }

    private static SkUiShimmer Shimmer(SkUiCoreNode skeleton, SkUiShimmerEffect effect = SkUiShimmerEffect.Wave) => new()
    {
        SkeletonContent = new SkUiCoreHost().SetContent(skeleton),
        Mode = SkUiShimmerMode.Bones,
        Effect = effect,
        BoneColor = GrayColor,
        HighlightColor = Colors.White,
        WaveWidth = 40,
        WaveDirection = SkUiShimmerWaveDirection.LeftToRight
    };

    private static SkUiTestSurface Surface(SkUiView content) =>
        new(new SkUiContentView { Content = content, Background = Colors.White }, 200, 20);

    private static SKBitmap RenderAt(SkUiTestSurface surface, double seconds)
    {
        using var canvas = new SKCanvas(surface.Bitmap);
        surface.Renderer.Render(canvas, surface.Bitmap.Info, TimeSpan.FromSeconds(seconds));
        return surface.Bitmap;
    }

    private static int Brightest(SKBitmap bitmap, int y, int from, int to)
    {
        var best = from;
        for (var x = from; x < to; x++)
            if (bitmap.GetPixel(x, y).Red > bitmap.GetPixel(best, y).Red)
                best = x;
        return best;
    }

    private static void AssertNear(SKColor expected, SKColor actual, int tolerance = 3) =>
        Assert.True(Math.Abs(expected.Red - actual.Red) <= tolerance && Math.Abs(expected.Green - actual.Green) <= tolerance
            && Math.Abs(expected.Blue - actual.Blue) <= tolerance, $"expected {expected}, got {actual}");

    [Fact]
    public void OnlyTheBonesShimmerAndOneWaveCrossesThemAll()
    {
        var shimmer = Shimmer(Card(out var left, out _));
        using var surface = Surface(shimmer);
        surface.Frame();
        Assert.True(surface.NeedsFrame);
        var props = shimmer.RenderState.Node.Props;
        Assert.False(props.ChildrenLayer); // no layer: the bones draw themselves
        Assert.Null(props.Drawable);
        Assert.True(props.DrawableSpace);
        Assert.True(Assert.IsType<SkUiShimmerBoneDrawable>(left.RenderState.Node.Props.Drawable).IsAnimated);
        var recorded = surface.RecordedPictures;

        // A linear pass of 1 s over the 200 DIP shimmer: the band's middle at 100 + (-120 + 240 · progress).
        var bitmap = RenderAt(surface, 10.25);
        Assert.Equal(SKColors.Green, bitmap.GetPixel(100, 2)); // the card draws as it is
        Assert.Equal(SKColors.Green, bitmap.GetPixel(100, 10)); // the band is not drawn between the bones
        Assert.InRange(Brightest(bitmap, 10, 10, 60), 37, 43);
        AssertNear(Gray, bitmap.GetPixel(160, 10));
        bitmap = RenderAt(surface, 10.75);
        Assert.InRange(Brightest(bitmap, 10, 140, 190), 157, 163);
        AssertNear(SKColors.White, bitmap.GetPixel(160, 10), 6);
        AssertNear(Gray, bitmap.GetPixel(40, 10));
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    [Fact]
    public void CoreBonesInACoreShimmerDrawTheSamePixels()
    {
        using var skUi = Surface(Shimmer(Card(out _, out _)));
        var core = new SkUiCoreShimmer
        {
            SkeletonContent = Card(out _, out _), Mode = SkUiShimmerMode.Bones, BoneColor = GrayColor, HighlightColor = Colors.White,
            WaveWidth = 40, WaveDirection = SkUiShimmerWaveDirection.LeftToRight
        };
        using var coreSurface = Surface(new SkUiCoreHost().SetContent(core));
        skUi.Frame();
        coreSurface.Frame();
        foreach (var seconds in new[] { 10.25, 10.6 })
        {
            using var expected = RenderAt(skUi, seconds).Copy();
            var actual = RenderAt(coreSurface, seconds);
            for (var x = 0; x < 200; x += 3)
                for (var y = 0; y < 20; y += 2)
                    Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
        }
    }

    [Fact]
    public void TheOneDrawPathAndTheClippedBandDrawTheSameWave()
    {
        // One radius: the bone color and the band in one shader; corners of different radii: the band clipped to the shape.
        SkUiShimmer Tilted(CornerRadius radii)
        {
            var card = new SkUiCoreAbsoluteLayout();
            card.Add(new SkUiCoreShimmerBone { CornerRadius = radii }, new Rect(0, 0, 200, 20));
            var shimmer = Shimmer(card);
            shimmer.WaveDirection = SkUiShimmerWaveDirection.Default; // tilted: the band is turned against the bone
            return shimmer;
        }
        using var single = Surface(Tilted(new CornerRadius(6)));
        using var several = Surface(Tilted(new CornerRadius(6, 6, 6, 7)));
        single.Frame();
        several.Frame();
        foreach (var seconds in new[] { 10.3, 10.5, 10.7 })
        {
            using var expected = RenderAt(single, seconds).Copy();
            var actual = RenderAt(several, seconds);
            Assert.InRange(Brightest(expected, 10, 0, 200), 5, 195); // the band is on the bone
            for (var x = 8; x < 192; x += 4)
                for (var y = 4; y < 16; y += 3)
                    AssertNear(expected.GetPixel(x, y), actual.GetPixel(x, y), 2);
        }
    }

    [Fact]
    public void BonesPulseAndShiftColorWithTheirShimmer()
    {
        var card = new SkUiCoreAbsoluteLayout();
        var bone = new SkUiCoreShimmerBone { CornerRadius = 4 };
        card.Add(bone, new Rect(0, 0, 200, 20));
        var shimmer = Shimmer(card, SkUiShimmerEffect.Pulse);
        shimmer.BoneColor = Colors.Black;
        shimmer.PulseOpacity = 0.3;
        shimmer.Duration = 750;
        using var surface = Surface(shimmer);
        surface.Frame();
        foreach (var t in new[] { 0, 0.375, 0.75, 1.1 })
        {
            var opacity = 0.3 + 0.7 * (0.5 - 0.5 * Math.Cos(2 * Math.PI * t / 1.5));
            var expected = (byte)Math.Round(255 * (1 - opacity));
            AssertNear(new SKColor(expected, expected, expected), RenderAt(surface, 30 + t).GetPixel(100, 10));
        }

        shimmer.Effect = SkUiShimmerEffect.Color;
        shimmer.RepeatMode = SkUiShimmerRepeatMode.Restart;
        shimmer.Easing = Easing.Linear;
        surface.Frame();
        AssertNear(SKColors.Black, RenderAt(surface, 30).GetPixel(100, 10));
        AssertNear(new SKColor(128, 128, 128), RenderAt(surface, 30.375).GetPixel(100, 10));

        // Its own color wins over the shimmer's.
        bone.Color = Colors.Red;
        shimmer.Effect = SkUiShimmerEffect.None;
        Assert.Equal(SKColors.Red, surface.Frame().GetPixel(100, 10));
    }

    [Fact]
    public void ChangesOfTheShimmerReachItsBones()
    {
        var shimmer = Shimmer(Card(out var left, out _));
        using var surface = Surface(shimmer);
        surface.Frame();
        var first = left.RenderState.Node.Props.Drawable;
        surface.Frame(16);
        Assert.Same(first, left.RenderState.Node.Props.Drawable);

        shimmer.Duration = 500;
        surface.Frame(32);
        Assert.NotSame(first, left.RenderState.Node.Props.Drawable);

        // In a silhouette the bones stand still and the shimmer draws the effect over everything.
        shimmer.Mode = SkUiShimmerMode.Silhouette;
        surface.Frame(48);
        Assert.False(left.RenderState.Node.Props.Drawable!.IsAnimated);
        Assert.IsType<SkUiShimmerSilhouette>(shimmer.RenderState.Node.Props.Drawable);
        AssertNear(Gray, RenderAt(surface, 10.125).GetPixel(100, 2)); // the card is a bone too now (the band is at 40)
    }

    [Fact]
    public void ABoneOutsideAShimmerStandsStillInTheLooksColor()
    {
        var bone = new SkUiShimmerBone();
        using var surface = Surface(bone);
        var color = SkUiShapePainter.ToSkColor(SkUiLook.Current.GetShimmerColors().Bone);
        Assert.Equal(color, surface.Frame().GetPixel(100, 10));
        Assert.False(surface.NeedsFrame);
        Assert.False(bone.RenderState.Node.Props.Drawable!.IsAnimated);
    }

    [Fact]
    public void ASkeletonWithoutContentCanHideItselfAfterItsFadeOut()
    {
        var shimmer = Shimmer(Card(out _, out _));
        var shown = new List<bool>();
        ((INotifyPropertyChanged)shimmer).PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiShimmer.IsShowingSkeleton))
                shown.Add(shimmer.IsShowingSkeleton);
        };
        using var surface = Surface(shimmer);
        surface.Frame();
        shimmer.IsLoading = false;
        Assert.Equal(SKColors.White, surface.Frame().GetPixel(100, 10)); // nothing shown
        Assert.False(surface.NeedsFrame);
        shimmer.IsLoading = true;
        Assert.Equal([false, true], shown);
    }
}
