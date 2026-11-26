using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.18 A2 (issue #133): <c>PressedScale</c> on views and Core nodes, and the look's <see cref="SkUiLook.PressedScale"/>: a
/// pressed view shrinks about its anchor with the look's press and release transitions, composited (no re-record), on top of
/// any overlay or fade, also without <c>ShowsPressEffect</c>; a press that becomes a scroll springs back; reduced motion shows none.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PressScaleTests
{
    private static long _pointer = 90_000;

    private sealed class HalfScaleLook : DefaultSkUiLook
    {
        public override double PressedScale => 0.5;
    }

    private static void WithLook(SkUiLook look, Action body, bool reduceMotion = false)
    {
        var previous = SkUiLook.Current;
        var reduce = SkUiMotion.ReduceMotion;
        try
        {
            SkUiLook.Current = look;
            SkUiMotion.ReduceMotion = reduceMotion;
            body();
        }
        finally
        {
            SkUiLook.Current = previous;
            SkUiMotion.ReduceMotion = reduce;
        }
    }

    private static void At(SkUiTestSurface surface, double milliseconds)
    {
        surface.Root.AnimationClock.Tick(TimeSpan.FromMilliseconds(milliseconds));
        surface.Frame(milliseconds);
    }

    /// <summary>A black 40 × 40 card centred in a white 100 × 100 root.</summary>
    private static (SkUiContentView Root, SkUiBorder Card) Card(double pressedScale = -1, bool showsPressEffect = false)
    {
        var card = new SkUiBorder
        {
            CornerRadius = 0, StrokeThickness = 0, BackgroundColor = Colors.Black, WidthRequest = 40, HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, PressedScale = pressedScale,
            ShowsPressEffect = showsPressEffect
        };
        card.Tapped += (_, _) => { };
        return (new SkUiContentView { BackgroundColor = Colors.White, Content = card }, card);
    }

    private static SkUiRenderProps Props(ISkUiRenderable node)
    {
        var props = SkUiRenderProps.Default;
        node.GetRenderProps(ref props);
        return props;
    }

    private static bool IsBlack(SkUiTestSurface surface, int x, int y) => surface.Bitmap.GetPixel(x, y).Red < 60;

    /// <summary>Presses the card's centre, checks it has shrunk to half (an edge pixel white, the centre black) with no re-record, releases.</summary>
    private static void AssertHalvesOnPress(SkUiView root, SkUiTestSurface surface)
    {
        surface.Frame();
        Assert.True(IsBlack(surface, 33, 50)); // inside the card, outside its half
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, new Point(50, 50), TimeSpan.FromSeconds(1)));
        surface.Frame(0);
        var recorded = surface.RecordedPictures;
        At(surface, 400);
        Assert.False(IsBlack(surface, 33, 50));
        Assert.True(IsBlack(surface, 50, 50));
        Assert.True(IsBlack(surface, 42, 50));
        Assert.Equal(recorded, surface.RecordedPictures);

        root.Touch(new(id, SkUiTouchAction.Released, new Point(50, 50), TimeSpan.FromSeconds(1.5)));
        surface.Frame(400);
        recorded = surface.RecordedPictures;
        At(surface, 1200);
        Assert.True(IsBlack(surface, 33, 50));
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    [Fact]
    public void APressedViewShrinksAboutItsCentreWithoutReRecording()
    {
        WithLook(new DefaultSkUiLook(), () =>
        {
            var (root, card) = Card(pressedScale: 0.5);
            using var surface = new SkUiTestSurface(root, 100, 100);
            AssertHalvesOnPress(root, surface);
            Assert.Equal(0.5, card.PressedScale);
        });
    }

    [Fact]
    public void ItCombinesWithTheOverlayAndTheFade()
    {
        WithLook(new DefaultSkUiLook(), () =>
        {
            var (root, card) = Card(pressedScale: 0.5, showsPressEffect: true);
            card.PressFeedback = SkUiPressFeedback.Fade;
            using var surface = new SkUiTestSurface(root, 100, 100);
            surface.Frame();
            root.Touch(new(++_pointer, SkUiTouchAction.Pressed, new Point(50, 50), TimeSpan.FromSeconds(1)));
            At(surface, 400);
            Assert.Equal(SKColors.White, surface.Bitmap.GetPixel(33, 50));
            var centre = surface.Bitmap.GetPixel(50, 50).Red;
            Assert.InRange(centre, 150, 156); // faded to the look's 0.4 as well
        });
    }

    [Fact]
    public void TheLookDefaultScalesButtonsAndTappedNodesAndAViewCanOptOut()
    {
        WithLook(new HalfScaleLook(), () =>
        {
            var (root, card) = Card();
            using var surface = new SkUiTestSurface(root, 100, 100);
            AssertHalvesOnPress(root, surface);

            card.PressedScale = 1;
            root.Touch(new(++_pointer, SkUiTouchAction.Pressed, new Point(50, 50), TimeSpan.FromSeconds(3)));
            At(surface, 3000);
            Assert.True(IsBlack(surface, 33, 50));
        });

        WithLook(new HalfScaleLook(), () =>
        {
            var box = new SkUiCoreBox().SetColor(Colors.Black);
            box.SetWidth(40).SetHeight(40).SetHorizontalAlignment(LayoutAlignment.Center).SetVerticalAlignment(LayoutAlignment.Center);
            box.Tapped += (_, _) => { };
            var host = new SkUiCoreHost { BackgroundColor = Colors.White };
            host.SetContent(box);
            using var surface = new SkUiTestSurface(host, 100, 100);
            AssertHalvesOnPress(host, surface);
            Assert.Equal(-1, box.PressedScale);
        });

        WithLook(new HalfScaleLook(), () =>
        {
            var button = new SkUiButton { Text = "", WidthRequest = 40, HeightRequest = 40, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            var root = new SkUiContentView { Content = button };
            using var surface = new SkUiTestSurface(root, 100, 100);
            surface.Frame();
            root.Touch(new(++_pointer, SkUiTouchAction.Pressed, new Point(50, 50), TimeSpan.FromSeconds(1)));
            At(surface, 400);
            Assert.True(button.IsPressed);
            Assert.Equal(0.5f, Props(button).PressScale, 0.001f);
        });
    }

    [Fact]
    public void ACoreNodesOwnScaleAndItsSetterValidate()
    {
        WithLook(new DefaultSkUiLook(), () =>
        {
            var box = new SkUiCoreBox().SetColor(Colors.Black);
            box.SetWidth(40).SetHeight(40).SetHorizontalAlignment(LayoutAlignment.Center).SetVerticalAlignment(LayoutAlignment.Center);
            box.SetPressedScale(0.5);
            box.Tapped += (_, _) => { };
            var host = new SkUiCoreHost { BackgroundColor = Colors.White };
            host.SetContent(box);
            using var surface = new SkUiTestSurface(host, 100, 100);
            AssertHalvesOnPress(host, surface);
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreBox().SetPressedScale(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreBox().SetPressedScale(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiBorder().SetPressedScale(-0.5));
        var view = new SkUiBorder { PressedScale = 0.9 };
        view.PressedScale = 0;
        Assert.Equal(0.9, view.PressedScale);
        Assert.Equal(-1, new SkUiBorder().PressedScale);
        Assert.Equal(1, new DefaultSkUiLook().PressedScale);
    }

    [Fact]
    public void ReducedMotionShowsNoScale()
    {
        WithLook(new DefaultSkUiLook(), () =>
        {
            var (root, card) = Card(pressedScale: 0.5);
            using var surface = new SkUiTestSurface(root, 100, 100);
            surface.Frame();
            root.Touch(new(++_pointer, SkUiTouchAction.Pressed, new Point(50, 50), TimeSpan.FromSeconds(1)));
            At(surface, 400);
            Assert.True(card.IsPressed);
            Assert.True(IsBlack(surface, 33, 50));
        }, reduceMotion: true);
    }

    [Fact]
    public void APressThatBecomesAScrollSpringsBack()
    {
        using var clock = new ManualGestureClock();
        WithLook(new DefaultSkUiLook(), () =>
        {
            var (cardRoot, card) = Card(pressedScale: 0.5);
            cardRoot.Content = null;
            card.VerticalOptions = LayoutOptions.Start;
            card.Margin = new Thickness(0, 30, 0, 0);
            var stack = new SkUiVerticalStackLayout { Children = { card, new SkUiBox { HeightRequest = 1000 } } };
            var scroll = new SkUiScrollView { BackgroundColor = Colors.White, Content = stack };
            using var surface = new SkUiTestSurface(scroll, 100, 100);
            surface.Frame();
            var id = ++_pointer;
            scroll.Touch(new(id, SkUiTouchAction.Pressed, new Point(50, 50), TimeSpan.Zero));
            clock.Advance(SkUiGestureSettings.PressDelay); // a contested press shows after the delay
            At(surface, 100);
            Assert.True(card.IsPressed);
            Assert.True(Props(card).PressScale < 1);
            scroll.Touch(new(id, SkUiTouchAction.Moved, new Point(50, 0), TimeSpan.FromMilliseconds(150)));
            Assert.False(card.IsPressed);
            At(surface, 1500);
            Assert.Equal(1f, Props(card).PressScale);
        });
    }
}
