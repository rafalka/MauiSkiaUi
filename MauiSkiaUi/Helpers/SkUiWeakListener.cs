using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUi;

/// <summary>
/// Listens to the changes of one source (a brush, dash array, point collection, geometry or stroke shape) for a view,
/// without the source keeping the view alive: brushes and geometries are often shared resources that outlive the views
/// using them. Property and collection changes, gradient-stop changes and geometry-group changes all report through
/// <paramref name="onChanged"/>, which must not capture (it receives the target). The view owns its listener; the source
/// reaches it only weakly, through the source's <see cref="SkUiChangeHub"/>.
/// </summary>
internal sealed class SkUiWeakListener<TTarget>(TTarget target, Action<TTarget, object?, string?> onChanged) : ISkUiChangeListener
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

    void ISkUiChangeListener.OnChanged(object? sender, string? propertyName)
    {
        if (_target.TryGetTarget(out var target))
            onChanged(target, sender, propertyName);
    }
}

/// <summary>A listener registered with a <see cref="SkUiChangeHub"/>.</summary>
internal interface ISkUiChangeListener
{
    void OnChanged(object? sender, string? propertyName);
}

/// <summary>
/// Subscribes once to a change source and forwards its changes to weakly held listeners. One hub per source, kept alive
/// by the source (and found again through a weak table), so a shared brush used by thousands of shapes holds one
/// subscription; listeners of collected views are pruned as listeners come and go, instead of piling up until the
/// source next changes.
/// </summary>
internal sealed class SkUiChangeHub
{
    private static readonly ConditionalWeakTable<object, SkUiChangeHub> Hubs = new();
    private readonly List<WeakReference<ISkUiChangeListener>> _listeners = [];
    private int _pruneAt = 8;

    private SkUiChangeHub(object source)
    {
        // The source's events reference this hub; the hub references the source only weakly (it is the table's key).
        _source = new WeakReference<object>(source);
        if (source is INotifyPropertyChanged properties) properties.PropertyChanged += OnPropertyChanged;
        if (source is INotifyCollectionChanged collection) collection.CollectionChanged += OnCollectionChanged;
        if (source is GradientBrush gradient) gradient.InvalidateGradientBrushRequested += OnInvalidated;
        if (source is GeometryGroup group) group.InvalidateGeometryRequested += OnInvalidated;
    }

    private readonly WeakReference<object> _source;

    /// <summary>The source, while it is alive.</summary>
    public object? Source => _source.TryGetTarget(out var source) ? source : null;

    /// <summary>The hub of <paramref name="source"/>, created on first use.</summary>
    public static SkUiChangeHub For(object source) => Hubs.GetValue(source, static key => new SkUiChangeHub(key));

    public void Add(ISkUiChangeListener listener)
    {
        // Views live on the UI thread, but MAUI's static brushes (Brush.Red, …) are shared by every view of every thread.
        lock (_listeners)
        {
            if (_listeners.Count >= _pruneAt)
            {
                _listeners.RemoveAll(reference => !reference.TryGetTarget(out _));
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

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e) => Raise(sender, e.PropertyName);
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Raise(sender, null);
    private void OnInvalidated(object? sender, EventArgs e) => Raise(sender, null);

    private void Raise(object? sender, string? propertyName)
    {
        // A snapshot: a listener may re-listen (and so add or remove listeners) while handling the change.
        List<ISkUiChangeListener> alive;
        lock (_listeners)
        {
            if (_listeners.Count == 0) return;
            alive = new List<ISkUiChangeListener>(_listeners.Count);
            foreach (var reference in _listeners)
                if (reference.TryGetTarget(out var listener))
                    alive.Add(listener);
            if (alive.Count != _listeners.Count)
                _listeners.RemoveAll(reference => !reference.TryGetTarget(out _));
        }
        foreach (var listener in alive)
            listener.OnChanged(sender, propertyName);
    }
}
