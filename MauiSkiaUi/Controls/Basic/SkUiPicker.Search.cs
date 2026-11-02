namespace MauiSkiaUi;

public partial class SkUiPicker
{
    /// <summary>Bindable property for <see cref="IsSearchEnabled"/>.</summary>
    public static readonly BindableProperty IsSearchEnabledProperty = BindableProperty.Create(nameof(IsSearchEnabled), typeof(bool), typeof(SkUiPicker), false,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Model.IsSearchEnabled = (bool)value);

    /// <summary>Bindable property for <see cref="SearchMinimumItemCount"/>.</summary>
    public static readonly BindableProperty SearchMinimumItemCountProperty = BindableProperty.Create(nameof(SearchMinimumItemCount), typeof(int), typeof(SkUiPicker), 0,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Model.SearchMinimumItemCount = (int)value);

    /// <summary>Bindable property for <see cref="SearchFilter"/>.</summary>
    public static readonly BindableProperty SearchFilterProperty = BindableProperty.Create(nameof(SearchFilter), typeof(Func<object?, string, bool>), typeof(SkUiPicker), null,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Model.SearchFilter = (Func<object?, string, bool>?)value);

    /// <summary>Bindable property for <see cref="SearchPlaceholder"/>.</summary>
    public static readonly BindableProperty SearchPlaceholderProperty = BindableProperty.Create(nameof(SearchPlaceholder), typeof(string), typeof(SkUiPicker), null,
        propertyChanged: (view, _, value) => ((SkUiPicker)view).Model.SearchPlaceholder = (string?)value);

    /// <summary>
    /// Whether the list's popup has a search field above the rows that filters them as the user types (default <c>false</c>), for
    /// at least <see cref="SearchMinimumItemCount"/> items. Typing on a keyboard while the list is open goes to it. A list the
    /// user can search shows as a list also where the look shows a wheel (<see cref="SkUiLook.PickerPaneShowsWheel"/>). Applies
    /// from the next opening. SkiaUi extension.
    /// </summary>
    public bool IsSearchEnabled { get => (bool)GetValue(IsSearchEnabledProperty); set => SetValue(IsSearchEnabledProperty, value); }

    /// <summary>
    /// The fewest items for which the search field shows (default 0: always, with <see cref="IsSearchEnabled"/>); a short list
    /// keeps its room for the rows. Not negative. SkiaUi extension.
    /// </summary>
    public int SearchMinimumItemCount { get => (int)GetValue(SearchMinimumItemCountProperty); set => SetValue(SearchMinimumItemCountProperty, value); }

    /// <summary>
    /// Whether an item matches the search phrase: the item (of <see cref="ItemsSource"/>, else a text of <see cref="Items"/>) and
    /// the phrase (trimmed, never empty). <c>null</c> (default): the item's text contains the phrase, ignoring case and accents.
    /// SkiaUi extension.
    /// </summary>
    public Func<object?, string, bool>? SearchFilter { get => (Func<object?, string, bool>?)GetValue(SearchFilterProperty); set => SetValue(SearchFilterProperty, value); }

    /// <summary>The search field's placeholder; <c>null</c> (default): <see cref="SkUiStrings.SearchPlaceholderText"/>. SkiaUi extension.</summary>
    public string? SearchPlaceholder { get => (string?)GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }

    /// <summary>Sets <see cref="IsSearchEnabled"/> (same as the property setter).</summary>
    public SkUiPicker SetIsSearchEnabled(bool value) { IsSearchEnabled = value; return this; }

    /// <summary>Sets <see cref="SearchMinimumItemCount"/>; throws when <paramref name="value"/> is negative.</summary>
    public SkUiPicker SetSearchMinimumItemCount(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        SearchMinimumItemCount = value;
        return this;
    }

    /// <summary>Sets <see cref="SearchFilter"/> (same as the property setter).</summary>
    public SkUiPicker SetSearchFilter(Func<object?, string, bool>? value) { SearchFilter = value; return this; }

    /// <summary>Sets <see cref="SearchPlaceholder"/> (same as the property setter).</summary>
    public SkUiPicker SetSearchPlaceholder(string? value) { SearchPlaceholder = value; return this; }
}
