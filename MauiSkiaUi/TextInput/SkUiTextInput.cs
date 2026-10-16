using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// What a drawn text field asks of the platform's text input (<see cref="ISkUiTextInputProxy"/>): the soft keyboard's layout
/// and return key, masking, corrections and autofill. From MAUI's <c>Entry</c> / <c>Editor</c> properties.
/// </summary>
/// <param name="Keyboard">MAUI's keyboard (text, numeric, telephone, email, URL, chat, plain, or a custom one's flags).</param>
/// <param name="ReturnType">The return key (an entry's <c>ReturnType</c>; an editor's is a line break).</param>
/// <param name="IsPassword">The text is masked; the keyboard offers no suggestions and nothing is learned.</param>
/// <param name="IsMultiline">Return inserts a line break (an editor).</param>
/// <param name="IsSpellCheckEnabled">Spelling is checked and corrected.</param>
/// <param name="IsTextPredictionEnabled">The keyboard suggests words.</param>
/// <param name="IsReadOnly">The text cannot be changed (the keyboard does not show; selection and copying still work).</param>
/// <param name="TextTransform">Upper or lower case input: the keyboard starts in that case where it can.</param>
/// <param name="AutofillHint">What the field holds for password managers and autofill (<see cref="SkUiAutofillHints"/>), or <c>null</c>.</param>
/// <param name="IsSearch">A search bar: the platform's search keyboard where it has one.</param>
/// <param name="NumberKeyboard">
/// A numeric entry's keyboard (D16), in place of <paramref name="Keyboard"/>: digits, with a decimal separator and a minus sign
/// when the value can have them. <see cref="SkUiNumberKeyboard.None"/>: <paramref name="Keyboard"/>.
/// </param>
internal readonly record struct SkUiTextInputOptions(
    Keyboard Keyboard,
    ReturnType ReturnType,
    bool IsPassword,
    bool IsMultiline,
    bool IsSpellCheckEnabled,
    bool IsTextPredictionEnabled,
    bool IsReadOnly,
    TextTransform TextTransform,
    string? AutofillHint,
    bool IsSearch = false,
    SkUiNumberKeyboard NumberKeyboard = SkUiNumberKeyboard.None)
{
    /// <summary>Whether the keyboard is a numeric entry's (<see cref="NumberKeyboard"/>).</summary>
    internal bool IsNumber => (NumberKeyboard & SkUiNumberKeyboard.Digits) != 0;

    /// <summary>A numeric entry's value can have decimals: the keyboard has a decimal separator.</summary>
    internal bool IsDecimalNumber => (NumberKeyboard & SkUiNumberKeyboard.Decimal) != 0;

    /// <summary>A numeric entry's value can be negative: the keyboard has a minus sign.</summary>
    internal bool IsSignedNumber => (NumberKeyboard & SkUiNumberKeyboard.Signed) != 0;

    /// <summary>The keyboard's capitalization: MAUI's custom keyboard flags, else sentences for chat / text keyboards.</summary>
    internal KeyboardFlags Flags => Keyboard is CustomKeyboard custom ? custom.Flags : KeyboardFlags.None;

    /// <summary>Whether the keyboard is MAUI's <c>Keyboard.Numeric</c>.</summary>
    internal bool IsNumeric => ReferenceEquals(Keyboard, Keyboard.Numeric);

    internal bool IsTelephone => ReferenceEquals(Keyboard, Keyboard.Telephone);
    internal bool IsEmail => ReferenceEquals(Keyboard, Keyboard.Email);
    internal bool IsUrl => ReferenceEquals(Keyboard, Keyboard.Url);
    internal bool IsChat => ReferenceEquals(Keyboard, Keyboard.Chat);
    internal bool IsPlain => ReferenceEquals(Keyboard, Keyboard.Plain);
}

/// <summary>A numeric entry's keyboard (D16): picked from the value's type and range instead of MAUI's one <c>Keyboard.Numeric</c>.</summary>
[Flags]
internal enum SkUiNumberKeyboard
{
    /// <summary>Not a numeric entry: the field's <c>Keyboard</c>.</summary>
    None = 0,

    /// <summary>Digits (Android <c>ClassNumber</c>, iOS <c>NumberPad</c>, Windows <c>Number</c>).</summary>
    Digits = 1,

    /// <summary>With a decimal separator (Android <c>NumberFlagDecimal</c>, iOS <c>DecimalPad</c>).</summary>
    Decimal = 2,

    /// <summary>
    /// With a minus sign (Android <c>NumberFlagSigned</c>; iOS <c>NumbersAndPunctuation</c>, since neither number pad has a minus key).
    /// </summary>
    Signed = 4
}

/// <summary>
/// The focused drawn text field, as the platform's text input sees it (<see cref="ISkUiTextInputProxy"/>): the editor's state,
/// edits from the input method (which the field does not echo back), geometry in surface DIPs for candidate windows and the
/// edit menu, and the field's actions. UI thread.
/// </summary>
internal interface ISkUiTextInputClient
{
    /// <summary>The text, selection and composition (read only: edits go through the methods below).</summary>
    SkUiTextEditor Editor { get; }

    /// <summary>The keyboard and input options.</summary>
    SkUiTextInputOptions Options { get; }

    /// <summary>The drawn node of the field.</summary>
    ISkUiRenderable Node { get; }

    /// <summary>The proxy attached the field (focused) or let it go (<c>null</c>).</summary>
    void InputAttached(ISkUiTextInputProxy? proxy);

    /// <summary>Starts a group of edits the field reports once (an input method's batch edit).</summary>
    void BeginInputBatch();

    /// <summary>Ends a group of edits.</summary>
    void EndInputBatch();

    /// <summary>Replaces <c>[start, end)</c> with <paramref name="text"/> (an input method's commit, a correction, the mirror's diff).</summary>
    void ReplaceText(int start, int end, string text, int? caret = null);

    /// <summary>Inserts committed text over the composition, else the selection.</summary>
    void InsertText(string text);

    /// <summary>Replaces the composition (else the selection) with marked text, the caret at <paramref name="caretInText"/> in it.</summary>
    void SetComposingText(string text, int? caretInText = null);

    /// <summary>Marks committed text <c>[start, end)</c> as the composition; empty: ends it.</summary>
    void SetComposingRegion(int start, int end);

    /// <summary>Commits the composition.</summary>
    void FinishComposing();

    /// <summary>Selects <c>[anchor, caret]</c>.</summary>
    void SetSelection(int anchor, int caret);

    /// <summary>Deletes backward like Backspace (an input method's delete key).</summary>
    void DeleteBackward();

    /// <summary>The return key: an entry completes (<c>Completed</c>, <c>ReturnCommand</c>), an editor inserts a line break.</summary>
    void PerformReturn();

    /// <summary>An edit menu action from the platform (or the field's own menu).</summary>
    void PerformEditAction(SkUiTextEditAction action);

    /// <summary>Whether <paramref name="action"/> can run now (the edit menu shows only those).</summary>
    bool CanPerformEditAction(SkUiTextEditAction action);

    /// <summary>A hardware key the platform's text view received (Apple: <c>pressesBegan</c>); returns whether the field used it.</summary>
    bool HandleKey(in SkUiKeyEvent key);

    /// <summary>The caret at <paramref name="index"/>, in surface DIPs (zero width).</summary>
    Rect GetCaretBounds(int index);

    /// <summary>The boxes of <c>[start, end)</c> in surface DIPs (one per line and bidi stretch).</summary>
    IReadOnlyList<Rect> GetRangeBounds(int start, int end);

    /// <summary>The caret position nearest <paramref name="surfacePoint"/>.</summary>
    int GetIndexAt(Point surfacePoint);

    /// <summary>The field's text box (where text is shown), in surface DIPs.</summary>
    Rect GetTextBounds();

    /// <summary>The field's bounds, in surface DIPs.</summary>
    Rect GetFieldBounds();

    /// <summary>Whether the text runs right to left at <paramref name="index"/>.</summary>
    bool IsRightToLeftAt(int index);

    /// <summary>
    /// The visual line holding the caret at <paramref name="index"/>: its start and its end (before a line break; where a wrapped
    /// line continues). A screen reader's line navigation (VoiceOver's Lines rotor).
    /// </summary>
    (int Start, int End) GetLineAt(int index);

    /// <summary>
    /// The caret <paramref name="lines"/> visual lines below (negative: above) <paramref name="index"/>, at the same x; past the first
    /// or last line the text's start or end (the platform's line movement: UIKit's up / down positions).
    /// </summary>
    int MoveByLines(int index, int lines);

    /// <summary>
    /// A screen reader or the platform's autofill replaces the text (one undo step, formatted as a paste; the platform's input
    /// follows); returns whether it changed.
    /// </summary>
    bool SetTextFromAssistant(string text);

    /// <summary>The placeholder: the platform's autofill reads it as the field's hint.</summary>
    string? Placeholder { get; }

    /// <summary>
    /// The platform's autofill that knows the field (Android, D14), or <c>null</c>: told when the text changes and when the field
    /// leaves its surface. Set by that autofill when it first describes or enters the field.
    /// </summary>
    ISkUiAutofillHost? AutofillHost { get; set; }

    /// <summary>A screen reader moves the selection or caret; returns whether it was done.</summary>
    bool SelectFromAssistant(int start, int end);
}

/// <summary>
/// The platform's autofill of a surface's drawn fields (Android's autofill framework, D14): password managers see the surface's
/// fields as one form, fill them and save what was typed. The fields it knows report to it.
/// </summary>
internal interface ISkUiAutofillHost
{
    /// <summary>The field's text changed (typing, the app, a fill): the platform's autofill gets the new value.</summary>
    void TextChanged(ISkUiTextInputClient client);

    /// <summary>The field left its surface: the platform's autofill takes it as gone (and may offer to save what was typed).</summary>
    void Removed(ISkUiTextInputClient client);
}

/// <summary>Edit menu actions (the platform's menu and keyboard shortcuts).</summary>
internal enum SkUiTextEditAction
{
    Cut,
    Copy,
    Paste,
    SelectAll,
    Undo,
    Redo,
    Delete
}

/// <summary>
/// The platform's text input for one surface (set by the platform handler on the surface's focus manager, or by tests): connects
/// the focused drawn field to the keyboard and input methods. Attached on focus and detached on unfocus by the focus manager.
/// </summary>
internal interface ISkUiTextInputProxy
{
    /// <summary>
    /// Whether typed characters reach the field through the input method (insert text), not as <see cref="SkUiKey.Character"/>
    /// key events: the field then ignores character keys, so nothing is typed twice.
    /// </summary>
    bool ReceivesCharacters { get; }

    /// <summary>The field took keyboard focus: connect it to the platform's text input and show the keyboard.</summary>
    void Attach(ISkUiTextInputClient client);

    /// <summary>The field lost keyboard focus: disconnect it and hide the keyboard (unless another field takes it at once).</summary>
    void Detach(ISkUiTextInputClient client);

    /// <summary>
    /// The user asked for the soft keyboard for the attached field (a tap on it, also after the keyboard was dismissed; a screen
    /// reader's activation); nothing happens while it shows.
    /// </summary>
    void ShowKeyboard(ISkUiTextInputClient client);

    /// <summary>Hides the soft keyboard; the field keeps focus.</summary>
    void HideKeyboard(ISkUiTextInputClient client);

    /// <summary>
    /// The field's text, selection or composition changed by something else than the platform's input (keys, the app,
    /// undo, the field's menu): the platform's copy follows. <paramref name="textChanged"/>: the text itself changed.
    /// </summary>
    void StateChanged(ISkUiTextInputClient client, bool textChanged);

    /// <summary>The field's options changed while attached (keyboard, password): the platform restarts its input.</summary>
    void OptionsChanged(ISkUiTextInputClient client);

    /// <summary>The field moved, scrolled or relaid out: candidate windows and menus follow the caret.</summary>
    void GeometryChanged(ISkUiTextInputClient client);

    /// <summary>
    /// Shows the platform's edit menu (cut, copy, paste, select all: what <see cref="ISkUiTextInputClient.CanPerformEditAction"/>
    /// allows) at <paramref name="anchor"/> (surface DIPs: the selection, the caret or the clicked point). <paramref name="byTouch"/>: a
    /// touch gesture opened it (a double tap, a long press, a handle), so it must not take focus from the keyboard; else a right click.
    /// </summary>
    void ShowEditMenu(ISkUiTextInputClient client, Rect anchor, bool byTouch);

    /// <summary>Hides the edit menu (typing, moving the selection, focus leaving).</summary>
    void HideEditMenu(ISkUiTextInputClient client);

    /// <summary>
    /// Shows the platform's magnifier over the text at <paramref name="caret"/> (surface DIPs: the caret or the selection end a
    /// finger moves), or moves it there: Android's magnifier, iOS's loupe. Nothing where the platform has none.
    /// </summary>
    void ShowMagnifier(ISkUiTextInputClient client, Rect caret) { }

    /// <summary>Hides the magnifier (the finger lifted).</summary>
    void HideMagnifier(ISkUiTextInputClient client) { }
}

/// <summary>A drawn node that edits text: keyboard focus attaches its <see cref="TextInputClient"/> to the platform's text input.</summary>
internal interface ISkUiTextInputNode : ISkUiAccessibleNode
{
    /// <summary>The field's input client.</summary>
    ISkUiTextInputClient TextInputClient { get; }

    /// <summary>Whether the node edits text now (a stepper only while it shows its value): otherwise it takes focus as other drawn controls.</summary>
    bool WantsTextInput { get; }

    /// <summary>
    /// Whether the field can take focus now from a tap or the app: visible and enabled. Unlike Tab, this ignores
    /// <c>IsTabStop</c> and accessibility being switched off (a text field is typed in either way).
    /// </summary>
    bool CanTakeTextFocus { get; }

    /// <summary>
    /// Whether a touch at <paramref name="local"/> (node coordinates) grabs one of the field's handles: they hang outside the
    /// field (the insertion handle under the caret), so a press is offered to the focused field before the hit test.
    /// </summary>
    bool HitsTextHandle(Point local) => false;
}

/// <summary>The clipboard drawn text fields cut, copy and paste with: MAUI's <c>Clipboard</c>; tests replace it.</summary>
internal static class SkUiTextClipboard
{
    /// <summary>Puts text on the clipboard.</summary>
    internal static Func<string, Task> SetText { get; set; } = text => Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(text);

    /// <summary>Reads text from the clipboard (<c>null</c>: none).</summary>
    internal static Func<Task<string?>> GetText { get; set; } = () => Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.GetTextAsync();

    /// <summary>Whether the clipboard holds text (paste is offered).</summary>
    internal static Func<bool> HasText { get; set; } = () => Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.HasText;
}

/// <summary>
/// The platform's autofill of drawn text fields: password managers fill the fields of a surface by their <c>AutofillHint</c> and
/// offer to save what was typed (Android's autofill service; iOS and Mac Catalyst through the Passwords app, by the hint alone).
/// Call <see cref="Commit"/> when a form is submitted (a sign-in, a new account), as Flutter's <c>finishAutofillContext</c>:
/// otherwise Android offers to save only once the fields leave the screen, and only with services that ask for that.
/// </summary>
public static partial class SkUiAutofill
{
    /// <summary>
    /// The form was submitted: the platform's autofill may offer to save or update what was typed (Android
    /// <c>AutofillManager.commit()</c>). Nothing on platforms without such a call (iOS decides by itself, Windows has no autofill).
    /// </summary>
    public static void Commit() => FinishContext(save: true);

    /// <summary>
    /// The form was abandoned or its input refused (a failed sign-in): the platform's autofill forgets it without offering to save
    /// (Android <c>AutofillManager.cancel()</c>).
    /// </summary>
    public static void Cancel() => FinishContext(save: false);

    static partial void FinishContext(bool save);
}

/// <summary>
/// Common values of a text field's <c>AutofillHint</c>: what password managers and the platform's autofill fill in. Any other
/// string is passed to the platform as it is (Android autofill hints, Apple text content types).
/// </summary>
public static class SkUiAutofillHints
{
    /// <summary>A user name or account name.</summary>
    public const string Username = "username";
    /// <summary>The current password.</summary>
    public const string Password = "password";
    /// <summary>A new password (password managers offer a generated one).</summary>
    public const string NewPassword = "newPassword";
    /// <summary>An email address.</summary>
    public const string EmailAddress = "emailAddress";
    /// <summary>A telephone number.</summary>
    public const string PhoneNumber = "phoneNumber";
    /// <summary>A one-time code (SMS verification).</summary>
    public const string OneTimeCode = "oneTimeCode";
    /// <summary>A person's full name.</summary>
    public const string Name = "name";
    /// <summary>A postal address.</summary>
    public const string PostalAddress = "postalAddress";
    /// <summary>A postal code.</summary>
    public const string PostalCode = "postalCode";
    /// <summary>A credit card number.</summary>
    public const string CreditCardNumber = "creditCardNumber";
}
