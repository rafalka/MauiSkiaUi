using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using Microsoft.Maui.Layouts;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.16 phase B: the public <see cref="SkUiScrollEffect"/> and <see cref="SkUiScrollReveal"/> on both layers: validation,
/// factories, finding the scroller (a reference, the nearest ancestor, a collection view's sticky parts, a Core node following a
/// SkUi* list), one view per effect, re-commits on scroll, release, and the reduced-motion rules.
/// </summary>
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
        Assert.Equal(new SKRect(30, 0, 40, 20), clip.Rect); // the right side: leading in right to left
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

    #endregion
}
