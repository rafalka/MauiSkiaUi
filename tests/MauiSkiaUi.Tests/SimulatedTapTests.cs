using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiDiagnostics.SimulateTapAt"/>: what took a synthetic tap, and cancelling one that is not a tap.</summary>
public class SimulatedTapTests
{
    private static SkUiContentView Surface(SkUiView content, double width = 100, double height = 100)
    {
        var root = new SkUiContentView { Content = content };
        SkUiTestHelpers.Arrange(root, width, height);
        return root;
    }

    private static SkUiScrollView ScrollerWithLabel(out SkUiLabel label)
    {
        label = new SkUiLabel { Text = "row", HeightRequest = 40 };
        var stack = new SkUiVerticalStackLayout { label, new SkUiLabel { HeightRequest = 400 } };
        return new SkUiScrollView { Content = stack };
    }

    /// <summary>Sets which native tap rule <c>PassedToNative</c> follows (Apple's tap gate, or Android / Windows) for one test.</summary>
    private static IDisposable NativeTapGate(bool on)
    {
        var old = SkUiDiagnostics.NativeTapGate;
        SkUiDiagnostics.NativeTapGate = on;
        return new Restore(() => SkUiDiagnostics.NativeTapGate = old);
    }

    private sealed class Restore(Action action) : IDisposable
    {
        public void Dispose() => action();
    }

    /// <summary>A continuous recognizer that records how each pointer ended.</summary>
    private sealed class Recorder : SkUiGestureRecognizer
    {
        public List<string> Seen { get; } = [];

        protected internal override void OnPointerReleased(SkUiPointer pointer) => Seen.Add("released");

        protected internal override void OnRejected(long pointerId) => Seen.Add("rejected");
    }

    [Fact]
    public void ATapOnAButtonIsTakenByItsTap()
    {
        var button = new SkUiButton { Text = "OK" };
        var root = Surface(button);
        var clicks = 0;
        button.Clicked += (_, _) => clicks++;

        var result = SkUiDiagnostics.SimulateTap(button, releaseOnlyIfTap: true);

        Assert.Same(button, result.Hit);
        Assert.NotNull(result.Winner);
        Assert.Same(button, result.Winner!.Owner);
        Assert.True(result.IsTap);
        Assert.True(result.Released);
        Assert.False(result.PassedToNative);
        Assert.Equal(1, clicks);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ALabelInAScrollerIsCancelledWithoutSideEffects(bool tapGate)
    {
        using var _ = NativeTapGate(tapGate);
        var scroll = ScrollerWithLabel(out var label);
        var root = Surface(scroll);

        var result = SkUiDiagnostics.SimulateTap(label, releaseOnlyIfTap: true);

        Assert.Same(label, result.Hit);
        Assert.NotNull(result.Winner);
        Assert.Same(scroll, result.Winner!.Owner);
        Assert.False(result.IsTap);
        Assert.False(result.Released);
        // Apple: nothing drawn holds native taps back. Android / Windows: the surface handled the press (the scroller took it).
        Assert.Equal(tapGate, result.PassedToNative);
        Assert.False(result.Winner.IsTracking);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Fact]
    public void WithoutReleaseOnlyIfTapTheScrollerPressIsReleased()
    {
        using var _ = NativeTapGate(true);
        var root = Surface(ScrollerWithLabel(out var label));

        var result = SkUiDiagnostics.SimulateTap(label, releaseOnlyIfTap: false);

        Assert.Null(result.Winner); // released without moving, the scroller resigns: nothing won
        Assert.False(result.IsTap);
        Assert.True(result.PassedToNative);
        Assert.True(result.Released);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Fact]
    public void ATapInsideAPanReachesTheTappableParent()
    {
        var child = new SkUiContentView { WidthRequest = 50, HeightRequest = 50 };
        child.PanUpdated += (_, _) => { };
        var parent = new SkUiContentView { Content = child };
        var taps = 0;
        parent.Tapped += (_, _) => taps++;
        Surface(parent);

        // The pan is the innermost member, but it resigns a release that did not move: the parent's tap takes it.
        var result = SkUiDiagnostics.SimulateTap(child, releaseOnlyIfTap: true);

        Assert.True(result.Released);
        Assert.True(result.IsTap);
        Assert.Same(parent, result.Winner?.Owner);
        Assert.Equal(1, taps);
    }

    [Fact]
    public void ATapOnAScrollerInsideATappableViewIsTaken()
    {
        var card = new SkUiContentView { Content = ScrollerWithLabel(out var label) };
        var taps = 0;
        card.Tapped += (_, _) => taps++;
        Surface(card);

        var result = SkUiDiagnostics.SimulateTap(label, releaseOnlyIfTap: true);

        Assert.True(result.Released);
        Assert.True(result.IsTap);
        Assert.Same(card, result.Winner?.Owner);
        Assert.Equal(1, taps);
    }

    [Fact]
    public void AnObservingPointerRecognizerKeepsThePressFromTheParentsTap()
    {
        var child = new SkUiContentView { WidthRequest = 50, HeightRequest = 50 };
        var events = new List<SkUiTouchAction>();
        var observer = new SkUiPointerGestureRecognizer { ClaimOnPress = false };
        observer.Pointer += (_, args) => events.Add(args.Action);
        child.Gestures.Add(observer);
        var parent = new SkUiContentView { Content = child };
        var taps = 0;
        parent.Tapped += (_, _) => taps++;
        Surface(parent);

        // The observer stays in the arena on a still release and wins the sweep: not a tap, so it is cancelled.
        var result = SkUiDiagnostics.SimulateTap(child, releaseOnlyIfTap: true);

        Assert.False(result.Released);
        Assert.False(result.IsTap);
        Assert.Same(observer, result.Winner);
        Assert.Equal(0, taps);
        Assert.DoesNotContain(SkUiTouchAction.Released, events);
    }

    [Fact]
    public void PassiveContentDrawnOverTheElementLetsTheTapThrough()
    {
        var border = new SkUiBorder();
        var taps = 0;
        border.Tapped += (_, _) => taps++;
        var label = new SkUiLabel { Text = "Buy" }; // a sibling drawn over the border, with no recognizers
        Surface(new SkUiGrid { Children = { border, label } });

        var result = SkUiDiagnostics.SimulateTap(border, releaseOnlyIfTap: true);

        Assert.Same(label, result.Hit);
        Assert.True(result.Released);
        Assert.True(result.IsTap);
        Assert.Equal(1, taps);
    }

    [Fact]
    public void AForcedPressAnotherElementsRecognizerWouldTakeIsCancelled()
    {
        var target = new SkUiButton { Text = "Row" };
        var cover = new SkUiContentView();
        var events = new List<SkUiTouchAction>();
        var pointer = new SkUiPointerGestureRecognizer(); // claims on press: the arena's winner, not a tap
        pointer.Pointer += (_, args) => events.Add(args.Action);
        cover.Gestures.Add(pointer);
        var root = Surface(new SkUiGrid { Children = { target, cover } });

        var result = SkUiDiagnostics.SimulateTap(target, releaseOnlyIfTap: false); // released on request, but not into another element

        Assert.Same(pointer, result.Winner);
        Assert.False(result.Released);
        Assert.False(result.IsTap);
        Assert.DoesNotContain(SkUiTouchAction.Released, events);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void APressAnotherElementsTapWouldTakeIsCancelled(bool releaseOnlyIfTap)
    {
        SkUiButton target = new() { Text = "Row" }, cover = new() { Text = "Delete" };
        int targetClicks = 0, coverClicks = 0;
        target.Clicked += (_, _) => targetClicks++;
        cover.Clicked += (_, _) => coverClicks++;
        var root = Surface(new SkUiGrid { Children = { target, cover } });

        var result = SkUiDiagnostics.SimulateTap(target, releaseOnlyIfTap);

        Assert.Same(cover, result.Hit);
        Assert.Same(cover, result.Winner?.Owner);
        Assert.False(result.Released);
        Assert.False(result.IsTap);
        Assert.Equal(0, coverClicks);
        Assert.Equal(0, targetClicks);
        Assert.Equal(0, root.Router.ActiveArenaCount);

        Assert.Same(cover, SkUiDiagnostics.SimulateTap(target)); // the 1.x overload still taps whatever covers the center
        Assert.Equal(1, coverClicks);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ADisabledButtonTakesNoTap(bool tapGate)
    {
        using var _ = NativeTapGate(tapGate);
        var button = new SkUiButton { Text = "OK", IsEnabled = false };
        var root = Surface(button);
        var clicks = 0;
        button.Clicked += (_, _) => clicks++;

        var result = SkUiDiagnostics.SimulateTapAt(root, new Point(50, 50));

        Assert.NotNull(result.Hit);
        Assert.Null(result.Winner);
        Assert.False(result.IsTap);
        Assert.False(result.Released);
        Assert.Equal(tapGate, result.PassedToNative); // the disabled control takes the press, without a tap
        Assert.Equal(0, clicks);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PlainContentPassesTheTapToNativeAncestors(bool tapGate)
    {
        using var _ = NativeTapGate(tapGate);
        var label = new SkUiLabel { Text = "plain" };
        var root = Surface(label);

        var result = SkUiDiagnostics.SimulateTap(label, releaseOnlyIfTap: true);

        Assert.NotNull(result.Hit);
        Assert.Null(result.Winner);
        Assert.False(result.IsTap);
        Assert.True(result.PassedToNative);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Fact]
    public void ANativeViewOnTheSurfaceIsNotPressed()
    {
        var overlay = new SkUiMauiContentView { Content = new Editor(), WidthRequest = 50, HeightRequest = 50 };
        var row = new SkUiContentView { Content = overlay };
        var taps = 0;
        row.Tapped += (_, _) => taps++;
        var root = Surface(row);

        var result = SkUiDiagnostics.SimulateTap(overlay, releaseOnlyIfTap: false);

        Assert.Same(overlay, result.Hit);
        Assert.True(result.PassedToNative); // a real finger lands on the native editor
        Assert.False(result.Released);
        Assert.Null(result.Winner);
        Assert.Equal(0, taps);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    /// <summary>What a tap handler does to end its own press (issue #95).</summary>
    public enum EndsPress
    {
        CancelsThePointers, // a popup or navigation cancelling the surface's touches
        DisablesItself,
        LeavesTheTree
    }

    private static void EndPress(EndsPress how, SkUiContentView root, SkUiView button, SkUiLayout parent)
    {
        switch (how)
        {
            case EndsPress.CancelsThePointers:
                root.Router.CancelAll();
                break;
            case EndsPress.DisablesItself:
                button.IsEnabled = false;
                break;
            case EndsPress.LeavesTheTree:
                parent.Remove(button);
                break;
        }
    }

    [Theory]
    [InlineData(EndsPress.CancelsThePointers)]
    [InlineData(EndsPress.DisablesItself)]
    [InlineData(EndsPress.LeavesTheTree)]
    public void ATapWhoseHandlerEndsThePressIsStillReportedAsTaken(EndsPress how)
    {
        var button = new SkUiButton { Text = "Open" };
        var stack = new SkUiVerticalStackLayout { button };
        var root = Surface(stack);
        var clicks = 0;
        button.Clicked += (_, _) =>
        {
            clicks++;
            EndPress(how, root, button, stack);
        };

        var result = SkUiDiagnostics.SimulateTapAt(root, new Point(10, 10));

        Assert.Equal(1, clicks);
        Assert.True(result.Released);
        Assert.Same(button, result.Winner?.Owner);
        Assert.True(result.IsTap);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Theory]
    [InlineData(EndsPress.CancelsThePointers)]
    [InlineData(EndsPress.DisablesItself)]
    [InlineData(EndsPress.LeavesTheTree)]
    public void ATapThatWinsOnReleaseAndEndsThePressIsStillReportedAsTaken(EndsPress how)
    {
        // Inside a scroller the button wins only when the release makes the scroller resign: its tap runs from OnAccepted.
        var button = new SkUiButton { Text = "Open", HeightRequest = 40 };
        var stack = new SkUiVerticalStackLayout { button, new SkUiLabel { HeightRequest = 400 } };
        var root = Surface(new SkUiScrollView { Content = stack });
        var clicks = 0;
        button.Clicked += (_, _) =>
        {
            clicks++;
            EndPress(how, root, button, stack);
        };

        var result = SkUiDiagnostics.SimulateTap(button, releaseOnlyIfTap: true);

        Assert.Equal(1, clicks);
        Assert.True(result.Released);
        Assert.Same(button, result.Winner?.Owner);
        Assert.True(result.IsTap);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Theory]
    [InlineData(EndsPress.CancelsThePointers)]
    [InlineData(EndsPress.DisablesItself)]
    [InlineData(EndsPress.LeavesTheTree)]
    public void ACoreTapWhoseHandlerEndsThePressIsStillReportedAsTaken(EndsPress how)
    {
        var button = new SkUiCoreButton { Text = "Open" };
        var stack = new SkUiCoreVerticalStackLayout();
        stack.Add(button);
        var root = Surface(new SkUiCoreHost().SetContent(stack));
        var clicks = 0;
        button.Clicked += (_, _) =>
        {
            clicks++;
            switch (how)
            {
                case EndsPress.CancelsThePointers:
                    root.Router.CancelAll();
                    break;
                case EndsPress.DisablesItself:
                    button.IsEnabled = false;
                    break;
                case EndsPress.LeavesTheTree:
                    stack.Remove(button);
                    break;
            }
        };

        var result = SkUiDiagnostics.SimulateTapAt(root, new Point(10, 10));

        Assert.Equal(1, clicks);
        Assert.True(result.Released);
        Assert.Same(button, result.Winner?.Owner);
        Assert.True(result.IsTap);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Fact]
    public void AButtonDisabledBeforeTheReleaseDoesNotTakeIt()
    {
        var button = new SkUiButton { Text = "OK" };
        var root = Surface(button);
        var clicks = 0;
        button.Clicked += (_, _) => clicks++;
        const long id = 7;

        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Pressed, new Point(10, 10)));
        var arena = root.Router.ArenaOf(id)!;
        button.IsEnabled = false; // not by the release: the press is cancelled from outside
        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Released, new Point(10, 10)));

        Assert.Null(arena.EndWinner);
        Assert.Equal(0, clicks);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }

    [Fact]
    public void OutsideTheSurfaceNothingIsPressed()
    {
        var root = Surface(new SkUiButton { Text = "OK" });

        Assert.Equal(default, SkUiDiagnostics.SimulateTapAt(root, new Point(500, 500)));
        Assert.Equal(default, SkUiDiagnostics.SimulateTap(new SkUiButton(), releaseOnlyIfTap: true));
    }

    [Fact]
    public void ANestedViewIsNotASurfaceRoot()
    {
        var button = new SkUiButton { Text = "OK" };
        Surface(button);

        Assert.Throws<ArgumentException>(() => SkUiDiagnostics.SimulateTapAt(button, new Point(5, 5)));
    }

    [Fact]
    public void TheElementOverloadStillReturnsTheHitAndAlwaysReleases()
    {
        var box = new SkUiContentView { WidthRequest = 50, HeightRequest = 50 };
        var recorder = new Recorder();
        box.Gestures.Add(recorder);
        var root = Surface(box);

        Assert.Same(box, SkUiDiagnostics.SimulateTap(box));
        Assert.Equal(["released"], recorder.Seen);

        recorder.Seen.Clear();
        Assert.False(SkUiDiagnostics.SimulateTap(box, releaseOnlyIfTap: true).Released); // not a tap: cancelled
        Assert.Equal(["rejected"], recorder.Seen);
        Assert.Equal(0, root.Router.ActiveArenaCount);
    }
}
