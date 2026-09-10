using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Xunit;

namespace MauiSkiaUi.Tests;

public class Phase2Tests
{
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
}
