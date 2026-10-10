using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>Keys the drawn tree reacts to (from the surface's platform key events).</summary>
internal enum SkUiKey
{
    None,
    Tab,
    Enter,
    Space,
    Escape,
    Left,
    Up,
    Right,
    Down,
    PageUp,
    PageDown,
    Home,
    End
}

/// <summary>Modifier keys held with a <see cref="SkUiKey"/>.</summary>
[Flags]
internal enum SkUiKeyModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
    Command = 8
}

/// <summary>
/// Keyboard focus of one surface (UI thread): at most one drawn node of either layer is focused, while the surface has
/// the platform's keyboard focus. Tab / Shift+Tab walk the focusable nodes (visible, enabled, tab stops, interactive) by
/// MAUI's <c>TabIndex</c>, then tree order, and leave the surface at either end; Space / Enter activate the focused node,
/// arrow keys adjust it (a slider) or scroll its scroller, Page Up / Page Down scroll by page. The look's focus ring
/// shows while focus came from the keyboard (after a key press, or programmatic focus while the keyboard was last used);
/// a pointer press hides it.
/// </summary>
internal sealed class SkUiFocusManager(SkUiView root)
{
    /// <summary>Line step of arrow-key scrolling, DIPs.</summary>
    internal const double LineStep = 40;

    private static readonly List<WeakReference<SkUiFocusManager>> _holders = [];
    private static bool _lastInputWasPointer = true;
    private readonly List<ISkUiAccessibleNode> _order = [];
    private readonly List<(int TabIndex, int Position, ISkUiAccessibleNode Node)> _sorted = [];
    private readonly List<ISkUiRenderable> _scratch = [];
    private readonly SkUiSemanticsInfo _info = new();
    private ISkUiAccessibleNode? _focused;
    private bool _ringVisible;
    private bool _hasNativeFocus = true;

    /// <summary>The focused node, or <c>null</c>.</summary>
    public ISkUiAccessibleNode? Focused => _focused;

    /// <summary>Whether the focused node shows its focus ring.</summary>
    public bool IsRingVisible => _ringVisible;

    /// <summary>Set by the platform handler: takes the platform's keyboard focus for the surface; returns whether it has it.</summary>
    public Func<bool>? RequestNativeFocus { get; set; }

    /// <summary>Set by the platform handler: gives the platform's keyboard focus up (the focused drawn node was unfocused).</summary>
    public Action? ReleaseNativeFocus { get; set; }

    /// <summary>
    /// Set by the platform handler: native scrollers around the surface show this rectangle of it (surface DIPs: the newly
    /// focused node, where drawn scrollers move it), as they do for a focused native control.
    /// </summary>
    public Action<Rect>? RequestNativeReveal { get; set; }

    /// <summary>
    /// Whether a pointer press focuses the interactive node it hits (the node or its nearest focusable ancestor), without
    /// the ring: on Windows, as WinUI's controls. Off by default.
    /// </summary>
    public bool PointerPressFocuses { get; set; }

    /// <summary>A press hit <paramref name="leaf"/>: focuses its nearest focusable node when <see cref="PointerPressFocuses"/>.</summary>
    internal void OnPointerPressed(ISkUiRenderable leaf)
    {
        if (!PointerPressFocuses || !SkUiAccessibility.IsEnabled)
            return;
        for (ISkUiRenderable? current = leaf; current is not null; current = current.RenderParent)
            if (current is ISkUiAccessibleNode { IsKeyboardFocusable: true } node)
            {
                Focus(node, ring: false);
                return;
            }
    }

    /// <summary>Raised after the focused node changed (the platform bridge moves the screen reader's keyboard focus).</summary>
    public event Action<ISkUiAccessibleNode?>? FocusChanged;

    /// <summary>A pointer pressed somewhere: focus rings hide until the next key press (focus-visible).</summary>
    internal static void NotePointerInput()
    {
        if (_lastInputWasPointer)
            return;
        _lastInputWasPointer = true;
        for (var index = _holders.Count - 1; index >= 0; index--)
            if (LiveHolder(index) is { } manager)
                manager.SetRingVisible(false);
    }

    /// <summary>
    /// The platform focus of the surface changed. Gained by Tab (<paramref name="forward"/> set) focuses the first or last
    /// node; lost unfocuses the focused node (MAUI's <c>IsFocused</c> follows keyboard focus). Returns whether a drawn node
    /// has focus afterwards: a surface tabbed into with nothing to focus passes focus on.
    /// </summary>
    public bool OnNativeFocusChanged(bool focused, bool? forward = null)
    {
        _hasNativeFocus = focused;
        if (!focused)
        {
            SetFocused(null, ring: false);
            return false;
        }
        if (_focused is null && forward is { } direction && SkUiAccessibility.IsEnabled)
        {
            _lastInputWasPointer = false;
            BuildOrder();
            var first = _order.Count == 0 ? null : direction ? _order[0] : _order[^1];
            _order.Clear();
            if (first is not null)
                Focus(first, ring: true);
        }
        return _focused is not null;
    }

    /// <summary>
    /// Whether any node of the surface takes keyboard focus now (UIKit asks on focus searches): stops at the first one, no
    /// order is built.
    /// </summary>
    public bool HasFocusableNodes => SkUiAccessibility.IsEnabled && AnyFocusable(root);

    private bool AnyFocusable(ISkUiRenderable node)
    {
        if (IsBlocked(node))
            return false;
        if (node is ISkUiAccessibleNode { IsKeyboardFocusable: true })
            return true;
        var start = _scratch.Count;
        node.GetRenderChildren(_scratch);
        try
        {
            for (var index = start; index < _scratch.Count; index++)
                if (AnyFocusable(_scratch[index]))
                    return true;
        }
        finally
        {
            _scratch.RemoveRange(start, _scratch.Count - start);
        }
        return false;
    }

    /// <summary>
    /// Whether keyboard focus skips <paramref name="node"/> and its subtree: hidden, input-transparent, disabled, or taken
    /// out of accessibility (<see cref="SkUiView.IsAccessibilityEnabled"/>).
    /// </summary>
    private static bool IsBlocked(ISkUiRenderable node) =>
        (node is ISkUiInputNode input && (!input.IsHitTestVisible || !input.IsInputEnabled)) || node is SkUiView { IsAccessibilityEnabled: false };

    /// <summary>Platform hooks are set and the surface starts without keyboard focus.</summary>
    public void Connect() => _hasNativeFocus = false;

    /// <summary>The surface went away: nothing stays focused.</summary>
    public void Disconnect()
    {
        SetFocused(null, ring: false);
        RequestNativeFocus = null;
        ReleaseNativeFocus = null;
        RequestNativeReveal = null;
        _hasNativeFocus = true;
    }

    /// <summary>Focuses <paramref name="node"/> (taking the surface's platform focus first); returns whether it is focused.</summary>
    public bool Focus(ISkUiAccessibleNode node, bool? ring = null)
    {
        if (!SkUiAccessibility.IsEnabled || !node.IsKeyboardFocusable || !IsLive(node))
            return false;
        // Asked every time rather than trusting the last report: platform focus events can arrive late (WinUI raises
        // GotFocus / LostFocus asynchronously). The request returns at once when the surface has focus already; the gain it
        // reports (OnNativeFocusChanged, no direction) focuses nothing itself.
        if (RequestNativeFocus is { } request)
        {
            if (!request())
                return false;
            _hasNativeFocus = true;
        }
        else if (!_hasNativeFocus)
        {
            return false;
        }
        SetFocused(node, ring ?? !_lastInputWasPointer);
        if (SkUiSemantics.BringIntoView(node) is { } bounds)
            RequestNativeReveal?.Invoke(bounds);
        return true;
    }

    /// <summary>Unfocuses <paramref name="node"/> when it is the focused node; the surface gives its platform focus up.</summary>
    public void Unfocus(ISkUiAccessibleNode node)
    {
        if (!ReferenceEquals(_focused, node))
            return;
        SetFocused(null, ring: false);
        ReleaseNativeFocus?.Invoke();
    }

    /// <summary>
    /// A key went down while the surface has keyboard focus; returns whether the drawn tree used it (otherwise the
    /// platform handles it, e.g. Tab past the last node moves to the next native control).
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">Modifier keys held.</param>
    /// <param name="isRepeat">A key held down repeating: it does not activate again (a held Enter would click many times).</param>
    public bool KeyDown(SkUiKey key, SkUiKeyModifiers modifiers = SkUiKeyModifiers.None, bool isRepeat = false)
    {
        if (!SkUiAccessibility.IsEnabled)
            return false;
        _lastInputWasPointer = false;
        Validate();
        if (_focused is not null && !_ringVisible && key is not SkUiKey.None)
            SetRingVisible(true);
        switch (key)
        {
            case SkUiKey.Tab when (modifiers & (SkUiKeyModifiers.Control | SkUiKeyModifiers.Alt | SkUiKeyModifiers.Command)) == 0:
                return MoveFocus(forward: (modifiers & SkUiKeyModifiers.Shift) == 0);
            case SkUiKey.Enter or SkUiKey.Space:
                // A repeat of a key that activated is used (not handed to the platform), without activating again.
                return _focused is not null && (isRepeat || Perform(_focused, SkUiSemanticsActions.Activate));
            case SkUiKey.Left or SkUiKey.Right or SkUiKey.Up or SkUiKey.Down:
                return Arrow(key);
            case SkUiKey.PageUp or SkUiKey.PageDown:
                return ScrollFocused(key == SkUiKey.PageDown, page: true);
            case SkUiKey.Home or SkUiKey.End:
                return HomeEnd(key == SkUiKey.End);
        }
        return false;
    }

    /// <summary>Moves focus to the next / previous node in tab order; <c>false</c> at either end (focus leaves the surface).</summary>
    public bool MoveFocus(bool forward)
    {
        var target = NextInOrder(forward);
        return target is not null && Focus(target, ring: true);
    }

    /// <summary>Whether Tab (<paramref name="forward"/>) or Shift+Tab would move the drawn focus rather than leave the surface.</summary>
    public bool CanMoveFocus(bool forward) => NextInOrder(forward) is not null;

    /// <summary>The node Tab / Shift+Tab moves to, or <c>null</c> past either end.</summary>
    private ISkUiAccessibleNode? NextInOrder(bool forward)
    {
        BuildOrder();
        var index = _focused is null ? -1 : _order.IndexOf(_focused);
        var next = index < 0 ? (forward ? 0 : _order.Count - 1) : index + (forward ? 1 : -1);
        var target = next >= 0 && next < _order.Count ? _order[next] : null;
        // The order is rebuilt each time: keeping it would keep removed nodes alive.
        _order.Clear();
        return target;
    }

    /// <summary>Focuses the first node in tab order (focus asked of a surface root that is not focusable itself).</summary>
    public bool FocusFirst()
    {
        BuildOrder();
        var first = _order.Count > 0 ? _order[0] : null;
        _order.Clear();
        return first is not null && Focus(first);
    }

    /// <summary>The focusable nodes in tab order (tests, diagnostics).</summary>
    internal IReadOnlyList<ISkUiAccessibleNode> GetTabOrder()
    {
        BuildOrder();
        ISkUiAccessibleNode[] order = [.. _order];
        _order.Clear();
        return order;
    }

    /// <summary>
    /// Clears focus from a node that left the surface or stopped being focusable (hidden, disabled, taken out of
    /// accessibility; or accessibility switched off app-wide).
    /// </summary>
    public void Validate()
    {
        if (_focused is { } focused && (!SkUiAccessibility.IsEnabled || !focused.IsKeyboardFocusable || !IsLive(focused)))
            SetFocused(null, ring: false);
    }

    /// <summary>The drawn tree changed (a subtree detached, a node hidden or disabled): every surface re-checks its focused node.</summary>
    internal static void ValidateAll()
    {
        for (var index = _holders.Count - 1; index >= 0; index--)
            LiveHolder(index)?.Validate();
    }

    /// <summary>The surface at <paramref name="index"/> while it still has a focused node; drops it from the list otherwise.</summary>
    private static SkUiFocusManager? LiveHolder(int index)
    {
        if (index < _holders.Count && _holders[index].TryGetTarget(out var manager) && manager._focused is not null)
            return manager;
        if (index < _holders.Count)
            _holders.RemoveAt(index);
        return null;
    }

    private void SetFocused(ISkUiAccessibleNode? node, bool ring)
    {
        var previous = _focused;
        if (ReferenceEquals(previous, node))
        {
            SetRingVisible(ring && node is not null);
            return;
        }
        _focused = node;
        _ringVisible = ring && node is not null;
        if (node is not null && previous is null)
            _holders.Add(new WeakReference<SkUiFocusManager>(this));
        previous?.SetKeyboardFocus(false, false);
        node?.SetKeyboardFocus(true, _ringVisible);
        SkUiSemantics.Invalidate(root);
        FocusChanged?.Invoke(node);
    }

    private void SetRingVisible(bool visible)
    {
        if (_ringVisible == visible || _focused is not { } focused)
            return;
        _ringVisible = visible;
        focused.SetKeyboardFocus(true, visible);
    }

    private static bool Perform(ISkUiAccessibleNode node, SkUiSemanticsActions action) => node.PerformSemanticsAction(action);

    private bool Arrow(SkUiKey key)
    {
        if (_focused is { } focused)
        {
            var info = _info;
            info.Reset();
            focused.GetSemantics(info);
            if (info.Range is not null && (info.Actions & (SkUiSemanticsActions.Increment | SkUiSemanticsActions.Decrement)) != 0)
            {
                // Up / Right raise the value; Left raises it in a right-to-left layout, as native sliders.
                var rightToLeft = focused is SkUiView { IsRightToLeft: true } || focused is Core.SkUiCoreNode { IsRightToLeft: true };
                var increase = key == SkUiKey.Up || (key == SkUiKey.Right) != rightToLeft;
                return focused.PerformSemanticsAction(increase ? SkUiSemanticsActions.Increment : SkUiSemanticsActions.Decrement);
            }
        }
        if (key is SkUiKey.Up or SkUiKey.Down)
            return ScrollFocused(key == SkUiKey.Down, page: false);
        return ScrollFocused(key == SkUiKey.Right, page: false, horizontal: true);
    }

    private bool HomeEnd(bool end)
    {
        if (_focused is { } focused)
        {
            var info = _info;
            info.Reset();
            focused.GetSemantics(info);
            if (info.Range is { } range && info.IsEnabled && (info.Actions & (SkUiSemanticsActions.Increment | SkUiSemanticsActions.Decrement)) != 0)
                return focused.SetSemanticsValue(end ? range.Maximum : range.Minimum);
        }
        if (FindScroller() is not { } scroller)
            return false;
        if (scroller.Stepper is { } stepper)
            return stepper.StepToEdge(end);
        var x = scroller.Horizontal && !scroller.Vertical ? (end ? scroller.MaxX : 0) : scroller.X;
        var y = scroller.Vertical ? (end ? scroller.MaxY : 0) : scroller.Y;
        if (x == scroller.X && y == scroller.Y)
            return false;
        _ = scroller.ScrollToAsync(x, y, animated: true);
        return true;
    }

    /// <summary>Scrolls the focused node's innermost scroller (else the surface's first one) by a line or a page.</summary>
    private bool ScrollFocused(bool forward, bool page, bool horizontal = false)
    {
        if (FindScroller(horizontal) is not { } scroller)
            return false;
        if (scroller.Stepper is { } stepper)
            return stepper.Step(forward, page);
        var useX = horizontal || (scroller.Horizontal && !scroller.Vertical);
        var extent = useX ? scroller.Viewport.Width : scroller.Viewport.Height;
        var step = (forward ? 1 : -1) * (page ? extent * 0.875 : LineStep);
        // Wheel deltas are positive towards the start.
        return scroller.Wheel(useX ? -step : 0, useX ? 0 : -step) != new Point(useX ? -step : 0, useX ? 0 : -step);
    }

    private SkUiScrollController? FindScroller(bool horizontal = false)
    {
        bool Fits(SkUiScrollController scroller) => horizontal ? scroller.Horizontal : scroller.Vertical || scroller.Horizontal;
        for (ISkUiRenderable? current = _focused; current is not null; current = current.RenderParent)
            if (current is ISkUiScrollHost host && Fits(host.Scroller))
                return host.Scroller;
        return _focused is null ? FirstScroller(root, horizontal) : null;
    }

    private SkUiScrollController? FirstScroller(ISkUiRenderable node, bool horizontal)
    {
        if (node is ISkUiScrollHost host && (horizontal ? host.Scroller.Horizontal : host.Scroller.MaxY > 0 || host.Scroller.MaxX > 0))
            return host.Scroller;
        var start = _scratch.Count;
        node.GetRenderChildren(_scratch);
        try
        {
            for (var index = start; index < _scratch.Count; index++)
                if (_scratch[index] is ISkUiInputNode { IsHitTestVisible: true } && FirstScroller(_scratch[index], horizontal) is { } found)
                    return found;
        }
        finally
        {
            _scratch.RemoveRange(start, _scratch.Count - start);
        }
        return null;
    }

    private static readonly Comparison<(int TabIndex, int Position, ISkUiAccessibleNode Node)> _byTabIndex =
        static (a, b) => a.TabIndex != b.TabIndex ? a.TabIndex.CompareTo(b.TabIndex) : a.Position.CompareTo(b.Position);

    private void BuildOrder()
    {
        _order.Clear();
        Collect(root);
        // Stable: equal tab indexes keep tree order (the position breaks ties; List.Sort itself is not stable).
        for (var index = 0; index < _order.Count; index++)
            _sorted.Add((_order[index].TabIndex, index, _order[index]));
        _sorted.Sort(_byTabIndex);
        for (var index = 0; index < _sorted.Count; index++)
            _order[index] = _sorted[index].Node;
        _sorted.Clear();
    }

    private void Collect(ISkUiRenderable node)
    {
        if (IsBlocked(node))
            return;
        if (node is ISkUiAccessibleNode accessible && accessible.IsKeyboardFocusable)
            _order.Add(accessible);
        var start = _scratch.Count;
        node.GetRenderChildren(_scratch);
        var end = _scratch.Count;
        try
        {
            for (var index = start; index < end; index++)
                Collect(_scratch[index]);
        }
        finally
        {
            _scratch.RemoveRange(start, _scratch.Count - start);
        }
    }

    /// <summary>Whether <paramref name="node"/> is under this surface's root, with no hidden, disabled or detached ancestor.</summary>
    private bool IsLive(ISkUiRenderable node)
    {
        for (ISkUiRenderable? current = node; current is not null; current = current.RenderParent)
        {
            if (IsBlocked(current))
                return false;
            if (ReferenceEquals(current, root))
                return true;
        }
        return false;
    }
}
