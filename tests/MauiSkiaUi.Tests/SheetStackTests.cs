using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// S5 stacking: several sheets open at once, the last opened on top and the only modal pane; Back, Escape and a scrim tap close the
/// topmost; closing it hands the modal pane back to the one below. Sheets opened by <see cref="SkUiSheet.ShowAsync{TResult}"/> sit on
/// the surface root's overlay layer, above everything in the surface.
/// </summary>
public class SheetStackTests
{
    private const int Width = 400;
    private const int Height = 800;
    private static long _pointer = 180_000;

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    private static void Settle(SkUiTestSurface surface, ref double time)
    {
        for (var frame = 0; frame < 60; frame++)
            surface.Frame(time += 16);
    }

    [Fact]
    public void TheLastOpenedIsTheModalPaneAndClosesFirst()
    {
        var page = new SkUiButton { Text = "Page" };
        var lower = new SkUiSheet { Content = new SkUiButton { Text = "Lower" } };
        var upper = new SkUiSheet { Content = new SkUiButton { Text = "Upper" }, Detents = [SkUiSheetDetent.Fraction(0.25)] };
        var root = new SkUiGrid { Children = { page, lower, upper } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        lower.IsOpen = true;
        Settle(surface, ref time);
        upper.IsOpen = true;
        Settle(surface, ref time);
        Assert.Same(upper, root.ModalPane);
        var labels = SkUiSemanticsTree.Build(root).Flatten().Select(node => node.Label).ToList();
        Assert.Contains("Upper", labels);
        Assert.DoesNotContain("Lower", labels);
        Assert.DoesNotContain("Page", labels);

        // A tap on the upper sheet's scrim, over the lower sheet, closes only the upper one.
        Tap(root, new Point(200, 500));
        Assert.False(upper.IsOpen);
        Assert.True(lower.IsOpen);
        Settle(surface, ref time);
        Assert.Same(lower, root.ModalPane);
        Assert.True(upper.OnBackPressed() is false); // closed: not the topmost
        Assert.True(lower.OnBackPressed());
        Assert.False(lower.IsOpen);
        Settle(surface, ref time);
        Assert.Null(root.ModalPane);
    }

    [Fact]
    public void BackGoesToTheTopmostOnly()
    {
        var lower = new SkUiSheet();
        var upper = new SkUiSheet();
        var root = new SkUiGrid { Children = { lower, upper } };
        SkUiTestHelpers.Arrange(root, Width, Height);
        lower.IsOpen = true;
        upper.IsOpen = true;
        SkUiTestHelpers.Arrange(root, Width, Height);
        Assert.False(lower.OnBackPressed());
        Assert.True(lower.IsOpen);
        Assert.True(upper.OnBackPressed());
        Assert.False(upper.IsOpen);
        Assert.True(lower.OnBackPressed());
    }

    [Fact]
    public void ShownSheetsGoOnTheOverlayLayerAboveEverything()
    {
        var page = new SkUiButton { Text = "Page" };
        var xamlSheet = new SkUiSheet { Content = new SkUiButton { Text = "Placed" } };
        var root = new SkUiGrid { Children = { page, xamlSheet } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        xamlSheet.IsOpen = true;
        Settle(surface, ref time);

        SkUiSheet? shown = null;
        var task = SkUiSheet.ShowAsync<string>(page, new SkUiButton { Text = "Shown" }, sheet => shown = sheet);
        Settle(surface, ref time);
        Assert.NotNull(shown);
        Assert.True(shown.IsOpen);
        Assert.Same(shown, root.ModalPane);
        Assert.Same(root.SurfaceOverlay, shown.Parent);
        var children = new List<Rendering.ISkUiRenderable>();
        ((Rendering.ISkUiRenderable)root).GetRenderChildren(children);
        Assert.Same(root.SurfaceOverlay, children[^1]);
        Assert.Equal([xamlSheet, shown], root.SurfaceOverlay.Shown); // the placed sheet is drawn there too, below the later one
        Assert.Equal(400, shown.Position, 1);

        Assert.True(root.FocusManager.KeyDown(SkUiKey.Escape) || shown.OnBackPressed());
        Settle(surface, ref time);
        Assert.True(task.IsCompleted);
        Assert.Null(task.Result);
        Assert.Null(shown.Parent); // off the layer once closed
        Assert.Empty(root.SurfaceOverlay.Views);
        Assert.Equal([xamlSheet], root.SurfaceOverlay.Shown);
        Assert.Same(xamlSheet, root.ModalPane);

        // Closed, the placed sheet goes back to its layout, and the empty layer out of the root's drawn children.
        xamlSheet.IsOpen = false;
        Settle(surface, ref time);
        Assert.False(root.SurfaceOverlay.HasViews);
        children.Clear();
        ((Rendering.ISkUiRenderable)root).GetRenderChildren(children);
        Assert.DoesNotContain(root.SurfaceOverlay, children);
        Assert.Contains(xamlSheet, children);
    }

    [Fact]
    public void EveryOverlaySheetIsDrawnNotOnlyTheFirst()
    {
        // The overlay layer leaves the root's drawn children when its last sheet closes; the next sheet must not sync into the
        // render node the compositor let go of (it was open and took taps, but nothing of it was drawn).
        var anchor = new SkUiButton { Text = "Open" };
        var root = new SkUiGrid { BackgroundColor = Colors.White, Children = { anchor } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        var page = surface.Bitmap.GetPixel(200, 20);
        for (var round = 0; round < 3; round++)
        {
            SkUiSheet? shown = null;
            _ = SkUiSheet.ShowAsync<string>(anchor, new SkUiBox { HeightRequest = 100, Color = Colors.Red }, sheet =>
            {
                shown = sheet;
                sheet.Detents = [SkUiSheetDetent.FitContent];
            });
            Settle(surface, ref time);
            Assert.True(shown!.IsOpen);
            Assert.NotEqual(page, surface.Bitmap.GetPixel(200, 20)); // the scrim dims the page
            Assert.Equal(new SkiaSharp.SKColor(255, 0, 0), surface.Bitmap.GetPixel(200, Height - 40)); // the content shows
            shown.Dismiss();
            Settle(surface, ref time);
            Assert.Equal(page, surface.Bitmap.GetPixel(200, 20));
        }
    }

    /// <summary>Whether <paramref name="color"/> is the red the test content draws.</summary>
    private static bool IsRed(SkiaSharp.SKColor color) => color.Red > 200 && color.Green < 110 && color.Blue < 110;

    [Fact]
    public void ASheetPlacedEarlierOpensOverAnOpenSheetPlacedLater()
    {
        // The demo's case: a non-modal form sheet is open (placed last), and a break sheet placed before it opens.
        var choose = new SkUiButton { Text = "Lunch", BackgroundColor = Colors.Red };
        var breaks = new SkUiSheet { Content = choose, Detents = [SkUiSheetDetent.FitContent] };
        var form = new SkUiSheet { Content = new SkUiBox { Color = Colors.Blue }, IsModal = false, Detents = [SkUiSheetDetent.Fraction(1)] };
        var root = new SkUiGrid { BackgroundColor = Colors.White, Children = { new SkUiButton { Text = "Page" }, breaks, form } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        form.IsOpen = true;
        Settle(surface, ref time);
        var chosen = 0;
        breaks.Closed += (_, args) => chosen += args.IsDismissed ? 0 : 1;
        choose.Clicked += (_, _) => breaks.Close("Lunch");
        breaks.IsOpen = true;
        Settle(surface, ref time);

        Assert.Equal([form, breaks], root.SurfaceOverlay.Shown);
        var row = SkUiDiagnostics.GetRootBounds(choose)!.Value;
        Assert.True(IsRed(surface.Bitmap.GetPixel((int)row.Center.X, (int)row.Center.Y))); // drawn over the form
        Tap(root, row.Center);
        Settle(surface, ref time);
        Assert.Equal(1, chosen); // and it takes the tap: nothing waits on a covered sheet
        Assert.Equal([form], root.SurfaceOverlay.Shown);
        Assert.True(form.IsOpen);
    }

    [Fact]
    public void ASheetPlacedInsideAnotherSheetIsDrawnOverIt()
    {
        var inner = new SkUiSheet { Content = new SkUiBox { HeightRequest = 100, Color = Colors.Red }, Detents = [SkUiSheetDetent.FitContent] };
        var open = new SkUiButton { Text = "More" };
        open.Clicked += (_, _) => inner.IsOpen = true;
        // The outer sheet's content is a grid with the inner sheet as its last child, as XAML places one.
        var outer = new SkUiSheet { Content = new SkUiGrid { HeightRequest = 300, Children = { open, inner } }, Detents = [SkUiSheetDetent.FitContent] };
        var root = new SkUiGrid { BackgroundColor = Colors.White, Children = { new SkUiButton { Text = "Page" }, outer } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        outer.IsOpen = true;
        Settle(surface, ref time);
        Tap(root, SkUiDiagnostics.GetRootBounds(open)!.Value.Center);
        Settle(surface, ref time);

        Assert.True(inner.IsOpen);
        Assert.Same(inner, root.ModalPane);
        Assert.Equal([outer, inner], root.SurfaceOverlay.Shown);
        // Covering the layout it is placed in (the outer sheet's content), drawn over the outer sheet.
        var innerBounds = SkUiDiagnostics.GetRootBounds(inner)!.Value;
        Assert.Equal(SkUiDiagnostics.GetRootBounds((SkUiView)outer.Content!)!.Value, innerBounds);
        Assert.True(IsRed(surface.Bitmap.GetPixel(Width / 2, (int)innerBounds.Bottom - 40)));
        var semantics = SkUiSemanticsTree.Build(root).Flatten().Select(node => node.Label).ToList();
        Assert.DoesNotContain("More", semantics); // the outer sheet is behind the modal pane
        inner.Dismiss();
        Settle(surface, ref time);
        Assert.Same(outer, root.ModalPane);
        Assert.Contains("More", SkUiSemanticsTree.Build(root).Flatten().Select(node => node.Label));
    }

    [Fact]
    public async Task ShowAsyncFromASheetsContentGoesOverIt()
    {
        var anchor = new SkUiButton { Text = "Delete…" };
        var placed = new SkUiSheet { Content = anchor, Detents = [SkUiSheetDetent.FitContent] };
        var root = new SkUiGrid { BackgroundColor = Colors.White, Children = { new SkUiButton { Text = "Page" }, placed } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        placed.IsOpen = true;
        Settle(surface, ref time);

        SkUiSheet? confirm = null;
        var yes = new SkUiButton { Text = "Yes", BackgroundColor = Colors.Red };
        var task = SkUiSheet.ShowAsync<bool>(anchor, yes, sheet =>
        {
            confirm = sheet;
            sheet.Presentation = SkUiSheetPresentation.Dialog;
            yes.Clicked += (_, _) => sheet.Close(true);
        });
        Settle(surface, ref time);
        Assert.Equal([placed, confirm!], root.SurfaceOverlay.Shown);
        Assert.Same(confirm, root.ModalPane);
        var yesBounds = SkUiDiagnostics.GetRootBounds(yes)!.Value;
        Assert.True(IsRed(surface.Bitmap.GetPixel((int)yesBounds.Center.X, (int)yesBounds.Center.Y)));
        Tap(root, yesBounds.Center);
        Settle(surface, ref time);
        Assert.True(await task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(placed.IsOpen);
        Assert.Same(placed, root.ModalPane);
    }

    [Fact]
    public void ReopenedWhileClosingItComesBackOnTop()
    {
        var first = new SkUiSheet { IsModal = false };
        var second = new SkUiSheet { IsModal = false };
        var root = new SkUiGrid { Children = { first, second } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        first.IsOpen = true;
        second.IsOpen = true;
        Settle(surface, ref time);
        Assert.Equal([first, second], root.SurfaceOverlay.Shown);
        first.IsOpen = false;
        surface.Frame(time += 16);
        first.IsOpen = true; // caught before it closed
        Settle(surface, ref time);
        Assert.Equal([second, first], root.SurfaceOverlay.Shown);
    }

    [Fact]
    public void ARootThatScrollsShowsItsSheetsWhereItShows()
    {
        // The overlay layer is pinned to the root: a scrolled root draws and hits its sheets in its viewport, not where its content
        // scrolled to.
        var anchor = new SkUiButton { Text = "Open" };
        var placed = new SkUiSheet { Content = new SkUiBox { HeightRequest = 100, Color = Colors.Blue }, Detents = [SkUiSheetDetent.FitContent] };
        var content = new SkUiGrid { HeightRequest = 3000, Children = { anchor, placed } };
        var root = new SkUiScrollView { BackgroundColor = Colors.White, Content = content };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        _ = root.ScrollToAsync(0, 800, animated: false);
        Settle(surface, ref time);

        SkUiSheet? shown = null;
        var red = new SkUiButton { Text = "Red", HeightRequest = 100, BackgroundColor = Colors.Red };
        var taps = 0;
        red.Clicked += (_, _) => taps++;
        _ = SkUiSheet.ShowAsync<string>(anchor, red, sheet =>
        {
            shown = sheet;
            sheet.Detents = [SkUiSheetDetent.FitContent];
        });
        Settle(surface, ref time);
        Assert.True(shown!.IsOpen);
        var bounds = SkUiDiagnostics.GetRootBounds(red)!.Value;
        Assert.InRange(bounds.Bottom, Height - 120, Height); // at the bottom of the viewport
        Assert.True(IsRed(surface.Bitmap.GetPixel((int)bounds.Center.X, (int)bounds.Center.Y)));
        Tap(root, bounds.Center);
        Assert.Equal(1, taps);
        shown.Dismiss();
        Settle(surface, ref time);

        // A sheet placed in the scrolled content covers its layout where it is now.
        placed.IsOpen = true;
        Settle(surface, ref time);
        Assert.Equal(SkUiDiagnostics.GetRootBounds(content)!.Value, SkUiDiagnostics.GetRootBounds(placed)!.Value);
        Assert.Equal(-800, SkUiDiagnostics.GetRootBounds(placed)!.Value.Y, 1);
    }

    [Fact]
    public void AHoistedSheetFollowsItsLayoutWhenTheSurfaceIsLaidOutAgain()
    {
        var banner = new SkUiBox { HeightRequest = 200, Color = Colors.Gray };
        var sheet = new SkUiSheet { Content = new SkUiBox { Color = Colors.Red }, Detents = [SkUiSheetDetent.Fraction(1)] };
        // The area keeps its size: only its place changes, which the sheet's own arrange does not see.
        var area = new SkUiGrid { HeightRequest = 500, Children = { new SkUiButton { Text = "Page" }, sheet } };
        var root = new SkUiVerticalStackLayout { Children = { banner, area } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.IsOpen = true;
        Settle(surface, ref time);
        Assert.Equal(200, SkUiDiagnostics.GetRootBounds(sheet)!.Value.Y, 1);

        banner.HeightRequest = 50; // the layout around the sheet moves up
        Settle(surface, ref time);
        Assert.Equal(50, SkUiDiagnostics.GetRootBounds(sheet)!.Value.Y, 1);
        Assert.True(IsRed(surface.Bitmap.GetPixel(Width / 2, 150))); // drawn there: where the banner was
    }

    [Fact]
    public void AnOpenSheetInAHiddenLayoutLetsGoOfTheSurfaceUntilShownAgain()
    {
        var page = new SkUiButton { Text = "Page" };
        var sheet = new SkUiSheet { Content = new SkUiButton { Text = "Inside" } };
        var area = new SkUiGrid { Children = { sheet } };
        var root = new SkUiGrid { Children = { page, area } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.IsOpen = true;
        Settle(surface, ref time);
        Assert.Same(sheet, root.ModalPane);
        Assert.Equal([sheet], root.SurfaceOverlay.Shown);

        area.IsVisible = false; // a tab page switched away, a container the app hides
        Settle(surface, ref time);
        Assert.True(sheet.IsOpen);
        Assert.Null(root.ModalPane);
        Assert.False(root.SurfaceOverlay.HasViews);
        Assert.Contains("Page", SkUiSemanticsTree.Build(root).Flatten().Select(node => node.Label));
        Assert.DoesNotContain("Inside", SkUiSemanticsTree.Build(root).Flatten().Select(node => node.Label));

        area.IsVisible = true;
        Settle(surface, ref time);
        Assert.Same(sheet, root.ModalPane);
        Assert.Equal([sheet], root.SurfaceOverlay.Shown);
    }

    [Fact]
    public void ReopenedWhileClosingItsModalPaneGoesBackOnTopToo()
    {
        var anchor = new SkUiButton { Text = "Ask" };
        var placed = new SkUiSheet { Content = anchor, Detents = [SkUiSheetDetent.FitContent] };
        var root = new SkUiGrid { Children = { new SkUiButton { Text = "Page" }, placed } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        placed.IsOpen = true;
        Settle(surface, ref time);
        SkUiSheet? dialog = null;
        _ = SkUiSheet.ShowAsync<bool>(anchor, new SkUiButton { Text = "Sure?" }, sheet =>
        {
            dialog = sheet;
            sheet.Presentation = SkUiSheetPresentation.Dialog;
        });
        Settle(surface, ref time);
        Assert.Same(dialog, root.ModalPane);

        placed.IsOpen = false;
        surface.Frame(time += 16);
        placed.IsOpen = true; // back before it closed: drawn on top, and the modal pane
        Settle(surface, ref time);
        Assert.Equal([dialog!, placed], root.SurfaceOverlay.Shown);
        Assert.Same(placed, root.ModalPane);
    }

    [Fact]
    public void AHiddenSheetThatOpensIsNotTheModalPane()
    {
        var sheet = new SkUiSheet { Content = new SkUiButton { Text = "Inside" } };
        var root = new SkUiGrid { Children = { new SkUiButton { Text = "Page" }, sheet } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        sheet.IsOpen = true; // laid out once
        Settle(surface, ref time);
        sheet.IsOpen = false;
        Settle(surface, ref time);

        sheet.IsVisible = false;
        sheet.IsOpen = true;
        Settle(surface, ref time);
        Assert.Null(root.ModalPane);
        Assert.False(root.SurfaceOverlay.HasViews);
        Assert.Contains("Page", SkUiSemanticsTree.Build(root).Flatten().Select(node => node.Label));

        sheet.IsVisible = true;
        Settle(surface, ref time);
        Assert.Same(sheet, root.ModalPane);
        Assert.Equal([sheet], root.SurfaceOverlay.Shown);
    }

    [Fact]
    public void ShownAgainASheetGoesBackUnderTheSheetsOpenedAfterIt()
    {
        var open = new SkUiButton { Text = "More" };
        var lower = new SkUiSheet { Content = open, Detents = [SkUiSheetDetent.Fraction(1)] };
        var area = new SkUiGrid { Children = { lower } };
        var upper = new SkUiSheet { Content = new SkUiButton { Text = "Upper" }, Detents = [SkUiSheetDetent.FitContent] };
        var root = new SkUiGrid { Children = { new SkUiButton { Text = "Page" }, area, upper } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        lower.IsOpen = true;
        Settle(surface, ref time);
        upper.IsOpen = true; // opened from the lower one
        Settle(surface, ref time);
        Assert.Equal([lower, upper], root.SurfaceOverlay.Shown);

        area.IsVisible = false;
        Settle(surface, ref time);
        Assert.Equal([upper], root.SurfaceOverlay.Shown);
        area.IsVisible = true;
        Settle(surface, ref time);
        Assert.Equal([lower, upper], root.SurfaceOverlay.Shown); // under the one opened after it
        Assert.Same(upper, root.ModalPane);
    }

    [Fact]
    public void BackAndEscapeCloseANonModalSheetOpenedOverTheModalPane()
    {
        var modal = new SkUiSheet { Content = new SkUiButton { Text = "Modal" } };
        var above = new SkUiSheet { Content = new SkUiButton { Text = "Above" }, IsModal = false, Detents = [SkUiSheetDetent.FitContent] };
        var root = new SkUiGrid { Children = { new SkUiButton { Text = "Page" }, modal, above } };
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        modal.IsOpen = true;
        Settle(surface, ref time);
        above.IsOpen = true;
        Settle(surface, ref time);
        Assert.Same(modal, root.ModalPane);
        Assert.Equal([modal, above], root.SurfaceOverlay.Shown);

        Assert.True(modal.OnBackPressed()); // the press reaches the modal pane; the sheet drawn above it closes
        Settle(surface, ref time);
        Assert.False(above.IsOpen);
        Assert.True(modal.IsOpen);

        above.IsOpen = true;
        Settle(surface, ref time);
        root.FocusManager.Focus((SkUiButton)modal.Content!); // keyboard focus is in the modal pane
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Escape));
        Settle(surface, ref time);
        Assert.False(above.IsOpen);
        Assert.True(modal.IsOpen);
        Assert.True(modal.OnBackPressed());
        Assert.False(modal.IsOpen);
    }
}
