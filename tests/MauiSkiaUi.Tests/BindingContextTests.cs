using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls.Shapes;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The binding context reaches everything a drawn view binds through, as in MAUI: children, image sources (also
/// nested ones), placeholders, grid definitions, brushes and geometries, template and presenter content.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class BindingContextTests
{
    private sealed record Model(string Icon, double Height = 30, string Title = "");

    private static FontImageSource BoundGlyph()
    {
        var source = new FontImageSource { FontFamily = "Arial", Size = 20 };
        source.SetBinding(FontImageSource.GlyphProperty, new Binding(nameof(Model.Icon)));
        return source;
    }

    [Fact]
    public void ImageSourcesBindAgainstTheirView()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var image = new SkUiImage { Source = BoundGlyph(), LoadingPlaceholder = BoundGlyph(), ErrorPlaceholder = BoundGlyph(), BindingContext = new Model("A") };
        var button = new SkUiButton { ImageSource = BoundGlyph(), BindingContext = new Model("B") };
        var imageButton = new SkUiImageButton { Source = BoundGlyph(), BindingContext = new Model("C") };
        var slider = new SkUiSlider { ThumbImageSource = BoundGlyph(), BindingContext = new Model("D") };

        Assert.Equal("A", ((FontImageSource)image.Source).Glyph);
        Assert.Equal("A", ((FontImageSource)image.LoadingPlaceholder!).Glyph);
        Assert.Equal("A", ((FontImageSource)image.ErrorPlaceholder!).Glyph);
        Assert.Equal("B", ((FontImageSource)button.ImageSource!).Glyph);
        Assert.Equal("C", ((FontImageSource)imageButton.Source).Glyph);
        Assert.Equal("D", ((FontImageSource)slider.ThumbImageSource!).Glyph);
        Assert.Same(image, image.Source.Parent); // also resources and ancestor bindings, as MAUI's Image

        image.BindingContext = new Model("E"); // later context changes follow
        Assert.Equal("E", ((FontImageSource)image.Source).Glyph);
        Assert.Equal("E", ((FontImageSource)image.ErrorPlaceholder!).Glyph);

        var replaced = (FontImageSource)image.Source;
        image.Source = BoundGlyph();
        Assert.Null(replaced.Parent);
        Assert.Equal("E", ((FontImageSource)image.Source).Glyph);
    }

    [Fact]
    public void AnInheritedContextReachesNestedSources()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var button = new SkUiButton { ImageSource = BoundGlyph() };
        var stack = new SkUiVerticalStackLayout { Children = { button } };
        stack.BindingContext = new Model("F");
        Assert.Equal("F", ((FontImageSource)button.ImageSource!).Glyph);
    }

    [Fact]
    public void ASharedSourceKeepsNoViewAlive()
    {
        var shared = new FontImageSource { Glyph = "x", FontFamily = "Arial" };
        var image = Use(shared);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(image.TryGetTarget(out _));
        GC.KeepAlive(shared);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<SkUiImage> Use(ImageSource source) => new(new SkUiImage { Source = source });

    [Fact]
    public void GridDefinitionsBindAgainstTheGrid()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var row = new RowDefinition();
        row.SetBinding(RowDefinition.HeightProperty, new Binding(nameof(Model.Height)));
        var grid = new SkUiGrid { RowDefinitions = [row, new RowDefinition(GridLength.Star)] };
        var second = new SkUiBox();
        Grid.SetRow(second, 1);
        grid.Children.Add(second);
        grid.BindingContext = new Model("", Height: 40);
        SkUiTestHelpers.Arrange(grid, 100, 100);
        Assert.Equal(40, second.Frame.Y);

        var added = new ColumnDefinition();
        added.SetBinding(ColumnDefinition.WidthProperty, new Binding(nameof(Model.Height)));
        grid.ColumnDefinitions.Add(added); // definitions added later get it too
        Assert.Equal(new GridLength(40), added.Width);
    }

    [Fact]
    public void ATemplateSelectorChoosesAgainWhenTheContextChanges()
    {
        var view = new SkUiContentView { ContentTemplate = new ByKindSelector() };
        _ = new SkUiVerticalStackLayout { Children = { view } };
        Assert.IsType<SkUiBox>(view.Content); // null context
        view.BindingContext = 2;
        var ellipse = Assert.IsType<SkUiEllipse>(view.Content);
        view.BindingContext = 4; // the same template: the content stays and rebinds
        Assert.Same(ellipse, view.Content);
        Assert.Equal(4, ellipse.BindingContext);
    }

    [Fact]
    public void PresentedContentBindsAgainstTheControlAboveATemplateContext()
    {
        var content = new SkUiLabel();
        var radio = new SkUiRadioButton
        {
            Content = content,
            BindingContext = "radio",
            ControlTemplate = new ControlTemplate(() => new SkUiVerticalStackLayout { BindingContext = "template", Children = { new SkUiContentPresenter() } })
        };
        Assert.Equal("radio", content.BindingContext);
        radio.BindingContext = "changed";
        Assert.Equal("changed", content.BindingContext);
    }

    [Fact]
    public void ReplacedBrushesAndGeometriesGiveTheContextBack()
    {
        var oldFill = new LinearGradientBrush();
        var oldData = new EllipseGeometry();
        var path = new SkUiPath { Fill = oldFill, Data = oldData, BindingContext = "model" };
        Assert.Equal(("model", "model"), (oldFill.BindingContext, oldData.BindingContext));
        path.Fill = new RadialGradientBrush();
        path.Data = new RectangleGeometry();
        Assert.Null(oldFill.BindingContext);
        Assert.Null(oldData.BindingContext);

        var oldShape = new RoundRectangle();
        var border = new SkUiBorder { StrokeShape = oldShape, BindingContext = "model" };
        border.StrokeShape = new Rectangle();
        Assert.Null(oldShape.BindingContext);
    }

    [Fact]
    public void DeferredAndNativeContentGetTheContextWhenAttached()
    {
        var deferred = new SkUiLabel();
        var view = new SkUiContentView { ContentLoading = SkUiContentLoading.WhenShown, Content = deferred, BindingContext = "model" };
        Assert.Null(deferred.BindingContext); // held, not attached
        view.LoadContent();
        Assert.Equal("model", deferred.BindingContext);

        var entry = new Entry();
        var host = new SkUiMauiContentView { Content = entry };
        _ = new SkUiVerticalStackLayout { Children = { host }, BindingContext = "page" };
        Assert.Equal("page", entry.BindingContext);
    }

    private const string Xaml = """
        <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                     xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
          <sk:SkUiGrid>
            <sk:SkUiGrid.RowDefinitions>
              <RowDefinition Height="{Binding Height}" />
              <RowDefinition Height="*" />
            </sk:SkUiGrid.RowDefinitions>
            <sk:SkUiImage>
              <sk:SkUiImage.Source>
                <FontImageSource Glyph="{Binding Icon}" FontFamily="Arial" Size="24" />
              </sk:SkUiImage.Source>
            </sk:SkUiImage>
            <sk:SkUiButton Grid.Row="1" Text="{Binding Title}">
              <sk:SkUiButton.ImageSource>
                <FontImageSource Glyph="{Binding Icon}" FontFamily="Arial" Size="16" />
              </sk:SkUiButton.ImageSource>
            </sk:SkUiButton>
          </sk:SkUiGrid>
        </ContentView>
        """;

    [Fact]
    public void MauiMarkupWithNestedBindingsWorksWithOnlyThePrefixChanged()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var root = new ContentView { BindingContext = new Model("★", Height: 48, Title: "Star") };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, Xaml);
        var grid = (SkUiGrid)root.Content;
        Assert.Equal(new GridLength(48), grid.RowDefinitions[0].Height);
        Assert.Equal("★", ((FontImageSource)((SkUiImage)grid.Children[0]).Source!).Glyph);
        var button = (SkUiButton)grid.Children[1];
        Assert.Equal(("Star", "★"), (button.Text, ((FontImageSource)button.ImageSource!).Glyph));
    }

    private sealed class ByKindSelector : DataTemplateSelector
    {
        private readonly DataTemplate _box = new(() => new SkUiBox());
        private readonly DataTemplate _ellipse = new(() => new SkUiEllipse());

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container) => item is int number && number % 2 == 0 ? _ellipse : _box;
    }

    [Fact]
    public void ASourceSharedBySlotsKeepsTheContextWhileAnySlotUsesIt()
    {
        var shared = new FontImageSource { Glyph = "x", FontFamily = "Arial" };
        var image = new SkUiImage { LoadingPlaceholder = shared, ErrorPlaceholder = shared, BindingContext = "model" };
        image.LoadingPlaceholder = null;
        Assert.Same(image, shared.Parent); // still the error placeholder
        Assert.Equal("model", shared.BindingContext);
        image.ErrorPlaceholder = null;
        Assert.Null(shared.Parent);

        var brush = new LinearGradientBrush();
        var shape = new SkUiRectangle { Fill = brush, Stroke = brush, BindingContext = "model" };
        shape.Fill = null;
        Assert.Equal("model", brush.BindingContext); // still the stroke
        shape.Stroke = null;
        Assert.Null(brush.BindingContext);
    }

    [Fact]
    public void RemovedGridDefinitionsGiveTheContextBack()
    {
        var row = new RowDefinition();
        var grid = new SkUiGrid { RowDefinitions = [row], BindingContext = "model" };
        Assert.Equal("model", row.BindingContext);
        grid.RowDefinitions.Remove(row);
        Assert.Null(row.BindingContext);

        var column = new ColumnDefinition();
        grid.ColumnDefinitions = [column];
        grid.ColumnDefinitions = [new ColumnDefinition()]; // replaced collection
        Assert.Null(column.BindingContext);
    }

    [Fact]
    public void AThrowingIsShownHandlerDoesNotStopTheRestOfTheBranch()
    {
        SkUiBox throwing = new(), sibling = new();
        var fail = true;
        throwing.IsShownChanged += (_, _) => { if (fail) throw new InvalidOperationException("handler"); };
        var siblingShown = false;
        sibling.IsShownChanged += (_, _) => siblingShown = sibling.IsShown;
        var pane = new SkUiVerticalStackLayout { IsVisible = false, Children = { throwing, sibling } };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = pane }, 100, 100);
        var error = Assert.Throws<InvalidOperationException>(() => pane.IsVisible = true);
        Assert.Equal("handler", error.Message); // still reported
        Assert.True(siblingShown); // after the sibling was updated
        fail = false; // the surface's release at the end hides them again
    }
}
