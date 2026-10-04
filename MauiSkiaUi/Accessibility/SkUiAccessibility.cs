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

    /// <summary>Raised after <see cref="IsEnabled"/> changes (live surfaces attach or remove their platform bridges). Subscribers are not kept alive.</summary>
    public static event EventHandler? Changed
    {
        add => _changed.Add(value);
        remove => _changed.Remove(value);
    }
}
