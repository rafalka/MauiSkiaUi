using Xunit;

namespace MauiSkiaUi.Tests;

public class MauiContentViewTests
{
    [Fact]
    public void MauiContentViewRootRelativeFrameIncludesAncestorAndOwnTranslation()
    {
        var overlay = new SkUiMauiContentView
        {
            Content = new Editor(), WidthRequest = 20, HeightRequest = 20, TranslationX = 5, TranslationY = 3,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start
        };
        var layout = new SkUiLayout { TranslationX = 100, TranslationY = 50 };
        layout.Children.Add(overlay);
        var root = new SkUiContentView { Content = layout };
        SkUiTestHelpers.Arrange(root, 200, 200);
        // root(0,0) -> layout Frame(0,0) + Translation(100,50) -> overlay Frame(0,0) + Translation(5,3).
        Assert.Equal(new Rect(105, 53, 20, 20), overlay.ComputeRootRelativeFrame());
    }

    [Fact]
    public void MauiContentViewMeasuresArrangesContentAndNeverConsumesTouch()
    {
        // A plain MAUI VisualElement without a platform handler cannot report a real measured size in
        // headless tests (that requires native text measurement); this is an FR-16 test limitation, not a
        // defect. Arrange still delegates the given bounds to Content regardless of the measured size.
        var editor = new Editor { Text = "hello" };
        var host = new SkUiMauiContentView { Content = editor };
        Assert.Equal(Size.Zero, ((IView)host).Measure(200, 200));
        ((IView)host).Arrange(new Rect(0, 0, 120, 40));
        Assert.Equal(new Rect(0, 0, 120, 40), editor.Frame);
        Assert.False(host.Touch(new(1, SkUiTouchAction.Pressed, new Point(10, 10))));
    }

    [Fact]
    public void MauiContentViewRejectsAlreadyOwnedContent()
    {
        var editor = new Editor();
        _ = new SkUiMauiContentView { Content = editor };
        Assert.Throws<InvalidOperationException>(() => new SkUiMauiContentView().SetContent(editor));
    }

    [Fact]
    public void MauiContentViewComputesRootRelativeFrameThroughNestedHostedLayouts()
    {
        var overlay = new SkUiMauiContentView
        {
            Content = new Editor(), WidthRequest = 50, HeightRequest = 20,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start
        };
        var grid = new SkUiGrid { Padding = new Thickness(10) };
        Grid.SetRow(overlay, 0);
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.Children.Add(overlay);
        var layout = new SkUiLayout();
        layout.Children.Add(grid);
        var root = new SkUiContentView { Content = layout };
        ((IView)root).Measure(300, 300);
        ((IView)root).Arrange(new Rect(0, 0, 300, 300));
        // root(0,0) -> layout(0,0, no padding) -> grid(Padding 10) -> overlay: expect (10,10,50,20).
        Assert.Equal(new Rect(10, 10, 50, 20), overlay.ComputeRootRelativeFrame());
    }

    [Fact]
    public void MauiContentViewNotifyHooksAreSafeNoOpsWithoutAPlatformRoot()
    {
        var host = new SkUiMauiContentView { Content = new Editor() };
        host.NotifyRootAttached();
        host.NotifyRootDetached();
    }
    [Fact]
    public void MauiContentViewHidesTheNativeViewWhileAnAncestorIsInvisible()
    {
        // The overlay sits above the surface: an invisible drawn ancestor (collapsed content) must hide it too.
        var overlay = new SkUiMauiContentView { Content = new Editor(), HeightRequest = 20 };
        var stack = new SkUiVerticalStackLayout { Children = { overlay } };
        var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { stack } } };
        using (var surface = new SkUiTestSurface(root, 100, 100))
        {
            Assert.False(overlay.IsNativeHidden);
            stack.IsVisible = false;
            Assert.True(overlay.IsNativeHidden);
            stack.IsVisible = true;
            Assert.False(overlay.IsNativeHidden);
            overlay.IsVisible = false;
            Assert.True(overlay.IsNativeHidden);
            overlay.IsVisible = true;

            // Removed while hidden: no tracker state stays behind; added back shown.
            stack.IsVisible = false;
            stack.Children.Remove(overlay);
            Assert.Equal(0, SkUiShownTracker.Watchers(stack));
            ((SkUiVerticalStackLayout)root.Content!).Children.Add(overlay);
            Assert.False(overlay.IsNativeHidden);
        }
        // The surface went away: nothing is shown.
        Assert.True(overlay.IsNativeHidden);
    }
}
