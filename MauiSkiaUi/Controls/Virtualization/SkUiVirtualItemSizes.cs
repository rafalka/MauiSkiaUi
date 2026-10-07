namespace MauiSkiaUi;

/// <summary>
/// Main-axis sizes of a virtual list's items, and the offsets they give. An item's size is its measured size once it has
/// been measured, else an estimate (<see cref="EstimatedSize"/>, or the average of the measured sizes); with a
/// <see cref="FixedExtent"/> every item has that size (the fast path: offsets are a multiplication). Offsets of variable
/// sizes come from two Fenwick trees (measured sizes and how many are measured) in O(log n); the item at an offset is
/// found by binary search. Layer-agnostic: no views.
/// </summary>
internal sealed class SkUiVirtualItemSizes
{
    /// <summary>The estimate before any item is measured and without <see cref="EstimatedSize"/>.</summary>
    public const double DefaultEstimate = 44;

    private double[] _sizes = [];   // NaN: not measured; length is the capacity
    private double[] _sumTree = [0]; // Fenwick (1-based) over measured sizes
    private int[] _countTree = [0];  // Fenwick (1-based) over "is measured"
    private bool _treesValid = true;
    private int _measuredCount;
    private double _measuredSum;

    /// <summary>Number of items.</summary>
    public int Count { get; private set; }

    /// <summary>Gap between items.</summary>
    public double Spacing { get; set; }

    /// <summary>When positive, the size of every item (measured sizes are ignored).</summary>
    public double FixedExtent { get; set; }

    /// <summary>When positive, the size assumed for items not measured yet; otherwise the average of the measured ones.</summary>
    public double EstimatedSize { get; set; }

    /// <summary>Number of measured items (diagnostics, tests).</summary>
    public int MeasuredCount => FixedExtent > 0 ? Count : _measuredCount;

    /// <summary>The size assumed for an item that is not measured.</summary>
    public double Estimate =>
        FixedExtent > 0 ? FixedExtent
        : EstimatedSize > 0 ? EstimatedSize
        : !double.IsNaN(_frozenEstimate) ? _frozenEstimate
        : _measuredCount > 0 ? _measuredSum / _measuredCount
        : DefaultEstimate;

    private double _frozenEstimate = double.NaN;

    /// <summary>
    /// Keeps <see cref="Estimate"/> at its current value until <see cref="ThawEstimate"/>, while items are measured. Each
    /// measured item moves the average, and with it the offset of every item after unmeasured ones: a pass that keeps an item
    /// far down the list in place would chase it, realizing ever more items. The new average is applied once, after the pass.
    /// </summary>
    public void FreezeEstimate() => _frozenEstimate = Estimate;

    /// <summary>Lets <see cref="Estimate"/> follow the measured average again.</summary>
    public void ThawEstimate() => _frozenEstimate = double.NaN;

    /// <summary>Whether the item's size is known (measured, or fixed).</summary>
    public bool IsMeasured(int index) => FixedExtent > 0 || !double.IsNaN(_sizes[index]);

    /// <summary>The item's measured size, or the estimate.</summary>
    public double SizeOf(int index)
    {
        if (FixedExtent > 0)
            return FixedExtent;
        var size = _sizes[index];
        return double.IsNaN(size) ? Estimate : size;
    }

    /// <summary>Where the item starts (<paramref name="index"/> in <c>[0, Count]</c>; <c>Count</c> gives the end plus one gap).</summary>
    public double OffsetOf(int index)
    {
        if (FixedExtent > 0)
            return index * (FixedExtent + Spacing);
        EnsureTrees();
        double sum = 0;
        var measured = 0;
        for (var node = index; node > 0; node -= node & -node)
        {
            sum += _sumTree[node];
            measured += _countTree[node];
        }
        return sum + (index - measured) * Estimate + index * Spacing;
    }

    /// <summary>The length of all items with the gaps between them.</summary>
    public double TotalLength => Count == 0 ? 0 : OffsetOf(Count) - Spacing;

    /// <summary>The item at <paramref name="offset"/>: the last one that starts at or before it (0 before the first; the last after the end).</summary>
    public int IndexAt(double offset)
    {
        if (Count == 0 || offset <= 0)
            return 0;
        if (FixedExtent > 0)
            return (int)Math.Clamp(Math.Floor(offset / (FixedExtent + Spacing)), 0, Count - 1);
        int low = 0, high = Count - 1;
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            if (OffsetOf(middle) <= offset)
                low = middle;
            else
                high = middle - 1;
        }
        return low;
    }

    /// <summary>Records an item's measured size.</summary>
    public void SetSize(int index, double size)
    {
        if (FixedExtent > 0)
            return;
        size = Math.Max(0, size);
        var previous = _sizes[index];
        if (previous == size)
            return;
        _sizes[index] = size;
        var wasMeasured = !double.IsNaN(previous);
        _measuredSum += size - (wasMeasured ? previous : 0);
        if (!wasMeasured)
            _measuredCount++;
        if (_treesValid)
            Update(index, size - (wasMeasured ? previous : 0), wasMeasured ? 0 : 1);
    }

    /// <summary>Forgets an item's measured size (it is estimated until measured again).</summary>
    public void Forget(int index)
    {
        var previous = _sizes[index];
        if (double.IsNaN(previous))
            return;
        _sizes[index] = double.NaN;
        _measuredSum -= previous;
        _measuredCount--;
        if (_treesValid)
            Update(index, -previous, -1);
    }

    /// <summary>Forgets every measured size (items are measured at another width).</summary>
    public void ForgetAll()
    {
        Array.Fill(_sizes, double.NaN);
        _measuredCount = 0;
        _measuredSum = 0;
        _treesValid = false;
    }

    /// <summary>Sets the number of items: new ones at the end are not measured, removed ones go.</summary>
    public void SetCount(int count)
    {
        if (count > Count)
            Insert(Count, count - Count);
        else if (count < Count)
            Remove(count, Count - count);
    }

    /// <summary>Inserts <paramref name="count"/> unmeasured items at <paramref name="index"/>.</summary>
    public void Insert(int index, int count)
    {
        if (count <= 0)
            return;
        var appending = index == Count;
        EnsureCapacity(Count + count);
        Array.Copy(_sizes, index, _sizes, index + count, Count - index);
        Array.Fill(_sizes, double.NaN, index, count);
        Count += count;
        // Unmeasured items add nothing to the trees: an append keeps them valid.
        if (!appending)
            _treesValid = false;
    }

    /// <summary>Removes <paramref name="count"/> items at <paramref name="index"/>.</summary>
    public void Remove(int index, int count)
    {
        if (count <= 0)
            return;
        for (var item = index; item < index + count; item++)
            if (!double.IsNaN(_sizes[item]))
            {
                _measuredSum -= _sizes[item];
                _measuredCount--;
            }
        Array.Copy(_sizes, index + count, _sizes, index, Count - index - count);
        Array.Fill(_sizes, double.NaN, Count - count, count);
        Count -= count;
        _treesValid = false;
    }

    /// <summary>Moves <paramref name="count"/> items from <paramref name="from"/> to <paramref name="to"/> (the index after removing them), with their sizes.</summary>
    public void Move(int from, int to, int count)
    {
        if (count <= 0 || from == to)
            return;
        var moved = _sizes.AsSpan(from, count).ToArray();
        Array.Copy(_sizes, from + count, _sizes, from, Count - from - count);
        Array.Copy(_sizes, to, _sizes, to + count, Count - count - to);
        moved.CopyTo(_sizes, to);
        _treesValid = false;
    }

    /// <summary>Removes every item.</summary>
    public void Clear()
    {
        Array.Fill(_sizes, double.NaN);
        Count = 0;
        _measuredCount = 0;
        _measuredSum = 0;
        _treesValid = false;
    }

    private void EnsureCapacity(int count)
    {
        if (count <= _sizes.Length)
            return;
        var capacity = Math.Max(16, Math.Max(count, _sizes.Length * 2));
        var sizes = new double[capacity];
        Array.Fill(sizes, double.NaN);
        Array.Copy(_sizes, sizes, Count);
        _sizes = sizes;
        _treesValid = false;
    }

    private void Update(int index, double size, int measured)
    {
        for (var node = index + 1; node < _sumTree.Length; node += node & -node)
        {
            _sumTree[node] += size;
            _countTree[node] += measured;
        }
    }

    /// <summary>Rebuilds the trees in O(capacity) after a structural change.</summary>
    private void EnsureTrees()
    {
        if (_treesValid)
            return;
        var length = _sizes.Length + 1;
        if (_sumTree.Length != length)
        {
            _sumTree = new double[length];
            _countTree = new int[length];
        }
        else
        {
            Array.Clear(_sumTree);
            Array.Clear(_countTree);
        }
        for (var node = 1; node < length; node++)
        {
            var size = _sizes[node - 1];
            if (!double.IsNaN(size))
            {
                _sumTree[node] += size;
                _countTree[node]++;
            }
            var parent = node + (node & -node);
            if (parent < length)
            {
                _sumTree[parent] += _sumTree[node];
                _countTree[parent] += _countTree[node];
            }
        }
        _treesValid = true;
    }
}
