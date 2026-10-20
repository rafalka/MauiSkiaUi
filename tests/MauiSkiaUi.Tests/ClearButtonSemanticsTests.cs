using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// M7: a text field's and a picker's clear button is an element of its own while it shows (a button child of the field's
/// element, where it is drawn), with the field's id plus <see cref="SkUiAccessibility.ClearButtonAutomationIdSuffix"/>, and
/// activating it clears, on both layers.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class ClearButtonSemanticsTests : IDisposable
{
    private const string FieldId = "Search";
    private const string ClearId = FieldId + SkUiAccessibility.ClearButtonAutomationIdSuffix;
    private static readonly string[] Fruits = ["Apple", "Pear", "Plum"];

    private readonly TestUiContext _ui = TestUiContext.Install();

    public void Dispose() => _ui.Dispose();

    private static SkUiVerticalStackLayout Root(SkUiView view)
    {
        var root = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        root.Children.Add(view);
        SkUiTestHelpers.Arrange(root, 300, 300);
        return root;
    }

    private static SkUiSemanticsNode? Clear(SkUiSemanticsTree tree) => tree.Flatten().SingleOrDefault(node => node.PartIndex >= 0);

    /// <summary>The field's element and its clear button's, which is its last child.</summary>
    private static (SkUiSemanticsNode Field, SkUiSemanticsNode Clear) Elements(SkUiView root)
    {
        var tree = SkUiSemanticsTree.Build(root);
        var clear = Assert.Single(tree.Flatten(), node => node.PartIndex >= 0);
        var field = clear.Parent!;
        Assert.Same(clear, field.Children[^1]);
        return (field, clear);
    }

    private static void AssertClearButton(SkUiSemanticsNode field, SkUiSemanticsNode clear, string label, double width)
    {
        Assert.Equal(SkUiSemanticsRole.Button, clear.Role);
        Assert.Equal(label, clear.Label);
        Assert.Equal(SkUiSemanticsActions.Activate, clear.Actions);
        Assert.Equal(ClearId, clear.AutomationId);
        Assert.False(clear.IsFocusable);
        Assert.NotEqual(field.Id, clear.Id);
        // Drawn inside the field at its end side, as wide as the look's button.
        Assert.True(field.Bounds.Contains(clear.Bounds.Center));
        Assert.True(clear.Bounds.Right <= field.Bounds.Right + 0.5);
        Assert.Equal(width, clear.Bounds.Width, 3);
    }

    [Fact]
    public void AnEntrysClearButtonIsAnElementWhileItShows()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = new SkUiEntry
        {
            Text = "pears", ClearButtonVisibility = ClearButtonVisibility.WhileEditing, AutomationId = FieldId,
            FontFamily = SkUiTestHelpers.BundledFontFamily
        };
        var root = Root(entry);
        Assert.Null(Clear(SkUiSemanticsTree.Build(root))); // only while editing

        entry.Focus();
        var (field, clear) = Elements(root);
        Assert.Equal(SkUiSemanticsRole.TextField, field.Role);
        Assert.Equal(FieldId, field.AutomationId);
        AssertClearButton(field, clear, SkUiStrings.Current.ClearTextButtonText, SkUiLook.Current.TextFieldClearButtonWidth);
        Assert.Equal(clear.Id, Clear(SkUiSemanticsTree.Build(root))!.Id); // a stable id

        // Activation clears, as a tap on it; the field keeps focus and the button goes.
        var owner = new SkUiSemanticsOwner(root);
        Assert.True(owner.Perform(clear.Id, SkUiSemanticsActions.Activate));
        Assert.Equal("", entry.Text);
        Assert.True(entry.IsFocused);
        Assert.Null(Clear(SkUiSemanticsTree.Build(root)));
        Assert.False(owner.Perform(clear.Id, SkUiSemanticsActions.Activate)); // gone from the tree
    }

    [Fact]
    public void AClearButtonHasNoIdWithoutTheFieldsAndIsDisabledWithIt()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var search = new SkUiSearchBar { Text = "pears", FontFamily = SkUiTestHelpers.BundledFontFamily };
        var root = Root(search);
        var (_, clear) = Elements(root);
        Assert.Null(clear.AutomationId);
        Assert.Equal(SkUiStrings.Current.ClearTextButtonText, clear.Label);

        // A disabled field, or one under a disabled parent, shows no clear button.
        search.IsEnabled = false;
        Assert.Null(Clear(SkUiSemanticsTree.Build(root)));
        search.IsEnabled = true;
        root.IsEnabled = false;
        Assert.Null(Clear(SkUiSemanticsTree.Build(root)));
        Assert.False(new SkUiSemanticsOwner(root).Perform(clear.Id, SkUiSemanticsActions.Activate));
        Assert.Equal("pears", search.Text);
    }

    [Fact]
    public void ACoreEntrysClearButtonIsTheSameElement()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var core = new SkUiCoreEntry().SetText("pears").SetClearButtonVisibility(ClearButtonVisibility.WhileEditing);
        core.SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        core.SetAutomationId(FieldId);
        var root = Root(new SkUiCoreHost().SetContent(core));
        Assert.True(core.Focus());

        var (field, clear) = Elements(root);
        AssertClearButton(field, clear, SkUiStrings.Current.ClearTextButtonText, SkUiLook.Current.TextFieldClearButtonWidth);
        Assert.True(new SkUiSemanticsOwner(root).Perform(clear.Id, SkUiSemanticsActions.Activate));
        Assert.Equal("", core.Text);
    }

    [Fact]
    public void APickersClearButtonIsAnElementWhileSomethingIsChosen()
    {
        var picker = new SkUiPicker { ItemsSource = Fruits, IsClearButtonVisible = true, AutomationId = FieldId };
        var root = Root(picker);
        Assert.Null(Clear(SkUiSemanticsTree.Build(root))); // nothing chosen

        picker.SelectedIndex = 1;
        var (field, clear) = Elements(root);
        Assert.Equal(SkUiSemanticsRole.ComboBox, field.Role);
        AssertClearButton(field, clear, SkUiStrings.Current.ClearText, SkUiLook.Current.PickerClearButtonWidth);

        picker.ClearText = "Remove";
        Assert.Equal("Remove", Clear(SkUiSemanticsTree.Build(root))!.Label);
        Assert.True(new SkUiSemanticsOwner(root).Perform(clear.Id, SkUiSemanticsActions.Activate));
        Assert.Equal(-1, picker.SelectedIndex);
        Assert.False(picker.IsOpen); // clears without opening, as a tap on it
        Assert.Null(Clear(SkUiSemanticsTree.Build(root)));
    }

    [Fact]
    public void ACorePickersClearButtonIsTheSameElement()
    {
        var core = new SkUiCorePicker().SetIsClearButtonVisible(true);
        foreach (var fruit in Fruits)
            core.Items.Add(fruit);
        core.SetSelectedIndex(2);
        core.SetAutomationId(FieldId);
        var root = Root(new SkUiCoreHost().SetContent(core));

        var (field, clear) = Elements(root);
        AssertClearButton(field, clear, SkUiStrings.Current.ClearText, SkUiLook.Current.PickerClearButtonWidth);
        Assert.True(new SkUiSemanticsOwner(root).Perform(clear.Id, SkUiSemanticsActions.Activate));
        Assert.Equal(-1, core.SelectedIndex);
    }

    [Fact]
    public void TheClearButtonIsHitInsideTheField()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var search = new SkUiSearchBar { Text = "pears", FontFamily = SkUiTestHelpers.BundledFontFamily };
        var root = Root(search);
        var tree = SkUiSemanticsTree.Build(root);
        var clear = Clear(tree)!;
        Assert.Same(clear, tree.HitTest(clear.Bounds.Center)); // explore by touch reaches it
        Assert.Same(clear.Parent, tree.HitTest(new Point(clear.Parent!.Bounds.Left + 30, clear.Bounds.Center.Y)));
    }
}
