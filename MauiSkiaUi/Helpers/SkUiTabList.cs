using System.Collections.ObjectModel;

namespace MauiSkiaUi;

/// <summary>A tab view's tabs: each item once, as it has one pane.</summary>
internal sealed class SkUiTabList<T> : ObservableCollection<T> where T : class
{
    protected override void InsertItem(int index, T item)
    {
        Validate(item, -1);
        base.InsertItem(index, item);
    }

    protected override void SetItem(int index, T item)
    {
        Validate(item, index);
        base.SetItem(index, item);
    }

    private void Validate(T item, int replaced)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (IndexOf(item) is >= 0 and var found && found != replaced)
            throw new ArgumentException("The same tab item cannot be added twice.", nameof(item));
    }
}
