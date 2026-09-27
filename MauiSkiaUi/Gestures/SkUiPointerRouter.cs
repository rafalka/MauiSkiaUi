using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn node that takes part in hit-testing and gestures (<see cref="SkUiView"/> or a Core node).</summary>
internal interface ISkUiInputNode : ISkUiRenderable
{
    /// <summary><c>false</c>: skipped by hit-testing (invisible / input-transparent); pointers reach what is underneath.</summary>
    bool IsHitTestVisible { get; }

    /// <summary><c>false</c>: the node blocks pointers (disabled) — it is hit, but neither it nor its subtree reacts.</summary>
    bool IsInputEnabled { get; }

    /// <summary>Appends this node's active recognizers (innermost priority first); nothing when passive.</summary>
    void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers);
}

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

/// <summary>One pointer's competition between recognizers.</summary>
internal sealed class SkUiGestureArena(long pointerId, SkUiPointerRouter router, Point start)
{
    private bool _open = true;

    public long PointerId { get; } = pointerId;
    public Point Start { get; } = start;
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
    private readonly List<SkUiGestureRecognizer> _recognizers = [];
    private readonly List<ISkUiRenderable> _children = [];

    /// <summary>Arenas alive in the process (detached subtrees only need cancelling while any exist).</summary>
    internal static int ActiveArenas { get; private set; }

    /// <summary>Raised when <see cref="NativeState"/> may have changed (native parent coordination).</summary>
    internal event Action? NativeStateChanged;

    internal bool HasActiveArenas => _arenas.Count > 0;

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

    internal void OnArenaChanged() => NativeStateChanged?.Invoke();

    public bool Dispatch(SkUiTouchEvent touch)
    {
        var time = touch.Timestamp ?? SkUiGestureSettings.Now;
        switch (touch.Action)
        {
            case SkUiTouchAction.Wheel:
                return DispatchWheel(touch);
            case SkUiTouchAction.Pressed:
                return Press(touch, time);
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
        var pointer = new SkUiPointer(touch.Id, touch.Position, arena.Start, time, this);
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

    /// <summary>Cancels every pointer (surface detached, root disabled…).</summary>
    public void CancelAll()
    {
        foreach (var arena in _arenas.Values.ToArray())
            EndArena(arena, cancelled: true);
    }

    private bool Press(SkUiTouchEvent touch, TimeSpan time)
    {
        if (_arenas.TryGetValue(touch.Id, out var stale))
            EndArena(stale, cancelled: true);
        if (FindTarget(root, ToSk(touch.Position), isRoot: true, requireParticipant: true) is not { } leaf)
            return false;
        var arena = new SkUiGestureArena(touch.Id, this, touch.Position);
        if (!leaf.IsInputEnabled)
        {
            // Disabled: blocks the pointer until release without reacting.
            _arenas[touch.Id] = arena;
            ActiveArenas++;
            arena.Close();
            return true;
        }
        for (var node = leaf; node is not null; node = node.RenderParent as ISkUiInputNode)
        {
            _recognizers.Clear();
            node.CollectGestureRecognizers(_recognizers);
            foreach (var recognizer in _recognizers)
            {
                recognizer.Node = node;
                if (!arena.Members.Contains(recognizer))
                    arena.Add(recognizer);
            }
            if (ReferenceEquals(node, root))
                break;
        }
        _recognizers.Clear();
        _arenas[touch.Id] = arena;
        ActiveArenas++;
        var pointer = new SkUiPointer(touch.Id, touch.Position, touch.Position, time, this);
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
            if (current is ISkUiInputNode input && (!input.IsHitTestVisible || !input.IsInputEnabled))
                return false;
            if (ReferenceEquals(current, root))
                return true;
        }
        return false;
    }

    private bool DispatchWheel(SkUiTouchEvent touch)
    {
        for (var node = FindTarget(root, ToSk(touch.Position), isRoot: true, requireParticipant: false);
             node is not null; node = node.RenderParent as ISkUiInputNode)
        {
            if (!node.IsInputEnabled)
                return true;
            if (node is ISkUiScrollHost host && host.Scroller.Wheel(touch.WheelDelta))
                return true;
            if (ReferenceEquals(node, root))
                break;
        }
        return false;
    }

    /// <summary>
    /// Topmost node under <paramref name="local"/> that reacts (has recognizers) or blocks (disabled); passive nodes
    /// let the search continue underneath. Nodes are only hit inside their arranged rectangle (the root always).
    /// </summary>
    private ISkUiInputNode? FindTarget(ISkUiInputNode node, SKPoint local, bool isRoot, bool requireParticipant)
    {
        if (!node.IsHitTestVisible)
            return null;
        var props = GetProps(node);
        if (!isRoot && !props.Bounds.Contains(local.X, local.Y))
            return null;
        if (!node.IsInputEnabled)
            return node;
        var clip = props.ChildrenClipRect;
        if (clip.IsEmpty || clip.Contains(local.X, local.Y))
        {
            var start = _children.Count;
            node.GetRenderChildren(_children);
            var childrenPoint = new SKPoint(local.X + props.ChildrenOffsetX, local.Y + props.ChildrenOffsetY);
            try
            {
                for (var index = _children.Count - 1; index >= start; index--)
                {
                    if (_children[index] is not ISkUiInputNode child || !child.IsHitTestVisible)
                        continue;
                    if (!GetProps(child).Matrix.TryInvert(out var inverse))
                        continue;
                    if (FindTarget(child, inverse.MapPoint(childrenPoint), isRoot: false, requireParticipant) is { } hit)
                        return hit;
                }
            }
            finally
            {
                _children.RemoveRange(start, _children.Count - start);
            }
        }
        if (!requireParticipant)
            return node;
        _recognizers.Clear();
        node.CollectGestureRecognizers(_recognizers);
        var participates = _recognizers.Count > 0;
        _recognizers.Clear();
        return participates ? node : null;
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
            var parentProps = GetProps(parent);
            point = new SKPoint(point.X + parentProps.ChildrenOffsetX, point.Y + parentProps.ChildrenOffsetY);
            if (GetProps(chain[index]).Matrix.TryInvert(out var inverse))
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
