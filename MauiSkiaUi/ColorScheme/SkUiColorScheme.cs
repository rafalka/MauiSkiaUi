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

    /// <summary>Raised after <see cref="Current"/> is replaced.</summary>
    public static event EventHandler? CurrentChanged;

    /// <summary>Raised after any token on this instance changes.</summary>
    public event EventHandler? Changed;

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
            CurrentChanged?.Invoke(null, EventArgs.Empty);
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

    /// <summary>Copies token values from <paramref name="source"/> into this instance.</summary>
    public virtual void CopyFrom(SkUiColorScheme source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Accent = source.Accent;
        DefaultBackground = source.DefaultBackground;
        DefaultForeground = source.DefaultForeground;
        Muted = source.Muted;
        TrackOff = source.TrackOff;
        Disabled = source.Disabled;
    }

    private void SetToken(ref Color field, Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (Equals(field, value)) return;
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
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
    }
}
