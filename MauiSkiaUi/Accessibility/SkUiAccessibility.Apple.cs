#if IOS || MACCATALYST
using CoreGraphics;
using Foundation;
using UIKit;

namespace MauiSkiaUi;

/// <summary>
/// VoiceOver (and every UIKit accessibility client: Switch Control, Voice Control, UI tests) over a drawn surface: the
/// surface container's accessibility elements are the semantics tree's elements in reading order, with hosted native
/// views in their place. Built when a client first asks, then kept up to date (layout-changed notifications).
/// </summary>
internal sealed class SkUiAccessibilityBridge
{
    private static UIAccessibilityTrait? _switchTraits;
    private static ulong? _textFieldTraits;
    private readonly SkUiOverlayContainer _container;
    private readonly SkUiView _root;
    private readonly Dictionary<int, SkUiAccessibilityElement> _elements = [];
    private SkUiSemanticsOwner? _owner;
    private NSArray? _array;
    private bool _listening;
    private SkUiTextInputView? _idleTextInput;

    public SkUiAccessibilityBridge(SkUiOverlayContainer container, SkUiView root)
    {
        _container = container;
        _root = root;
        root.SemanticFocusRequested = RequestAccessibilityFocus;
    }

    private SkUiSemanticsTree Tree => (_owner ??= new SkUiSemanticsOwner(_root)).Tree;

    /// <summary>
    /// Change reports (rebuilds while the drawn tree changes) only while VoiceOver runs, to tell it about new layouts; other
    /// clients (Switch Control, UI tests) get an up-to-date array whenever they ask.
    /// </summary>
    private void Listen(bool listen)
    {
        if (_owner is null || listen == _listening)
            return;
        _listening = listen;
        if (listen)
            _owner.Changed += OnSemanticsChanged;
        else
            _owner.Changed -= OnSemanticsChanged;
    }

    /// <summary>Stops keeping the tree (the handler disconnects).</summary>
    public void Detach()
    {
        if (ReferenceEquals(_root.SemanticFocusRequested?.Target, this))
            _root.SemanticFocusRequested = null;
        Listen(false);
        _owner?.Detach();
        _owner = null;
        _elements.Clear();
        _array = null;
        _idleTextInput?.RemoveFromSuperview();
        _idleTextInput = null;
    }

    /// <summary>The container's accessibility elements (<c>accessibilityElements</c>).</summary>
    public NSArray Elements()
    {
        var tree = Tree;
        Listen(UIAccessibility.IsVoiceOverRunning);
        if (_array is { } array && _builtVersion == _owner!.Version)
            return array;
        var items = new List<NSObject>();
        var live = new HashSet<int>();
        foreach (var node in tree.Flatten())
        {
            if (node.IsNative)
            {
                if ((node.Source as SkUiMauiContentView)?.Content?.Handler?.PlatformView is UIView native)
                    items.Add(native);
                continue;
            }
            // A scroller without a name is only a container: its children are the elements (they scroll it).
            if (node.Role == SkUiSemanticsRole.ScrollView && node.Label is null)
                continue;
            if (!_elements.TryGetValue(node.Id, out var element) || element is SkUiTextFieldAccessibilityElement != NavigatesText(node))
                _elements[node.Id] = element = NavigatesText(node)
                    ? new SkUiTextFieldAccessibilityElement(_container, this, node.Id)
                    : new SkUiAccessibilityElement(_container, this, node.Id);
            Apply(element, node);
            items.Add(element);
            live.Add(node.Id);
        }
        foreach (var id in _elements.Keys.Where(id => !live.Contains(id)).ToList())
            _elements.Remove(id);
        ReleaseIdleTextInput();
        _builtVersion = _owner!.Version;
        return _array = NSArray.FromNSObjects(items.ToArray());
    }

    /// <summary>The tree version the element array was built from (not the tree: it would keep its nodes alive).</summary>
    private int _builtVersion = -1;

    private void OnSemanticsChanged(bool structureChanged, IReadOnlyList<int> changed)
    {
        if (!UIAccessibility.IsVoiceOverRunning)
        {
            Listen(false);
            return;
        }
        var tree = Tree;
        // A field became or stopped being a password: its element changes kind, as when the structure changes.
        if (!structureChanged)
            foreach (var id in changed)
                if (_elements.TryGetValue(id, out var element) && tree.Find(id) is { } node
                    && element is SkUiTextFieldAccessibilityElement != NavigatesText(node))
                    structureChanged = true;
        if (structureChanged)
        {
            _array = null;
            ReleaseIdleTextInput();
            UIAccessibility.PostNotification(UIAccessibilityPostNotification.LayoutChanged, null);
            return;
        }
        // Values and states: update the elements in place (VoiceOver reads them when it next asks).
        foreach (var id in changed)
            if (_elements.TryGetValue(id, out var element) && tree.Find(id) is { } node)
                Apply(element, node);
        _builtVersion = _owner!.Version;
    }

    private void Apply(SkUiAccessibilityElement element, SkUiSemanticsNode node)
    {
        element.AccessibilityLabel = node.Label;
        element.AccessibilityHint = node.Hint;
        element.AccessibilityIdentifier = node.AutomationId;
        var bounds = node.Bounds;
        element.AccessibilityFrameInContainerSpace = new CGRect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        var traits = UIAccessibilityTrait.None;
        string? value = node.DisplayValue;
        switch (node.Role)
        {
            case SkUiSemanticsRole.Text:
                traits |= UIAccessibilityTrait.StaticText;
                break;
            case SkUiSemanticsRole.Image:
                traits |= UIAccessibilityTrait.Image;
                break;
            case SkUiSemanticsRole.Button:
                traits |= UIAccessibilityTrait.Button;
                break;
            case SkUiSemanticsRole.CheckBox or SkUiSemanticsRole.Switch:
                // As MAUI's check box: UISwitch's traits (VoiceOver reads "switch button, on / off") and its 1 / 0 value.
                traits |= SwitchTraits | UIAccessibilityTrait.Button;
                // A mixed state has no switch value: "mixed", as AppKit reads a mixed check box (a control's own value wins).
                value = node.CheckState switch
                {
                    SkUiCheckState.Checked => "1",
                    SkUiCheckState.Indeterminate => node.Value ?? "mixed",
                    _ => "0"
                };
                break;
            case SkUiSemanticsRole.RadioButton:
                traits |= UIAccessibilityTrait.Button;
                if (node.CheckState == SkUiCheckState.Checked)
                    traits |= UIAccessibilityTrait.Selected;
                break;
            case SkUiSemanticsRole.Slider:
                traits |= UIAccessibilityTrait.Adjustable;
                break;
            case SkUiSemanticsRole.ProgressBar:
                traits |= UIAccessibilityTrait.UpdatesFrequently;
                break;
            case SkUiSemanticsRole.ComboBox:
                // As UIKit's pop-up button (UIButton with a menu): a button read with its selection as the value.
                traits |= UIAccessibilityTrait.Button;
                break;
            case SkUiSemanticsRole.TextField:
                // As a UITextField: its traits (read as a text field), the text as the value (the placeholder, as the label, while
                // empty); activating focuses it. The element navigates the text itself (SkUiTextFieldAccessibilityElement).
                value = string.IsNullOrEmpty(node.Value) ? null : node.Value;
                if (element is SkUiTextFieldAccessibilityElement field)
                    field.Field = (node.Source as ISkUiTextInputNode)?.TextInputClient;
                traits |= (UIAccessibilityTrait)TextFieldTraits;
                break;
            default:
                if ((node.Actions & SkUiSemanticsActions.Activate) != 0)
                    traits |= UIAccessibilityTrait.Button;
                break;
        }
        if (node.HeadingLevel != SemanticHeadingLevel.None)
            traits |= UIAccessibilityTrait.Header;
        if (!node.IsEnabled)
            traits |= UIAccessibilityTrait.NotEnabled;
        element.AccessibilityTraits = (ulong)traits;
        element.AccessibilityValue = value;
    }

    /// <summary>UISwitch's traits (a private switch trait among them); <c>None</c> until VoiceOver has run once, as in MAUI.</summary>
    private static UIAccessibilityTrait SwitchTraits
    {
        get
        {
            if (_switchTraits is { } traits && traits != UIAccessibilityTrait.None)
                return traits;
            using var probe = new UISwitch();
            _switchTraits = probe.AccessibilityTraits;
            return _switchTraits.Value;
        }
    }

    /// <summary>UITextField's traits (private ones: VoiceOver reads "text field"); <c>0</c> until an accessibility client has run, as <see cref="SwitchTraits"/>.</summary>
    private static ulong TextFieldTraits
    {
        get
        {
            if (_textFieldTraits is { } traits && traits != 0)
                return traits;
            using var probe = new UITextField();
            _textFieldTraits = (ulong)probe.AccessibilityTraits;
            return _textFieldTraits.Value;
        }
    }

    /// <summary>
    /// Whether the element of <paramref name="node"/> navigates its text (<see cref="SkUiTextFieldAccessibilityElement"/>): an
    /// editable field, not a password.
    /// </summary>
    private static bool NavigatesText(SkUiSemanticsNode node) =>
        node is { Role: SkUiSemanticsRole.TextField, TextField.IsPassword: false, Source: ISkUiTextInputNode };

    /// <summary>The focused field's text input view when <paramref name="field"/> is that field (VoiceOver edits through it).</summary>
    internal SkUiTextInputView? FocusedTextInput(ISkUiTextInputClient field) =>
        _container.TextInput?.View is { } view && ReferenceEquals(view.Client, field) ? view : null;

    /// <summary>
    /// The view answering VoiceOver's text questions about <paramref name="field"/>: the focused field's text input view, else an
    /// idle one of the same kind (never first responder, at the container's origin: its coordinates are the surface's) set to the
    /// field until the next field asks or the elements are rebuilt.
    /// </summary>
    internal SkUiTextInputView TextInputFor(ISkUiTextInputClient field)
    {
        if (FocusedTextInput(field) is { } focused)
            return focused;
        if (_idleTextInput is null)
        {
            _idleTextInput = new SkUiTextInputView(_container) { Frame = new CGRect(0, 0, 1, 1) };
            _container.AddSubview(_idleTextInput);
        }
        _idleTextInput.Client = field;
        return _idleTextInput;
    }

    /// <summary>The idle view lets its field go (it would keep a removed field alive).</summary>
    internal void ReleaseIdleTextInput()
    {
        if (_idleTextInput is { } idle)
            idle.Client = null;
    }

    /// <summary>VoiceOver moves the caret or selects in field <paramref name="id"/>.</summary>
    public bool SetSelection(int id, int start, int end) => _owner?.SetSelection(id, start, end) == true;

    public bool Perform(int id, SkUiSemanticsActions action) => _owner?.Perform(id, action) == true;

    /// <summary>VoiceOver's three-finger scroll on an element: pages its innermost scrollable ancestor.</summary>
    public bool Scroll(int id, UIAccessibilityScrollDirection direction)
    {
        if (_owner is not { } owner)
            return false;
        // Vertical directions are the scroll bar's (Down shows what is below: forward); horizontal ones the finger's (Left
        // shows what is on the right: forward), as UIKit's scroll views read them.
        var (forward, horizontal) = direction switch
        {
            UIAccessibilityScrollDirection.Down => (true, false),
            UIAccessibilityScrollDirection.Up => (false, false),
            UIAccessibilityScrollDirection.Left => (true, true),
            UIAccessibilityScrollDirection.Right => (false, true),
            UIAccessibilityScrollDirection.Next => (true, (bool?)null),
            _ => (false, (bool?)null)
        };
        var action = forward ? SkUiSemanticsActions.ScrollForward : SkUiSemanticsActions.ScrollBackward;
        for (var node = owner.Tree.Find(id); node is not null; node = node.Parent)
        {
            if (node.Role != SkUiSemanticsRole.ScrollView || (horizontal is { } axis && axis != node.IsHorizontal)
                || (node.EnabledActions & action) == 0)
                continue;
            if (!owner.Perform(node.Id, action))
                return false;
            UIAccessibility.PostNotification(UIAccessibilityPostNotification.PageScrolled, null);
            return true;
        }
        return false;
    }

    /// <summary>VoiceOver moved to an element: a partly scrolled-out element scrolls into view, as in native lists.</summary>
    public void OnFocused(int id)
    {
        if (_owner?.Tree.Find(id)?.Source is { } source)
            SkUiSemantics.BringIntoView(source);
    }

    private void RequestAccessibilityFocus(int id)
    {
        _ = Elements();
        if (_elements.TryGetValue(id, out var element))
            UIAccessibility.PostNotification(UIAccessibilityPostNotification.LayoutChanged, element);
    }
}

/// <summary>One drawn element as VoiceOver sees it.</summary>
internal class SkUiAccessibilityElement : UIAccessibilityElement
{
    private readonly WeakReference<SkUiAccessibilityBridge> _bridge;

    public SkUiAccessibilityElement(NSObject container, SkUiAccessibilityBridge bridge, int id) : base(container)
    {
        _bridge = new WeakReference<SkUiAccessibilityBridge>(bridge);
        Id = id;
        IsAccessibilityElement = true;
    }

    public int Id { get; }

    /// <summary>The bridge, while the surface is there.</summary>
    protected SkUiAccessibilityBridge? Bridge => _bridge.TryGetTarget(out var bridge) ? bridge : null;

    private bool Perform(SkUiSemanticsActions action) => Bridge?.Perform(Id, action) == true;

    /// <summary>VoiceOver's double tap (an informal-protocol method: exported, not overridden).</summary>
    [Export("accessibilityActivate")]
    public bool AccessibilityActivate() => Perform(SkUiSemanticsActions.Activate);

    public override void AccessibilityIncrement() => Perform(SkUiSemanticsActions.Increment);

    public override void AccessibilityDecrement() => Perform(SkUiSemanticsActions.Decrement);

    public override bool AccessibilityScroll(UIAccessibilityScrollDirection direction) => Bridge?.Scroll(Id, direction) == true;

    public override void AccessibilityElementDidBecomeFocused() => Bridge?.OnFocused(Id);
}

/// <summary>Hardware keyboard presses of a focused surface container as <see cref="SkUiKey"/>s.</summary>
internal static class SkUiAppleKeys
{
    /// <summary>The key as a <see cref="SkUiKey"/>; a printable character (without Command or Control) as <see cref="SkUiKey.Character"/>.</summary>
    public static SkUiKey Map(UIKey key, out char character)
    {
        character = '\0';
        var mapped = Map(key);
        if (mapped != SkUiKey.None || (key.ModifierFlags & (UIKeyModifierFlags.Command | UIKeyModifierFlags.Control)) != 0)
            return mapped;
        if (key.Characters is { Length: 1 } text && text[0] > ' ' && !char.IsControl(text[0]))
        {
            character = text[0];
            return SkUiKey.Character;
        }
        return SkUiKey.None;
    }

    public static SkUiKey Map(UIKey key) => key.KeyCode switch
    {
        UIKeyboardHidUsage.KeyboardTab => SkUiKey.Tab,
        UIKeyboardHidUsage.KeyboardReturnOrEnter or UIKeyboardHidUsage.KeypadEnter => SkUiKey.Enter,
        UIKeyboardHidUsage.KeyboardSpacebar => SkUiKey.Space,
        UIKeyboardHidUsage.KeyboardEscape => SkUiKey.Escape,
        UIKeyboardHidUsage.KeyboardLeftArrow => SkUiKey.Left,
        UIKeyboardHidUsage.KeyboardUpArrow => SkUiKey.Up,
        UIKeyboardHidUsage.KeyboardRightArrow => SkUiKey.Right,
        UIKeyboardHidUsage.KeyboardDownArrow => SkUiKey.Down,
        UIKeyboardHidUsage.KeyboardPageUp => SkUiKey.PageUp,
        UIKeyboardHidUsage.KeyboardPageDown => SkUiKey.PageDown,
        UIKeyboardHidUsage.KeyboardHome => SkUiKey.Home,
        UIKeyboardHidUsage.KeyboardEnd => SkUiKey.End,
        UIKeyboardHidUsage.KeyboardDeleteOrBackspace => SkUiKey.Backspace,
        UIKeyboardHidUsage.KeyboardDeleteForward => SkUiKey.Delete,
        _ => SkUiKey.None
    };

    public static SkUiKeyModifiers Modifiers(UIKey key)
    {
        var flags = key.ModifierFlags;
        return (flags.HasFlag(UIKeyModifierFlags.Shift) ? SkUiKeyModifiers.Shift : 0)
            | (flags.HasFlag(UIKeyModifierFlags.Control) ? SkUiKeyModifiers.Control : 0)
            | (flags.HasFlag(UIKeyModifierFlags.Alternate) ? SkUiKeyModifiers.Alt : 0)
            | (flags.HasFlag(UIKeyModifierFlags.Command) ? SkUiKeyModifiers.Command : 0);
    }
}
#endif
