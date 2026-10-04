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
    ScrollView
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

    internal string? Description;
    internal string? Hint;
    internal SemanticHeadingLevel HeadingLevel;
    internal bool? IsInAccessibleTree;
    internal bool ExcludedWithChildren;
    internal string? AutomationId;
    /// <summary>A hosted native view (<see cref="SkUiMauiContentView"/>): the platform reads it itself.</summary>
    internal bool IsNative;

    internal void Reset()
    {
        Role = SkUiSemanticsRole.None;
        Text = Value = Description = Hint = AutomationId = null;
        CheckState = null;
        Range = null;
        IsEnabled = true;
        Actions = SkUiSemanticsActions.None;
        IsHorizontal = false;
        HeadingLevel = SemanticHeadingLevel.None;
        IsInAccessibleTree = null;
        ExcludedWithChildren = false;
        IsNative = false;
    }
}
