namespace MauiSkiaUi;

/// <summary>
/// Shared default palette for Core and MAUI-compatible SkiaUi controls (FR-19).
/// Replace <see cref="Current"/> for a full pack (e.g. light/dark), or mutate individual tokens
/// such as <see cref="Accent"/>. This is a <b>color scheme</b>, not control look (FR-18) and not
/// MAUI <c>Style</c>/VSM (FR-12).
/// </summary>
/// <remarks>
/// Explicit control property values (fluent <c>Set*</c>, XAML, or MAUI styles) win over scheme
/// defaults. Instance fields snapshot <see cref="Current"/> at construction; paint-time reads of
/// tokens via <see cref="SkUiColors"/> or <see cref="Current"/> pick up later scheme changes on the
/// next paint. After swapping <see cref="Current"/>, invalidate the SkiaUi tree (measure/paint).
/// </remarks>
public class SkUiColorScheme
{
    private static SkUiColorScheme? _current;
    private Color _accent = Color.FromArgb("#087F83");
    private Color _defaultBackground = Colors.White;
    private Color _defaultForeground = Colors.Black;
    private Color _muted = Color.FromArgb("#8A9A9C");
    private Color _trackOff = Color.FromArgb("#C5D4D6");
    private Color _disabled = Color.FromArgb("#596467");
    private Color _error = Color.FromArgb("#B3261E");
    // Control tokens: null follows the scheme's fallback (another token or a fixed color).
    private Color? _buttonBackground;
    private Color? _buttonForeground;
    private Color? _buttonBorder;
    private Color? _activityIndicator;

    private static readonly SkUiWeakEvent _currentChanged = new();
    private readonly SkUiWeakEvent _changed = new();

    /// <summary>Raised after <see cref="Current"/> is replaced.</summary>
    /// <remarks>Subscribers are not kept alive by the event (a page that forgets to unsubscribe can still be collected).</remarks>
    public static event EventHandler? CurrentChanged
    {
        add => _currentChanged.Add(value);
        remove => _currentChanged.Remove(value);
    }

    /// <summary>Raised after any token on this instance changes.</summary>
    /// <remarks>Subscribers are not kept alive by the event: the built-in schemes live for the whole app.</remarks>
    public event EventHandler? Changed
    {
        add => _changed.Add(value);
        remove => _changed.Remove(value);
    }

    /// <summary>
    /// Active app-wide scheme. Defaults to <see cref="LightSkUiColorScheme.Instance"/>.
    /// Setting a new instance raises <see cref="CurrentChanged"/>.
    /// </summary>
    public static SkUiColorScheme Current
    {
        get => _current ??= LightSkUiColorScheme.Instance;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_current, value)) return;
            _current = value;
            _currentChanged.Raise(null, EventArgs.Empty);
        }
    }

    /// <summary>Primary accent (buttons, toggle on-state, interactive chrome).</summary>
    public virtual Color Accent
    {
        get => _accent;
        set => SetToken(ref _accent, value);
    }

    /// <summary>Default surface / page background.</summary>
    public virtual Color DefaultBackground
    {
        get => _defaultBackground;
        set => SetToken(ref _defaultBackground, value);
    }

    /// <summary>Default text and icon foreground.</summary>
    public virtual Color DefaultForeground
    {
        get => _defaultForeground;
        set => SetToken(ref _defaultForeground, value);
    }

    /// <summary>Neutral outline/ring when a toggle is unchecked.</summary>
    public virtual Color Muted
    {
        get => _muted;
        set => SetToken(ref _muted, value);
    }

    /// <summary>Switch track color while toggled off.</summary>
    public virtual Color TrackOff
    {
        get => _trackOff;
        set => SetToken(ref _trackOff, value);
    }

    /// <summary>Fill when a control is disabled or cannot receive a tap.</summary>
    public virtual Color Disabled
    {
        get => _disabled;
        set => SetToken(ref _disabled, value);
    }

    /// <summary>Invalid input: a text field's error outline and message (<c>HasError</c>, <c>ErrorText</c>).</summary>
    public virtual Color Error
    {
        get => _error;
        set => SetToken(ref _error, value);
    }

    /// <summary>
    /// A button's fill when it sets none (read when the button is created). Until set, the scheme's
    /// (<see cref="Accent"/> by default; iOS and the Mac: transparent, as a system button; Windows: Fluent's light control fill).
    /// </summary>
    public virtual Color ButtonBackground
    {
        get => _buttonBackground ?? ButtonBackgroundFallback;
        set => SetControlToken(ref _buttonBackground, value);
    }

    /// <summary>
    /// A button's text color when it sets none (read when the button is created). Until set, the scheme's (white by default;
    /// iOS and the Mac: <see cref="Accent"/>; Windows: <see cref="DefaultForeground"/>).
    /// </summary>
    public virtual Color ButtonForeground
    {
        get => _buttonForeground ?? ButtonForegroundFallback;
        set => SetControlToken(ref _buttonForeground, value);
    }

    /// <summary>
    /// A button's border color when it sets none, drawn <see cref="SkUiLook.DefaultButtonBorderWidth"/> wide (read when the
    /// button is created). Until set, the scheme's (transparent by default; Windows: Fluent's faint control stroke).
    /// </summary>
    public virtual Color ButtonBorder
    {
        get => _buttonBorder ?? ButtonBorderFallback;
        set => SetControlToken(ref _buttonBorder, value);
    }

    /// <summary>
    /// An activity indicator's color when it sets none (read when it is created). Until set, the scheme's (<see cref="Muted"/>
    /// by default, as iOS's gray spinner; Android and Windows: <see cref="Accent"/>).
    /// </summary>
    public virtual Color ActivityIndicator
    {
        get => _activityIndicator ?? ActivityIndicatorFallback;
        set => SetControlToken(ref _activityIndicator, value);
    }

    /// <summary><see cref="ButtonBackground"/> until it is set: a token it follows, or a fixed color.</summary>
    private protected virtual Color ButtonBackgroundFallback => Accent;

    /// <summary><see cref="ButtonForeground"/> until it is set.</summary>
    private protected virtual Color ButtonForegroundFallback => Colors.White;

    /// <summary><see cref="ButtonBorder"/> until it is set.</summary>
    private protected virtual Color ButtonBorderFallback => Colors.Transparent;

    /// <summary><see cref="ActivityIndicator"/> until it is set.</summary>
    private protected virtual Color ActivityIndicatorFallback => Muted;

    /// <summary>
    /// Copies token values from <paramref name="source"/> into this instance. Control tokens that follow another one in the
    /// source (<see cref="ButtonBackground"/> following <see cref="Accent"/>) are copied as the color they have now.
    /// </summary>
    public virtual void CopyFrom(SkUiColorScheme source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Accent = source.Accent;
        DefaultBackground = source.DefaultBackground;
        DefaultForeground = source.DefaultForeground;
        Muted = source.Muted;
        TrackOff = source.TrackOff;
        Disabled = source.Disabled;
        Error = source.Error;
        ButtonBackground = source.ButtonBackground;
        ButtonForeground = source.ButtonForeground;
        ButtonBorder = source.ButtonBorder;
        ActivityIndicator = source.ActivityIndicator;
    }

    private void SetToken(ref Color field, Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (Equals(field, value)) return;
        field = value;
        _changed.Raise(this, EventArgs.Empty);
    }

    private void SetControlToken(ref Color? field, Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (Equals(field, value)) return;
        field = value;
        _changed.Raise(this, EventArgs.Empty);
    }
}

/// <summary>Built-in light palette (historical <c>SkUiColors</c> defaults).</summary>
public sealed class LightSkUiColorScheme : SkUiColorScheme
{
    /// <summary>Shared light instance used as the process default.</summary>
    public static LightSkUiColorScheme Instance { get; } = new();

    /// <summary>Creates a mutable light pack (safe to customize without touching <see cref="Instance"/>).</summary>
    public LightSkUiColorScheme()
    {
        Accent = Color.FromArgb("#087F83");
        DefaultBackground = Colors.White;
        DefaultForeground = Colors.Black;
        Muted = Color.FromArgb("#8A9A9C");
        TrackOff = Color.FromArgb("#C5D4D6");
        Disabled = Color.FromArgb("#596467");
        Error = Color.FromArgb("#B3261E");
    }
}

/// <summary>Built-in dark palette for dark surfaces.</summary>
public sealed class DarkSkUiColorScheme : SkUiColorScheme
{
    /// <summary>Shared dark instance.</summary>
    public static DarkSkUiColorScheme Instance { get; } = new();

    /// <summary>Creates a mutable dark pack.</summary>
    public DarkSkUiColorScheme()
    {
        Accent = Color.FromArgb("#2BB4B8");
        DefaultBackground = Color.FromArgb("#121A1B");
        DefaultForeground = Color.FromArgb("#E8F1F2");
        Muted = Color.FromArgb("#7A8E90");
        TrackOff = Color.FromArgb("#3A4A4C");
        Disabled = Color.FromArgb("#4A585A");
        Error = Color.FromArgb("#F2B8B5");
    }
}
