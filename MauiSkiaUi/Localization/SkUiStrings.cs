using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// Every text SkiaUi itself shows or gives screen readers, in one place, to translate them at once: the OK and Cancel buttons of
/// popups, the load-more button of collection views, and the states screen readers read (selected, expanded, collapsed).
/// Works as <see cref="SkUiLook"/>: one app-wide instance, <see cref="Current"/>, replaced as a whole or changed in place.
/// Texts the app passes in (a picker's <c>Title</c>, its own <c>AcceptText</c>) are not here.
/// </summary>
/// <remarks>
/// <para>Two ways to translate:</para>
/// <list type="bullet">
/// <item><b>Set the texts</b> on an instance and make it current, e.g. per language at startup or on a language switch:
/// <c>SkUiStrings.Current = new SkUiStrings { AcceptText = "Übernehmen", CancelText = "Abbrechen", … };</c> (also
/// <c>UseSkiaUi(options =&gt; options.Strings = …)</c>). Setting a text on the current instance applies like replacing it.</item>
/// <item><b>Derive</b> and override the getters to read the app's resources: they are read each time a text is needed (a popup
/// opens, a screen reader reads an element), so they follow <c>CultureInfo.CurrentUICulture</c>; call <see cref="NotifyChanged"/>
/// after switching the culture so texts already shown (a load-more button) and screen readers pick it up.</item>
/// </list>
/// <para>
/// Replacing <see cref="Current"/> or <see cref="NotifyChanged"/> raises <see cref="CurrentChanged"/>: live surfaces rebuild what
/// screen readers read, and drawn texts SkiaUi created (the load-more button) take the new text. An open popup keeps the texts it
/// opened with. Change the texts on the UI thread.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class AppSkUiStrings : SkUiStrings
/// {
///     public override string AcceptText =&gt; AppResources.Ok;
///     public override string CancelText =&gt; AppResources.Cancel;
///     public override string LoadMoreText =&gt; AppResources.LoadMore;
/// }
///
/// builder.UseSkiaUi(options =&gt; options.Strings = new AppSkUiStrings());
/// </code>
/// </example>
public class SkUiStrings
{
    private static readonly SkUiWeakEvent CurrentChangedEvent = new();
    private static SkUiStrings? _current;

    /// <summary>
    /// Raised after <see cref="Current"/> is replaced, a text of it is set, or <see cref="NotifyChanged"/> is called. Live surfaces
    /// then rebuild what screen readers read.
    /// </summary>
    /// <remarks>Subscribers are not kept alive by the event (a page that forgets to unsubscribe can still be collected).</remarks>
    public static event EventHandler? CurrentChanged
    {
        add => CurrentChangedEvent.Add(value);
        remove => CurrentChangedEvent.Remove(value);
    }

    /// <summary>The app-wide texts. Defaults to an instance with the English texts.</summary>
    public static SkUiStrings Current
    {
        get => _current ??= new SkUiStrings();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_current, value)) return;
            _current = value;
            NotifyChanged();
        }
    }

    /// <summary>
    /// Call after the texts changed without a new <see cref="Current"/> (a culture switch read by overridden getters): raises
    /// <see cref="CurrentChanged"/>. Setting a text on the current instance does this by itself.
    /// </summary>
    public static void NotifyChanged()
    {
        Version++;
        CurrentChangedEvent.Raise(null, EventArgs.Empty);
    }

    /// <summary>Counts the changes <see cref="NotifyChanged"/> reported: what caches texts (a calendar's day names) reads them again.</summary>
    internal static int Version { get; private set; }

    /// <summary>
    /// The OK button of popups whose choice waits to be accepted (a picker's <c>UpdateMode</c>
    /// <see cref="SkUiPickerUpdateMode.WhenFinished"/>), unless the picker sets its own <c>AcceptText</c>. Default "OK".
    /// </summary>
    public virtual string AcceptText { get; set => Set(ref field, value); } = "OK";

    /// <summary>
    /// The Cancel button of such popups (unless the picker sets its own <c>CancelText</c>), and of MAUI's action sheet
    /// (<see cref="SkUiActionSheetPopupPresenter"/>). Default "Cancel".
    /// </summary>
    public virtual string CancelText { get; set => Set(ref field, value); } = "Cancel";

    /// <summary>
    /// What screen readers call the close button (×) in the toolbar of a bottom pane (a picker's <c>PopupPlacement</c>
    /// <see cref="SkUiPopupPlacement.BottomPane"/>), unless the picker sets its own <c>AcceptText</c>. Default "Done".
    /// </summary>
    public virtual string DoneText { get; set => Set(ref field, value); } = "Done";

    /// <summary>The Clear button of popups (a picker's <c>IsPopupClearButtonVisible</c>), unless the picker sets its own <c>ClearText</c>. Default "Clear".</summary>
    public virtual string ClearText { get; set => Set(ref field, value); } = "Clear";

    /// <summary>A text field's clear button (<c>ClearButtonVisibility</c>, a search bar's) as screen readers read it, as iOS's. Default "Clear text".</summary>
    public virtual string ClearTextButtonText { get; set => Set(ref field, value); } = "Clear text";

    /// <summary>A password field's reveal button (<c>ShowsPasswordRevealButton</c>) as screen readers read it while the text is masked. Default "Show password".</summary>
    public virtual string ShowPasswordText { get; set => Set(ref field, value); } = "Show password";

    /// <summary>A password field's reveal button as screen readers read it while the text shows. Default "Hide password".</summary>
    public virtual string HidePasswordText { get; set => Set(ref field, value); } = "Hide password";

    /// <summary>
    /// The "Select all" row above a list with several choices (a picker's <c>IsSelectAllVisible</c>), unless the picker sets its own
    /// <c>SelectAllText</c>; and the item of a text field's edit menu where the platform's menu has no text of its own (Windows).
    /// Default "Select all".
    /// </summary>
    public virtual string SelectAllText { get; set => Set(ref field, value); } = "Select all";

    /// <summary>The placeholder of a picker's search field (<c>IsSearchEnabled</c>), unless the picker sets its own <c>SearchPlaceholder</c>. Default "Search".</summary>
    public virtual string SearchPlaceholderText { get; set => Set(ref field, value); } = "Search";

    /// <summary>What a picker's list shows when nothing matches its search. Default "No results".</summary>
    public virtual string NoResultsText { get; set => Set(ref field, value); } = "No results";

    /// <summary>The Cut item of a text field's edit menu where the platform's menu has no text of its own (Windows). Default "Cut".</summary>
    public virtual string CutText { get; set => Set(ref field, value); } = "Cut";

    /// <summary>The Copy item of a text field's edit menu (Windows). Default "Copy".</summary>
    public virtual string CopyText { get; set => Set(ref field, value); } = "Copy";

    /// <summary>The Paste item of a text field's edit menu (Windows). Default "Paste".</summary>
    public virtual string PasteText { get; set => Set(ref field, value); } = "Paste";

    /// <summary>
    /// The token that stands for the chosen items that do not fit in a picker's field (<c>MaxTokenLines</c>), a <c>string.Format</c>
    /// format with their count as <c>{0}</c>. Default "+{0}".
    /// </summary>
    public virtual string MoreTokensFormat { get; set => Set(ref field, value); } = "+{0}";

    /// <summary>
    /// What screen readers read after the item in a picker wheel's middle row: its place among the wheel's items, a
    /// <c>string.Format</c> format with the place (from 1) as <c>{0}</c> and the count as <c>{1}</c>. Default "{0} of {1}".
    /// </summary>
    public virtual string WheelPositionFormat { get; set => Set(ref field, value); } = "{0} of {1}";

    /// <summary>The header of a date picker's day wheel (<c>AreColumnHeadersVisible</c>). Default "Day".</summary>
    public virtual string DayText { get; set => Set(ref field, value); } = "Day";

    /// <summary>The header of a date picker's month wheel. Default "Month".</summary>
    public virtual string MonthText { get; set => Set(ref field, value); } = "Month";

    /// <summary>The header of a date picker's year wheel. Default "Year".</summary>
    public virtual string YearText { get; set => Set(ref field, value); } = "Year";

    /// <summary>The header of a time picker's hour wheel. Default "Hour".</summary>
    public virtual string HourText { get; set => Set(ref field, value); } = "Hour";

    /// <summary>The header of a time picker's minute wheel. Default "Minute".</summary>
    public virtual string MinuteText { get; set => Set(ref field, value); } = "Minute";

    /// <summary>The header of a time picker's second wheel. Default "Second".</summary>
    public virtual string SecondText { get; set => Set(ref field, value); } = "Second";

    /// <summary>The default load-more button of a collection view (<see cref="SkUiCollectionView.LoadMoreMode"/> <c>Manual</c>). Default "Load more".</summary>
    public virtual string LoadMoreText { get; set => Set(ref field, value); } = "Load more";

    /// <summary>What screen readers read as the value of a selected collection view item (or a chosen row of a picker with several choices). Default "Selected".</summary>
    public virtual string SelectedStateText { get; set => Set(ref field, value); } = "Selected";

    /// <summary>A calendar's week number with the first letter of "week" (<c>SkUiCalendarWeekNumberFormat.FirstLetter</c>), a <c>string.Format</c> format of the number. Default "W{0}".</summary>
    public virtual string WeekNumberLetterFormat { get; set => Set(ref field, value); } = "W{0}";

    /// <summary>A calendar's week number with the abbreviation of "week" (<c>SkUiCalendarWeekNumberFormat.Abbreviated</c>), a <c>string.Format</c> format of the number. Default "Wk {0}".</summary>
    public virtual string WeekNumberAbbreviatedFormat { get; set => Set(ref field, value); } = "Wk {0}";

    /// <summary>A calendar day that is today, as screen readers read it after the date. Default "Today".</summary>
    public virtual string TodayStateText { get; set => Set(ref field, value); } = "Today";

    /// <summary>A calendar day that cannot be chosen (outside the range, a blackout date), as screen readers read it after the date. Default "Unavailable".</summary>
    public virtual string UnavailableStateText { get; set => Set(ref field, value); } = "Unavailable";

    /// <summary>A calendar's button to the previous page (month, year, …), as screen readers read it. Default "Previous".</summary>
    public virtual string PreviousText { get; set => Set(ref field, value); } = "Previous";

    /// <summary>A calendar's button to the next page, as screen readers read it. Default "Next".</summary>
    public virtual string NextText { get; set => Set(ref field, value); } = "Next";

    /// <summary>What screen readers read after the header of an expanded expander or collection view group. Default "Expanded".</summary>
    public virtual string ExpandedStateText { get; set => Set(ref field, value); } = "Expanded";

    /// <summary>What screen readers read after the header of a collapsed expander or collection view group. Default "Collapsed".</summary>
    public virtual string CollapsedStateText { get; set => Set(ref field, value); } = "Collapsed";

    /// <summary>What screen readers read for a loading shimmer instead of its skeleton. Default "Loading".</summary>
    public virtual string LoadingText { get; set => Set(ref field, value); } = "Loading";

    /// <summary>The screen-reader action that moves a reorderable collection view item one place up. Default "Move up".</summary>
    public virtual string MoveUpText { get; set => Set(ref field, value); } = "Move up";

    /// <summary>The screen-reader action that moves a reorderable collection view item one place down. Default "Move down".</summary>
    public virtual string MoveDownText { get; set => Set(ref field, value); } = "Move down";

    /// <summary>The screen-reader action that moves a reorderable collection view item one place left. Default "Move left".</summary>
    public virtual string MoveLeftText { get; set => Set(ref field, value); } = "Move left";

    /// <summary>The screen-reader action that moves a reorderable collection view item one place right. Default "Move right".</summary>
    public virtual string MoveRightText { get; set => Set(ref field, value); } = "Move right";

    /// <summary>
    /// The day names a calendar shows above its columns in the <see cref="SkUiCalendarDayNameFormat.Abbreviated"/> format
    /// ("Mon"), seven names from Sunday; <c>null</c> (default): the current culture's.
    /// </summary>
    public virtual IReadOnlyList<string>? AbbreviatedDayNames { get; set => SetDayNames(ref field, value); }

    /// <summary>The day names in the <see cref="SkUiCalendarDayNameFormat.Shortest"/> format ("Mo"), seven from Sunday; <c>null</c> (default): the culture's.</summary>
    public virtual IReadOnlyList<string>? ShortestDayNames { get; set => SetDayNames(ref field, value); }

    /// <summary>The day names in the <see cref="SkUiCalendarDayNameFormat.FirstLetter"/> format ("M"), seven from Sunday; <c>null</c> (default): the first letter of the culture's shortest names.</summary>
    public virtual IReadOnlyList<string>? FirstLetterDayNames { get; set => SetDayNames(ref field, value); }

    /// <summary>
    /// The name of <paramref name="day"/> a calendar shows above its column in <paramref name="format"/>: the names set on
    /// this instance (<see cref="AbbreviatedDayNames"/>, <see cref="ShortestDayNames"/>, <see cref="FirstLetterDayNames"/>),
    /// else <paramref name="culture"/>'s. Override it to read the app's resources.
    /// </summary>
    public virtual string GetDayName(DayOfWeek day, SkUiCalendarDayNameFormat format, CultureInfo culture)
    {
        var names = format switch
        {
            SkUiCalendarDayNameFormat.Abbreviated => AbbreviatedDayNames,
            SkUiCalendarDayNameFormat.FirstLetter => FirstLetterDayNames,
            _ => ShortestDayNames
        };
        return names is not null ? names[(int)day] : SkUiCalendarGrid.DayName(day, format, culture);
    }

    private void SetDayNames(ref IReadOnlyList<string>? field, IReadOnlyList<string>? value)
    {
        if (value is not null && (value.Count != 7 || value.Any(name => name is null)))
            throw new ArgumentException("Seven day names, from Sunday, none of them null.", nameof(value));
        if (ReferenceEquals(field, value)) return;
        field = value is null ? null : [.. value];
        if (ReferenceEquals(_current, this))
            NotifyChanged();
    }

    /// <summary>The expanded or collapsed state's text.</summary>
    internal string ExpansionStateText(bool expanded) => expanded ? ExpandedStateText : CollapsedStateText;

    private void Set(ref string field, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        // Exact: an unchanged text needs no notification.
        if (field == value) return;
        field = value;
        if (ReferenceEquals(_current, this))
            NotifyChanged();
    }
}
