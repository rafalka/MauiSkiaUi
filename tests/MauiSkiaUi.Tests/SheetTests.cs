using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// S3–S5 <see cref="SkUiSheet"/>: opening and closing on detents as render-thread motion, the scrim following the sheet, drags,
/// flings and the hand-off from a scroller inside, the dialog presentation, side sheets, and the modal pane (focus trap, Escape,
/// semantics).
/// </summary>
public class SheetTests
{
    private const int Width = 400;
    private const int Height = 800;
    private static long _pointer = 170_000;

    /// <summary>A page (a button filling it) with a sheet over it, both in a grid.</summary>
    private static SkUiGrid Page(out SkUiSheet sheet, out SkUiButton page, View? content = null)
    {
        page = new SkUiButton { Text = "Page", AutomationId = "PageButton" };
        sheet = new SkUiSheet { Content = content ?? new SkUiBox { HeightRequest = 200 } };
        return new SkUiGrid { Children = { page, sheet } };
    }

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    /// <summary>A slow drag (no fling) from <paramref name="from"/> to <paramref name="to"/>, released after a pause.</summary>
    private static void Drag(SkUiView root, Point from, Point to, double startMs = 0, int steps = 6, double stepMs = 100, bool pause = true)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, from, TimeSpan.FromMilliseconds(startMs)));
        for (var step = 1; step <= steps; step++)
        {
            var point = new Point(from.X + (to.X - from.X) * step / steps, from.Y + (to.Y - from.Y) * step / steps);
            root.Touch(new(id, SkUiTouchAction.Moved, point, TimeSpan.FromMilliseconds(startMs + step * stepMs)));
        }
        root.Touch(new(id, SkUiTouchAction.Released, to, TimeSpan.FromMilliseconds(startMs + steps * stepMs + (pause ? 300 : stepMs))));
    }

    /// <summary>Runs frames until the sheet's motions are done.</summary>
    private static void Settle(SkUiTestSurface surface, ref double time)
    {
        for (var frame = 0; frame < 120; frame++)
        {
            time += 16;
            surface.Frame(time);
        }
    }

    [Fact]
    public void DefaultsFollowTheBrief()
    {
        var sheet = new SkUiSheet();
        Assert.Equal(SkUiSheetEdge.Bottom, sheet.Edge);
        Assert.Equal([SkUiSheetDetent.Fraction(0.5), SkUiSheetDetent.Fraction(1)], sheet.Detents);
        Assert.NotSame(sheet.Detents, new SkUiSheet().Detents);
        Assert.Equal(0, sheet.SelectedDetentIndex);
        Assert.False(sheet.IsOpen);
        Assert.True(sheet.IsModal);
        Assert.Null(sheet.ScrimColor);
        Assert.True(sheet.DismissOnScrimTap);
        Assert.True(sheet.IsDragEnabled);
        Assert.True(sheet.HasGrabber);
        Assert.Equal(-1, sheet.CornerRadius);
        Assert.Equal(SkUiSheetPresentation.Sheet, sheet.Presentation);
        Assert.Null(sheet.Title);
        Assert.Null(sheet.Description);
        Assert.Null(sheet.Actions);
        Assert.Equal(BindingMode.TwoWay, SkUiSheet.IsOpenProperty.DefaultBindingMode);
        Assert.Equal(BindingMode.TwoWay, SkUiSheet.SelectedDetentIndexProperty.DefaultBindingMode);
    }

    [Fact]
    public void ClosedSheetDrawsNothingAndLetsPointersThrough()
    {
        var root = Page(out var sheet, out var page);
        var taps = 0;
        page.Clicked += (_, _) => taps++;
        using var surface = new SkUiTestSurface(root, Width, Height);
        surface.Frame(0);
        Tap(root, new Point(200, 700));
        Assert.Equal(1, taps);
        Assert.Equal(0, sheet.Position);
    }

    [Fact]
    public async Task OpensToItsDetentOnTheRenderThreadAndCloses()
    {
        var root = Page(out var sheet, out _);
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        var opened = 0;
        var closed = new List<SkUiSheetClosedEventArgs>();
        sheet.Opened += (_, _) => opened++;
        sheet.Closed += (_, args) => closed.Add(args);

        var opening = sheet.OpenAsync();
        Assert.True(sheet.IsOpen);
        surface.Frame(time += 16);
        Assert.False(opening.IsCompleted);
        Assert.True(sheet.Position < 400);
        Settle(surface, ref time);
        await opening.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, opened);
        Assert.Equal(400, sheet.Position, 1); // half of the 800 the surface leaves
        Assert.Equal(0, sheet.SelectedDetentIndex);

        var closing = sheet.CloseAsync();
        Assert.False(sheet.IsOpen);
        Settle(surface, ref time);
        await closing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, sheet.Position, 1);
        Assert.Single(closed);
        Assert.True(closed[0].IsDismissed);
        Assert.Null(closed[0].Result);
    }

    [Fact]
    public void SelectedDetentMovesTheOpenSheetAndDragsWriteItBack()
    {
        var root = Page(out var sheet, out _);
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        var changes = new List<(int, int)>();
        sheet.DetentChanged += (_, args) => changes.Add((args.PreviousIndex, args.CurrentIndex));
        sheet.IsOpen = true;
        Settle(surface, ref time);

        sheet.SelectedDetentIndex = 1;
        Settle(surface, ref time);
        Assert.Equal(800, sheet.Position, 1);

        // Dragged down to about half: rests on the half detent, which is written back.
        Drag(root, new Point(200, 100), new Point(200, 460), startMs: time);
        Settle(surface, ref time);
        Assert.Equal(400, sheet.Position, 1);
        Assert.Equal(0, sheet.SelectedDetentIndex);
        Assert.Equal([(0, 1), (1, 0)], changes);
        Assert.True(sheet.IsOpen);
    }

    [Fact]
    public void DraggedAwayItClosesDismissed()
    {
        var root = Page(out var sheet, out _);
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        SkUiSheetClosedEventArgs? closed = null;
        sheet.Closed += (_, args) => closed = args;
        sheet.IsOpen = true;
        Settle(surface, ref time);

        Drag(root, new Point(200, 450), new Point(200, 780), startMs: time);
        Settle(surface, ref time);
        Assert.Equal(0, sheet.Position, 1);
        Assert.False(sheet.IsOpen);
        Assert.NotNull(closed);
        Assert.True(closed.IsDismissed);
    }

    [Fact]
    public void ScrimTapDismissesAndBlocksThePage()
    {
        var root = Page(out var sheet, out var page);
        var taps = 0;
        page.Clicked += (_, _) => taps++;
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.DismissOnScrimTap = false;
        sheet.IsOpen = true;
        Settle(surface, ref time);

        Tap(root, new Point(200, 100)); // above the sheet: the scrim
        Assert.Equal(0, taps);
        Assert.True(sheet.IsOpen);
        Tap(root, new Point(200, 600)); // on the sheet
        Assert.Equal(0, taps);
        Assert.True(sheet.IsOpen);

        sheet.DismissOnScrimTap = true;
        Tap(root, new Point(200, 100));
        Assert.False(sheet.IsOpen);
        Settle(surface, ref time);
        Tap(root, new Point(200, 100));
        Assert.Equal(1, taps);
    }

    [Fact]
    public void ScrimOpacityFollowsTheSheetUpToTheLowestDetent()
    {
        var root = Page(out var sheet, out _);
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.IsOpen = true;
        Settle(surface, ref time);
        var scrim = (SkUiView)sheet.SkiaChildren.First();
        var props = Rendering.SkUiRenderProps.Default;
        ((Rendering.ISkUiRenderable)scrim).GetRenderProps(ref props);
        Assert.NotNull(props.Links);
        Assert.Equal(1, props.EffectiveOpacity, 3);

        // Dragged halfway down to closed (held, not released): half the scrim.
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, new Point(200, 450), TimeSpan.FromMilliseconds(time)));
        root.Touch(new(id, SkUiTouchAction.Moved, new Point(200, 500), TimeSpan.FromMilliseconds(time + 100)));
        root.Touch(new(id, SkUiTouchAction.Moved, new Point(200, 650), TimeSpan.FromMilliseconds(time + 200)));
        props = Rendering.SkUiRenderProps.Default;
        ((Rendering.ISkUiRenderable)scrim).GetRenderProps(ref props);
        Assert.Equal(sheet.Position / 400, props.EffectiveOpacity, 2);
        Assert.InRange(sheet.Position, 150, 250);
        root.Touch(new(id, SkUiTouchAction.Cancelled, new Point(200, 650), TimeSpan.FromMilliseconds(time + 300)));
    }

    [Fact]
    public void ScrollerInsideHandsTheDragOverAtItsTop()
    {
        var list = new SkUiVerticalStackLayout();
        for (var row = 0; row < 40; row++)
            list.Children.Add(new SkUiBox { HeightRequest = 50 });
        var scroller = new SkUiScrollView { Content = list };
        var root = Page(out var sheet, out _, scroller);
        sheet.SelectedDetentIndex = 1;
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.IsOpen = true;
        Settle(surface, ref time);
        Assert.Equal(800, sheet.Position, 1);

        // Up: the list scrolls (the sheet is at its top detent).
        Drag(root, new Point(200, 600), new Point(200, 300), startMs: time);
        Settle(surface, ref time);
        Assert.True(scroller.ScrollY > 200);
        Assert.Equal(800, sheet.Position, 1);

        // Down from the list's top: the list goes back to its top, then the sheet follows the finger.
        scroller.ScrollTo(0, 0);
        Drag(root, new Point(200, 300), new Point(200, 660), startMs: time);
        Settle(surface, ref time);
        Assert.Equal(0, scroller.ScrollY, 1);
        Assert.Equal(400, sheet.Position, 1);
        Assert.Equal(0, sheet.SelectedDetentIndex);
    }

    [Fact]
    public void AFlingPassesTheMiddleDetent()
    {
        var root = Page(out var sheet, out _);
        sheet.Detents = [SkUiSheetDetent.Fraction(0.25), SkUiSheetDetent.Fraction(0.5), SkUiSheetDetent.Fraction(1)];
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.IsOpen = true;
        Settle(surface, ref time);
        Assert.Equal(200, sheet.Position, 1);

        Drag(root, new Point(200, 700), new Point(200, 500), startMs: time, steps: 4, stepMs: 10, pause: false);
        Settle(surface, ref time);
        Assert.Equal(800, sheet.Position, 1);
        Assert.Equal(2, sheet.SelectedDetentIndex);
    }

    [Fact]
    public void DragDisabledStillTakesPressesOnTheSheet()
    {
        var root = Page(out var sheet, out _);
        var scrimTaps = 0;
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.IsDragEnabled = false;
        sheet.Closed += (_, _) => scrimTaps++;
        sheet.IsOpen = true;
        Settle(surface, ref time);
        Drag(root, new Point(200, 450), new Point(200, 780), startMs: time);
        Settle(surface, ref time);
        Assert.Equal(400, sheet.Position, 1);
        Tap(root, new Point(200, 600));
        Assert.True(sheet.IsOpen);
        Assert.Equal(0, scrimTaps);
    }

    [Fact]
    public void WithoutAFrameItOpensAtOnce()
    {
        var root = Page(out var sheet, out _);
        SkUiTestHelpers.Arrange(root, Width, Height);
        sheet.IsOpen = true;
        SkUiTestHelpers.Arrange(root, Width, Height);
        Assert.Equal(400, sheet.Position, 1);
        Assert.True(sheet.CloseAsync().IsCompleted);
        Assert.Equal(0, sheet.Position, 1);
    }

    [Fact]
    public void FitContentAndHeightDetentsKeepClearOfTheBottomInset()
    {
        SkUiView.WindowPlacementOverride = _ => (new Size(Width, Height), Point.Zero, new Thickness(0, 47, 0, 34));
        try
        {
            var root = Page(out var sheet, out _, new SkUiBox { HeightRequest = 150 });
            sheet.Detents = [SkUiSheetDetent.FitContent, SkUiSheetDetent.Height(300), SkUiSheetDetent.Fraction(1)];
            sheet.HasGrabber = false;
            SkUiTestHelpers.Arrange(root, Width, Height);
            sheet.IsOpen = true;
            SkUiTestHelpers.Arrange(root, Width, Height);
            Assert.Equal(184, sheet.Engine.HeightOf(0), 1);
            Assert.Equal(334, sheet.Engine.HeightOf(1), 1);
            Assert.Equal(800 - 47, sheet.Engine.HeightOf(2), 1);
            Assert.Equal(184, sheet.Position, 1);
            // The content sits above the inset.
            var content = (SkUiView)sheet.Content!;
            Assert.Equal(150, content.Height, 1);
        }
        finally
        {
            SkUiView.WindowPlacementOverride = null;
        }
    }

    [Theory]
    [InlineData(SkUiSheetEdge.Start, false, true)]
    [InlineData(SkUiSheetEdge.End, false, false)]
    [InlineData(SkUiSheetEdge.Start, true, false)]
    public void SideSheetsComeFromTheirEdgeAndAreFullByDefault(SkUiSheetEdge edge, bool rightToLeft, bool fromLeft)
    {
        var root = Page(out var sheet, out var page);
        root.FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        sheet.Edge = edge;
        var taps = 0;
        page.Clicked += (_, _) => taps++;
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.IsOpen = true;
        Settle(surface, ref time);
        var width = Math.Min(400, Width - 56d);
        Assert.Equal(width, sheet.Position, 1);
        Assert.Equal(1, sheet.Engine.Count);
        var inside = fromLeft ? new Point(20, 400) : new Point(Width - 20, 400);
        var outside = fromLeft ? new Point(Width - 20, 400) : new Point(20, 400);
        Tap(root, inside);
        Assert.True(sheet.IsOpen);
        // Dragged towards its edge: gone.
        Drag(root, inside, new Point(fromLeft ? inside.X - 340 + 20 : inside.X + 340 - 20, 400), startMs: time);
        Drag(root, new Point(fromLeft ? 150 : Width - 150, 400), new Point(fromLeft ? 0 : Width, 400), startMs: time + 2000);
        Settle(surface, ref time);
        Assert.False(sheet.IsOpen);
        Tap(root, outside);
        Assert.Equal(1, taps);
    }

    [Fact]
    public async Task DialogIsACenteredCardThatFades()
    {
        var root = Page(out var sheet, out _, new SkUiBox { HeightRequest = 120 });
        sheet.Presentation = SkUiSheetPresentation.Dialog;
        sheet.Title = "Delete the shift?";
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        var opening = sheet.OpenAsync();
        surface.Frame(time += 16);
        Assert.True(sheet.IsDialog);
        Assert.False(opening.IsCompleted);
        Settle(surface, ref time);
        await opening.WaitAsync(TimeSpan.FromSeconds(5));
        var panel = (SkUiView)((SkUiView)((SkUiView)sheet.SkiaChildren.ElementAt(1)).SkiaChildren.First()).SkiaChildren.First();
        Assert.Equal(Width - 48, panel.Width, 1);
        Assert.Equal((Height - panel.Height) / 2, panel.Y, 1);
        Assert.Equal(1, panel.Opacity);

        // A tap on the card stays; a tap beside it dismisses.
        Tap(root, new Point(200, 400));
        Assert.True(sheet.IsOpen);
        Tap(root, new Point(200, 20));
        Assert.False(sheet.IsOpen);
        Settle(surface, ref time);
        Assert.Equal(0, panel.Opacity);
    }

    [Fact]
    public void AutoIsADialogOnWideSurfaces()
    {
        var root = Page(out var sheet, out _);
        sheet.Presentation = SkUiSheetPresentation.Auto;
        sheet.IsOpen = true;
        SkUiTestHelpers.Arrange(root, 400, 800);
        Assert.False(sheet.IsDialog);
        SkUiTestHelpers.Arrange(root, 900, 800);
        Assert.True(sheet.IsDialog);
    }

    [Fact]
    public async Task OpaqueContentIsClippedToThePanelsCorners()
    {
        // A popup on SkUiSheetPopupPresenter brings its own opaque background: it must not square off the panel's corners.
        var root = Page(out var sheet, out _, new SkUiBox { Color = Colors.Red, HeightRequest = 120 });
        sheet.Presentation = SkUiSheetPresentation.Dialog;
        sheet.CornerRadius = 24;
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        var opening = sheet.OpenAsync();
        Settle(surface, ref time);
        await opening.WaitAsync(TimeSpan.FromSeconds(5));
        var panel = (SkUiView)((SkUiView)((SkUiView)sheet.SkiaChildren.ElementAt(1)).SkiaChildren.First()).SkiaChildren.First();
        var top = (int)Math.Ceiling(panel.Y);
        var left = (int)Math.Ceiling(panel.X);
        static bool IsRed(SkiaSharp.SKColor color) => color.Red > 200 && color.Green < 60 && color.Blue < 60;

        var bitmap = surface.Frame(time += 16);
        Assert.True(IsRed(bitmap.GetPixel(Width / 2, top + 2))); // the content reaches the panel's top edge
        Assert.False(IsRed(bitmap.GetPixel(left + 1, top + 1)), $"corner: {bitmap.GetPixel(left + 1, top + 1)}");
        Assert.False(IsRed(bitmap.GetPixel(Width - left - 2, top + 1)));

        // Square corners: the clip follows the new shape at the same size.
        sheet.CornerRadius = 0;
        bitmap = surface.Frame(time += 16);
        Assert.True(IsRed(bitmap.GetPixel(left + 1, top + 1)));
    }

    [Fact]
    public void ModalSheetTrapsFocusAndEscapeClosesIt()
    {
        var root = Page(out var sheet, out var page);
        var first = new SkUiButton { Text = "First" };
        var second = new SkUiButton { Text = "Second" };
        sheet.Content = new SkUiVerticalStackLayout { Children = { first, second } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        Assert.True(page.Focus());
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Tab) || page.IsFocused);
        sheet.IsOpen = true;
        Settle(surface, ref time);
        Assert.True(first.IsFocused); // the keyboard user's focus moved in
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Tab));
        Assert.True(second.IsFocused);
        // Tab past the end goes round, never out to the page.
        var order = root.FocusManager.GetTabOrder();
        Assert.DoesNotContain(page, order);
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Tab));
        Assert.False(page.IsFocused);
        Assert.False(page.Focus());

        Assert.True(root.FocusManager.KeyDown(SkUiKey.Escape));
        Assert.False(sheet.IsOpen);
        Settle(surface, ref time);
        Assert.True(page.IsFocused); // back where it was
        Assert.Contains(page, root.FocusManager.GetTabOrder());
    }

    [Fact]
    public void ModalSheetTakesThePageOutOfTheSemanticsTree()
    {
        var root = Page(out var sheet, out _);
        sheet.AutomationId = "Sheet";
        sheet.Title = "Breaks";
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        Assert.Contains(SkUiSemanticsTree.Build(root).Flatten(), node => node.Label == "Page");
        sheet.IsOpen = true;
        Settle(surface, ref time);
        var tree = SkUiSemanticsTree.Build(root, exposeAutomationIds: true).Flatten().ToList();
        Assert.DoesNotContain(tree, node => node.Label == "Page");
        Assert.Contains(tree, node => node.Label == "Breaks");
        Assert.Contains(tree, node => node.Label == SkUiStrings.Current.SheetGrabberText && node.AutomationId == "Sheet" + SkUiSheet.GrabberAutomationIdSuffix);
        Assert.Contains(tree, node => node.AutomationId == "Sheet" + SkUiSheet.ScrimAutomationIdSuffix);

        // Not modal: the page stays, and nothing dims it.
        sheet.IsModal = false;
        Assert.Contains(SkUiSemanticsTree.Build(root).Flatten(), node => node.Label == "Page");
        sheet.IsOpen = false;
        Settle(surface, ref time);
        Assert.Contains(SkUiSemanticsTree.Build(root).Flatten(), node => node.Label == "Page");
    }

    [Fact]
    public void GrabberStepsThroughTheDetents()
    {
        var root = Page(out var sheet, out _);
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.IsOpen = true;
        Settle(surface, ref time);
        Tap(root, new Point(200, 405)); // the grabber's band at the sheet's top
        Settle(surface, ref time);
        Assert.Equal(1, sheet.SelectedDetentIndex);
        Assert.Equal(800, sheet.Position, 1);
        Tap(root, new Point(200, 5));
        Settle(surface, ref time);
        Assert.Equal(0, sheet.SelectedDetentIndex); // round to the lowest
    }

    [Fact]
    public void LeavingTheTreeWhileOpenLetsGoOfTheSurfaceUntilItIsBack()
    {
        var root = Page(out var sheet, out var page);
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.IsOpen = true;
        Settle(surface, ref time);
        Assert.Same(sheet, root.ModalPane);
        root.Children.Remove(sheet);
        Assert.Null(root.ModalPane);
        Assert.True(sheet.IsOpen); // moved views are removed and added again: it stays open
        Assert.True(page.Focus());
        root.Children.Add(sheet);
        Assert.Same(sheet, root.ModalPane);
        Assert.False(page.IsFocused);
    }

    [Fact]
    public void OpenedBeforeItIsInASurfaceItIsThePagesModalPaneOnceLaidOut()
    {
        // XAML and bindings set IsOpen while the tree is being built, before the page's root exists.
        var sheet = new SkUiSheet { IsOpen = true, Content = new SkUiButton { Text = "Inside" } };
        var inner = new SkUiGrid { Children = { sheet } };
        var page = new SkUiButton { Text = "Page" };
        var root = new SkUiGrid { Children = { page, inner } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        Settle(surface, ref time);
        Assert.Same(sheet, root.ModalPane);
        Assert.Null(inner.ModalPane);
        Assert.Equal(400, sheet.Position, 1);
        Assert.DoesNotContain(SkUiSemanticsTree.Build(root).Flatten(), node => node.Label == "Page");
        // Drawn, hit and read on the page's overlay layer, not on the layer of the grid it was in before the grid joined the page.
        Assert.Equal([sheet], root.SurfaceOverlay.Shown);
        var inside = (SkUiButton)sheet.Content!;
        Assert.Contains(SkUiSemanticsTree.Build(root).Flatten(), node => node.Label == "Inside");
        var bounds = SkUiDiagnostics.GetRootBounds(inside)!.Value;
        var at = new Point(bounds.Center.X, bounds.Top + 20); // the sheet rests at its half detent: the button's top part shows
        Assert.Same(inside, SkUiDiagnostics.HitTest(root, at));
        var taps = 0;
        inside.Clicked += (_, _) => taps++;
        Tap(root, at);
        Assert.Equal(1, taps);

        // Moved to another surface while open: the modal pane goes with it.
        var other = new SkUiGrid();
        root.Children.Remove(inner);
        Assert.Null(root.ModalPane);
        other.Children.Add(inner);
        Assert.True(sheet.IsOpen);
        Assert.Same(sheet, other.ModalPane);
    }

    [Fact]
    public async Task CaughtWhileOpeningItRestsWhereTheDragLeavesIt()
    {
        var root = Page(out var sheet, out _);
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        var opened = 0;
        sheet.Opened += (_, _) => opened++;
        var opening = sheet.OpenAsync();
        surface.Frame(time += 16);
        surface.Frame(time += 100);
        var caughtAt = sheet.Position;
        Assert.InRange(caughtAt, 1, 399);
        // The finger catches it and drags it up to the full detent.
        Drag(root, new Point(200, Height - caughtAt + 30), new Point(200, 60), startMs: time);
        Assert.Equal(0, opened); // not open while held
        Settle(surface, ref time);
        await opening.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, opened);
        Assert.Equal(800, sheet.Position, 1);
        Assert.Equal(1, sheet.SelectedDetentIndex);
    }

    [Fact]
    public async Task OpenedAgainWhileClosingTheCloseIsDone()
    {
        var root = Page(out var sheet, out _);
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.IsOpen = true;
        Settle(surface, ref time);
        var closed = 0;
        sheet.Closed += (_, _) => closed++;
        var closing = sheet.CloseAsync();
        surface.Frame(time += 16);
        surface.Frame(time += 60);
        var opening = sheet.OpenAsync();
        await closing.WaitAsync(TimeSpan.FromSeconds(5));
        Settle(surface, ref time);
        await opening.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, closed);
        Assert.True(sheet.IsOpen);
        Assert.Equal(400, sheet.Position, 1);
    }

    [Fact]
    public void DetentListChangesAreFollowed()
    {
        var detents = new System.Collections.ObjectModel.ObservableCollection<SkUiSheetDetent> { SkUiSheetDetent.Fraction(0.5) };
        var root = Page(out var sheet, out _);
        sheet.Detents = detents;
        SkUiTestHelpers.Arrange(root, Width, Height);
        sheet.IsOpen = true;
        SkUiTestHelpers.Arrange(root, Width, Height);
        Assert.Equal(400, sheet.Position, 1);
        detents[0] = SkUiSheetDetent.Fraction(0.25);
        SkUiTestHelpers.Arrange(root, Width, Height);
        Assert.Equal(200, sheet.Position, 1);

    }

    [Fact]
    public async Task DisabledWhileClosingItStillCloses()
    {
        // A layout around the sheet disabled mid-motion (a page busy while it saves) stops the track's scrolling: not a rest where a
        // drag left it.
        var root = Page(out var sheet, out _);
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.IsOpen = true;
        Settle(surface, ref time);
        var closed = new List<SkUiSheetClosedEventArgs>();
        sheet.Closed += (_, args) => closed.Add(args);
        var closing = sheet.CloseAsync();
        surface.Frame(time += 16);
        surface.Frame(time += 60);
        root.IsEnabled = false;
        Settle(surface, ref time);
        await closing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(sheet.IsOpen);
        Assert.Equal(0, sheet.Position, 1);
        Assert.True(Assert.Single(closed).IsDismissed);
    }

    [Fact]
    public async Task DisabledWhileOpeningItStillOpens()
    {
        var root = Page(out var sheet, out _);
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        var closed = 0;
        sheet.Closed += (_, _) => closed++;
        var opening = sheet.OpenAsync();
        surface.Frame(time += 16);
        surface.Frame(time += 60);
        Assert.InRange(sheet.Position, 1, 399); // on its way
        root.IsEnabled = false; // the page behind the sheet disabled while it opens
        Settle(surface, ref time);
        await opening.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(sheet.IsOpen);
        Assert.Equal(0, closed);
        Assert.Equal(400, sheet.Position, 1);
    }

    [Fact]
    public void WheeledClosedItCloses()
    {
        // Wheel and trackpad scrolling never sets the track moving; landing closed needs no spring, so the rest comes from the wheel's
        // pause (what its idle delay calls: headless tests have no dispatcher).
        var root = Page(out var sheet, out _);
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.IsOpen = true;
        Settle(surface, ref time);
        var closed = new List<SkUiSheetClosedEventArgs>();
        sheet.Closed += (_, args) => closed.Add(args);
        sheet.TrackController.Wheel(0, 500);
        sheet.TrackController.OnWheelRest();
        Settle(surface, ref time);
        Assert.False(sheet.IsOpen);
        Assert.True(Assert.Single(closed).IsDismissed);
        Assert.Null(root.ModalPane);
    }

    [Fact]
    public async Task ADialogClosedAfterItsSurfaceIsGoneCloses()
    {
        var root = Page(out var sheet, out _);
        sheet.Presentation = SkUiSheetPresentation.Dialog;
        var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.IsOpen = true;
        Settle(surface, ref time);
        surface.Dispose(); // the page is gone: no more frames
        await sheet.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(sheet.IsOpen);
    }
}
