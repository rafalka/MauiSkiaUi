using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Leading and trailing views in drawn text fields (D11): <c>LeadingView</c> / <c>TrailingView</c> on both layers, placed beside
/// the text with the search icon and the clear button, the text inset so they never overlap, presses on icons focusing the field
/// and presses on buttons staying theirs. In the global collection: the keyboard / pointer mode is process-wide.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class TextFieldAdornmentTests
{
    private const double Width = 300;
    private const double IconSize = 24;
    private static readonly double Spacing = SkUiLook.Current.TextFieldAdornmentSpacing;
    private static readonly Thickness Padding = SkUiLook.Current.TextFieldPadding;

    private static SkUiBox Icon(double size = IconSize) => new() { WidthRequest = size, HeightRequest = size };

    private static (SkUiVerticalStackLayout Root, T Field) Field<T>(T field) where T : SkUiInputView
    {
        field.FontFamily = SkUiTestHelpers.BundledFontFamily;
        var root = new SkUiVerticalStackLayout { Children = { field } };
        SkUiTestHelpers.Arrange(root, Width, 400);
        return (root, field);
    }

    private static void Tap(SkUiView root, Point point, double seconds, SkUiPointerDevice device = SkUiPointerDevice.Touch)
    {
        var id = (long)(seconds * 1000);
        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Pressed, point, TimeSpan.FromSeconds(seconds), Device: device));
        root.Touch(new SkUiTouchEvent(id, SkUiTouchAction.Released, point, TimeSpan.FromSeconds(seconds + 0.05), Device: device));
    }

    [Fact]
    public void TheViewsSitInsideThePaddingAndTheTextBetweenThem()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var leading = Icon();
        var trailing = Icon(20);
        var (_, entry) = Field(new SkUiEntry { Text = "text", LeadingView = leading, TrailingView = trailing });

        Assert.Equal(new Rect(Padding.Left, (entry.Height - IconSize) / 2, IconSize, IconSize), leading.Frame);
        Assert.Equal(new Rect(Width - Padding.Right - 20, (entry.Height - 20) / 2, 20, 20), trailing.Frame);
        var text = entry.Model.GetTextBounds();
        Assert.Equal(Padding.Left + IconSize + Spacing, text.Left, 3);
        Assert.Equal(Width - Padding.Right - 20 - Spacing, text.Right, 3);
        Assert.Same(entry, leading.Parent);
        Assert.Same(entry, trailing.Parent);
    }

    [Fact]
    public void RightToLeftMirrorsTheViews()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var leading = Icon();
        var trailing = Icon(20);
        var (_, entry) = Field(new SkUiEntry { FlowDirection = FlowDirection.RightToLeft, LeadingView = leading, TrailingView = trailing });

        Assert.Equal(Width - Padding.Right - IconSize, leading.Frame.X, 3);
        Assert.Equal(Padding.Left, trailing.Frame.X, 3);
        var text = entry.Model.GetTextBounds();
        Assert.Equal(Width - Padding.Right - IconSize - Spacing, text.Right, 3);
        Assert.Equal(Padding.Left + 20 + Spacing, text.Left, 3);
    }

    [Fact]
    public void TheClearButtonSitsBetweenTheTextAndTheTrailingView()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var trailing = Icon();
        var (root, entry) = Field(new SkUiEntry { Text = "clear me", ClearButtonVisibility = ClearButtonVisibility.WhileEditing, TrailingView = trailing });
        entry.Focus();
        root.Measure(Width, 400);
        root.Arrange(new Rect(0, 0, Width, 400));

        Assert.True(entry.Model.ClearButtonShown);
        var trailingFrame = trailing.Frame;
        Assert.Equal(Width - Padding.Right - IconSize, trailingFrame.X, 3);
        var clearWidth = SkUiLook.Current.TextFieldClearButtonWidth;
        Assert.Equal(Width - Padding.Right - IconSize - clearWidth, entry.Model.GetTextBounds().Right, 3);

        Tap(root, new Point(trailingFrame.X - clearWidth / 2, entry.Height / 2), 1);
        Assert.Equal(string.Empty, entry.Text);
        Assert.True(entry.IsFocused);
        Assert.Equal(trailingFrame, trailing.Frame);
    }

    [Fact]
    public void ASearchBarsIconComesBeforeTheLeadingView()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var leading = Icon();
        var (_, bar) = Field(new SkUiSearchBar { LeadingView = leading });
        Assert.Equal(Padding.Left + SkUiLook.Current.SearchIconWidth, leading.Frame.X, 3);
        Assert.Equal(leading.Frame.Right + Spacing, bar.Model.GetTextBounds().Left, 3);
    }

    [Fact]
    public void ATallViewGrowsTheFieldAndAHiddenOneTakesNoRoom()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var leading = Icon(64);
        var (root, entry) = Field(new SkUiEntry { LeadingView = leading });
        Assert.Equal(64, entry.Height, 3);
        Assert.Equal(new Rect(Padding.Left, 0, 64, 64), leading.Frame);

        leading.IsVisible = false;
        SkUiTestHelpers.Arrange(root, Width, 400);
        Assert.Equal(SkUiLook.Current.DefaultTextFieldMinimumHeight, entry.Height, 3);
        Assert.Equal(Padding.Left, entry.Model.GetTextBounds().Left, 3);
    }

    [Fact]
    public void AViewThatChangesSizeMovesTheText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var leading = Icon();
        var (root, entry) = Field(new SkUiEntry { LeadingView = leading });
        leading.WidthRequest = 40;
        SkUiTestHelpers.Arrange(root, Width, 400);
        Assert.Equal(40, leading.Frame.Width, 3);
        Assert.Equal(Padding.Left + 40 + Spacing, entry.Model.GetTextBounds().Left, 3);
    }

    [Fact]
    public void AnEditorsViewsFollowTheTextToTheTop()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var leading = Icon();
        var (root, editor) = Field(new SkUiEditor { HeightRequest = 120, LeadingView = leading });
        Assert.Equal(Padding.Top, leading.Frame.Y, 3);

        editor.VerticalTextAlignment = TextAlignment.End;
        SkUiTestHelpers.Arrange(root, Width, 400);
        Assert.Equal(120 - Padding.Bottom - IconSize, leading.Frame.Y, 3);
    }

    [Fact]
    public void ATapOnAnIconFocusesTheFieldAndATapOnAButtonIsTheButtons()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var clicks = 0;
        var button = new SkUiButton { Text = "", WidthRequest = 32, HeightRequest = 32 };
        button.Clicked += (_, _) => clicks++;
        var (root, entry) = Field(new SkUiEntry { Text = "abc", LeadingView = Icon(), TrailingView = button });

        Tap(root, new Point(button.Frame.Center.X, button.Frame.Center.Y), 1);
        Assert.Equal(1, clicks);
        Assert.False(entry.IsFocused);

        Tap(root, new Point(Padding.Left + IconSize / 2, entry.Height / 2), 2);
        Assert.True(entry.IsFocused);
        Assert.Equal(0, entry.CursorPosition);

        // Focused: a click on the button stays the button's (the field neither takes the mouse nor moves its caret).
        entry.CursorPosition = 2;
        Tap(root, new Point(button.Frame.Center.X, button.Frame.Center.Y), 3, SkUiPointerDevice.Mouse);
        Assert.Equal(2, clicks);
        Assert.True(entry.IsFocused);
        Assert.Equal(2, entry.CursorPosition);
    }

    [Fact]
    public void ATapOnEmptySpaceInALayoutAroundAButtonIsTheFields()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var clicks = 0;
        var button = new SkUiButton { Text = "", WidthRequest = 32, HeightRequest = 32 };
        button.Clicked += (_, _) => clicks++;
        var stack = new SkUiHorizontalStackLayout { Children = { new SkUiBox { WidthRequest = 40, HeightRequest = 32 }, button } };
        var (root, entry) = Field(new SkUiEntry { Text = "abc", TrailingView = stack });

        // The spacer: no dead zone, the field takes the tap.
        Tap(root, new Point(stack.Frame.X + 20, entry.Height / 2), 1);
        Assert.True(entry.IsFocused);
        Assert.Equal(0, clicks);

        entry.Unfocus();
        Tap(root, new Point(stack.Frame.X + button.Frame.Center.X, entry.Height / 2), 2);
        Assert.Equal(1, clicks);
        Assert.False(entry.IsFocused);
    }

    [Fact]
    public void APressOnAButtonInAFieldDoesNotTakeFocusOnWindows()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var button = new SkUiButton { Text = "", WidthRequest = 32, HeightRequest = 32 };
        var (root, entry) = Field(new SkUiEntry { TrailingView = button });
        root.FocusManager.PointerPressFocuses = true;
        entry.Focus();
        Tap(root, new Point(button.Frame.Center.X, button.Frame.Center.Y), 1, SkUiPointerDevice.Mouse);
        Assert.True(entry.IsFocused);
        Assert.False(button.IsFocused);
    }

    [Fact]
    public void ALabelsTextIsReadWithTheFieldAndAButtonIsItsOwnElement()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var button = new SkUiButton { Text = "Scan", HeightRequest = 32, FontFamily = SkUiTestHelpers.BundledFontFamily };
        var (root, _) = Field(new SkUiEntry { Placeholder = "Weight", TrailingView = new SkUiLabel { Text = "kg", FontFamily = SkUiTestHelpers.BundledFontFamily }, LeadingView = button });
        var owner = new SkUiSemanticsOwner(root);
        var nodes = owner.Tree.Flatten();
        var field = nodes.Single(node => node.Role == SkUiSemanticsRole.TextField);
        Assert.Equal("Weight, kg", field.Label);
        Assert.Contains(nodes, node => node.Role == SkUiSemanticsRole.Button && node.Label == "Scan");
    }

    [Fact]
    public void AViewIsInOneSlotOfOneField()
    {
        var icon = Icon();
        var entry = new SkUiEntry { LeadingView = icon };
        Assert.Throws<InvalidOperationException>(() => entry.TrailingView = icon);
        Assert.Throws<InvalidOperationException>(() => new SkUiEntry().LeadingView = icon);
        entry.LeadingView = null;
        Assert.Null(icon.Parent);
        entry.TrailingView = icon;
        Assert.Same(entry, icon.Parent);
    }

    [Fact]
    public void CoreFieldsPlaceTheirViewsLikeTheSkUiFields()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var leading = new SkUiCoreBox();
        leading.SetWidth(IconSize).SetHeight(IconSize);
        var clicks = 0;
        var button = new SkUiCoreButton().SetClicked(() => clicks++);
        button.SetWidth(32).SetHeight(32);
        var core = new SkUiCoreEntry().SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        core.SetLeadingView(leading).SetTrailingView(button);
        var root = new SkUiVerticalStackLayout { Children = { new SkUiCoreHost().SetContent(core) } };
        SkUiTestHelpers.Arrange(root, Width, 400);

        Assert.Equal(new Rect(Padding.Left, (core.Frame.Height - IconSize) / 2, IconSize, IconSize), leading.Frame);
        Assert.Equal(Width - Padding.Right - 32, button.Frame.X, 3);
        Assert.Equal(Padding.Left + IconSize + Spacing, core.Model.GetTextBounds().Left, 3);
        Assert.Equal(Width - Padding.Right - 32 - Spacing, core.Model.GetTextBounds().Right, 3);

        Tap(root, new Point(button.Frame.Center.X, core.Frame.Center.Y), 1);
        Assert.Equal(1, clicks);
        Assert.False(core.IsFocused);
        Tap(root, new Point(Padding.Left + IconSize / 2, core.Frame.Center.Y), 2);
        Assert.True(core.IsFocused);

        Assert.Throws<InvalidOperationException>(() => core.SetLeadingView(button));
        core.SetTrailingView(null);
        Assert.Null(button.Parent);
    }
}
