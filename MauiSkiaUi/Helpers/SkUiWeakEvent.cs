using System.Runtime.CompilerServices;

namespace MauiSkiaUi;

/// <summary>
/// The subscriber list of an app-wide event (<c>SkUiLook.CurrentChanged</c>, <c>SkUiColorScheme.CurrentChanged</c>, a
/// scheme's <c>Changed</c>) that does not keep its subscribers alive: a page that subscribes and never unsubscribes can
/// still be collected. A handler on an object (a method group, or a lambda that uses only <c>this</c>) is held as long
/// as that object lives; static handlers and lambdas that capture locals are held strongly, because nothing else
/// references a compiler-generated closure and a weak one would stop firing at the next collection. Handlers run
/// directly, without reflection.
/// </summary>
internal sealed class SkUiWeakEvent
{
    private readonly List<Entry> _entries = [];
    // Keeps each weakly held handler alive exactly as long as its target (the table's key) lives.
    private readonly ConditionalWeakTable<object, List<EventHandler>> _handlers = new();
    private int _pruneAt = 8;

    /// <summary>A subscription: a strong handler, or a weak reference to one kept alive by its target.</summary>
    private sealed class Entry(EventHandler? strong, WeakReference<EventHandler>? weak)
    {
        public EventHandler? Strong { get; } = strong;
        public WeakReference<EventHandler>? Weak { get; } = weak;

        public EventHandler? Handler => Strong ?? (Weak!.TryGetTarget(out var handler) ? handler : null);
    }

    public void Add(EventHandler? value)
    {
        if (value is null) return;
        lock (_entries)
        {
            // Raised rarely: drop collected subscribers as new ones come, amortized (again once the live count doubles).
            if (_entries.Count >= _pruneAt)
            {
                _entries.RemoveAll(static entry => entry.Handler is null);
                _pruneAt = Math.Max(8, _entries.Count * 2);
            }
            foreach (EventHandler handler in value.GetInvocationList())
            {
                if (handler.Target is { } target && !IsClosure(target))
                {
                    _handlers.GetOrCreateValue(target).Add(handler);
                    _entries.Add(new Entry(null, new WeakReference<EventHandler>(handler)));
                }
                else
                {
                    _entries.Add(new Entry(handler, null));
                }
            }
        }
    }

    public void Remove(EventHandler? value)
    {
        if (value is null) return;
        lock (_entries)
        {
            foreach (EventHandler handler in value.GetInvocationList())
            {
                // As a multicast delegate: the last matching subscription goes.
                var index = _entries.FindLastIndex(entry => entry.Handler is { } existing && existing.Equals(handler));
                if (index < 0) continue;
                _entries.RemoveAt(index);
                if (handler.Target is { } target && _handlers.TryGetValue(target, out var handlers))
                    handlers.Remove(handler);
            }
        }
    }

    public void Raise(object? sender, EventArgs args)
    {
        List<EventHandler> handlers;
        lock (_entries)
        {
            if (_entries.Count == 0) return;
            handlers = new List<EventHandler>(_entries.Count);
            foreach (var entry in _entries)
                if (entry.Handler is { } handler)
                    handlers.Add(handler);
            if (handlers.Count != _entries.Count)
                _entries.RemoveAll(static entry => entry.Handler is null); // subscribers that were collected
        }
        foreach (var handler in handlers)
            handler(sender, args);
    }

    /// <summary>A C# compiler-generated closure or lambda cache (their type names contain '&lt;').</summary>
    private static bool IsClosure(object target) => target.GetType().Name.Contains('<');
}
