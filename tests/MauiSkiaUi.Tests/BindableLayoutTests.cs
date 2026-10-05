using System.Collections.ObjectModel;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>MAUI's <see cref="BindableLayout"/> (items source, templates, empty view) on drawn layouts.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class BindableLayoutTests
{
    private static DataTemplate LabelTemplate(Color? background = null) => new(() =>
    {
        var label = new SkUiLabel { Background = background ?? Colors.Transparent };
        label.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath);
        return label;
    });

    private static string[] Texts(SkUiLayout layout) => layout.Children.Cast<SkUiLabel>().Select(label => label.Text ?? "").ToArray();

    [Fact]
    public void ItemsSourceAndTemplateCreateOneBoundChildPerItem()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var stack = new SkUiVerticalStackLayout();
        BindableLayout.SetItemTemplate(stack, LabelTemplate());
        BindableLayout.SetItemsSource(stack, new[] { "a", "b", "c" });

        Assert.Equal(["a", "b", "c"], Texts(stack));
        Assert.Equal(["a", "b", "c"], stack.Children.Cast<SkUiLabel>().Select(label => label.BindingContext));
        Assert.All(stack.Children, child => Assert.Same(stack, ((Element)child).Parent));
    }

    [Fact]
    public void CollectionChangesMapToChildChanges()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var items = new ObservableCollection<string> { "a", "b" };
        var stack = new SkUiVerticalStackLayout();
        BindableLayout.SetItemsSource(stack, items);
        BindableLayout.SetItemTemplate(stack, LabelTemplate());

        items.Add("c");
        items.Insert(0, "first");
        Assert.Equal(["first", "a", "b", "c"], Texts(stack));

        var removed = (SkUiLabel)stack.Children[1];
        items.RemoveAt(1);
        Assert.Equal(["first", "b", "c"], Texts(stack));
        Assert.Null(removed.Parent);
        Assert.Null(removed.BindingContext); // MAUI clears the context it set

        var reused = stack.Children[2];
        items[2] = "replaced"; // same template: the view is reused with the new item
        Assert.Same(reused, stack.Children[2]);
        Assert.Equal(["first", "b", "replaced"], Texts(stack));

        items.Move(0, 2);
        Assert.Equal(["b", "replaced", "first"], Texts(stack));

        items.Clear();
        Assert.Empty(stack.Children);
    }

    [Fact]
    public void ANewItemsSourceReusesChildrenAndNullClearsThem()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var stack = new SkUiHorizontalStackLayout();
        BindableLayout.SetItemTemplate(stack, LabelTemplate());
        BindableLayout.SetItemsSource(stack, new[] { "a", "b", "c" });
        var first = stack.Children[0];

        BindableLayout.SetItemsSource(stack, new[] { "x", "y" });
        Assert.Equal(["x", "y"], Texts(stack));
        Assert.Same(first, stack.Children[0]);

        BindableLayout.SetItemsSource(stack, null);
        Assert.Empty(stack.Children);
    }

    [Fact]
    public void TemplateSelectorPicksATemplatePerItem()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var stack = new SkUiVerticalStackLayout();
        BindableLayout.SetItemTemplateSelector(stack, new EvenOddSelector(LabelTemplate(Colors.Red), LabelTemplate(Colors.Blue)));
        BindableLayout.SetItemsSource(stack, new[] { 1, 2, 3 });

        Assert.Equal([Colors.Blue, Colors.Red, Colors.Blue],
            stack.Children.Cast<SkUiLabel>().Select(label => ((SolidColorBrush)label.Background).Color));
    }

    [Fact]
    public void EmptyViewShowsWhileTheSourceIsEmpty()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var items = new ObservableCollection<string>();
        var empty = new SkUiLabel { Text = "Nothing" };
        var stack = new SkUiVerticalStackLayout();
        BindableLayout.SetItemTemplate(stack, LabelTemplate());
        BindableLayout.SetEmptyView(stack, empty);
        BindableLayout.SetItemsSource(stack, items);
        Assert.Same(empty, Assert.Single(stack.Children));

        items.Add("a");
        Assert.Equal(["a"], Texts(stack));
        Assert.Null(empty.Parent);

        items.RemoveAt(0);
        Assert.Same(empty, Assert.Single(stack.Children)); // re-added after it was removed

        items.Add("b");
        items.Clear();
        Assert.Same(empty, Assert.Single(stack.Children));
    }

    [Fact]
    public void EmptyViewTemplateCreatesTheEmptyView()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var stack = new SkUiVerticalStackLayout();
        BindableLayout.SetItemTemplate(stack, LabelTemplate());
        BindableLayout.SetEmptyViewTemplate(stack, new DataTemplate(() => new SkUiLabel { Text = "From template" }));
        BindableLayout.SetItemsSource(stack, Array.Empty<string>());

        Assert.Equal(["From template"], Texts(stack));
    }

    [Fact]
    public void WithoutATemplateItemsShowAsCenteredDrawnLabels()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var items = new ObservableCollection<object> { "a", 2 };
        var stack = new SkUiVerticalStackLayout();
        BindableLayout.SetItemsSource(stack, items); // MAUI's default template makes a native Label per item

        items.Insert(1, "inserted");
        Assert.Equal(["a", "inserted", "2"], Texts(stack));
        Assert.All(stack.Children.Cast<SkUiLabel>(), label => Assert.Equal(TextAlignment.Center, label.HorizontalTextAlignment));
    }

    [Fact]
    public void ATemplateSetAfterTheItemsSourceReplacesTheDefaultItems()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var stack = new SkUiVerticalStackLayout();
        BindableLayout.SetItemsSource(stack, new[] { "a", "b" });
        BindableLayout.SetItemTemplate(stack, LabelTemplate(Colors.Red));

        Assert.Equal(["a", "b"], Texts(stack));
        Assert.All(stack.Children.Cast<SkUiLabel>(), label => Assert.Equal(Colors.Red, ((SolidColorBrush)label.Background).Color));
    }

    [Fact]
    public void AStringEmptyViewFailsWithAnExplanation()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var stack = new SkUiVerticalStackLayout();
        BindableLayout.SetItemTemplate(stack, LabelTemplate());
        var error = Assert.Throws<ArgumentException>(() => BindableLayout.SetEmptyView(stack, "Nothing"));
        Assert.Contains("EmptyViewTemplate", error.Message);
        Assert.Contains(nameof(Label), error.Message);

        var native = new SkUiVerticalStackLayout();
        BindableLayout.SetItemsSource(native, new[] { "a" });
        Assert.Throws<ArgumentException>(() => BindableLayout.SetItemTemplate(native, new DataTemplate(() => new Entry())));
    }

    [Fact]
    public void AddingItemsRemeasuresTheLayout()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var items = new ObservableCollection<string> { "a" };
        var stack = new SkUiVerticalStackLayout();
        BindableLayout.SetItemTemplate(stack, new DataTemplate(() => new SkUiBox { HeightRequest = 20, WidthRequest = 20 }));
        BindableLayout.SetItemsSource(stack, items);
        Assert.Equal(20, ((IView)stack).Measure(100, double.PositiveInfinity).Height);

        items.Add("b");
        items.Add("c");
        Assert.Equal(60, ((IView)stack).Measure(100, double.PositiveInfinity).Height);
    }

    [Fact]
    public void TemplatesSetAttachedLayoutProperties()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var grid = new SkUiGrid { RowDefinitions = [new RowDefinition(30), new RowDefinition(30), new RowDefinition(30)] };
        BindableLayout.SetItemTemplate(grid, new DataTemplate(() =>
        {
            var box = new SkUiBox();
            box.SetBinding(Grid.RowProperty, Binding.SelfPath);
            return box;
        }));
        BindableLayout.SetItemsSource(grid, new[] { 2, 0, 1 });
        SkUiTestHelpers.Arrange(grid, 100, 90);

        Assert.Equal([60d, 0, 30], grid.Children.Select(child => child.Frame.Y));
    }

    private const string ChipsXaml = """
        <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                     xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
          <sk:SkUiWrapLayout Spacing="4" BindableLayout.ItemsSource="{Binding Tags}">
            <BindableLayout.ItemTemplate>
              <DataTemplate>
                <sk:SkUiLabel Text="{Binding}" Padding="8,4" />
              </DataTemplate>
            </BindableLayout.ItemTemplate>
            <BindableLayout.EmptyView>
              <sk:SkUiLabel Text="No tags" />
            </BindableLayout.EmptyView>
          </sk:SkUiWrapLayout>
        </ContentView>
        """;

    [Fact]
    public void MauiBindableLayoutMarkupWorksWithOnlyThePrefixChanged()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new TagsModel();
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, ChipsXaml);
        var wrap = (SkUiWrapLayout)root.Content;
        Assert.Equal(["No tags"], Texts(wrap));

        model.Tags.Add("maui");
        model.Tags.Add("skia");
        Assert.Equal(["maui", "skia"], Texts(wrap));
        Assert.Equal("skia", ((SkUiLabel)wrap.Children[1]).BindingContext);

        model.Tags.Clear();
        Assert.Equal(["No tags"], Texts(wrap));
    }

    private sealed class TagsModel
    {
        public ObservableCollection<string> Tags { get; } = [];
    }

    private sealed class EvenOddSelector(DataTemplate even, DataTemplate odd) : DataTemplateSelector
    {
        protected override DataTemplate OnSelectTemplate(object item, BindableObject container) => (int)item % 2 == 0 ? even : odd;
    }
}
