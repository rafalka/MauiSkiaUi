using System.Buffers;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUi;

/// <summary>What kind of change a <see cref="SkUiChangeHub"/> reports.</summary>
internal enum SkUiChangeKind
{
    /// <summary><see cref="INotifyPropertyChanged.PropertyChanged"/>; the name is in <see cref="SkUiChange.PropertyName"/>.</summary>
    Property,
    /// <summary><see cref="INotifyCollectionChanged.CollectionChanged"/>.</summary>
    Collection,
    /// <summary><see cref="ICommand.CanExecuteChanged"/>.</summary>
    CanExecute,
    /// <summary>A gradient brush's stops or a geometry group's children changed.</summary>
    Invalidated
}

/// <summary>One change reported by a <see cref="SkUiChangeHub"/>.</summary>
internal readonly record struct SkUiChange(object? Sender, SkUiChangeKind Kind, string? PropertyName = null);

/// <summary>
/// Listens to the changes of one source that may outlive the view listening (a brush, dash array, point collection,
/// geometry, stroke shape, image source, transformation list or command; often a shared resource), without the source
/// keeping the view alive. <paramref name="onChanged"/> must not capture: it receives the target. The view owns its
/// listener; the source reaches it only weakly, through the source's <see cref="SkUiChangeHub"/>.
/// </summary>
/// <remarks>
/// For sources the view does not own. Owned and structural subscriptions (a parent and its children, a handler and its
/// platform view) stay ordinary events with explicit cleanup: their lifetimes are the same, and a weak reference would
/// only hide a missing cleanup.
/// </remarks>
internal sealed class SkUiWeakListener<TTarget>(TTarget target, Action<TTarget, SkUiChange> onChanged) : ISkUiChangeListener
    where TTarget : class
{
    private readonly WeakReference<TTarget> _target = new(target);
    private SkUiChangeHub? _hub;

    /// <summary>The source listened to.</summary>
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
            onChanged(target, change);
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
        if (source is GradientBrush gradient) gradient.InvalidateGradientBrushRequested += OnInvalidated;
        if (source is GeometryGroup group) group.InvalidateGeometryRequested += OnInvalidated;
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
