using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// What the platform's autofill (Android, D14) gets from the shared engine: the surface's form (the fields that can be typed in,
/// both layers, in tree order), every text change of a field it knows, fills as one formatted undo step, and fields leaving
/// their surface. The Android structure itself is checked on devices (<c>docs/design/Testing.md</c>).
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class TextFieldAutofillTests
{
    private sealed class TestAutofill : ISkUiAutofillHost
    {
        public readonly List<string> Calls = [];
        public void TextChanged(ISkUiTextInputClient client) => Calls.Add("text " + client.Editor.Text);
        public void Removed(ISkUiTextInputClient client) => Calls.Add("removed");
    }

    private static ISkUiTextInputClient ClientOf(ISkUiTextInputNode field) => field.TextInputClient;

    private static SkUiFrameRenderer Draw(SkUiView root)
    {
        SkUiTestHelpers.Arrange(root, 300, 400);
        var renderer = new SkUiFrameRenderer(root, _ => { }, () => { }, () => { });
        renderer.PresentFrame();
        using var bitmap = new SKBitmap(300, 400);
        using var canvas = new SKCanvas(bitmap);
        renderer.Render(canvas, bitmap.Info, TimeSpan.Zero);
        return renderer;
    }

    [Fact]
    public void TheFormIsTheFieldsThatCanBeTypedInOnBothLayersInTreeOrder()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var user = new SkUiEntry { Placeholder = "User", AutofillHint = SkUiAutofillHints.Username, FontFamily = SkUiTestHelpers.BundledFontFamily };
        var hidden = new SkUiEntry { IsVisible = false, FontFamily = SkUiTestHelpers.BundledFontFamily };
        var disabled = new SkUiEditor { IsEnabled = false, FontFamily = SkUiTestHelpers.BundledFontFamily };
        // Out of accessibility, still typed in (and filled).
        var search = new SkUiSearchBar { FontFamily = SkUiTestHelpers.BundledFontFamily };
        var core = new SkUiCoreEntry().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetIsPassword(true);
        var root = new SkUiVerticalStackLayout
        {
            Children =
            {
                user, hidden, disabled,
                new SkUiVerticalStackLayout { IsAccessibilityEnabled = false, Children = { search } },
                new SkUiCoreHost().SetContent(core)
            }
        };
        SkUiTestHelpers.Arrange(root, 300, 400);

        var form = new List<ISkUiTextInputClient>();
        root.FocusManager.CollectTextFields(form);

        Assert.Equal([ClientOf(user), ClientOf(search), ClientOf(core)], form);
        Assert.Equal("User", form[0].Placeholder);
        Assert.Equal(SkUiAutofillHints.Username, form[0].Options.AutofillHint);
        // A password field without a hint is a password to autofill.
        Assert.Equal(SkUiAutofillHints.Password, form[2].Options.AutofillHint);
    }

    [Fact]
    public void AKnownFieldReportsEveryTextChange()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily };
        var root = new SkUiVerticalStackLayout { Children = { entry } };
        SkUiTestHelpers.Arrange(root, 300, 400);
        var autofill = new TestAutofill();
        var client = ClientOf(entry);
        client.AutofillHost = autofill;

        entry.Text = "app";
        Assert.True(entry.Focus());
        root.FocusManager.KeyDown(SkUiKey.Character, character: 's');
        Assert.True(client.SetTextFromAssistant("filled"));

        Assert.Equal(["text app", "text apps", "text filled"], autofill.Calls);
    }

    [Fact]
    public void AFillKeepsTheFieldsRulesIsOneUndoStepAndTextFollows()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = new SkUiEntry { MaxLength = 4, FontFamily = SkUiTestHelpers.BundledFontFamily };
        var root = new SkUiVerticalStackLayout { Children = { entry } };
        SkUiTestHelpers.Arrange(root, 300, 400);
        var client = ClientOf(entry);

        Assert.True(client.SetTextFromAssistant("username"));
        Assert.Equal("user", entry.Text);
        Assert.Equal(4, entry.CursorPosition);

        Assert.True(client.Editor.Undo());
        Assert.Equal(string.Empty, entry.Text);

        entry.IsReadOnly = true;
        Assert.False(client.SetTextFromAssistant("other"));
        Assert.Equal(string.Empty, entry.Text);
    }

    [Fact]
    public void FieldsLeavingTheirSurfaceAreLetGoOnBothLayers()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily };
        var core = new SkUiCoreEntry().SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        var host = new SkUiCoreHost().SetContent(core);
        var root = new SkUiVerticalStackLayout { Children = { entry, host } };
        using var renderer = Draw(root);
        var autofill = new TestAutofill();
        ClientOf(entry).AutofillHost = autofill;
        ClientOf(core).AutofillHost = autofill;

        root.Children.Remove(entry);
        host.SetContent(null);

        Assert.Equal(["removed", "removed"], autofill.Calls);
        Assert.Null(ClientOf(entry).AutofillHost);
        Assert.Null(ClientOf(core).AutofillHost);
        // Let go: later edits are not reported.
        entry.Text = "after";
        Assert.Equal(2, autofill.Calls.Count);
    }
}
