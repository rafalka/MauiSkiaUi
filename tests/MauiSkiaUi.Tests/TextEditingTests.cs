using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The editing engine of drawn text fields (D1): caret stops, the editor's text / selection / composition / undo, and the
/// edit layout's caret geometry, hit-testing and selection boxes.
/// </summary>
public class TextEditingTests
{
    private sealed class Owner : ISkUiTextEditorOwner
    {
        public int TextChanges;
        public int SelectionChanges;
        public bool LastByUser;

        public void OnEditorTextChanged(string oldText, bool byUser)
        {
            TextChanges++;
            LastByUser = byUser;
        }

        public void OnEditorSelectionChanged() => SelectionChanges++;
    }

    private static SkUiTextEditor Editor(string text, int caret = -1, Owner? owner = null)
    {
        var editor = new SkUiTextEditor(owner) { WordRightGoesToWordEnd = false };
        editor.SetText(text);
        editor.Select(caret < 0 ? text.Length : caret, caret < 0 ? text.Length : caret);
        return editor;
    }

    [Theory]
    [InlineData("a👍🏽b", 1, 5)] // emoji with a skin tone modifier: one stop
    [InlineData("éx", 0, 2)] // e + combining acute
    [InlineData("🇵🇱🇺🇸", 0, 4)] // flags
    [InlineData("ab\n", 2, 3)] // a line break before a caret at a line's start
    [InlineData("a\n\nb", 2, 3)] // an empty line
    [InlineData("a\r\nb", 1, 3)] // CR LF: one stop
    public void GraphemeStopsKeepClustersWhole(string text, int from, int next)
    {
        Assert.Equal(next, SkUiTextBoundaries.NextGrapheme(text, from));
        Assert.Equal(from, SkUiTextBoundaries.PreviousGrapheme(text, next));
    }

    [Fact]
    public void WordStopsSkipSpacesAndKeepApostrophesInWords()
    {
        const string text = "don't stop, now";
        Assert.Equal(6, SkUiTextBoundaries.PreviousWordStart(text, 10));
        Assert.Equal(0, SkUiTextBoundaries.PreviousWordStart(text, 5));
        Assert.Equal(5, SkUiTextBoundaries.NextWordEnd(text, 0));
        Assert.Equal(6, SkUiTextBoundaries.NextWordStart(text, 0));
        Assert.Equal(10, SkUiTextBoundaries.NextWordStart(text, 6));
        Assert.Equal((0, 5), SkUiTextBoundaries.WordAt(text, 2));
        Assert.Equal((10, 11), SkUiTextBoundaries.WordAt(text, 10)); // punctuation is a word of its own
        Assert.Equal((5, 6), SkUiTextBoundaries.WordAt(text, 5)); // white space between words
        Assert.Equal((12, 15), SkUiTextBoundaries.WordAt(text, 15)); // past the end: the last word
    }

    [Fact]
    public void IdeographsAreWordsOfTheirOwn()
    {
        const string text = "日本語 text";
        Assert.Equal(1, SkUiTextBoundaries.NextWordEnd(text, 0));
        Assert.Equal(2, SkUiTextBoundaries.PreviousWordStart(text, 3));
        Assert.Equal((1, 2), SkUiTextBoundaries.WordAt(text, 1));
    }

    [Fact]
    public void ParagraphAtStopsAtLineBreaks()
    {
        const string text = "one\ntwo two\nthree";
        Assert.Equal((4, 11), SkUiTextBoundaries.ParagraphAt(text, 6));
        Assert.Equal((0, 3), SkUiTextBoundaries.ParagraphAt(text, 0));
        Assert.Equal((12, 17), SkUiTextBoundaries.ParagraphAt(text, 17));
    }

    [Fact]
    public void TypingInsertsAtTheCaretAndReplacesTheSelection()
    {
        var owner = new Owner();
        var editor = Editor("hello world", 5, owner);
        editor.InsertText(",");
        Assert.Equal("hello, world", editor.Text);
        Assert.Equal(6, editor.Caret);
        editor.Select(7, 12);
        editor.InsertText("there");
        Assert.Equal("hello, there", editor.Text);
        Assert.Equal(12, editor.Caret);
        Assert.False(editor.HasSelection);
        Assert.True(owner.LastByUser);
    }

    [Fact]
    public void SetTextPutsTheCaretAtTheEndAndClearsUndo()
    {
        var owner = new Owner();
        var editor = Editor("abc", 1, owner);
        editor.InsertText("x");
        Assert.True(editor.CanUndo);
        editor.SetText("new text");
        Assert.Equal(8, editor.Caret);
        Assert.False(editor.CanUndo);
        Assert.False(owner.LastByUser);
    }

    [Fact]
    public void BackspaceAndDeleteRemoveWholeGraphemesAndWords()
    {
        var editor = Editor("ab👍🏽cd", 6);
        editor.DeleteBackward();
        Assert.Equal("abcd", editor.Text);
        Assert.Equal(2, editor.Caret);
        editor.DeleteForward();
        Assert.Equal("abd", editor.Text);

        editor = Editor("one two three", 8);
        editor.DeleteBackward(word: true);
        Assert.Equal("one three", editor.Text);
        editor.DeleteForward(word: true);
        Assert.Equal("one ", editor.Text);
    }

    [Fact]
    public void BackspacePeelsTheLastMarkOffALetter()
    {
        // As native fields in scripts that build letters from parts: a decomposed é loses its accent first.
        var editor = Editor("é");
        editor.DeleteBackward();
        Assert.Equal("e", editor.Text);
    }

    [Fact]
    public void MaxLengthCutsInputAndRefusesItWhenFull()
    {
        var editor = Editor("abc");
        editor.MaxLength = 5;
        editor.InsertText("defg");
        Assert.Equal("abcde", editor.Text);
        Assert.False(editor.InsertText("x"));
        Assert.Equal("abcde", editor.Text);
        // Replacing a selection makes room.
        editor.Select(0, 2);
        editor.InsertText("XY");
        Assert.Equal("XYcde", editor.Text);
    }

    [Fact]
    public void MaxLengthNeverSplitsASurrogatePair()
    {
        var editor = Editor("ab");
        editor.MaxLength = 3;
        editor.InsertText("😀");
        Assert.Equal("ab", editor.Text);
    }

    [Fact]
    public void ReadOnlyRefusesInputButSelects()
    {
        var editor = Editor("abc");
        editor.IsReadOnly = true;
        Assert.False(editor.InsertText("x"));
        Assert.False(editor.DeleteBackward());
        editor.SelectAll();
        Assert.Equal("abc", editor.SelectedText);
    }

    [Fact]
    public void SingleLineTurnsLineBreaksIntoSpacesAndMultilineNormalizesThem()
    {
        var entry = Editor("");
        entry.InsertText("a\r\nb\nc");
        Assert.Equal("a b c", entry.Text);
        var editor = Editor("");
        editor.IsMultiline = true;
        editor.InsertText("a\r\nb\rc");
        Assert.Equal("a\nb\nc", editor.Text);
    }

    [Fact]
    public void TextTransformAppliesToInput()
    {
        var editor = Editor("");
        editor.Transform = TextTransform.Uppercase;
        editor.InsertText("abc");
        Assert.Equal("ABC", editor.Text);
    }

    [Fact]
    public void UndoJoinsTypingWordByWordAndDeletingRuns()
    {
        var editor = Editor("");
        foreach (var c in "one two")
            editor.InsertText(c.ToString());
        Assert.True(editor.Undo());
        Assert.Equal("one ", editor.Text);
        Assert.True(editor.Undo());
        Assert.Equal("", editor.Text);
        Assert.True(editor.Redo());
        Assert.True(editor.Redo());
        Assert.Equal("one two", editor.Text);

        editor.DeleteBackward();
        editor.DeleteBackward();
        Assert.Equal("one t", editor.Text);
        editor.Undo();
        Assert.Equal("one two", editor.Text);
        Assert.Equal(7, editor.Caret);
    }

    [Fact]
    public void ACaretMoveEndsTheUndoStep()
    {
        var editor = Editor("");
        editor.InsertText("a");
        editor.InsertText("b");
        editor.Move(SkUiTextMove.Left, extend: false);
        editor.Move(SkUiTextMove.Right, extend: false);
        editor.InsertText("c");
        editor.Undo();
        Assert.Equal("ab", editor.Text);
    }

    [Fact]
    public void UndoRestoresTheSelectionAndRedoIsClearedByANewEdit()
    {
        var editor = Editor("hello world");
        editor.Select(0, 5);
        editor.InsertText("bye", SkUiTextEditKind.Other);
        editor.Undo();
        Assert.Equal("hello world", editor.Text);
        Assert.Equal((0, 5), (editor.SelectionStart, editor.SelectionEnd));
        editor.InsertText("x");
        Assert.False(editor.CanRedo);
    }

    [Fact]
    public void CompositionIsMarkedReplacedAndCommittedAsOneUndoStep()
    {
        var owner = new Owner();
        var editor = Editor("ab", 1, owner);
        editor.SetComposingText("k");
        editor.SetComposingText("ka");
        editor.SetComposingText("か");
        Assert.Equal("aかb", editor.Text);
        Assert.Equal((1, 2), (editor.CompositionStart, editor.CompositionEnd));
        Assert.Equal(2, editor.Caret);
        editor.SetComposingText("漢字");
        Assert.Equal("a漢字b", editor.Text);
        editor.FinishComposing();
        Assert.False(editor.HasComposition);
        Assert.Equal("a漢字b", editor.Text);
        editor.Undo();
        Assert.Equal("ab", editor.Text);
    }

    [Fact]
    public void InsertTextCommitsOverTheComposition()
    {
        var editor = Editor("x");
        editor.SetComposingText("nihao");
        editor.InsertText("你好");
        Assert.Equal("x你好", editor.Text);
        Assert.False(editor.HasComposition);
        Assert.Equal(3, editor.Caret);
    }

    [Fact]
    public void CancelledCompositionRemovesItsText()
    {
        var editor = Editor("ab", 1);
        editor.SetComposingText("ㅎ");
        editor.CancelComposition();
        Assert.Equal("ab", editor.Text);
        Assert.False(editor.HasComposition);
    }

    [Fact]
    public void ComposingRegionMarksCommittedTextAndFollowsEditsBeforeIt()
    {
        var editor = Editor("hello world");
        editor.SetComposingRegion(6, 11);
        Assert.Equal((6, 11), (editor.CompositionStart, editor.CompositionEnd));
        editor.Replace(0, 0, ">> ");
        Assert.Equal((9, 14), (editor.CompositionStart, editor.CompositionEnd));
        editor.SetComposingText("there");
        Assert.Equal(">> hello there", editor.Text);
    }

    [Fact]
    public void BatchRaisesOnce()
    {
        var owner = new Owner();
        var editor = Editor("abc", 3, owner);
        owner.TextChanges = owner.SelectionChanges = 0;
        editor.BeginBatch();
        editor.InsertText("d");
        editor.InsertText("e");
        editor.Select(0, 1);
        editor.EndBatch();
        Assert.Equal(1, owner.TextChanges);
        Assert.Equal(1, owner.SelectionChanges);
    }

    [Fact]
    public void LeftAndRightCollapseASelectionToItsSide()
    {
        var editor = Editor("abcdef");
        editor.Select(1, 4);
        editor.Move(SkUiTextMove.Left, extend: false);
        Assert.Equal((1, 1), (editor.Anchor, editor.Caret));
        editor.Select(1, 4);
        editor.Move(SkUiTextMove.Right, extend: false);
        Assert.Equal((4, 4), (editor.Anchor, editor.Caret));
        editor.Move(SkUiTextMove.Left, extend: true);
        editor.Move(SkUiTextMove.WordLeft, extend: true);
        Assert.Equal((4, 0), (editor.Anchor, editor.Caret));
    }

    [Fact]
    public void WordRightFollowsThePlatform()
    {
        var editor = Editor("one two", 0);
        editor.Move(SkUiTextMove.WordRight, extend: false);
        Assert.Equal(4, editor.Caret);
        editor = Editor("one two", 0);
        editor.WordRightGoesToWordEnd = true;
        editor.Move(SkUiTextMove.WordRight, extend: false);
        Assert.Equal(3, editor.Caret);
    }

    [Fact]
    public void WithoutGeometryLinesAreParagraphs()
    {
        var editor = Editor("one\ntwo\nthree", 5);
        editor.Move(SkUiTextMove.LineStart, extend: false);
        Assert.Equal(4, editor.Caret);
        editor.Move(SkUiTextMove.LineEnd, extend: true);
        Assert.Equal("two", editor.SelectedText);
        editor.Move(SkUiTextMove.DocumentEnd, extend: false);
        Assert.Equal(13, editor.Caret);
    }

    // ---- Layout ----

    private static SkUiTextStyle Style(double size = 10) =>
        new(SkUiTypefaces.Resolve(SkUiTestHelpers.BundledFontFamily), size);

    [Fact]
    public void SingleLineCaretsAndHitTestingFollowAdvances()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var layout = new SkUiTextEditLayout();
        layout.Update("abcd", Style(), double.PositiveInfinity);
        layout.Arrange(TextAlignment.Start, 100);
        Assert.Single(layout.Lines);
        var advance = layout.Width / 4;
        Assert.True(advance > 0);
        Assert.Equal(0, layout.CaretX(0, 0), 3);
        Assert.Equal(2 * advance, layout.CaretX(0, 2), 3);
        Assert.Equal(4 * advance, layout.CaretX(0, 4), 3);
        Assert.Equal(1, layout.HitTest(new SKPoint(advance * 1.4f, 5)).Index);
        Assert.Equal(2, layout.HitTest(new SKPoint(advance * 1.6f, 5)).Index);
        Assert.Equal(0, layout.HitTest(new SKPoint(-20, 5)).Index);
        Assert.Equal(4, layout.HitTest(new SKPoint(500, 500)).Index);
    }

    [Fact]
    public void AlignmentOffsetsTheLines()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var layout = new SkUiTextEditLayout();
        layout.Update("ab", Style(), double.PositiveInfinity);
        layout.Arrange(TextAlignment.End, 100);
        Assert.Equal(100 - layout.Width, layout.CaretX(0, 0), 3);
        layout.Arrange(TextAlignment.Center, 100);
        Assert.Equal(50, layout.CaretX(0, 1), 3);
    }

    [Fact]
    public void ShapedTextHasTheSameCaretsAsSimpleText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var simple = new SkUiTextEditLayout();
        simple.Update("hello", Style(), double.PositiveInfinity);
        var shaped = new SkUiTextEditLayout();
        shaped.Update("hello", Style() with { Rendering = SkUiTextRendering.Shaped }, double.PositiveInfinity);
        for (var index = 0; index <= 5; index++)
            Assert.Equal(simple.CaretX(0, index), shaped.CaretX(0, index), 1);
    }

    [Fact]
    public void MultilineWrapsKeepsTrailingSpacesOnTheLineAndBreaksParagraphs()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var layout = new SkUiTextEditLayout();
        var style = Style();
        layout.Update("aaaa bbbb", style, double.PositiveInfinity);
        var advance = layout.Width / 9;
        layout.Update("aaaa bbbb\ncc", style, advance * 6);
        layout.Arrange(TextAlignment.Start, advance * 6);
        Assert.Equal(3, layout.Lines.Count);
        Assert.Equal((0, 5, false), (layout.Lines[0].Start, layout.Lines[0].End, layout.Lines[0].EndsParagraph));
        Assert.Equal((5, 9, true), (layout.Lines[1].Start, layout.Lines[1].End, layout.Lines[1].EndsParagraph));
        Assert.Equal((10, 12), (layout.Lines[2].Start, layout.Lines[2].End));
        // The caret at the wrap shows at the start of the second line, or (upstream) after the space.
        Assert.Equal(1, layout.LineIndexOf(5));
        Assert.Equal(0, layout.LineIndexOf(5, upstream: true));
        Assert.Equal(5 * advance, layout.CaretRect(5, upstream: true).Left, 2);
        // A click past the first line's end stays on it.
        Assert.Equal((5, true), layout.HitTest(new SKPoint(advance * 5.9f, layout.Lines[0].Top + 1)));
        Assert.Equal(layout.Lines[1].Top, layout.Lines[0].Bottom, 3);
    }

    [Fact]
    public void AnEditReshapesOnlyItsParagraph()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var layout = new SkUiTextEditLayout();
        var style = Style();
        layout.Update("one\ntwo\nthree", style, 200);
        Assert.Equal(3, layout.LastShapedParagraphs);
        layout.Update("one\ntwos\nthree", style, 200);
        Assert.Equal(1, layout.LastShapedParagraphs);
        Assert.Equal((4, 8), (layout.Lines[1].Start, layout.Lines[1].End));
        Assert.Equal(9, layout.Lines[2].Start);
        layout.Update("one\ntwos\n\nthree", style, 200);
        Assert.Equal(1, layout.LastShapedParagraphs);
        Assert.Equal(4, layout.Lines.Count);
        Assert.Equal(10, layout.Lines[3].Start);
    }

    [Fact]
    public void SelectionBoxesCoverTheRangeAndTheSelectedLineBreak()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var layout = new SkUiTextEditLayout();
        layout.Update("abc\ndef", Style(), double.PositiveInfinity);
        layout.Arrange(TextAlignment.Start, 200);
        var rects = new List<SKRect>();
        layout.AddRangeRects(1, 5, rects);
        var advance = layout.Lines[0].Width / 3;
        Assert.Equal(3, rects.Count);
        Assert.Equal(advance, rects[0].Left, 2);
        Assert.Equal(3 * advance, rects[0].Right, 2);
        Assert.True(rects[1].Left >= 3 * advance - 0.01f); // the newline box
        Assert.Equal(layout.Lines[1].Top, rects[2].Top, 2);
        Assert.Equal(advance, rects[2].Right, 2);
    }

    [Fact]
    public void EmptyTextHasOneLineWithACaret()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var layout = new SkUiTextEditLayout();
        layout.Update("", Style(), double.PositiveInfinity);
        layout.Arrange(TextAlignment.Start, 100);
        Assert.Single(layout.Lines);
        var caret = layout.CaretRect(0);
        Assert.True(caret.Height > 5);
        Assert.Equal(0, caret.Left);
    }

    [Fact]
    public void RightToLeftCaretsStartAtTheRight()
    {
        const string hebrew = "שלום";
        if (SKFontManager.Default.MatchCharacter('ש') is null)
            return;
        using var _ = SkUiTestHelpers.UseBundledFont();
        var layout = new SkUiTextEditLayout();
        layout.Update(hebrew, Style(16), double.PositiveInfinity);
        layout.Arrange(TextAlignment.Start, 200);
        var line = layout.Lines[0];
        Assert.True(line.IsRightToLeft);
        // The caret before the first letter is at the line's right end, after the last at its left end.
        Assert.Equal(200, layout.CaretX(0, 0), 1);
        Assert.Equal(200 - line.Width, layout.CaretX(0, hebrew.Length), 1);
        Assert.True(layout.CaretX(0, 1) < layout.CaretX(0, 0));
        // A click near the right end is the text's start.
        Assert.Equal(0, layout.HitTest(new SKPoint(199, 5)).Index);
    }

    [Fact]
    public void MixedDirectionSelectionSplitsIntoVisualStretches()
    {
        const string text = "ab שלום cd";
        if (SKFontManager.Default.MatchCharacter('ש') is null)
            return;
        using var _ = SkUiTestHelpers.UseBundledFont();
        var layout = new SkUiTextEditLayout();
        layout.Update(text, Style(16), double.PositiveInfinity);
        layout.Arrange(TextAlignment.Start, 400);
        var rects = new List<SKRect>();
        // "b" and the first two Hebrew letters: the Hebrew run is reversed, so they are not side by side.
        layout.AddRangeRects(1, 5, rects);
        Assert.Equal(2, rects.Count);
    }

    [Fact]
    public void ReleaseDisposesBlobs()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var layout = new SkUiTextEditLayout();
        layout.Update("abc", Style() with { Rendering = SkUiTextRendering.Shaped }, double.PositiveInfinity);
        var blob = layout.Lines[0].Blob;
        Assert.NotNull(blob);
        layout.Release();
        Assert.Empty(layout.Lines);
        Assert.Equal(IntPtr.Zero, blob!.Handle);
    }
}
