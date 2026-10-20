using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <see cref="SkUiShimmer"/> / <see cref="SkUiCoreShimmer"/> (FR-31): the skeleton while loading, the content after; bones in
/// the fill; the effect on the render thread from the shared clock (nothing recorded per frame, every shimmer in phase); the
/// pulse and color animations; repeats; one "Loading" element; the switch animations.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class ShimmerTests
{
    private static readonly SKColor Gray = new(0x80, 0x80, 0x80);
    private static readonly Color GrayColor = Color.FromRgb(0x80, 0x80, 0x80);

    /// <summary>One bone filling a 200 × 20 shimmer: a red box (its color is replaced by the fill).</summary>
    private static SkUiShimmer Shimmer(SkUiShimmerEffect effect = SkUiShimmerEffect.Wave) => new()
    {
        SkeletonContent = new SkUiBox { Color = Colors.Red },
        Content = new SkUiBox { Color = Colors.Green },
        Effect = effect,
        BoneColor = GrayColor,
        HighlightColor = Colors.White,
        WaveWidth = 40,
        WaveDirection = SkUiShimmerWaveDirection.LeftToRight
    };

    private static SkUiTestSurface Surface(SkUiView content, int width = 200, int height = 20) =>
        new(new SkUiContentView { Content = content, Background = Colors.White }, width, height);

    /// <summary>Renders the committed tree at <paramref name="seconds"/> on the render thread only (no UI pass, no recording).</summary>
    private static SKBitmap RenderAt(SkUiTestSurface surface, double seconds)
    {
        using var canvas = new SKCanvas(surface.Bitmap);
        surface.Renderer.Render(canvas, surface.Bitmap.Info, TimeSpan.FromSeconds(seconds));
        return surface.Bitmap;
    }

    /// <summary>The x of the brightest pixel of row <paramref name="y"/>.</summary>
    private static int Brightest(SKBitmap bitmap, int y, int from = 0, int to = -1)
    {
        to = to < 0 ? bitmap.Width : to;
        var best = from;
        for (var x = from; x < to; x++)
            if (bitmap.GetPixel(x, y).Red > bitmap.GetPixel(best, y).Red)
                best = x;
        return best;
    }

    private static void AssertNear(SKColor expected, SKColor actual, int tolerance = 3)
    {
        Assert.True(Math.Abs(expected.Red - actual.Red) <= tolerance && Math.Abs(expected.Green - actual.Green) <= tolerance
            && Math.Abs(expected.Blue - actual.Blue) <= tolerance, $"expected {expected}, got {actual}");
    }

    [Fact]
    public void ShowsTheSkeletonWhileActiveAndTheContentAfterReleasingTheSkeleton()
    {
        var shimmer = Shimmer(SkUiShimmerEffect.None);
        var skeleton = (SkUiBox)shimmer.SkeletonContent!;
        var content = (SkUiBox)shimmer.Content!;
        Assert.True(shimmer.IsLoading);
        Assert.Same(shimmer, skeleton.Parent);
        Assert.Null(content.Parent);
        using var surface = Surface(shimmer);
        AssertNear(Gray, surface.Frame().GetPixel(100, 10)); // the bone in the fill, not red
        Assert.True(skeleton.RenderState.HasCommitted);

        shimmer.IsLoading = false;
        Assert.Equal(SKColors.Green, surface.Frame().GetPixel(100, 10));
        Assert.Null(skeleton.Parent);
        Assert.False(skeleton.RenderState.HasCommitted); // its pictures went with it
        Assert.False(shimmer.RenderState.Node.Props.ChildrenLayer); // no group, no effect drawn
        Assert.Null(shimmer.RenderState.Node.Props.Drawable);

        shimmer.IsLoading = true;
        AssertNear(Gray, surface.Frame().GetPixel(100, 10));
    }

    [Fact]
    public void ATransparentFillKeepsTheBonesOwnColors()
    {
        var shimmer = Shimmer(SkUiShimmerEffect.None);
        shimmer.BoneColor = Colors.Transparent;
        using var surface = Surface(shimmer);
        Assert.Equal(SKColors.Red, surface.Frame().GetPixel(100, 10));
    }

    [Fact]
    public void TheWaveMovesOnTheRenderThreadWithoutRecording()
    {
        using var surface = Surface(Shimmer());
        surface.Frame();
        Assert.True(surface.NeedsFrame, "a loading shimmer keeps frames coming while drawn");
        var recorded = surface.RecordedPictures;

        // Linear pass of 1 s over 200 DIPs with a 40-wide band: its middle is at -20 + 240 · progress.
        Assert.InRange(Brightest(RenderAt(surface, 10.25), 10), 37, 43);
        Assert.InRange(Brightest(RenderAt(surface, 10.5), 10), 97, 103);
        AssertNear(SKColors.White, surface.Bitmap.GetPixel(100, 10), 6);
        AssertNear(Gray, surface.Bitmap.GetPixel(10, 10));
        Assert.InRange(Brightest(RenderAt(surface, 10.75), 10), 157, 163);
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    [Theory]
    [InlineData(SkUiShimmerWaveDirection.RightToLeft, false)]
    [InlineData(SkUiShimmerWaveDirection.Default, true)]
    public void TheWaveMovesFromTheRightInRightToLeftLayoutsAndOnRequest(SkUiShimmerWaveDirection direction, bool rightToLeft)
    {
        var shimmer = Shimmer();
        shimmer.WaveDirection = direction;
        var root = new SkUiContentView { Content = shimmer, Background = Colors.White };
        if (rightToLeft)
            root.FlowDirection = FlowDirection.RightToLeft;
        using var surface = new SkUiTestSurface(root, 200, 20);
        surface.Frame();
        Assert.InRange(Brightest(RenderAt(surface, 10.25), 10), 140, 175); // a quarter of the way from the right
    }

    [Fact]
    public void TheDefaultDirectionLeansTheBandSoItsTopLeads()
    {
        var shimmer = Shimmer();
        shimmer.WaveDirection = SkUiShimmerWaveDirection.Default;
        using var surface = Surface(shimmer, 200, 60);
        surface.Frame();
        var bitmap = RenderAt(surface, 10.5);
        Assert.True(Brightest(bitmap, 5) > Brightest(bitmap, 55), "the band's top is ahead of its bottom");
    }

    [Fact]
    public void PulseMatchesAHandMadeSkeletonFade()
    {
        // A hand-made skeleton view's fade: opacity 0.3 + 0.7 · (0.5 - 0.5 · cos(2π t)) over 1.5 s: a pulse of 750 ms passes there and back.
        var shimmer = Shimmer(SkUiShimmerEffect.Pulse);
        shimmer.BoneColor = Colors.Black;
        shimmer.PulseOpacity = 0.3;
        shimmer.Duration = 750;
        using var surface = Surface(shimmer);
        surface.Frame();
        foreach (var t in new[] { 0, 0.2, 0.375, 0.75, 1.1, 1.5 })
        {
            var opacity = 0.3 + 0.7 * (0.5 - 0.5 * Math.Cos(2 * Math.PI * t / 1.5));
            var expected = (byte)Math.Round(255 * (1 - opacity));
            AssertNear(new SKColor(expected, expected, expected), RenderAt(surface, 30 + t).GetPixel(100, 10));
        }
    }

    [Fact]
    public void ColorShiftMatchesAHandMadeSkeletonColorAnimation()
    {
        // A hand-made skeleton view's color animation: StartColor to EndColor linearly in 750 ms, then again from StartColor.
        var shimmer = Shimmer(SkUiShimmerEffect.Color);
        shimmer.BoneColor = Colors.Black;
        shimmer.Duration = 750;
        shimmer.RepeatMode = SkUiShimmerRepeatMode.Restart;
        shimmer.Easing = Easing.Linear;
        using var surface = Surface(shimmer);
        surface.Frame();
        AssertNear(SKColors.Black, RenderAt(surface, 30).GetPixel(100, 10));
        AssertNear(new SKColor(128, 128, 128), RenderAt(surface, 30.375).GetPixel(100, 10));
        AssertNear(new SKColor(245, 245, 245), RenderAt(surface, 30.735).GetPixel(100, 10), 6);
        AssertNear(SKColors.Black, RenderAt(surface, 30.75).GetPixel(100, 10));

        // The effect's own repeat goes back instead of jumping.
        shimmer.RepeatMode = SkUiShimmerRepeatMode.Default;
        surface.Frame();
        AssertNear(SKColors.White, RenderAt(surface, 30.75).GetPixel(100, 10));
        AssertNear(SKColors.Black, RenderAt(surface, 31.5).GetPixel(100, 10));
    }

    [Fact]
    public void ShimmersAreInPhaseWhenEverTheyStarted()
    {
        SkUiShimmer first = Shimmer(), second = Shimmer();
        second.IsLoading = false;
        var stack = new SkUiVerticalStackLayout { Children = { new SkUiContentView { Content = first, HeightRequest = 20 }, new SkUiContentView { Content = second, HeightRequest = 20 } } };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = stack, Background = Colors.White }, 200, 40);
        surface.Frame(10_000);
        second.IsLoading = true; // starts later
        surface.Frame(10_400);
        foreach (var seconds in new[] { 10.6, 10.83 })
        {
            var bitmap = RenderAt(surface, seconds);
            Assert.Equal(Brightest(bitmap, 10), Brightest(bitmap, 30));
        }
        // One time base for every surface's render thread.
        using var other = Surface(Shimmer());
        Assert.InRange((other.Renderer.Now - surface.Renderer.Now).Duration().TotalMilliseconds, 0, 1000);
    }

    [Theory]
    [InlineData(SkUiShimmerWaveRegion.Surface)]
    [InlineData(SkUiShimmerWaveRegion.Shimmer)]
    public void ASurfaceWideWaveCrossesSeparateShimmersAsOne(SkUiShimmerWaveRegion region)
    {
        SkUiShimmer left = Shimmer(), right = Shimmer();
        left.WaveRegion = right.WaveRegion = region;
        var row = new SkUiHorizontalStackLayout { Children = { new SkUiContentView { Content = left, WidthRequest = 100 }, new SkUiContentView { Content = right, WidthRequest = 100 } } };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = row, Background = Colors.White }, 200, 20);
        surface.Frame();
        var bitmap = RenderAt(surface, 10.25);
        if (region == SkUiShimmerWaveRegion.Surface)
        {
            // One band over the surface: its middle at -20 + 240 · 0.25 = 40, in the left shimmer only.
            Assert.InRange(Brightest(bitmap, 10), 37, 43);
            AssertNear(Gray, bitmap.GetPixel(140, 10));
        }
        else
        {
            // A band per shimmer, in step: -20 + 140 · 0.25 = 15 in each.
            Assert.InRange(Brightest(bitmap, 10, 0, 100), 12, 18);
            Assert.InRange(Brightest(bitmap, 10, 100, 200), 112, 118);
        }
    }

    [Fact]
    public void ScreenReadersReadOneLoadingElementInsteadOfTheBones()
    {
        var shimmer = new SkUiShimmer
        {
            SkeletonContent = new SkUiVerticalStackLayout { Children = { new SkUiLabel { Text = "Bone" }, new SkUiButton { Text = "Tap" } } },
            Content = new SkUiLabel { Text = "Loaded" }
        };
        var root = new SkUiContentView { Content = shimmer };
        SkUiTestHelpers.Arrange(root, 200, 100);
        var element = Assert.Single(SkUiSemanticsTree.Build(root).Flatten(), node => node.Source is not null);
        Assert.Equal((SkUiSemanticsRole.ProgressBar, "Loading"), (element.Role, element.Label));

        shimmer.IsLoading = false;
        SkUiTestHelpers.Arrange(root, 200, 100);
        var labels = SkUiSemanticsTree.Build(root).Flatten().Select(node => node.Label).ToList();
        Assert.Contains("Loaded", labels);
        Assert.DoesNotContain("Loading", labels);
    }

    [Fact]
    public void TheCoreShimmerDrawsTheSamePixels()
    {
        var core = new SkUiCoreShimmer
        {
            SkeletonContent = new SkUiCoreBox { Color = Colors.Red, CornerRadius = 6 },
            BoneColor = GrayColor, HighlightColor = Colors.White, WaveWidth = 40, WaveDirection = SkUiShimmerWaveDirection.LeftToRight
        };
        var view = Shimmer();
        ((SkUiBox)view.SkeletonContent!).CornerRadius = 6;
        using var skUi = Surface(view);
        using var coreSurface = Surface(new SkUiCoreHost().SetContent(core));
        skUi.Frame();
        coreSurface.Frame();
        foreach (var seconds in new[] { 10.3, 10.6 })
        {
            var expected = RenderAt(skUi, seconds).Copy();
            var actual = RenderAt(coreSurface, seconds);
            for (var x = 0; x < 200; x += 7)
                for (var y = 0; y < 20; y += 3)
                    Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
            expected.Dispose();
        }
    }

    [Fact]
    public void TheCoreShimmerSwitchesAndReleasesTheSkeleton()
    {
        var bone = new SkUiCoreBox { Color = Colors.Red };
        var content = new SkUiCoreBox { Color = Colors.Green };
        var core = new SkUiCoreShimmer { SkeletonContent = bone, Content = content, Effect = SkUiShimmerEffect.None, BoneColor = GrayColor };
        Assert.Same(core, bone.Parent);
        Assert.Null(content.Parent);
        using var surface = Surface(new SkUiCoreHost().SetContent(core));
        AssertNear(Gray, surface.Frame().GetPixel(100, 10));
        Assert.True(bone.RenderState.HasCommitted);

        core.SetIsLoading(false);
        Assert.Equal(SKColors.Green, surface.Frame().GetPixel(100, 10));
        Assert.Null(bone.Parent);
        Assert.False(bone.RenderState.HasCommitted);
        Assert.Throws<InvalidOperationException>(() => core.SetSkeletonContent(content));
        Assert.Throws<ArgumentException>(() => core.SetBeforeStateChangeAnimation(new SkUiViewAnimation([new(SkUiAnimatableProperty.ScaleX, to: 0)])));
    }

    [Fact]
    public void InvalidValuesAreIgnoredByPropertiesAndRejectedBySetters()
    {
        var shimmer = new SkUiShimmer { WaveWidth = -1, Duration = 0, PulseOpacity = 2 };
        Assert.Equal((200d, 1000d, 0.4), (shimmer.WaveWidth, shimmer.Duration, shimmer.PulseOpacity));
        Assert.Throws<ArgumentOutOfRangeException>(() => shimmer.SetWaveWidth(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => shimmer.SetDuration(0));
        var core = new SkUiCoreShimmer();
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetDuration(-5));
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetPulseOpacity(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetEffect((SkUiShimmerEffect)42));
    }

    [Fact]
    public void AnUnchangedLayoutCommitsTheSameDrawableAndAChangeANewOne()
    {
        var shimmer = Shimmer();
        using var surface = Surface(shimmer);
        surface.Frame();
        var props = shimmer.RenderState.Node.Props;
        Assert.True(props.ChildrenLayer);
        var first = Assert.IsType<SkUiShimmerSilhouette>(props.Drawable);
        var recorded = surface.RecordedPictures;
        surface.Frame(16);
        Assert.Same(first, shimmer.RenderState.Node.Props.Drawable); // nothing changed: nothing new committed or recorded
        Assert.Equal(recorded, surface.RecordedPictures);

        shimmer.Duration = 500;
        surface.Frame(32);
        Assert.NotSame(first, shimmer.RenderState.Node.Props.Drawable);
    }

    [Fact]
    public void ShowingTheSkeletonAgainCommitsANewSilhouette()
    {
        // The compositor disposes the silhouette once the content replaces it: coming back must not commit it again.
        var shimmer = Shimmer();
        using var surface = Surface(shimmer);
        surface.Frame();
        var first = shimmer.RenderState.Node.Props.Drawable;
        Assert.IsType<SkUiShimmerSilhouette>(first);
        shimmer.IsLoading = false;
        surface.Frame();
        Assert.Null(shimmer.RenderState.Node.Props.Drawable);
        shimmer.IsLoading = true;
        surface.Frame();
        Assert.IsType<SkUiShimmerSilhouette>(shimmer.RenderState.Node.Props.Drawable);
        Assert.NotSame(first, shimmer.RenderState.Node.Props.Drawable);

        var core = new SkUiCoreShimmer { SkeletonContent = new SkUiCoreBox { Color = Colors.Red }, Content = new SkUiCoreBox { Color = Colors.Green } };
        using var coreSurface = Surface(new SkUiCoreHost().SetContent(core));
        coreSurface.Frame();
        var coreFirst = core.RenderState.Node.Props.Drawable;
        Assert.NotNull(coreFirst);
        core.IsLoading = false;
        coreSurface.Frame();
        core.IsLoading = true;
        coreSurface.Frame();
        Assert.NotSame(coreFirst, core.RenderState.Node.Props.Drawable);
    }

    [Fact]
    public void ShowingTheSkeletonAgainDrawsTheWave()
    {
        // Back from the content: the effect is drawn again (once, a disposed effect drew a black band here).
        var shimmer = Shimmer();
        using var surface = Surface(shimmer);
        surface.Frame();
        shimmer.IsLoading = false;
        surface.Frame();
        shimmer.IsLoading = true;
        surface.Frame();
        var bitmap = RenderAt(surface, 10.5);
        AssertNear(SKColors.White, bitmap.GetPixel(100, 10), 6); // the band's middle
        AssertNear(Gray, bitmap.GetPixel(10, 10));
        Assert.DoesNotContain(Enumerable.Range(0, 200).Select(x => bitmap.GetPixel(x, 10)), pixel => pixel.Red < 0x70);

        // The Core twin, the same way.
        var core = new SkUiCoreShimmer { SkeletonContent = new SkUiCoreBox { Color = Colors.Red }, Content = new SkUiCoreBox { Color = Colors.Green },
            BoneColor = GrayColor, HighlightColor = Colors.White, WaveWidth = 40, WaveDirection = SkUiShimmerWaveDirection.LeftToRight };
        using var coreSurface = Surface(new SkUiCoreHost().SetContent(core));
        coreSurface.Frame();
        core.IsLoading = false;
        coreSurface.Frame();
        core.IsLoading = true;
        coreSurface.Frame();
        AssertNear(SKColors.White, RenderAt(coreSurface, 10.5).GetPixel(100, 10), 6);
    }

    [Fact]
    public void TheImmediatePainterDrawsAStillFrame()
    {
        var shimmer = Shimmer(SkUiShimmerEffect.Pulse);
        shimmer.BoneColor = Colors.Black;
        var root = new SkUiContentView { Content = shimmer, Background = Colors.White };
        SkUiTestHelpers.Arrange(root, 200, 20);
        using var bitmap = new SKBitmap(200, 20);
        using (var canvas = new SKCanvas(bitmap))
            ((ISkUiView)root).Paint(canvas);
        // Time zero: the pulse's low point (0.4).
        AssertNear(new SKColor(153, 153, 153), bitmap.GetPixel(100, 10));
    }

    [Fact]
    public async Task SwitchesAnimateToTheNewestValue()
    {
        using var ui = TestUiContext.Install();
        var shimmer = Shimmer(SkUiShimmerEffect.None);
        shimmer.BeforeStateChangeAnimation = SkUiViewAnimation.FadeOut(100);
        var skeleton = (SkUiView)shimmer.SkeletonContent!;
        using var surface = Surface(shimmer);
        var time = 0d;
        surface.Frame(time += 16);

        shimmer.IsLoading = false;
        Assert.True(shimmer.IsSwitching);
        Assert.True(shimmer.IsShowingSkeleton); // fading out
        for (var frame = 0; frame < 100 && shimmer.IsSwitching; frame++)
        {
            surface.Frame(time += 50);
            ui.RunPending();
        }
        Assert.False(shimmer.IsSwitching);
        Assert.False(shimmer.IsShowingSkeleton);
        Assert.Equal(1, skeleton.Opacity); // the skeleton that left got its opacity back
        Assert.Equal(SKColors.Green, surface.Frame(time += 16).GetPixel(100, 10));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ViewAnimationsRunOnCoreNodes()
    {
        var node = new SkUiCoreBox { Opacity = 0.8 };
        Assert.True(await SkUiViewAnimation.FadeOut().RunAsync(node)); // not drawn: jumps to the end
        Assert.Equal(0, node.Opacity);
        await Assert.ThrowsAsync<NotSupportedException>(() => new SkUiViewAnimation([new(SkUiAnimatableProperty.ScaleY, to: 2)]).RunAsync(node));
    }

    private const string Xaml = """
        <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                     xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
          <sk:SkUiShimmer IsLoading="{Binding IsLoading}" Effect="Pulse" Duration="750" PulseOpacity="0.3" Easing="CubicInOut"
                          WaveRegion="Surface" BeforeStateChangeAnimation="FadeOut 350 CubicInOut">
            <sk:SkUiShimmer.SkeletonContent>
              <sk:SkUiBox HeightRequest="15" CornerRadius="4" />
            </sk:SkUiShimmer.SkeletonContent>
            <sk:SkUiLabel Text="Loaded" />
          </sk:SkUiShimmer>
        </ContentView>
        """;

    [Fact]
    public void LoadsFromXaml()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new LoadingModel { IsLoading = true };
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, Xaml);
        var shimmer = (SkUiShimmer)root.Content;
        Assert.Equal((SkUiShimmerEffect.Pulse, 750d, 0.3, SkUiShimmerWaveRegion.Surface), (shimmer.Effect, shimmer.Duration, shimmer.PulseOpacity, shimmer.WaveRegion));
        Assert.Same(Easing.CubicInOut, shimmer.Easing);
        Assert.Equal(350u, shimmer.BeforeStateChangeAnimation!.Length);
        Assert.IsType<SkUiBox>(shimmer.SkeletonContent);
        Assert.True(shimmer.IsShowingSkeleton);

        model.IsLoading = false; // off screen: at once
        Assert.False(shimmer.IsShowingSkeleton);
        Assert.Equal("Loaded", ((SkUiLabel)shimmer.Content!).Text);
    }

    private sealed class LoadingModel : System.ComponentModel.INotifyPropertyChanged
    {
        private static readonly System.ComponentModel.PropertyChangedEventArgs IsLoadingChanged = new(nameof(IsLoading));
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public bool IsLoading { get => field; set { field = value; PropertyChanged?.Invoke(this, IsLoadingChanged); } }
    }
}

/// <summary>Shimmer behavior that reads process-wide state: reduced motion and the look.</summary>
[Collection(GlobalStateCollection.Name)]
public class ShimmerGlobalStateTests
{
    [Fact]
    public void ReducedMotionShowsStillBones()
    {
        var reduce = SkUiMotion.ReduceMotion;
        try
        {
            SkUiMotion.ReduceMotion = true;
            var shimmer = new SkUiShimmer { SkeletonContent = new SkUiBox { Color = Colors.Red }, BoneColor = Colors.Black };
            using var surface = new SkUiTestSurface(new SkUiContentView { Content = shimmer, Background = Colors.White }, 100, 10);
            surface.Frame();
            surface.Frame(16);
            Assert.False(surface.NeedsFrame);
            Assert.False(shimmer.RenderState.Node.Props.Drawable!.IsAnimated);
            Assert.Equal(SKColors.Black, surface.Frame(500).GetPixel(50, 5));
        }
        finally
        {
            SkUiMotion.ReduceMotion = reduce;
        }
    }

    [Fact]
    public void TheLookMakesTheWaveAndTheDefaultColors()
    {
        var look = new DefaultSkUiLook();
        SkUiShimmerWavePaint? asked = null;
        look.ShimmerWaveShader = wave =>
        {
            asked = wave;
            return SKShader.CreateColor(SKColors.Blue);
        };
        var previous = SkUiLook.Current;
        try
        {
            SkUiLook.Current = look;
            var shimmer = new SkUiShimmer { SkeletonContent = new SkUiBox { Color = Colors.Red }, WaveWidth = 30, HighlightColor = Colors.Yellow };
            using var surface = new SkUiTestSurface(new SkUiContentView { Content = shimmer, Background = Colors.White }, 100, 10);
            surface.Frame();
            Assert.Equal((30f, SKColors.Yellow), (asked!.Value.Bounds.Width, asked.Value.Color));
            var colors = look.GetShimmerColors();
            Assert.NotEqual(colors.Bone, colors.Highlight);
            // The band (solid blue here) over the default bone color.
            using var canvas = new SKCanvas(surface.Bitmap);
            surface.Renderer.Render(canvas, surface.Bitmap.Info, TimeSpan.FromSeconds(10.5));
            Assert.Equal(SKColors.Blue, surface.Bitmap.GetPixel(50, 5));
            Assert.Equal(SkUiShapePainter.ToSkColor(colors.Bone), surface.Bitmap.GetPixel(2, 5));
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }
}
