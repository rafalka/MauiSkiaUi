using System.Collections.ObjectModel;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI's <c>IndicatorView.IndicatorTemplate</c> / <c>IndicatorLayout</c> on <see cref="SkUiIndicatorView"/> (step T3 in
/// docs/archive/ImplementationPlan-1.2.md): a drawn view per indicator in a stack, bound to its item, colored and marked
/// <c>Selected</c> as MAUI marks it, with taps selecting.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class IndicatorTemplateTests
{
    private static long _pointer = 190_000;

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    private static ObservableCollection<string> Items(int count) => [.. Enumerable.Range(0, count).Select(index => $"Item {index}")];

    private static SkUiIndicatorView Indicator(int count) => new()
    {
        ItemsSource = Items(count),
        IndicatorTemplate = new DataTemplate(() => new SkUiBox { WidthRequest = 10, HeightRequest = 10 })
    };

    private static IList<ISkUiView> Views(SkUiIndicatorView indicator) => ((SkUiLayout)indicator.IndicatorLayout!).Children;

    [Fact]
    public void MauisDocumentedTemplateLoadsWithThePrefixChanged()
    {
        var root = new ContentView { BindingContext = new { Monkeys = Items(3) } };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiIndicatorView ItemsSource="{Binding Monkeys}" IndicatorColor="Transparent" SelectedIndicatorColor="Transparent" HorizontalOptions="Center">
                <sk:SkUiIndicatorView.IndicatorTemplate>
                  <DataTemplate>
                    <sk:SkUiLabel Text="&#xf30c;" FontSize="12">
                      <VisualStateManager.VisualStateGroups>
                        <VisualStateGroup x:Name="CommonStates" xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
                          <VisualState x:Name="Normal">
                            <VisualState.Setters><Setter Property="TextColor" Value="LightGray" /></VisualState.Setters>
                          </VisualState>
                          <VisualState x:Name="Selected">
                            <VisualState.Setters><Setter Property="TextColor" Value="Black" /></VisualState.Setters>
                          </VisualState>
                        </VisualStateGroup>
                      </VisualStateManager.VisualStateGroups>
                    </sk:SkUiLabel>
                  </DataTemplate>
                </sk:SkUiIndicatorView.IndicatorTemplate>
              </sk:SkUiIndicatorView>
            </ContentView>
            """);
        var indicator = (SkUiIndicatorView)root.Content;
        var labels = Views(indicator).Cast<SkUiLabel>().ToList();
        Assert.Equal(3, labels.Count);
        Assert.Equal(["Item 0", "Item 1", "Item 2"], labels.Select(label => label.BindingContext));
        Assert.Equal([Colors.Black, Colors.LightGray, Colors.LightGray], labels.Select(label => label.TextColor));

        indicator.Position = 2;
        Assert.Equal([Colors.LightGray, Colors.LightGray, Colors.Black], labels.Select(label => label.TextColor));
    }

    [Fact]
    public void ViewsFollowTheItemsAndTheColorsMarkTheSelection()
    {
        var indicator = Indicator(4);
        indicator.IndicatorColor = Colors.Silver;
        indicator.SelectedIndicatorColor = Colors.Gray;
        indicator.Position = 1;
        Assert.Equal([Colors.Silver, Colors.Gray, Colors.Silver, Colors.Silver], Views(indicator).Select(view => ((SkUiView)view).BackgroundColor));
        Assert.True(((SkUiView)Views(indicator)[1]).IsSelectedItem);

        ((ObservableCollection<string>)indicator.ItemsSource!).Add("Item 4");
        Assert.Equal(5, Views(indicator).Count);
        indicator.MaximumVisible = 2;
        Assert.Equal(2, Views(indicator).Count);

        // A single indicator hides by default, as the drawn row does.
        indicator.ItemsSource = Items(1);
        Assert.False(((SkUiLayout)indicator.IndicatorLayout!).IsVisible);
    }

    private sealed class BySuffix : DataTemplateSelector
    {
        public readonly DataTemplate Even = new(() => new SkUiBox());
        public readonly DataTemplate Odd = new(() => new SkUiEllipse());

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
            item is string text && text.EndsWith('1') ? Odd : Even;
    }

    [Fact]
    public void ASelectorChoosesAgainWhenTheItemAtAnIndexChanges()
    {
        var items = Items(3);
        var indicator = new SkUiIndicatorView { ItemsSource = items, IndicatorTemplate = new BySuffix() };
        Assert.IsType<SkUiEllipse>(Views(indicator)[1]);
        var unchanged = Views(indicator)[2];

        items[1] = "Item 4"; // same count: the view at index 1 is made again from the other template
        Assert.IsType<SkUiBox>(Views(indicator)[1]);
        Assert.Equal("Item 4", ((BindableObject)Views(indicator)[1]).BindingContext);
        Assert.Same(unchanged, Views(indicator)[2]); // the same template: kept and rebound
    }

    [Fact]
    public void ACountWithoutItemsBindsTheIndices()
    {
        var indicator = new SkUiIndicatorView { Count = 3, IndicatorTemplate = new DataTemplate(() => new SkUiBox()) };
        Assert.Equal([0, 1, 2], Views(indicator).Select(view => ((BindableObject)view).BindingContext));
    }

    [Fact]
    public void ALinkedCarouselsItemsAreBound()
    {
        var indicator = new SkUiIndicatorView { IndicatorTemplate = new DataTemplate(() => new SkUiBox()) };
        var carousel = new SkUiCarouselView { ItemsSource = Items(3), IndicatorView = indicator };
        Assert.Equal(["Item 0", "Item 1", "Item 2"], Views(indicator).Select(view => ((BindableObject)view).BindingContext));
        // Other items at the same count bind again.
        carousel.ItemsSource = new[] { "A", "B", "C" };
        Assert.Equal(["A", "B", "C"], Views(indicator).Select(view => ((BindableObject)view).BindingContext));
        carousel.IndicatorView = null;
        Assert.DoesNotContain(Views(indicator), view => ((BindableObject)view).BindingContext is string);
    }

    [Fact]
    public void ItemsMakeTheViewsOnceAndASourceThatIsNotAListBindsItsItems()
    {
        var created = 0;
        var indicator = new SkUiIndicatorView { IndicatorTemplate = new DataTemplate(() => { created++; return new SkUiBox(); }) };
        indicator.ItemsSource = Items(3).Select(item => item);
        Assert.Equal(3, created);
        Assert.Equal(["Item 0", "Item 1", "Item 2"], Views(indicator).Select(view => ((BindableObject)view).BindingContext));
    }

    [Fact]
    public void ATapSelectsTheViewItHits()
    {
        var indicator = Indicator(3);
        using var surface = new SkUiTestSurface(indicator, 100, 20);
        surface.Frame(0);
        var stack = (SkUiLayout)indicator.IndicatorLayout!;
        // Centered: three 10 DIP views 4 apart.
        Assert.Equal(new Rect(31, 5, 38, 10), stack.Frame);
        Tap(indicator, new Point(31 + 28 + 5, 10));
        Assert.Equal(2, indicator.Position);
    }

    [Fact]
    public void ClearingTheTemplateDrawsTheIndicatorsAgain()
    {
        var indicator = Indicator(3);
        Assert.IsType<SkUiHorizontalStackLayout>(indicator.IndicatorLayout);
        indicator.Orientation = StackOrientation.Vertical;
        Assert.IsType<SkUiVerticalStackLayout>(indicator.IndicatorLayout);
        Assert.Equal(3, Views(indicator).Count);

        indicator.IndicatorTemplate = null;
        Assert.Null(indicator.IndicatorLayout);
        SkUiTestHelpers.Arrange(indicator, 300, 100);
        Assert.Equal(new Size(6, 3 * 6 + 2 * 8), indicator.DesiredSize); // the drawn column
    }

    [Fact]
    public void AnAppLayoutIsShownAsItIs()
    {
        var own = new SkUiHorizontalStackLayout { new SkUiBox { WidthRequest = 30, HeightRequest = 8 } };
        var indicator = new SkUiIndicatorView { Count = 3, IndicatorLayout = own };
        SkUiTestHelpers.Arrange(indicator, 300, 100);
        Assert.Equal(new Size(30, 8), indicator.DesiredSize);
        Assert.Single(own.Children);
        indicator.IndicatorLayout = new Grid(); // not drawn: ignored, as MAUI ignores invalid values
        Assert.Same(own, indicator.IndicatorLayout);
    }

    [Fact]
    public void TemplatesMustMakeDrawnViews()
    {
        var indicator = new SkUiIndicatorView { Count = 2 };
        Assert.Throws<InvalidOperationException>(() => indicator.IndicatorTemplate = new DataTemplate(() => new Label()));
    }
}
