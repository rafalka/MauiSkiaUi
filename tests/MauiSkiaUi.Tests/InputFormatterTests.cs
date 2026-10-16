using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Input formatters (D9): the editor's hook (<see cref="SkUiTextEditor.Replace"/>, composition commits, undo), the built-in
/// formatters (filtering, length limits, masks) and their order.
/// </summary>
public class InputFormatterTests
{
    private static SkUiTextEditor Editor(params SkUiTextInputFormatter[] formatters)
    {
        var editor = new SkUiTextEditor { Formatters = formatters };
        return editor;
    }

    private static void Type(SkUiTextEditor editor, string text)
    {
        foreach (var character in text)
            editor.InsertText(character.ToString());
    }

    [Fact]
    public void AllowFilterDropsWhatDoesNotMatchAndKeepsTheCaretAfterTheTyping()
    {
        var editor = Editor(SkUiFilteringFormatter.DigitsOnly());
        Type(editor, "1a2");
        Assert.Equal("12", editor.Text);
        Assert.Equal(2, editor.Caret);
        editor.Select(1, 1);
        Assert.False(editor.InsertText("x"));
        Assert.Equal(("12", 1), (editor.Text, editor.Caret));
        // Paste keeps the digits, the caret after them.
        Assert.True(editor.InsertText("(34) 5", SkUiTextEditKind.Other));
        Assert.Equal(("13452", 4), (editor.Text, editor.Caret));
    }

    [Fact]
    public void AnEditMadeDifferentlyThanAskedIsFlaggedForThePlatformInput()
    {
        // UIKit assumes its insertion landed as sent: the field flags what it changed so the view can tell UIKit.
        static bool Adjusted(SkUiTextEditor editor, Action<SkUiTextEditor> edit)
        {
            editor.InputAdjusted = false;
            edit(editor);
            return editor.InputAdjusted;
        }

        var editor = Editor(SkUiFilteringFormatter.DigitsOnly());
        Assert.False(Adjusted(editor, e => e.InsertText("1")));
        Assert.True(Adjusted(editor, e => e.InsertText("x")));
        Assert.True(Adjusted(editor, e => e.InsertText("2 3")));
        Assert.Equal("123", editor.Text);
        Assert.False(Adjusted(editor, e => e.DeleteBackward()));

        var limited = new SkUiTextEditor { MaxLength = 3 };
        Assert.False(Adjusted(limited, e => e.InsertText("ab")));
        Assert.True(Adjusted(limited, e => e.InsertText("cd")));
        Assert.Equal("abc", limited.Text);

        var upper = new SkUiTextEditor { Transform = TextTransform.Uppercase };
        Assert.True(Adjusted(upper, e => e.InsertText("a")));
        Assert.False(Adjusted(upper, e => e.InsertText("B")));

        var mask = Editor(new SkUiMaskFormatter("(000) 000"));
        Assert.True(Adjusted(mask, e => e.InsertText("1")));
        Assert.Equal("(1", mask.Text);

        var readOnly = new SkUiTextEditor { IsReadOnly = true };
        Assert.True(Adjusted(readOnly, e => e.InsertText("a")));
    }

    [Fact]
    public void RegexFiltersAllowOrDenyWithAReplacement()
    {
        var editor = Editor(new SkUiFilteringFormatter("[a-z]+"));
        editor.InsertText("ab1cD", SkUiTextEditKind.Other);
        Assert.Equal("abc", editor.Text);

        editor = Editor(new SkUiFilteringFormatter(@"\s+", SkUiFilteringMode.Deny, "_"));
        editor.InsertText("a  b c", SkUiTextEditKind.Other);
        Assert.Equal(("a_b_c", 5), (editor.Text, editor.Caret));
    }

    [Fact]
    public void PredicateFiltersWorkOnRunes()
    {
        var editor = Editor(new SkUiFilteringFormatter(rune => rune.Value > 0xFFFF, SkUiFilteringMode.Deny));
        editor.InsertText("a👍b", SkUiTextEditKind.Other);
        Assert.Equal(("ab", 2), (editor.Text, editor.Caret));
    }

    [Fact]
    public void LengthLimitCountsGraphemesAndKeepsWhatFitsOfAPaste()
    {
        var editor = Editor(new SkUiLengthLimitingFormatter(3));
        editor.InsertText("👍🏽é", SkUiTextEditKind.Other);
        Assert.Equal(2, new System.Globalization.StringInfo(editor.Text).LengthInTextElements);
        editor.InsertText("xyz", SkUiTextEditKind.Other);
        Assert.Equal("👍🏽éx", editor.Text);
        Assert.Equal(editor.Text.Length, editor.Caret);
        Assert.False(editor.InsertText("q"));
        // Replacing the emoji makes room for one.
        editor.Select(0, 4);
        Assert.True(editor.InsertText("ab", SkUiTextEditKind.Other));
        Assert.Equal("aéx", editor.Text);
    }

    [Fact]
    public void DigitLimitCountsDigitsOnly()
    {
        var editor = Editor(new SkUiLengthLimitingFormatter(4, SkUiTextLengthUnit.Digits));
        editor.InsertText("12-3", SkUiTextEditKind.Other);
        Assert.Equal("12-3", editor.Text);
        // One digit fits: the separator after it goes with the rest.
        editor.InsertText("4-56", SkUiTextEditKind.Other);
        Assert.Equal("12-34", editor.Text);
        Assert.False(editor.InsertText("7"));
        Assert.True(editor.InsertText(" "));
        Assert.Equal("12-34 ", editor.Text);
    }

    [Fact]
    public void MaskInsertsLiteralsAsTheUserReachesThem()
    {
        var mask = new SkUiMaskFormatter("(000) 000-0000");
        var editor = Editor(mask);
        Type(editor, "555");
        Assert.Equal(("(555", 4), (editor.Text, editor.Caret));
        Type(editor, "1234567");
        Assert.Equal(("(555) 123-4567", 14), (editor.Text, editor.Caret));
        // Past the mask, and characters no placeholder takes.
        Assert.False(editor.InsertText("8"));
        Assert.Equal("5551234567", mask.Unmask(editor.Text));
        Assert.True(mask.IsComplete(editor.Text));
        Assert.False(mask.IsComplete("(555) 1"));
    }

    [Fact]
    public void MaskKeepsTypedLiteralsAndRemasksPaste()
    {
        var editor = Editor(new SkUiMaskFormatter("(000) 000-0000"));
        Type(editor, "(555) 1");
        Assert.Equal("(555) 1", editor.Text);
        editor.SelectAll();
        editor.InsertText("555.123.4567 ext", SkUiTextEditKind.Other);
        Assert.Equal("(555) 123-4567", editor.Text);
        // Letters are dropped where digits go.
        editor = Editor(new SkUiMaskFormatter("(000) 000-0000"));
        Type(editor, "55a5");
        Assert.Equal("(555", editor.Text);
    }

    [Fact]
    public void BackspaceAndDeleteOnALiteralRemoveTheCharacterBeyondIt()
    {
        var editor = Editor(new SkUiMaskFormatter("(000) 000-0000"));
        Type(editor, "5551234");
        Assert.Equal("(555) 123-4", editor.Text);
        // Backspace after "-": the 3 before it goes, and the text closes up.
        editor.Select(10, 10);
        Assert.True(editor.DeleteBackward());
        Assert.Equal(("(555) 124", 8), (editor.Text, editor.Caret));
        // Delete before ") ": the 1 after it goes.
        editor.Select(4, 4);
        Assert.True(editor.DeleteForward());
        Assert.Equal(("(555) 24", 6), (editor.Text, editor.Caret));
        // A deletion takes the literals left at the end with it.
        editor = Editor(new SkUiMaskFormatter("(000) 000-0000"));
        Type(editor, "5551");
        Assert.Equal("(555) 1", editor.Text);
        editor.DeleteBackward();
        Assert.Equal(("(555", 4), (editor.Text, editor.Caret));
        Type(editor, " ");
        Assert.Equal("(555) ", editor.Text);
    }

    [Fact]
    public void MaskPlaceholderKindsAndEscapes()
    {
        var editor = Editor(new SkUiMaskFormatter(@"LL-A\0&"));
        editor.InsertText("ab1x!", SkUiTextEditKind.Other);
        Assert.Equal("ab-10x", editor.Text);
        editor = Editor(new SkUiMaskFormatter(@"LL-A\0&"));
        editor.InsertText("a1bz?", SkUiTextEditKind.Other);
        Assert.Equal("ab-z0?", editor.Text);
    }

    [Theory]
    [InlineData("(999) 000-0000")]
    [InlineData("#000")]
    [InlineData("Code 000")]
    [InlineData(">LL")]
    public void MaskRefusesReservedCharacters(string mask)
    {
        Assert.Throws<ArgumentException>(() => new SkUiMaskFormatter(mask));
    }

    [Fact]
    public void MaskTakesEscapedReservedCharacters()
    {
        var editor = Editor(new SkUiMaskFormatter(@"\#\9-00"));
        editor.InsertText("12", SkUiTextEditKind.Other);
        Assert.Equal("#9-12", editor.Text);
    }

    [Fact]
    public void FormattersRunInOrder()
    {
        var calls = new List<string>();
        var editor = Editor(
            SkUiTextInputFormatter.Create((_, value) => { calls.Add("upper"); return value with { Text = value.Text.ToUpperInvariant() }; }),
            SkUiTextInputFormatter.Create((_, value) => { calls.Add("check"); Assert.Equal(value.Text.ToUpperInvariant(), value.Text); return value; }));
        editor.InsertText("ab");
        Assert.Equal("AB", editor.Text);
        Assert.Equal(["upper", "check"], calls);
    }

    [Fact]
    public void AFormatterMayOnlyMoveTheCaret()
    {
        // A second "-" steps over the one already there.
        var editor = Editor(SkUiTextInputFormatter.Create((old, value) =>
            value.Text.Count(c => c == '-') > 1 ? new SkUiTextEditValue(old.Text, old.Text.IndexOf('-') + 1) : value));
        editor.InsertText("12-34", SkUiTextEditKind.Other);
        editor.Select(1, 1);
        Assert.False(editor.InsertText("-"));
        Assert.Equal(("12-34", 3), (editor.Text, editor.Caret));
    }

    [Fact]
    public void AFormattedEditIsOneUndoStepAndUndoIsNotFormatted()
    {
        var formats = 0;
        var mask = new SkUiMaskFormatter("00-00");
        var editor = Editor(SkUiTextInputFormatter.Create((old, value) => { formats++; return mask.Format(old, value); }));
        Type(editor, "123");
        Assert.Equal("12-3", editor.Text);
        var before = formats;
        Assert.True(editor.Undo());
        Assert.Equal("", editor.Text);
        Assert.True(editor.Redo());
        Assert.Equal(("12-3", 4), (editor.Text, editor.Caret));
        Assert.Equal(before, formats);
    }

    [Fact]
    public void ACompositionIsFormattedWhenCommitted()
    {
        var editor = Editor(SkUiFilteringFormatter.DigitsOnly());
        editor.InsertText("1");
        editor.SetComposingText("か");
        Assert.Equal("1か", editor.Text);
        Assert.True(editor.HasComposition);
        editor.FinishComposing();
        Assert.Equal(("1", 1), (editor.Text, editor.Caret));

        editor.SetComposingText("2x");
        Assert.Equal("12x", editor.Text);
        editor.InsertText("2x3");
        Assert.Equal(("123", 3), (editor.Text, editor.Caret));
        Assert.False(editor.HasComposition);
    }

    [Fact]
    public void ACommittedCompositionIsAnInsertionToTheFormatters()
    {
        var limited = Editor(new SkUiLengthLimitingFormatter(3));
        limited.InsertText("12");
        limited.SetComposingText("あい");
        limited.FinishComposing();
        Assert.Equal(("12あ", 3), (limited.Text, limited.Caret));

        // A commit over the marked text (iOS's insertText) too, and a formatter can refuse it by returning the old value.
        var refusing = Editor(SkUiTextInputFormatter.Create((old, value) => value.Text.Length > 2 ? old : value));
        refusing.InsertText("12");
        refusing.SetComposingText("あ");
        refusing.InsertText("あ");
        Assert.Equal(("12", 2), (refusing.Text, refusing.Caret));
        Assert.False(refusing.HasComposition);

        SkUiTextEditValue? seenOld = null;
        var watching = Editor(SkUiTextInputFormatter.Create((old, value) => { seenOld = old; return value; }));
        watching.InsertText("ab");
        watching.Select(1, 1);
        watching.SetComposingText("か");
        watching.FinishComposing();
        Assert.Equal(new SkUiTextEditValue("ab", 1), seenOld);
        Assert.Equal("aかb", watching.Text);
    }

    [Fact]
    public void MaxLengthStillHoldsAfterFormatters()
    {
        var editor = Editor(new SkUiMaskFormatter("(000) 000"));
        editor.MaxLength = 6;
        editor.InsertText("55512", SkUiTextEditKind.Other);
        Assert.Equal("(555) ", editor.Text);
    }

    [Fact]
    public void SetTextIsNotFormatted()
    {
        var editor = Editor(SkUiFilteringFormatter.DigitsOnly());
        editor.SetText("a1");
        Assert.Equal("a1", editor.Text);
    }
}

/// <summary>Input formatters on the controls: typing through the focus manager, assigned text, both layers.</summary>
[Collection(GlobalStateCollection.Name)]
public class InputFormatterControlTests
{
    private static void Type(SkUiView root, string text)
    {
        foreach (var character in text)
            root.FocusManager.KeyDown(character == ' ' ? SkUiKey.Space : SkUiKey.Character, character: character);
    }

    [Fact]
    public void EntryFormatsTypingAndTextFollows()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily }.SetInputFormatters(new SkUiMaskFormatter("0000 0000"));
        var root = new SkUiVerticalStackLayout { Children = { entry } };
        SkUiTestHelpers.Arrange(root, 300, 200);
        var changes = new List<string?>();
        entry.TextChanged += (_, args) => changes.Add(args.NewTextValue);
        Assert.True(entry.Focus());
        Type(root, "12345x");
        Assert.Equal("1234 5", entry.Text);
        Assert.Equal(6, entry.CursorPosition);
        Assert.Equal(["1", "12", "123", "1234", "1234 5"], changes);
        root.FocusManager.KeyDown(SkUiKey.Backspace);
        Assert.Equal("1234", entry.Text);
    }

    [Fact]
    public void ChangingTheFormattersFormatsTheTextAlreadyThere()
    {
        var entry = new SkUiEntry { Text = "a1 b2" };
        var changes = new List<string?>();
        entry.TextChanged += (_, args) => changes.Add(args.NewTextValue);
        entry.InputFormatters.Add(SkUiFilteringFormatter.DigitsOnly());
        Assert.Equal("12", entry.Text);
        Assert.Equal("12", entry.Model.Editor.Text);
        entry.SetInputFormatters(new SkUiMaskFormatter("0-0"));
        Assert.Equal("1-2", entry.Text);
        // Removing them leaves the text as it is.
        entry.InputFormatters.Clear();
        Assert.Equal("1-2", entry.Text);
        Assert.Equal(["12", "1-2"], changes);

        var core = new SkUiCoreEntry().SetText("x9y").SetInputFormatters(SkUiFilteringFormatter.DigitsOnly());
        Assert.Equal("9", core.Text);
    }

    [Fact]
    public void AssignedTextIsFormattedOnlyWhenAsked()
    {
        var entry = new SkUiEntry();
        entry.InputFormatters.Add(new SkUiMaskFormatter("000-000"));
        entry.Text = "123456";
        Assert.Equal("123456", entry.Text);
        // Turning it on formats the text already there.
        entry.FormatsAssignedText = true;
        Assert.Equal("123-456", entry.Text);
        Assert.Equal("123-456", entry.Model.Editor.Text);
        entry.Text = "98 76 54";
        Assert.Equal("987-654", entry.Text);
        // A formatter added later formats it again.
        entry.InputFormatters.Insert(0, new SkUiLengthLimitingFormatter(4, SkUiTextLengthUnit.Digits));
        Assert.Equal("987-6", entry.Text);
    }

    [Fact]
    public void CoreEntryFormatsLikeTheSkUiEntry()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var core = new SkUiCoreEntry().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetInputFormatters(SkUiFilteringFormatter.DigitsOnly());
        var root = new SkUiVerticalStackLayout { Children = { new SkUiCoreHost().SetContent(core) } };
        SkUiTestHelpers.Arrange(root, 300, 200);
        Assert.True(core.Focus());
        Type(root, "a1 b2");
        Assert.Equal("12", core.Text);
        core.SetText("x3");
        Assert.Equal("x3", core.Text);
        core.SetFormatsAssignedText(true);
        Assert.Equal("3", core.Text);
        core.SetText("4y5");
        Assert.Equal("45", core.Text);
    }

    [Fact]
    public void InputFromThePlatformIsFormattedAndTheProxyFollows()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily }.SetInputFormatters(SkUiFilteringFormatter.DigitsOnly());
        var root = new SkUiVerticalStackLayout { Children = { entry } };
        SkUiTestHelpers.Arrange(root, 300, 200);
        ISkUiTextInputClient client = entry.Model;
        client.ReplaceText(0, 0, "1a");
        Assert.Equal(("1", 1), (entry.Text, entry.CursorPosition));
        client.SetComposingText("ab");
        Assert.Equal("1ab", entry.Model.Editor.Text);
        client.FinishComposing();
        Assert.Equal("1", entry.Text);
        Assert.True(client.SetTextFromAssistant("4b2"));
        Assert.Equal("42", entry.Text);
    }
}

/// <summary>Input formatters set in XAML.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class InputFormatterXamlTests
{
    [Fact]
    public void FormattersAreSetInXaml()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <VerticalStackLayout>
                <sk:SkUiEntry Text="5551234567" FormatsAssignedText="True">
                  <sk:SkUiEntry.InputFormatters>
                    <sk:SkUiMaskFormatter Mask="(000) 000-0000" />
                  </sk:SkUiEntry.InputFormatters>
                </sk:SkUiEntry>
                <sk:SkUiEditor Text="a1 b2 c3 d4" FormatsAssignedText="True">
                  <sk:SkUiEditor.InputFormatters>
                    <sk:SkUiFilteringFormatter Pattern="[0-9 ]" />
                    <sk:SkUiLengthLimitingFormatter MaxLength="3" Unit="Digits" />
                  </sk:SkUiEditor.InputFormatters>
                </sk:SkUiEditor>
              </VerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var fields = ((VerticalStackLayout)root.Content).Children;
        var entry = (SkUiEntry)fields[0];
        Assert.IsType<SkUiMaskFormatter>(Assert.Single(entry.InputFormatters));
        Assert.Equal("(555) 123-4567", entry.Text);
        var editor = (SkUiEditor)fields[1];
        Assert.Equal(2, editor.InputFormatters.Count);
        Assert.Equal("1 2 3", editor.Text);
    }
}
