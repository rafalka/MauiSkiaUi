using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Whether the drawn tree would use a wheel or trackpad scroll, asked without scrolling: Apple surfaces take a scroll gesture
/// only then and leave it to a native scroller around the surface (a MAUI ScrollView) otherwise.
/// </summary>
public class WheelTakingTests
{
    private static SkUiBox Box(double width, double height) =>
        new() { WidthRequest = width, HeightRequest = height, BackgroundColor = Colors.Gray };

    /// <summary>A drawn surface as a native page holds it: a list 100 high over 200 of content, then a plain box.</summary>
    private static (SkUiVerticalStackLayout Root, SkUiScrollView List) Surface()
    {
        var list = new SkUiScrollView { HeightRequest = 100, Content = Box(300, 200) };
        var root = new SkUiVerticalStackLayout { Children = { list, Box(300, 200) } };
        SkUiTestHelpers.Arrange(root, 300, 300);
        return (root, list);
    }

    /// <summary>Asks, checks that asking moved nothing, then dispatches the same wheel: the dispatch must agree.</summary>
    private static bool AskThenDispatch(SkUiView root, SkUiScrollView list, Point point, double deltaX, double deltaY)
    {
        var before = (list.ScrollX, list.ScrollY);
        var takes = root.Router.TakesWheel(point, deltaX, deltaY);
        Assert.Equal(before, (list.ScrollX, list.ScrollY));
        Assert.Equal(takes, root.Touch(new SkUiTouchEvent(0, SkUiTouchAction.Wheel, point, WheelDelta: deltaY, WheelDeltaX: deltaX)));
        return takes;
    }

    [Fact]
    public void NothingDrawnThatScrollsLeavesTheWheelToThePage()
    {
        var (root, _) = Surface();
        Assert.False(root.Router.TakesWheel(new Point(50, 200), 0, -40));
        Assert.False(root.Router.TakesWheel(new Point(50, 200), 0, 40));
        Assert.False(root.Touch(new SkUiTouchEvent(0, SkUiTouchAction.Wheel, new Point(50, 200), WheelDelta: -40)));
    }

    [Fact]
    public void AListTakesTheWheelUntilItsEndThenLeavesIt()
    {
        var (root, list) = Surface();
        var point = new Point(50, 50);
        // At its top: up is the page's, down is the list's.
        Assert.False(AskThenDispatch(root, list, point, 0, 40));
        Assert.True(AskThenDispatch(root, list, point, 0, -60));
        Assert.Equal(60, list.ScrollY);
        // The step that reaches the end is still the list's; the next one is not.
        Assert.True(AskThenDispatch(root, list, point, 0, -60));
        Assert.Equal(100, list.ScrollY);
        Assert.False(AskThenDispatch(root, list, point, 0, -60));
        Assert.True(AskThenDispatch(root, list, point, 0, 30));
        Assert.Equal(70, list.ScrollY);
    }

    [Fact]
    public void AHorizontalListTakesAVerticalWheelAndNotAWheelAcross()
    {
        var row = new SkUiHorizontalStackLayout();
        for (var i = 0; i < 10; i++)
            row.Children.Add(Box(100, 50));
        var list = new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, Content = row };
        var root = new SkUiVerticalStackLayout { Children = { list } };
        SkUiTestHelpers.Arrange(root, 300, 100);
        // A mouse wheel (vertical only) scrolls a horizontal-only list, as the dispatch does.
        Assert.True(AskThenDispatch(root, list, new Point(50, 25), 0, -40));
        Assert.Equal(40, list.ScrollX);
        // A trackpad's vertical scroll with some sideways drift away from the start: only the horizontal axis can move.
        Assert.True(root.Router.TakesWheel(new Point(50, 25), -5, -40));
        // A trackpad's horizontal scroll towards the start at the start is not the list's.
        Assert.True(AskThenDispatch(root, list, new Point(50, 25), 0, 40));
        Assert.Equal(0, list.ScrollX);
        Assert.False(root.Router.TakesWheel(new Point(50, 25), 30, 0));
    }

    [Fact]
    public void AListThatDoesNotScrollDoesNotTakeTheWheel()
    {
        var (root, list) = Surface();
        list.Orientation = ScrollOrientation.Neither;
        Assert.False(AskThenDispatch(root, list, new Point(50, 50), 0, -40));
    }

    [Fact]
    public void ADisabledControlPassesTheWheelOn()
    {
        // In a drawn list: the list scrolls. Outside one: the page's (nothing drawn takes it).
        var inList = new SkUiButton { Text = "Submit", IsEnabled = false, HeightRequest = 200 };
        var list = new SkUiScrollView { HeightRequest = 100, Content = inList };
        var outside = new SkUiButton { Text = "Submit", IsEnabled = false, HeightRequest = 100 };
        var root = new SkUiVerticalStackLayout { Children = { list, outside } };
        SkUiTestHelpers.Arrange(root, 300, 300);
        Assert.True(AskThenDispatch(root, list, new Point(50, 50), 0, -40));
        Assert.Equal(40, list.ScrollY);
        Assert.False(AskThenDispatch(root, list, new Point(50, 150), 0, -40));
        // A disabled list does not scroll.
        list.IsEnabled = false;
        Assert.False(AskThenDispatch(root, list, new Point(50, 50), 0, -40));
        Assert.Equal(40, list.ScrollY);
    }

    [Fact]
    public void AnInputTransparentListPassesTheWheelOn()
    {
        var (root, list) = Surface();
        list.InputTransparent = true;
        Assert.False(AskThenDispatch(root, list, new Point(50, 50), 0, -40));
        // Inside a drawn scroller, that one takes it.
        var (inner, outer, nested) = Nested();
        inner.InputTransparent = true;
        Assert.True(nested.Router.TakesWheel(new Point(50, 50), 0, -40));
        Assert.True(nested.Touch(new SkUiTouchEvent(0, SkUiTouchAction.Wheel, new Point(50, 50), WheelDelta: -40)));
        Assert.Equal((0.0, 40.0), (inner.ScrollY, outer.ScrollY));
    }

    [Fact]
    public void ADisabledListInsideADrawnScrollerLeavesTheWheelToIt()
    {
        var (inner, outer, root) = Nested();
        inner.IsEnabled = false;
        Assert.True(root.Router.TakesWheel(new Point(50, 50), 0, -40));
        Assert.True(root.Touch(new SkUiTouchEvent(0, SkUiTouchAction.Wheel, new Point(50, 50), WheelDelta: -40)));
        Assert.Equal((0.0, 40.0), (inner.ScrollY, outer.ScrollY));
    }

    /// <summary>A list 100 high over 200 of content, inside a scroller 200 high over 500.</summary>
    private static (SkUiScrollView Inner, SkUiScrollView Outer, SkUiVerticalStackLayout Root) Nested()
    {
        var inner = new SkUiScrollView { HeightRequest = 100, Content = Box(300, 200) };
        var outer = new SkUiScrollView { HeightRequest = 200, Content = new SkUiVerticalStackLayout { Children = { inner, Box(300, 400) } } };
        var root = new SkUiVerticalStackLayout { Children = { outer } };
        SkUiTestHelpers.Arrange(root, 300, 200);
        return (inner, outer, root);
    }

    [Fact]
    public void AnInnerListAtItsEndLeavesTheWheelToAnOuterDrawnScroller()
    {
        var (inner, outer, root) = Nested();
        // Up at both tops: nothing drawn moves.
        Assert.False(AskThenDispatch(root, inner, new Point(50, 50), 0, 40));
        root.Touch(new SkUiTouchEvent(0, SkUiTouchAction.Wheel, new Point(50, 50), WheelDelta: -100));
        Assert.Equal((100.0, 0.0), (inner.ScrollY, outer.ScrollY));
        // Down with the inner list at its end: the outer scroller takes it.
        Assert.True(AskThenDispatch(root, inner, new Point(50, 50), 0, -40));
        Assert.Equal(40, outer.ScrollY);
    }

    [Fact]
    public void ACoreListAsksTheSameWay()
    {
        var stack = new SkUiCoreVerticalStackLayout();
        for (var i = 0; i < 4; i++)
            stack.Add(new SkUiCoreBox().SetHeight(50));
        var core = new SkUiCoreScrollView();
        core.SetContent(stack);
        core.SetHeight(100);
        var root = new SkUiVerticalStackLayout { Children = { new SkUiCoreHost().SetContent(core), Box(300, 200) } };
        SkUiTestHelpers.Arrange(root, 300, 300);
        Assert.False(root.Router.TakesWheel(new Point(50, 50), 0, 40));
        Assert.True(root.Router.TakesWheel(new Point(50, 50), 0, -40));
        Assert.Equal(0, core.ScrollY);
    }

    [Fact]
    public void AZoomViewTakesTheWheelUntilItsLimit()
    {
        var zoom = new SkUiCoreZoomView();
        zoom.SetContent(new SkUiCoreBox());
        zoom.Measure(200, 100);
        zoom.Arrange(new Rect(0, 0, 200, 100));
        var handler = (ISkUiWheelHandler)zoom;
        // A positive delta zooms in; out is at its limit (1) already.
        Assert.True(handler.TakesWheel(0, 100));
        Assert.False(handler.TakesWheel(0, -100));
        Assert.False(handler.TakesWheel(30, 0));
        Assert.Equal(1, zoom.ZoomScale);
        zoom.ZoomTo(5, new Point(100, 50));
        Assert.False(handler.TakesWheel(0, 100));
        Assert.True(handler.TakesWheel(0, -100));
        zoom.SetIsWheelZoomEnabled(false);
        Assert.False(handler.TakesWheel(0, -100));
        // Drawn, under the pointer: asking zooms nothing, and the dispatch agrees.
        var drawn = new SkUiZoomView { Content = new SkUiBox(), HeightRequest = 100 };
        var root = new SkUiVerticalStackLayout { Children = { drawn } };
        SkUiTestHelpers.Arrange(root, 200, 100);
        var point = new Point(100, 50);
        Assert.False(root.Router.TakesWheel(point, 0, -100));
        Assert.False(root.Touch(new SkUiTouchEvent(0, SkUiTouchAction.Wheel, point, WheelDelta: -100)));
        Assert.True(root.Router.TakesWheel(point, 0, 100));
        Assert.Equal(1, drawn.ZoomScale);
        Assert.True(root.Touch(new SkUiTouchEvent(0, SkUiTouchAction.Wheel, point, WheelDelta: 100)));
        Assert.True(drawn.ZoomScale > 1);
    }

    [Fact]
    public void AnEditorTakesTheWheelWhileItsTextCanScroll()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var editor = new SkUiEditor
        {
            Text = string.Join("\n", Enumerable.Range(1, 30).Select(index => $"line {index}")),
            HeightRequest = 100,
            FontFamily = SkUiTestHelpers.BundledFontFamily
        };
        var root = new SkUiVerticalStackLayout { Children = { editor, Box(300, 200) } };
        SkUiTestHelpers.Arrange(root, 300, 300);
        editor.CursorPosition = 0;
        using (var bitmap = new SkiaSharp.SKBitmap(300, 100))
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
            editor.Model.Draw(canvas, 300, 100);
        var point = new Point(50, 50);
        Assert.True(root.Router.TakesWheel(point, 0, -40));
        Assert.False(root.Router.TakesWheel(point, 0, 40));
        Assert.False(editor.Model.CanScroll(-1));
        root.Touch(new SkUiTouchEvent(0, SkUiTouchAction.Wheel, point, WheelDelta: -4000));
        Assert.False(root.Router.TakesWheel(point, 0, -40));
        Assert.True(root.Router.TakesWheel(point, 0, 40));
    }

    [Fact]
    public void ANumericEntryTakesTheWheelOnlyWhileFocused()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = new SkUiNumericEntry<int> { Value = 0, FontFamily = SkUiTestHelpers.BundledFontFamily };
        var root = new SkUiVerticalStackLayout { Children = { entry, Box(300, 200) } };
        SkUiTestHelpers.Arrange(root, 300, 300);
        var point = new Point(20, entry.Height / 2);
        Assert.False(root.Router.TakesWheel(point, 0, 40));
        Assert.True(entry.Focus());
        Assert.True(root.Router.TakesWheel(point, 0, 40));
        Assert.False(root.Router.TakesWheel(point, 30, 0));
        Assert.Equal(0, entry.Value);
        Assert.True(root.Touch(new SkUiTouchEvent(0, SkUiTouchAction.Wheel, point, WheelDelta: 40)));
        Assert.Equal(2, entry.Value);
        // Disabled while focused: the wheel is the page's.
        entry.IsEnabled = false;
        Assert.False(root.Router.TakesWheel(point, 0, 40));
        Assert.False(root.Touch(new SkUiTouchEvent(0, SkUiTouchAction.Wheel, point, WheelDelta: 40)));
    }

    [Fact]
    public void TheRendererAsksInSurfaceDips()
    {
        var (root, list) = Surface();
        using var renderer = new SkUiFrameRenderer(root, _ => { }, () => { }, () => { });
        Assert.True(renderer.TakesWheelDips(new Point(50, 50), 0, -40));
        Assert.False(renderer.TakesWheelDips(new Point(50, 200), 0, -40));
        Assert.Equal(0, list.ScrollY);
        renderer.Dispose();
        Assert.False(renderer.TakesWheelDips(new Point(50, 50), 0, -40));
    }
}
