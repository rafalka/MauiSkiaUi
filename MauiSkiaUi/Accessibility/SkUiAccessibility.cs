using MauiSkiaUi.Core;

namespace MauiSkiaUi;

/// <summary>How a screen reader reads a change of an element (<see cref="SkUiAccessibility.LiveRegionProperty"/>), as ARIA's <c>aria-live</c>.</summary>
public enum SkUiLiveRegion
{
    /// <summary>Changes are not read (default): the user hears the new text when they move to the element.</summary>
    None,
    /// <summary>Changes are read when the screen reader is done speaking (a status line, a counter).</summary>
    Polite,
    /// <summary>Changes are read at once, interrupting what is being read (an error, a warning).</summary>
    Assertive
}

/// <summary>
/// App-wide switch for drawn accessibility: what screen readers read (TalkBack, VoiceOver, Narrator) and keyboard focus of
/// drawn controls. On by default; it costs nothing while no assistive technology reads a surface. Switch it off only for
/// apps that guarantee none is used (kiosks, games): surfaces then read as single native views and drawn controls take no
/// keyboard focus. Per surface or subtree, use <see cref="SkUiView.IsAccessibilityEnabled"/>. Font scaling
/// (<see cref="SkUiFontScaling"/>, <see cref="SkUiLook.FontScale"/>) is not affected.
/// </summary>
public static class SkUiAccessibility
{
    private static readonly SkUiWeakEvent _changed = new();
    private static bool _isEnabled = true;

    /// <summary>Whether drawn accessibility (semantics and keyboard focus) is on. Default <c>true</c>.</summary>
    public static bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            SkUiFocusManager.ValidateAll();
            _changed.Raise(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Whether drawn nodes with an <c>AutomationId</c> that screen readers do not read as elements of their own are still
    /// exposed with their id, so UI-test drivers (Appium, XCUITest, WinAppDriver) find every id the app set: nodes absorbed
    /// into an actionable or described ancestor (a status label inside a tappable row), decorative ones (a plain layout, an
    /// image without a description) and ones with <c>AutomationProperties.IsInAccessibleTree="False"</c>. Each becomes an
    /// id-only element: no name, no actions, not focusable; its text is still read as part of the element that absorbs it.
    /// Default <c>false</c>: TalkBack skips id-only elements and VoiceOver does not get them while it runs, but they cost an
    /// element each and other assistive clients may list them. The package is a Release build and cannot tell the app's
    /// configuration, so turn it on in the app's UI-test builds (<c>UseSkiaUi(o =&gt; o.ExposeAutomationIds = true)</c>
    /// under the app's own symbol); surfaces read after a change follow it. Ids that need no option: a collection item's root id is taken by
    /// its item container when that absorbs it (a tappable or selectable item), and a surface root's id is on its native view.
    /// </summary>
    public static bool ExposeAutomationIds { get; set; }

    /// <summary>
    /// Appended to a text field's or picker's <c>AutomationId</c> for its clear button's element, while the button shows: an entry
    /// with <c>AutomationId="Search"</c> has its clear button at <c>Search_Clear</c>. Without an id on the control the button has none.
    /// </summary>
    public const string ClearButtonAutomationIdSuffix = "_Clear";

    /// <summary>
    /// Appended to a password field's <c>AutomationId</c> for its reveal button's element (<c>ShowsPasswordRevealButton</c>): an entry
    /// with <c>AutomationId="Password"</c> has its reveal button at <c>Password_Reveal</c>.
    /// </summary>
    public const string PasswordRevealButtonAutomationIdSuffix = "_Reveal";

    /// <summary>
    /// Attached: makes a drawn view a live region, so a screen reader reads its new text (its name and value) when it changes
    /// without the user moving to it: "Clocked in at 9:02", a timer, a form's status. On a container it applies to the elements
    /// inside. A change is read only while the element shows (not hidden, scrolled out or behind a modal pane); quick changes are
    /// coalesced, so the last text within <see cref="LiveRegionDelay"/> is read once. An element that appears with a region set
    /// is read once. TalkBack reads it as a live region, VoiceOver as an announcement (queued when polite) and Narrator through
    /// the element's <c>LiveSetting</c>. Core nodes: <see cref="SkUiCoreNode.SetLiveRegion"/>. Costs nothing while no screen
    /// reader runs.
    /// </summary>
    public static readonly BindableProperty LiveRegionProperty = BindableProperty.CreateAttached("LiveRegion", typeof(SkUiLiveRegion),
        typeof(SkUiAccessibility), SkUiLiveRegion.None, validateValue: static (_, value) => value is SkUiLiveRegion region && Enum.IsDefined(region));

    /// <summary>The view's <see cref="LiveRegionProperty"/>.</summary>
    public static SkUiLiveRegion GetLiveRegion(BindableObject view) => (SkUiLiveRegion)view.GetValue(LiveRegionProperty);

    /// <summary>Sets the view's <see cref="LiveRegionProperty"/>.</summary>
    public static void SetLiveRegion(BindableObject view, SkUiLiveRegion value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Not a defined live region.");
        view.SetValue(LiveRegionProperty, value);
    }

    /// <summary>
    /// Attached: the element's value changes often (a running timer, a download): VoiceOver gets UIKit's
    /// <c>UpdatesFrequently</c> trait (it polls the value instead of reading every change; progress bars have it already), and
    /// TalkBack is told about its changes at most once a second. Core nodes: <see cref="SkUiCoreNode.SetUpdatesFrequently"/>.
    /// </summary>
    public static readonly BindableProperty UpdatesFrequentlyProperty = BindableProperty.CreateAttached("UpdatesFrequently", typeof(bool),
        typeof(SkUiAccessibility), false);

    /// <summary>The view's <see cref="UpdatesFrequentlyProperty"/>.</summary>
    public static bool GetUpdatesFrequently(BindableObject view) => (bool)view.GetValue(UpdatesFrequentlyProperty);

    /// <summary>Sets the view's <see cref="UpdatesFrequentlyProperty"/>.</summary>
    public static void SetUpdatesFrequently(BindableObject view, bool value) => view.SetValue(UpdatesFrequentlyProperty, value);

    /// <summary>How long a live region's changes are gathered before the last text is read (300 ms).</summary>
    public static TimeSpan LiveRegionDelay => SkUiSemanticsOwner.LiveRegionDelay;

    /// <summary>
    /// Reads <paramref name="text"/> with the screen reader of <paramref name="anchor"/>'s surface, tied to no element ("Saved",
    /// "3 results"): VoiceOver's announcement, Narrator's notification, and on Android a hidden live region of the surface (the
    /// deprecated <c>announceForAccessibility</c> is not used). Nothing happens while no screen reader reads the surface. Prefer
    /// a <see cref="LiveRegionProperty"/> on the element that shows the text when there is one.
    /// </summary>
    public static void Announce(SkUiView anchor, string text, SkUiLiveRegion politeness = SkUiLiveRegion.Polite) =>
        AnnounceCore(anchor, text, politeness);

    /// <inheritdoc cref="Announce(SkUiView, string, SkUiLiveRegion)"/>
    public static void Announce(SkUiCoreNode anchor, string text, SkUiLiveRegion politeness = SkUiLiveRegion.Polite) =>
        AnnounceCore(anchor, text, politeness);

    private static void AnnounceCore(Rendering.ISkUiRenderable anchor, string text, SkUiLiveRegion politeness)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(text);
        if (politeness == SkUiLiveRegion.None || string.IsNullOrWhiteSpace(text))
            return;
        if (!Enum.IsDefined(politeness))
            throw new ArgumentOutOfRangeException(nameof(politeness), politeness, "Not a defined live region.");
        SkUiSemantics.RootOf(anchor)?.SemanticsOwner?.Announce(text.Trim(), politeness);
    }

    /// <summary>Raised after <see cref="IsEnabled"/> changes (live surfaces attach or remove their platform bridges). Subscribers are not kept alive.</summary>
    public static event EventHandler? Changed
    {
        add => _changed.Add(value);
        remove => _changed.Remove(value);
    }
}
