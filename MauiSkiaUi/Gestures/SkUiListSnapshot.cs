using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MauiSkiaUi;

/// <summary>
/// A copy of a list's items in a buffer rented from <see cref="ArrayPool{T}.Shared"/>, for loops whose handlers change the
/// list while they run (a recognizer leaving its arena, a listener unsubscribing) without an array per pass (NFR-2). Each
/// snapshot rents its own buffer, so a handler that opens another snapshot of the same list is safe. <see cref="Dispose"/>
/// clears the copied range before returning the buffer, so a pooled array never keeps the items alive.
/// </summary>
/// <remarks>Use it with <c>using</c>; loops still re-check membership where an item removed mid-loop must be skipped.</remarks>
internal ref struct SkUiListSnapshot<T>
{
    private T[]? _buffer;
    private readonly int _count;

    public SkUiListSnapshot(List<T> list)
    {
        _count = list.Count;
        if (_count == 0)
            return;
        _buffer = ArrayPool<T>.Shared.Rent(_count);
        CollectionsMarshal.AsSpan(list).CopyTo(_buffer);
    }

    public SkUiListSnapshot(IList<T> list)
    {
        if (list is List<T> concrete)
        {
            this = new SkUiListSnapshot<T>(concrete);
            return;
        }
        _count = list.Count;
        if (_count == 0)
            return;
        _buffer = ArrayPool<T>.Shared.Rent(_count);
        list.CopyTo(_buffer, 0);
    }

    /// <summary>The items as they were when the snapshot was taken.</summary>
    public readonly ReadOnlySpan<T> Span => _buffer is null ? default : new ReadOnlySpan<T>(_buffer, 0, _count);

    public void Dispose()
    {
        if (_buffer is not { } buffer)
            return;
        _buffer = null;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Array.Clear(buffer, 0, _count);
        ArrayPool<T>.Shared.Return(buffer);
    }
}
