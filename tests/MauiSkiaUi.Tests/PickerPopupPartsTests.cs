using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>The app's popup header and footer (both kinds of popup), and "Select all" above a list with several choices.</summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerPopupPartsTests : IDisposable
{
    private static readonly string[] Fruits = ["Apple", "Pear", "Plum", "Fig"];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public PickerPopupPartsTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    private SkUiView Open(SkUiPickerBase picker)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(picker);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        picker.IsOpen = true;
        return (SkUiView)_presenter.Current!.Content!;
    }

    private static IEnumerable<ISkUiView> Descendants(ISkUiView view)
    {
        yield return view;
        if (view is SkUiView node)
            foreach (var child in node.SkiaChildren)
                foreach (var descendant in Descendants(child))
                    yield return descendant;
    }

    private static DataTemplate Hint(string text) => new(() =>
    {
        var label = new SkUiLabel { Text = text, Padding = new Thickness(12) };
        label.BindingContextChanged += (_, _) => label.AutomationId = label.BindingContext?.ToString();
        return label;
    });

    [Fact]
    public void TheAppsHeaderAndFooterSurroundTheList()
    {
        var picker = new SkUiPicker
        {
            ItemsSource = Fruits, BindingContext = "the model", PopupPlacement = SkUiPopupPlacement.Window, Title = "Fruit",
            PopupHeaderTemplate = Hint("Seasonal fruit"), PopupFooterTemplate = Hint("Prices per kg"), UpdateMode = SkUiPickerUpdateMode.WhenFinished
        };
        var content = Assert.IsType<SkUiPickerPopup>(Open(picker));
        using var surface = new SkUiTestSurface(content, 360, 600);
        surface.Frame();

        var labels = Descendants(content).OfType<SkUiLabel>().ToList();
        var header = labels.Single(label => label.Text == "Seasonal fruit");
        var footer = labels.Single(label => label.Text == "Prices per kg");
        Assert.Equal("the model", header.BindingContext); // the picker's binding context
        double Top(SkUiView view) => SkUiDiagnostics.GetRootBounds(view)!.Value.Top;
        var title = labels.Single(label => label.Text == "Fruit");
        var ok = Descendants(content).OfType<SkUiButton>().Single(button => button.Text == "OK");
        Assert.True(Top(title) < Top(header));
        Assert.True(Top(header) < Top(content.List));
        Assert.True(Top(content.List) < Top(footer));
        Assert.True(Top(footer) < Top(ok));
        Assert.True(_presenter.Current!.PreferredSize.Height >= 4 * SkUiLook.Current.PopupItemHeight + header.Height + footer.Height);
    }

    [Fact]
    public void TheWheelsHaveTheAppsPartsToo()
    {
        var picker = new SkUiMultiColumnPicker { PopupHeaderTemplate = Hint("Pick the hour"), PopupFooterTemplate = Hint("24-hour clock") }
            .AddColumn(SkUiPickerColumn.Range(0, 23));
        var content = Open(picker);
        Assert.Contains(Descendants(content).OfType<SkUiLabel>(), label => label.Text == "Pick the hour");
        Assert.Contains(Descendants(content).OfType<SkUiLabel>(), label => label.Text == "24-hour clock");
    }

    [Fact]
    public void SelectAllChoosesEveryRowThatCanBeChosen()
    {
        var picker = new SkUiPicker
        {
            ItemsSource = Fruits, SelectionMode = SkUiSelectionMode.Multiple, IsSelectAllVisible = true,
            ItemEnabledBinding = new Binding(".", converter: new NotPlum())
        };
        var changes = 0;
        picker.SelectionChanged += (_, _) => changes++;
        var content = Assert.IsType<SkUiPickerPopup>(Open(picker));
        Assert.Contains(Descendants(content).OfType<SkUiLabel>(), label => label.Text == "Select all");
        Assert.Equal(SkUiCheckState.Unchecked, content.SelectAllCheckState);

        content.TapSelectAll();
        Assert.Equal([0, 1, 3], picker.SelectedIndices); // Plum cannot be chosen
        Assert.Equal(1, changes); // one change
        Assert.Equal(SkUiCheckState.Checked, content.SelectAllCheckState);

        picker.SelectedItems.Remove("Pear");
        Assert.Equal(SkUiCheckState.Indeterminate, content.SelectAllCheckState);

        content.TapSelectAll(); // some: all
        Assert.Equal([0, 1, 3], picker.SelectedIndices);
        content.TapSelectAll(); // all: none
        Assert.Empty(picker.SelectedIndices);
        Assert.True(picker.IsOpen);
    }

    [Fact]
    public void SelectAllIsADraftWhenFinished()
    {
        var picker = new SkUiPicker { ItemsSource = Fruits, SelectionMode = SkUiSelectionMode.Multiple, IsSelectAllVisible = true, SelectAllText = "All fruit", UpdateMode = SkUiPickerUpdateMode.WhenFinished };
        var content = Assert.IsType<SkUiPickerPopup>(Open(picker));
        Assert.Contains(Descendants(content).OfType<SkUiLabel>(), label => label.Text == "All fruit");

        content.TapSelectAll();
        Assert.Empty(picker.SelectedIndices);
        Assert.Equal([0, 1, 2, 3], ((SkUiMultiListPopupRequest)_presenter.Current!).SelectedIndices);
        Assert.True(_presenter.Current!.Accept());
        _ui.RunPending();
        Assert.Equal([0, 1, 2, 3], picker.SelectedIndices);
    }

    [Fact]
    public void SelectAllReadsAsACheckBox()
    {
        var picker = new SkUiPicker { ItemsSource = Fruits, SelectionMode = SkUiSelectionMode.Multiple, IsSelectAllVisible = true };
        var content = Assert.IsType<SkUiPickerPopup>(Open(picker));
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        var element = SkUiSemanticsTree.Build(content).Flatten().Single(node => node.Label == "Select all");
        Assert.Equal(SkUiSemanticsRole.CheckBox, element.Role);
        Assert.Equal(SkUiCheckState.Unchecked, element.CheckState);
    }

    [Fact]
    public void TheCorePickerHasThemToo()
    {
        var core = new SkUiCorePicker().SetSelectionMode(SkUiSelectionMode.Multiple).SetIsSelectAllVisible(true)
            .SetPopupHeaderTemplate(Hint("Core header"));
        foreach (var fruit in Fruits)
            core.Items.Add(fruit);
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(new SkUiCoreHost().SetContent(core));
        SkUiTestHelpers.Arrange(stack, 360, 700);
        core.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);

        Assert.Contains(Descendants(content).OfType<SkUiLabel>(), label => label.Text == "Core header");
        content.TapSelectAll();
        Assert.Equal([0, 1, 2, 3], core.SelectedIndices);
    }

    private sealed class NotPlum : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => !Equals(value, "Plum");

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }
}
