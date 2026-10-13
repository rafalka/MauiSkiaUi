using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The popup's width: as wide as its items within <c>MinPopupWidth</c> / <c>MaxPopupWidth</c> (or <c>PopupWidth</c> exactly), and a
/// narrow field that shows a short text of the selected item (<c>SelectedItemDisplayBinding</c>): a currency's code over a list of
/// full names.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerPopupWidthTests : IDisposable
{
    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly IDisposable _font = SkUiTestHelpers.UseBundledFont();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public PickerPopupWidthTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _font.Dispose();
        _ui.Dispose();
    }

    /// <summary>A currency: its code and full name.</summary>
    public sealed record Currency(string Code, string Name);

    private static readonly Currency[] Currencies =
        [new("PLN", "Polish Zloty (PLN)"), new("EUR", "Euro (EUR)"), new("CHF", "Swiss Franc (CHF)"), new("USD", "United States Dollar (USD)")];

    private static SkUiPicker CurrencyPicker() => new()
    {
        ItemsSource = Currencies, SelectedIndex = 0, FontFamily = SkUiTestHelpers.BundledFontFamily, HorizontalOptions = LayoutOptions.Start,
        ItemDisplayBinding = new Binding(nameof(Currency.Name)), SelectedItemDisplayBinding = new Binding(nameof(Currency.Code)),
        PopupPlacement = SkUiPopupPlacement.Anchored
    };

    private static SkUiVerticalStackLayout Root(SkUiView view)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(view);
        SkUiTestHelpers.Arrange(stack, 400, 700);
        return stack;
    }

    private static IEnumerable<ISkUiView> Descendants(ISkUiView view)
    {
        yield return view;
        if (view is SkUiView node)
            foreach (var child in node.SkiaChildren)
                foreach (var descendant in Descendants(child))
                    yield return descendant;
    }

    /// <summary>The longest row's label in the open list (laid out on a surface): whether it shows its whole text.</summary>
    private (bool Whole, SkUiPickerPopup Content) LongestRow(string text)
    {
        var content = Assert.IsType<SkUiPickerPopup>(_presenter.Current!.Content);
        using var surface = new SkUiTestSurface(content, (int)Math.Ceiling(content.WidthRequest), 400);
        surface.Frame();
        var label = Descendants(content).OfType<SkUiLabel>().Single(row => row.Text == text);
        var wanted = label.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        return (wanted <= label.Width + 0.5, content);
    }

    [Fact]
    public void ANarrowFieldShowsTheCodeAndItsListTheWholeNames()
    {
        var picker = CurrencyPicker();
        var root = Root(picker);
        var withCode = picker.Width;
        picker.SelectedItemDisplayBinding = null;
        SkUiTestHelpers.Arrange(root, 400, 700);
        Assert.True(withCode < picker.Width); // the code is narrower than the name
        picker.SelectedItemDisplayBinding = new Binding(nameof(Currency.Code));
        SkUiTestHelpers.Arrange(root, 400, 700);
        Assert.Equal(withCode, picker.Width, 0.5);

        // Screen readers read the item's whole name.
        var element = SkUiSemanticsTree.Build(root).Flatten().Single(node => ReferenceEquals(node.Source, picker));
        Assert.Equal("Polish Zloty (PLN)", element.Value);

        picker.IsOpen = true;
        var request = _presenter.Current!;
        Assert.True(request.Bounds.Width > picker.Width); // not limited to the field
        Assert.Equal(SkUiDiagnostics.GetRootBounds(picker)!.Value.Left, request.Bounds.Left, 0.5); // still anchored at it
        Assert.True(LongestRow("United States Dollar (USD)").Whole);
    }

    [Fact]
    public void TheMaximumWidthTruncatesTheItems()
    {
        var picker = CurrencyPicker();
        picker.MaxPopupWidth = 150;
        Root(picker);

        picker.IsOpen = true;

        Assert.Equal(150, _presenter.Current!.Bounds.Width, 0.5);
        Assert.Equal(150, _presenter.Current.MaximumSize.Width, 0.5);
        Assert.False(LongestRow("United States Dollar (USD)").Whole);
    }

    [Fact]
    public void TheMinimumWidthWidensAListOfShortItems()
    {
        var digits = new SkUiPicker
        {
            ItemsSource = Enumerable.Range(0, 10).Select(digit => digit.ToString()).ToList(), FontFamily = SkUiTestHelpers.BundledFontFamily,
            PopupPlacement = SkUiPopupPlacement.Window
        };
        Root(digits);

        // A window popup is the look's preferred width by default; a minimum of the app's replaces it.
        digits.IsOpen = true;
        Assert.Equal(SkUiLook.Current.PopupPreferredWidth, _presenter.Current!.Bounds.Width, 0.5);
        digits.IsOpen = false;
        _ui.RunPending();

        digits.MinPopupWidth = 96;
        digits.IsOpen = true;
        Assert.Equal(96, _presenter.Current!.Bounds.Width, 0.5);
        digits.IsOpen = false;
        _ui.RunPending();

        // Anchored to a wide field: the minimum replaces the field's width.
        digits.PopupPlacement = SkUiPopupPlacement.Anchored;
        digits.IsOpen = true;
        Assert.Equal(96, _presenter.Current!.Bounds.Width, 0.5);
        Assert.True(digits.Width > 96);
    }

    [Fact]
    public void ThePopupWidthIsExact()
    {
        var picker = CurrencyPicker();
        picker.MinPopupWidth = 50;
        picker.MaxPopupWidth = 500;
        picker.PopupWidth = 210;
        Root(picker);

        picker.IsOpen = true;

        Assert.Equal(210, _presenter.Current!.Bounds.Width, 0.5);
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SetMaxPopupWidth(-3));
    }

    [Fact]
    public void SeveralChoicesShowTheShortTexts()
    {
        var picker = CurrencyPicker();
        picker.SelectedIndex = -1;
        picker.SelectionMode = SkUiSelectionMode.Multiple;
        picker.SelectedItems.Add(Currencies[1]);
        picker.SelectedItems.Add(Currencies[3]);
        var root = Root(picker);
        var element = SkUiSemanticsTree.Build(root).Flatten().Single(node => ReferenceEquals(node.Source, picker));
        Assert.Equal("EUR, USD", element.Value);

        picker.SelectedItemsDisplay = SkUiSelectedItemsDisplay.Tokens;
        SkUiTestHelpers.Arrange(root, 400, 700);
        Assert.Equal(["EUR", "USD"], picker.TokenBoxes.Select(token => token.Text));
    }

    [Fact]
    public void TheCorePickerHasAFunction()
    {
        var core = new SkUiCorePicker().SetItemsSource(Currencies).SetItemDisplay(item => ((Currency)item!).Name)
            .SetSelectedItemDisplay(item => ((Currency)item!).Code).SetSelectedIndex(2).SetMaxPopupWidth(180).SetPopupPlacement(SkUiPopupPlacement.Anchored);
        var root = Root(new SkUiCoreHost().SetContent(core));
        var element = SkUiSemanticsTree.Build(root).Flatten().Single(node => ReferenceEquals(node.Source, core));
        Assert.Equal("Swiss Franc (CHF)", element.Value);

        core.IsOpen = true;
        Assert.Equal(180, _presenter.Current!.Bounds.Width, 0.5);
    }
}
