using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Pointer events through the gesture arena allocate nothing once warm (NFR-2), and handlers can change the arena mid-loop.</summary>
[Collection(GlobalStateCollection.Name)]
public class GestureAllocationTests
{
    /// <summary>Tracks every pointer it is offered and never claims it, so the arena keeps both members.</summary>
    private sealed class Watcher : SkUiGestureRecognizer
    {
        public int Moves { get; private set; }
        public int Releases { get; private set; }
        public bool CancelOnMove { get; set; }

        protected internal override bool OnPointerPressed(SkUiPointer pointer) => true;

        protected internal override void OnPointerMoved(SkUiPointer pointer)
        {
            Moves++;
            if (CancelOnMove)
                Cancel();
        }

        protected internal override void OnPointerReleased(SkUiPointer pointer) => Releases++;
    }

    /// <summary>Counts the times a hovering pointer leaves it.</summary>
    private sealed class HoverBox : SkUiCoreBox
    {
        public bool IsOver { get; private set; }
        public int Left { get; private set; }

        internal override void OnPointerOverChanged(bool isOver)
        {
            if (IsOver && !isOver)
                Left++;
            IsOver = isOver;
        }
    }

    /// <summary>A drawn root hosting <paramref name="content"/> in its top-left 200 × 200 (a 100 × 100 box in it is centered: 50 to 150).</summary>
    private static SkUiVerticalStackLayout Root(SkUiCoreNode content)
    {
        var root = new SkUiVerticalStackLayout();
        root.Children.Add(new SkUiCoreHost { WidthRequest = 200, HeightRequest = 200, HorizontalOptions = LayoutOptions.Start }.SetContent(content));
        SkUiTestHelpers.Arrange(root, 300, 300);
        return root;
    }

    /// <summary>The fewest bytes of three batches of 100 calls, after a warm-up (the runtime's occasional work does not show in every batch).</summary>
    private static long FewestBytes(Action<int> step)
    {
        for (var round = 0; round < 20; round++) step(round);
        var fewest = long.MaxValue;
        for (var batch = 0; batch < 3 && fewest > 0; batch++)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var round = 0; round < 100; round++) step(round);
            fewest = Math.Min(fewest, GC.GetAllocatedBytesForCurrentThread() - allocated);
        }
        return fewest;
    }

    [Fact]
    public void MovesOfAPressedPointerAllocateNothing()
    {
        var outer = new Watcher();
        var inner = new Watcher();
        var box = new SkUiCoreBox().SetWidth(100).SetHeight(100).SetBackground(Colors.Red).AddGestureRecognizer(inner);
        var host = Root(new SkUiCoreContentView().SetContent(box).AddGestureRecognizer(outer));
        host.Touch(new(1, SkUiTouchAction.Pressed, new Point(60, 60), TimeSpan.Zero));

        var bytes = FewestBytes(round => host.Touch(new(1, SkUiTouchAction.Moved, new Point(60 + round % 2, 60), TimeSpan.FromMilliseconds(10 + round))));

        Assert.Equal(0, bytes);
        Assert.True(inner.Moves >= 120 && outer.Moves >= 120); // the warm-up and at least one batch
        host.Touch(new(1, SkUiTouchAction.Released, new Point(60, 60), TimeSpan.FromSeconds(10)));
        Assert.False(host.Router.HasActiveArenas);
    }

    [Fact]
    public void HoverMovesBetweenTwoNodesAllocateNothing()
    {
        var box = new HoverBox();
        box.SetWidth(100).SetHeight(100).SetBackground(Colors.Red);
        var panel = new SkUiCoreContentView().SetContent(box).SetBackground(Colors.Blue);
        var host = Root(panel);

        // Between the box and the panel beside it: the box is entered and left, the drawn views above stay hovered (MAUI
        // allocates the PropertyChanged args of their IsPointerOver, outside the router).
        var bytes = FewestBytes(round => host.Touch(new(0, SkUiTouchAction.HoverMoved, round % 2 == 0 ? new Point(60, 60) : new Point(10, 10),
            TimeSpan.FromMilliseconds(round))));

        Assert.Equal(0, bytes);
        Assert.True(box.Left >= 60); // left on every odd move: 10 in the warm-up, 50 per batch
        host.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(60, 60), TimeSpan.FromSeconds(10)));
        Assert.True(box.IsOver);
        host.Touch(new(0, SkUiTouchAction.HoverExited, new Point(60, 60), TimeSpan.FromSeconds(11)));
        Assert.False(box.IsOver);
    }

    [Fact]
    public void ARecognizerCancellingItsArenaWhileMovingLeavesTheRouterConsistent()
    {
        var quitter = new Watcher { CancelOnMove = true };
        var stayer = new Watcher();
        var box = new SkUiCoreBox().SetWidth(100).SetHeight(100).SetBackground(Colors.Red).AddGestureRecognizer(quitter);
        var host = Root(new SkUiCoreContentView().SetContent(box).AddGestureRecognizer(stayer));

        host.Touch(new(1, SkUiTouchAction.Pressed, new Point(60, 60), TimeSpan.Zero));
        host.Touch(new(1, SkUiTouchAction.Moved, new Point(61, 60), TimeSpan.FromMilliseconds(10)));
        host.Touch(new(1, SkUiTouchAction.Moved, new Point(62, 60), TimeSpan.FromMilliseconds(20)));
        host.Touch(new(1, SkUiTouchAction.Released, new Point(62, 60), TimeSpan.FromMilliseconds(30)));

        Assert.Equal(1, quitter.Moves); // left the arena on its first move
        Assert.False(quitter.IsTracking);
        Assert.Equal(2, stayer.Moves); // still saw the move the quitter left in, and the next one
        Assert.Equal(1, stayer.Releases);
        Assert.False(stayer.IsTracking);
        Assert.False(host.Router.HasActiveArenas);

        // The next press starts from a clean router.
        quitter.CancelOnMove = false;
        host.Touch(new(2, SkUiTouchAction.Pressed, new Point(60, 60), TimeSpan.FromSeconds(1)));
        host.Touch(new(2, SkUiTouchAction.Moved, new Point(61, 60), TimeSpan.FromSeconds(1.01)));
        Assert.Equal(2, quitter.Moves);
        host.Touch(new(2, SkUiTouchAction.Released, new Point(61, 60), TimeSpan.FromSeconds(1.02)));
        Assert.False(host.Router.HasActiveArenas);
    }
}
