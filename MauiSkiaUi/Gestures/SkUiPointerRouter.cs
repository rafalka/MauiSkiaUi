using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn node that takes part in hit-testing and gestures (<see cref="SkUiView"/> or a Core node).</summary>
internal interface ISkUiInputNode : ISkUiRenderable
{
    /// <summary>
    /// <c>false</c>: the node and its subtree are skipped by hit-testing (invisible, or input-transparent with the transparency
    /// cascading to the subtree); pointers reach what is underneath.
    /// </summary>
    bool IsHitTestVisible { get; }

    /// <summary>
    /// <c>false</c>: pointers pass through the node itself while its subtree still takes them (input-transparent with
    /// <c>CascadeInputTransparent</c> off): it is never a target, its recognizers stay out of the arena, it is not hovered and
    /// it does not scroll. Only read while <see cref="IsHitTestVisible"/>.
    /// </summary>
    bool HitTestsSelf => true;

    /// <summary><c>false</c>: the node blocks pointers (disabled) — it is hit, but neither it nor its subtree reacts.</summary>
    bool IsInputEnabled { get; }

    /// <summary>
    /// <c>false</c>: pointers and keyboard focus do not reach the node's subtree (a shimmer showing its content as a skeleton);
    /// the node itself and its ancestors react as usual (a scroller around it scrolls).
    /// </summary>
    bool HitTestsChildren => true;

    /// <summary>Appends this node's active recognizers (innermost priority first); nothing when passive.</summary>
    void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers);

    /// <summary>A hovering pointer entered (<c>true</c>) or left this node or one of its descendants.</summary>
    void SetPointerOver(bool isOver) { }

    /// <summary>
    /// A hovering pointer moved over this node or one of its descendants, to <paramref name="position"/> in
    /// <paramref name="router"/>'s root coordinates; after <see cref="SetPointerOver"/>.
    /// </summary>
    void PointerHovered(SkUiPointerRouter router, Point position) { }

    /// <summary>The node's tooltip (<c>ToolTipProperties.Text</c>, a Core node's <c>ToolTip</c>); <c>null</c> or empty for none.</summary>
    string? ToolTipText => null;
}

/// <summary>A tooltip to show: its text and the bounds of the node it belongs to (the region it stays up over).</summary>
internal readonly record struct SkUiToolTipTarget(string Text, Rect Bounds);

/// <summary>What a surface's gestures mean for native ancestors (e.g. a MAUI ScrollView around the surface).</summary>
internal enum SkUiNativeGestureState
{
    /// <summary>Nothing drawn wants a continuous gesture: native ancestors may take the touch.</summary>
    None,
    /// <summary>A drawn continuous gesture may still claim (within the touch slop): hold native ancestors back.</summary>
    Pending,
    /// <summary>A drawn continuous gesture owns the touch.</summary>
    Claimed
}

/// <summary>
/// What a surface's drawn taps mean for native ancestor tap and long-press recognizers (e.g. a MAUI
/// <c>TapGestureRecognizer</c> on a parent of the surface), which cancel the surface's touches when they recognize.
/// </summary>
internal enum SkUiNativeTapState
{
    /// <summary>Nothing drawn takes a tap or long press of the touch: native ancestors may recognize.</summary>
    None,
    /// <summary>A drawn long press may still fire; released now, the touch is not taken (ancestors may recognize then).</summary>
    Holding,
    /// <summary>Released now, the touch completes a drawn tap: ancestors must not recognize.</summary>
    Tapping,
    /// <summary>A drawn gesture claimed the touch (a long press fired, a scroller caught a fling, a drag began).</summary>
    Claimed
}

/// <summary>One pointer's competition between recognizers.</summary>
internal sealed class SkUiGestureArena(long pointerId, SkUiPointerRouter router, Point start, SkUiPointerDevice device = SkUiPointerDevice.Touch)
{
    private bool _open = true;

    public long PointerId { get; } = pointerId;
    public Point Start { get; } = start;
    /// <summary>What produces the pointer's samples, as reported on its press.</summary>
    public SkUiPointerDevice Device { get; } = device;

    /// <summary>The buttons the pointer was pressed with.</summary>
    public SkUiPointerButtons Buttons { get; init; }
    public List<SkUiGestureRecognizer> Members { get; } = [];
    public SkUiGestureRecognizer? Winner { get; private set; }
    /// <summary>The winner claimed explicitly (its gesture started) rather than winning by default.</summary>
    public bool WinnerClaimed { get; private set; }
    public bool SlopExceeded { get; set; }
    /// <summary>A member was cancelled from outside (detached / disabled element).</summary>
    public bool Invalidated { get; set; }

    public void Add(SkUiGestureRecognizer recognizer)
    {
        Members.Add(recognizer);
        recognizer.JoinArena(this);
    }

    /// <summary>Press dispatch finished: a sole member wins by default.</summary>
    public void Close()
    {
        _open = false;
        if (Winner is null && Members.Count == 1)
            Resolve(Members[0], explicitClaim: false);
    }

    public void Resolve(SkUiGestureRecognizer recognizer, bool explicitClaim)
    {
        if (Winner is not null)
        {
            if (ReferenceEquals(Winner, recognizer) && explicitClaim && !WinnerClaimed)
            {
                WinnerClaimed = true;
                router.OnArenaChanged();
            }
            return;
        }
        if (!Members.Contains(recognizer))
            return;
        Winner = recognizer;
        WinnerClaimed = explicitClaim;
        foreach (var member in Members.ToArray())
            if (!ReferenceEquals(member, recognizer))
                Remove(member, rejected: true);
        recognizer.OnAccepted(PointerId);
        router.OnArenaChanged();
    }

    public void Remove(SkUiGestureRecognizer recognizer, bool rejected)
    {
        if (!Members.Remove(recognizer))
            return;
        recognizer.LeaveArena(this);
        if (ReferenceEquals(Winner, recognizer))
            Winner = null;
        if (rejected)
            recognizer.OnRejected(PointerId);
        if (!_open && Winner is null && Members.Count == 1)
            Resolve(Members[0], explicitClaim: false);
        router.OnArenaChanged();
    }

    /// <summary>Release: an undecided arena is won by the innermost remaining member.</summary>
    public void Sweep()
    {
        if (Winner is null && Members.Count > 0)
            Resolve(Members[0], explicitClaim: false);
    }

    /// <summary>Ends the arena: members leave silently (released) or are rejected (cancelled).</summary>
    public void End(bool cancelled)
    {
        foreach (var member in Members.ToArray())
        {
            Members.Remove(member);
            member.LeaveArena(this);
            if (cancelled)
                member.OnRejected(PointerId);
        }
        Winner = null;
    }
}

/// <summary>
/// Routes a surface root's pointer samples (<see cref="ISkUiView.Touch"/>): hit-tests once per press through the
/// drawn tree (SkUi* and Core alike), runs one <see cref="SkUiGestureArena"/> per pointer, and routes wheel deltas to
/// the innermost scroller that can scroll. UI thread only.
/// </summary>
internal sealed class SkUiPointerRouter(ISkUiInputNode root)
{
    private readonly Dictionary<long, SkUiGestureArena> _arenas = [];
    /// <summary>The hovered node and its ancestors up to the root, innermost first.</summary>
    private readonly List<ISkUiInputNode> _hovered = [];
    private readonly List<ISkUiInputNode> _hoverChain = [];
    private ISkUiInputNode? _hoveredToolTipNode;
    private string? _hoveredToolTipText;
    private Action? _hoveredToolTipChanged;
    private bool _watchesToolTips;

    /// <summary>
    /// Routers of this UI thread with a hover a platform follows (<see cref="HoveredToolTipChanged"/> subscribed): a tooltip set
    /// while the pointer rests re-checks only them. Usually none or one.
    /// </summary>
    [ThreadStatic] private static List<SkUiPointerRouter>? _toolTipWatchers;
    private readonly List<SkUiGestureRecognizer> _recognizers = [];
    private readonly List<ISkUiRenderable> _children = [];

    /// <summary>Arenas alive in the process (detached subtrees only need cancelling while any exist).</summary>
    internal static int ActiveArenas { get; private set; }

    /// <summary>Raised when <see cref="NativeState"/> may have changed (native parent coordination).</summary>
    internal event Action? NativeStateChanged;

    internal bool HasActiveArenas => _arenas.Count > 0;

    /// <summary>Per-pointer arenas still open (diagnostics: a stuck one means a pointer never ended).</summary>
    internal int ActiveArenaCount => _arenas.Count;

    /// <summary>Current meaning of the drawn gestures for native ancestors.</summary>
    internal SkUiNativeGestureState NativeState
    {
        get
        {
            var state = SkUiNativeGestureState.None;
            foreach (var arena in _arenas.Values)
            {
                if (arena.Winner is { IsExclusive: true } && arena.WinnerClaimed)
                    return SkUiNativeGestureState.Claimed;
                if (!arena.SlopExceeded && arena.Members.Exists(member => member.IsExclusive))
                    state = SkUiNativeGestureState.Pending;
            }
            return state;
        }
    }

    /// <summary>
    /// <see cref="NativeState"/> of one pointer: <c>Claimed</c> once a continuous drawn gesture took it, <c>Pending</c>
    /// while one still may (within the slop), otherwise <c>None</c> (also when the pointer is unknown or ended).
    /// </summary>
    internal SkUiNativeGestureState StateOf(long pointerId)
    {
        if (!_arenas.TryGetValue(pointerId, out var arena))
            return SkUiNativeGestureState.None;
        if (arena.Winner is { IsExclusive: true } && arena.WinnerClaimed)
            return SkUiNativeGestureState.Claimed;
        return !arena.SlopExceeded && arena.Members.Exists(member => member.IsExclusive)
            ? SkUiNativeGestureState.Pending
            : SkUiNativeGestureState.None;
    }

    /// <summary>
    /// Current meaning of the drawn taps for native ancestor tap / long-press recognizers, over all pointers (the
    /// strongest wins: <c>Claimed</c>, then <c>Tapping</c>, then <c>Holding</c>).
    /// </summary>
    internal SkUiNativeTapState NativeTapState
    {
        get
        {
            var state = SkUiNativeTapState.None;
            foreach (var arena in _arenas.Values)
            {
                var arenaState = TapStateOf(arena);
                if (arenaState > state)
                    state = arenaState;
            }
            return state;
        }
    }

    private static SkUiNativeTapState TapStateOf(SkUiGestureArena arena)
    {
        if (arena.Winner is { } winner && arena.WinnerClaimed && (winner.IsExclusive || winner.HoldsNativeTaps))
            return SkUiNativeTapState.Claimed;
        var state = SkUiNativeTapState.None;
        foreach (var member in arena.Members)
        {
            if (member.IsDiscrete)
                return SkUiNativeTapState.Tapping;
            if (member.HoldsNativeTaps)
                state = SkUiNativeTapState.Holding;
        }
        return state;
    }

    internal void OnArenaChanged() => NativeStateChanged?.Invoke();

    public bool Dispatch(SkUiTouchEvent touch) => Dispatch(touch, overlay: null);

    /// <summary>
    /// A pointer that started on a native overlay (<see cref="SkUiMauiContentView"/>): only the <b>continuous</b>
    /// recognizers (scroll, pan, swipe, pinch) of the overlay's drawn ancestors compete, so a drag can scroll the drawn
    /// content under the native control while taps, text selection and cursor placement stay native. The platform
    /// cancels the native touch once <see cref="NativeState"/> reports <see cref="SkUiNativeGestureState.Claimed"/>.
    /// </summary>
    public bool DispatchFromOverlay(SkUiTouchEvent touch, ISkUiInputNode overlay) => Dispatch(touch, overlay);

    private bool Dispatch(SkUiTouchEvent touch, ISkUiInputNode? overlay)
    {
        var time = touch.Timestamp ?? SkUiGestureSettings.Now;
        switch (touch.Action)
        {
            case SkUiTouchAction.Wheel:
                return DispatchWheel(touch);
            case SkUiTouchAction.HoverMoved:
                Hover(touch.Position);
                return false;
            case SkUiTouchAction.HoverExited:
                ClearHover();
                return false;
            case SkUiTouchAction.Pressed:
                SkUiFocusManager.NotePointerInput();
                return overlay is null ? Press(touch, time) : PressFromOverlay(touch, time, overlay);
        }
        if (!_arenas.TryGetValue(touch.Id, out var arena))
            return false;
        // The pointer stays taken until release (even after every member resigned), unless the elements that took it
        // left this root or became disabled / hidden.
        var handled = !((Validate(arena) | arena.Invalidated) && arena.Members.Count == 0);
        if (touch.Action == SkUiTouchAction.Cancelled)
        {
            EndArena(arena, cancelled: true);
            return handled;
        }
        var pointer = new SkUiPointer(touch.Id, touch.Position, arena.Start, time, this, arena.Device, arena.Buttons);
        var slop = SkUiGestureSettings.TouchSlop;
        if (!arena.SlopExceeded && (Math.Pow(pointer.TotalX, 2) + Math.Pow(pointer.TotalY, 2)) > slop * slop)
        {
            arena.SlopExceeded = true;
            OnArenaChanged();
        }
        foreach (var member in arena.Members.ToArray())
        {
            if (!arena.Members.Contains(member))
                continue;
            if (touch.Action == SkUiTouchAction.Moved)
                member.OnPointerMoved(pointer);
            else
                member.OnPointerReleased(pointer);
        }
        if (touch.Action == SkUiTouchAction.Released)
        {
            arena.Sweep();
            EndArena(arena, cancelled: false);
        }
        return handled;
    }

    /// <summary>Cancels every pointer (surface detached, root disabled…) and clears the hover.</summary>
    public void CancelAll()
    {
        foreach (var arena in _arenas.Values.ToArray())
            EndArena(arena, cancelled: true);
        ClearHover();
    }

    /// <summary>
    /// A hovering pointer at <paramref name="position"/> (root coordinates): the topmost hit-test-visible node there and
    /// its ancestors are pointer-over, as with native hover (a parent stays hovered while the pointer is over a child);
    /// nodes that left the chain are told first. Passive nodes count; a disabled node ends the search like a press does.
    /// </summary>
    private void Hover(Point position)
    {
        _hoverChain.Clear();
        for (var node = FindTarget(root, ToSk(position), isRoot: true, requireParticipant: false);
             node is not null; node = node.RenderParent as ISkUiInputNode)
        {
            if (node.HitTestsSelf)
                _hoverChain.Add(node);
            if (ReferenceEquals(node, root))
                break;
        }
        foreach (var node in _hovered)
            if (!_hoverChain.Contains(node))
                node.SetPointerOver(false);
        // Outermost first, as native hover enters a parent before its child. Unconditional (SetPointerOver ignores an
        // unchanged value): a node detached while hovered cleared its own flag, and may be back under the pointer.
        for (var index = _hoverChain.Count - 1; index >= 0; index--)
            _hoverChain[index].SetPointerOver(true);
        for (var index = _hoverChain.Count - 1; index >= 0; index--)
            _hoverChain[index].PointerHovered(this, position);
        _hovered.Clear();
        _hovered.AddRange(_hoverChain);
        _hoverChain.Clear();
        TrackHoveredToolTip();
    }

    /// <summary>Nothing is pointer-over any more (the pointer left, or the surface went away).</summary>
    internal void ClearHover()
    {
        if (_hovered.Count == 0)
        {
            TrackHoveredToolTip();
            return;
        }
        var left = _hovered.ToArray();
        _hovered.Clear();
        TrackHoveredToolTip();
        foreach (var node in left)
            node.SetPointerOver(false);
    }

    /// <summary>
    /// The hovered tooltip changed: another node shows one (or none does), its text changed under a resting pointer
    /// (<see cref="OnToolTipChanged"/>), or a press closed it and the pointer moved again (<see cref="ResetHoveredToolTip"/>).
    /// Platforms that time tooltips themselves (Windows) follow it.
    /// </summary>
    internal event Action? HoveredToolTipChanged
    {
        add
        {
            _hoveredToolTipChanged += value;
            TrackHoveredToolTip();
        }
        remove
        {
            _hoveredToolTipChanged -= value;
            TrackHoveredToolTip();
        }
    }

    /// <summary>
    /// The tooltip of the hovered node (or its nearest ancestor with one), read now; <c>null</c> for none, or when that node
    /// left the tree under the pointer.
    /// </summary>
    internal SkUiToolTipTarget? HoveredToolTip =>
        _hoveredToolTipNode is { } node ? ToolTipOf(node) : null;

    /// <summary>
    /// A press closed the tooltip: the next hover move reports the hovered tooltip as new, so it opens again after the delay
    /// (the pointer moving on, as a native view's tooltip shows again).
    /// </summary>
    internal void ResetHoveredToolTip()
    {
        _hoveredToolTipNode = null;
        _hoveredToolTipText = null;
    }

    /// <summary>A node's tooltip text changed (UI thread): routers with a followed hover re-check theirs.</summary>
    internal static void OnToolTipChanged()
    {
        if (_toolTipWatchers is not { Count: > 0 } watchers)
            return;
        // Backwards: a router that stops watching removes itself.
        for (var index = watchers.Count - 1; index >= 0; index--)
            watchers[index].TrackHoveredToolTip();
    }

    /// <summary>
    /// The tooltip at <paramref name="position"/> (root coordinates): of the topmost hit-test-visible node there, or of its
    /// nearest ancestor with one, as hover finds them; for platforms that ask by position (Apple's tooltip interaction).
    /// </summary>
    internal SkUiToolTipTarget? ToolTipAt(Point position)
    {
        for (var node = FindTarget(root, ToSk(position), isRoot: true, requireParticipant: false);
             node is not null; node = node.RenderParent as ISkUiInputNode)
        {
            if (node.HitTestsSelf && ToolTipOf(node) is { } target)
                return target;
            if (ReferenceEquals(node, root))
                break;
        }
        return null;
    }

    private static ISkUiInputNode? FindToolTipNode(List<ISkUiInputNode> chain)
    {
        foreach (var node in chain)
            if (!string.IsNullOrEmpty(node.ToolTipText))
                return node;
        return null;
    }

    /// <summary>Finds the hovered tooltip (node and text) and raises <see cref="HoveredToolTipChanged"/> when it changed.</summary>
    private void TrackHoveredToolTip()
    {
        var watches = _hoveredToolTipChanged is not null && _hovered.Count > 0;
        if (watches != _watchesToolTips)
        {
            _watchesToolTips = watches;
            if (watches)
                (_toolTipWatchers ??= []).Add(this);
            else
                _toolTipWatchers?.Remove(this);
        }
        var node = FindToolTipNode(_hovered);
        var text = node?.ToolTipText;
        // Exact comparisons: whether anything changed, not a measure.
        if (ReferenceEquals(_hoveredToolTipNode, node) && _hoveredToolTipText == text)
            return;
        _hoveredToolTipNode = node;
        _hoveredToolTipText = text;
        _hoveredToolTipChanged?.Invoke();
    }

    private SkUiToolTipTarget? ToolTipOf(ISkUiInputNode node) =>
        node.ToolTipText is { Length: > 0 } text && MapToRoot(node) is { } bounds ? new SkUiToolTipTarget(text, bounds) : null;

    /// <summary>
    /// The bounds of <paramref name="node"/> in root coordinates (through transforms and scroll offsets); <c>null</c> when it is
    /// not under this root (it left the tree).
    /// </summary>
    internal Rect? MapToRoot(ISkUiRenderable node)
    {
        if (!TryGetToRootMatrix(node, out var toRoot))
            return null;
        var bounds = toRoot.MapRect(GetProps(node).Bounds);
        return new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }

    /// <summary>
    /// The matrix from <paramref name="node"/>'s coordinates to the root's, built from the root down (recursive: no list per
    /// call, the depth is the tree's); <c>false</c> when the node is not under this root.
    /// </summary>
    private bool TryGetToRootMatrix(ISkUiRenderable node, out SKMatrix toRoot)
    {
        if (ReferenceEquals(node, root))
        {
            toRoot = SKMatrix.Identity;
            return true;
        }
        if (node.RenderParent is not { } parent || !TryGetToRootMatrix(parent, out toRoot))
        {
            toRoot = default;
            return false;
        }
        var props = GetProps(node);
        if (!props.Pinned)
            toRoot = toRoot.PreConcat(GetProps(parent).ChildrenMatrix);
        toRoot = toRoot.PreConcat(props.Matrix);
        return true;
    }

    private bool Press(SkUiTouchEvent touch, TimeSpan time)
    {
        if (_arenas.TryGetValue(touch.Id, out var stale))
            EndArena(stale, cancelled: true);
        if ((FocusedTextHandleAt(touch) ?? FindTarget(root, ToSk(touch.Position), isRoot: true, requireParticipant: true)) is not { } leaf)
            return false;
        if (root is SkUiView { FocusManagerIfCreated: { PointerPressFocuses: true } focus })
            focus.OnPointerPressed(leaf);
        var arena = new SkUiGestureArena(touch.Id, this, touch.Position, touch.Device) { Buttons = touch.Buttons };
        if (!leaf.IsInputEnabled)
        {
            // Disabled: takes the pointer until release without reacting, so what is underneath gets no tap; the continuous
            // gestures of its ancestors still compete, so a scroller around it scrolls. FindTarget stops at the outermost node
            // whose input is disabled, so every ancestor here is enabled: a disabled scroller is the leaf, never in this arena.
            if (!ReferenceEquals(leaf, root) && leaf.RenderParent is ISkUiInputNode parent)
            {
                AddMembers(arena, parent, continuousOnly: true);
                if (arena.Members.Count > 0 && Open(arena, touch, time))
                    return true;
                arena = new SkUiGestureArena(touch.Id, this, touch.Position, touch.Device) { Buttons = touch.Buttons };
            }
            _arenas[touch.Id] = arena;
            ActiveArenas++;
            arena.Close();
            return true;
        }
        AddMembers(arena, leaf, continuousOnly: false);
        return Open(arena, touch, time);
    }

    /// <summary>
    /// The focused text field when a finger presses one of its handles: they hang outside the field (the insertion handle under
    /// the caret), where the hit test, bounded by each node's rectangle, would not find it. Only where they are drawn: inside the
    /// clips of the field's ancestors (a scroller's viewport hides a handle hanging past it).
    /// </summary>
    private ISkUiInputNode? FocusedTextHandleAt(SkUiTouchEvent touch)
    {
        if (touch.Device != SkUiPointerDevice.Touch || root is not SkUiView { FocusManagerIfCreated.Focused: ISkUiTextInputNode field }
            || field is not ISkUiInputNode node || !node.IsHitTestVisible || !node.HitTestsSelf || !node.IsInputEnabled || !IsLive(node))
            return null;
        return MapInsideClips(node, touch.Position) is { } local && field.HitsTextHandle(local) ? node : null;
    }

    private readonly List<ISkUiRenderable> _chain = [];

    /// <summary>
    /// <paramref name="rootPoint"/> in <paramref name="node"/>'s local coordinates (as <see cref="MapToNode"/>), or <c>null</c> when an
    /// ancestor clips it away: outside a children clip (a scroller's viewport) or the bounds of an ancestor that clips to them.
    /// </summary>
    private Point? MapInsideClips(ISkUiRenderable node, Point rootPoint)
    {
        _chain.Clear();
        for (ISkUiRenderable? current = node; current is not null && !ReferenceEquals(current, root); current = current.RenderParent)
            _chain.Add(current);
        try
        {
            if (_chain.Count == 0 || _chain[^1].RenderParent is null)
                return ReferenceEquals(node, root) ? rootPoint : null;
            var point = ToSk(rootPoint);
            var parentProps = GetProps(root);
            for (var index = _chain.Count - 1; index >= 0; index--)
            {
                var props = GetProps(_chain[index]);
                if (!props.Pinned)
                {
                    if (!parentProps.ChildrenClipRect.IsEmpty && !parentProps.ChildrenClipRect.Contains(point.X, point.Y))
                        return null;
                    point = parentProps.MapToChildren(point);
                }
                if (props.Matrix.TryInvert(out var inverse))
                    point = inverse.MapPoint(point);
                // The node itself draws its handles past its bounds; its ancestors' clips hold.
                if (index > 0 && props.ClipToBounds && !props.Bounds.Contains(point.X, point.Y))
                    return null;
                parentProps = props;
            }
            return new Point(point.X, point.Y);
        }
        finally
        {
            _chain.Clear();
        }
    }

    private bool PressFromOverlay(SkUiTouchEvent touch, TimeSpan time, ISkUiInputNode overlay)
    {
        if (_arenas.TryGetValue(touch.Id, out var stale))
            EndArena(stale, cancelled: true);
        if (overlay.RenderParent is not ISkUiInputNode parent || !IsLive(overlay))
            return false;
        var arena = new SkUiGestureArena(touch.Id, this, touch.Position, touch.Device) { Buttons = touch.Buttons };
        AddMembers(arena, parent, continuousOnly: true);
        return Open(arena, touch, time);
    }

    private void AddMembers(SkUiGestureArena arena, ISkUiInputNode leaf, bool continuousOnly)
    {
        for (var node = leaf; node is not null; node = node.RenderParent as ISkUiInputNode)
        {
            _recognizers.Clear();
            if (node.HitTestsSelf)
                node.CollectGestureRecognizers(_recognizers);
            foreach (var recognizer in _recognizers)
            {
                if (continuousOnly && !recognizer.IsExclusive)
                    continue;
                recognizer.Node = node;
                if (!arena.Members.Contains(recognizer))
                    arena.Add(recognizer);
            }
            if (ReferenceEquals(node, root))
                break;
        }
        _recognizers.Clear();
    }

    private bool Open(SkUiGestureArena arena, SkUiTouchEvent touch, TimeSpan time)
    {
        _arenas[touch.Id] = arena;
        ActiveArenas++;
        var pointer = new SkUiPointer(touch.Id, touch.Position, touch.Position, time, this, arena.Device, arena.Buttons);
        foreach (var member in arena.Members.ToArray())
            if (arena.Members.Contains(member) && !member.OnPointerPressed(pointer))
                arena.Remove(member, rejected: false);
        if (arena.Members.Count == 0 && arena.Winner is null)
        {
            _arenas.Remove(touch.Id);
            ActiveArenas--;
            return false;
        }
        arena.Close();
        OnArenaChanged();
        return true;
    }

    private void EndArena(SkUiGestureArena arena, bool cancelled)
    {
        if (_arenas.Remove(arena.PointerId))
            ActiveArenas--;
        arena.End(cancelled);
        OnArenaChanged();
    }

    /// <summary>Rejects members whose element left this root or became hidden, input-transparent or disabled; returns whether any did.</summary>
    private bool Validate(SkUiGestureArena arena)
    {
        var removed = false;
        foreach (var member in arena.Members.ToArray())
            if (member.Node is not { } node || !IsLive(node))
            {
                arena.Remove(member, rejected: true);
                removed = true;
            }
        return removed;
    }

    private bool IsLive(ISkUiInputNode node)
    {
        for (ISkUiRenderable? current = node; current is not null; current = current.RenderParent)
        {
            if (current is ISkUiInputNode input && (!input.IsHitTestVisible || !input.IsInputEnabled
                    || (ReferenceEquals(current, node) ? !input.HitTestsSelf : !input.HitTestsChildren)))
                return false;
            if (ReferenceEquals(current, root))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Wheel and trackpad scrolling, from the innermost scroller under the pointer outwards: each scroller uses the axes it
    /// can move that way, and the rest goes on to the outer ones.
    /// </summary>
    private bool DispatchWheel(SkUiTouchEvent touch)
    {
        var rest = new Point(touch.WheelDeltaX, touch.WheelDelta);
        for (var node = FindTarget(root, ToSk(touch.Position), isRoot: true, requireParticipant: false);
             node is not null; node = node.RenderParent as ISkUiInputNode)
        {
            if (!node.IsInputEnabled)
                return true;
            // A node that passes presses through passes the wheel on too.
            if (node.HitTestsSelf && ((node as ISkUiScrollHost)?.Scroller ?? (node as ISkUiWheelProxy)?.WheelScroller) is { } scroller)
            {
                rest = scroller.Wheel(rest.X, rest.Y);
                if (rest.X == 0 && rest.Y == 0)
                    return true;
            }
            else if (node is ISkUiWheelHandler { HitTestsSelf: true } handler)
            {
                rest = handler.OnWheel(rest.X, rest.Y);
                if (rest.X == 0 && rest.Y == 0)
                    return true;
            }
            if (ReferenceEquals(node, root))
                break;
        }
        return rest.X != touch.WheelDeltaX || rest.Y != touch.WheelDelta;
    }

    /// <summary>
    /// Set by the platform handler: whether a native ancestor of the surface (a MAUI ScrollView around it) can scroll by an
    /// offset delta (positive: towards the end). A drawn scroller at its edge then leaves the drag to it instead of
    /// overscrolling.
    /// </summary>
    internal Func<double, double, bool>? NativeAncestorCanScroll { get; set; }

    /// <summary>
    /// Topmost node under <paramref name="local"/> that reacts (has recognizers) or blocks (disabled); passive nodes
    /// let the search continue underneath. Nodes are only hit inside their arranged rectangle (the root always), and a text field
    /// also on the message it hangs below a fixed height (<see cref="ISkUiTextInputNode.HitsOverflowMessage"/>). A node that
    /// does not hit-test itself (<see cref="ISkUiInputNode.HitTestsSelf"/>) is searched for children only.
    /// </summary>
    private ISkUiInputNode? FindTarget(ISkUiInputNode node, SKPoint local, bool isRoot, bool requireParticipant)
    {
        if (!node.IsHitTestVisible)
            return null;
        var props = GetProps(node);
        if (!isRoot && !props.Bounds.Contains(local.X, local.Y)
            && !(node is ISkUiTextInputNode field && field.HitsOverflowMessage(new Point(local.X, local.Y))))
            return null;
        var hitTestsSelf = node.HitTestsSelf;
        if (!node.IsInputEnabled && hitTestsSelf)
            return node;
        var clip = props.ChildrenClipRect;
        var insideClip = clip.IsEmpty || clip.Contains(local.X, local.Y);
        // Pinned children, then (inside the clip) the others; none when the node hides its subtree from pointers.
        var passes = !node.HitTestsChildren ? 0 : insideClip ? 2 : 1;
        var start = _children.Count;
        node.GetRenderChildren(_children);
        var childrenPoint = props.MapToChildren(local);
        try
        {
            // Pinned children draw above the others, in local coordinates and outside the children clip: they are hit
            // first; the others only inside the clip.
            for (var pass = 0; pass < passes; pass++)
                for (var index = _children.Count - 1; index >= start; index--)
                {
                    if (_children[index] is not ISkUiInputNode child || !child.IsHitTestVisible)
                        continue;
                    var childProps = GetProps(child);
                    if (childProps.Pinned != (pass == 0) || !childProps.Matrix.TryInvert(out var inverse))
                        continue;
                    if (FindTarget(child, inverse.MapPoint(childProps.Pinned ? local : childrenPoint), isRoot: false, requireParticipant) is { } hit)
                        return hit;
                }
        }
        finally
        {
            _children.RemoveRange(start, _children.Count - start);
        }
        if (!hitTestsSelf)
            return null;
        if (!requireParticipant)
            return node;
        _recognizers.Clear();
        node.CollectGestureRecognizers(_recognizers);
        var participates = _recognizers.Count > 0;
        _recognizers.Clear();
        return participates ? node : null;
    }

    /// <summary>Whether <paramref name="element"/> is this router's root or drawn below it.</summary>
    internal bool Contains(object element)
    {
        for (var current = element as ISkUiRenderable; current is not null; current = current.RenderParent)
            if (ReferenceEquals(current, root))
                return true;
        return false;
    }

    /// <summary>Maps a root point into <paramref name="element"/>'s local coordinates (through transforms and scroll offsets).</summary>
    internal Point MapToNode(object element, Point rootPoint)
    {
        if (element is not ISkUiRenderable node || ReferenceEquals(node, root))
            return rootPoint;
        var chain = new List<ISkUiRenderable>(8);
        for (ISkUiRenderable? current = node; current is not null && !ReferenceEquals(current, root); current = current.RenderParent)
            chain.Add(current);
        if (chain[^1].RenderParent is null)
            return rootPoint; // not under this root
        var point = ToSk(rootPoint);
        ISkUiRenderable parent = root;
        for (var index = chain.Count - 1; index >= 0; index--)
        {
            var props = GetProps(chain[index]);
            if (!props.Pinned)
                point = GetProps(parent).MapToChildren(point);
            if (props.Matrix.TryInvert(out var inverse))
                point = inverse.MapPoint(point);
            parent = chain[index];
        }
        return new Point(point.X, point.Y);
    }

    internal static Size GetSize(ISkUiRenderable node)
    {
        var props = GetProps(node);
        return new Size(props.Width, props.Height);
    }

    private static SkUiRenderProps GetProps(ISkUiRenderable node)
    {
        var props = SkUiRenderProps.Default;
        node.GetRenderProps(ref props);
        return props;
    }

    private static SKPoint ToSk(Point point) => new((float)point.X, (float)point.Y);
}
