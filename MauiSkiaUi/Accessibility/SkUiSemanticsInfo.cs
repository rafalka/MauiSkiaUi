using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>What a drawn element is to assistive technologies (TalkBack, VoiceOver, Narrator).</summary>
public enum SkUiSemanticsRole
{
    /// <summary>
    /// No role of its own: a container. It becomes an element only with a description, a heading level or an action, and is
    /// read as a group then.
    /// </summary>
    None,
    /// <summary>Static text (a label).</summary>
    Text,
    /// <summary>An image; read only with a description.</summary>
    Image,
    /// <summary>A button: activating it runs its action.</summary>
    Button,
    /// <summary>A check box (<see cref="SkUiSemanticsInfo.CheckState"/>).</summary>
    CheckBox,
    /// <summary>A switch (<see cref="SkUiSemanticsInfo.CheckState"/>).</summary>
    Switch,
    /// <summary>A radio button (<see cref="SkUiSemanticsInfo.CheckState"/>).</summary>
    RadioButton,
    /// <summary>An adjustable value (<see cref="SkUiSemanticsInfo.Range"/>, increment / decrement).</summary>
    Slider,
    /// <summary>Progress (<see cref="SkUiSemanticsInfo.Range"/>, or none while indeterminate).</summary>
    ProgressBar,
    /// <summary>A scrolling container: its children are elements, and it scrolls by page.</summary>
    ScrollView,
    /// <summary>
    /// A combo box / pop-up button (a picker): activating it opens its list; <see cref="SkUiSemanticsInfo.Value"/> is the
    /// selected item's text and <see cref="SkUiSemanticsInfo.IsExpanded"/> whether the list is open.
    /// </summary>
    ComboBox,
    /// <summary>
    /// An editable text field (an entry, an editor): <see cref="SkUiSemanticsInfo.Value"/> is its text (bullets for a password),
    /// <see cref="SkUiSemanticsInfo.Text"/> its placeholder; activating it focuses it for typing.
    /// </summary>
    TextField
}

/// <summary>Actions assistive technologies (and the keyboard) can perform on a drawn element.</summary>
[Flags]
public enum SkUiSemanticsActions
{
    /// <summary>None.</summary>
    None = 0,
    /// <summary>The tap: click a button, toggle a check box or switch, check a radio button (double tap in TalkBack / VoiceOver, Narrator's invoke, Space / Enter).</summary>
    Activate = 1,
    /// <summary>The long press.</summary>
    LongPress = 2,
    /// <summary>Raise the value one step (slider: arrow keys, swipe up in TalkBack / VoiceOver).</summary>
    Increment = 4,
    /// <summary>Lower the value one step.</summary>
    Decrement = 8,
    /// <summary>Scroll one page towards the end.</summary>
    ScrollForward = 16,
    /// <summary>Scroll one page towards the start.</summary>
    ScrollBackward = 32
}

/// <summary>An editable text field as screen readers see it: its selection (UTF-16 indices of the text read), and whether it can be changed.</summary>
/// <param name="SelectionStart">Start of the selection (the caret without one).</param>
/// <param name="SelectionEnd">End of the selection.</param>
/// <param name="IsPassword">The text is masked (read as bullets).</param>
/// <param name="IsReadOnly">The text cannot be changed.</param>
/// <param name="IsMultiline">Several lines (an editor).</param>
/// <param name="HasError">The input is invalid (the field's <c>HasError</c>).</param>
/// <param name="Message">The message under the field (its helper text, or its error text with an error), read after the field.</param>
internal readonly record struct SkUiSemanticsTextField(int SelectionStart, int SelectionEnd, bool IsPassword, bool IsReadOnly, bool IsMultiline,
    bool HasError = false, string? Message = null);

/// <summary>A range value: a slider's or a determinate progress bar's.</summary>
/// <param name="Minimum">Lowest value.</param>
/// <param name="Maximum">Highest value.</param>
/// <param name="Value">Current value.</param>
public readonly record struct SkUiSemanticsRange(double Minimum, double Maximum, double Value)
{
    /// <summary>Where <see cref="Value"/> lies between the bounds, from 0 to 1.</summary>
    public double Fraction => Maximum > Minimum ? Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1) : 0;
}

/// <summary>
/// What a drawn view or Core node reports to assistive technologies, filled by its <c>OnPopulateSemantics</c>. The
/// description, hint and heading level come from MAUI's <c>SemanticProperties</c> (SkUi*) or the node's semantic
/// properties (Core) and are applied on top. One instance is reused while the tree is built: do not keep it.
/// </summary>
public sealed class SkUiSemanticsInfo
{
    /// <summary>What the element is.</summary>
    public SkUiSemanticsRole Role { get; set; }

    /// <summary>
    /// The element's own text, read as its name: a label's text, a button's caption, a radio button's text content. A
    /// description replaces it.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>A value read after the name (e.g. "3 of 5"); range elements derive one from <see cref="Range"/> when <c>null</c>.</summary>
    public string? Value { get; set; }

    /// <summary>The checked state of a check box, switch or radio button; <c>null</c> when the element is not checkable.</summary>
    public SkUiCheckState? CheckState { get; set; }

    /// <summary>The range of a slider or a determinate progress bar; <c>null</c> otherwise.</summary>
    public SkUiSemanticsRange? Range { get; set; }

    /// <summary>
    /// Whether the element reacts to its actions (a disabled element is read as dimmed, and its actions do nothing). Default
    /// <c>true</c>.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>What can be done with the element.</summary>
    public SkUiSemanticsActions Actions { get; set; }

    /// <summary>Whether a scrolling element moves horizontally (its page actions scroll left / right).</summary>
    public bool IsHorizontal { get; set; }

    /// <summary>Whether a combo box's list is open; <c>null</c> when the element does not expand.</summary>
    public bool? IsExpanded { get; set; }

    /// <summary>An editable text field's state (<see cref="SkUiSemanticsRole.TextField"/>): screen readers edit and select its text.</summary>
    internal SkUiSemanticsTextField? TextField;

    internal string? Description;
    internal string? Hint;
    internal SemanticHeadingLevel HeadingLevel;
    internal bool? IsInAccessibleTree;
    internal bool ExcludedWithChildren;
    /// <summary>The element stands for its subtree: its descendants are not read (a loading shimmer's skeleton).</summary>
    internal bool ChildrenHidden;
    internal string? AutomationId;
    /// <summary>
    /// A descendant whose <c>AutomationId</c> the element takes when it absorbs that descendant and has none of its own (a
    /// collection item container: its item's root), so UI tests find the item by the id the app gave it.
    /// </summary>
    internal ISkUiRenderable? AutomationIdRoot;
    /// <summary>A hosted native view (<see cref="SkUiMauiContentView"/>): the platform reads it itself.</summary>
    internal bool IsNative;

    internal void Reset()
    {
        Role = SkUiSemanticsRole.None;
        Text = Value = Description = Hint = AutomationId = null;
        AutomationIdRoot = null;
        CheckState = null;
        Range = null;
        IsEnabled = true;
        Actions = SkUiSemanticsActions.None;
        IsHorizontal = false;
        IsExpanded = null;
        TextField = null;
        HeadingLevel = SemanticHeadingLevel.None;
        IsInAccessibleTree = null;
        ExcludedWithChildren = false;
        ChildrenHidden = false;
        IsNative = false;
    }
}
