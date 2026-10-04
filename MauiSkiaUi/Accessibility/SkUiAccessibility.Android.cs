#if ANDROID
using Android.OS;
using Android.Views.Accessibility;
using AndroidX.Core.View;
using AndroidX.Core.View.Accessibility;
using AndroidX.CustomView.Widget;
using AView = Android.Views.View;

namespace MauiSkiaUi;

/// <summary>
/// TalkBack (and every Android accessibility service) over a drawn surface: the surface's semantics tree as virtual views of
/// its container, with explore-by-touch, actions, scrolling and change events. The tree is built only while a service is on.
/// Hosted native views are read natively, as real children placed where they are in the semantics tree (as Flutter embeds
/// platform views): the container's own node is built here, since <see cref="ExploreByTouchHelper"/> refuses a host with
/// both real and virtual children.
/// </summary>
internal sealed class SkUiAccessibilityHelper : ExploreByTouchHelper
{
    private readonly AView _host;
    private readonly SkUiView _root;
    private readonly AccessibilityManager? _manager;
    private SkUiSemanticsOwner? _owner;
    private readonly int[] _location = new int[2];
    private HostNodeProvider? _provider;

    public SkUiAccessibilityHelper(AView host, SkUiView root) : base(host)
    {
        _host = host;
        _root = root;
        _manager = host.Context?.GetSystemService(Android.Content.Context.AccessibilityService) as AccessibilityManager;
        root.SemanticFocusRequested = RequestAccessibilityFocus;
        root.FocusManager.FocusChanged += OnKeyboardFocusChanged;
    }

    private float Density => _host.Resources?.DisplayMetrics?.Density ?? 1;

    /// <summary>The semantics (created while a service reads the surface).</summary>
    private SkUiSemanticsTree? Tree
    {
        get
        {
            if (_owner is null)
            {
                if (_manager is not { IsEnabled: true })
                    return null;
                _owner = new SkUiSemanticsOwner(_root);
                _owner.Changed += OnSemanticsChanged;
            }
            return _owner.Tree;
        }
    }

    /// <summary>Stops keeping the tree (the handler disconnects).</summary>
    public void Detach()
    {
        _root.FocusManager.FocusChanged -= OnKeyboardFocusChanged;
        if (ReferenceEquals(_root.SemanticFocusRequested?.Target, this))
            _root.SemanticFocusRequested = null;
        _owner?.Detach();
        _owner = null;
    }

    private void OnSemanticsChanged(bool structureChanged, IReadOnlyList<int> changed)
    {
        if (_manager is not { IsEnabled: true })
        {
            // The service was turned off: stop rebuilding until one asks again.
            _owner?.Detach();
            _owner = null;
            return;
        }
        if (structureChanged || changed.Count > 16)
        {
            InvalidateRoot();
            return;
        }
        foreach (var id in changed)
            InvalidateVirtualView(id);
    }

    private bool _syncingKeyboardFocus;

    /// <summary>
    /// The drawn keyboard focus moved: the helper's own keyboard focus follows, so TalkBack's nodes report it (focused state,
    /// focus actions) and get a focus event.
    /// </summary>
    private void OnKeyboardFocusChanged(ISkUiAccessibleNode? node)
    {
        if (_owner is not { } owner)
            return;
        _syncingKeyboardFocus = true;
        try
        {
            if (node is not null && owner.Tree.Find(node) is { } element)
                RequestKeyboardFocusForVirtualView(element.Id);
            else if (KeyboardFocusedVirtualViewId != InvalidId)
                ClearKeyboardFocusForVirtualView(KeyboardFocusedVirtualViewId);
        }
        finally
        {
            _syncingKeyboardFocus = false;
        }
    }

    private void RequestAccessibilityFocus(int id) =>
        GetAccessibilityNodeProvider(_host)?.PerformAction(id, AccessibilityNodeInfoCompat.ActionAccessibilityFocus, null);

    /// <inheritdoc />
    protected override int GetVirtualViewAt(float x, float y)
    {
        // Native views (hosted controls) and the empty surface are not virtual views: hover goes on to the real children.
        if (Tree?.HitTest(new Point(x / Density, y / Density)) is { IsNative: false } node)
            return node.Id;
        return InvalidId;
    }

    /// <inheritdoc />
    protected override void GetVisibleVirtualViews(IList<Java.Lang.Integer>? virtualViewIds)
    {
        // Every element, nested ones too (the helper's own focus search walks this list; the host's children are set by
        // CreateHostNode).
        if (virtualViewIds is null || Tree is not { } tree)
            return;
        foreach (var node in tree.Flatten())
            if (!node.IsNative)
                virtualViewIds.Add(Java.Lang.Integer.ValueOf(node.Id));
    }

    /// <inheritdoc />
    public override AccessibilityNodeProviderCompat? GetAccessibilityNodeProvider(AView? host) =>
        _provider ??= base.GetAccessibilityNodeProvider(host) is { } inner ? new HostNodeProvider(this, inner) : null;

    /// <summary>
    /// The container's node: its own state (as the helper sets it), then the surface's top-level elements in tree order,
    /// hosted native views among them.
    /// </summary>
    internal AccessibilityNodeInfoCompat CreateHostNodeForProvider() => CreateHostNode();

    private AccessibilityNodeInfoCompat CreateHostNode()
    {
#pragma warning disable CS0618 // Obtain / OnInitializeAccessibilityNodeInfo: what ExploreByTouchHelper itself does for the host
        var info = AccessibilityNodeInfoCompat.Obtain(_host)!;
        ViewCompat.OnInitializeAccessibilityNodeInfo(_host, info);
#pragma warning restore CS0618
        if (Tree is not { } tree)
            return info;
        // The container may have listed hosted views as its own children: each goes where the tree has it instead.
        foreach (var node in tree.Flatten())
            if (node.IsNative && NativeView(node) is { } native)
                info.RemoveChild(native);
        foreach (var node in tree.Root.Children)
            AddChild(info, node);
        return info;
    }

    /// <summary>Adds <paramref name="child"/> to <paramref name="info"/>: a virtual view, or the hosted native view.</summary>
    private void AddChild(AccessibilityNodeInfoCompat info, SkUiSemanticsNode child)
    {
        if (!child.IsNative)
            info.AddChild(_host, child.Id);
        else if (NativeView(child) is { } native)
            info.AddChild(native);
    }

    /// <summary>The native view a hosted-content element stands for (read by Android itself).</summary>
    private static AView? NativeView(SkUiSemanticsNode node) =>
        (node.Source as SkUiMauiContentView)?.Content?.Handler?.PlatformView as AView;

    /// <inheritdoc />
    protected override void OnPopulateNodeForVirtualView(int virtualViewId, AccessibilityNodeInfoCompat? node)
    {
        if (node is null)
            return;
        var density = Density;
        if (Tree?.Find(virtualViewId) is not { } element || element.Source is null)
        {
            // A stale id (the element went away): an empty, invisible node.
            node.ContentDescription = string.Empty;
#pragma warning disable CS0618 // ExploreByTouchHelper requires the parent bounds
            node.SetBoundsInParent(new Android.Graphics.Rect(0, 0, 1, 1));
#pragma warning restore CS0618
            return;
        }
        node.ClassName = ClassName(element);
        if (element.Role is SkUiSemanticsRole.Text or SkUiSemanticsRole.Button)
            node.Text = element.Label ?? string.Empty;
        else
            node.ContentDescription = element.Label ?? string.Empty;
        if (element.Hint is { } hint && OperatingSystem.IsAndroidVersionAtLeast(26))
            node.HintText = hint;
        if (element.HeadingLevel != SemanticHeadingLevel.None)
            node.Heading = true;
        if (element.AutomationId is { } automationId && OperatingSystem.IsAndroidVersionAtLeast(18))
            node.ViewIdResourceName = automationId;
        node.Enabled = element.IsEnabled;
        node.Focusable = element.IsFocusable;
        if (element.CheckState is { } state)
        {
            node.Checkable = true;
            node.Checked = state == SkUiCheckState.Checked;
        }
        if (element.Range is { } range)
            node.RangeInfo = AccessibilityNodeInfoCompat.RangeInfoCompat.Obtain(
                AccessibilityNodeInfoCompat.RangeInfoCompat.RangeTypeFloat, (float)range.Minimum, (float)range.Maximum, (float)range.Value);
        if (element.Value is { } value && OperatingSystem.IsAndroidVersionAtLeast(30))
            node.StateDescription = value;

        var actions = element.EnabledActions;
        if ((actions & SkUiSemanticsActions.Activate) != 0)
        {
            node.Clickable = true;
            node.AddAction(AccessibilityNodeInfoCompat.ActionClick);
        }
        if ((actions & SkUiSemanticsActions.LongPress) != 0)
        {
            node.LongClickable = true;
            node.AddAction(AccessibilityNodeInfoCompat.ActionLongClick);
        }
        // Sliders step and scrollers page with the scroll actions, as Android's SeekBar and ScrollView do.
        if ((actions & (SkUiSemanticsActions.Increment | SkUiSemanticsActions.ScrollForward)) != 0)
            node.AddAction(AccessibilityNodeInfoCompat.ActionScrollForward);
        if ((actions & (SkUiSemanticsActions.Decrement | SkUiSemanticsActions.ScrollBackward)) != 0)
            node.AddAction(AccessibilityNodeInfoCompat.ActionScrollBackward);
        if (element.Role == SkUiSemanticsRole.ScrollView)
            node.Scrollable = (actions & (SkUiSemanticsActions.ScrollForward | SkUiSemanticsActions.ScrollBackward)) != 0;
        if (element.Range is not null && (actions & SkUiSemanticsActions.Increment) != 0 && OperatingSystem.IsAndroidVersionAtLeast(24))
            node.AddAction(AccessibilityNodeInfoCompat.AccessibilityActionCompat.ActionSetProgress);

        // The tree: elements inside scrollers or tappable groups are children of those (TalkBack scrolls the scrollable
        // ancestor of the focused node when reading past the visible ones).
        if (element.Parent is { Id: not SkUiSemanticsNode.RootId } parent)
            node.SetParent(_host, parent.Id);
        foreach (var child in element.Children)
            AddChild(node, child);

        // Bounds: the host's pixels, and the screen's (so nested virtual parents need no offsets).
        var bounds = element.Bounds;
        var local = new Android.Graphics.Rect(
            (int)Math.Floor(bounds.Left * density), (int)Math.Floor(bounds.Top * density),
            (int)Math.Ceiling(bounds.Right * density), (int)Math.Ceiling(bounds.Bottom * density));
#pragma warning disable CS0618 // ExploreByTouchHelper requires the parent bounds
        node.SetBoundsInParent(local);
#pragma warning restore CS0618
        _host.GetLocationOnScreen(_location);
        local.Offset(_location[0], _location[1]);
        node.SetBoundsInScreen(local);
    }

    /// <inheritdoc />
    protected override void OnPopulateEventForVirtualView(int virtualViewId, AccessibilityEvent? e)
    {
        if (e is null)
            return;
        var element = _owner?.Tree.Find(virtualViewId);
        e.ContentDescription = element?.Label ?? string.Empty;
        e.ClassName = element is null ? "android.view.View" : ClassName(element);
        if (element is { Source: { } source } && e.EventType == EventTypes.ViewAccessibilityFocused)
            SkUiSemantics.BringIntoView(source); // focused while partly scrolled out: show it, as native lists do
    }

    /// <inheritdoc />
    protected override bool OnPerformActionForVirtualView(int virtualViewId, int action, Bundle? arguments)
    {
        if (_owner is not { } owner || owner.Tree.Find(virtualViewId) is not { } element)
            return false;
        bool done;
        switch (action)
        {
            case AccessibilityNodeInfoCompat.ActionClick:
                done = owner.Perform(virtualViewId, SkUiSemanticsActions.Activate);
                if (done)
                    SendEventForVirtualView(virtualViewId, (int)EventTypes.ViewClicked);
                break;
            case AccessibilityNodeInfoCompat.ActionLongClick:
                done = owner.Perform(virtualViewId, SkUiSemanticsActions.LongPress);
                if (done)
                    SendEventForVirtualView(virtualViewId, (int)EventTypes.ViewLongClicked);
                break;
            case AccessibilityNodeInfoCompat.ActionScrollForward:
                done = owner.Perform(virtualViewId, element.Range is not null ? SkUiSemanticsActions.Increment : SkUiSemanticsActions.ScrollForward);
                break;
            case AccessibilityNodeInfoCompat.ActionScrollBackward:
                done = owner.Perform(virtualViewId, element.Range is not null ? SkUiSemanticsActions.Decrement : SkUiSemanticsActions.ScrollBackward);
                break;
            default:
                if (OperatingSystem.IsAndroidVersionAtLeast(24) && action == Android.Resource.Id.AccessibilityActionSetProgress
                    && arguments?.ContainsKey(AccessibilityNodeInfoCompat.ActionArgumentProgressValue) == true)
                {
                    done = owner.SetValue(virtualViewId, arguments.GetFloat(AccessibilityNodeInfoCompat.ActionArgumentProgressValue));
                    break;
                }
                return false;
        }
        if (done)
            InvalidateVirtualView(virtualViewId);
        return done;
    }

    /// <inheritdoc />
    protected override void OnVirtualViewKeyboardFocusChanged(int virtualViewId, bool hasFocus)
    {
        // An accessibility service moved keyboard focus (ACTION_FOCUS): focus the drawn node.
        if (!_syncingKeyboardFocus && hasFocus && _owner?.Tree.Find(virtualViewId)?.Source is { } source)
            _root.FocusManager.Focus(source);
    }

    /// <summary>The framework class whose behavior TalkBack announces for a role.</summary>
    private static string ClassName(SkUiSemanticsNode node) => node.Role switch
    {
        SkUiSemanticsRole.Text => "android.widget.TextView",
        SkUiSemanticsRole.Image => "android.widget.ImageView",
        SkUiSemanticsRole.Button => "android.widget.Button",
        SkUiSemanticsRole.CheckBox => "android.widget.CheckBox",
        SkUiSemanticsRole.Switch => "android.widget.Switch",
        SkUiSemanticsRole.RadioButton => "android.widget.RadioButton",
        SkUiSemanticsRole.Slider => "android.widget.SeekBar",
        SkUiSemanticsRole.ProgressBar => "android.widget.ProgressBar",
        SkUiSemanticsRole.ScrollView => node.IsHorizontal ? "android.widget.HorizontalScrollView" : "android.widget.ScrollView",
        _ => "android.view.View"
    };
}

/// <summary>
/// The helper's node provider, except for the host's node (<see cref="SkUiAccessibilityHelper"/> builds it: the helper's own
/// throws when the container has real accessible children, i.e. hosted native views).
/// </summary>
internal sealed class HostNodeProvider(SkUiAccessibilityHelper helper, AccessibilityNodeProviderCompat inner) : AccessibilityNodeProviderCompat
{
    public override AccessibilityNodeInfoCompat? CreateAccessibilityNodeInfo(int virtualViewId) =>
        virtualViewId == ExploreByTouchHelper.HostId ? helper.CreateHostNodeForProvider() : inner.CreateAccessibilityNodeInfo(virtualViewId);

    public override bool PerformAction(int virtualViewId, int action, Bundle? arguments) => inner.PerformAction(virtualViewId, action, arguments);

    public override AccessibilityNodeInfoCompat? FindFocus(int focus) => inner.FindFocus(focus);

    public override IList<AccessibilityNodeInfoCompat>? FindAccessibilityNodeInfosByText(string? text, int virtualViewId) =>
        inner.FindAccessibilityNodeInfosByText(text, virtualViewId);
}

/// <summary>Android key events of a focused surface container as <see cref="SkUiKey"/>s.</summary>
internal static class SkUiAndroidKeys
{
    public static SkUiKey Map(Android.Views.Keycode code) => code switch
    {
        Android.Views.Keycode.Tab => SkUiKey.Tab,
        Android.Views.Keycode.Enter or Android.Views.Keycode.NumpadEnter or Android.Views.Keycode.DpadCenter => SkUiKey.Enter,
        Android.Views.Keycode.Space => SkUiKey.Space,
        Android.Views.Keycode.Escape => SkUiKey.Escape,
        Android.Views.Keycode.DpadLeft => SkUiKey.Left,
        Android.Views.Keycode.DpadUp => SkUiKey.Up,
        Android.Views.Keycode.DpadRight => SkUiKey.Right,
        Android.Views.Keycode.DpadDown => SkUiKey.Down,
        Android.Views.Keycode.PageUp => SkUiKey.PageUp,
        Android.Views.Keycode.PageDown => SkUiKey.PageDown,
        Android.Views.Keycode.MoveHome => SkUiKey.Home,
        Android.Views.Keycode.MoveEnd => SkUiKey.End,
        _ => SkUiKey.None
    };

    public static SkUiKeyModifiers Modifiers(Android.Views.KeyEvent e) =>
        (e.IsShiftPressed ? SkUiKeyModifiers.Shift : 0) | (e.IsCtrlPressed ? SkUiKeyModifiers.Control : 0)
        | (e.IsAltPressed ? SkUiKeyModifiers.Alt : 0) | (e.IsMetaPressed ? SkUiKeyModifiers.Command : 0);
}
#endif
