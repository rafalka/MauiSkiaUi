using System.ComponentModel;

namespace MauiSkiaUi;

/// <summary>
/// The overflow tile's binding context on <see cref="SkUiTileLayout"/> and <see cref="Core.SkUiCoreTileLayout"/>: how many
/// children did not fit the row limit, for a "+5" over the last tile.
/// </summary>
public sealed class SkUiTileOverflow : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs HiddenCountChanged = new(nameof(HiddenCount));

    /// <summary>Visible children past <c>MaximumRows</c>, neither measured nor drawn; 0 while everything fits.</summary>
    public int HiddenCount { get; private set; }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void SetHiddenCount(int value)
    {
        if (HiddenCount == value) return;
        HiddenCount = value;
        PropertyChanged?.Invoke(this, HiddenCountChanged);
    }
}
