using System.Runtime.CompilerServices;

namespace MauiSkiaUi;

/// <summary>
/// The subscriber list of an event that does not keep its subscribers alive, for events of long-lived publishers (an
/// app-wide setting, a static service, a theme): a page that subscribes and never unsubscribes can still be collected.
/// SkiaUi's own app-wide events use it (<c>SkUiLook.CurrentChanged</c>, <c>SkUiColorScheme.CurrentChanged</c>).
/// </summary>
/// <remarks>
/// <para>
/// A handler on an object (a method group, or a lambda that uses only <c>this</c>) is held as long as that object lives.
/// Static handlers, and lambdas that capture locals, are held strongly, as by a plain event: nothing else references a
/// compiler-generated closure, so a weakly held one would stop firing at the next collection. Handlers run directly,
/// without reflection, on the thread that raises.
/// </para>
/// <para>For publishers that outlive their subscribers. Events of a node to its owner stay plain events.</para>
/// </remarks>
/// <example>
/// <code>
/// public static class Units
/// {
///     private static readonly SkUiWeakEvent _changed = new();
///
///     public static event EventHandler? Changed { add =&gt; _changed.Add(value); remove =&gt; _changed.Remove(value); }
///
///     public static void Notify() =&gt; _changed.Raise(null, EventArgs.Empty);
/// }
/// </code>
/// </example>
public sealed class SkUiWeakEvent
{
    private readonly SkUiWeakDelegates<EventHandler> _handlers = new();

    /// <summary>Subscribes <paramref name="handler"/> (the event's <c>add</c> accessor).</summary>
    public void Add(EventHandler? handler) => _handlers.Add(handler);

    /// <summary>Unsubscribes <paramref name="handler"/> (the event's <c>remove</c> accessor).</summary>
    public void Remove(EventHandler? handler) => _handlers.Remove(handler);

    /// <summary>Calls every live subscriber, in subscription order.</summary>
    public void Raise(object? sender, EventArgs args)
    {
        foreach (var handler in _handlers.Snapshot())
            handler(sender, args);
    }
}

/// <summary>The subscriber list of an <see cref="EventHandler{TEventArgs}"/> event that does not keep its subscribers alive; see <see cref="SkUiWeakEvent"/>.</summary>
/// <typeparam name="TEventArgs">The event's arguments.</typeparam>
public sealed class SkUiWeakEvent<TEventArgs>
{
    private readonly SkUiWeakDelegates<EventHandler<TEventArgs>> _handlers = new();

    /// <summary>Subscribes <paramref name="handler"/> (the event's <c>add</c> accessor).</summary>
    public void Add(EventHandler<TEventArgs>? handler) => _handlers.Add(handler);

    /// <summary>Unsubscribes <paramref name="handler"/> (the event's <c>remove</c> accessor).</summary>
    public void Remove(EventHandler<TEventArgs>? handler) => _handlers.Remove(handler);

    /// <summary>Calls every live subscriber, in subscription order.</summary>
    public void Raise(object? sender, TEventArgs args)
    {
        foreach (var handler in _handlers.Snapshot())
            handler(sender, args);
    }
}

/// <summary>The subscribers of a <see cref="SkUiWeakEvent"/>: strong or weak entries, pruned as subscribers come and go.</summary>
internal sealed class SkUiWeakDelegates<TDelegate> where TDelegate : Delegate
{
    private readonly List<Entry> _entries = [];
    // Keeps each weakly held handler alive exactly as long as its target (the table's key) lives.
    private readonly ConditionalWeakTable<object, List<TDelegate>> _byTarget = new();
    private int _pruneAt = 8;

    /// <summary>A subscription: a strong handler, or a weak reference to one kept alive by its target.</summary>
    private sealed class Entry(TDelegate? strong, WeakReference<TDelegate>? weak)
    {
        public TDelegate? Handler => strong ?? (weak!.TryGetTarget(out var handler) ? handler : null);
    }

    public void Add(TDelegate? value)
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
            foreach (var handler in value.GetInvocationList().Cast<TDelegate>())
            {
                if (handler.Target is { } target && !IsClosure(target))
                {
                    _byTarget.GetOrCreateValue(target).Add(handler);
                    _entries.Add(new Entry(null, new WeakReference<TDelegate>(handler)));
                }
                else
                {
                    _entries.Add(new Entry(handler, null));
                }
            }
        }
    }

    public void Remove(TDelegate? value)
    {
        if (value is null) return;
        lock (_entries)
        {
            foreach (var handler in value.GetInvocationList().Cast<TDelegate>())
            {
                // As a multicast delegate: the last matching subscription goes.
                var index = _entries.FindLastIndex(entry => entry.Handler is { } existing && existing.Equals(handler));
                if (index < 0) continue;
                _entries.RemoveAt(index);
                if (handler.Target is { } target && _byTarget.TryGetValue(target, out var handlers))
                    handlers.Remove(handler);
            }
        }
    }

    /// <summary>The live subscribers, in order; collected ones are dropped.</summary>
    public List<TDelegate> Snapshot()
    {
        lock (_entries)
        {
            var handlers = new List<TDelegate>(_entries.Count);
            foreach (var entry in _entries)
                if (entry.Handler is { } handler)
                    handlers.Add(handler);
            if (handlers.Count != _entries.Count)
                _entries.RemoveAll(static entry => entry.Handler is null);
            return handlers;
        }
    }

    /// <summary>A C# compiler-generated closure or lambda cache (their type names contain '&lt;').</summary>
    private static bool IsClosure(object target) => target.GetType().Name.Contains('<');
}
