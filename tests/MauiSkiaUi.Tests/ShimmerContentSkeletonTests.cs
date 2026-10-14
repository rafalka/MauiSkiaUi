using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// A skeleton made from a shimmer's content (FR-31, C9b): without <c>SkeletonContent</c> a loading shimmer shows its content,
/// each control drawing its bones (a bar per text line, placeholder lines for empty labels, shapes, toggle outlines) through
/// the look, containers keeping their chrome in <see cref="SkUiShimmerMode.Bones"/>; the <c>Skeleton</c> override; nothing
/// recorded per frame; the content records once on entering and leaving; no input; both layers.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class ShimmerContentSkeletonTests
{
    private static readonly SKColor Gray = new(0x80, 0x80, 0x80);
    private static readonly Color GrayColor = Color.FromRgb(0x80, 0x80, 0x80);
    private const string TwoWords = "Hello world";

    private static SkUiShimmer Shimmer(SkUiView content, SkUiShimmerMode mode = SkUiShimmerMode.Bones) => new()
    {
        Content = content,
        Mode = mode,
        BoneColor = GrayColor,
        HighlightColor = Colors.White,
        Effect = SkUiShimmerEffect.None
    };

    private static SkUiTestSurface Surface(SkUiView content, int width = 200, int height = 100) =>
        new(new SkUiContentView { Content = content, Background = Colors.White }, width, height);

    private static SkUiLabel Label(string text = TwoWords) => new() { Text = text, FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = 20 };

    private static SkUiShimmerBoneDrawable Bones(ISkUiRenderable node) =>
        Assert.IsType<SkUiShimmerBoneDrawable>(node.RenderState.Node.Props.Drawable);

    private static void AssertNear(SKColor expected, SKColor actual, int tolerance = 3) =>
        Assert.True(Math.Abs(expected.Red - actual.Red) <= tolerance && Math.Abs(expected.Green - actual.Green) <= tolerance
            && Math.Abs(expected.Blue - actual.Blue) <= tolerance, $"expected {expected}, got {actual}");

    [Fact]
    public void ALabelDrawsABarPerLineInsteadOfItsText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label();
        var shimmer = Shimmer(new SkUiVerticalStackLayout { Children = { label } });
        using var surface = Surface(shimmer);
        var bitmap = surface.Frame();
        Assert.True(shimmer.ShowsContentAsSkeleton);
        var bar = Assert.Single(Bones(label).BoneBounds);
        // As wide as the line (11 monospaced characters of 20 DIPs: 12 DIPs each), 60 % of its spacing, centered on it.
        Assert.InRange(bar.Width, 125, 140);
        Assert.Equal(0, bar.Left, 1);
        Assert.Equal(label.Height * 0.6, bar.Height, 1);
        Assert.Equal(label.Height / 2, bar.MidY, 1);
        AssertNear(Gray, bitmap.GetPixel((int)bar.MidX, (int)bar.MidY));
        Assert.Equal(SKColors.White, bitmap.GetPixel((int)bar.MidX, 1)); // no glyphs above the bar
        Assert.Equal(Gray, Bones(label).Color);
    }

    [Fact]
    public void WrappedLinesAreBarsOfTheirOwnWidthsAlignedAsTheText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var start = Label("One two three four five six");
        var end = Label("One two three four five six");
        end.FlowDirection = FlowDirection.RightToLeft;
        var shimmer = Shimmer(new SkUiVerticalStackLayout { WidthRequest = 150, HorizontalOptions = LayoutOptions.Start, Children = { start, end } });
        using var surface = Surface(shimmer, 200, 300);
        surface.Frame();
        var lines = Bones(start).BoneBounds;
        Assert.True(lines.Length >= 2);
        Assert.All(lines, line => Assert.Equal(0, line.Left, 1));
        Assert.All(lines, line => Assert.True(line.Right <= 150.5f));
        Assert.True(lines[0].MidY < lines[1].MidY);
        Assert.All(Bones(end).BoneBounds, line => Assert.Equal(150, line.Right, 1)); // right-to-left: at the start, the right edge
    }

    [Fact]
    public void AnEmptyLabelMeasuresAndDrawsPlaceholderLinesOnlyInTheSkeleton()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var one = Label(string.Empty);
        var three = Label(string.Empty);
        SkUiShimmer.SetSkeletonLines(three, 3);
        var none = Label(string.Empty);
        SkUiShimmer.SetSkeletonLines(none, 0);
        var stack = new SkUiVerticalStackLayout { Children = { one, three, none } };
        var shimmer = Shimmer(stack);
        using var surface = Surface(shimmer, 200, 300);
        surface.Frame();
        Assert.True(one.Height > 20);
        Assert.Equal(one.Height * 3, three.Height, 1);
        Assert.Equal(0, none.Height);
        Assert.Equal(200, Assert.Single(Bones(one).BoneBounds).Width, 1); // full width
        var lines = Bones(three).BoneBounds;
        Assert.Equal(3, lines.Length);
        Assert.Equal(200 * 0.6, lines[2].Width, 1); // the last of several is shorter
        Assert.Null(none.RenderState.Node.Props.Drawable);

        shimmer.IsLoading = false;
        surface.Frame();
        SkUiTestHelpers.Arrange(surface.Root, 200, 300);
        surface.Frame();
        Assert.Equal(0, one.Height);
        Assert.Null(one.RenderState.Node.Props.Drawable);
    }

    [Fact]
    public void ACardKeepsItsChromeInBonesModeAndDrawsNothingInASilhouette()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        SkUiBorder Card(out SkUiLabel label) => new()
        {
            Background = Colors.Green, StrokeThickness = 0, Padding = new Thickness(10, 40, 10, 10),
            Content = label = Label()
        };
        var bones = Shimmer(Card(out var bonesLabel));
        using var bonesSurface = Surface(bones);
        var bitmap = bonesSurface.Frame();
        Assert.Equal(SKColors.Green, bitmap.GetPixel(5, 5)); // the card as it is
        Assert.Single(Bones(bonesLabel).BoneBounds);

        var silhouette = Shimmer(Card(out var silhouetteLabel), SkUiShimmerMode.Silhouette);
        using var silhouetteSurface = Surface(silhouette);
        bitmap = silhouetteSurface.Frame();
        Assert.Equal(SKColors.White, bitmap.GetPixel(5, 5)); // only the bones
        var bar = Assert.Single(Bones(silhouetteLabel).BoneBounds);
        AssertNear(Gray, bitmap.GetPixel(10 + (int)bar.MidX, 40 + (int)bar.MidY));

        // Switching the mode records the card again.
        silhouette.Mode = SkUiShimmerMode.Bones;
        bitmap = silhouetteSurface.Frame();
        Assert.Equal(SKColors.Green, bitmap.GetPixel(5, 5));
    }

    [Fact]
    public void ControlsDrawTheirShapesAndOutlinesThroughTheLook()
    {
        var box = new SkUiBox { Color = Colors.Red, CornerRadius = 6, WidthRequest = 40, HeightRequest = 30 };
        var button = new SkUiButton { Text = "Go", CornerRadius = 8 };
        var toggle = new SkUiSwitch();
        var check = new SkUiCheckBox();
        var slider = new SkUiSlider { WidthRequest = 120 };
        var progress = new SkUiProgressBar { Progress = 0.5, WidthRequest = 120 };
        var stopped = new SkUiActivityIndicator { IsRunning = false };
        var stack = new SkUiVerticalStackLayout { Children = { box, button, toggle, check, slider, progress, stopped } };
        using var surface = Surface(Shimmer(stack), 200, 400);
        surface.Frame();

        var boneBox = Assert.Single(Bones(box).BoneBounds);
        Assert.Equal(new SKRect(0, 0, 40, 30), boneBox);
        Assert.Equal(new SKRect(0, 0, (float)button.Width, (float)button.Height), Assert.Single(Bones(button).BoneBounds));
        Assert.Equal(new SKRect(0, 0, (float)toggle.Width, (float)toggle.Height), Assert.Single(Bones(toggle).BoneBounds));
        var boneCheck = Assert.Single(Bones(check).BoneBounds);
        Assert.Equal(Math.Min(check.Width, check.Height), boneCheck.Width, 1);
        var track = Assert.Single(Bones(slider).BoneBounds);
        Assert.Equal(SkUiLook.Current.SliderThumbRadius, track.Height, 1);
        Assert.Equal(slider.Height / 2, track.MidY, 1);
        Assert.Equal(new SKRect(0, 0, 120, (float)progress.Height), Assert.Single(Bones(progress).BoneBounds));
        Assert.Null(stopped.RenderState.Node.Props.Drawable); // a stopped indicator draws nothing
    }

    [Fact]
    public void TheSkeletonValueKeepsHidesOrJoinsViews()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var kept = Label("Leaderboard");
        SkUiShimmer.SetSkeleton(kept, SkUiSkeleton.Keep);
        var hidden = Label();
        SkUiShimmer.SetSkeleton(hidden, SkUiSkeleton.Hidden);
        var inBlock = Label();
        var block = new SkUiHorizontalStackLayout { Children = { inBlock } };
        SkUiShimmer.SetSkeleton(block, SkUiSkeleton.Bone);
        var inPlainCard = Label();
        var plainCard = new SkUiBorder { Background = Colors.Green, StrokeThickness = 0, Content = inPlainCard };
        SkUiShimmer.SetSkeleton(plainCard, SkUiSkeleton.Children);
        var stack = new SkUiVerticalStackLayout { Children = { kept, hidden, block, plainCard } };
        using var surface = Surface(Shimmer(stack), 200, 300);
        var bitmap = surface.Frame();

        Assert.Null(kept.RenderState.Node.Props.Drawable);
        Assert.Contains(Enumerable.Range(0, 150), x => bitmap.GetPixel(x, (int)(kept.Height / 2)).Red < 128); // its glyphs are drawn
        Assert.False(hidden.RenderState.Node.Props.IsVisible);
        Assert.Equal(new SKRect(0, 0, (float)block.Width, (float)block.Height), Assert.Single(Bones(block).BoneBounds));
        Assert.False(inBlock.RenderState.Node.Props.IsVisible);
        Assert.Single(Bones(inPlainCard).BoneBounds);
        Assert.Equal(SKColors.White, bitmap.GetPixel(1, (int)(plainCard.Y + 1))); // no chrome of its own

        // Setting a value while shown draws again.
        SkUiShimmer.SetSkeleton(kept, SkUiSkeleton.Default);
        surface.Frame();
        Assert.Single(Bones(kept).BoneBounds);
    }

    [Fact]
    public void NothingIsRecordedPerFrameAndTheContentRecordsOnceWhenLoaded()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label();
        var button = new SkUiButton { Text = "Go" };
        var shimmer = Shimmer(new SkUiVerticalStackLayout { Children = { label, button } });
        shimmer.Effect = SkUiShimmerEffect.Wave;
        using var surface = Surface(shimmer);
        surface.Frame();
        Assert.True(surface.NeedsFrame);
        Assert.True(Bones(label).IsAnimated);
        var recorded = surface.RecordedPictures;
        surface.Frame(250);
        surface.Frame(500);
        Assert.Equal(recorded, surface.RecordedPictures);

        shimmer.IsLoading = false;
        surface.Frame(750);
        Assert.False(shimmer.ShowsContentAsSkeleton);
        Assert.Null(label.RenderState.Node.Props.Drawable);
        Assert.Null(button.RenderState.Node.Props.Drawable);
        Assert.InRange(surface.RecordedPictures - recorded, 2, 6); // the label, the button, their layout: once each
        Assert.False(surface.NeedsFrame);
    }

    [Fact]
    public void TheContentTakesNoInputWhileItIsASkeleton()
    {
        var clicks = 0;
        var button = new SkUiButton { Text = "Go", WidthRequest = 100, HeightRequest = 40, HorizontalOptions = LayoutOptions.Start };
        button.Clicked += (_, _) => clicks++;
        var shimmer = Shimmer(new SkUiVerticalStackLayout { Children = { button } });
        var root = new SkUiContentView { Content = shimmer };
        SkUiTestHelpers.Arrange(root, 200, 100);
        var pointer = 0;
        void Tap()
        {
            pointer++;
            root.Touch(new SkUiTouchEvent(pointer, SkUiTouchAction.Pressed, new Point(20, 20), TimeSpan.FromSeconds(pointer)));
            root.Touch(new SkUiTouchEvent(pointer, SkUiTouchAction.Released, new Point(20, 20), TimeSpan.FromSeconds(pointer + 0.02)));
        }
        Tap();
        Assert.Equal(0, clicks);
        shimmer.IsLoading = false;
        SkUiTestHelpers.Arrange(root, 200, 100);
        Tap();
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void AScrollerAroundTheShimmerScrollsTheSkeleton()
    {
        var stack = new SkUiVerticalStackLayout();
        for (var row = 0; row < 20; row++)
            stack.Children.Add(new SkUiButton { Text = $"Row {row}", HeightRequest = 40 });
        var scroller = new SkUiScrollView { Content = Shimmer(stack) };
        var root = new SkUiContentView { Content = scroller };
        SkUiTestHelpers.Arrange(root, 200, 100);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, new Point(100, 90), TimeSpan.Zero));
        for (var step = 1; step <= 6; step++)
            root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Moved, new Point(100, 90 - step * 10), TimeSpan.FromMilliseconds(step * 100)));
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, new Point(100, 30), TimeSpan.FromMilliseconds(900)));
        Assert.True(scroller.ScrollY > 0);
    }

    [Fact]
    public void ASkeletonOfItsOwnWins()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label();
        var shimmer = Shimmer(label);
        using var surface = Surface(shimmer);
        surface.Frame();
        Assert.NotNull(label.RenderState.Node.Props.Drawable);
        var box = new SkUiBox { Color = Colors.Red };
        shimmer.SkeletonContent = box;
        var bitmap = surface.Frame();
        Assert.False(shimmer.ShowsContentAsSkeleton);
        Assert.NotEqual(SKColors.White, bitmap.GetPixel(100, 50)); // the skeleton's silhouette
        shimmer.IsLoading = false;
        surface.Frame();
        Assert.Null(label.RenderState.Node.Props.Drawable); // the content is itself again
    }

    [Fact]
    public void RowsAddedWhileLoadingDrawTheirBones()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var stack = new SkUiVerticalStackLayout();
        using var surface = Surface(Shimmer(stack));
        surface.Frame();
        var label = Label();
        stack.Children.Add(label);
        surface.Frame();
        Assert.Single(Bones(label).BoneBounds);
    }

    [Fact]
    public void ViewsOutsideTheSkeletonKeepNoSkeletonState()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var inside = Label();
        var beside = Label();
        var page = new SkUiVerticalStackLayout { Children = { Shimmer(new SkUiVerticalStackLayout { Children = { inside } }), beside } };
        using var surface = Surface(page);
        var core = new SkUiCoreLabel { Text = TwoWords, FontFamily = SkUiTestHelpers.BundledFontFamily };
        using var other = Surface(new SkUiCoreHost().SetContent(new SkUiCoreVerticalStackLayout().Add(core)));
        surface.Frame();
        other.Frame();
        Assert.True(inside.SkeletonState?.InSkeleton);
        // Nodes beside a skeleton or on another surface never look for one: no state, nothing to resolve.
        Assert.Null(beside.SkeletonState);
        Assert.Null(page.SkeletonState);
        Assert.Null(core.SkeletonState);
        Assert.Null(beside.RenderState.Node.Props.Drawable);
    }

    [Fact]
    public void AnEmptyLabelAddedWhileLoadingMeasuresPlaceholderLinesBeforeItIsDrawn()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var stack = new SkUiVerticalStackLayout();
        using var surface = Surface(Shimmer(stack));
        surface.Frame();
        var empty = new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = 20 };
        stack.Children.Add(empty);
        // Its place comes with its parent: the first measure already has its placeholder line.
        Assert.True(((IView)empty).Measure(200, double.PositiveInfinity).Height > 20);
    }

    [Fact]
    public void ViewsAddedUnderALoadedShimmerKeepNoStateUntilItLoadsAgain()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var stack = new SkUiVerticalStackLayout();
        var shimmer = Shimmer(stack);
        shimmer.IsLoading = false;
        using var surface = Surface(shimmer);
        surface.Frame();
        var label = Label();
        stack.Children.Add(label);
        surface.Frame();
        Assert.Null(label.SkeletonState); // a loaded shimmer passes nothing on: recycled rows skip the skeleton path
        shimmer.IsLoading = true;
        surface.Frame();
        Assert.True(label.SkeletonState?.InSkeleton);
        Assert.IsType<SkUiShimmerBoneDrawable>(label.RenderState.Node.Props.Drawable);
    }

    [Fact]
    public void ABoneShownAsABlockMakesANewDrawableOnceTheSkeletonReplacedItsOwn()
    {
        var bone = new SkUiShimmerBone { WidthRequest = 40, HeightRequest = 10, HorizontalOptions = LayoutOptions.Start };
        SkUiShimmer.SetSkeleton(bone, SkUiSkeleton.Bone);
        var shimmer = Shimmer(new SkUiVerticalStackLayout { Children = { bone } });
        shimmer.IsLoading = false;
        using var surface = Surface(shimmer);
        surface.Frame();
        var own = bone.RenderState.Node.Props.Drawable;
        Assert.NotNull(own);
        shimmer.IsLoading = true; // the skeleton's block replaces it: the compositor disposes it
        surface.Frame();
        Assert.NotSame(own, bone.RenderState.Node.Props.Drawable);
        shimmer.IsLoading = false;
        surface.Frame();
        Assert.NotNull(bone.RenderState.Node.Props.Drawable);
        Assert.NotSame(own, bone.RenderState.Node.Props.Drawable); // a new one, not the disposed one
    }

    [Fact]
    public void AViewMovedOutOfTheSkeletonDrawsItselfAgain()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label();
        var skeleton = new SkUiVerticalStackLayout { Children = { label } };
        var outside = new SkUiVerticalStackLayout();
        using var surface = Surface(new SkUiVerticalStackLayout { Children = { Shimmer(skeleton), outside } });
        surface.Frame();
        Assert.IsType<SkUiShimmerBoneDrawable>(label.RenderState.Node.Props.Drawable);
        skeleton.Children.Remove(label);
        Assert.False(label.SkeletonState!.InSkeleton); // a detached view keeps no shimmer alive
        outside.Children.Add(label);
        surface.Frame();
        Assert.False(label.SkeletonState.InSkeleton);
        Assert.Null(label.RenderState.Node.Props.Drawable);
        Assert.Equal(SkUiSkeletonDraw.None, SkUiSkeletonScope.Resolve(label).Draw);
    }

    [Fact]
    public void ALoadedShimmerInASkeletonDrawsItsContentAsItIs()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label();
        var inner = Shimmer(new SkUiVerticalStackLayout { Children = { label } });
        inner.IsLoading = false;
        var outer = Shimmer(new SkUiVerticalStackLayout { Children = { inner } });
        using var surface = Surface(outer);
        surface.Frame();
        Assert.Equal(SkUiSkeletonDraw.None, SkUiSkeletonScope.Resolve(label).Draw);
        Assert.Null(label.RenderState.Node.Props.Drawable);
        inner.IsLoading = true;
        surface.Frame();
        Assert.Same(inner, SkUiSkeletonScope.Resolve(label).Shimmer);
        Assert.IsType<SkUiShimmerBoneDrawable>(label.RenderState.Node.Props.Drawable);
        outer.IsLoading = false;
        surface.Frame();
        Assert.Same(inner, SkUiSkeletonScope.Resolve(label).Shimmer); // the inner one still loads
    }

    /// <summary>A custom drawn control with bones of its own: a dot and a bar beside it.</summary>
    private sealed class Rating : SkUiView
    {
        public Rating()
        {
            WidthRequest = 120;
            HeightRequest = 20;
        }

        // An app overrides it as protected; this assembly sees the library's internals (InternalsVisibleTo).
        protected internal override void AddSkeletonBones(SkUiSkeletonBuilder bones)
        {
            bones.AddShape(new SKRect(0, 0, bones.Height, bones.Height), new CornerRadius(bones.Height / 2));
            bones.Add(SkUiSkeletonPartKind.TextLine, new SKRect(bones.Height + 8, 0, bones.Width, bones.Height));
        }
    }

    /// <summary>The Core twin of <see cref="Rating"/>.</summary>
    private sealed class CoreRating : SkUiCoreNode
    {
        public CoreRating()
        {
            Width = 120;
            Height = 20;
        }

        protected internal override void AddSkeletonBones(SkUiSkeletonBuilder bones) =>
            bones.AddShape(new SKRect(0, 0, bones.Height, bones.Height), new CornerRadius(bones.Height / 2));
    }

    [Fact]
    public void ACustomControlAddsItsOwnBones()
    {
        var rating = new Rating();
        var coreRating = new CoreRating();
        var host = new SkUiCoreHost().SetContent(new SkUiCoreVerticalStackLayout().Add(coreRating));
        using var surface = Surface(Shimmer(new SkUiVerticalStackLayout { Children = { rating, host } }));
        surface.Frame();
        var bones = Bones(rating).BoneBounds;
        Assert.Equal(2, bones.Length);
        Assert.Equal(new SKRect(0, 0, 20, 20), bones[0]);
        Assert.Equal(28, bones[1].Left, 1);
        Assert.Equal(120, bones[1].Right, 1);
        Assert.True(bones[1].Height < 20); // a text line's bar is thinner than its line
        Assert.Equal(new SKRect(0, 0, 20, 20), Assert.Single(Bones(coreRating).BoneBounds));
    }

    [Fact]
    public void ReducedMotionShowsStillBones()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var reduce = SkUiMotion.ReduceMotion;
        try
        {
            SkUiMotion.ReduceMotion = true;
            var label = Label();
            var shimmer = Shimmer(label);
            shimmer.Effect = SkUiShimmerEffect.Wave;
            using var surface = Surface(shimmer);
            surface.Frame();
            Assert.False(Bones(label).IsAnimated);
            Assert.False(surface.NeedsFrame);
        }
        finally
        {
            SkUiMotion.ReduceMotion = reduce;
        }
    }

    [Fact]
    public void ScreenReadersReadOneLoadingElement()
    {
        var shimmer = Shimmer(new SkUiVerticalStackLayout { Children = { new SkUiLabel { Text = "Name" }, new SkUiButton { Text = "Tap" } } });
        var root = new SkUiContentView { Content = shimmer };
        SkUiTestHelpers.Arrange(root, 200, 100);
        var element = Assert.Single(SkUiSemanticsTree.Build(root).Flatten(), node => node.Source is not null);
        Assert.Equal((SkUiSemanticsRole.ProgressBar, "Loading"), (element.Role, element.Label));
    }

    [Fact]
    public void TheCoreShimmerDrawsTheSamePixels()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var skUi = Shimmer(new SkUiVerticalStackLayout
        {
            Padding = 10, Spacing = 8, Children = { Label(), new SkUiBox { Color = Colors.Red, CornerRadius = 6, WidthRequest = 40, HeightRequest = 30 } }
        });
        using var skUiSurface = Surface(skUi);
        var core = new SkUiCoreShimmer { Mode = SkUiShimmerMode.Bones, BoneColor = GrayColor, Effect = SkUiShimmerEffect.None };
        core.SetContent(new SkUiCoreVerticalStackLayout { Padding = new Thickness(10), Spacing = 8 }
            .Add(new SkUiCoreLabel { Text = TwoWords, FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = 20 })
            .Add(new SkUiCoreBox { Color = Colors.Red, CornerRadius = 6, Width = 40, Height = 30, HorizontalAlignment = LayoutAlignment.Fill }));
        using var coreSurface = Surface(new SkUiCoreHost().SetContent(core));
        using var expected = skUiSurface.Frame().Copy();
        var actual = coreSurface.Frame();
        Assert.True(core.ShowsContentAsSkeleton);
        for (var x = 0; x < 200; x += 3)
            for (var y = 0; y < 100; y += 2)
                Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
    }

    [Fact]
    public void CoreNodesTakeTheSkeletonValueAndPlaceholderLines()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var kept = new SkUiCoreLabel { Text = TwoWords, FontFamily = SkUiTestHelpers.BundledFontFamily };
        kept.SetValue(SkUiCoreShimmer.SkeletonProperty, SkUiSkeleton.Keep);
        var empty = new SkUiCoreLabel { FontFamily = SkUiTestHelpers.BundledFontFamily };
        empty.SetValue(SkUiCoreShimmer.SkeletonLinesProperty, 2);
        var core = new SkUiCoreShimmer().SetContent(new SkUiCoreVerticalStackLayout().Add(kept).Add(empty));
        using var surface = Surface(new SkUiCoreHost().SetContent(core));
        surface.Frame();
        Assert.Null(kept.RenderState.Node.Props.Drawable);
        Assert.Equal(2, Bones(empty).BoneBounds.Length);
        Assert.True(empty.Frame.Height > 20);

        core.IsLoading = false;
        surface.Frame();
        SkUiTestHelpers.Arrange(surface.Root, 200, 100);
        surface.Frame();
        Assert.Equal(0, empty.Frame.Height);
    }

    [Fact]
    public void AHostedNativeViewIsHiddenBehindItsBone()
    {
        var hosted = new SkUiMauiContentView { Content = new Entry(), WidthRequest = 100, HeightRequest = 30 };
        var shimmer = Shimmer(new SkUiVerticalStackLayout { Children = { hosted } });
        using var surface = Surface(shimmer);
        surface.Frame();
        Assert.True(hosted.IsNativeHidden);
        Assert.Single(Bones(hosted).BoneBounds);
        shimmer.IsLoading = false;
        surface.Frame();
        Assert.False(hosted.IsNativeHidden);
    }

    /// <summary>A list whose rows are a bound name over a box, in a 200 DIP viewport; its page context has a name of its own.</summary>
    private static SkUiCollectionView List(System.Collections.ObjectModel.ObservableCollection<Person> people) => new()
    {
        HeightRequest = 200,
        ItemSpacing = 10,
        BindingContext = new Person("Page"),
        ItemsSource = people,
        EmptyView = new SkUiLabel { Text = "Nobody", FontFamily = SkUiTestHelpers.BundledFontFamily },
        ItemTemplate = new DataTemplate(() =>
        {
            var name = Label(string.Empty);
            name.SetBinding(SkUiLabel.TextProperty, nameof(Person.Name));
            return new SkUiVerticalStackLayout { Children = { name, new SkUiBox { Color = Colors.Red, HeightRequest = 10 } } };
        })
    };

    public sealed record Person(string Name);

    private static SkUiLabel NameOf(ISkUiView row) => (SkUiLabel)((SkUiVerticalStackLayout)row).Children[0];

    /// <summary>A selector as apps write them: it reads the item, so it throws for none.</summary>
    private sealed class PersonSelector(DataTemplate template) : DataTemplateSelector
    {
        protected override DataTemplate OnSelectTemplate(object item, BindableObject container) => ((Person)item).Name.Length > 0 ? template : template;
    }

    [Fact]
    public void ASelectorThatNeedsAnItemGetsPlainPlaceholderRows()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var people = new System.Collections.ObjectModel.ObservableCollection<Person>();
        var list = List(people);
        list.ItemTemplate = new PersonSelector(list.ItemTemplate!);
        using var surface = Surface(Shimmer(list), 200, 200);
        surface.Frame(); // no exception from the app's selector during layout
        SkUiTestHelpers.Arrange(surface.Root, 200, 200);
        surface.Frame();
        Assert.True(list.ShowsPlaceholders);
        Assert.All(list.PlaceholderItems, item => Assert.IsType<SkUiLabel>(item)); // a placeholder line each
        people.Add(new Person("Ada"));
        surface.Frame();
        Assert.False(list.ShowsPlaceholders); // items still use the selector
    }

    [Fact]
    public void AnEmptyListShowsPlaceholderRowsOfItsTemplateUntilItemsCome()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var people = new System.Collections.ObjectModel.ObservableCollection<Person>();
        var list = List(people);
        var shimmer = Shimmer(list);
        using var surface = Surface(shimmer, 200, 200);
        surface.Frame();
        SkUiTestHelpers.Arrange(surface.Root, 200, 200);
        surface.Frame();

        Assert.True(list.ShowsPlaceholders);
        Assert.False(list.ShowsEmptyView);
        var rows = list.PlaceholderItems;
        // Each row: one 20 DIP line of placeholder text and a 10 DIP box; 10 apart, enough rows to fill 200 DIPs.
        var row = ((SkUiView)rows[0]).Height;
        Assert.Equal((int)Math.Ceiling(210 / (row + 10)), rows.Count);
        Assert.All(rows, item => Assert.Equal(string.Empty, NameOf(item).Text)); // no item: not the page's name
        Assert.Single(Bones(NameOf(rows[0])).BoneBounds);
        Assert.Equal(new SKRect(0, 0, 200, 10), Assert.Single(Bones((SkUiBox)((SkUiVerticalStackLayout)rows[0]).Children[1]).BoneBounds));

        people.Add(new Person("Ada"));
        surface.Frame();
        Assert.False(list.ShowsPlaceholders);
        Assert.Empty(list.PlaceholderItems);

        people.Clear();
        surface.Frame();
        Assert.True(list.ShowsPlaceholders);
        shimmer.IsLoading = false;
        surface.Frame();
        Assert.False(list.ShowsPlaceholders); // loaded and empty: the empty view
        Assert.True(list.ShowsEmptyView);
    }

    [Fact]
    public void ACountAndASpanPlaceThePlaceholderItemsAsAGrid()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var list = List([]);
        list.Span = 2;
        list.SpanSpacing = 20;
        SkUiShimmer.SetSkeletonItemCount(list, 4);
        using var surface = Surface(Shimmer(list), 200, 200);
        surface.Frame();
        var items = list.PlaceholderItems.Cast<SkUiView>().ToList();
        Assert.Equal(4, items.Count);
        Assert.Equal((0d, 90d), (items[0].X, items[0].Width));
        Assert.Equal((110d, 90d), (items[1].X, items[1].Width));
        Assert.Equal(items[0].Height + 10, items[2].Y, 1);

        SkUiShimmer.SetSkeletonItemCount(list, 0);
        surface.Frame();
        Assert.False(list.ShowsPlaceholders);
    }

    [Fact]
    public void AListOutsideASkeletonShowsItsEmptyView()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var list = List([]);
        using var surface = Surface(new SkUiVerticalStackLayout { Children = { list } }, 200, 200);
        surface.Frame();
        Assert.False(list.ShowsPlaceholders);
        Assert.Empty(list.PlaceholderItems);
    }

    [Fact]
    public void TheLookMakesTheBones()
    {
        var look = new DefaultSkUiLook();
        var bones = new List<SkUiSkeletonBone>();
        look.AddSkeletonBones(new SkUiSkeletonPart(SkUiSkeletonPartKind.TextLine, new SKRect(0, 0, 100, 20)), bones);
        Assert.Equal(new SKRect(0, 4, 100, 16), bones[0].Bounds);
        bones.Clear();
        look.AddSkeletonBones(new SkUiSkeletonPart(SkUiSkeletonPartKind.RadioButton, new SKRect(0, 0, 30, 20)), bones);
        Assert.Equal((new SKRect(5, 0, 25, 20), new CornerRadius(10)), (bones[0].Bounds, bones[0].CornerRadius));
        bones.Clear();
        look.SkeletonBones = (part, list) => list.Add(new SkUiSkeletonBone(part.Bounds));
        look.AddSkeletonBones(new SkUiSkeletonPart(SkUiSkeletonPartKind.TextLine, new SKRect(0, 0, 100, 20)), bones);
        Assert.Equal(new SKRect(0, 0, 100, 20), bones[0].Bounds);
    }
}
