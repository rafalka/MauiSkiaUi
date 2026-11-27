using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// S6 results: <see cref="SkUiSheet.Close"/> with a result, <see cref="SkUiSheet.Closed"/>'s arguments, and
/// <see cref="SkUiSheet.ShowAsync{TResult}"/> completing with the result, <c>default</c> when dismissed, of another type or cancelled;
/// the header (title, description) and the action row.
/// </summary>
public class SheetResultTests
{
    private const int Width = 400;
    private const int Height = 800;

    private static void Settle(SkUiTestSurface surface, ref double time)
    {
        for (var frame = 0; frame < 60; frame++)
            surface.Frame(time += 16);
    }

    private static (SkUiGrid Root, SkUiButton Anchor) Page()
    {
        var anchor = new SkUiButton { Text = "Open" };
        return (new SkUiGrid { Children = { anchor } }, anchor);
    }

    [Fact]
    public async Task ShowAsyncCompletesWithTheResultGivenToClose()
    {
        var (root, anchor) = Page();
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        var yes = new SkUiButton { Text = "Delete" };
        SkUiSheet? shown = null;
        var task = SkUiSheet.ShowAsync<bool>(anchor, new SkUiLabel { Text = "Delete the shift?" }, sheet =>
        {
            shown = sheet;
            sheet.Presentation = SkUiSheetPresentation.Dialog;
            sheet.Title = "Delete";
            sheet.Actions = yes;
            yes.Clicked += (_, _) => sheet.Close(true);
        });
        Settle(surface, ref time);
        Assert.False(task.IsCompleted);
        SkUiSheetClosedEventArgs? closed = null;
        shown!.Closed += (_, args) => closed = args;
        ((ISkUiAccessibleNode)yes).PerformSemanticsAction(SkUiSemanticsActions.Activate);
        Settle(surface, ref time);
        Assert.True(await task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.NotNull(closed);
        Assert.False(closed.IsDismissed);
        Assert.Equal(true, closed.Result);
    }

    [Fact]
    public async Task DismissedOrOfAnotherTypeIsDefault()
    {
        var (root, anchor) = Page();
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);

        SkUiSheet? shown = null;
        var task = SkUiSheet.ShowAsync<int>(anchor, new SkUiBox(), sheet => shown = sheet);
        Settle(surface, ref time);
        shown!.Close("not an int");
        Settle(surface, ref time);
        Assert.Equal(0, await task.WaitAsync(TimeSpan.FromSeconds(5)));

        var dismissed = SkUiSheet.ShowAsync<string>(anchor, new SkUiBox(), sheet => shown = sheet);
        Settle(surface, ref time);
        await shown.CloseAsync().WaitAsync(TimeSpan.FromSeconds(1)).ContinueWith(_ => { }); // completes once the frames below ran
        Settle(surface, ref time);
        Assert.Null(await dismissed.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task CancellingTheTokenDismisses()
    {
        var (root, anchor) = Page();
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        using var cancel = new CancellationTokenSource();
        var task = SkUiSheet.ShowAsync<string>(anchor, new SkUiBox(), sheet => sheet.Close("never"), cancel.Token);
        // Closed with a result in configure, before it opened: closing a closed sheet does nothing, so it opens.
        Settle(surface, ref time);
        Assert.False(task.IsCompleted);
        cancel.Cancel();
        Settle(surface, ref time);
        Assert.Null(await task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(root.SurfaceOverlay.HasViews);

        Assert.Null(await SkUiSheet.ShowAsync<string>(anchor, new SkUiBox(), cancellationToken: cancel.Token));
    }

    [Fact]
    public async Task ATokenCancelledBeforeItOpensClosesIt()
    {
        // Cancelled after the check at the start but before the sheet opens (here: while it is configured).
        var (root, anchor) = Page();
        using var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        using var cancel = new CancellationTokenSource();
        var task = SkUiSheet.ShowAsync<string>(anchor, new SkUiBox(), _ => cancel.Cancel(), cancel.Token);
        Settle(surface, ref time);
        Assert.Null(await task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(root.SurfaceOverlay.HasViews);
    }

    [Fact]
    public async Task ShowAsyncCompletesWhenTheSurfaceIsReleased()
    {
        var (root, anchor) = Page();
        var surface = new SkUiTestSurface(root, Width, Height);
        var time = 0d;
        surface.Frame(time);
        var task = SkUiSheet.ShowAsync<string>(anchor, new SkUiBox(), sheet => sheet.Presentation = SkUiSheetPresentation.Dialog);
        Settle(surface, ref time);
        Assert.False(task.IsCompleted);
        surface.Dispose(); // the page is gone
        Assert.Null(await task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(root.SurfaceOverlay.HasViews);
    }

    [Fact]
    public void HeaderAndActionsSitAroundTheContent()
    {
        var content = new SkUiBox { HeightRequest = 100 };
        var actions = new SkUiButton { Text = "Done" };
        var sheet = new SkUiSheet
        {
            Content = content,
            Title = "Shift",
            Description = "Thursday, 8:00 to 16:00",
            Actions = actions,
            Detents = [SkUiSheetDetent.FitContent]
        };
        var root = new SkUiGrid { Children = { sheet } };
        SkUiTestHelpers.Arrange(root, Width, Height);
        sheet.IsOpen = true;
        SkUiTestHelpers.Arrange(root, Width, Height);
        var tree = SkUiSemanticsTree.Build(root).Flatten().Select(node => node.Label).ToList();
        var title = tree.IndexOf("Shift");
        Assert.True(title >= 0);
        Assert.True(tree.IndexOf("Thursday, 8:00 to 16:00") > title);
        Assert.True(tree.IndexOf("Done") > title);
        var contentTop = SkUiDiagnostics.GetRootBounds(content)!.Value.Top;
        var actionsTop = SkUiDiagnostics.GetRootBounds(actions)!.Value.Top;
        Assert.True(actionsTop > contentTop + 99);
        // The fitted sheet holds all three.
        Assert.True(sheet.Position > 100 + actions.Height);

        sheet.Title = null;
        sheet.Description = null;
        sheet.Actions = null;
        SkUiTestHelpers.Arrange(root, Width, Height);
        Assert.DoesNotContain("Shift", SkUiSemanticsTree.Build(root).Flatten().Select(node => node.Label));
        Assert.Null(actions.Parent?.Parent);
    }

    [Fact]
    public void NativeViewsAreRefused()
    {
        var sheet = new SkUiSheet();
        Assert.Throws<ArgumentException>(() => sheet.SetContent(new Label()));
        Assert.Throws<ArgumentException>(() => sheet.SetActions(new Label()));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.SetSelectedDetentIndex(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.SetCornerRadius(-2));
        Assert.Same(sheet, sheet.SetDetents(SkUiSheetDetent.FitContent).SetEdge(SkUiSheetEdge.End).SetTitle("T"));
        Assert.Equal([SkUiSheetDetent.FitContent], sheet.Detents);
    }

    [Fact]
    public void OneWayBindingsSurviveTheUserClosingTheSheet()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var sheet = new SkUiSheet();
        var source = BindingSource<bool>.Bind(sheet, SkUiSheet.IsOpenProperty, true);
        var detent = BindingSource<int>.Bind(sheet, SkUiSheet.SelectedDetentIndexProperty, 0);
        var root = new SkUiGrid { Children = { sheet } };
        SkUiTestHelpers.Arrange(root, Width, Height);
        Assert.True(sheet.IsOpen);
        Assert.True(sheet.StepDetent(up: true, wraps: false)); // the user moves it up
        Assert.Equal(1, sheet.SelectedDetentIndex);
        sheet.Dismiss();
        Assert.False(sheet.IsOpen);
        source.Value = false;
        source.Value = true;
        Assert.True(sheet.IsOpen); // the bindings still hold
        detent.Value = 1;
        detent.Value = 0;
        Assert.Equal(0, sheet.SelectedDetentIndex);
    }
}
