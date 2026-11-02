namespace MauiSkiaUi.Core;

public partial class SkUiCorePicker
{
    /// <summary>Whether the list's popup has a search field that filters the rows (default <c>false</c>; see <c>SkUiPicker.IsSearchEnabled</c>).</summary>
    public bool IsSearchEnabled { get => Model.IsSearchEnabled; set => SetIsSearchEnabled(value); }

    /// <summary>The fewest items for which the search field shows (default 0: always; see <c>SkUiPicker.SearchMinimumItemCount</c>).</summary>
    public int SearchMinimumItemCount { get => Model.SearchMinimumItemCount; set => SetSearchMinimumItemCount(value); }

    /// <summary>
    /// Whether an item matches the search phrase; <c>null</c> (default): the item's text contains the phrase, ignoring case and
    /// accents (see <c>SkUiPicker.SearchFilter</c>).
    /// </summary>
    public Func<object?, string, bool>? SearchFilter { get => Model.SearchFilter; set => SetSearchFilter(value); }

    /// <summary>The search field's placeholder; <c>null</c> (default): <see cref="SkUiStrings.SearchPlaceholderText"/>.</summary>
    public string? SearchPlaceholder { get => Model.SearchPlaceholder; set => SetSearchPlaceholder(value); }

    /// <summary>Sets <see cref="IsSearchEnabled"/>.</summary>
    public SkUiCorePicker SetIsSearchEnabled(bool value)
    {
        Update(ref Model.IsSearchEnabled, value, nameof(IsSearchEnabled), paint: false);
        return this;
    }

    /// <summary>Sets <see cref="SearchMinimumItemCount"/>; throws when <paramref name="value"/> is negative.</summary>
    public SkUiCorePicker SetSearchMinimumItemCount(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Update(ref Model.SearchMinimumItemCount, value, nameof(SearchMinimumItemCount), paint: false);
        return this;
    }

    /// <summary>Sets <see cref="SearchFilter"/>.</summary>
    public SkUiCorePicker SetSearchFilter(Func<object?, string, bool>? value)
    {
        Update(ref Model.SearchFilter, value, nameof(SearchFilter), paint: false);
        return this;
    }

    /// <summary>Sets <see cref="SearchPlaceholder"/>.</summary>
    public SkUiCorePicker SetSearchPlaceholder(string? value)
    {
        Update(ref Model.SearchPlaceholder, value, nameof(SearchPlaceholder), paint: false);
        return this;
    }
}
