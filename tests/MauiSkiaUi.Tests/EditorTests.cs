using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The drawn editor (D5): multiline text on the shared field engine — line breaks, wrapping, moving by lines, AutoSize, scrolling
/// inside the editor (handing the rest to its scroller), MAUI's <c>Completed</c> on unfocus, and the Core twin.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class EditorTests
{
    private static (SkUiVerticalStackLayout Root, SkUiEditor Editor) Field(SkUiEditor? editor = null, double width = 200)
    {
        editor ??= new SkUiEditor();
        editor.FontFamily = SkUiTestHelpers.BundledFontFamily;
        editor.WidthRequest = width;
        editor.HorizontalOptions = LayoutOptions.Start;
        var root = new SkUiVerticalStackLayout { Children = { editor } };
        SkUiTestHelpers.Arrange(root, 400, 600);
        return (root, editor);
    }

    private static void Key(SkUiView root, SkUiKey key, SkUiKeyModifiers modifiers = SkUiKeyModifiers.None) => root.FocusManager.KeyDown(key, modifiers);

    [Fact]
    public void EnterInsertsALineBreakAndTheVerticalAlignmentStartsAtTheTop()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, editor) = Field();
        Assert.Equal(TextAlignment.Start, editor.VerticalTextAlignment);
        var completed = 0;
        editor.Completed += (_, _) => completed++;
        editor.Focus();
        root.FocusManager.KeyDown(SkUiKey.Character, character: 'a');
        Key(root, SkUiKey.Enter);
        root.FocusManager.KeyDown(SkUiKey.Character, character: 'b');
        Assert.Equal("a\nb", editor.Text);
        Assert.Equal(0, completed);
        editor.Unfocus();
        Assert.Equal(1, completed);
    }

    [Fact]
    public void EnterAsAKeyWhileComposingKeepsTheComposedWord()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, editor) = Field();
        editor.Focus();
        ISkUiTextInputClient client = editor.Model;
        // Samsung Keyboard: the word is still composing when Enter comes as a key event.
        client.SetComposingText("Ab");
        Key(root, SkUiKey.Enter);
        Assert.Equal("Ab\n", editor.Text);
        Assert.False(editor.Model.Editor.HasComposition);
        client.SetComposingText("C");
        root.FocusManager.KeyDown(SkUiKey.Character, character: 'd');
        Assert.Equal("Ab\nCd", editor.Text);
    }

    [Fact]
    public void BackspaceAtALineStartJoinsTheLines()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, editor) = Field();
        editor.Focus();
        root.FocusManager.KeyDown(SkUiKey.Character, character: 'a');
        Key(root, SkUiKey.Enter);
        Key(root, SkUiKey.Enter);
        Assert.Equal("a\n\n", editor.Text);
        Key(root, SkUiKey.Backspace);
        Assert.Equal("a\n", editor.Text);
        Key(root, SkUiKey.Backspace);
        Assert.Equal("a", editor.Text);
        Assert.Equal(1, editor.CursorPosition);
    }

    [Fact]
    public void LongTextWrapsAndUpDownMoveByLines()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, editor) = Field(new SkUiEditor { Text = "one two three four five six seven eight nine ten" }, width: 120);
        editor.Model.Measure(120, 600);
        Assert.True(editor.Model.Layout.Lines.Count > 2);
        editor.Focus();
        editor.CursorPosition = 0;
        Key(root, SkUiKey.Down);
        var second = editor.Model.Layout.Lines[1];
        Assert.InRange(editor.CursorPosition, second.Start, second.End);
        Key(root, SkUiKey.Up);
        Assert.Equal(0, editor.CursorPosition);
        Key(root, SkUiKey.End);
        // End of the first (wrapped) line: the caret stays on it.
        Assert.Equal(editor.Model.Layout.Lines[0].End, editor.CursorPosition);
        Assert.True(editor.Model.Editor.CaretUpstream);
    }

    [Fact]
    public void AutoSizeGrowsTheEditorWithItsText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (_, editor) = Field(new SkUiEditor { AutoSize = EditorAutoSizeOption.TextChanges });
        var one = ((IView)editor).Measure(200, double.PositiveInfinity).Height;
        editor.Text = "1\n2\n3\n4\n5";
        var five = ((IView)editor).Measure(200, double.PositiveInfinity).Height;
        Assert.True(five > one + 50, $"{one} → {five}");
    }

    [Fact]
    public void WithoutAutoSizeTypingDoesNotMeasureAgain()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (root, editor) = Field();
        editor.Focus();
        var measured = 0;
        editor.MeasureInvalidated += (_, _) => measured++;
        root.FocusManager.KeyDown(SkUiKey.Character, character: 'x');
        Key(root, SkUiKey.Enter);
        Assert.Equal(0, measured);
        Assert.Equal("x\n", editor.Text);
    }

    [Fact]
    public void TheWheelScrollsTheTextAndHandsOnTheRest()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var editor = new SkUiEditor
        {
            Text = string.Join("\n", Enumerable.Range(1, 30).Select(index => $"line {index}")),
            HeightRequest = 100,
            FontFamily = SkUiTestHelpers.BundledFontFamily
        };
        var content = new SkUiVerticalStackLayout { Children = { editor, new SkUiBox { HeightRequest = 2000 } } };
        var scroller = new SkUiScrollView { Content = content };
        SkUiTestHelpers.Arrange(scroller, 300, 400);
        // The caret at the start keeps the text at its top.
        editor.CursorPosition = 0;
        editor.Model.Draw(new SkiaSharp.SKCanvas(new SkiaSharp.SKBitmap(300, 100)), 300, 100);
        // Down: the editor scrolls its text first.
        scroller.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Wheel, new Point(50, 50), WheelDelta: -40));
        Assert.Equal(0, scroller.ScrollY);
        Assert.True(editor.Model.CanScroll(-1));
        // Up past the text's top: the editor stops there and the rest goes on (the scroller is at its start).
        scroller.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Wheel, new Point(50, 50), WheelDelta: 4000));
        Assert.False(editor.Model.CanScroll(-1));
        // Down past the text's end: the rest scrolls the page.
        scroller.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Wheel, new Point(50, 50), WheelDelta: -4000));
        Assert.False(editor.Model.CanScroll(1));
        Assert.True(scroller.ScrollY > 0);
    }

    [Fact]
    public void PasteKeepsLineBreaksInAnEditorAndNotInAnEntry()
    {
        var editor = new SkUiEditor();
        editor.Model.Editor.InsertText("a\r\nb", SkUiTextEditKind.Other);
        Assert.Equal("a\nb", editor.Text);
        var entry = new SkUiEntry();
        entry.Model.Editor.InsertText("a\r\nb", SkUiTextEditKind.Other);
        Assert.Equal("a b", entry.Text);
    }

    [Fact]
    public void CoreEditorEditsLikeTheSkUiEditor()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var core = new SkUiCoreEditor().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetAutoSize(EditorAutoSizeOption.TextChanges);
        var root = new SkUiVerticalStackLayout { Children = { new SkUiCoreHost().SetContent(core) } };
        SkUiTestHelpers.Arrange(root, 300, 400);
        var completed = 0;
        core.Completed += (_, _) => completed++;
        Assert.True(core.Focus());
        root.FocusManager.KeyDown(SkUiKey.Character, character: 'a');
        Key(root, SkUiKey.Enter);
        root.FocusManager.KeyDown(SkUiKey.Character, character: 'b');
        Assert.Equal("a\nb", core.Text);
        Assert.Equal(TextAlignment.Start, core.VerticalTextAlignment);
        core.Unfocus();
        Assert.Equal(1, completed);
    }
}
