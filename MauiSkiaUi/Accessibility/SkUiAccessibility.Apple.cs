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
    private readonly UIView _container;
    private readonly SkUiView _root;
    private readonly Dictionary<int, SkUiAccessibilityElement> _elements = [];
    private SkUiSemanticsOwner? _owner;
    private NSArray? _array;
    private bool _listening;

    public SkUiAccessibilityBridge(UIView container, SkUiView root)
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
            if (!_elements.TryGetValue(node.Id, out var element))
                _elements[node.Id] = element = new SkUiAccessibilityElement(_container, this, node.Id);
            Apply(element, node);
            items.Add(element);
            live.Add(node.Id);
        }
        foreach (var id in _elements.Keys.Where(id => !live.Contains(id)).ToList())
            _elements.Remove(id);
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
        if (structureChanged)
        {
            _array = null;
            UIAccessibility.PostNotification(UIAccessibilityPostNotification.LayoutChanged, null);
            return;
        }
        // Values and states: update the elements in place (VoiceOver reads them when it next asks).
        var tree = Tree;
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
internal sealed class SkUiAccessibilityElement : UIAccessibilityElement
{
    private readonly WeakReference<SkUiAccessibilityBridge> _bridge;

    public SkUiAccessibilityElement(NSObject container, SkUiAccessibilityBridge bridge, int id) : base(container)
    {
        _bridge = new WeakReference<SkUiAccessibilityBridge>(bridge);
        Id = id;
        IsAccessibilityElement = true;
    }

    public int Id { get; }

    private bool Perform(SkUiSemanticsActions action) => _bridge.TryGetTarget(out var bridge) && bridge.Perform(Id, action);

    /// <summary>VoiceOver's double tap (an informal-protocol method: exported, not overridden).</summary>
    [Export("accessibilityActivate")]
    public bool AccessibilityActivate() => Perform(SkUiSemanticsActions.Activate);

    public override void AccessibilityIncrement() => Perform(SkUiSemanticsActions.Increment);

    public override void AccessibilityDecrement() => Perform(SkUiSemanticsActions.Decrement);

    public override bool AccessibilityScroll(UIAccessibilityScrollDirection direction) =>
        _bridge.TryGetTarget(out var bridge) && bridge.Scroll(Id, direction);

    public override void AccessibilityElementDidBecomeFocused()
    {
        if (_bridge.TryGetTarget(out var bridge))
            bridge.OnFocused(Id);
    }
}

/// <summary>Hardware keyboard presses of a focused surface container as <see cref="SkUiKey"/>s.</summary>
internal static class SkUiAppleKeys
{
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
