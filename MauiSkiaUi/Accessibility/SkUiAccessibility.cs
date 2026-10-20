namespace MauiSkiaUi;

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

    /// <summary>Raised after <see cref="IsEnabled"/> changes (live surfaces attach or remove their platform bridges). Subscribers are not kept alive.</summary>
    public static event EventHandler? Changed
    {
        add => _changed.Add(value);
        remove => _changed.Remove(value);
    }
}
