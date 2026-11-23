using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using Microsoft.Maui.Layouts;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.16 A5: scroll-direction reveals (<see cref="SkUiRenderReveal"/>) on the render thread: tracked and triggered
/// responses, the start band, corrections, jumps and overscroll left out, settle / show / hide requests, and a node drawn
/// from a reveal with its frames and its report to the UI side.
/// </summary>
public class ScrollRevealTests
{
    private const float Tolerance = 1e-4f;

    /// <summary>A bare scroller node (100 DIP viewport) and a reveal on it; <see cref="Scroll"/> moves its offset.</summary>
    private sealed class Rig
    {
        private TimeSpan _now;

        public Rig(SkUiRenderRevealSettings settings, float offset = 200)
        {
            Node.Props = SkUiRenderProps.Default with { Width = 100, Height = 100, ChildrenClipRect = new SKRect(0, 0, 100, 100), ChildrenOffsetY = offset };
            Reveal = new SkUiRenderReveal(Node, horizontal: false) { Settings = settings, MaxOffset = 1000 };
            Advance(0);
        }

        public SkUiRenderNode Node { get; } = new();

        public SkUiRenderReveal Reveal { get; }

        public bool Ended { get; private set; }

        /// <summary>Moves the offset by <paramref name="delta"/> and draws a frame <paramref name="ms"/> later.</summary>
        public float Scroll(float delta, double ms = 16)
        {
            Node.Props.ChildrenOffsetY += delta;
            return Advance(ms);
        }

        /// <summary>Scrolls <paramref name="total"/> DIPs in steps of 10, a frame each.</summary>
        public float ScrollBy(float total)
        {
            var steps = (int)Math.Abs(total / 10);
            var progress = Reveal.Progress;
            for (var step = 0; step < steps; step++)
                progress = Scroll(Math.Sign(total) * 10);
            return progress;
        }

        public float Advance(double ms)
        {
            _now += TimeSpan.FromMilliseconds(ms);
            var progress = Reveal.Advance(_now, out var ended);
            Ended = ended;
            return progress;
        }
    }

    private static SkUiRenderRevealSettings Tracked(float distance = 50, bool nearStart = true) =>
        new(SkUiRenderRevealResponse.Tracked, distance, 24, 0.2f, nearStart);

    private static SkUiRenderRevealSettings Triggered(float threshold = 24) =>
        new(SkUiRenderRevealResponse.Triggered, 50, threshold, 0.2f, true);

    [Fact]
    public void ATrackedRevealFollowsTheScrollEitherWay()
    {
        var rig = new Rig(Tracked());
        Assert.Equal(0, rig.Reveal.Progress);
        Assert.Equal(0.6f, rig.ScrollBy(30), Tolerance);
        Assert.Equal(1, rig.ScrollBy(100), Tolerance);
        Assert.Equal(0.6f, rig.ScrollBy(-20), Tolerance);
        Assert.Equal(0, rig.ScrollBy(-40), Tolerance);
        Assert.False(rig.Reveal.IsMoving);
    }

    [Fact]
    public void NearTheStartARevealIsCappedSoItIsFullyShownAtTheTop()
    {
        var rig = new Rig(Tracked(), offset: 0);
        // 20 DIPs from the top: at most 20 / 50 hidden, however it got there.
        rig.Reveal.Request(SkUiRenderRevealRequest.Hide);
        rig.ScrollBy(20);
        Assert.Equal(0.4f, rig.Advance(500), Tolerance);
        Assert.False(rig.Reveal.IsMoving); // the hide ended where the cap holds it
        Assert.Equal(0, rig.ScrollBy(-20), Tolerance);
    }

    [Fact]
    public void WithoutTheStartBandAHideAtTheTopHides()
    {
        var rig = new Rig(Tracked(nearStart: false), offset: 0);
        rig.Reveal.Request(SkUiRenderRevealRequest.Hide);
        Assert.Equal(1, rig.Advance(300), Tolerance);
    }

    [Fact]
    public void ACorrectionIsNotAScroll()
    {
        var rig = new Rig(Tracked());
        // 500 DIPs of rows inserted above: the offset moves with them, the list does not.
        rig.Node.ScrollCorrection += new SKPoint(0, 500);
        rig.Node.Props.ChildrenOffsetY += 500;
        Assert.Equal(0, rig.Advance(16), Tolerance);
        Assert.Equal(0.2f, rig.ScrollBy(10), Tolerance);
    }

    [Fact]
    public void AJumpOfMoreThanTwoViewportsShowsIt()
    {
        var rig = new Rig(Tracked());
        rig.ScrollBy(100);
        Assert.Equal(1, rig.Reveal.Progress, Tolerance);
        Assert.Equal(0, rig.Scroll(250), Tolerance);
    }

    [Fact]
    public void OverscrollDoesNotMoveIt()
    {
        var rig = new Rig(Tracked(), offset: 980);
        Assert.Equal(0.4f, rig.ScrollBy(20), Tolerance); // to the end (1,000)
        rig.Scroll(40); // a bounce past the end
        Assert.Equal(0.4f, rig.Scroll(-40), Tolerance);
    }

    [Fact]
    public void ASettleMovesATrackedRevealToTheNearerEnd()
    {
        var rig = new Rig(Tracked());
        rig.ScrollBy(30); // 0.6
        rig.Reveal.Request(SkUiRenderRevealRequest.Settle);
        Assert.Equal(0.6f, rig.Advance(0), Tolerance);
        Assert.True(rig.Reveal.IsMoving);
        Assert.Equal(0.85f, rig.Advance(50), Tolerance); // 0.25 per 50 ms: a full move takes 0.2 s
        Assert.Equal(1, rig.Advance(100), Tolerance);
        Assert.True(rig.Ended);
        Assert.False(rig.Reveal.IsMoving);
        // A request is acted on once.
        rig.ScrollBy(-30);
        Assert.Equal(0.4f, rig.Advance(100), Tolerance);
    }

    [Fact]
    public void AScrollTakesOverFromASettle()
    {
        var rig = new Rig(Tracked());
        rig.ScrollBy(20); // 0.4
        rig.Reveal.Request(SkUiRenderRevealRequest.Settle);
        rig.Advance(20);
        Assert.True(rig.Reveal.IsMoving);
        var before = rig.Reveal.Progress;
        Assert.Equal(before + 0.2f, rig.Scroll(10), 0.001f);
        Assert.False(rig.Reveal.IsMoving);
    }

    [Fact]
    public void ATriggeredRevealMovesFullyOnceARunPassesTheThreshold()
    {
        var rig = new Rig(Triggered());
        rig.ScrollBy(20);
        Assert.Equal(0, rig.Reveal.Progress);
        Assert.False(rig.Reveal.IsMoving);
        rig.Scroll(10, ms: 0); // a run of 30: past 24
        Assert.True(rig.Reveal.IsMoving);
        // The move runs its duration whatever the scroll does (here, nothing).
        Assert.Equal(0.5f, rig.Advance(100), Tolerance);
        Assert.Equal(1, rig.Advance(100), Tolerance);
        Assert.True(rig.Ended);
        // Back: a run of 30 the other way shows it.
        rig.ScrollBy(-30);
        Assert.True(rig.Reveal.IsMoving);
        rig.Advance(300);
        Assert.Equal(0, rig.Reveal.Progress, Tolerance);
    }

    [Fact]
    public void ATurnStartsANewRun()
    {
        var rig = new Rig(Triggered());
        rig.ScrollBy(20);
        rig.ScrollBy(-10);
        rig.ScrollBy(20);
        Assert.False(rig.Reveal.IsMoving); // runs of 20, 10 and 20: none reached 24
    }

    [Fact]
    public void ShowAndHideAreAnimated()
    {
        var rig = new Rig(Triggered());
        rig.Reveal.Request(SkUiRenderRevealRequest.Hide);
        Assert.Equal(0.5f, rig.Advance(100), Tolerance);
        Assert.Equal(1, rig.Advance(100), Tolerance);
        rig.Reveal.Request(SkUiRenderRevealRequest.Show);
        Assert.Equal(0.5f, rig.Advance(100), Tolerance);
        Assert.Equal(0, rig.Advance(100), Tolerance);
        Assert.True(rig.Ended);
    }

    [Fact]
    public void ChannelsOnARevealMapItsProgressOntoShownAndHidden()
    {
        var rig = new Rig(Tracked());
        SkUiRenderLink[] links =
        [
            new(rig.Reveal, SkUiRenderLinkTarget.TranslationY, 0, 64),
            new(rig.Reveal, SkUiRenderLinkTarget.Opacity, 1, 0)
        ];
        rig.ScrollBy(20); // 0.4
        var ui = SkUiRenderLinkValues.Evaluate(links, 0, 9999); // the offset is not what drives them
        Assert.Equal(25.6f, ui.TranslationY, Tolerance);
        Assert.Equal(0.6f, ui.Opacity, Tolerance);
        var moving = false;
        var ended = new List<SkUiRenderReveal>();
        Assert.Equal(ui, SkUiRenderLinkValues.Evaluate(links, SkUiRenderLinkValues.Identity, TimeSpan.FromSeconds(10), ended, ref moving));
    }

    /// <summary>A box over a list, hidden downward by a triggered reveal on the list (a floating button).</summary>
    private sealed class RevealedBox(SkUiScrollView scroll) : SkUiCoreBox
    {
        private SkUiRenderLink[]? _links;

        public SkUiRenderReveal? Reveal { get; private set; }

        internal override void OnGetRenderProps(ref SkUiRenderProps props)
        {
            base.OnGetRenderProps(ref props);
            if (_links is null)
            {
                Reveal = new SkUiRenderReveal(((ISkUiRenderable)scroll).RenderState.Node, horizontal: false) { Settings = Triggered(), MaxOffset = 3900 };
                _links = [new SkUiRenderLink(Reveal, SkUiRenderLinkTarget.TranslationY, 0, 60)];
            }
            props.Links = _links;
            var input = ((ISkUiScrollHost)scroll).Scroller.LinkInput;
            props.LinkValues = SkUiRenderLinkValues.Evaluate(_links, input.X, input.Y);
        }
    }

    [Fact]
    public void ARevealedNodeIsDrawnMovingWhileItsMoveRunsAndTheUiSideHearsWhenItEnds()
    {
        var scroll = new SkUiScrollView { Content = new SkUiBox { Color = Colors.White, HeightRequest = 4000 } };
        var box = new RevealedBox(scroll);
        box.SetColor(Colors.Red).SetWidth(20).SetHeight(20).SetHorizontalAlignment(LayoutAlignment.Start).SetVerticalAlignment(LayoutAlignment.End);
        var root = new SkUiGrid { ColumnDefinitions = [new ColumnDefinition(40), new ColumnDefinition(100)] };
        root.Children.Add(new SkUiCoreHost().SetContent(box));
        Grid.SetColumn(scroll, 1);
        root.Children.Add(scroll);
        using var surface = new SkUiTestSurface(root, 140, 100);
        scroll.ScrollTo(0, 200); // away from the start band
        Assert.True(surface.Frame(0).GetPixel(10, 90).Red > 200);
        var ended = 0;
        box.Reveal!.MoveEnded += _ => ended++;
        var recorded = surface.RecordedPictures;

        // A run of 30 down: the button starts to leave and keeps going with no more scrolling.
        for (var step = 1; step <= 3; step++)
        {
            scroll.ScrollTo(0, 200 + step * 10);
            surface.Frame(step * 16);
        }
        Assert.True(surface.NeedsFrame);
        surface.Frame(150);
        var progress = box.Reveal.Progress;
        Assert.InRange(progress, 0.1f, 0.99f);
        surface.Frame(500);
        Assert.Equal(1, box.Reveal.Progress, Tolerance);
        Assert.Equal(1, ended);
        // Once the scroll bar has faded too, nothing asks for frames: a reveal at rest does not.
        surface.Frame(5000);
        surface.Frame(5100);
        Assert.False(surface.NeedsFrame);
        Assert.Equal(1, ended);
        // Gone below the surface; the UI side places it where it is drawn, and nothing was recorded again.
        Assert.Equal(0, surface.Bitmap.GetPixel(10, 90).Alpha);
        var props = SkUiRenderProps.Default;
        ((ISkUiRenderable)box).GetRenderProps(ref props);
        Assert.Equal(60, props.EffectiveTranslationY, Tolerance);
        Assert.Equal(recorded, surface.RecordedPictures);
    }
}
