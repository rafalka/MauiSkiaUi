using System.Buffers;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>What kind of change a <see cref="SkUiWeakListener{TTarget}"/> reports.</summary>
public enum SkUiChangeKind
{
    /// <summary><see cref="INotifyPropertyChanged.PropertyChanged"/>; the name is in <see cref="SkUiChange.PropertyName"/>.</summary>
    Property,
    /// <summary><see cref="INotifyCollectionChanged.CollectionChanged"/>.</summary>
    Collection,
    /// <summary><see cref="ICommand.CanExecuteChanged"/>.</summary>
    CanExecute,
    /// <summary>A change a source reports by its own event: a MAUI gradient brush's stops, a geometry group's children.</summary>
    Invalidated
}

/// <summary>One change reported by a <see cref="SkUiWeakListener{TTarget}"/>.</summary>
/// <param name="Sender">The object that raised the change (the source, or an item of it).</param>
/// <param name="Kind">The kind of change.</param>
/// <param name="PropertyName">The changed property, for <see cref="SkUiChangeKind.Property"/>.</param>
public readonly record struct SkUiChange(object? Sender, SkUiChangeKind Kind, string? PropertyName = null);

/// <summary>
/// Listens to the changes of a source that may outlive the listening view, without the source keeping that view alive:
/// a brush, geometry or image source from app resources, a view model's command or <see cref="INotifyPropertyChanged"/>
/// object, a shared collection. It reports property changes, collection changes, <see cref="ICommand.CanExecuteChanged"/>,
/// and MAUI's gradient-brush and geometry-group changes. The drawn controls use it for every source they do not own; use
/// it in your own controls (Core nodes or <c>SkUiView</c>s) the same way.
/// </summary>
/// <remarks>
/// <para>
/// <b>Store the listener</b> in a field of <paramref name="target"/> (or of something that lives as long). The source
/// references it only weakly, so a listener nobody keeps is collected and stops reporting. The target is held weakly too:
/// <paramref name="onChanged"/> receives it, so the callback can be <c>static</c>.
/// </para>
/// <para>
/// One subscription per source, however many views listen to it, and the listeners of collected views are dropped as
/// listeners come and go. <paramref name="onChanged"/> runs on the thread that raised the change (commands may raise on
/// any thread).
/// </para>
/// <para>
/// For sources the view does not own. Owned and structural subscriptions (a node and its children, a handler and its
/// platform view) stay ordinary events with explicit cleanup: their lifetimes are the same, and a weak reference would
/// only hide a missing cleanup. Weak listening does not replace detach cleanup either: a removed view that is still
/// referenced is alive and keeps listening.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class LegendNode : SkUiCoreNode
/// {
///     private readonly SkUiWeakListener&lt;LegendNode&gt; _modelListener;
///
///     public LegendNode() =&gt; _modelListener = new(this, static (node, change) =&gt;
///     {
///         if (change.PropertyName is nameof(ChartModel.Series)) node.InvalidateMeasure();
///     });
///
///     public LegendNode SetModel(ChartModel? model) { _modelListener.Listen(model); InvalidateMeasure(); return this; }
/// }
/// </code>
/// </example>
/// <typeparam name="TTarget">The object notified (usually the view that owns the listener).</typeparam>
/// <param name="target">The object notified; held weakly.</param>
/// <param name="onChanged">Called with the target for each change while the target lives.</param>
public sealed class SkUiWeakListener<TTarget>(TTarget target, Action<TTarget, SkUiChange> onChanged) : ISkUiChangeListener
    where TTarget : class
{
    private readonly WeakReference<TTarget> _target = new(target ?? throw new ArgumentNullException(nameof(target)));
    private readonly Action<TTarget, SkUiChange> _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
    private SkUiChangeHub? _hub;

    /// <summary>The source listened to (<c>null</c>: none).</summary>
    public object? Source => _hub?.Source;

    /// <summary>Stops listening to the previous source and listens to <paramref name="source"/> (<c>null</c>: none).</summary>
    public void Listen(object? source)
    {
        if (ReferenceEquals(source, Source)) return;
        _hub?.Remove(this);
        _hub = source is null ? null : SkUiChangeHub.For(source);
        _hub?.Add(this);
    }

    void ISkUiChangeListener.OnChanged(in SkUiChange change)
    {
        if (_target.TryGetTarget(out var target))
            _onChanged(target, change);
    }
}

/// <summary>A listener registered with a <see cref="SkUiChangeHub"/>.</summary>
internal interface ISkUiChangeListener
{
    void OnChanged(in SkUiChange change);
}

/// <summary>
/// Subscribes once to a change source and forwards its changes to weakly held listeners. One hub per source, kept alive
/// by the source (and found again through a weak table), so a shared brush or command used by thousands of views holds
/// one subscription; listeners of collected views are pruned as listeners come and go, instead of piling up until the
/// source next changes.
/// </summary>
internal sealed class SkUiChangeHub
{
    /// <summary>
    /// Subscribes to source events beyond the standard interfaces (MAUI Controls' gradient brushes and geometry groups,
    /// <see cref="SkUiMauiChangeSources"/>): the layer-agnostic hub knows no MAUI Controls type.
    /// </summary>
    private static readonly Action<object, EventHandler> SubscribeOtherEvents = SkUiMauiChangeSources.Subscribe;

    private static readonly ConditionalWeakTable<object, SkUiChangeHub> Hubs = new();
    private readonly List<WeakReference<ISkUiChangeListener>> _listeners = [];
    private readonly WeakReference<object> _source;
    private int _pruneAt = 8;

    private SkUiChangeHub(object source)
    {
        // The source's events reference this hub; the hub references the source only weakly (it is the table's key).
        _source = new WeakReference<object>(source);
        if (source is INotifyPropertyChanged properties) properties.PropertyChanged += OnPropertyChanged;
        if (source is INotifyCollectionChanged collection) collection.CollectionChanged += OnCollectionChanged;
        if (source is ICommand command) command.CanExecuteChanged += OnCanExecuteChanged;
        SubscribeOtherEvents(source, OnInvalidated);
    }

    /// <summary>The source, while it is alive.</summary>
    public object? Source => _source.TryGetTarget(out var source) ? source : null;

    /// <summary>The hub of <paramref name="source"/>, created on first use.</summary>
    public static SkUiChangeHub For(object source) => Hubs.GetValue(source, static key => new SkUiChangeHub(key));

    public void Add(ISkUiChangeListener listener)
    {
        // Views live on the UI thread, but MAUI's static brushes (Brush.Red, …) and app commands are shared by every
        // view of every thread, and commands may raise from any thread.
        lock (_listeners)
        {
            if (_listeners.Count >= _pruneAt)
            {
                _listeners.RemoveAll(static reference => !reference.TryGetTarget(out _));
                _pruneAt = Math.Max(8, _listeners.Count * 2); // amortized: prune again once the live count doubles
            }
            _listeners.Add(new WeakReference<ISkUiChangeListener>(listener));
        }
    }

    public void Remove(ISkUiChangeListener listener)
    {
        lock (_listeners)
            _listeners.RemoveAll(reference => !reference.TryGetTarget(out var target) || ReferenceEquals(target, listener));
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e) => Raise(new SkUiChange(sender, SkUiChangeKind.Property, e.PropertyName));
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Raise(new SkUiChange(sender, SkUiChangeKind.Collection));
    private void OnCanExecuteChanged(object? sender, EventArgs e) => Raise(new SkUiChange(sender, SkUiChangeKind.CanExecute));
    private void OnInvalidated(object? sender, EventArgs e) => Raise(new SkUiChange(sender, SkUiChangeKind.Invalidated));

    private void Raise(in SkUiChange change)
    {
        // A snapshot (pooled): a listener may re-listen, and so add or remove listeners, while handling the change.
        ISkUiChangeListener[] alive;
        var count = 0;
        lock (_listeners)
        {
            if (_listeners.Count == 0) return;
            alive = ArrayPool<ISkUiChangeListener>.Shared.Rent(_listeners.Count);
            foreach (var reference in _listeners)
                if (reference.TryGetTarget(out var listener))
                    alive[count++] = listener;
            if (count != _listeners.Count)
                _listeners.RemoveAll(static reference => !reference.TryGetTarget(out _));
        }
        try
        {
            for (var index = 0; index < count; index++)
                alive[index].OnChanged(change);
        }
        finally
        {
            Array.Clear(alive, 0, count); // the pool must not keep views alive
            ArrayPool<ISkUiChangeListener>.Shared.Return(alive);
        }
    }
}
