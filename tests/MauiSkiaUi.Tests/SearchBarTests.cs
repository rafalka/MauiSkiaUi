using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>The drawn search bar (D8): MAUI's <c>SearchBar</c> on the entry's engine — searching, the clear button, the icon's room, Core.</summary>
[Collection(GlobalStateCollection.Name)]
public class SearchBarTests
{
    [Fact]
    public void ReturnSearchesAndRunsTheCommand()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var order = new List<string>();
        var bar = new SkUiSearchBar { FontFamily = SkUiTestHelpers.BundledFontFamily, SearchCommand = new Command<string>(p => order.Add($"command {p}")), SearchCommandParameter = "q" };
        bar.SearchButtonPressed += (_, _) => order.Add("pressed");
        var root = new SkUiVerticalStackLayout { Children = { bar } };
        SkUiTestHelpers.Arrange(root, 300, 200);
        bar.Focus();
        root.FocusManager.KeyDown(SkUiKey.Character, character: 'x');
        root.FocusManager.KeyDown(SkUiKey.Enter);
        Assert.Equal(["pressed", "command q"], order);
        var options = ((ISkUiTextInputClient)bar.Model).Options;
        Assert.Equal(ReturnType.Search, options.ReturnType);
        Assert.True(options.IsSearch);
    }

    [Fact]
    public void TheClearButtonShowsWhileThereIsTextAndTheIconKeepsItsRoom()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var bar = new SkUiSearchBar { FontFamily = SkUiTestHelpers.BundledFontFamily };
        var entry = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily };
        var root = new SkUiVerticalStackLayout { Children = { bar, entry } };
        SkUiTestHelpers.Arrange(root, 300, 200);
        Assert.False(bar.Model.ClearButtonShown);
        bar.Text = "query";
        // Unfocused too (MAUI's search bars show their clear button whenever they have text).
        Assert.True(bar.Model.ClearButtonShown);
        var barWidth = ((IView)new SkUiSearchBar { FontFamily = SkUiTestHelpers.BundledFontFamily, Text = "abc" }).Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        var entryWidth = ((IView)new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily, Text = "abc" }).Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        Assert.True(barWidth >= entryWidth + SkUiLook.Current.SearchIconWidth, $"{barWidth} vs {entryWidth}");
        var x = bar.Width - SkUiLook.Current.TextFieldPadding.Right - SkUiLook.Current.TextFieldClearButtonWidth / 2;
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, new Point(x, bar.Height / 2), TimeSpan.Zero));
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, new Point(x, bar.Height / 2), TimeSpan.FromMilliseconds(40)));
        Assert.Equal("", bar.Text);
    }

    [Fact]
    public void CoreSearchBarSearches()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var core = new SkUiCoreSearchBar().SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        var searched = 0;
        core.SearchButtonPressed += (_, _) => searched++;
        var root = new SkUiVerticalStackLayout { Children = { new SkUiCoreHost().SetContent(core) } };
        SkUiTestHelpers.Arrange(root, 300, 200);
        core.Focus();
        root.FocusManager.KeyDown(SkUiKey.Enter);
        Assert.Equal(1, searched);
    }
}
