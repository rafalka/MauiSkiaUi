using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using Microsoft.Maui.Layouts;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.16 phase B: the public <see cref="SkUiScrollEffect"/> and <see cref="SkUiScrollReveal"/> on both layers: validation,
/// factories, finding the scroller (a reference, the nearest ancestor, a collection view's sticky parts, a Core node following a
/// SkUi* list), one view per effect, re-commits on scroll, release, and the reduced-motion rules. In the global-state
/// collection: the reduced-motion tests set <see cref="SkUiMotion.ReduceMotion"/>, which would turn the animations of tests
/// running in parallel off (a sheet found open on its first frames).
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class ScrollEffectTests
{
    private const float Tolerance = 1e-3f;

    private static SkUiRenderProps Props(object node)
    {
        var props = SkUiRenderProps.Default;
        ((ISkUiRenderable)node).GetRenderProps(ref props);
        return props;
    }

    private static SkUiScrollView List() => new() { Content = new SkUiBox { Color = Colors.White, HeightRequest = 4000 } };

    /// <summary>A list and, beside it, a red 20 × 20 box at the top of a 40 DIP column.</summary>
    private static (SkUiScrollView List, SkUiBox Box, SkUiGrid Root) Page()
    {
        var list = List();
        var box = new SkUiBox { Color = Colors.Red, WidthRequest = 20, HeightRequest = 20, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        var root = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(40), new ColumnDefinition(100)] };
        root.Children.Add(box);
        Grid.SetColumn(list, 1);
        root.Children.Add(list);
        return (list, box, root);
    }

    private static int TopOfRed(SKBitmap bitmap) =>
        Enumerable.Range(0, 100).First(y => bitmap.GetPixel(10, y) is { Red: > 200, Green: < 60, Alpha: > 200 });

    private sealed class ReducedMotion : IDisposable
    {
        private readonly bool? _saved = SkUiMotion.ReduceMotion;

        public ReducedMotion() => SkUiMotion.ReduceMotion = true;

        public void Dispose() => SkUiMotion.ReduceMotion = _saved;
    }

    #region The effect

    [Fact]
    public void InvalidValuesThrow()
    {
        var effect = new SkUiScrollEffect();
        Assert.Throws<ArgumentOutOfRangeException>(() => effect.Orientation = ScrollOrientation.Both);
        Assert.Throws<ArgumentOutOfRangeException>(() => effect.From = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => effect.InputEnd = double.PositiveInfinity);
        Assert.Throws<ArgumentOutOfRangeException>(() => effect.ClipCornerRadius = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => effect.Property = (SkUiScrollEffectProperty)99);
        var reveal = new SkUiScrollReveal();
        Assert.Throws<ArgumentOutOfRangeException>(() => reveal.Distance = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => reveal.Duration = TimeSpan.FromSeconds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => reveal.Orientation = ScrollOrientation.Neither);
    }

    [Fact]
    public void AChangeRaisesOnceAndTheEasingIsSampled()
    {
        var effect = new SkUiScrollEffect();
        var raised = new List<string?>();
        effect.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        effect.To = 10;
        effect.To = 10;
        effect.Easing = Easing.CubicIn;
        Assert.Equal([nameof(SkUiScrollEffect.To), nameof(SkUiScrollEffect.Easing)], raised);
        var curve = effect.Curve!;
        Assert.Equal(SkUiScrollEffect.CurveSamples, curve.Length);
        Assert.Equal((float)Easing.CubicIn.Ease(0.5), curve[16], Tolerance);
        effect.Easing = null;
        Assert.Null(effect.Curve);
    }

    [Fact]
    public void TheFactoriesBuildWhatTheySay()
    {
        var parallax = SkUiScrollEffect.Parallax(0.5, 1000);
        Assert.Equal((SkUiScrollEffectProperty.TranslationY, 0d, 1000d, 0d, -500d), (parallax.Property, parallax.InputStart, parallax.InputEnd, parallax.From, parallax.To));
        var fade = SkUiScrollEffect.FadeIn(40, 100);
        Assert.Equal((SkUiScrollEffectProperty.Opacity, 40d, 100d, 0d, 1d), (fade.Property, fade.InputStart, fade.InputEnd, fade.From, fade.To));
        var collapse = SkUiScrollEffect.CollapseTo(240, 56);
        Assert.Equal((SkUiScrollEffectProperty.ClipHeight, 0d, 184d, 240d, 56d), (collapse.Property, collapse.InputStart, collapse.InputEnd, collapse.From, collapse.To));
        var reveal = new SkUiScrollReveal();
        var hide = SkUiScrollEffect.HideOnScroll(reveal, 88);
        Assert.Equal((SkUiScrollEffectProperty.TranslationY, 0d, 88d), (hide.Property, hide.From, hide.To));
        Assert.Same(reveal, hide.Reveal);
    }

    [Fact]
    public void AnEffectBelongsToOneViewAtATime()
    {
        var effect = new SkUiScrollEffect();
        var first = new SkUiBox();
        first.AddScrollEffect(effect);
        Assert.Throws<InvalidOperationException>(() => new SkUiBox().AddScrollEffect(effect));
        Assert.Throws<InvalidOperationException>(() => new SkUiCoreBox().AddScrollEffect(effect));
        Assert.True(first.RemoveScrollEffect(effect));
        new SkUiCoreBox().AddScrollEffect(effect);
    }

    #endregion

    #region Finding the scroller

    [Fact]
    public void AnEffectFollowsTheScrollerItNamesOnBothThreads()
    {
        var (list, box, root) = Page();
        box.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.TranslationY, 0, 1000, 0, 50) { Source = list });
        using var surface = new SkUiTestSurface(root, 140, 100);
        Assert.Equal(0, TopOfRed(surface.Frame(0)));
        var recorded = surface.RecordedPictures;
        list.ScrollTo(0, 400);
        Assert.Equal(20, Props(box).EffectiveTranslationY, Tolerance);
        Assert.Equal(20, TopOfRed(surface.Frame(10)));
        // A render-thread fling moves it with no UI work, and nothing is recorded again.
        Assert.True(((ISkUiScrollHost)list).Scroller.StartFling(new Point(0, 2000)));
        surface.Frame(20);
        var later = TopOfRed(surface.Frame(200));
        Assert.True(later > 20, later.ToString());
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    [Fact]
    public void WithoutASourceTheNearestScrollingAncestorDrivesIt()
    {
        var inner = new SkUiBox { Color = Colors.Red, HeightRequest = 20 };
        inner.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.Opacity, 0, 100, 1, 0));
        var content = new SkUiVerticalStackLayout();
        content.Children.Add(inner);
        content.Children.Add(new SkUiBox { HeightRequest = 4000 });
        var list = new SkUiScrollView { Content = content };
        using var surface = new SkUiTestSurface(list, 100, 100);
        surface.Frame(0);
        list.ScrollTo(0, 50);
        surface.Frame(10);
        Assert.Equal(0.5f, Props(inner).LinkValues.Opacity, Tolerance);
    }

    [Fact]
    public void AStickyHeadersEffectsFollowItsCollectionView()
    {
        var reveal = new SkUiScrollReveal { Response = SkUiScrollRevealResponse.Triggered };
        var header = new SkUiBox { Color = Colors.Red, HeightRequest = 30 };
        header.AddScrollEffect(new SkUiScrollEffect { Property = SkUiScrollEffectProperty.TranslationY, From = 0, To = -30, Reveal = reveal });
        var list = new SkUiCollectionView
        {
            ItemsSource = Enumerable.Range(0, 200).ToList(), IsStickyHeader = true, Header = header,
            ItemTemplate = new DataTemplate(() => new SkUiBox { HeightRequest = 40, Color = Colors.White })
        };
        using var surface = new SkUiTestSurface(list, 100, 200);
        surface.Frame(0);
        Assert.NotNull(Props(header).Links);
        Assert.True(reveal.IsShown);
        list.ScrollTo(100, position: ScrollToPosition.Start, animate: false);
        surface.Frame(16);
        for (var step = 1; step <= 4; step++)
        {
            var scroller = ((ISkUiScrollHost)ScrollerOf(list)).Scroller;
            scroller.SetOffset(0, scroller.Y + 10);
            surface.Frame(16 + step * 16);
        }
        surface.Frame(600);
        Assert.False(reveal.IsShown);
        Assert.Equal(-30, Props(header).EffectiveTranslationY, Tolerance);
    }

    private static SkUiScrollView ScrollerOf(SkUiCollectionView list) =>
        (SkUiScrollView)((ISkUiScrollEffectSource)list).ScrollEffectHost!;

    [Fact]
    public void ACoreNodeFollowsASkUiList()
    {
        var list = List();
        var core = new SkUiCoreBox().SetColor(Colors.Red).SetWidth(20).SetHeight(20);
        core.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.ScaleY, 0, 100, 1, 2) { Source = list });
        Assert.Single(core.ScrollEffects);
        var root = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(40), new ColumnDefinition(100)] };
        root.Children.Add(new SkUiCoreHost().SetContent(core));
        Grid.SetColumn(list, 1);
        root.Children.Add(list);
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        list.ScrollTo(0, 100);
        surface.Frame(10);
        Assert.Equal(2, Props(core).Matrix.ScaleY, Tolerance);
    }

    [Fact]
    public void ACoreNodeInACoreScrollViewFollowsIt()
    {
        var core = new SkUiCoreBox().SetColor(Colors.Red).SetHeight(20);
        core.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.TranslationX, 0, 100, 0, 30));
        var stack = new SkUiCoreVerticalStackLayout();
        stack.Add(core);
        stack.Add(new SkUiCoreBox().SetHeight(4000));
        var scroll = new SkUiCoreScrollView();
        scroll.SetContent(stack);
        using var surface = new SkUiTestSurface(new SkUiCoreHost().SetContent(scroll), 100, 100);
        surface.Frame(0);
        scroll.ScrollTo(0, 100);
        surface.Frame(10);
        Assert.Equal(30, Props(core).EffectiveTranslationX, Tolerance);
    }

    [Fact]
    public void AnEffectChangeCommitsTheViewWithoutRecordingIt()
    {
        var (list, box, root) = Page();
        var effect = new SkUiScrollEffect(SkUiScrollEffectProperty.TranslationY, 0, 100, 0, 10) { Source = list };
        box.AddScrollEffect(effect);
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        var recorded = surface.RecordedPictures;
        list.ScrollTo(0, 100);
        surface.Frame(10);
        Assert.Equal(10, TopOfRed(surface.Bitmap));
        effect.To = 30;
        Assert.Equal(30, TopOfRed(surface.Frame(20)));
        effect.IsEnabled = false;
        Assert.Equal(0, TopOfRed(surface.Frame(30)));
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    [Fact]
    public void ARightToLeftClipKeepsTheLeadingSide()
    {
        var (list, box, root) = Page();
        box.WidthRequest = 40;
        box.FlowDirection = FlowDirection.RightToLeft;
        box.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.ClipWidth, 0, 100, 40, 10) { Source = list });
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        list.ScrollTo(0, 100);
        surface.Frame(10);
        Assert.True(Props(box).TryGetLinkClip(out var clip));
        Assert.Equal((30f, 40f), (clip.Rect.Left, clip.Rect.Right)); // the right side: leading in right to left
    }

    [Fact]
    public void LeavingTheTreeStopsListeningAndComingBackListensAgain()
    {
        var (list, box, root) = Page();
        box.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.TranslationY, 0, 100, 0, 10) { Source = list });
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        var scroller = ((ISkUiScrollHost)list).Scroller;
        Assert.Equal(1, Listeners(scroller));
        root.Children.Remove(box);
        surface.Frame(10);
        Assert.Equal(0, Listeners(scroller));
        root.Children.Insert(0, box);
        surface.Frame(20);
        Assert.Equal(1, Listeners(scroller));
    }

    /// <summary>How many scroll effect sets listen to <paramref name="scroller"/>'s scroll reports.</summary>
    private static int Listeners(SkUiScrollController scroller)
    {
        var field = typeof(SkUiScrollController).GetField("Scrolled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var handlers = (field?.GetValue(scroller) as Delegate)?.GetInvocationList() ?? [];
        return handlers.Count(handler => handler.Target is SkUiScrollEffectSet);
    }

    #endregion

    [Fact]
    public void AClipOnAHeaderStaysInPlaceWhileWhatIsInsideItMoves()
    {
        // The collapsing header recipe: the clip on the container, the parallax on its child.
        var list = List();
        var picture = new SkUiBox { Color = Colors.Red, HeightRequest = 100 };
        picture.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.TranslationY, 0, 80, 0, -40) { Source = list });
        var header = new SkUiGrid { HeightRequest = 100, VerticalOptions = LayoutOptions.Start, WidthRequest = 20, HorizontalOptions = LayoutOptions.Start };
        header.Children.Add(picture);
        header.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.ClipHeight, 0, 80, 100, 20) { Source = list });
        var root = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(40), new ColumnDefinition(100)] };
        root.Children.Add(header);
        Grid.SetColumn(list, 1);
        root.Children.Add(list);
        using var surface = new SkUiTestSurface(root, 140, 120);
        surface.Frame(0);
        list.ScrollTo(0, 80);
        var bitmap = surface.Frame(10);
        // The window stays at the top (0…20) and shows the picture, moved up by 40 inside it.
        Assert.True(bitmap.GetPixel(10, 10) is { Red: > 200, Green: < 60, Alpha: > 200 });
        Assert.Equal(0, bitmap.GetPixel(10, 30).Alpha);
    }

    #region Snapping to the ends

    /// <summary>A list with a header effect that settles on its ends over the first 144 DIPs.</summary>
    private static (SkUiScrollView List, SkUiScrollController Scroller, SkUiScrollEffect Effect, SkUiTestSurface Surface) SnappingPage()
    {
        var (list, box, root) = Page();
        var effect = new SkUiScrollEffect(SkUiScrollEffectProperty.ClipHeight, 0, 144, 200, 56) { Source = list, SnapsToEnds = true };
        box.AddScrollEffect(effect);
        var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        return (list, ((ISkUiScrollHost)list).Scroller, effect, surface);
    }

    /// <summary>Draws frames until the scroller's motion ends (or 3 s).</summary>
    private static void Settle(SkUiTestSurface surface, double startMs)
    {
        for (var ms = startMs + 16; ms < startMs + 3000; ms += 16)
            surface.Frame(ms);
    }

    [Theory]
    [InlineData(60, 0)]
    [InlineData(100, 144)]
    [InlineData(144, 144)]
    [InlineData(300, 300)]
    public void AtRestInsideTheRangeTheScrollerSettlesOnTheNearerEnd(double offset, double settled)
    {
        var (_, scroller, _, surface) = SnappingPage();
        using var _ = surface;
        scroller.SetOffset(0, offset);
        scroller.Snap(Point.Zero); // what a drag's release does
        Settle(surface, 10);
        Assert.Equal(settled, scroller.Y, 0.5);
    }

    [Fact]
    public void AFlingThatWouldStopInsideTheRangeEndsOnAnEndAndOnePassingThroughIsAFling()
    {
        var (_, scroller, _, surface) = SnappingPage();
        using var _ = surface;
        // From 0, a velocity whose fling would stop near 100: settles on 144 (nearer) instead.
        Assert.True(scroller.StartFling(new Point(0, 100 / SkUiRenderFling.TotalTravelPerVelocity)));
        Settle(surface, 10);
        Assert.Equal(144, scroller.Y, 0.5);
        // A fast fling goes on past the range as a fling.
        scroller.SetOffset(0, 0);
        Assert.True(scroller.StartFling(new Point(0, 3000)));
        Settle(surface, 4000);
        Assert.True(scroller.Y > 300, scroller.Y.ToString());
    }

    [Fact]
    public void ADragReleasedInsideTheRangeSettles()
    {
        var (list, scroller, _, surface) = SnappingPage();
        using var _ = surface;
        var root = surface.Root;
        // Slowly up by 50 (no fling): released at 50, nearer the start.
        root.Touch(new(9, SkUiTouchAction.Pressed, new Point(90, 80), TimeSpan.FromSeconds(1)));
        for (var step = 1; step <= 10; step++)
            root.Touch(new(9, SkUiTouchAction.Moved, new Point(90, 80 - 5 * step), TimeSpan.FromSeconds(1) + TimeSpan.FromMilliseconds(step * 100)));
        root.Touch(new(9, SkUiTouchAction.Released, new Point(90, 30), TimeSpan.FromSeconds(2.5)));
        Settle(surface, 10);
        Assert.Equal(0, scroller.Y, 0.5);
        GC.KeepAlive(list);
    }

    [Fact]
    public void RemovingTheEffectOrTurningSnappingOffDropsTheRange()
    {
        var (_, scroller, effect, surface) = SnappingPage();
        using var _ = surface;
        Assert.True(scroller.HasEndSnapRanges);
        effect.SnapsToEnds = false;
        surface.Frame(10);
        Assert.False(scroller.HasEndSnapRanges);
        effect.SnapsToEnds = true;
        surface.Frame(20);
        Assert.True(scroller.HasEndSnapRanges);
        ((SkUiView)effect.Owner!).RemoveScrollEffect(effect);
        surface.Frame(30);
        Assert.False(scroller.HasEndSnapRanges);
        Assert.Equal(0, Listeners(scroller)); // the last effect gone: the view no longer listens
    }

    [Fact]
    public void WithSnapPointsATargetInsideTheRangeMovesToAnEnd()
    {
        var (list, scroller, _, surface) = SnappingPage();
        using var _ = surface;
        // Every 40 DIPs a snap point (the content's children); 80 is one, inside 0…144: the nearer end, 144, wins.
        var stack = new SkUiVerticalStackLayout();
        for (var index = 0; index < 100; index++)
            stack.Children.Add(new SkUiBox { HeightRequest = 40 });
        list.Content = stack;
        list.SnapPointsType = SnapPointsType.Mandatory;
        surface.Frame(10);
        scroller.SetOffset(0, 82);
        scroller.Snap(Point.Zero);
        Settle(surface, 20);
        Assert.Equal(144, scroller.Y, 0.5);
    }

    #endregion

    #region Reduced motion

    [Fact]
    public void UnderReducedMotionFadesAndClipsRunAndParallaxAndScalesHold()
    {
        using var _ = new ReducedMotion();
        var (list, box, root) = Page();
        var fade = new SkUiScrollEffect(SkUiScrollEffectProperty.Opacity, 0, 100, 1, 0.5) { Source = list };
        var clip = new SkUiScrollEffect(SkUiScrollEffectProperty.ClipHeight, 0, 100, 20, 10) { Source = list };
        var parallax = SkUiScrollEffect.Parallax(0.5, 100).SetSource(list);
        var withContent = new SkUiScrollEffect(SkUiScrollEffectProperty.TranslationX, 0, 100, 0, -100) { Source = list };
        var scale = new SkUiScrollEffect(SkUiScrollEffectProperty.Scale, 0, 100, 1, 2) { Source = list };
        box.AddScrollEffects(fade, clip, parallax, withContent, scale);
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        list.ScrollTo(0, 100);
        var values = Props(box).LinkValues;
        Assert.Equal(0.5f, values.Opacity, Tolerance);
        Assert.Equal(10, values.ClipHeight, Tolerance);
        Assert.Equal(0, values.TranslationY, Tolerance);    // parallax held
        Assert.Equal(-100, values.TranslationX, Tolerance); // moves with the content: runs
        Assert.Equal(1, values.ScaleX, Tolerance);          // held
        // Overrides.
        scale.ReducedMotion = SkUiScrollEffectReducedMotion.Run;
        fade.ReducedMotion = SkUiScrollEffectReducedMotion.HoldFrom;
        values = Props(box).LinkValues;
        Assert.Equal(2, values.ScaleX, Tolerance);
        Assert.Equal(1, values.Opacity, Tolerance);
    }

    [Fact]
    public void UnderReducedMotionATriggeredHideJumpsWhileItsFadeRuns()
    {
        using var _ = new ReducedMotion();
        var (list, box, root) = Page();
        var reveal = new SkUiScrollReveal { Response = SkUiScrollRevealResponse.Triggered, Source = list, Duration = TimeSpan.FromMilliseconds(400) };
        box.AddScrollEffects(
            SkUiScrollEffect.HideOnScroll(reveal, 50),
            new SkUiScrollEffect { Property = SkUiScrollEffectProperty.Opacity, From = 1, To = 0, Reveal = reveal });
        using var surface = new SkUiTestSurface(root, 140, 100);
        list.ScrollTo(0, 500);
        surface.Frame(0);
        reveal.Hide();
        surface.Frame(100); // a quarter of the way
        var values = Props(box).LinkValues;
        Assert.Equal(0, values.TranslationY, Tolerance); // not yet: it jumps halfway
        Assert.Equal(0.75f, values.Opacity, 0.02f);
        surface.Frame(300);
        values = Props(box).LinkValues;
        Assert.Equal(50, values.TranslationY, Tolerance);
        Assert.Equal(0.25f, values.Opacity, 0.02f);
    }

    [Fact]
    public void AManualRevealFollowsShowAndHideNotTheScroll()
    {
        var (list, box, root) = Page();
        var reveal = new SkUiScrollReveal { Response = SkUiScrollRevealResponse.Manual, Source = list, Duration = TimeSpan.FromMilliseconds(200) };
        box.AddScrollEffect(SkUiScrollEffect.HideOnScroll(reveal, 50));
        using var surface = new SkUiTestSurface(root, 140, 100);
        list.ScrollTo(0, 400);
        surface.Frame(0);
        list.ScrollTo(0, 800);
        surface.Frame(16);
        Assert.True(reveal.IsShown);
        Assert.Equal(0, Props(box).LinkValues.TranslationY, Tolerance);
        reveal.Hide();
        surface.Frame(20);
        surface.Frame(300);
        Assert.False(reveal.IsShown);
        Assert.Equal(50, Props(box).LinkValues.TranslationY, Tolerance);
        reveal.Show();
        surface.Frame(310);
        surface.Frame(600);
        Assert.True(reveal.IsShown);
    }

    [Fact]
    public void AWhileScrollingRevealHidesDuringAFlingAndComesBackWhenItEnds()
    {
        var (list, box, root) = Page();
        var reveal = new SkUiScrollReveal { Response = SkUiScrollRevealResponse.WhileScrolling, Source = list, Distance = 60, Duration = TimeSpan.FromMilliseconds(200) };
        box.AddScrollEffect(SkUiScrollEffect.HideOnScroll(reveal, 50));
        using var surface = new SkUiTestSurface(root, 140, 100);
        list.ScrollTo(0, 400);
        surface.Frame(0);
        Assert.True(((ISkUiScrollHost)list).Scroller.StartFling(new Point(0, 2000)));
        surface.Frame(10);
        surface.Frame(60);
        Assert.False(reveal.IsShown); // hidden while it flings
        var hidden = Props(box).LinkValues.TranslationY;
        Assert.True(hidden > 25, hidden.ToString());
        // The fling ends, the list rests, the button comes back.
        for (var ms = 76; ms < 4000; ms += 16)
            surface.Frame(ms);
        Assert.True(reveal.IsShown);
        Assert.Equal(0, Props(box).LinkValues.TranslationY, Tolerance);
    }

    #region Review fixes

    [Fact]
    public void AShowOrHideAtRestDrawsAFrameOnItsOwn()
    {
        var (list, box, root) = Page();
        var reveal = new SkUiScrollReveal { Response = SkUiScrollRevealResponse.Manual, Source = list };
        box.AddScrollEffect(SkUiScrollEffect.HideOnScroll(reveal, 50));
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        surface.Frame(16);
        var requests = surface.RenderRequests;
        reveal.Hide(); // nothing else changes: the request alone must reach the render thread
        surface.Frame(32);
        Assert.True(surface.RenderRequests > requests);
    }

    [Fact]
    public void ACorrectionAtRestIsNotAScrollForAReveal()
    {
        var (list, box, root) = Page();
        var reveal = new SkUiScrollReveal { Source = list, Distance = 50 };
        box.AddScrollEffect(SkUiScrollEffect.HideOnScroll(reveal, 50));
        using var surface = new SkUiTestSurface(root, 140, 100);
        list.ScrollTo(0, 600);
        surface.Frame(0);
        surface.Frame(16);
        Assert.True(reveal.IsShown);
        // 120 DIPs of rows inserted above while the list rests (under two viewports, so it is not taken for a jump either):
        // the offset moves with them; the reveal must not.
        ((ISkUiScrollHost)list).Scroller.CorrectOffset(0, 120);
        surface.Frame(32);
        surface.Frame(48);
        Assert.True(reveal.IsShown);
        Assert.Equal(0, Props(box).LinkValues.TranslationY, Tolerance);
    }

    [Fact]
    public void ARefreshViewSourceFollowsItsContentsScrollerBeforeAnyPull()
    {
        var inner = List();
        var refresh = new SkUiRefreshView { Content = inner };
        var box = new SkUiBox { Color = Colors.Red, WidthRequest = 20, HeightRequest = 20, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        box.AddScrollEffect(new SkUiScrollEffect(SkUiScrollEffectProperty.TranslationY, 0, 100, 0, 30) { Source = refresh });
        var root = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(40), new ColumnDefinition(100)] };
        root.Children.Add(box);
        Grid.SetColumn(refresh, 1);
        root.Children.Add(refresh);
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        inner.ScrollTo(0, 100);
        surface.Frame(10);
        Assert.Equal(30, Props(box).EffectiveTranslationY, Tolerance);
    }

    [Fact]
    public void UnderReducedMotionAScaleOnAScrollFollowingRevealJumps()
    {
        using var _ = new ReducedMotion();
        var (list, box, root) = Page();
        var reveal = new SkUiScrollReveal { Source = list };
        box.AddScrollEffect(new SkUiScrollEffect { Property = SkUiScrollEffectProperty.Scale, From = 1, To = 0.8, Reveal = reveal });
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        var link = Assert.Single(Props(box).Links!);
        Assert.Equal(link.InputStart, link.InputEnd); // a step halfway, not a zoom
    }

    [Fact]
    public void AHideBeforeTheFirstDrawIsKept()
    {
        var (list, box, root) = Page();
        var reveal = new SkUiScrollReveal { Response = SkUiScrollRevealResponse.Manual, Source = list, Duration = TimeSpan.Zero };
        box.AddScrollEffect(SkUiScrollEffect.HideOnScroll(reveal, 50));
        reveal.Hide();
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        surface.Frame(16);
        surface.Frame(32);
        Assert.False(reveal.IsShown);
    }

    [Fact]
    public void ARevealSharedByViewsUnderTwoScrollersKeepsTheFirstAndLetsGoWhenTheyLeave()
    {
        var reveal = new SkUiScrollReveal { Distance = 50 };
        SkUiScrollView Column(out SkUiBox box)
        {
            box = new SkUiBox { HeightRequest = 20 };
            box.AddScrollEffect(SkUiScrollEffect.HideOnScroll(reveal, 50));
            var stack = new SkUiVerticalStackLayout();
            stack.Children.Add(box);
            stack.Children.Add(new SkUiBox { HeightRequest = 4000 });
            return new SkUiScrollView { Content = stack };
        }
        var first = Column(out var firstBox);
        var second = Column(out var secondBox);
        var root = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(70), new ColumnDefinition(70)] };
        root.Children.Add(first);
        Grid.SetColumn(second, 1);
        root.Children.Add(second);
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        // The second view binds after the first: both follow the first list.
        for (var step = 1; step <= 10; step++)
        {
            first.ScrollTo(0, step * 10);
            surface.Frame(step * 16);
        }
        Assert.False(reveal.IsShown);
        Assert.Equal(Props(firstBox).LinkValues.TranslationY, Props(secondBox).LinkValues.TranslationY, Tolerance);
        // Both views gone: the reveal keeps no scroller.
        root.Children.Clear();
        surface.Frame(400);
        var render = typeof(SkUiScrollReveal).GetField("_render", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.Null(render.GetValue(reveal));
    }

    [Fact]
    public void WhenTheFirstViewOfASharedRevealLeavesTheOthersFollowTheirOwnScroller()
    {
        var reveal = new SkUiScrollReveal { Distance = 50 };
        SkUiScrollView Column(out SkUiBox box)
        {
            box = new SkUiBox { HeightRequest = 20 };
            box.AddScrollEffect(SkUiScrollEffect.HideOnScroll(reveal, 50));
            var stack = new SkUiVerticalStackLayout();
            stack.Children.Add(box);
            stack.Children.Add(new SkUiBox { HeightRequest = 4000 });
            return new SkUiScrollView { Content = stack };
        }
        var first = Column(out _);
        var second = Column(out var secondBox);
        var root = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(70), new ColumnDefinition(70)] };
        root.Children.Add(first);
        Grid.SetColumn(second, 1);
        root.Children.Add(second);
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        // Hidden through the first list, which then leaves: shown again, and the second view binds to its own list.
        for (var step = 1; step <= 10; step++)
        {
            first.ScrollTo(0, step * 10);
            surface.Frame(step * 16);
        }
        Assert.False(reveal.IsShown);
        root.Children.Remove(first);
        surface.Frame(300);
        surface.Frame(316);
        Assert.True(reveal.IsShown);
        for (var step = 1; step <= 10; step++)
        {
            second.ScrollTo(0, step * 10);
            surface.Frame(316 + step * 16);
        }
        Assert.False(reveal.IsShown);
        Assert.Equal(50, Props(secondBox).LinkValues.TranslationY, Tolerance);
    }

    #endregion

    [Fact]
    public void AGentleFlingFromAnEndIsConsumedByTheEndSnap()
    {
        var (_, scroller, _, surface) = SnappingPage();
        using var _ = surface;
        // At 0, a fling that would stop near 50 (inside 0…144): the nearer end is where it already is; no free fling.
        Assert.True(scroller.StartFling(new Point(0, 50 / SkUiRenderFling.TotalTravelPerVelocity)));
        Settle(surface, 10);
        Assert.Equal(0, scroller.Y, 0.5);
    }

    [Fact]
    public void AWheelPauseIsARestForReveals()
    {
        var (list, box, root) = Page();
        var reveal = new SkUiScrollReveal { Response = SkUiScrollRevealResponse.WhileScrolling, Source = list, Distance = 40, Duration = TimeSpan.FromMilliseconds(100) };
        box.AddScrollEffect(SkUiScrollEffect.HideOnScroll(reveal, 50));
        using var surface = new SkUiTestSurface(root, 140, 100);
        list.ScrollTo(0, 400);
        surface.Frame(0);
        var scroller = ((ISkUiScrollHost)list).Scroller;
        scroller.Wheel(0, -60);
        surface.Frame(16);
        surface.Frame(32);
        Assert.False(reveal.IsShown);
        scroller.OnWheelRest(); // what the wheel's idle delay calls (headless tests have no dispatcher)
        for (var ms = 48; ms < 400; ms += 16)
            surface.Frame(ms);
        Assert.True(reveal.IsShown);
        Assert.Throws<ArgumentNullException>(() => SkUiScrollEffect.HideOnScroll(null!, 10));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverlappingEndSnapRangesActAsOneInEitherOrder(bool reversed)
    {
        var (list, box, root) = Page();
        var first = new SkUiScrollEffect(SkUiScrollEffectProperty.Opacity, 0, 144, 1, 1) { Source = list, SnapsToEnds = true };
        var second = new SkUiScrollEffect(SkUiScrollEffectProperty.Opacity, 100, 300, 1, 1) { Source = list, SnapsToEnds = true };
        if (reversed)
            box.AddScrollEffects(second, first);
        else
            box.AddScrollEffects(first, second);
        using var surface = new SkUiTestSurface(root, 140, 100);
        surface.Frame(0);
        var scroller = ((ISkUiScrollHost)list).Scroller;
        // 120 is inside both: as one range 0…300, the nearer end is 0 (not 144, which is inside 100…300).
        scroller.SetOffset(0, 120);
        scroller.Snap(Point.Zero);
        Settle(surface, 10);
        Assert.Equal(0, scroller.Y, 0.5);
    }

    [Fact]
    public void UnderReducedMotionAWhileScrollingRevealComesBackAtOnceAtRest()
    {
        using var _ = new ReducedMotion();
        var (list, box, root) = Page();
        var reveal = new SkUiScrollReveal { Response = SkUiScrollRevealResponse.WhileScrolling, Source = list, Distance = 40, Duration = TimeSpan.FromSeconds(1) };
        box.AddScrollEffect(SkUiScrollEffect.HideOnScroll(reveal, 50));
        using var surface = new SkUiTestSurface(root, 140, 100);
        list.ScrollTo(0, 400);
        surface.Frame(0);
        var scroller = ((ISkUiScrollHost)list).Scroller;
        scroller.Wheel(0, -60); // hides it smoothly, in step with the scroll
        surface.Frame(16);
        surface.Frame(32);
        Assert.False(reveal.IsShown);
        scroller.OnWheelRest();
        surface.Frame(48); // the request
        surface.Frame(64); // a 1 s show, finished at once
        Assert.True(reveal.IsShown);
        Assert.Equal(0, Props(box).LinkValues.TranslationY, Tolerance);
    }

    #endregion
}
