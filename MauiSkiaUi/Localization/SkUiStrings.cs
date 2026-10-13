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
    public static void NotifyChanged() => CurrentChangedEvent.Raise(null, EventArgs.Empty);

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

    /// <summary>The Clear button of popups (a picker's <c>IsPopupClearButtonVisible</c>), unless the picker sets its own <c>ClearText</c>. Default "Clear".</summary>
    public virtual string ClearText { get; set => Set(ref field, value); } = "Clear";

    /// <summary>The "Select all" row above a list with several choices (a picker's <c>IsSelectAllVisible</c>), unless the picker sets its own <c>SelectAllText</c>. Default "Select all".</summary>
    public virtual string SelectAllText { get; set => Set(ref field, value); } = "Select all";

    /// <summary>
    /// The token that stands for the chosen items that do not fit in a picker's field (<c>MaxTokenLines</c>), a <c>string.Format</c>
    /// format with their count as <c>{0}</c>. Default "+{0}".
    /// </summary>
    public virtual string MoreTokensFormat { get; set => Set(ref field, value); } = "+{0}";

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

    /// <summary>What screen readers read after the header of an expanded expander or collection view group. Default "Expanded".</summary>
    public virtual string ExpandedStateText { get; set => Set(ref field, value); } = "Expanded";

    /// <summary>What screen readers read after the header of a collapsed expander or collection view group. Default "Collapsed".</summary>
    public virtual string CollapsedStateText { get; set => Set(ref field, value); } = "Collapsed";

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
