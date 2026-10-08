using System.Collections;
using System.Collections.Specialized;

namespace MauiSkiaUi;

/// <summary>
/// A drawn vertical stack whose item views are created on demand: only the items near what its ancestor scrollers show
/// exist (the visible window, the intersection of every ancestor <see cref="SkUiScrollView"/>'s viewport, plus prefetch).
/// Items come from <see cref="ItemsSource"/> and <see cref="ItemTemplate"/> (a <see cref="DataTemplateSelector"/> chooses
/// per item, views are recycled per selected template; incremental <see cref="INotifyCollectionChanged"/> changes), or from
/// <see cref="ItemFactory"/>, which can be endless.
/// </summary>
/// <remarks>
/// <para>
/// <b>It needs a drawn scroller above it.</b> The layout does not scroll; it follows the <see cref="SkUiScrollView"/>s
/// around it: put it in one (as its content, or below a header or other content in a scrolled page, also nested in
/// another list), or use <see cref="SkUiVirtualScrollView"/>, a scroll view with one inside, for a plain list. Without a
/// drawn scroller it cannot scroll and creates only the items the surface shows; inside a native MAUI <c>ScrollView</c>
/// (around the surface) the surface is as tall as the list, so every item is created. Shown that way, the layout reports
/// it once as a <c>SkiaUi:</c> trace line.
/// </para>
/// <para>
/// Sizing, scroll anchoring, prefetch and recycling are described on <see cref="SkUiVirtualVerticalStackLayoutBase"/>, the
/// engine; derive from that class instead for a list control with its own way of creating items.
/// </para>
/// </remarks>
public class SkUiVirtualVerticalStackLayout : SkUiVirtualVerticalStackLayoutBase, ISkUiVirtualList
{
    private static readonly object DefaultTemplateKey = new();

    private readonly CollectionObserver _observer;
    private IList? _source;
    private bool _sourceIsSnapshot;
    private DataTemplate? _template;
    private Func<int, ISkUiView?>? _factory;
    private int? _factoryCount;

    /// <summary>Creates an empty virtual stack.</summary>
    public SkUiVirtualVerticalStackLayout() => _observer = new CollectionObserver(this);

    /// <summary>Bindable property for <see cref="ItemsSource"/>.</summary>
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable),
        typeof(SkUiVirtualVerticalStackLayout), null,
        propertyChanged: (view, _, value) => ((SkUiVirtualVerticalStackLayout)view).OnItemsSourceChanged((IEnumerable?)value));

    /// <summary>
    /// The items, one view each from <see cref="ItemTemplate"/>, with the item as its binding context. A list
    /// (<see cref="IList"/>) is read by index; with <see cref="INotifyCollectionChanged"/> its inserts, removes, moves and
    /// replacements realize or release only the items they touch (the source is listened to weakly). Other sequences are
    /// copied once. Takes precedence over <see cref="ItemFactory"/>. Recycled views waiting for reuse keep their last item as
    /// binding context until they are rebound (as MAUI's CollectionView, so scrolling evaluates bindings once per item);
    /// replacing the source clears them.
    /// </summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>Bindable property for <see cref="ItemTemplate"/>.</summary>
    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate),
        typeof(SkUiVirtualVerticalStackLayout), null,
        propertyChanged: (view, _, value) => ((SkUiVirtualVerticalStackLayout)view).OnItemTemplateChanged((DataTemplate?)value));

    /// <summary>
    /// Creates an item's view (drawn SkUi* views only); a <see cref="DataTemplateSelector"/> chooses per item, and views are
    /// recycled per selected template. Without a template each item shows its text in an <see cref="SkUiLabel"/>.
    /// </summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>
    /// Creates the view of an item by index when there is no <see cref="ItemsSource"/>: called while scrolling, before the
    /// item shows, again after the item was released (pool views in <see cref="SkUiVirtualVerticalStackLayoutBase.ItemReleased"/>
    /// to reuse them). Returning <c>null</c> ends the list when <see cref="ItemFactoryCount"/> is not set (an endless list asks
    /// until then). Factory views are not recycled, so <see cref="SkUiVirtualVerticalStackLayoutBase.ScrollToIndex"/> to an
    /// item not measured yet creates a view only to measure it; a list that scrolls to items often can derive from
    /// <see cref="SkUiVirtualVerticalStackLayoutBase"/> with recycle keys instead.
    /// </summary>
    public Func<int, ISkUiView?>? ItemFactory
    {
        get => _factory;
        set
        {
            if (ReferenceEquals(_factory, value))
                return;
            _factory = value;
            OnItemsChanged();
        }
    }

    /// <summary>The number of <see cref="ItemFactory"/> items, or <c>null</c> (default) to ask the factory until it returns <c>null</c>.</summary>
    public int? ItemFactoryCount
    {
        get => _factoryCount;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The count cannot be negative.");
            if (_factoryCount == value)
                return;
            _factoryCount = value;
            OnItemsChanged();
        }
    }

    /// <summary>Sets <see cref="ItemsSource"/> (same as the property setter).</summary>
    public SkUiVirtualVerticalStackLayout SetItemsSource(IEnumerable? value) { ItemsSource = value; return this; }

    /// <summary>Sets <see cref="ItemTemplate"/> (same as the property setter).</summary>
    public SkUiVirtualVerticalStackLayout SetItemTemplate(DataTemplate? value) { ItemTemplate = value; return this; }

    /// <summary>Sets <see cref="ItemFactory"/> and <see cref="ItemFactoryCount"/>.</summary>
    public SkUiVirtualVerticalStackLayout SetItemFactory(Func<int, ISkUiView?>? factory, int? count = null)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "The count cannot be negative.");
        _factoryCount = count;
        _factory = factory;
        OnItemsChanged();
        return this;
    }

    private void OnItemsSourceChanged(IEnumerable? value)
    {
        _observer.Observe(value as INotifyCollectionChanged);
        ReadSource(value);
        OnItemsChanged();
        // Recycled views keep their last item until rebound (as MAUI's CollectionView); items of a replaced source go.
        foreach (var view in RecycledViews)
            ((BindableObject)view).BindingContext = null;
        SourceChanged?.Invoke(null);
    }

    /// <summary>The items changed after they were applied: the arguments of an incremental change of the source, or <c>null</c> when the source was replaced or read again (collection views follow selection and the empty view).</summary>
    internal event Action<NotifyCollectionChangedEventArgs?>? SourceChanged;

    private void ReadSource(IEnumerable? value)
    {
        _sourceIsSnapshot = value is not null and not IList;
        _source = value switch
        {
            null => null,
            IList list => list,
            _ => value.Cast<object?>().ToList()
        };
    }

    private void OnItemTemplateChanged(DataTemplate? value)
    {
        _template = value;
        // Views of the old templates go; every item is measured again with the new ones.
        ResetItems(ItemCount, HasMoreItems);
        ClearRecycledViews();
    }

    /// <summary>The items were replaced: every view goes, sizes are forgotten.</summary>
    private void OnItemsChanged() =>
        ResetItems(_source?.Count ?? (_factory is null ? 0 : _factoryCount ?? 0),
            hasMoreItems: _source is null && _factory is not null && _factoryCount is null);

    /// <inheritdoc />
    /// <remarks>The selected template (<see cref="DataTemplateSelector"/>), or one key for default labels; factory views are not recycled.</remarks>
    protected override object? GetRecycleKey(int index)
    {
        if (_source is not { } source || index >= source.Count)
            return null;
        var template = _template is DataTemplateSelector selector ? selector.SelectTemplate(source[index], this) : _template;
        return (object?)template ?? DefaultTemplateKey;
    }

    /// <inheritdoc />
    protected override ISkUiView? CreateItemView(int index, object? recycleKey)
    {
        if (_source is not null)
            return recycleKey switch
            {
                DataTemplate template => WrapItemView(template.CreateContent() as ISkUiView
                    ?? throw new InvalidOperationException($"{nameof(ItemTemplate)} of {nameof(SkUiVirtualVerticalStackLayout)} must create drawn (SkUi*) views; put native views inside an SkUiMauiContentView.")),
                _ => WrapItemView(new SkUiLabel())
            };
        return _factory?.Invoke(index);
    }

    /// <summary>Hosts a view created from a template (or a default label) in the view the layout realizes; identity by default (the collection view adds an item host).</summary>
    internal virtual ISkUiView WrapItemView(ISkUiView content) => content;

    /// <summary>The template view inside a realized view (inverse of <see cref="WrapItemView"/>).</summary>
    internal virtual ISkUiView UnwrapItemView(ISkUiView view) => view;

    /// <summary>An item of <see cref="ItemsSource"/> was bound to a new or recycled view.</summary>
    internal virtual void OnItemBound(int index, ISkUiView view, object? item) { }

    /// <inheritdoc />
    /// <remarks>An item of <see cref="ItemsSource"/> becomes its view's binding context (a default label shows its text).</remarks>
    protected override void BindItemView(int index, ISkUiView view, object? recycleKey)
    {
        if (_source is not { } source || index >= source.Count)
            return;
        var item = source[index];
        ((BindableObject)view).BindingContext = item;
        if (ReferenceEquals(recycleKey, DefaultTemplateKey))
            ((SkUiLabel)UnwrapItemView(view)).Text = item?.ToString() ?? string.Empty;
        OnItemBound(index, view, item);
    }

    /// <inheritdoc />
    protected override object? GetItem(int index) => _source is { } source && index < source.Count ? source[index] : null;

    /// <summary>The item at <paramref name="index"/> of <see cref="ItemsSource"/> (as read: a copy for other sequences), or <c>null</c>.</summary>
    internal object? ItemAt(int index) => GetItem(index);

    /// <summary>The index of <paramref name="item"/> in <see cref="ItemsSource"/> (as read), or -1.</summary>
    internal int IndexOfItem(object? item) => _source?.IndexOf(item) ?? -1;

    private void OnSourceCollectionChanged(NotifyCollectionChangedEventArgs args)
    {
        ApplySourceChange(args);
        SourceChanged?.Invoke(_sourceIsSnapshot ? null : args);
    }

    private void ApplySourceChange(NotifyCollectionChangedEventArgs args)
    {
        if (_source is not { } source)
            return;
        if (_sourceIsSnapshot)
        {
            ReadSource(ItemsSource);
            OnItemsChanged();
            return;
        }
        var count = ItemCount;
        switch (args.Action)
        {
            // An Add without its index (-1) does not say where: it starts over (below), as any change out of step.
            case NotifyCollectionChangedAction.Add when args.NewItems is { } added && args.NewStartingIndex >= 0
                && args.NewStartingIndex <= count && count + added.Count == source.Count:
                InsertItems(args.NewStartingIndex, added.Count);
                break;
            case NotifyCollectionChangedAction.Remove when args.OldItems is { } removed && args.OldStartingIndex >= 0
                && args.OldStartingIndex + removed.Count <= count && count - removed.Count == source.Count:
                RemoveItems(args.OldStartingIndex, removed.Count);
                break;
            case NotifyCollectionChangedAction.Replace when args.NewItems is { } replaced && args.NewStartingIndex >= 0
                && args.NewStartingIndex + replaced.Count <= count && count == source.Count:
                ReplaceItems(args.NewStartingIndex, replaced.Count);
                break;
            case NotifyCollectionChangedAction.Move when args.OldItems is { } moved && args.OldStartingIndex >= 0 && args.NewStartingIndex >= 0
                && args.OldStartingIndex + moved.Count <= count && args.NewStartingIndex + moved.Count <= count && count == source.Count:
                MoveItems(args.OldStartingIndex, moved.Count, args.NewStartingIndex);
                break;
            default:
                // A reset, or a change out of step with the list (raised without its indices): start over.
                OnItemsChanged();
                break;
        }
    }

    /// <summary>Listens to the items' collection without the collection keeping the layout alive.</summary>
    private sealed class CollectionObserver(SkUiVirtualVerticalStackLayout owner)
    {
        private readonly WeakReference<SkUiVirtualVerticalStackLayout> _owner = new(owner);
        private INotifyCollectionChanged? _source;

        public void Observe(INotifyCollectionChanged? source)
        {
            if (ReferenceEquals(source, _source))
                return;
            if (_source is not null)
                _source.CollectionChanged -= OnCollectionChanged;
            _source = source;
            if (source is not null)
                source.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
        {
            if (_owner.TryGetTarget(out var owner))
                owner.OnSourceCollectionChanged(args);
            else
                Observe(null);
        }
    }
}
