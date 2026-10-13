using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Several choices shown as tokens in the field: layout and lines, the × and Backspace, disabled items, "+N", Core.</summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerTokensTests : IDisposable
{
    private static readonly string[] Monkeys = ["Baboon", "Capuchin Monkey", "Blue Monkey", "Squirrel Monkey", "Golden Lion Tamarin", "Howler Monkey"];
    private static int _pointer = 7000;

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly IDisposable _font = SkUiTestHelpers.UseBundledFont();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public PickerTokensTests() => SkUiPopups.Presenter = new RecordingPopupPresenter();

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _font.Dispose();
        _ui.Dispose();
    }

    private static SkUiPicker Picker(params string[] chosen)
    {
        var picker = new SkUiPicker
        {
            ItemsSource = Monkeys, SelectionMode = SkUiSelectionMode.Multiple, SelectedItemsDisplay = SkUiSelectedItemsDisplay.Tokens,
            FontFamily = SkUiTestHelpers.BundledFontFamily
        };
        foreach (var monkey in chosen)
            picker.SelectedItems.Add(monkey);
        return picker;
    }

    private static SkUiVerticalStackLayout Root(SkUiView view)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10) };
        stack.Children.Add(view);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        return stack;
    }

    private static void TapAt(SkUiView root, SkUiView view, Point point)
    {
        var bounds = SkUiDiagnostics.GetRootBounds(view)!.Value;
        var at = new Point(bounds.X + point.X, bounds.Y + point.Y);
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    [Fact]
    public void ChosenItemsAreTokensInTheOrderOfTheItems()
    {
        var picker = Picker("Howler Monkey", "Baboon");
        Root(picker);

        Assert.Equal(["Baboon", "Howler Monkey"], picker.TokenBoxes.Select(token => token.Text));
        Assert.All(picker.TokenBoxes, token => Assert.NotEqual(Rect.Zero, token.Remove));
    }

    [Fact]
    public void TheFieldGrowsByLines()
    {
        var picker = Picker("Baboon");
        var root = Root(picker);
        var oneLine = picker.Height;

        foreach (var monkey in Monkeys.Skip(1))
            picker.SelectedItems.Add(monkey);
        SkUiTestHelpers.Arrange(root, 360, 700);

        var tops = picker.TokenBoxes.Select(token => token.Bounds.Top).Distinct().Count();
        Assert.True(tops > 1);
        Assert.True(picker.Height > oneLine);
        Assert.All(picker.TokenBoxes, token => Assert.True(token.Bounds.Right <= picker.Width - SkUiLook.Current.PickerChevronWidth + 0.5));
    }

    [Fact]
    public void TokensBeyondTheLinesBecomeACount()
    {
        var picker = Picker(Monkeys);
        picker.MaxTokenLines = 1;
        Root(picker);

        var tokens = picker.TokenBoxes;
        Assert.Single(tokens.Select(token => token.Bounds.Top).Distinct());
        var more = tokens[^1];
        Assert.Equal($"+{Monkeys.Length - (tokens.Count - 1)}", more.Text);
        Assert.Equal(Rect.Zero, more.Remove); // "+N" has no ×
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SetMaxTokenLines(0));
    }

    [Fact]
    public void ATokensCrossRemovesItsItemWithoutOpening()
    {
        var picker = Picker("Baboon", "Blue Monkey");
        var root = Root(picker);
        var changes = 0;
        picker.SelectionChanged += (_, _) => changes++;

        var remove = picker.TokenBoxes[0].Remove;
        TapAt(root, picker, remove.Center);

        Assert.Equal(["Blue Monkey"], picker.SelectedItems.Cast<string>());
        Assert.Equal(1, changes);
        Assert.False(picker.IsOpen);

        // A tap on a token's text opens the list.
        TapAt(root, picker, new Point(picker.TokenBoxes[0].Bounds.Left + 12, picker.TokenBoxes[0].Bounds.Center.Y));
        Assert.True(picker.IsOpen);
    }

    [Fact]
    public void BackspaceRemovesTheLastToken()
    {
        var picker = Picker("Baboon", "Blue Monkey");
        var root = Root(picker);
        Assert.True(picker.Focus());

        Assert.True(root.FocusManager.KeyDown(SkUiKey.Backspace));
        Assert.Equal(["Baboon"], picker.SelectedItems.Cast<string>());
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Backspace));
        Assert.Empty(picker.SelectedItems);
        Assert.False(root.FocusManager.KeyDown(SkUiKey.Backspace)); // nothing left
    }

    [Fact]
    public void ItemsThatCannotBeChosenHaveNoCross()
    {
        var picker = Picker("Baboon", "Blue Monkey");
        picker.ItemEnabledBinding = new Binding(".", converter: new NotBlue());
        var root = Root(picker);

        Assert.Equal(Rect.Zero, picker.TokenBoxes[1].Remove);
        Assert.True(picker.Focus());
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Backspace)); // Baboon, the last one that can be removed
        Assert.Equal(["Blue Monkey"], picker.SelectedItems.Cast<string>());
        Assert.False(root.FocusManager.KeyDown(SkUiKey.Backspace));
    }

    private sealed class NotBlue : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => !Equals(value, "Blue Monkey");

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }

    [Fact]
    public void TheCountIsTranslatable()
    {
        var original = SkUiStrings.Current;
        try
        {
            SkUiStrings.Current = new SkUiStrings { MoreTokensFormat = "i {0} więcej" };
            var picker = Picker(Monkeys);
            picker.MaxTokenLines = 1;
            Root(picker);
            Assert.StartsWith("i ", picker.TokenBoxes[^1].Text);
        }
        finally
        {
            SkUiStrings.Current = original;
        }
    }

    [Fact]
    public void TheCorePickerHasTokensToo()
    {
        var core = new SkUiCorePicker().SetItemsSource(Monkeys).SetSelectionMode(SkUiSelectionMode.Multiple)
            .SetSelectedItemsDisplay(SkUiSelectedItemsDisplay.Tokens).SetSelectedItems(["Baboon", "Howler Monkey"]);
        var host = new SkUiCoreHost().SetContent(core);
        var root = Root(host);

        Assert.Equal(["Baboon", "Howler Monkey"], core.TokenBoxes.Select(token => token.Text));
        TapAt(root, host, core.TokenBoxes[1].Remove.Center);
        Assert.Equal(["Baboon"], core.SelectedItems.Cast<string>());
    }
}
