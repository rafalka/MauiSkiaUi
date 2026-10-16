using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MauiSkiaUi;

/// <summary>
/// A text field's text and selection as <see cref="SkUiTextInputFormatter"/>s see them: <c>[SelectionStart, SelectionEnd)</c> in
/// UTF-16 code units; equal ends are the caret.
/// </summary>
/// <remarks>Not positional (no <c>Deconstruct</c>), so later members (a composition's range) come as properties.</remarks>
public readonly record struct SkUiTextEditValue
{
    /// <summary>Text with <c>[<paramref name="selectionStart"/>, <paramref name="selectionEnd"/>)</c> selected.</summary>
    public SkUiTextEditValue(string text, int selectionStart, int selectionEnd)
    {
        Text = text;
        SelectionStart = selectionStart;
        SelectionEnd = selectionEnd;
    }

    /// <summary>Text with the caret at <paramref name="caret"/>.</summary>
    public SkUiTextEditValue(string text, int caret) : this(text, caret, caret)
    {
    }

    /// <summary>The field's text.</summary>
    public string Text { get; init; }

    /// <summary>Where the selection starts (the caret when nothing is selected), in UTF-16 code units.</summary>
    public int SelectionStart { get; init; }

    /// <summary>Where the selection ends (exclusive; <see cref="SelectionStart"/> when nothing is selected).</summary>
    public int SelectionEnd { get; init; }

    /// <summary>How many characters are selected.</summary>
    public int SelectionLength => SelectionEnd - SelectionStart;

    /// <summary>The same value with its text non-null and its selection ordered and inside the text.</summary>
    internal SkUiTextEditValue Clamped()
    {
        var text = Text ?? string.Empty;
        var start = Math.Clamp(SelectionStart, 0, text.Length);
        var end = Math.Clamp(SelectionEnd, 0, text.Length);
        return new SkUiTextEditValue(text, Math.Min(start, end), Math.Max(start, end));
    }
}

/// <summary>
/// Rewrites or refuses the user's edits of a drawn text field (Flutter's <c>TextInputFormatter</c>): typing, an input method's
/// committed text, paste, cut, Backspace / Delete, a screen reader's set-text. The field's <c>InputFormatters</c> run in order,
/// each on the previous one's result; what the last returns is the edit (one undo step; returning
/// the old value from <see cref="Format"/> refuses it). An input method's composition (marked text) is formatted when it
/// is committed, not while it is composed. Undo / redo and text the app sets are not formatted (unless the field's
/// <c>FormatsAssignedText</c>).
/// </summary>
/// <remarks>
/// A formatter may be shared by fields: it should keep no state between calls. The built-in ones are
/// <see cref="SkUiFilteringFormatter"/> (allow or deny characters by a regular expression or a predicate),
/// <see cref="SkUiLengthLimitingFormatter"/> (cap graphemes, digits or letters) and <see cref="SkUiMaskFormatter"/>
/// (mask-as-you-type). <see cref="Create"/> wraps a function.
/// </remarks>
public abstract class SkUiTextInputFormatter
{
    /// <summary>
    /// The field's value after the edit: <paramref name="newValue"/> as the user made it (or as an earlier formatter returned it),
    /// with <paramref name="oldValue"/> the value before the edit. Returns the value the field takes.
    /// </summary>
    public abstract SkUiTextEditValue Format(SkUiTextEditValue oldValue, SkUiTextEditValue newValue);

    /// <summary>A formatter that calls <paramref name="format"/> (old value, new value → the value the field takes).</summary>
    public static SkUiTextInputFormatter Create(Func<SkUiTextEditValue, SkUiTextEditValue, SkUiTextEditValue> format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return new FunctionFormatter(format);
    }

    private sealed class FunctionFormatter(Func<SkUiTextEditValue, SkUiTextEditValue, SkUiTextEditValue> format) : SkUiTextInputFormatter
    {
        public override SkUiTextEditValue Format(SkUiTextEditValue oldValue, SkUiTextEditValue newValue) => format(oldValue, newValue);
    }

    /// <summary>
    /// Where an index of a text goes when ranges of it are replaced: inside or at the end of a replaced range, after its
    /// replacement; elsewhere, shifted by what the ranges before it changed.
    /// </summary>
    private protected static int MapIndex(int index, ReadOnlySpan<(int Start, int End, int Length)> replaced)
    {
        var shift = 0;
        foreach (var (start, end, length) in replaced)
        {
            if (index <= start)
                break;
            if (index <= end)
                return start + shift + length;
            shift += length - (end - start);
        }
        return index + shift;
    }
}

/// <summary>Whether <see cref="SkUiFilteringFormatter"/> keeps what matches or removes it.</summary>
public enum SkUiFilteringMode
{
    /// <summary>Only what matches stays (Flutter's <c>FilteringTextInputFormatter.allow</c>).</summary>
    Allow,

    /// <summary>What matches is removed (or replaced by <see cref="SkUiFilteringFormatter.Replacement"/>).</summary>
    Deny
}

/// <summary>
/// Keeps (<see cref="SkUiFilteringMode.Allow"/>) or removes (<see cref="SkUiFilteringMode.Deny"/>) the text that matches
/// <see cref="Pattern"/> (a .NET regular expression), or the characters a predicate accepts: typed and pasted text loses what
/// does not belong, and the caret stays after what was typed. A removed stretch is replaced by <see cref="Replacement"/>
/// (default: nothing). An edit that only adds refused text changes nothing. With <see cref="SkUiFilteringMode.Allow"/>, an edit
/// after which nothing matches is refused (a deletion stands), so a whole-text pattern (<c>^[0-9]*$</c>) refuses a wrong
/// character instead of clearing the field.
/// </summary>
/// <example><c>&lt;skui:SkUiFilteringFormatter Pattern="[0-9]" /&gt;</c> (digits only), <c>Pattern="\s" Mode="Deny"</c> (no spaces).</example>
public sealed class SkUiFilteringFormatter : SkUiTextInputFormatter
{
    private Regex? _regex;
    private readonly Func<Rune, bool>? _predicate;

    /// <summary>A filter set up by <see cref="Pattern"/> and <see cref="Mode"/> (XAML).</summary>
    public SkUiFilteringFormatter()
    {
    }

    /// <summary>A filter by a regular expression.</summary>
    public SkUiFilteringFormatter(string pattern, SkUiFilteringMode mode = SkUiFilteringMode.Allow, string replacement = "")
    {
        Pattern = pattern;
        Mode = mode;
        Replacement = replacement;
    }

    /// <summary>A filter by character: <paramref name="predicate"/> tells which characters match.</summary>
    public SkUiFilteringFormatter(Func<Rune, bool> predicate, SkUiFilteringMode mode = SkUiFilteringMode.Allow, string replacement = "")
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _predicate = predicate;
        Mode = mode;
        Replacement = replacement;
    }

    /// <summary>A new formatter that keeps the digits 0–9 only.</summary>
    public static SkUiFilteringFormatter DigitsOnly() => new(static rune => rune.Value is >= '0' and <= '9');

    /// <summary>The regular expression (culture-invariant); <c>null</c> with no predicate: everything matches.</summary>
    public string? Pattern
    {
        get;
        set
        {
            if (_predicate is not null)
                throw new InvalidOperationException("This filter matches by a predicate.");
            _regex = value is null ? null : new Regex(value, RegexOptions.CultureInvariant);
            field = value;
        }
    }

    /// <summary>Keep what matches (default) or remove it.</summary>
    public SkUiFilteringMode Mode
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            field = value;
        }
    }

    /// <summary>What each removed stretch becomes (default: nothing).</summary>
    public string Replacement { get; set => field = value ?? string.Empty; } = string.Empty;

    /// <inheritdoc />
    public override SkUiTextEditValue Format(SkUiTextEditValue oldValue, SkUiTextEditValue newValue)
    {
        var text = newValue.Text;
        if (text.Length == 0 || _predicate is null && _regex is null)
            return newValue;
        var removed = new List<(int Start, int End, int Length)>();
        var replacement = Replacement.Length;
        if (_predicate is not null)
        {
            var start = -1;
            for (var i = 0; i < text.Length;)
            {
                var rune = Rune.GetRuneAt(text, i);
                var matches = _predicate(rune);
                if (matches == (Mode == SkUiFilteringMode.Deny))
                {
                    if (start < 0)
                        start = i;
                }
                else if (start >= 0)
                {
                    removed.Add((start, i, replacement));
                    start = -1;
                }
                i += rune.Utf16SequenceLength;
            }
            if (start >= 0)
                removed.Add((start, text.Length, replacement));
        }
        else
        {
            var last = 0;
            foreach (var match in _regex!.EnumerateMatches(text))
            {
                if (match.Length == 0)
                    continue;
                if (Mode == SkUiFilteringMode.Deny)
                    removed.Add((match.Index, match.Index + match.Length, replacement));
                else if (match.Index > last)
                    removed.Add((last, match.Index, replacement));
                last = match.Index + match.Length;
            }
            if (Mode == SkUiFilteringMode.Allow && last < text.Length)
                removed.Add((last, text.Length, replacement));
        }
        if (removed.Count == 0)
            return newValue;
        // Nothing matches at all (a whole-text pattern such as ^[0-9]*$ after a wrong character): the edit is refused rather than
        // the field cleared; a deletion stands.
        if (Mode == SkUiFilteringMode.Allow && removed is [(0, var removedEnd, _)] && removedEnd == text.Length)
            return SkUiTextEditor.Diff(oldValue.Text, text).Inserted.Length == 0 ? newValue : oldValue;
        var builder = new StringBuilder(text.Length);
        var kept = 0;
        foreach (var (start, end, _) in removed)
        {
            builder.Append(text, kept, start - kept).Append(Replacement);
            kept = end;
        }
        builder.Append(text, kept, text.Length - kept);
        var ranges = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(removed);
        return new SkUiTextEditValue(builder.ToString(), MapIndex(newValue.SelectionStart, ranges), MapIndex(newValue.SelectionEnd, ranges));
    }
}

/// <summary>What <see cref="SkUiLengthLimitingFormatter"/> counts.</summary>
public enum SkUiTextLengthUnit
{
    /// <summary>User-perceived characters (an emoji with its modifiers, a letter with its marks: one each).</summary>
    Graphemes,

    /// <summary>Decimal digits only (a phone number's or a card number's digits, whatever separators it has).</summary>
    Digits,

    /// <summary>Letters and decimal digits only.</summary>
    LettersAndDigits
}

/// <summary>
/// Caps the text at <see cref="MaxLength"/> <see cref="Unit"/>s: what an edit adds past the cap is cut (whole graphemes; with
/// <see cref="SkUiTextLengthUnit.Digits"/>, after the last digit that fits), so paste keeps what fits. Unlike the field's
/// <c>MaxLength</c> (UTF-16 code units of the whole text), it counts what the user sees, or only the digits. Text already over
/// the cap (set by the app) is not cut, but cannot grow.
/// </summary>
public sealed class SkUiLengthLimitingFormatter : SkUiTextInputFormatter
{
    /// <summary>A cap set up by <see cref="MaxLength"/> and <see cref="Unit"/> (XAML).</summary>
    public SkUiLengthLimitingFormatter()
    {
    }

    /// <summary>A cap of <paramref name="maxLength"/> <paramref name="unit"/>s.</summary>
    public SkUiLengthLimitingFormatter(int maxLength, SkUiTextLengthUnit unit = SkUiTextLengthUnit.Graphemes)
    {
        MaxLength = maxLength;
        Unit = unit;
    }

    /// <summary>The most units the text may have (default <see cref="int.MaxValue"/>: no cap).</summary>
    public int MaxLength
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    } = int.MaxValue;

    /// <summary>What is counted (default graphemes).</summary>
    public SkUiTextLengthUnit Unit
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            field = value;
        }
    }

    /// <inheritdoc />
    public override SkUiTextEditValue Format(SkUiTextEditValue oldValue, SkUiTextEditValue newValue)
    {
        if (MaxLength == int.MaxValue || Count(newValue.Text) <= MaxLength)
            return newValue;
        var (start, end, inserted) = SkUiTextEditor.Diff(oldValue.Text, newValue.Text);
        var room = MaxLength - Count(oldValue.Text) + Count(oldValue.Text.AsSpan(start, end - start));
        var kept = Fit(inserted, room);
        if (kept == inserted.Length)
            return newValue;
        var text = string.Concat(oldValue.Text.AsSpan(0, start), inserted.AsSpan(0, kept), oldValue.Text.AsSpan(end));
        if (text == oldValue.Text)
            return oldValue;
        return new SkUiTextEditValue(text, start + kept);
    }

    private int Count(ReadOnlySpan<char> text)
    {
        if (Unit == SkUiTextLengthUnit.Graphemes)
        {
            var count = 0;
            for (var i = 0; i < text.Length; i += StringInfo.GetNextTextElementLength(text[i..]))
                count++;
            return count;
        }
        var units = 0;
        foreach (var character in text)
            if (Counts(character))
                units++;
        return units;
    }

    // UTF-16 code units of the start of `inserted` that hold at most `room` units (whole graphemes).
    private int Fit(string inserted, int room)
    {
        if (Unit != SkUiTextLengthUnit.Graphemes)
            return CutBeforeUnit(inserted, Math.Max(0, room));
        var length = 0;
        for (var n = 0; n < room && length < inserted.Length; n++)
            length += StringInfo.GetNextTextElementLength(inserted.AsSpan(length));
        return length;
    }

    // Where the counted character past `room` starts, cut back to just after the last one that fits (the separators between
    // them go too); all of it when nothing counted is past the room.
    private int CutBeforeUnit(string inserted, int room)
    {
        var seen = 0;
        var lastEnd = 0;
        for (var i = 0; i < inserted.Length; i++)
        {
            if (!Counts(inserted[i]))
                continue;
            if (seen == room)
                return lastEnd;
            seen++;
            lastEnd = i + 1;
        }
        return inserted.Length;
    }

    private bool Counts(char character) => Unit switch
    {
        SkUiTextLengthUnit.Digits => character is >= '0' and <= '9',
        _ => character is >= '0' and <= '9' || char.IsLetter(character)
    };
}

/// <summary>
/// Mask-as-you-type: the text follows <see cref="Mask"/>, whose placeholders take characters and whose other characters are
/// literals the field inserts as the user reaches them (<c>"(000) 000-0000"</c>: typing <c>5551234567</c> shows
/// <c>(555) 123-4567</c>). Placeholders, as .NET's <c>MaskedTextProvider</c> (and the vendors' masked entries) required ones:
/// <c>0</c> a digit, <c>L</c> a letter, <c>A</c> a letter or digit, <c>&amp;</c> any character; <c>\</c> makes the next mask
/// character a literal. <c>MaskedTextProvider</c>'s other mask characters (<c>9 # ? a C &lt; &gt; |</c>) are reserved: a mask
/// with one of them unescaped is refused, so a mask ported from it does not show them as literals. Characters that fit no
/// placeholder are dropped, typed literals are kept, pasted text is re-masked, text past the mask is cut, and literals after the
/// last typed character are not shown until something follows them (a deletion removes them). Backspace or Delete on a literal
/// removes the character before (after) it.
/// </summary>
public sealed class SkUiMaskFormatter : SkUiTextInputFormatter
{
    private enum SlotKind : byte { Literal, Digit, Letter, LetterOrDigit, Any }

    // MaskedTextProvider's optional placeholders and case shifts: refused until SkiaUi supports them, so supporting them later does not
    // change what an existing mask shows.
    private const string ReservedMaskCharacters = "9#?aC<>|";

    private (SlotKind Kind, char Literal)[] _slots = [];

    /// <summary>A mask set up by <see cref="Mask"/> (XAML).</summary>
    public SkUiMaskFormatter()
    {
    }

    /// <summary>A formatter for <paramref name="mask"/>.</summary>
    public SkUiMaskFormatter(string mask)
    {
        Mask = mask;
    }

    /// <summary>
    /// The mask (see the class); <c>null</c> or empty: the text is not masked.
    /// </summary>
    /// <exception cref="ArgumentException">The mask has a reserved character (<c>9 # ? a C &lt; &gt; |</c>) that is not escaped.</exception>
    public string? Mask
    {
        get;
        set
        {
            var slots = new List<(SlotKind, char)>();
            var text = value ?? string.Empty;
            for (var i = 0; i < text.Length; i++)
            {
                var character = text[i];
                if (character == '\\' && i + 1 < text.Length)
                    slots.Add((SlotKind.Literal, text[++i]));
                else
                    slots.Add(character switch
                    {
                        '0' => (SlotKind.Digit, '\0'),
                        'L' => (SlotKind.Letter, '\0'),
                        'A' => (SlotKind.LetterOrDigit, '\0'),
                        '&' => (SlotKind.Any, '\0'),
                        _ when ReservedMaskCharacters.Contains(character) => throw new ArgumentException(
                            $"'{character}' is a reserved mask character (MaskedTextProvider's); escape it as \\{character} for a literal.", nameof(value)),
                        _ => (SlotKind.Literal, character)
                    });
            }
            _slots = [.. slots];
            field = value;
        }
    }

    /// <summary>The characters of <paramref name="text"/> (masked by this formatter) that fill placeholders, without the literals.</summary>
    public string Unmask(string? text)
    {
        if (string.IsNullOrEmpty(text) || _slots.Length == 0)
            return text ?? string.Empty;
        var builder = new StringBuilder(text.Length);
        Apply(text, null, builder);
        return builder.ToString();
    }

    /// <summary>Whether <paramref name="text"/> fills every placeholder of the mask.</summary>
    public bool IsComplete(string? text)
    {
        if (_slots.Length == 0)
            return true;
        var placeholders = 0;
        foreach (var (kind, _) in _slots)
            if (kind != SlotKind.Literal)
                placeholders++;
        return Unmask(text).Length == placeholders;
    }

    /// <inheritdoc />
    public override SkUiTextEditValue Format(SkUiTextEditValue oldValue, SkUiTextEditValue newValue)
    {
        if (_slots.Length == 0)
            return newValue;
        var text = newValue.Text;
        var positions = new int[text.Length + 1];
        var masked = Apply(text, positions, null);
        if (masked == oldValue.Text && text.Length < oldValue.Text.Length)
        {
            // Only literals were deleted, and masking put them back: the character before them (Backspace) or after them (Delete) goes.
            var (start, end, inserted) = SkUiTextEditor.Diff(oldValue.Text, text);
            if (inserted.Length == 0 && end > start)
            {
                var forward = oldValue.SelectionLength == 0 && oldValue.SelectionStart == start;
                var index = InputIndex(text, positions, start, forward);
                if (index >= 0)
                {
                    text = text.Remove(index, 1);
                    positions = new int[text.Length + 1];
                    masked = Apply(text, positions, null);
                    return WithoutTrailingLiterals(new SkUiTextEditValue(masked, positions[index]));
                }
            }
        }
        var value = new SkUiTextEditValue(masked, positions[newValue.SelectionStart], positions[newValue.SelectionEnd]);
        return text.Length < oldValue.Text.Length ? WithoutTrailingLiterals(value) : value;
    }

    // After a deletion, literals the mask put at the end go too: Backspace after "(555) 1" leaves "(555", not "(555) ".
    private SkUiTextEditValue WithoutTrailingLiterals(SkUiTextEditValue value)
    {
        var length = value.Text.Length;
        while (length > 0 && _slots[length - 1].Kind == SlotKind.Literal)
            length--;
        return length == value.Text.Length
            ? value
            : new SkUiTextEditValue(value.Text[..length], Math.Min(value.SelectionStart, length), Math.Min(value.SelectionEnd, length));
    }

    // The index of the character of `text` that fills a placeholder nearest before (or from) `index`, or -1.
    private int InputIndex(string text, int[] positions, int index, bool forward)
    {
        if (forward)
        {
            for (var i = index; i < text.Length; i++)
                if (FillsPlaceholder(positions, i))
                    return i;
        }
        else
        {
            for (var i = Math.Min(index, text.Length) - 1; i >= 0; i--)
                if (FillsPlaceholder(positions, i))
                    return i;
        }
        return -1;
    }

    // The output has a character per mask slot, so a character placed by masking sits in slot positions[i + 1] - 1.
    private bool FillsPlaceholder(int[] positions, int i) =>
        positions[i + 1] > positions[i] && _slots[positions[i + 1] - 1].Kind != SlotKind.Literal;

    // Masks `text`; `positions[i]`: where the output stands before text[i] (and after the last character at text.Length). With
    // `inputs`, appends the characters that fill placeholders there instead and returns an empty string.
    private string Apply(string text, int[]? positions, StringBuilder? inputs)
    {
        var output = inputs is null ? new StringBuilder(_slots.Length) : null;
        var slot = 0;
        var length = 0;
        var i = 0;
        for (; i < text.Length && slot < _slots.Length; i++)
        {
            if (positions is not null)
                positions[i] = length;
            var character = text[i];
            var (slotBefore, lengthBefore) = (slot, length);
            var placed = false;
            while (slot < _slots.Length)
            {
                var (kind, literal) = _slots[slot];
                if (kind == SlotKind.Literal)
                {
                    output?.Append(literal);
                    slot++;
                    length++;
                    if (character == literal)
                    {
                        placed = true;
                        break;
                    }
                    continue;
                }
                if (Matches(kind, character))
                {
                    output?.Append(character);
                    inputs?.Append(character);
                    slot++;
                    length++;
                    placed = true;
                }
                break;
            }
            if (!placed)
            {
                // Dropped: the literals put in front of it go too.
                output?.Remove(lengthBefore, length - lengthBefore);
                (slot, length) = (slotBefore, lengthBefore);
            }
        }
        if (positions is not null)
            for (; i <= text.Length; i++)
                positions[i] = length;
        return output?.ToString() ?? string.Empty;
    }

    private static bool Matches(SlotKind kind, char character) => kind switch
    {
        SlotKind.Digit => character is >= '0' and <= '9',
        SlotKind.Letter => char.IsLetter(character),
        SlotKind.LetterOrDigit => character is >= '0' and <= '9' || char.IsLetter(character),
        SlotKind.Any => !char.IsSurrogate(character) && !char.IsControl(character),
        _ => false
    };
}
