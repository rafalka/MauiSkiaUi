#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using WinPoint = Windows.Foundation.Point;
using WinRect = Windows.Foundation.Rect;

namespace MauiSkiaUi;

/// <summary>
/// Narrator (and every UI Automation client) over a drawn surface: the surface container's automation peer has one peer per
/// semantics element, with the Invoke, Toggle, SelectionItem and RangeValue patterns, hit-testing and keyboard focus.
/// Hosted native views keep their own peers, placed where they are in the semantics tree. The tree is built when a client first asks.
/// </summary>
internal sealed class SkUiAccessibilityBridge
{
    private readonly FrameworkElement _container;
    private readonly SkUiView _root;
    private readonly Dictionary<int, SkUiElementAutomationPeer> _peers = [];
    private SkUiSemanticsOwner? _owner;
    private bool _listening;

    public SkUiAccessibilityBridge(FrameworkElement container, SkUiView root)
    {
        _container = container;
        _root = root;
        root.SemanticFocusRequested = RequestAccessibilityFocus;
        root.FocusManager.FocusChanged += OnKeyboardFocusChanged;
    }

    internal SkUiView Root => _root;

    private bool _detached;

    /// <summary>The tree; empty once the bridge is detached (clients may still hold peers of it).</summary>
    internal SkUiSemanticsTree Tree => _detached ? SkUiSemanticsTree.Empty : (_owner ??= new SkUiSemanticsOwner(_root)).Tree;

    /// <summary>A client reads the surface's children: report changes from now on while it listens.</summary>
    internal void UpdateListening()
    {
        if (_detached)
            return;
        _owner ??= new SkUiSemanticsOwner(_root);
        Listen(HasListeners);
    }

    /// <summary>Whether a UI Automation client listens for the events the bridge raises.</summary>
    private static bool HasListeners =>
        AutomationPeer.ListenerExists(AutomationEvents.StructureChanged) || AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged);

    /// <summary>Change reports (rebuilds while the drawn tree changes) only while a client listens; queries always see the current tree.</summary>
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

    internal SkUiSemanticsOwner? Owner => _owner;


    /// <summary>Stops keeping the tree (the handler disconnects).</summary>
    public void Detach()
    {
        _root.FocusManager.FocusChanged -= OnKeyboardFocusChanged;
        if (ReferenceEquals(_root.SemanticFocusRequested?.Target, this))
            _root.SemanticFocusRequested = null;
        Listen(false);
        _detached = true;
        _owner?.Detach();
        _owner = null;
        _peers.Clear();
    }

    /// <summary>The peer of element <paramref name="id"/> (one per element while it exists).</summary>
    internal SkUiElementAutomationPeer PeerOf(int id)
    {
        if (!_peers.TryGetValue(id, out var peer))
            _peers[id] = peer = new SkUiElementAutomationPeer(this, id);
        return peer;
    }

    /// <summary>The peers of <paramref name="node"/>'s child elements in tree order, hosted native views' own peers among them.</summary>
    internal List<AutomationPeer> ChildPeers(SkUiSemanticsNode node)
    {
        var peers = new List<AutomationPeer>(node.Children.Count);
        foreach (var child in node.Children)
            if (!child.IsNative)
                peers.Add(PeerOf(child.Id));
            else if (NativePeer(child) is { } native)
                peers.Add(native);
        return peers;
    }

    /// <summary>The native peers <see cref="ChildPeers"/> places (anywhere in the tree).</summary>
    internal HashSet<AutomationPeer> PlacedNativePeers()
    {
        var placed = new HashSet<AutomationPeer>();
        foreach (var node in Tree.Flatten())
            if (node.IsNative && NativePeer(node) is { } native)
                placed.Add(native);
        return placed;
    }

    /// <summary>The automation peer of the native view a hosted-content element stands for (none for a plain panel).</summary>
    private static AutomationPeer? NativePeer(SkUiSemanticsNode node) =>
        (node.Source as SkUiMauiContentView)?.Content?.Handler?.PlatformView is UIElement native
            ? FrameworkElementAutomationPeer.CreatePeerForElement(native)
            : null;

    /// <summary>Window DIPs of a surface rectangle (what WinUI's own peers report).</summary>
    internal WinRect ToWindow(Rect bounds)
    {
        if (_container.XamlRoot is null)
            return default;
        var origin = _container.TransformToVisual(null).TransformPoint(new WinPoint(bounds.X, bounds.Y));
        return new WinRect(origin.X, origin.Y, bounds.Width, bounds.Height);
    }

    /// <summary>Surface DIPs of a window point.</summary>
    internal Point FromWindow(WinPoint point)
    {
        var origin = _container.TransformToVisual(null).TransformPoint(new WinPoint(0, 0));
        return new Point(point.X - origin.X, point.Y - origin.Y);
    }

    private void OnSemanticsChanged(bool structureChanged, IReadOnlyList<int> changed)
    {
        if (!HasListeners)
        {
            Listen(false);
            return;
        }
        var tree = Tree;
        foreach (var id in _peers.Keys.Where(id => tree.Find(id) is null).ToList())
            _peers.Remove(id);
        if (structureChanged)
            if (FrameworkElementAutomationPeer.FromElement(_container) is { } surface)
                surface.RaiseStructureChangedEvent(AutomationStructureChangeType.ChildrenInvalidated, surface);
        foreach (var id in changed)
            if (_peers.TryGetValue(id, out var peer) && tree.Find(id) is { } node)
                peer.RaiseChanges(node);
    }

    private void OnKeyboardFocusChanged(ISkUiAccessibleNode? node)
    {
        if (node is not null && AutomationPeer.ListenerExists(AutomationEvents.AutomationFocusChanged) && Tree.Find(node) is { } element)
            PeerOf(element.Id).RaiseAutomationEvent(AutomationEvents.AutomationFocusChanged);
    }

    private void RequestAccessibilityFocus(int id)
    {
        if (Tree.Find(id) is not null)
            PeerOf(id).RaiseAutomationEvent(AutomationEvents.AutomationFocusChanged);
    }
}

/// <summary>
/// The surface container's peer: the drawn elements and hosted native views in tree order, then native peers the tree does
/// not place (a hosted panel without a peer of its own: its children's peers). WinUI creates it once per
/// container, so it asks the container for its current bridge each time: without one (accessibility switched off for the
/// surface) it is a plain container peer.
/// </summary>
internal sealed partial class SkUiSurfaceAutomationPeer(FrameworkElement owner, Func<SkUiAccessibilityBridge?> bridge) : FrameworkElementAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override IList<AutomationPeer> GetChildrenCore()
    {
        if (bridge() is not { } current)
            return base.GetChildrenCore();
        current.UpdateListening();
        var children = current.ChildPeers(current.Tree.Root);
        if (base.GetChildrenCore() is { Count: > 0 } native)
        {
            var placed = current.PlacedNativePeers();
            foreach (var peer in native)
                if (!placed.Contains(peer))
                    children.Add(peer);
        }
        return children;
    }

    /// <inheritdoc />
    protected override AutomationPeer GetPeerFromPointCore(WinPoint point)
    {
        if (bridge() is { } current && current.Tree.HitTest(current.FromWindow(point)) is { IsNative: false } node)
            return current.PeerOf(node.Id);
        return base.GetPeerFromPointCore(point);
    }

    /// <inheritdoc />
    protected override object GetFocusedElementCore()
    {
        if (bridge() is { } current && current.Root.FocusManagerIfCreated?.Focused is { } focused && current.Tree.Find(focused) is { } node)
            return current.PeerOf(node.Id);
        return base.GetFocusedElementCore();
    }

    /// <inheritdoc />
    protected override string GetClassNameCore() => "SkUiSurface";
}

/// <summary>One drawn element as UI Automation sees it.</summary>
internal sealed partial class SkUiElementAutomationPeer(SkUiAccessibilityBridge bridge, int id)
    : AutomationPeer, IInvokeProvider, IToggleProvider, IRangeValueProvider, ISelectionItemProvider
{
    private string _name = string.Empty;
    private ToggleState _toggle;
    private double _value;

    private SkUiSemanticsNode? Node => bridge.Tree.Find(id);

    /// <summary>Raises property changes a client may be watching (name, toggle state, range value).</summary>
    internal void RaiseChanges(SkUiSemanticsNode node)
    {
        var name = node.Label ?? string.Empty;
        if (name != _name)
            RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, _name, name);
        _name = name;
        var toggle = ToToggleState(node.CheckState);
        if (node.CheckState is not null && toggle != _toggle)
            RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, _toggle, toggle);
        _toggle = toggle;
        if (node.Range is { } range && range.Value != _value)
        {
            RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, _value, range.Value);
            _value = range.Value;
        }
    }

    private static ToggleState ToToggleState(SkUiCheckState? state) => state switch
    {
        SkUiCheckState.Checked => ToggleState.On,
        SkUiCheckState.Indeterminate => ToggleState.Indeterminate,
        _ => ToggleState.Off
    };

    private bool Perform(SkUiSemanticsActions action) => bridge.Owner?.Perform(id, action) == true;

    /// <inheritdoc />
    protected override IList<AutomationPeer> GetChildrenCore() => Node is { } node ? bridge.ChildPeers(node) : [];

    /// <inheritdoc />
    protected override string GetNameCore() => _name = Node?.Label ?? string.Empty;

    /// <inheritdoc />
    protected override string GetHelpTextCore() => Node?.Hint ?? string.Empty;

    /// <inheritdoc />
    protected override string GetAutomationIdCore() => Node?.AutomationId ?? string.Empty;

    /// <inheritdoc />
    protected override string GetClassNameCore() => Node?.Source?.GetType().Name ?? string.Empty;

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => Node?.Role switch
    {
        SkUiSemanticsRole.Text => AutomationControlType.Text,
        SkUiSemanticsRole.Image => AutomationControlType.Image,
        SkUiSemanticsRole.Button => AutomationControlType.Button,
        SkUiSemanticsRole.CheckBox => AutomationControlType.CheckBox,
        SkUiSemanticsRole.Switch => AutomationControlType.Button, // WinUI's ToggleSwitch: a button with the Toggle pattern
        SkUiSemanticsRole.RadioButton => AutomationControlType.RadioButton,
        SkUiSemanticsRole.Slider => AutomationControlType.Slider,
        SkUiSemanticsRole.ProgressBar => AutomationControlType.ProgressBar,
        SkUiSemanticsRole.ScrollView => AutomationControlType.Pane,
        _ => Node is { Actions: var actions } && (actions & SkUiSemanticsActions.Activate) != 0 ? AutomationControlType.Button : AutomationControlType.Group
    };

    /// <inheritdoc />
    protected override WinRect GetBoundingRectangleCore() => Node is { } node ? bridge.ToWindow(node.Bounds) : default;

    /// <inheritdoc />
    protected override WinPoint GetClickablePointCore()
    {
        var bounds = GetBoundingRectangleCore();
        return new WinPoint(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }

    /// <inheritdoc />
    protected override bool IsContentElementCore() => true;

    /// <inheritdoc />
    protected override bool IsControlElementCore() => true;

    /// <inheritdoc />
    protected override bool IsEnabledCore() => Node?.IsEnabled == true;

    /// <inheritdoc />
    protected override bool IsKeyboardFocusableCore() => Node?.IsFocusable == true;

    /// <inheritdoc />
    protected override bool HasKeyboardFocusCore() => Node?.IsFocused == true;

    /// <inheritdoc />
    protected override bool IsOffscreenCore() => Node is null;

    /// <inheritdoc />
    protected override void SetFocusCore()
    {
        if (Node?.Source is { } source)
            bridge.Root.FocusManager.Focus(source);
    }

    /// <inheritdoc />
    protected override AutomationHeadingLevel GetHeadingLevelCore() => (int)(Node?.HeadingLevel ?? SemanticHeadingLevel.None) switch
    {
        >= 1 and <= 9 and var level => (AutomationHeadingLevel)level,
        _ => AutomationHeadingLevel.None
    };

    /// <inheritdoc />
    protected override AutomationPeer GetPeerFromPointCore(WinPoint point)
    {
        if (Node is { } node && bridge.Tree.HitTest(bridge.FromWindow(point)) is { IsNative: false } hit)
            for (var current = hit; current is not null; current = current.Parent)
                if (current.Id == node.Id)
                    return bridge.PeerOf(hit.Id);
        return this;
    }

    /// <inheritdoc />
    protected override object? GetPatternCore(PatternInterface patternInterface)
    {
        if (Node is not { } node)
            return null;
        var actions = node.Actions;
        return patternInterface switch
        {
            PatternInterface.Toggle when node.CheckState is not null && node.Role != SkUiSemanticsRole.RadioButton => this,
            PatternInterface.SelectionItem when node.Role == SkUiSemanticsRole.RadioButton => this,
            PatternInterface.RangeValue when node.Range is not null => this,
            PatternInterface.Invoke when node.CheckState is null && (actions & SkUiSemanticsActions.Activate) != 0 => this,
            _ => null
        };
    }

    // Invoke
    public void Invoke() => Perform(SkUiSemanticsActions.Activate);

    // Toggle
    // Cached as read, so the next change raises the value the client saw (as the name).
    public ToggleState ToggleState => _toggle = ToToggleState(Node?.CheckState);

    public void Toggle() => Perform(SkUiSemanticsActions.Activate);

    // RangeValue
    public double Value => _value = Node?.Range?.Value ?? 0;

    public double Minimum => Node?.Range?.Minimum ?? 0;

    public double Maximum => Node?.Range?.Maximum ?? 0;

    public double SmallChange => Node?.Range is { } range ? SkUiSemantics.RangeStep(range.Minimum, range.Maximum) : 0;

    public double LargeChange => SmallChange * 2;

    public bool IsReadOnly => Node is not { Role: SkUiSemanticsRole.Slider, IsEnabled: true };

    public void SetValue(double value) => bridge.Owner?.SetValue(id, value);

    // SelectionItem
    public bool IsSelected => Node?.CheckState == SkUiCheckState.Checked;

    public IRawElementProviderSimple SelectionContainer => null!;

    public void Select()
    {
        if (!IsSelected)
            Perform(SkUiSemanticsActions.Activate);
    }

    public void AddToSelection() => Select();

    public void RemoveFromSelection() { }
}

/// <summary>WinUI key events of a focused surface container as <see cref="SkUiKey"/>s.</summary>
internal static class SkUiWindowsKeys
{
    public static SkUiKey Map(Windows.System.VirtualKey key) => key switch
    {
        Windows.System.VirtualKey.Tab => SkUiKey.Tab,
        Windows.System.VirtualKey.Enter => SkUiKey.Enter,
        Windows.System.VirtualKey.Space => SkUiKey.Space,
        Windows.System.VirtualKey.Escape => SkUiKey.Escape,
        Windows.System.VirtualKey.Left => SkUiKey.Left,
        Windows.System.VirtualKey.Up => SkUiKey.Up,
        Windows.System.VirtualKey.Right => SkUiKey.Right,
        Windows.System.VirtualKey.Down => SkUiKey.Down,
        Windows.System.VirtualKey.PageUp => SkUiKey.PageUp,
        Windows.System.VirtualKey.PageDown => SkUiKey.PageDown,
        Windows.System.VirtualKey.Home => SkUiKey.Home,
        Windows.System.VirtualKey.End => SkUiKey.End,
        _ => SkUiKey.None
    };

    public static SkUiKeyModifiers Modifiers()
    {
        static bool Down(Windows.System.VirtualKey key) =>
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        return (Down(Windows.System.VirtualKey.Shift) ? SkUiKeyModifiers.Shift : 0)
            | (Down(Windows.System.VirtualKey.Control) ? SkUiKeyModifiers.Control : 0)
            | (Down(Windows.System.VirtualKey.Menu) ? SkUiKeyModifiers.Alt : 0);
    }
}
#endif
