using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// C1 <see cref="SkUiSwipeView"/>: MAUI's <c>SwipeView</c> API on drawn content. Revealing, opening and closing by
/// swipes (thresholds, fast swipes, both sides of an axis), item sizes as MAUI's handlers make them, reveal and execute
/// modes, item taps and <see cref="SwipeBehaviorOnInvoked"/>, taps on open content, <see cref="SkUiSwipeView.Open"/> /
/// <see cref="SkUiSwipeView.Close"/>, the arena against scrollers and list taps, closing on scrolls and recycling, drawn
/// item views, and moving without re-recording.
/// </summary>
public class SwipeViewTests
{
    private const string Archive = "Archive";
    private const string Delete = "Delete";

    private static long _pointer = 140_000;

    private sealed class Command(Action<object?> execute, Func<bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => execute(parameter);
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private static SkUiSwipeView Swipe(out SkUiBox content, double height = 60)
    {
        content = new SkUiBox { HeightRequest = height };
        return new SkUiSwipeView { Content = content };
    }

    private static SwipeItems TwoItems(List<string>? log = null)
    {
        var archive = new SwipeItem { Text = Archive, BackgroundColor = Colors.Gray };
        var delete = new SwipeItem { Text = Delete, BackgroundColor = Colors.Red };
        if (log is not null)
        {
            archive.Invoked += (_, _) => log.Add(Archive);
            delete.Invoked += (_, _) => log.Add(Delete);
        }
        return [archive, delete];
    }

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    /// <summary>A slow drag (100 ms per step: no fling) from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static void Drag(SkUiView root, Point from, Point to, bool release = true, int steps = 6, double stepMs = 100)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.FromMilliseconds(0)));
        for (var step = 1; step <= steps; step++)
        {
            var point = new Point(from.X + (to.X - from.X) * step / steps, from.Y + (to.Y - from.Y) * step / steps);
            root.Touch(new(id, SkUiTouchAction.Moved, point, TimeSpan.FromMilliseconds(step * stepMs)));
        }
        if (release)
            root.Touch(new(id, SkUiTouchAction.Released, to, TimeSpan.FromMilliseconds(steps * stepMs + (stepMs >= 100 ? 300 : stepMs))));
    }

    private static SkUiButton ItemButton(SkUiSwipeView swipe, OpenSwipeItem side, int index) =>
        (SkUiButton)((SkUiView)swipe.GetItemsHost(side)!).SkiaChildren.ElementAt(index);

    #region Revealing and opening

    [Fact]
    public void DefaultsMatchMaui()
    {
        var swipe = new SkUiSwipeView();
        var other = new SkUiSwipeView();
        Assert.Equal(0, swipe.Threshold);
        Assert.False(swipe.IsOpen);
        Assert.Null(swipe.Content);
        Assert.Empty(swipe.LeftItems);
        Assert.Empty(swipe.RightItems);
        Assert.Empty(swipe.TopItems);
        Assert.Empty(swipe.BottomItems);
        Assert.NotSame(swipe.RightItems, other.RightItems);
        Assert.Equal(SwipeMode.Reveal, swipe.RightItems.Mode);
        Assert.Equal(SwipeBehaviorOnInvoked.Auto, swipe.RightItems.SwipeBehaviorOnInvoked);
        Assert.True(swipe.ClipToBounds); // as MAUI's platform views
        Assert.Same(swipe, swipe.RightItems.Parent); // logical children, as on MAUI
    }

    [Fact]
    public void ASwipeToTheLeftRevealsTheRightItemsAndAReleasePastTheThresholdOpensThem()
    {
        var log = new List<string>();
        var swipe = Swipe(out _);
        swipe.RightItems = TwoItems();
        swipe.SwipeStarted += (_, args) => log.Add($"started {args.SwipeDirection}");
        swipe.SwipeChanging += (_, args) => log.Add($"changing {args.SwipeDirection} {args.Offset}");
        swipe.SwipeEnded += (_, args) => log.Add($"ended {args.SwipeDirection} {args.IsOpen}");
        SkUiTestHelpers.Arrange(swipe, 300, 60);
        Assert.Null(swipe.GetItemsHost(OpenSwipeItem.RightItems)); // created when first revealed

        Drag(swipe, new Point(250, 30), new Point(100, 30), release: false);

        // The content follows the finger; two 100 DIP items slide in with its right edge.
        Assert.Equal(OpenSwipeItem.RightItems, swipe.ShownSide);
        Assert.Equal(-150, swipe.Offset);
        Assert.Equal(-150, swipe.ContentHost.TranslationX);
        var host = swipe.GetItemsHost(OpenSwipeItem.RightItems)!;
        Assert.Equal(new Rect(100, 0, 200, 60), host.Frame);
        Assert.Equal(50, host.TranslationX);
        // As MAUI's handlers, the first right item is at the outer (right) edge.
        Assert.Equal(new Rect(100, 0, 100, 60), ItemButton(swipe, OpenSwipeItem.RightItems, 0).Frame);
        Assert.Equal(new Rect(0, 0, 100, 60), ItemButton(swipe, OpenSwipeItem.RightItems, 1).Frame);
        Assert.Equal(Archive, ItemButton(swipe, OpenSwipeItem.RightItems, 0).Text);
        Assert.Equal(Colors.Red, ItemButton(swipe, OpenSwipeItem.RightItems, 1).FillColor);
        Assert.Equal(Colors.White, ItemButton(swipe, OpenSwipeItem.RightItems, 1).TextColor); // contrast with red, as MAUI's
        Assert.False(swipe.IsOpen);

        // 150 ≥ 60 % of the 200 DIP open distance: the items open fully.
        swipe.Touch(new(_pointer, SkUiTouchAction.Released, new Point(100, 30), TimeSpan.FromMilliseconds(900)));
        Assert.True(swipe.IsOpen);
        Assert.Equal(-200, swipe.Offset);
        Assert.Equal(0, host.TranslationX);
        Assert.Equal("started Left", log[0]);
        Assert.Equal(6, log.Count(line => line.StartsWith("changing Left", StringComparison.Ordinal)));
        Assert.Equal("changing Left -150", log[^2]);
        Assert.Equal("ended Left True", log[^1]);
    }

    [Fact]
    public void AReleaseBeforeTheThresholdClosesAndGivesTheContentItsInputBack()
    {
        var swipe = Swipe(out _);
        swipe.RightItems = TwoItems();
        var ended = new List<bool>();
        swipe.SwipeEnded += (_, args) => ended.Add(args.IsOpen);
        SkUiTestHelpers.Arrange(swipe, 300, 60);

        Drag(swipe, new Point(250, 30), new Point(150, 30)); // 100 < 120

        Assert.Equal([false], ended);
        Assert.False(swipe.IsOpen);
        Assert.Null(swipe.ShownSide);
        Assert.Equal(0, swipe.ContentHost.TranslationX);
        Assert.False(swipe.ContentHost.InputTransparent);
        Assert.False(swipe.GetItemsHost(OpenSwipeItem.RightItems)!.IsVisible);
    }

    [Fact]
    public void TheContentStopsAtTheOpenDistance()
    {
        var swipe = Swipe(out _);
        swipe.RightItems = TwoItems();
        SkUiTestHelpers.Arrange(swipe, 300, 60);

        Drag(swipe, new Point(290, 30), new Point(10, 30), release: false);

        Assert.Equal(-200, swipe.Offset);
    }

    [Fact]
    public void SwipesTowardsASideWithoutItemsAreLeftAlone()
    {
        var swipe = Swipe(out var content);
        swipe.RightItems = TwoItems();
        var pans = 0;
        content.PanUpdated += (_, _) => pans++;
        var started = 0;
        swipe.SwipeStarted += (_, _) => started++;
        SkUiTestHelpers.Arrange(swipe, 300, 60);

        Drag(swipe, new Point(50, 30), new Point(200, 30));

        Assert.Equal(0, started);
        Assert.Null(swipe.ShownSide);
        Assert.True(pans > 0); // the content's own pan got the drag
    }

    [Fact]
    public void DragsOfViewsInsideTheContentWinOverTheSwipe()
    {
        var swipe = Swipe(out var content);
        swipe.RightItems = TwoItems();
        content.PanAxis = SkUiPanAxis.Horizontal;
        var pans = 0;
        content.PanUpdated += (_, _) => pans++;
        SkUiTestHelpers.Arrange(swipe, 300, 60);

        Drag(swipe, new Point(250, 30), new Point(100, 30));

        Assert.True(pans > 0);
        Assert.Null(swipe.ShownSide);
    }

    [Fact]
    public void DraggingBackPastTheClosedPositionRevealsTheOtherSide()
    {
        var swipe = Swipe(out _);
        swipe.RightItems = TwoItems();
        swipe.LeftItems = [new SwipeItem { Text = Archive }];
        SkUiTestHelpers.Arrange(swipe, 300, 60);

        var id = ++_pointer;
        swipe.Touch(new(id, SkUiTouchAction.Pressed, new Point(150, 30), TimeSpan.Zero));
        swipe.Touch(new(id, SkUiTouchAction.Moved, new Point(70, 30), TimeSpan.FromMilliseconds(100)));
        Assert.Equal(OpenSwipeItem.RightItems, swipe.ShownSide);
        swipe.Touch(new(id, SkUiTouchAction.Moved, new Point(230, 30), TimeSpan.FromMilliseconds(200)));

        Assert.Equal(OpenSwipeItem.LeftItems, swipe.ShownSide);
        Assert.Equal(80, swipe.Offset);
        Assert.False(swipe.GetItemsHost(OpenSwipeItem.RightItems)!.IsVisible);
        Assert.True(swipe.GetItemsHost(OpenSwipeItem.LeftItems)!.IsVisible);
        Assert.Equal(new Rect(0, 0, 100, 60), swipe.GetItemsHost(OpenSwipeItem.LeftItems)!.Frame);
        Assert.Equal(-20, swipe.GetItemsHost(OpenSwipeItem.LeftItems)!.TranslationX);
    }

    [Fact]
    public void AFastSwipeOpensAndAFastSwipeBackCloses()
    {
        var swipe = Swipe(out _);
        swipe.RightItems = TwoItems();
        SkUiTestHelpers.Arrange(swipe, 300, 60);

        Drag(swipe, new Point(250, 30), new Point(200, 30), steps: 5, stepMs: 8); // 50 DIPs, far below the threshold, fast
        Assert.True(swipe.IsOpen);
        Assert.Equal(-200, swipe.Offset);

        Drag(swipe, new Point(50, 30), new Point(110, 30), steps: 5, stepMs: 8); // back to 140 open, past the threshold, but fast
        Assert.False(swipe.IsOpen);
        Assert.Null(swipe.ShownSide);
    }

    [Fact]
    public void ThresholdSetsTheDistanceToOpenUpToTheOpenDistance()
    {
        var swipe = Swipe(out _);
        swipe.RightItems = TwoItems();
        swipe.Threshold = 50;
        SkUiTestHelpers.Arrange(swipe, 300, 60);

        Drag(swipe, new Point(250, 30), new Point(190, 30));
        Assert.True(swipe.IsOpen);
        swipe.Close(animated: false);

        // Beyond the open distance it is the open distance.
        swipe.Threshold = 500;
        Drag(swipe, new Point(250, 30), new Point(60, 30));
        Assert.False(swipe.IsOpen);
        Drag(swipe, new Point(290, 30), new Point(50, 30));
        Assert.True(swipe.IsOpen);
    }

    [Fact]
    public void TopItemsAreRevealedBySwipingDownAndBottomItemsBySwipingUp()
    {
        var swipe = Swipe(out _, height: 100);
        swipe.TopItems = TwoItems();
        swipe.BottomItems = [new SkUiSwipeItemView { Content = new SkUiBox { HeightRequest = 40 } }];
        SkUiTestHelpers.Arrange(swipe, 300, 100);

        Drag(swipe, new Point(150, 10), new Point(150, 80), release: false);
        Assert.Equal(OpenSwipeItem.TopItems, swipe.ShownSide);
        Assert.Equal(70, swipe.Offset);
        Assert.Equal(70, swipe.ContentHost.TranslationY);
        // Menu items share the width and are as tall as the content.
        Assert.Equal(new Rect(0, 0, 300, 100), swipe.GetItemsHost(OpenSwipeItem.TopItems)!.Frame);
        Assert.Equal(new Rect(150, 0, 150, 100), ItemButton(swipe, OpenSwipeItem.TopItems, 1).Frame);
        swipe.Touch(new(_pointer, SkUiTouchAction.Released, new Point(150, 80), TimeSpan.FromSeconds(1)));
        Assert.Equal(100, swipe.Offset);
        swipe.Close(animated: false);

        // An item view is as tall as it measures.
        Drag(swipe, new Point(150, 90), new Point(150, 20));
        Assert.Equal(OpenSwipeItem.BottomItems, swipe.ShownSide);
        Assert.Equal(new Rect(0, 60, 300, 40), swipe.GetItemsHost(OpenSwipeItem.BottomItems)!.Frame);
        Assert.Equal(-40, swipe.Offset);
    }

    [Fact]
    public void ItemsThatAreNotVisibleTakeNoRoom()
    {
        var swipe = Swipe(out _);
        var items = TwoItems();
        ((SwipeItem)items[0]).IsVisible = false;
        swipe.RightItems = items;
        SkUiTestHelpers.Arrange(swipe, 300, 60);

        swipe.Open(OpenSwipeItem.RightItems, animated: false);
        Assert.Equal(-100, swipe.Offset);
        Assert.Equal(new Rect(200, 0, 100, 60), swipe.GetItemsHost(OpenSwipeItem.RightItems)!.Frame);

        // Shown again while open: the items open to their new length.
        ((SwipeItem)items[0]).IsVisible = true;
        Assert.Equal(-200, swipe.Offset);
        Assert.Equal(new Rect(100, 0, 200, 60), swipe.GetItemsHost(OpenSwipeItem.RightItems)!.Frame);

        // None left: closed.
        ((SwipeItem)items[0]).IsVisible = false;
        ((SwipeItem)items[1]).IsVisible = false;
        Assert.Null(swipe.ShownSide);
        Assert.False(swipe.IsOpen);
    }

    [Fact]
    public void ItemsAddedOrRemovedWhileOpenAreLaidOutAgain()
    {
        var swipe = Swipe(out _);
        swipe.RightItems.Add(new SwipeItem { Text = Archive });
        SkUiTestHelpers.Arrange(swipe, 300, 60);
        swipe.Open(OpenSwipeItem.RightItems, animated: false);
        Assert.Equal(-100, swipe.Offset);

        swipe.RightItems.Add(new SwipeItem { Text = Delete });
        Assert.Equal(-200, swipe.Offset);
        Assert.Equal(Delete, ItemButton(swipe, OpenSwipeItem.RightItems, 1).Text);

        swipe.RightItems.RemoveAt(0);
        Assert.Equal(-100, swipe.Offset);
        Assert.Equal(Delete, ItemButton(swipe, OpenSwipeItem.RightItems, 0).Text);
    }

    [Fact]
    public void ItemsRemovedWhileDraggedEndTheSwipe()
    {
        var swipe = Swipe(out _);
        swipe.RightItems = TwoItems();
        var ended = new List<bool>();
        swipe.SwipeEnded += (_, args) => ended.Add(args.IsOpen);
        SkUiTestHelpers.Arrange(swipe, 300, 60);
        Drag(swipe, new Point(250, 30), new Point(100, 30), release: false);

        swipe.RightItems.Clear();
        Assert.Null(swipe.ShownSide);
        Assert.False(swipe.IsOpen);
        Assert.Equal([false], ended);

        // The rest of the drag does nothing.
        swipe.Touch(new(_pointer, SkUiTouchAction.Moved, new Point(50, 30), TimeSpan.FromSeconds(1)));
        swipe.Touch(new(_pointer, SkUiTouchAction.Released, new Point(50, 30), TimeSpan.FromSeconds(1.1)));
        Assert.Null(swipe.ShownSide);
        Assert.Single(ended);
    }

    #endregion

    #region Invoking items

    [Fact]
    public void TappingAnItemInvokesItThenCloses()
    {
        var log = new List<string>();
        var swipe = Swipe(out _);
        swipe.RightItems = TwoItems(log);
        var delete = (SwipeItem)swipe.RightItems[1];
        delete.Command = new Command(parameter => log.Add($"command {parameter}"));
        delete.CommandParameter = 7;
        SkUiTestHelpers.Arrange(swipe, 300, 60);
        swipe.Open(OpenSwipeItem.RightItems, animated: false);

        Tap(swipe, new Point(150, 30)); // Delete, beside the content (Archive is at the right edge)

        Assert.Equal(["command 7", Delete], log);
        Assert.False(swipe.IsOpen);
        Assert.Null(swipe.ShownSide);
    }

    [Fact]
    public void RemainOpenKeepsTheItemsOpenAfterAnInvoke()
    {
        var log = new List<string>();
        var swipe = Swipe(out _);
        swipe.RightItems = TwoItems(log);
        swipe.RightItems.SwipeBehaviorOnInvoked = SwipeBehaviorOnInvoked.RemainOpen;
        SkUiTestHelpers.Arrange(swipe, 300, 60);
        swipe.Open(OpenSwipeItem.RightItems, animated: false);

        Tap(swipe, new Point(250, 30));

        Assert.Equal([Archive], log);
        Assert.True(swipe.IsOpen);
        Assert.Equal(-200, swipe.Offset);
    }

    [Fact]
    public void ATapOnOpenContentClosesWithoutReachingTheContent()
    {
        var swipe = Swipe(out var content);
        swipe.RightItems = TwoItems();
        var taps = 0;
        content.Tapped += (_, _) => taps++;
        SkUiTestHelpers.Arrange(swipe, 300, 60);
        swipe.Open(OpenSwipeItem.RightItems, animated: false);

        Tap(swipe, new Point(50, 30));
        Assert.Equal(0, taps);
        Assert.False(swipe.IsOpen);
        Assert.Null(swipe.ShownSide);

        Tap(swipe, new Point(50, 30));
        Assert.Equal(1, taps);
    }

    [Fact]
    public void ExecuteModeInvokesTheFirstVisibleItemPastTheThreshold()
    {
        var log = new List<string>();
        var swipe = Swipe(out _);
        swipe.RightItems = TwoItems(log);
        swipe.RightItems.Mode = SwipeMode.Execute;
        ((SwipeItem)swipe.RightItems[0]).IsVisible = false;
        SkUiTestHelpers.Arrange(swipe, 300, 60);

        // The items share the width; the open distance is 80 % of it (240), the threshold 60 % of that (144).
        Drag(swipe, new Point(290, 30), new Point(160, 30), release: false);
        Assert.Equal(new Rect(0, 0, 300, 60), swipe.GetItemsHost(OpenSwipeItem.RightItems)!.Frame);
        swipe.Touch(new(_pointer, SkUiTouchAction.Released, new Point(160, 30), TimeSpan.FromSeconds(1)));
        Assert.Empty(log);
        Assert.False(swipe.IsOpen);

        Drag(swipe, new Point(290, 30), new Point(10, 30));
        Assert.Equal([Delete], log);
        Assert.False(swipe.IsOpen); // closes unless the items remain open
        Assert.Null(swipe.ShownSide);

        swipe.RightItems.SwipeBehaviorOnInvoked = SwipeBehaviorOnInvoked.RemainOpen;
        Drag(swipe, new Point(290, 30), new Point(10, 30));
        Assert.Equal([Delete, Delete], log);
        Assert.True(swipe.IsOpen);
        Assert.Equal(-240, swipe.Offset);
    }

    [Fact]
    public void ExecuteModeDoesNotInvokeADisabledFirstItem()
    {
        // As MAUI's handlers: the first visible item is invoked only when it is enabled; no other item is invoked instead.
        var log = new List<string>();
        var swipe = Swipe(out _);
        swipe.RightItems = TwoItems(log);
        swipe.RightItems.Mode = SwipeMode.Execute;
        ((SwipeItem)swipe.RightItems[0]).IsEnabled = false;
        SkUiTestHelpers.Arrange(swipe, 300, 60);
        Drag(swipe, new Point(290, 30), new Point(10, 30));
        Assert.Empty(log);
        Assert.False(swipe.IsOpen);

        // A drawn item view whose command cannot execute is disabled too.
        var canExecute = false;
        var view = new SkUiSwipeItemView { Command = new Command(_ => log.Add("command"), () => canExecute), Content = new SkUiBox() };
        view.Invoked += (_, _) => log.Add("view");
        swipe.RightItems = new SwipeItems([view]) { Mode = SwipeMode.Execute };
        SkUiTestHelpers.Arrange(swipe, 300, 60);
        Drag(swipe, new Point(290, 30), new Point(10, 30));
        Assert.Empty(log);
        canExecute = true;
        Drag(swipe, new Point(290, 30), new Point(10, 30));
        Assert.Equal(["command", "view"], log);
    }

    [Fact]
    public void ExecuteItemsShowTheirTextAtTheInnerEdgeSoAPartialSwipeReadsIt()
    {
        var swipe = Swipe(out _);
        swipe.LeftItems = [new SwipeItem { Text = Archive }];
        swipe.RightItems = [new SwipeItem { Text = Delete }];
        swipe.LeftItems.Mode = SwipeMode.Execute;
        swipe.RightItems.Mode = SwipeMode.Execute;
        SkUiTestHelpers.Arrange(swipe, 300, 60);

        // Swiped a little to the left: the delete item is revealed from its left edge, where its text is.
        Drag(swipe, new Point(250, 30), new Point(150, 30), release: false);
        var delete = ItemButton(swipe, OpenSwipeItem.RightItems, 0);
        Assert.Equal(TextAlignment.Start, delete.HorizontalTextAlignment);
        Assert.Equal(20, delete.Padding.Left);
        swipe.Touch(new(_pointer, SkUiTouchAction.Released, new Point(150, 30), TimeSpan.FromSeconds(1)));
        Drag(swipe, new Point(50, 30), new Point(150, 30), release: false);
        var archive = ItemButton(swipe, OpenSwipeItem.LeftItems, 0);
        Assert.Equal(TextAlignment.End, archive.HorizontalTextAlignment);
        Assert.Equal(20, archive.Padding.Right);
        swipe.Touch(new(_pointer, SkUiTouchAction.Released, new Point(150, 30), TimeSpan.FromSeconds(1)));

        // Physical sides in right-to-left layouts (start and end swap there).
        swipe.FlowDirection = FlowDirection.RightToLeft;
        Assert.Equal(TextAlignment.End, delete.HorizontalTextAlignment);
        Assert.Equal(TextAlignment.Start, archive.HorizontalTextAlignment);

        // Reveal items show whole once open: centered, as on MAUI.
        swipe.RightItems.Mode = SwipeMode.Reveal;
        Assert.Equal(TextAlignment.Center, delete.HorizontalTextAlignment);
        Assert.Equal(new Thickness(4), delete.Padding);
    }

    [Fact]
    public void DrawnItemViewsAreHostedSizedAndInvoked()
    {
        var log = new List<string>();
        var enabled = true;
        var command = new Command(parameter => log.Add($"command {parameter}"), () => enabled);
        var item = new SkUiSwipeItemView { Content = new SkUiBox { WidthRequest = 80 }, Command = command, CommandParameter = "x" };
        item.Invoked += (_, _) => log.Add("invoked");
        var swipe = Swipe(out _);
        swipe.RightItems = [item, new SwipeItem { Text = Delete }];
        Assert.Same(swipe.RightItems, item.Parent); // the collection's until shown
        SkUiTestHelpers.Arrange(swipe, 300, 60);

        swipe.Open(OpenSwipeItem.RightItems, animated: false);
        Assert.Same(swipe.GetItemsHost(OpenSwipeItem.RightItems), item.Parent);
        Assert.Equal(new Rect(100, 0, 80, 60), item.Frame); // first: at the right edge
        Assert.Equal(-180, swipe.Offset);

        Tap(swipe, new Point(260, 30));
        Assert.Equal(["command x", "invoked"], log);
        Assert.False(swipe.IsOpen);

        // A command that cannot execute disables it.
        enabled = false;
        command.RaiseCanExecuteChanged();
        swipe.Open(OpenSwipeItem.RightItems, animated: false);
        Tap(swipe, new Point(260, 30));
        Assert.Equal(2, log.Count);
        Assert.True(swipe.IsOpen);

        // Removed from the items: unparented again.
        swipe.RightItems.Remove(item);
        Assert.Null(item.Parent);
    }

    [Fact]
    public void MauisSwipeItemViewIsRejected()
    {
        var swipe = new SkUiSwipeView();
        Assert.Throws<NotSupportedException>(() => swipe.RightItems.Add(new SwipeItemView()));
        Assert.Throws<NotSupportedException>(() => swipe.LeftItems = [new SwipeItemView()]);
    }

    #endregion

    #region Open and close

    [Fact]
    public void OpenAndCloseMoveTheContentAndTheirEventsComeFromSwipesOnly()
    {
        var swipe = Swipe(out _);
        swipe.LeftItems = TwoItems();
        var events = 0;
        swipe.SwipeStarted += (_, _) => events++;
        swipe.SwipeEnded += (_, _) => events++;

        // Before the first layout it opens once laid out; a side without items does not open.
        swipe.Open(OpenSwipeItem.RightItems, animated: false);
        Assert.Null(swipe.ShownSide);
        swipe.Open(OpenSwipeItem.LeftItems, animated: false);
        Assert.True(swipe.IsOpen);
        SkUiTestHelpers.Arrange(swipe, 300, 60);
        Assert.Equal(200, swipe.Offset);
        Assert.Equal(new Rect(0, 0, 200, 60), swipe.GetItemsHost(OpenSwipeItem.LeftItems)!.Frame);

        // A resize keeps it open.
        SkUiTestHelpers.Arrange(swipe, 400, 80);
        Assert.Equal(200, swipe.Offset);
        Assert.Equal(new Rect(0, 0, 200, 80), swipe.GetItemsHost(OpenSwipeItem.LeftItems)!.Frame);

        swipe.Close(animated: false);
        Assert.False(swipe.IsOpen);
        Assert.Null(swipe.ShownSide);
        Assert.Equal(0, events);
        Assert.Throws<ArgumentOutOfRangeException>(() => swipe.Open((OpenSwipeItem)9));
    }

    [Fact]
    public void OpeningAnimatesOnTheUiClockWithoutRecordingAgain()
    {
        var swipe = Swipe(out _);
        swipe.RightItems = TwoItems();
        using var surface = new SkUiTestSurface(swipe, 300, 60);
        surface.Frame(0);
        swipe.Open(OpenSwipeItem.RightItems, animated: false); // the items are recorded when first drawn
        surface.Frame(1);
        swipe.Close(animated: false);
        surface.Frame(2);
        var recorded = surface.RecordedPictures;

        swipe.Open(OpenSwipeItem.RightItems);
        Assert.True(swipe.IsOpen);
        Assert.True(swipe.IsSettling);
        surface.Root.AnimationClock.Tick(TimeSpan.FromMilliseconds(100));
        surface.Frame(100);
        Assert.InRange(swipe.Offset, -199, -1);
        surface.Root.AnimationClock.Tick(TimeSpan.FromMilliseconds(300));
        surface.Frame(300);
        Assert.Equal(-200, swipe.Offset);
        Assert.False(swipe.IsSettling);
        // Neither the content nor the item buttons were recorded again; only the items' host, shown again (it draws nothing).
        var opened = surface.RecordedPictures;
        Assert.InRange(opened - recorded, 0, 1);

        // Dragging moves at composite time too.
        Drag(swipe, new Point(50, 30), new Point(150, 30), release: false);
        surface.Frame(400);
        Assert.Equal(-100, swipe.Offset);
        Assert.Equal(opened, surface.RecordedPictures);
    }

    #endregion

    #region Around the swipe view

    private static (SkUiScrollView Scroller, List<SkUiSwipeView> Rows) ScrolledRows()
    {
        var rows = new List<SkUiSwipeView>();
        var stack = new SkUiVerticalStackLayout();
        for (var index = 0; index < 20; index++)
        {
            var row = Swipe(out _);
            row.RightItems = TwoItems();
            rows.Add(row);
            stack.Children.Add(row);
        }
        var scroller = new SkUiScrollView { Content = stack, Overscroll = SkUiOverscrollMode.None, VerticalScrollBarVisibility = ScrollBarVisibility.Never };
        SkUiTestHelpers.Arrange(scroller, 300, 300);
        return (scroller, rows);
    }

    [Fact]
    public void VerticalDragsScrollAndHorizontalOnesSwipe()
    {
        var (scroller, rows) = ScrolledRows();

        Drag(scroller, new Point(150, 250), new Point(150, 50));
        Assert.Equal(200, scroller.ScrollY);
        Assert.All(rows, row => Assert.Null(row.ShownSide));

        Drag(scroller, new Point(250, 30), new Point(100, 30)); // 30 on screen is 230 in the content: row 3
        var swiped = rows.Single(row => row.ShownSide is not null);
        Assert.Same(rows[3], swiped);
        Assert.True(swiped.IsOpen);
        Assert.Equal(200, scroller.ScrollY);
    }

    [Fact]
    public void ScrollingAroundOpenItemsClosesThem()
    {
        var (scroller, rows) = ScrolledRows();
        rows[1].Open(OpenSwipeItem.RightItems, animated: false);

        Drag(scroller, new Point(150, 250), new Point(150, 150));

        Assert.False(rows[1].IsOpen);
        Assert.Null(rows[1].ShownSide);
    }

    [Fact]
    public void ItemsBindToTheBindingContextAndANewContextCloses()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var swipe = Swipe(out _);
        var item = new SwipeItem();
        item.SetBinding(MenuItem.TextProperty, new Binding("Text"));
        swipe.RightItems = [item];
        swipe.BindingContext = new { Text = Archive };
        SkUiTestHelpers.Arrange(swipe, 300, 60);
        swipe.Open(OpenSwipeItem.RightItems, animated: false);
        Assert.Equal(Archive, ItemButton(swipe, OpenSwipeItem.RightItems, 0).Text);

        // A recycled row shows another item, closed.
        swipe.BindingContext = new { Text = Delete };
        Assert.False(swipe.IsOpen);
        Assert.Null(swipe.ShownSide);
        swipe.Open(OpenSwipeItem.RightItems, animated: false);
        Assert.Equal(Delete, ItemButton(swipe, OpenSwipeItem.RightItems, 0).Text);
    }

    [Fact]
    public void ListRowsKeepTheirItemTapsAndScrollVertically()
    {
        var tapped = new List<object?>();
        var list = new SkUiCollectionView
        {
            ItemsSource = new ObservableCollection<int>(Enumerable.Range(0, 100)),
            ItemTemplate = new DataTemplate(() =>
            {
                var row = Swipe(out _);
                row.RightItems = TwoItems();
                return row;
            }),
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Overscroll = SkUiOverscrollMode.None
        };
        list.ItemTapped += (_, args) => tapped.Add(args.Item);
        SkUiTestHelpers.Arrange(list, 300, 300);

        Tap(list, new Point(150, 90));
        Assert.Equal([1], tapped);

        Drag(list, new Point(250, 90), new Point(100, 90));
        var row = (SkUiSwipeView)list.GetRealizedView(1)!;
        Assert.True(row.IsOpen);
        Assert.Single(tapped);

        // Tapping the open row closes it; the list does not see a tap.
        Tap(list, new Point(50, 90));
        Assert.False(row.IsOpen);
        Assert.Single(tapped);

        Drag(list, new Point(150, 250), new Point(150, 50));
        Assert.Equal(200, list.ScrollView.ScrollY);
    }

    [Fact]
    public void ADisabledSwipeViewDoesNotSwipe()
    {
        var swipe = Swipe(out _);
        swipe.RightItems = TwoItems();
        swipe.IsEnabled = false;
        SkUiTestHelpers.Arrange(swipe, 300, 60);

        Drag(swipe, new Point(250, 30), new Point(50, 30));

        Assert.Null(swipe.ShownSide);
    }

    #endregion
}

/// <summary><see cref="SkUiSwipeView"/> XAML: MAUI's docs samples with the prefix changed.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class SwipeViewXamlTests
{
    private sealed class Model : INotifyPropertyChanged
    {
        public List<string> Log { get; } = [];
        public ICommand FavoriteCommand => new Command(() => Log.Add("favorite"));
        public ICommand CheckAnswerCommand => new Command<string>(answer => Log.Add($"answer {answer}"));
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
    }

    [Fact]
    public void MauiDocsSamplesLoadWithThePrefixChanged()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiSwipeView Threshold="200">
                  <sk:SkUiSwipeView.LeftItems>
                    <SwipeItems>
                      <SwipeItem Text="Favorite" BackgroundColor="LightGreen" Command="{Binding FavoriteCommand}" />
                      <SwipeItem Text="Delete" BackgroundColor="LightPink" />
                    </SwipeItems>
                  </sk:SkUiSwipeView.LeftItems>
                  <sk:SkUiGrid HeightRequest="60" WidthRequest="300" BackgroundColor="LightGray">
                    <sk:SkUiLabel Text="Swipe right" HorizontalOptions="Center" VerticalOptions="Center" />
                  </sk:SkUiGrid>
                </sk:SkUiSwipeView>
                <sk:SkUiSwipeView>
                  <sk:SkUiSwipeView.TopItems>
                    <SwipeItems Mode="Execute" SwipeBehaviorOnInvoked="RemainOpen">
                      <sk:SkUiSwipeItemView Command="{Binding CheckAnswerCommand}" CommandParameter="42">
                        <sk:SkUiVerticalStackLayout Margin="10" WidthRequest="300">
                          <sk:SkUiLabel Text="Check" FontAttributes="Bold" HorizontalOptions="Center" />
                        </sk:SkUiVerticalStackLayout>
                      </sk:SkUiSwipeItemView>
                    </SwipeItems>
                  </sk:SkUiSwipeView.TopItems>
                  <sk:SkUiGrid HeightRequest="60" WidthRequest="300" BackgroundColor="LightGray">
                    <sk:SkUiLabel Text="What's 2+2?" HorizontalOptions="Center" VerticalOptions="Center" />
                  </sk:SkUiGrid>
                </sk:SkUiSwipeView>
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var model = new Model();
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var stack = (SkUiVerticalStackLayout)root.Content;
        var (favorites, question) = ((SkUiSwipeView)stack.Children[0], (SkUiSwipeView)stack.Children[1]);
        SkUiTestHelpers.Arrange(stack, 300, 400);

        Assert.Equal(200, favorites.Threshold);
        Assert.Equal(2, favorites.LeftItems.Count);
        Assert.Equal(60, favorites.Frame.Height);
        favorites.Open(OpenSwipeItem.LeftItems, animated: false);
        Assert.Equal(200, favorites.Offset);
        ((Microsoft.Maui.ISwipeItem)favorites.LeftItems[0]).OnInvoked();
        Assert.Equal(["favorite"], model.Log);

        Assert.Equal(SwipeMode.Execute, question.TopItems.Mode);
        var item = (SkUiSwipeItemView)question.TopItems[0];
        Assert.Same(model, item.BindingContext);
        item.OnInvoked();
        Assert.Equal(["favorite", "answer 42"], model.Log);
    }
}
