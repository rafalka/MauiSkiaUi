using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// Keeps one surface's semantics tree for its platform accessibility bridge (UI thread): rebuilt lazily after the drawn
/// tree changed (a committed frame, a scroll, a semantic property), and the differences reported to the bridge,
/// coalesced, while it listens. Created by the bridge when the platform asks for accessibility, so surfaces pay nothing
/// otherwise.
/// </summary>
internal sealed class SkUiSemanticsOwner
{
    private readonly SkUiView _root;
    private SkUiSemanticsTree? _tree;
    private SkUiSemanticsTree? _reported;
    private bool _dirty = true;
    private bool _scheduled;
    private Action<bool, IReadOnlyList<int>>? _changed;

    /// <summary>
    /// How long the drawn tree must stay unchanged before changes are reported (platforms throttle content changes too):
    /// while it keeps changing (a scroll, a fling, an animation) the tree is not rebuilt for every frame.
    /// </summary>
    internal static TimeSpan ReportDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>The longest a change waits to be reported while the tree keeps changing.</summary>
    internal static TimeSpan MaxReportDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    private long _firstChange;
    private long _lastChange;

    public SkUiSemanticsOwner(SkUiView root)
    {
        _root = root;
        root.SemanticsOwner = this;
    }

    /// <summary>The current tree (rebuilt first when the drawn tree changed).</summary>
    public SkUiSemanticsTree Tree
    {
        get
        {
            if (_dirty || _tree is null)
            {
                _tree = SkUiSemanticsTree.Build(_root, _root.FocusManagerIfCreated);
                _dirty = false;
                Version++;
            }
            return _tree;
        }
    }

    /// <summary>Increments with every rebuild: bridges compare it instead of keeping a tree (which holds its nodes).</summary>
    public int Version { get; private set; }

    /// <summary>
    /// Raised (UI thread, coalesced over <see cref="ReportDelay"/>) when the tree differs from the last one reported:
    /// whether elements were added, removed or reordered, and the ids whose content changed.
    /// </summary>
    public event Action<bool, IReadOnlyList<int>>? Changed
    {
        add
        {
            _changed += value;
            _reported ??= Tree;
        }
        remove
        {
            _changed -= value;
            // Nobody compares with it any more: a kept tree would keep removed nodes alive.
            if (_changed is null)
                _reported = null;
        }
    }

    /// <summary>Detaches from the root (the bridge went away).</summary>
    public void Detach()
    {
        if (ReferenceEquals(_root.SemanticsOwner, this))
            _root.SemanticsOwner = null;
        _changed = null;
        _tree = _reported = null;
    }

    /// <summary>The drawn tree changed: rebuild on the next read, and report the change when listened to.</summary>
    public void Invalidate()
    {
        _dirty = true;
        // Rebuilt on the next read; dropped now, so nodes removed from the drawn tree are not kept by it.
        _tree = null;
        if (_changed is null)
            return;
        _lastChange = Environment.TickCount64;
        if (_scheduled)
            return;
        // Without a dispatcher (headless) the change waits for the next read or Flush.
        if (Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread() is not { } dispatcher)
            return;
        _scheduled = true;
        _firstChange = _lastChange;
        dispatcher.DispatchDelayed(ReportDelay, OnReportDue);
    }

    /// <summary>Reports once the tree stopped changing for <see cref="ReportDelay"/>, or after <see cref="MaxReportDelay"/>.</summary>
    private void OnReportDue()
    {
        var now = Environment.TickCount64;
        var quiet = now - _lastChange;
        if (_changed is not null && quiet < ReportDelay.TotalMilliseconds && now - _firstChange < MaxReportDelay.TotalMilliseconds
            && Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread() is { } dispatcher)
        {
            dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(ReportDelay.TotalMilliseconds - quiet), OnReportDue);
            return;
        }
        Flush();
    }

    /// <summary>Reports what changed since the last report (normally run by <see cref="Invalidate"/>'s timer).</summary>
    internal void Flush()
    {
        _scheduled = false;
        if (_changed is not { } changed)
            return;
        var current = Tree;
        if (ReferenceEquals(current, _reported))
            return;
        var (structure, ids) = current.Diff(_reported);
        _reported = current;
        if (structure || ids.Count > 0)
            changed(structure, ids);
    }

    /// <summary>Performs <paramref name="action"/> on element <paramref name="id"/>; returns whether it was done.</summary>
    public bool Perform(int id, SkUiSemanticsActions action) =>
        Tree.Find(id) is { Source: { } source } node && (node.EnabledActions & action) != 0 && IsInputLive(source)
        && source.PerformSemanticsAction(action);

    /// <summary>Sets the range value of element <paramref name="id"/>; returns whether it was done.</summary>
    public bool SetValue(int id, double value) =>
        Tree.Find(id) is { Source: { } source, IsEnabled: true, Range: not null } && IsInputLive(source) && source.SetSemanticsValue(value);

    /// <summary>Whether no ancestor blocks input now (disabled), as for pointers and keys.</summary>
    private static bool IsInputLive(ISkUiRenderable node)
    {
        for (var current = node.RenderParent; current is not null; current = current.RenderParent)
            if (current is ISkUiInputNode { IsInputEnabled: false })
                return false;
        return true;
    }
}

/// <summary>Semantics plumbing shared by both layers.</summary>
internal static class SkUiSemantics
{
    /// <summary>The surface root above <paramref name="node"/> (the node itself at the top), or <c>null</c>.</summary>
    public static SkUiView? RootOf(ISkUiRenderable node)
    {
        var current = node;
        while (current.RenderParent is { } parent)
            current = parent;
        return current as SkUiView;
    }

    /// <summary>Something <paramref name="node"/> reports changed: its surface's tree is rebuilt (when one is kept).</summary>
    public static void Invalidate(ISkUiRenderable node)
    {
        var current = node;
        while (current.RenderParent is { } parent)
            current = parent;
        (current as SkUiView)?.SemanticsOwner?.Invalidate();
    }

    /// <summary>
    /// Moves the screen reader's focus to <paramref name="node"/> (MAUI's <c>SetSemanticFocus</c>) through its surface's
    /// bridge; nothing when no screen reader reads the surface.
    /// </summary>
    public static void SetSemanticFocus(ISkUiAccessibleNode node)
    {
        if (RootOf(node) is not { SemanticsOwner: { } owner } root)
            return;
        if (owner.Tree.Find(node) is { } element)
            root.SemanticFocusRequested?.Invoke(element.Id);
    }

    /// <summary>
    /// Scrolls every drawn scroller around <paramref name="node"/> so it shows (keyboard focus, screen reader focus),
    /// innermost first; only the outermost move animates, so the inner offsets are final when the outer ones are computed.
    /// Returns where the node ends up in its surface (DIPs, once the scrollers have moved), for native scrollers around the
    /// surface to show; <c>null</c> when it is not in a surface.
    /// </summary>
    public static Rect? BringIntoView(ISkUiRenderable node)
    {
        if (RootOf(node) is not { } root || SkUiScrollController.GetContentBounds(root, node) is not { } inRoot)
            return null;
        for (var current = node.RenderParent; current is not null; current = current.RenderParent)
        {
            if (current is not ISkUiScrollHost host)
                continue;
            var scroller = host.Scroller;
            if (SkUiScrollController.GetContentBounds(host, node) is not { } bounds)
                continue;
            var target = scroller.GetOffsetFor(bounds, ScrollToPosition.MakeVisible);
            var x = Math.Clamp(target.X, 0, scroller.MaxX);
            var y = Math.Clamp(target.Y, 0, scroller.MaxY);
            if (Math.Abs(x - scroller.X) < 0.5 && Math.Abs(y - scroller.Y) < 0.5)
                continue;
            // The node moves against the scroll; outer scrollers then measure it at its new place.
            inRoot = inRoot.Offset(scroller.X - x, scroller.Y - y);
            _ = scroller.ScrollToAsync(x, y, animated: !HasScrollHostAbove(current));
        }
        return inRoot;
    }

    private static bool HasScrollHostAbove(ISkUiRenderable node)
    {
        for (var current = node.RenderParent; current is not null; current = current.RenderParent)
            if (current is ISkUiScrollHost)
                return true;
        return false;
    }

    /// <summary>Scrolls a scroller one page (a screen reader's scroll action); returns whether it moved.</summary>
    public static bool ScrollPage(SkUiScrollController scroller, bool forward)
    {
        if (scroller.Stepper is { } stepper)
            return stepper.Step(forward, page: true);
        var (dx, dy) = PageDelta(scroller, forward);
        if (!scroller.CanScroll(dx, dy))
            return false;
        _ = scroller.ScrollToAsync(Math.Clamp(scroller.X + dx, 0, scroller.MaxX), Math.Clamp(scroller.Y + dy, 0, scroller.MaxY), animated: true);
        return true;
    }

    /// <summary>One page along the scroller's main axis (vertical when it scrolls both ways).</summary>
    private static (double X, double Y) PageDelta(SkUiScrollController scroller, bool forward)
    {
        var sign = forward ? 1 : -1;
        return scroller.Horizontal && !scroller.Vertical
            ? (sign * scroller.Viewport.Width * 0.8, 0)
            : (0, scroller.Vertical ? sign * scroller.Viewport.Height * 0.8 : 0);
    }

    /// <summary>One step of a range element's increment / decrement: 5 % of the range (as Android's seek bars).</summary>
    public static double RangeStep(double minimum, double maximum) => (maximum - minimum) / 20;

    /// <summary>The semantics of a scroller (role, page actions, axis).</summary>
    public static void PopulateScroller(SkUiSemanticsInfo info, SkUiScrollController scroller)
    {
        info.Role = SkUiSemanticsRole.ScrollView;
        info.IsHorizontal = scroller.Horizontal && !scroller.Vertical;
        if (scroller.Stepper is { } stepper)
        {
            if (stepper.CanStep(forward: true))
                info.Actions |= SkUiSemanticsActions.ScrollForward;
            if (stepper.CanStep(forward: false))
                info.Actions |= SkUiSemanticsActions.ScrollBackward;
            return;
        }
        // The axis the page actions move (ScrollPage), so an advertised action always scrolls.
        var (forwardX, forwardY) = PageDelta(scroller, forward: true);
        if (scroller.CanScroll(forwardX, forwardY))
            info.Actions |= SkUiSemanticsActions.ScrollForward;
        if (scroller.CanScroll(-forwardX, -forwardY))
            info.Actions |= SkUiSemanticsActions.ScrollBackward;
    }

    /// <summary>A scroller's page actions.</summary>
    public static bool PerformScroll(SkUiScrollController scroller, SkUiSemanticsActions action) => action switch
    {
        SkUiSemanticsActions.ScrollForward => ScrollPage(scroller, forward: true),
        SkUiSemanticsActions.ScrollBackward => ScrollPage(scroller, forward: false),
        _ => false
    };
}
