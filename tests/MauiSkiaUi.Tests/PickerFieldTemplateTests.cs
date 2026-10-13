using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiPicker.SelectedItemTemplate"/>: the field shows the selected item through drawn views of the app's.</summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerFieldTemplateTests : IDisposable
{
    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public PickerFieldTemplateTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    /// <summary>A paint: a swatch and its name.</summary>
    public sealed record Paint(string Name, Color Color);

    private static readonly Paint[] Paints = [new("Teal", Colors.Teal), new("Coral", Colors.Coral), new("Sky", Colors.SkyBlue)];

    private static DataTemplate Swatch(double height = 32) => new(() =>
    {
        var box = new SkUiBox { WidthRequest = 20, HeightRequest = 20 };
        var name = new SkUiLabel();
        var row = new SkUiHorizontalStackLayout { Spacing = 8, HeightRequest = height, Children = { box, name } };
        row.BindingContextChanged += (_, _) =>
        {
            box.Color = (row.BindingContext as Paint)?.Color;
            name.Text = (row.BindingContext as Paint)?.Name ?? string.Empty;
        };
        return row;
    });

    private static SkUiVerticalStackLayout Root(SkUiView view, bool rightToLeft = false)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10), FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight };
        stack.Children.Add(view);
        SkUiTestHelpers.Arrange(stack, 300, 600);
        return stack;
    }

    [Fact]
    public void TheFieldShowsTheSelectedItemThroughTheTemplate()
    {
        var picker = new SkUiPicker { ItemsSource = Paints, ItemDisplayBinding = Binding.Create(static (Paint paint) => paint.Name), SelectedItemTemplate = Swatch() };
        var root = Root(picker);
        Assert.Null(picker.FieldView); // nothing selected: the placeholder text

        picker.SelectedIndex = 1;
        SkUiTestHelpers.Arrange(root, 300, 600);
        var view = Assert.IsType<SkUiHorizontalStackLayout>(picker.FieldView);
        Assert.Equal(Paints[1], view.BindingContext);
        Assert.True(view.InputTransparent);
        // Inside the field, before the chevron.
        var look = SkUiLook.Current;
        Assert.Equal(look.PickerFieldPadding.Left, view.Frame.Left, 0.5);
        Assert.True(view.Frame.Right <= picker.Width - look.PickerChevronWidth + 0.5);

        picker.SelectedIndex = 2;
        Assert.Same(view, picker.FieldView); // the same view, bound again
        Assert.Equal(Paints[2], view.BindingContext);

        // Screen readers still read the item's text.
        var element = SkUiSemanticsTree.Build((SkUiView)picker.Parent!).Flatten().Single(node => ReferenceEquals(node.Source, picker));
        Assert.Equal("Sky", element.Value);

        picker.SelectedIndex = -1;
        Assert.Null(picker.FieldView);
    }

    [Fact]
    public void TheFieldGrowsWithTheTemplate()
    {
        var picker = new SkUiPicker { ItemsSource = Paints, SelectedIndex = 0, SelectedItemTemplate = Swatch(height: 70) };
        Root(picker);

        Assert.Equal(70 + SkUiLook.Current.PickerFieldPadding.VerticalThickness, picker.Height, 0.5);
    }

    [Fact]
    public void ATapOnTheTemplateOpensTheList()
    {
        var picker = new SkUiPicker { ItemsSource = Paints, SelectedIndex = 0, SelectedItemTemplate = Swatch() };
        Root(picker);

        Assert.NotNull(SkUiDiagnostics.SimulateTap(picker.FieldView!));

        Assert.True(picker.IsOpen);
    }

    [Fact]
    public void ASelectorPicksPerItem()
    {
        var tall = Swatch(60);
        var shortTemplate = Swatch(30);
        var picker = new SkUiPicker
        {
            ItemsSource = Paints, SelectedIndex = 0,
            SelectedItemTemplate = new Selector(item => ((Paint)item!).Name == "Sky" ? tall : shortTemplate)
        };
        Root(picker);
        var first = picker.FieldView;

        picker.SelectedIndex = 2;

        Assert.NotSame(first, picker.FieldView);
        Assert.Equal(60, picker.FieldView!.HeightRequest);
    }

    private sealed class Selector(Func<object?, DataTemplate> select) : DataTemplateSelector
    {
        protected override DataTemplate OnSelectTemplate(object item, BindableObject container) => select(item);
    }

    [Fact]
    public void SeveralChoicesShowText()
    {
        var picker = new SkUiPicker { ItemsSource = Paints, SelectedIndex = 1, SelectedItemTemplate = Swatch() };
        Root(picker);
        Assert.NotNull(picker.FieldView);

        picker.SelectionMode = SkUiSelectionMode.Multiple;

        Assert.Null(picker.FieldView);
        picker.SelectionMode = SkUiSelectionMode.Single;
        Assert.NotNull(picker.FieldView); // the first choice came back
    }

    [Fact]
    public void RightToLeftPutsTheTemplateAtTheRight()
    {
        var picker = new SkUiPicker { ItemsSource = Paints, SelectedIndex = 0, SelectedItemTemplate = Swatch() };
        Root(picker, rightToLeft: true);

        var view = picker.FieldView!;
        Assert.Equal(picker.Width - SkUiLook.Current.PickerFieldPadding.Left, view.Frame.Right, 0.5);
    }
}
