using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// S7 <see cref="SkUiSheetPopupPresenter"/>: a picker's popup opens in a sheet on the surface root's overlay layer, commits a choice
/// and closes; dismissing the sheet closes the popup without one; the content is let go of afterwards. In the global collection: the
/// app-wide presenter is process-wide.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class SheetPopupPresenterTests : IDisposable
{
    private static readonly string[] Fruits = ["Apple", "Pear", "Plum", "Fig"];
    private static long _pointer = 190_000;

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public SheetPopupPresenterTests() => SkUiPopups.Presenter = new SkUiSheetPopupPresenter();

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    private static void Settle(SkUiTestSurface surface, ref double time)
    {
        for (var frame = 0; frame < 60; frame++)
            surface.Frame(time += 16);
    }

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    private static (SkUiVerticalStackLayout Root, SkUiPicker Picker) Page()
    {
        var picker = new SkUiPicker { Title = "Fruit", PopupPlacement = SkUiPopupPlacement.Window };
        foreach (var fruit in Fruits)
            picker.Items.Add(fruit);
        return (new SkUiVerticalStackLayout { Children = { picker } }, picker);
    }

    [Fact]
    public void APickerOpensInASheetAndCommitsThroughIt()
    {
        var (root, picker) = Page();
        using var surface = new SkUiTestSurface(root, 400, 800);
        var time = 0d;
        surface.Frame(time);

        picker.IsOpen = true;
        _ui.RunPending();
        Settle(surface, ref time);
        var sheet = Assert.IsType<SkUiSheet>(Assert.Single(root.SurfaceOverlay.Views));
        Assert.True(sheet.IsOpen);
        Assert.Same(sheet, root.ModalPane);
        var content = Assert.IsType<SkUiPickerPopup>(sheet.Content);
        Assert.Equal(400, content.Width, 1); // the sheet's width
        Assert.True(sheet.Position > content.Height - 1); // fitted

        // A tap on the third row chooses it.
        var rows = SkUiSemanticsTree.Build(root).Flatten().Where(node => node.Label == "Plum").ToList();
        var row = Assert.Single(rows);
        Tap(root, new Point(row.Bounds.Center.X, row.Bounds.Center.Y));
        _ui.RunPending();
        Settle(surface, ref time);
        _ui.RunPending();
        Assert.Equal(2, picker.SelectedIndex);
        Assert.False(picker.IsOpen);
        Assert.False(root.SurfaceOverlay.HasViews);
        Assert.Null(content.Parent);
        Assert.Null(root.ModalPane);
    }

    [Fact]
    public void DismissingTheSheetClosesThePopupWithoutAChoice()
    {
        var (root, picker) = Page();
        picker.SelectedIndex = 1;
        using var surface = new SkUiTestSurface(root, 400, 800);
        var time = 0d;
        surface.Frame(time);
        picker.IsOpen = true;
        _ui.RunPending();
        Settle(surface, ref time);
        var sheet = Assert.IsType<SkUiSheet>(Assert.Single(root.SurfaceOverlay.Views));

        Tap(root, new Point(200, 20)); // the scrim
        Settle(surface, ref time);
        _ui.RunPending();
        Assert.False(sheet.IsOpen);
        Assert.False(picker.IsOpen);
        Assert.Equal(1, picker.SelectedIndex);
        Assert.False(root.SurfaceOverlay.HasViews);

        // Closed by the control (IsOpen = false): the sheet goes too.
        picker.IsOpen = true;
        _ui.RunPending();
        Settle(surface, ref time);
        Assert.True(root.SurfaceOverlay.HasViews);
        picker.IsOpen = false;
        _ui.RunPending();
        Settle(surface, ref time);
        _ui.RunPending();
        Assert.False(root.SurfaceOverlay.HasViews);
    }

    [Fact]
    public void APickerInASheetOpensItsListInASheetOnTop()
    {
        var picker = new SkUiPicker { Title = "Fruit", PopupPlacement = SkUiPopupPlacement.Window, AutomationId = "FruitField" };
        foreach (var fruit in Fruits)
            picker.Items.Add(fruit);
        var form = new SkUiSheet { Content = new SkUiVerticalStackLayout { Children = { picker } }, Detents = [SkUiSheetDetent.Fraction(1)] };
        var root = new SkUiGrid { Children = { new SkUiButton { Text = "Page" }, form } };
        using var surface = new SkUiTestSurface(root, 400, 800);
        var time = 0d;
        surface.Frame(time);
        form.IsOpen = true;
        Settle(surface, ref time);

        picker.IsOpen = true;
        _ui.RunPending();
        Settle(surface, ref time);
        var popup = Assert.IsType<SkUiSheet>(Assert.Single(root.SurfaceOverlay.Views));
        Assert.Equal([form, popup], root.SurfaceOverlay.Shown); // over the form
        Assert.Same(popup, root.ModalPane);
        var nodes = SkUiSemanticsTree.Build(root, exposeAutomationIds: true).Flatten().ToList();
        Assert.Contains(nodes, node => node.Label == "Pear");
        Assert.DoesNotContain(nodes, node => node.AutomationId == "FruitField"); // the form's field is behind the popup's modal pane

        var row = Assert.Single(SkUiSemanticsTree.Build(root).Flatten(), node => node.Label == "Pear");
        Tap(root, new Point(row.Bounds.Center.X, row.Bounds.Center.Y));
        _ui.RunPending();
        Settle(surface, ref time);
        _ui.RunPending();
        Assert.Equal(1, picker.SelectedIndex);
        Assert.Equal([form], root.SurfaceOverlay.Shown);
        Assert.True(form.IsOpen);
        Assert.Same(form, root.ModalPane);
    }
}
