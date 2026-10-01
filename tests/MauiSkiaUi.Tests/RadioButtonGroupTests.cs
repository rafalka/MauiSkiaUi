using System.ComponentModel;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Radio button exclusion and MAUI's <see cref="RadioButtonGroup"/> on drawn layouts, with MAUI's <c>RadioButton</c> rules, on both layers.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class RadioButtonGroupTests
{
    private static void Tap(SkUiView control)
    {
        var root = SkUiDiagnostics.GetSurfaceRoot(control)!;
        SkUiTestHelpers.Arrange(root, 200, 200);
        Assert.NotNull(SkUiDiagnostics.SimulateTap(control));
    }

    private static SkUiVerticalStackLayout Stack(params SkUiRadioButton[] radios)
    {
        var stack = new SkUiVerticalStackLayout();
        foreach (var radio in radios)
            stack.Children.Add(radio);
        return stack;
    }

    [Fact]
    public void RadioButtonsWithoutAGroupNameExcludeTheirSiblingsOnly()
    {
        SkUiRadioButton a = new(), b = new(), other = new();
        var root = new SkUiVerticalStackLayout { Children = { Stack(a, b), Stack(other) } };
        other.IsChecked = true;
        a.IsChecked = true;
        b.IsChecked = true;
        Assert.False(a.IsChecked);
        Assert.True(b.IsChecked);
        Assert.True(other.IsChecked); // another parent: another group
        Assert.False((bool)a.GetValue(SkUiToggleControl.IsCheckedProperty)); // written back, so bindings see it
        Assert.NotNull(root);
    }

    [Fact]
    public void ANamedGroupSpansThePageAndUnchecksBeforeTheNewSelectionReports()
    {
        SkUiRadioButton a = new() { GroupName = "plan" }, b = new() { GroupName = "plan" }, unnamed = new(), elsewhere = new() { GroupName = "size" };
        var content = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { Stack(a, unnamed), Stack(b, elsewhere) } } };
        _ = new ContentPage { Content = content };
        a.IsChecked = true;
        unnamed.IsChecked = true;
        elsewhere.IsChecked = true;
        var events = new List<string>();
        a.CheckedChanged += (_, args) => events.Add($"a {args.Value}");
        b.CheckedChanged += (_, args) => events.Add($"b {args.Value}");

        Tap(b);
        Assert.Equal(["a False", "b True"], events);
        Assert.True(unnamed.IsChecked);
        Assert.True(elsewhere.IsChecked);

        Tap(b); // a tap only selects
        Assert.True(b.IsChecked);
    }

    [Fact]
    public void GroupLayoutNamesItsRadioButtonsAndReportsTheSelectedValue()
    {
        SkUiRadioButton red = new() { Value = "Red" }, green = new() { Value = "Green" }, named = new() { GroupName = "kept", Value = "Kept" };
        var layout = Stack(red, green, named);
        RadioButtonGroup.SetGroupName(layout, "colors");
        Assert.Equal("colors", red.GroupName);
        Assert.Equal("kept", named.GroupName); // radio buttons with their own name keep it

        var later = new SkUiRadioButton { Value = "Blue" };
        layout.Children.Add(later);
        Assert.Equal("colors", later.GroupName);

        green.IsChecked = true;
        Assert.Equal("Green", RadioButtonGroup.GetSelectedValue(layout));
        green.Value = "Lime";
        Assert.Equal("Lime", RadioButtonGroup.GetSelectedValue(layout));

        RadioButtonGroup.SetSelectedValue(layout, "Red");
        Assert.True(red.IsChecked);
        Assert.False(green.IsChecked);

        RadioButtonGroup.SetSelectedValue(layout, null);
        Assert.False(red.IsChecked);
        Assert.False(green.IsChecked);
    }

    [Fact]
    public void SelectedValueBindsBothWays()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new SelectionModel { Selection = "Green" };
        SkUiRadioButton red = new() { Value = "Red" }, green = new() { Value = "Green" };
        var layout = new SkUiVerticalStackLayout { BindingContext = model };
        RadioButtonGroup.SetGroupName(layout, "colors");
        layout.SetBinding(RadioButtonGroup.SelectedValueProperty, nameof(SelectionModel.Selection));
        layout.Children.Add(red);
        layout.Children.Add(green); // added after the selection was set: checked on arrival
        Assert.True(green.IsChecked);

        _ = new SkUiContentView { Content = layout };
        Tap(red);
        Assert.Equal("Red", model.Selection);
        Assert.False(green.IsChecked);

        model.Selection = "Green";
        Assert.True(green.IsChecked);
        Assert.False(red.IsChecked);
    }

    [Fact]
    public void MauiGroupMarkupWorksUnchangedOnDrawnLayouts()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout RadioButtonGroup.GroupName="colors"
                                          RadioButtonGroup.SelectedValue="{Binding Selection}">
                <sk:SkUiRadioButton Value="Red" />
                <sk:SkUiRadioButton Value="Green" />
                <sk:SkUiRadioButton Value="Blue" />
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var model = new SelectionModel { Selection = "Blue" };
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var radios = ((SkUiVerticalStackLayout)root.Content).Children.Cast<SkUiRadioButton>().ToArray();
        Assert.All(radios, radio => Assert.Equal("colors", radio.GroupName));
        Assert.Equal([false, false, true], radios.Select(radio => radio.IsChecked));
        radios[0].IsChecked = true;
        Assert.Equal("Red", model.Selection);
        Assert.False(radios[2].IsChecked);
    }

    [Fact]
    public void DemoPageShowsTheGroupsSelectedValueOnBothSides()
    {
        var page = new MauiSkiaUiDemo.RadioButtonDemoPage();
        var skia = (SkUiVerticalStackLayout)page.SkiaControl;
        var native = (VerticalStackLayout)page.NativeControl!;
        var radios = skia.Children.OfType<SkUiHorizontalStackLayout>().Select(row => (SkUiRadioButton)row.Children[0]).ToArray();
        var skiaLabel = (SkUiLabel)skia.Children[^1];
        var nativeLabel = (Label)native.Children[^1];
        Assert.Equal([false, true, false], radios.Select(radio => radio.IsChecked));
        Assert.Equal("SelectedValue: Green", skiaLabel.Text);
        Assert.Equal("SelectedValue: Green", nativeLabel.Text);

        radios[2].IsChecked = true;
        Assert.Equal("SelectedValue: Blue", skiaLabel.Text);
        Assert.False(radios[1].IsChecked);
        ((RadioButton)native.Children[2]).IsChecked = true;
        Assert.Equal("SelectedValue: Blue", nativeLabel.Text);

        page.ResetProperties();
        Assert.Equal("SelectedValue: Green", skiaLabel.Text);
        Assert.True(radios[1].IsChecked);
    }

    [Fact]
    public void CoreRadioButtonsExcludeTheirSiblings()
    {
        SkUiCoreRadioButton a = new(), b = new(), other = new();
        var left = new SkUiCoreVerticalStackLayout();
        left.Add(a);
        left.Add(b);
        var right = new SkUiCoreVerticalStackLayout();
        right.Add(other);
        var root = new SkUiCoreVerticalStackLayout();
        root.Add(left);
        root.Add(right);
        other.IsChecked = true;
        a.IsChecked = true;
        b.IsChecked = true;
        Assert.Equal([false, true, true], new[] { a.IsChecked, b.IsChecked, other.IsChecked }); // another parent: another group

        root.Measure(100, 200);
        root.Arrange(new Rect(0, 0, 100, 200));
        var tap = new Point(a.Frame.X + 4, a.Frame.Y + 4);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, tap));
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, tap));
        Assert.Equal([true, false, true], new[] { a.IsChecked, b.IsChecked, other.IsChecked });
    }

    private sealed class SelectionModel : INotifyPropertyChanged
    {
        private object? _selection;

        public event PropertyChangedEventHandler? PropertyChanged;

        public object? Selection
        {
            get => _selection;
            set
            {
                if (Equals(_selection, value)) return;
                _selection = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selection)));
            }
        }
    }
}
