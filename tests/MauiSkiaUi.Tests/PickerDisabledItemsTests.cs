using System.Collections.ObjectModel;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Disabled items (<c>ItemEnabledBinding</c>, Core <c>ItemEnabled</c>, a column's <c>ItemEnabledBinding</c>): shown dimmed, passed by
/// taps, keys and type-ahead, refused by requests, followed while open; a wheel moves off an item that became disabled.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerDisabledItemsTests : IDisposable
{
    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public PickerDisabledItemsTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    /// <summary>A product that may be out of stock.</summary>
    public sealed class Product(string name, bool inStock)
    {
        public string Name { get; } = name;

        public bool InStock { get; set; } = inStock;

        public override string ToString() => Name;
    }

    private static List<Product> Products() =>
        [new("Apple", true), new("Banana", false), new("Cherry", true), new("Date", false), new("Elderberry", true)];

    private static SkUiPicker Picker(List<Product> products, SkUiSelectionMode mode = SkUiSelectionMode.Single) =>
        new()
        {
            ItemsSource = products, SelectionMode = mode,
            ItemEnabledBinding = Binding.Create(static (Product product) => product.InStock)
        };

    private static SkUiVerticalStackLayout Root(SkUiView view)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(view);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        return stack;
    }

    private static bool Key(ISkUiKeyHandler handler, SkUiKey key, char character = '\0') =>
        handler.OnKeyDown(new SkUiKeyEvent(key, SkUiKeyModifiers.None, false, character));

    [Fact]
    public void ATapOnADisabledRowDoesNothing()
    {
        var picker = Picker(Products());
        Root(picker);
        picker.IsOpen = true;
        var request = Assert.IsType<SkUiListPopupRequest>(_presenter.Current);
        Assert.Equal([true, false, true, false, true], request.ItemsEnabled);
        var content = Assert.IsType<SkUiPickerPopup>(request.Content);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        Assert.NotNull(SkUiDiagnostics.SimulateTap(Assert.IsAssignableFrom<SkUiView>(content.List.GetRealizedView(1))));
        _ui.RunPending();

        Assert.True(picker.IsOpen);
        Assert.Equal(-1, picker.SelectedIndex);
        Assert.Null(content.List.SelectedItem);
        Assert.False(request.Complete(3));
        Assert.False(request.SetCurrentIndex(1));
        Assert.True(request.Complete(2));
        _ui.RunPending();
        Assert.Equal(2, picker.SelectedIndex);
    }

    [Fact]
    public void TheKeyboardPassesDisabledRowsBy()
    {
        var picker = Picker(Products());
        Root(picker);
        picker.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);

        Assert.True(Key(content, SkUiKey.Down)); // the first enabled row
        Assert.Equal(0, content.HighlightedIndex);
        Assert.True(Key(content, SkUiKey.Down)); // Banana is disabled
        Assert.Equal(2, content.HighlightedIndex);
        Assert.True(Key(content, SkUiKey.Down)); // Date too
        Assert.Equal(4, content.HighlightedIndex);
        Assert.True(Key(content, SkUiKey.Up));
        Assert.Equal(2, content.HighlightedIndex);
        Assert.True(Key(content, SkUiKey.Character, 'd')); // Date is disabled: nothing else starts with d
        Assert.Equal(2, content.HighlightedIndex);
        Assert.True(Key(content, SkUiKey.Home));
        Assert.Equal(0, content.HighlightedIndex);
    }

    [Fact]
    public void TheClosedFieldsArrowsPassDisabledItemsBy()
    {
        var picker = Picker(Products());
        var root = Root(picker);
        picker.SelectedIndex = 0;
        Assert.True(picker.Focus());

        Assert.True(root.FocusManager.KeyDown(SkUiKey.Down));
        Assert.Equal(2, picker.SelectedIndex);
        Assert.True(root.FocusManager.KeyDown(SkUiKey.End));
        Assert.Equal(4, picker.SelectedIndex);
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Up));
        Assert.Equal(2, picker.SelectedIndex);
    }

    [Fact]
    public void DisabledRowsAreDimmedAndTheirTemplatesDisabled()
    {
        var picker = Picker(Products());
        Root(picker);
        picker.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        static SkUiLabel Label(SkUiPickerPopup content, int index) =>
            Descendants(Assert.IsAssignableFrom<SkUiView>(content.List.GetRealizedView(index))).OfType<SkUiLabel>().Single();
        Assert.Equal(1, Label(content, 0).Opacity);
        Assert.True(Label(content, 1).Opacity < 0.5);

        // Templated rows: the root is disabled (its Disabled visual state) and dimmed.
        picker.IsOpen = false;
        _ui.RunPending();
        picker.ItemTemplate = new DataTemplate(() => new SkUiContentView { HeightRequest = 40, Content = new SkUiLabel { Text = "row" } });
        picker.IsOpen = true;
        content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);
        using var templated = new SkUiTestSurface(content, 360, 400);
        templated.Frame();
        var root = TemplateRoot(content, 1);
        Assert.False(root.IsEnabled);
        Assert.True(Opacity(root) < 0.5);
        Assert.Equal(1, Opacity(TemplateRoot(content, 0)));
    }

    [Fact]
    public void TemplatesThatStyleTheDisabledStateAreNotDimmed()
    {
        var picker = Picker(Products());
        picker.ItemTemplate = new DataTemplate(() =>
        {
            var row = new SkUiContentView { HeightRequest = 40, Content = new SkUiLabel { Text = "row" } };
            VisualStateManager.SetVisualStateGroups(row,
            [
                new VisualStateGroup
                {
                    Name = "CommonStates",
                    States =
                    {
                        new VisualState { Name = VisualStateManager.CommonStates.Normal },
                        new VisualState
                        {
                            Name = VisualStateManager.CommonStates.Disabled,
                            Setters = { new Setter { Property = VisualElement.BackgroundColorProperty, Value = Colors.LightGray } }
                        }
                    }
                }
            ]);
            return row;
        });
        Root(picker);
        picker.IsOpen = true;
        var content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        var root = TemplateRoot(content, 1);
        Assert.False(root.IsEnabled);
        Assert.Equal(Colors.LightGray, root.BackgroundColor); // the app's look of a disabled row
        Assert.Equal(1, Opacity(root));
    }

    [Fact]
    public void TemplatedWheelItemsAreDimmed()
    {
        var column = new SkUiPickerColumn
        {
            ItemsSource = Products(), SelectedIndex = 0,
            ItemEnabledBinding = Binding.Create(static (Product product) => product.InStock),
            ItemTemplate = new DataTemplate(() => new SkUiContentView { HeightRequest = 40, Content = new SkUiLabel { Text = "item" } })
        };
        var picker = new SkUiMultiColumnPicker().AddColumn(column);
        Root(picker);
        picker.IsOpen = true;
        var content = Assert.IsType<SkUiColumnsPopup>(_presenter.Current!.Content);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        var roots = Descendants(content.Wheel(0)).OfType<SkUiContentView>().Where(view => view.BindingContext is Product).ToList();
        var banana = roots.First(view => ((Product)view.BindingContext!).Name == "Banana");
        var apple = roots.First(view => ((Product)view.BindingContext!).Name == "Apple");
        Assert.False(banana.IsEnabled);
        Assert.True(Opacity(banana) < 0.5);
        Assert.Equal(1, Opacity(apple));
    }

    /// <summary>The root of the item template's view in the list's row <paramref name="index"/>.</summary>
    private static SkUiContentView TemplateRoot(SkUiPickerPopup content, int index) =>
        Descendants(Assert.IsAssignableFrom<SkUiView>(content.List.GetRealizedView(index))).OfType<SkUiContentView>().Last();

    /// <summary>How opaque a template's root shows: its own opacity times its item's (the popup dims the item around it).</summary>
    private static double Opacity(VisualElement root) => root.Opacity * ((VisualElement)root.Parent).Opacity;

    [Fact]
    public void SeveralChoicesKeepDisabledItemsAsTheyAre()
    {
        var products = Products();
        var picker = Picker(products, SkUiSelectionMode.Multiple);
        picker.SelectedItems.Add(products[3]); // the app may choose a disabled item
        Root(picker);

        picker.SelectAll(); // the enabled items, and the disabled one already chosen
        Assert.Equal([0, 2, 3, 4], picker.SelectedIndices);

        picker.ClearSelection();
        picker.SelectedItems.Add(products[3]);
        picker.IsOpen = true;
        var request = Assert.IsType<SkUiMultiListPopupRequest>(_presenter.Current);
        Assert.False(request.SetSelected(1, true)); // disabled: cannot be chosen
        Assert.False(request.SetSelected(3, false)); // nor un-chosen
        Assert.True(request.SetSelected(0, true));
        Assert.True(request.Clear()); // keeps the disabled one
        Assert.Equal([3], picker.SelectedIndices);
    }

    [Fact]
    public void AnOpenListFollowsRefreshedStates()
    {
        var products = Products();
        var picker = Picker(products);
        Root(picker);
        picker.IsOpen = true;
        var request = Assert.IsType<SkUiListPopupRequest>(_presenter.Current);
        var content = Assert.IsType<SkUiPickerPopup>(request.Content);

        products[1].InStock = true;
        picker.RefreshItemStates();

        Assert.Equal([true, true, true, false, true], request.ItemsEnabled);
        Assert.True(Key(content, SkUiKey.Down));
        Assert.True(Key(content, SkUiKey.Down));
        Assert.Equal(1, content.HighlightedIndex); // Banana now
    }

    [Fact]
    public void AWheelMovesOffAnItemThatBecameDisabled()
    {
        var days = new ObservableCollection<Day>(Enumerable.Range(1, 31).Select(day => new Day(day)));
        var column = new SkUiPickerColumn
        {
            ItemsSource = days, SelectedIndex = 30,
            ItemEnabledBinding = Binding.Create(static (Day day) => day.IsValid)
        };
        var picker = new SkUiMultiColumnPicker().AddColumn(column);
        Root(picker);
        picker.IsOpen = true;
        var request = Assert.IsType<SkUiColumnsPopupRequest>(_presenter.Current);
        var content = Assert.IsType<SkUiColumnsPopup>(request.Content);

        // February: 29, 30 and 31 cannot be chosen; the wheel on 31 moves to 28.
        foreach (var day in days.Where(day => day.Number > 28))
            day.IsValid = false;
        column.RefreshItemStates();

        Assert.False(request.ItemsEnabled[0]![29]);
        Assert.Equal(27, column.SelectedIndex);
        Assert.Equal(27, content.Wheel(0).Position);
        Assert.False(request.SetSelectedIndex(0, 30));
        Assert.True(Key(content, SkUiKey.End)); // the last day that can be chosen
        Assert.Equal(27, column.SelectedIndex);
    }

    [Fact]
    public void AWheelThatStopsOnADisabledItemRollsOn()
    {
        var column = new SkUiPickerColumn
        {
            ItemsSource = Products(), SelectedIndex = 0,
            ItemEnabledBinding = Binding.Create(static (Product product) => product.InStock)
        };
        var picker = new SkUiMultiColumnPicker().AddColumn(column);
        Root(picker);
        picker.IsOpen = true;
        var content = Assert.IsType<SkUiColumnsPopup>(_presenter.Current!.Content);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        // Scrolled (animated) onto Date, which cannot be chosen: passing it chooses nothing, stopping on it rolls back to Cherry.
        _ = content.Wheel(0).ScrollToIndex(3);
        for (var time = 16; time <= 2000; time += 16)
            surface.Frame(time);
        _ui.RunPending();

        Assert.Equal(2, column.SelectedIndex);
        Assert.Equal(2, content.Wheel(0).Position);
    }

    /// <summary>A day of a month that may not exist in the month shown.</summary>
    public sealed class Day(int number)
    {
        public int Number { get; } = number;

        public bool IsValid { get; set; } = true;

        public override string ToString() => Number.ToString();
    }

    [Fact]
    public void TheCorePickerHasItsOwnPredicate()
    {
        var core = new SkUiCorePicker().SetItemsSource(Products()).SetItemEnabled(item => ((Product)item!).InStock);
        var host = new SkUiCoreHost().SetContent(core);
        var root = Root(host);
        core.SetSelectedIndex(0);
        Assert.True(core.Focus());

        Assert.True(root.FocusManager.KeyDown(SkUiKey.Down));
        Assert.Equal(2, core.SelectedIndex);
        core.IsOpen = true;
        Assert.Equal([true, false, true, false, true], Assert.IsType<SkUiListPopupRequest>(_presenter.Current).ItemsEnabled);
    }

    private static IEnumerable<ISkUiView> Descendants(ISkUiView view)
    {
        yield return view;
        if (view is SkUiView node)
            foreach (var child in node.SkiaChildren)
                foreach (var descendant in Descendants(child))
                    yield return descendant;
    }
}
