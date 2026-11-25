using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace MauiSkiaUi;

/// <summary>
/// Keeps <see cref="SkUiView.IsShown"/> up to date for views with <see cref="SkUiView.IsShownChanged"/> handlers.
/// Costs nothing while no view has handlers: the hooks (parent set, <c>IsVisible</c> change, surface attach) return
/// after reading one app-wide count, and views carry no fields for it. State lives in a side table only for watched
/// views and their drawn ancestors (which count the watched views below them, so a change walks only branches that
/// have handlers), and goes away when the handlers are removed.
/// </summary>
internal static class SkUiShownTracker
{
    private static readonly ConditionalWeakTable<SkUiView, State> _states = new();
    private static int _watchedViews; // views with handlers, app-wide

    private sealed class State
    {
        public EventHandler? Handlers;
        public bool IsShown;         // valid while Handlers is set
        public int Watchers;         // views with handlers in this subtree, this one included
        public SkUiView? WatchParent; // the drawn parent whose counts include this subtree

        private bool _watched;

        /// <summary>The view has handlers now, or no longer: it counts in <see cref="_watchedViews"/>.</summary>
        public void SetWatched(bool value)
        {
            if (_watched == value)
                return;
            _watched = value;
            if (value)
            {
                Interlocked.Increment(ref _watchedViews);
                GC.ReRegisterForFinalize(this);
            }
            else
            {
                Interlocked.Decrement(ref _watchedViews);
                GC.SuppressFinalize(this);
            }
        }

        public State() => GC.SuppressFinalize(this); // only a watched view's counts

        // A view dropped with its handlers (a page that closes does not remove them): it stops counting when it is collected,
        // else the checks above would never be free again.
        ~State() => SetWatched(false);
    }

    public static bool IsShown(SkUiView view) =>
        Volatile.Read(ref _watchedViews) > 0 && _states.TryGetValue(view, out var state) && state.Handlers is not null ? state.IsShown : Compute(view);

    public static void Add(SkUiView view, EventHandler? handler)
    {
        if (handler is null)
            return;
        var state = _states.GetOrCreateValue(view);
        var first = state.Handlers is null;
        state.Handlers += handler;
        if (!first)
            return;
        state.SetWatched(true);
        state.IsShown = Compute(view);
        AddWatchers(view, 1);
    }

    public static void Remove(SkUiView view, EventHandler? handler)
    {
        if (handler is null || !_states.TryGetValue(view, out var state) || state.Handlers is null)
            return;
        state.Handlers -= handler;
        if (state.Handlers is not null)
            return;
        AddWatchers(view, -1);
        state.SetWatched(false);
    }

    /// <summary>The view's parent changed: its subtree's watchers move to the new ancestors.</summary>
    public static void OnParentSet(SkUiView view)
    {
        if (Volatile.Read(ref _watchedViews) == 0 || !_states.TryGetValue(view, out var state) || state.Watchers == 0)
            return;
        var parent = view.SkiaParent;
        if (!ReferenceEquals(parent, state.WatchParent))
        {
            var watchers = state.Watchers;
            AddWatchers(state.WatchParent, -watchers);
            AddWatchers(parent, watchers);
            state.WatchParent = parent;
        }
        Refresh(view, state);
    }

    /// <summary>The view was shown or hidden.</summary>
    public static void OnVisibilityChanged(SkUiView view)
    {
        if (Volatile.Read(ref _watchedViews) > 0 && _states.TryGetValue(view, out var state) && state.Watchers > 0)
            Refresh(view, state);
    }

    /// <summary>A surface started or stopped drawing the tree <paramref name="root"/> is the top of.</summary>
    public static void OnSurfaceChanged(SkUiView root) => OnVisibilityChanged(root);

    /// <summary>Views with handlers in <paramref name="view"/>'s subtree (tests).</summary>
    internal static int Watchers(SkUiView view) => _states.TryGetValue(view, out var state) ? state.Watchers : 0;

    /// <summary>How many views count as having handlers (tests).</summary>
    internal static int WatchedViews => Volatile.Read(ref _watchedViews);

    /// <summary>Whether the tracker keeps state for <paramref name="view"/> (tests).</summary>
    internal static bool HasState(SkUiView view) => _states.TryGetValue(view, out _);

    private static bool Compute(SkUiView view)
    {
        var node = view;
        for (; node.SkiaParent is { } parent; node = parent)
            if (!node.IsVisible)
                return false;
        return node.IsVisible && node.HasLiveSurface;
    }

    private static void AddWatchers(SkUiView? from, int delta)
    {
        for (var node = from; node is not null; node = node.SkiaParent)
        {
            var state = _states.GetOrCreateValue(node);
            if (state.Watchers == 0)
                state.WatchParent = node.SkiaParent;
            state.Watchers += delta;
            if (state.Watchers == 0 && state.Handlers is null)
                _states.Remove(node);
        }
    }

    // A handler that throws does not stop the walk: every watched view of the branch is updated (deferred content of
    // its siblings still loads), then the first exception is rethrown.
    private static void Refresh(SkUiView view, State state)
    {
        ExceptionDispatchInfo? failure = null;
        Refresh(view, state, view.SkiaParent is { } parent ? Compute(parent) : view.HasLiveSurface, ref failure);
        failure?.Throw();
    }

    private static void Refresh(SkUiView node, State state, bool parentShown, ref ExceptionDispatchInfo? failure)
    {
        var shown = parentShown && node.IsVisible;
        if (state.Handlers is { } handlers && shown != state.IsShown)
        {
            state.IsShown = shown;
            foreach (EventHandler handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(node, EventArgs.Empty);
                }
                catch (Exception exception)
                {
                    failure ??= ExceptionDispatchInfo.Capture(exception);
                }
            }
        }
        foreach (var child in node.SkiaChildren.ToArray())
            if (child is SkUiView view && _states.TryGetValue(view, out var childState) && childState.Watchers > 0)
                Refresh(view, childState, shown, ref failure);
    }
}
