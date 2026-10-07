namespace MauiSkiaUi;

/// <summary>An item view of a <see cref="SkUiVirtualVerticalStackLayout"/> was realized (created or recycled, and measured) or released.</summary>
/// <param name="index">The item's index.</param>
/// <param name="view">The item's view.</param>
/// <param name="item">The item of <see cref="SkUiVirtualVerticalStackLayout.ItemsSource"/>, or <c>null</c> for views of <see cref="SkUiVirtualVerticalStackLayout.ItemFactory"/>.</param>
public sealed class SkUiVirtualItemEventArgs(int index, ISkUiView view, object? item) : EventArgs
{
    /// <summary>The item's index.</summary>
    public int Index { get; } = index;

    /// <summary>The item's view.</summary>
    public ISkUiView View { get; } = view;

    /// <summary>The item of <see cref="SkUiVirtualVerticalStackLayout.ItemsSource"/>, or <c>null</c> for views of <see cref="SkUiVirtualVerticalStackLayout.ItemFactory"/>.</summary>
    public object? Item { get; } = item;
}

/// <summary>The items a <see cref="SkUiVirtualVerticalStackLayout"/> shows changed.</summary>
/// <param name="firstVisibleIndex">The first item that shows (at least partly), or -1 when none shows.</param>
/// <param name="lastVisibleIndex">The last item that shows (at least partly), or -1 when none shows.</param>
public sealed class SkUiVisibleRangeChangedEventArgs(int firstVisibleIndex, int lastVisibleIndex) : EventArgs
{
    /// <summary>The first item that shows (at least partly), or -1 when none shows.</summary>
    public int FirstVisibleIndex { get; } = firstVisibleIndex;

    /// <summary>The last item that shows (at least partly), or -1 when none shows.</summary>
    public int LastVisibleIndex { get; } = lastVisibleIndex;
}
