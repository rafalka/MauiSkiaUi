using System.Collections;
using System.Collections.Specialized;

namespace MauiSkiaUi;

/// <summary>
/// A picker's item texts (<see cref="SkUiPicker.Items"/>, <see cref="Core.SkUiCorePicker.Items"/>), as MAUI's picker
/// <c>Items</c>: add, insert and remove texts to fill the picker; while <c>ItemsSource</c> is set, it holds the source's
/// display texts and cannot be changed (as MAUI's, it throws).
/// </summary>
public sealed class SkUiPickerItems : IList<string>, IReadOnlyList<string>, INotifyCollectionChanged
{
    private static readonly NotifyCollectionChangedEventArgs ResetArgs = new(NotifyCollectionChangedAction.Reset);
    private readonly List<string> _items = [];

    internal SkUiPickerItems()
    {
    }

    /// <summary>Raised when the texts change (by the app, or because the picker's source changed).</summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>Whether the texts follow the picker's <c>ItemsSource</c> (then they cannot be changed).</summary>
    public bool IsReadOnly { get; internal set; }

    /// <inheritdoc cref="ICollection{T}.Count" />
    public int Count => _items.Count;

    /// <inheritdoc cref="IList{T}.this" />
    public string this[int index]
    {
        get => _items[index];
        set
        {
            ThrowIfLocked();
            var old = _items[index];
            _items[index] = value;
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, old, index));
        }
    }

    /// <inheritdoc />
    public void Add(string item) => Insert(_items.Count, item);

    /// <inheritdoc />
    public void Insert(int index, string item)
    {
        ThrowIfLocked();
        _items.Insert(index, item);
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
    }

    /// <inheritdoc />
    public bool Remove(string item)
    {
        ThrowIfLocked();
        var index = _items.IndexOf(item);
        if (index < 0)
            return false;
        RemoveAt(index);
        return true;
    }

    /// <inheritdoc />
    public void RemoveAt(int index)
    {
        ThrowIfLocked();
        var item = _items[index];
        _items.RemoveAt(index);
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
    }

    /// <inheritdoc />
    public void Clear()
    {
        ThrowIfLocked();
        _items.Clear();
        CollectionChanged?.Invoke(this, ResetArgs);
    }

    /// <inheritdoc />
    public bool Contains(string item) => _items.Contains(item);

    /// <inheritdoc />
    public int IndexOf(string item) => _items.IndexOf(item);

    /// <inheritdoc />
    public void CopyTo(string[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    /// <summary>The picker refills the texts from its source (allowed while locked).</summary>
    internal void Reset(IEnumerable<string> texts)
    {
        _items.Clear();
        _items.AddRange(texts);
        CollectionChanged?.Invoke(this, ResetArgs);
    }

    private void ThrowIfLocked()
    {
        if (IsReadOnly)
            throw new InvalidOperationException("The Items list can not be manipulated if the ItemsSource property is set.");
    }
}
