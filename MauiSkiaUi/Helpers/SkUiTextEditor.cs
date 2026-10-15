namespace MauiSkiaUi;

/// <summary>Caret movements of <see cref="SkUiTextEditor.Move"/> (arrow keys, Home / End, Page Up / Down and their modifiers).</summary>
internal enum SkUiTextMove
{
    /// <summary>One grapheme toward the left (backward in left-to-right text).</summary>
    Left,
    /// <summary>One grapheme toward the right.</summary>
    Right,
    /// <summary>To the start of the word on the left (Ctrl / Alt + Left).</summary>
    WordLeft,
    /// <summary>To the next word on the right: its start (Windows, Android) or the current word's end (Apple).</summary>
    WordRight,
    /// <summary>To the start of the visual line (Home, Cmd + Left).</summary>
    LineStart,
    /// <summary>To the end of the visual line (End, Cmd + Right).</summary>
    LineEnd,
    /// <summary>One line up, keeping the x the caret had.</summary>
    Up,
    /// <summary>One line down, keeping the x the caret had.</summary>
    Down,
    /// <summary>A page of lines up.</summary>
    PageUp,
    /// <summary>A page of lines down.</summary>
    PageDown,
    /// <summary>To the start of the text (Ctrl + Home, Cmd + Up).</summary>
    DocumentStart,
    /// <summary>To the end of the text (Ctrl + End, Cmd + Down).</summary>
    DocumentEnd
}

/// <summary>What an edit was, for undo coalescing: typing joins typing, deleting joins deleting.</summary>
internal enum SkUiTextEditKind
{
    /// <summary>A replacement that is its own undo step (paste, cut, a programmatic insert, autocorrect).</summary>
    Other,
    /// <summary>Typed characters: consecutive ones are one undo step until a new word starts.</summary>
    Typing,
    /// <summary>Backspace / Delete: consecutive ones are one undo step.</summary>
    Deleting,
    /// <summary>An input method's composition: the whole composition is one undo step.</summary>
    Composing
}

/// <summary>
/// Where lines are, for caret movement across them (the field's <see cref="SkUiTextEditLayout"/>). Without one, the editor
/// moves by paragraphs.
/// </summary>
internal interface ISkUiTextEditGeometry
{
    /// <summary>Whether the line holding the caret at <paramref name="index"/> runs right to left (Left / Right swap).</summary>
    bool IsRightToLeftAt(int index, bool upstream);

    /// <summary>The start of the visual line holding the caret at <paramref name="index"/>.</summary>
    int LineStart(int index, bool upstream);

    /// <summary>The end of that line (a wrapped line's end is reached upstream).</summary>
    (int Index, bool Upstream) LineEnd(int index, bool upstream);

    /// <summary>
    /// The caret <paramref name="lines"/> lines below (negative: above) the one at <paramref name="index"/>, at x
    /// <paramref name="goalX"/> (set from the caret when <c>null</c>); past the first or last line: the text's start or end.
    /// </summary>
    (int Index, bool Upstream) MoveVertically(int index, bool upstream, int lines, ref float? goalX);

    /// <summary>Lines in a page (Page Up / Down).</summary>
    int PageLines { get; }
}

/// <summary>The editor's text, selection or composition changed (the field redraws, raises events and tells the input method).</summary>
internal interface ISkUiTextEditorOwner
{
    /// <summary>The text changed from <paramref name="oldText"/>; <paramref name="byUser"/>: by input (typing, the input method, paste, undo), not the app.</summary>
    void OnEditorTextChanged(string oldText, bool byUser);

    /// <summary>The selection, caret or composition moved.</summary>
    void OnEditorSelectionChanged();
}

/// <summary>
/// The editing model of a drawn text field (both layers, no drawing, no platform): the text, the selection (an anchor and an
/// active end, the caret), the input method's composition (marked text) and undo / redo. Edits honour <see cref="MaxLength"/>,
/// <see cref="IsReadOnly"/>, single-line fields (line breaks become spaces) and <see cref="Transform"/>. Caret movement steps
/// by graphemes and words (<see cref="SkUiTextBoundaries"/>) and by lines through an <see cref="ISkUiTextEditGeometry"/>.
/// UI thread.
/// </summary>
internal sealed class SkUiTextEditor(ISkUiTextEditorOwner? owner = null)
{
    // Undo steps kept; older ones are dropped.
    private const int UndoLimit = 100;
    private readonly List<Edit> _undo = [];
    private readonly List<Edit> _redo = [];
    // Whether the next edit may join the last undo step (cleared by caret moves and other edits).
    private bool _coalesce;
    private int _batch;
    private bool _batchTextChanged;
    private bool _batchSelectionChanged;
    private string _batchOldText = string.Empty;
    private bool _batchByUser;

    private sealed class Edit
    {
        public int Start;
        public string Removed = string.Empty;
        public string Inserted = string.Empty;
        public int AnchorBefore, CaretBefore, AnchorAfter, CaretAfter;
        public SkUiTextEditKind Kind;
    }

    /// <summary>The text.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>The fixed end of the selection.</summary>
    public int Anchor { get; private set; }

    /// <summary>The moving end of the selection: where the caret is.</summary>
    public int Caret { get; private set; }

    /// <summary>At a wrap, the caret shows at the end of the earlier line (after End, or a click past a wrapped line's end).</summary>
    public bool CaretUpstream { get; private set; }

    public int SelectionStart => Math.Min(Anchor, Caret);
    public int SelectionEnd => Math.Max(Anchor, Caret);
    public int SelectionLength => SelectionEnd - SelectionStart;
    public bool HasSelection => Anchor != Caret;
    public string SelectedText => HasSelection ? Text.Substring(SelectionStart, SelectionLength) : string.Empty;

    /// <summary>Start of the input method's composition (marked text), or -1.</summary>
    public int CompositionStart { get; private set; } = -1;

    /// <summary>End of the composition, or -1.</summary>
    public int CompositionEnd { get; private set; } = -1;

    public bool HasComposition => CompositionStart >= 0 && CompositionEnd > CompositionStart;

    /// <summary>The most characters (UTF-16 code units) input may make the text; <see cref="int.MaxValue"/>: no limit.</summary>
    public int MaxLength { get; set; } = int.MaxValue;

    /// <summary>Input does not change the text (selection, copying and the app's changes still work).</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>Line breaks are kept (an editor); otherwise input turns them into spaces (an entry).</summary>
    public bool IsMultiline { get; set; }

    /// <summary>MAUI's <c>TextTransform</c>, applied to input.</summary>
    public TextTransform Transform { get; set; }

    /// <summary>Ctrl / Alt + Right goes to the current word's end (Apple) rather than the next word's start.</summary>
    public bool WordRightGoesToWordEnd { get; set; } = OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst();

    /// <summary>The x line movement keeps (set by the first Up / Down, cleared by other moves).</summary>
    public float? GoalX { get; set; }

    public bool CanUndo => _undo.Count > 0 && !IsReadOnly;
    public bool CanRedo => _redo.Count > 0 && !IsReadOnly;

    /// <summary>
    /// Groups changes so the owner hears of them once, at the end (an input method's batch edit). Nested batches end with
    /// the outermost.
    /// </summary>
    public void BeginBatch()
    {
        if (_batch++ == 0)
        {
            _batchTextChanged = _batchSelectionChanged = _batchByUser = false;
            _batchOldText = Text;
        }
    }

    public void EndBatch()
    {
        if (_batch == 0 || --_batch > 0)
            return;
        if (_batchTextChanged && !ReferenceEquals(_batchOldText, Text) && _batchOldText != Text)
            owner?.OnEditorTextChanged(_batchOldText, _batchByUser);
        if (_batchSelectionChanged || _batchTextChanged)
            owner?.OnEditorSelectionChanged();
        _batchOldText = string.Empty;
    }

    /// <summary>
    /// The app set the text: it replaces the text (no undo step; the undo history is cleared), the composition ends and the
    /// caret goes to the end (or stays where it was when <paramref name="keepCaret"/>, clamped).
    /// </summary>
    public void SetText(string? text, bool keepCaret = false)
    {
        text ??= string.Empty;
        if (text == Text)
            return;
        var old = Text;
        Text = text;
        _undo.Clear();
        _redo.Clear();
        _coalesce = false;
        CompositionStart = CompositionEnd = -1;
        if (keepCaret)
            SetSelectionCore(SkUiTextBoundaries.AlignToGrapheme(text, Anchor), SkUiTextBoundaries.AlignToGrapheme(text, Caret), false);
        else
            SetSelectionCore(text.Length, text.Length, false);
        RaiseText(old, byUser: false);
        RaiseSelection();
    }

    /// <summary>Selects <c>[anchor, caret]</c> (clamped); equal: places the caret. Ends coalescing of undo steps.</summary>
    public void Select(int anchor, int caret, bool upstream = false)
    {
        anchor = Math.Clamp(anchor, 0, Text.Length);
        caret = Math.Clamp(caret, 0, Text.Length);
        _coalesce = false;
        GoalX = null;
        if (anchor == Anchor && caret == Caret && upstream == CaretUpstream)
            return;
        SetSelectionCore(anchor, caret, upstream);
        RaiseSelection();
    }

    public void SelectAll() => Select(0, Text.Length);

    /// <summary>Selects the word at <paramref name="index"/> (a double click / tap).</summary>
    public void SelectWordAt(int index)
    {
        var (start, end) = SkUiTextBoundaries.WordAt(Text, index);
        Select(start, end);
    }

    /// <summary>Selects the paragraph at <paramref name="index"/> (a triple click).</summary>
    public void SelectParagraphAt(int index)
    {
        var (start, end) = SkUiTextBoundaries.ParagraphAt(Text, index);
        Select(start, end);
    }

    private void SetSelectionCore(int anchor, int caret, bool upstream)
    {
        Anchor = anchor;
        Caret = caret;
        CaretUpstream = upstream && anchor == caret;
    }

    /// <summary>Moves the caret (<paramref name="extend"/>: the selection's active end) by <paramref name="move"/>.</summary>
    public void Move(SkUiTextMove move, bool extend, ISkUiTextEditGeometry? geometry = null)
    {
        FinishComposing();
        var text = Text;
        var caret = Caret;
        var upstream = CaretUpstream;
        var keepGoal = false;
        var rtl = geometry?.IsRightToLeftAt(caret, upstream) ?? false;
        var forward = move is SkUiTextMove.Right or SkUiTextMove.WordRight ? !rtl : rtl;
        switch (move)
        {
            case SkUiTextMove.Left or SkUiTextMove.Right when HasSelection && !extend:
                // Left / Right without Shift collapse a selection to its side.
                caret = forward ? SelectionEnd : SelectionStart;
                upstream = false;
                break;
            case SkUiTextMove.Left or SkUiTextMove.Right:
                caret = forward ? SkUiTextBoundaries.NextGrapheme(text, caret) : SkUiTextBoundaries.PreviousGrapheme(text, caret);
                upstream = false;
                break;
            case SkUiTextMove.WordLeft or SkUiTextMove.WordRight:
                caret = !forward ? SkUiTextBoundaries.PreviousWordStart(text, caret)
                    : WordRightGoesToWordEnd ? SkUiTextBoundaries.NextWordEnd(text, caret) : SkUiTextBoundaries.NextWordStart(text, caret);
                upstream = false;
                break;
            case SkUiTextMove.LineStart:
                caret = geometry?.LineStart(caret, upstream) ?? SkUiTextBoundaries.ParagraphAt(text, caret).Start;
                upstream = false;
                break;
            case SkUiTextMove.LineEnd:
                (caret, upstream) = geometry?.LineEnd(caret, upstream) ?? (SkUiTextBoundaries.ParagraphAt(text, caret).End, false);
                break;
            case SkUiTextMove.Up or SkUiTextMove.Down or SkUiTextMove.PageUp or SkUiTextMove.PageDown:
            {
                var page = geometry?.PageLines ?? 1;
                var lines = move switch { SkUiTextMove.Up => -1, SkUiTextMove.Down => 1, SkUiTextMove.PageUp => -page, _ => page };
                if (HasSelection && !extend && move is SkUiTextMove.Up or SkUiTextMove.Down)
                    caret = lines < 0 ? SelectionStart : SelectionEnd;
                if (geometry is not null)
                {
                    var goal = GoalX;
                    (caret, upstream) = geometry.MoveVertically(caret, upstream, lines, ref goal);
                    GoalX = goal;
                    keepGoal = true;
                }
                else
                {
                    caret = lines < 0 ? 0 : text.Length;
                    upstream = false;
                }
                break;
            }
            case SkUiTextMove.DocumentStart:
                caret = 0;
                upstream = false;
                break;
            case SkUiTextMove.DocumentEnd:
                caret = text.Length;
                upstream = false;
                break;
        }
        var anchor = extend ? Anchor : caret;
        var goalX = GoalX;
        Select(anchor, caret, upstream);
        if (keepGoal)
            GoalX = goalX;
    }

    /// <summary>
    /// Replaces <c>[start, end)</c> with <paramref name="text"/> as input: refused when read-only; line breaks become spaces
    /// in a single-line field; <see cref="Transform"/> applies; what would pass <see cref="MaxLength"/> is cut (a refused or
    /// emptied insertion changes nothing). The caret goes after the insertion (or to <paramref name="caret"/>). Returns whether
    /// the text changed.
    /// </summary>
    public bool Replace(int start, int end, string text, SkUiTextEditKind kind = SkUiTextEditKind.Other, int? caret = null)
    {
        if (IsReadOnly)
            return false;
        start = Math.Clamp(start, 0, Text.Length);
        end = Math.Clamp(end, start, Text.Length);
        text = Clean(text);
        var room = MaxLength == int.MaxValue ? int.MaxValue : MaxLength - (Text.Length - (end - start));
        if (text.Length > room)
        {
            var length = Math.Max(0, room);
            // Never splits a surrogate pair.
            if (length > 0 && length < text.Length && char.IsLowSurrogate(text[length]))
                length--;
            text = text[..length];
            if (text.Length == 0 && end == start)
                return false;
        }
        var removed = Text.Substring(start, end - start);
        if (removed == text)
        {
            Select(caret ?? start + text.Length, caret ?? start + text.Length);
            return false;
        }
        var old = Text;
        var anchorBefore = Anchor;
        var caretBefore = Caret;
        Text = string.Concat(Text.AsSpan(0, start), text, Text.AsSpan(end));
        var after = Math.Clamp(caret ?? start + text.Length, 0, Text.Length);
        SetSelectionCore(after, after, false);
        GoalX = null;
        ShiftComposition(start, end, text.Length);
        Record(start, removed, text, anchorBefore, caretBefore, kind);
        RaiseText(old, byUser: true);
        RaiseSelection();
        return true;
    }

    /// <summary>Inserts <paramref name="text"/> over the composition, else over the selection (typing, an input method's commit, paste).</summary>
    public bool InsertText(string text, SkUiTextEditKind kind = SkUiTextEditKind.Typing)
    {
        var composing = HasComposition;
        var (start, end) = composing ? (CompositionStart, CompositionEnd) : (SelectionStart, SelectionEnd);
        // Committing a composition joins its undo step; typing over a selection is a step of its own.
        if (composing)
            kind = SkUiTextEditKind.Composing;
        else if (kind == SkUiTextEditKind.Typing && end > start)
            kind = SkUiTextEditKind.Other;
        BeginBatch();
        try
        {
            ClearComposition();
            return Replace(start, end, text, kind);
        }
        finally
        {
            EndBatch();
        }
    }

    /// <summary>
    /// The input method's marked text: replaces the composition (else the selection) with <paramref name="text"/> and
    /// marks it; <paramref name="caretInText"/>: where the caret goes inside it (its end by default). Empty text ends the
    /// composition. The whole composition is one undo step.
    /// </summary>
    public void SetComposingText(string text, int? caretInText = null)
    {
        if (IsReadOnly)
            return;
        var (start, end) = HasComposition ? (CompositionStart, CompositionEnd) : (SelectionStart, SelectionEnd);
        BeginBatch();
        try
        {
            var cleaned = Clean(text);
            ClearComposition();
            Replace(start, end, cleaned, SkUiTextEditKind.Composing);
            var inserted = Math.Min(cleaned.Length, Text.Length - start);
            if (inserted > 0)
            {
                CompositionStart = start;
                CompositionEnd = start + inserted;
            }
            var caret = start + Math.Clamp(caretInText ?? inserted, 0, inserted);
            SetSelectionCore(caret, caret, false);
            _batchSelectionChanged = true;
        }
        finally
        {
            EndBatch();
        }
    }

    /// <summary>Marks <c>[start, end)</c> as the composition (an input method reconverting committed text); an empty range ends it.</summary>
    public void SetComposingRegion(int start, int end)
    {
        start = Math.Clamp(start, 0, Text.Length);
        end = Math.Clamp(end, 0, Text.Length);
        if (start > end)
            (start, end) = (end, start);
        if (start == end)
        {
            FinishComposing();
            return;
        }
        if (start == CompositionStart && end == CompositionEnd)
            return;
        CompositionStart = start;
        CompositionEnd = end;
        RaiseSelection();
    }

    /// <summary>Commits the composition: its text stays, unmarked.</summary>
    public void FinishComposing()
    {
        if (CompositionStart < 0)
            return;
        ClearComposition();
        _coalesce = false;
        RaiseSelection();
    }

    /// <summary>Removes the composition's text (the input method cancelled it).</summary>
    public void CancelComposition()
    {
        if (!HasComposition)
        {
            FinishComposing();
            return;
        }
        var (start, end) = (CompositionStart, CompositionEnd);
        BeginBatch();
        try
        {
            ClearComposition();
            Replace(start, end, string.Empty, SkUiTextEditKind.Composing);
        }
        finally
        {
            EndBatch();
        }
    }

    private void ClearComposition() => CompositionStart = CompositionEnd = -1;

    /// <summary>Backspace: deletes the selection, else the grapheme (or word) before the caret.</summary>
    public bool DeleteBackward(bool word = false, bool toLineStart = false, ISkUiTextEditGeometry? geometry = null)
    {
        FinishComposing();
        if (HasSelection)
            return Replace(SelectionStart, SelectionEnd, string.Empty, SkUiTextEditKind.Other);
        var start = toLineStart ? geometry?.LineStart(Caret, CaretUpstream) ?? SkUiTextBoundaries.ParagraphAt(Text, Caret).Start
            : word ? SkUiTextBoundaries.PreviousWordStart(Text, Caret)
            : PreviousForBackspace(Text, Caret);
        if (toLineStart && start == Caret)
            start = SkUiTextBoundaries.PreviousGrapheme(Text, Caret);
        return start < Caret && Replace(start, Caret, string.Empty, SkUiTextEditKind.Deleting);
    }

    /// <summary>Delete: deletes the selection, else the grapheme (or word) after the caret.</summary>
    public bool DeleteForward(bool word = false)
    {
        FinishComposing();
        if (HasSelection)
            return Replace(SelectionStart, SelectionEnd, string.Empty, SkUiTextEditKind.Other);
        var end = word ? SkUiTextBoundaries.NextWordStart(Text, Caret) : SkUiTextBoundaries.NextGrapheme(Text, Caret);
        return end > Caret && Replace(Caret, end, string.Empty, SkUiTextEditKind.Deleting);
    }

    /// <summary>
    /// Backspace deletes one grapheme, except that it peels the last mark off a base letter and the last character off a
    /// conjunct in scripts that compose them (as native text fields: Devanagari, Thai, Hangul jamo), and whole emoji.
    /// </summary>
    private static int PreviousForBackspace(string text, int caret)
    {
        var previous = SkUiTextBoundaries.PreviousGrapheme(text, caret);
        if (caret - previous <= 1)
            return previous;
        var last = char.IsLowSurrogate(text[caret - 1]) && caret - 2 >= previous ? caret - 2 : caret - 1;
        var cp = text.ConvertToUtf32OrReplacement(last);
        // Emoji and other symbols (with modifiers, joiners, variation selectors) go whole; letters lose their last mark.
        var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(text.ConvertToUtf32OrReplacement(previous));
        return category is System.Globalization.UnicodeCategory.OtherSymbol or System.Globalization.UnicodeCategory.Surrogate
            || cp is 0xFE0F or 0x200D or >= 0x1F3FB and <= 0x1F3FF or >= 0xE0020 and <= 0xE007F
            ? previous
            : last;
    }

    /// <summary>Undoes the last edit step; returns whether there was one.</summary>
    public bool Undo()
    {
        if (!CanUndo)
            return false;
        FinishComposing();
        var edit = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(edit);
        Apply(edit.Start, edit.Inserted.Length, edit.Removed, edit.AnchorBefore, edit.CaretBefore);
        return true;
    }

    /// <summary>Redoes the last undone edit step; returns whether there was one.</summary>
    public bool Redo()
    {
        if (!CanRedo)
            return false;
        FinishComposing();
        var edit = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(edit);
        Apply(edit.Start, edit.Removed.Length, edit.Inserted, edit.AnchorAfter, edit.CaretAfter);
        return true;
    }

    private void Apply(int start, int length, string text, int anchor, int caret)
    {
        var old = Text;
        Text = string.Concat(Text.AsSpan(0, start), text, Text.AsSpan(start + length));
        _coalesce = false;
        GoalX = null;
        SetSelectionCore(Math.Clamp(anchor, 0, Text.Length), Math.Clamp(caret, 0, Text.Length), false);
        RaiseText(old, byUser: true);
        RaiseSelection();
    }

    /// <summary>Ends the current undo step: the next edit starts a new one (focus left, the caret was moved).</summary>
    public void BreakUndoCoalescing() => _coalesce = false;

    private void Record(int start, string removed, string inserted, int anchorBefore, int caretBefore, SkUiTextEditKind kind)
    {
        _redo.Clear();
        if (_coalesce && _undo.Count > 0 && TryCoalesce(_undo[^1], start, removed, inserted, kind))
        {
            _undo[^1].AnchorAfter = Anchor;
            _undo[^1].CaretAfter = Caret;
            return;
        }
        _undo.Add(new Edit
        {
            Start = start,
            Removed = removed,
            Inserted = inserted,
            AnchorBefore = anchorBefore,
            CaretBefore = caretBefore,
            AnchorAfter = Anchor,
            CaretAfter = Caret,
            Kind = kind
        });
        if (_undo.Count > UndoLimit)
            _undo.RemoveAt(0);
        _coalesce = kind != SkUiTextEditKind.Other;
    }

    /// <summary>Joins an edit to the last step: typing after typing (until a new word starts), deleting next to deleting, a composition's updates.</summary>
    private static bool TryCoalesce(Edit last, int start, string removed, string inserted, SkUiTextEditKind kind)
    {
        var insertedEnd = last.Start + last.Inserted.Length;
        switch (kind)
        {
            case SkUiTextEditKind.Composing when last.Kind is SkUiTextEditKind.Composing or SkUiTextEditKind.Typing
                                                 && start >= last.Start && start + removed.Length <= insertedEnd:
            case SkUiTextEditKind.Typing when last.Kind is SkUiTextEditKind.Typing or SkUiTextEditKind.Composing
                                              && removed.Length == 0 && start == insertedEnd
                                              && !StartsWord(last.Inserted, inserted):
                // The new text lands inside (or right after) what the step inserted.
                var offset = start - last.Start;
                last.Inserted = string.Concat(last.Inserted.AsSpan(0, offset), inserted, last.Inserted.AsSpan(offset + removed.Length));
                if (kind == SkUiTextEditKind.Composing)
                    last.Kind = SkUiTextEditKind.Composing;
                return true;
            case SkUiTextEditKind.Deleting when last.Kind == SkUiTextEditKind.Deleting && inserted.Length == 0 && last.Inserted.Length == 0:
                if (start + removed.Length == last.Start)
                {
                    // Backspace: the removed text grows to the left.
                    last.Start = start;
                    last.Removed = removed + last.Removed;
                    return true;
                }
                if (start == last.Start)
                {
                    // Delete: it grows to the right.
                    last.Removed += removed;
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    // A non-space typed after a space starts a new word: typing becomes a new undo step (word-by-word undo).
    private static bool StartsWord(string before, string typed) =>
        before.Length > 0 && char.IsWhiteSpace(before[^1]) && typed.Length > 0 && !char.IsWhiteSpace(typed[0]);

    /// <summary>Keeps the composition on its text when an edit elsewhere moves it; an edit across it ends it.</summary>
    private void ShiftComposition(int start, int end, int inserted)
    {
        if (CompositionStart < 0)
            return;
        if (end <= CompositionStart)
        {
            var delta = inserted - (end - start);
            CompositionStart += delta;
            CompositionEnd += delta;
        }
        else if (start < CompositionEnd)
        {
            ClearComposition();
        }
    }

    /// <summary>Input text as the field takes it: line breaks normalized (or spaces in a single line), transformed.</summary>
    private string Clean(string text)
    {
        if (text.Length == 0)
            return text;
        if (text.AsSpan().IndexOfAny('\r', '\n') >= 0)
            text = IsMultiline ? text.Replace("\r\n", "\n").Replace('\r', '\n') : text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        return SkUiTextTransform.Apply(text, Transform);
    }

    private void RaiseText(string old, bool byUser)
    {
        if (_batch > 0)
        {
            _batchTextChanged = true;
            _batchByUser |= byUser;
            return;
        }
        owner?.OnEditorTextChanged(old, byUser);
    }

    private void RaiseSelection()
    {
        if (_batch > 0)
        {
            _batchSelectionChanged = true;
            return;
        }
        owner?.OnEditorSelectionChanged();
    }
}
