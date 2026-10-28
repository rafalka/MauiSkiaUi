using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI's small members on drawn controls (step R4 in docs/archive/ImplementationPlan-1.2.md): <c>StrokeDashPattern</c>,
/// <c>IsOpaque</c>, <c>Padding</c> on the indicator and refresh views, and the read-only <c>IsPressed</c> bindable property
/// that MAUI triggers use. <c>IsLoading</c>, <c>IsDragging</c> and <c>UpdateSelectedItems</c> are checked with the image,
/// carousel and collection view tests.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class SmallParityMembersTests
{
    private static readonly Thickness IndicatorPadding = new(10, 4, 20, 8);

    private static void Tap(SkUiView root, Point position)
    {
        root.Touch(new(1, SkUiTouchAction.Pressed, position, TimeSpan.Zero));
        root.Touch(new(1, SkUiTouchAction.Released, position, TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public void StrokeDashPatternIsTheDashArrayAsFloatsOnBothLayers()
    {
        var rectangle = new SkUiRectangle { StrokeDashArray = [4, 2.5] };
        var border = new SkUiBorder { StrokeDashArray = [3, 1] };
        var coreRectangle = new SkUiCoreRectangle();
        coreRectangle.SetStrokeDashArray(4, 2.5);
        var coreBorder = new SkUiCoreBorder().SetStrokeDashArray(3, 1);

        Assert.Equal([4f, 2.5f], rectangle.StrokeDashPattern);
        Assert.Equal([3f, 1f], border.StrokeDashPattern);
        Assert.Equal([4f, 2.5f], coreRectangle.StrokeDashPattern);
        Assert.Equal([3f, 1f], coreBorder.StrokeDashPattern);
        Assert.NotSame(rectangle.StrokeDashPattern, rectangle.StrokeDashPattern); // a copy per read, as MAUI's
        Assert.Empty(new SkUiEllipse().StrokeDashPattern);
        Assert.Empty(new SkUiCoreBorder().StrokeDashPattern);
    }

    [Fact]
    public void AnIsPressedTriggerAppliesWhileAButtonIsPressed()
    {
        var button = new SkUiButton { Text = "Press", WidthRequest = 100, HeightRequest = 40 };
        button.Triggers.Add(new Trigger(typeof(SkUiButton))
        {
            Property = SkUiButton.IsPressedProperty,
            Value = true,
            Setters = { new Setter { Property = VisualElement.OpacityProperty, Value = 0.5 } },
        });
        var notified = 0;
        button.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiButton.IsPressed)) notified++;
        };
        SkUiTestHelpers.Arrange(button, 100, 40);

        button.Touch(new(1, SkUiTouchAction.Pressed, new Point(50, 20), TimeSpan.Zero));
        Assert.True((bool)button.GetValue(SkUiButton.IsPressedProperty));
        Assert.Equal(0.5, button.Opacity);
        button.Touch(new(1, SkUiTouchAction.Released, new Point(50, 20), TimeSpan.FromMilliseconds(50)));
        Assert.False(button.IsPressed);
        Assert.Equal(1, button.Opacity);
        Assert.Equal(2, notified); // once per press and release, not twice
        Assert.True(SkUiImageButton.IsPressedProperty.IsReadOnly);
    }

    [Fact]
    public void IndicatorPaddingSurroundsTheRowOnBothLayers()
    {
        var view = new SkUiIndicatorView { Count = 3 };
        var unpadded = view.Measure(double.PositiveInfinity, double.PositiveInfinity);
        view.Padding = IndicatorPadding;
        var padded = view.Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.Equal(new Size(unpadded.Width + 30, unpadded.Height + 12), padded);

        var core = new SkUiCoreIndicatorView().SetCount(3).SetPadding(IndicatorPadding);
        Assert.Equal(padded, core.Measure(double.PositiveInfinity, double.PositiveInfinity));

        // A tap just inside the right padding selects the last indicator, one just inside the left the first.
        SkUiTestHelpers.Arrange(view, padded.Width, padded.Height);
        var middle = IndicatorPadding.Top + (padded.Height - IndicatorPadding.VerticalThickness) / 2;
        Tap(view, new Point(padded.Width - IndicatorPadding.Right - 1, middle));
        Assert.Equal(2, view.Position);
        Tap(view, new Point(IndicatorPadding.Left + 1, middle));
        Assert.Equal(0, view.Position);
    }

    [Fact]
    public void RefreshViewPaddingInsetsTheContent()
    {
        var content = new SkUiBox();
        var refresh = new SkUiRefreshView { Content = content, Padding = new Thickness(10, 20, 30, 40) };
        SkUiTestHelpers.Arrange(refresh, 200, 300);
        Assert.Equal(new Rect(10, 20, 160, 240), content.Bounds);
    }

    [Fact]
    public void MauiMarkupForTheSmallMembersParses()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiImage IsOpaque="True" />
                <sk:SkUiImageButton IsOpaque="True" />
                <sk:SkUiIndicatorView Padding="4,2" />
                <sk:SkUiRefreshView Padding="8" />
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var children = ((SkUiVerticalStackLayout)root.Content).Children;
        Assert.True(((SkUiImage)children[0]).IsOpaque);
        Assert.True(((SkUiImageButton)children[1]).IsOpaque);
        Assert.Equal(new Thickness(4, 2), ((SkUiIndicatorView)children[2]).Padding);
        Assert.Equal(new Thickness(8), ((SkUiRefreshView)children[3]).Padding);
    }
}
