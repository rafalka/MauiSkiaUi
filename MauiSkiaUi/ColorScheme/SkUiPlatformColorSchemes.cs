namespace MauiSkiaUi;

/// <summary>iOS's system colors (UIKit's systemBlue, systemBackground and label colors), light and dark.</summary>
/// <remarks>Mutable tokens as any scheme; the shared <see cref="Light"/> and <see cref="Dark"/> instances are for assigning to <see cref="SkUiColorScheme.Current"/>, new instances (or <see cref="WithAccent"/>) for customizing. The platform's grays are fixed colors here; dynamic type and vibrancy are not modelled.</remarks>
public sealed class IosSkUiColorScheme : SkUiColorScheme
{
    /// <summary>Shared light instance.</summary>
    public static IosSkUiColorScheme Light { get; } = new(false);

    /// <summary>Shared dark instance.</summary>
    public static IosSkUiColorScheme Dark { get; } = new(true);

    /// <summary>Creates a mutable iOS scheme: dark when <paramref name="isDark"/>.</summary>
    public IosSkUiColorScheme(bool isDark)
    {
        if (isDark)
        {
            Accent = Color.FromArgb("#0A84FF");
            DefaultBackground = Color.FromArgb("#000000");
            DefaultForeground = Color.FromArgb("#FFFFFF");
            Muted = Color.FromArgb("#98989F");
            TrackOff = Color.FromArgb("#39393D");
            Disabled = Color.FromArgb("#5A5A5F");
            Error = Color.FromArgb("#FF453A");
        }
        else
        {
            Accent = Color.FromArgb("#007AFF");
            DefaultBackground = Color.FromArgb("#FFFFFF");
            DefaultForeground = Color.FromArgb("#000000");
            Muted = Color.FromArgb("#8A8A8E");
            TrackOff = Color.FromArgb("#E9E9EA");
            Disabled = Color.FromArgb("#A5A5AA");
            Error = Color.FromArgb("#FF3B30");
        }
    }

    /// <summary>A new scheme like <see cref="Light"/> or <see cref="Dark"/> with another accent (the user's system accent, a brand color).</summary>
    public static IosSkUiColorScheme WithAccent(Color accent, bool isDark) => new(isDark) { Accent = accent };
}

/// <summary>macOS's system colors (the default blue accent, the window background and label colors), light and dark.</summary>
/// <remarks>Mutable tokens as any scheme; the shared <see cref="Light"/> and <see cref="Dark"/> instances are for assigning to <see cref="SkUiColorScheme.Current"/>, new instances (or <see cref="WithAccent"/>) for customizing. The user's accent color choice is not read; pass it to WithAccent.</remarks>
public sealed class MacSkUiColorScheme : SkUiColorScheme
{
    /// <summary>Shared light instance.</summary>
    public static MacSkUiColorScheme Light { get; } = new(false);

    /// <summary>Shared dark instance.</summary>
    public static MacSkUiColorScheme Dark { get; } = new(true);

    /// <summary>Creates a mutable macOS scheme: dark when <paramref name="isDark"/>.</summary>
    public MacSkUiColorScheme(bool isDark)
    {
        if (isDark)
        {
            Accent = Color.FromArgb("#0A84FF");
            DefaultBackground = Color.FromArgb("#1E1E1E");
            DefaultForeground = Color.FromArgb("#DFDFDF");
            Muted = Color.FromArgb("#8E8E8E");
            TrackOff = Color.FromArgb("#5A5A5A");
            Disabled = Color.FromArgb("#4E4E4E");
            Error = Color.FromArgb("#FF453A");
        }
        else
        {
            Accent = Color.FromArgb("#007AFF");
            DefaultBackground = Color.FromArgb("#ECECEC");
            DefaultForeground = Color.FromArgb("#262626");
            Muted = Color.FromArgb("#8C8C8C");
            TrackOff = Color.FromArgb("#D1D1D1");
            Disabled = Color.FromArgb("#A5A5A5");
            Error = Color.FromArgb("#FF3B30");
        }
    }

    /// <summary>A new scheme like <see cref="Light"/> or <see cref="Dark"/> with another accent (the user's system accent, a brand color).</summary>
    public static MacSkUiColorScheme WithAccent(Color accent, bool isDark) => new(isDark) { Accent = accent };
}

/// <summary>Material 3's baseline color roles (primary, surface, on-surface, outline), light and dark.</summary>
/// <remarks>Mutable tokens as any scheme; the shared <see cref="Light"/> and <see cref="Dark"/> instances are for assigning to <see cref="SkUiColorScheme.Current"/>, new instances (or <see cref="WithAccent"/>) for customizing. Android 12+ dynamic color is not read; pass the system's primary to WithAccent.</remarks>
public sealed class AndroidSkUiColorScheme : SkUiColorScheme
{
    /// <summary>Shared light instance.</summary>
    public static AndroidSkUiColorScheme Light { get; } = new(false);

    /// <summary>Shared dark instance.</summary>
    public static AndroidSkUiColorScheme Dark { get; } = new(true);

    /// <summary>Creates a mutable Material 3 scheme: dark when <paramref name="isDark"/>.</summary>
    public AndroidSkUiColorScheme(bool isDark)
    {
        if (isDark)
        {
            Accent = Color.FromArgb("#D0BCFF");
            DefaultBackground = Color.FromArgb("#141218");
            DefaultForeground = Color.FromArgb("#E6E0E9");
            Muted = Color.FromArgb("#CAC4D0");
            TrackOff = Color.FromArgb("#938F99");
            Disabled = Color.FromArgb("#49454F");
            Error = Color.FromArgb("#F2B8B5");
        }
        else
        {
            Accent = Color.FromArgb("#6750A4");
            DefaultBackground = Color.FromArgb("#FEF7FF");
            DefaultForeground = Color.FromArgb("#1D1B20");
            Muted = Color.FromArgb("#49454F");
            TrackOff = Color.FromArgb("#79747E");
            Disabled = Color.FromArgb("#B3B0B8");
            Error = Color.FromArgb("#B3261E");
        }
    }

    /// <summary>A new scheme like <see cref="Light"/> or <see cref="Dark"/> with another accent (the user's system accent, a brand color).</summary>
    public static AndroidSkUiColorScheme WithAccent(Color accent, bool isDark) => new(isDark) { Accent = accent };
}

/// <summary>Fluent's colors (the default blue accent, the solid background and text fill colors), light and dark.</summary>
/// <remarks>Mutable tokens as any scheme; the shared <see cref="Light"/> and <see cref="Dark"/> instances are for assigning to <see cref="SkUiColorScheme.Current"/>, new instances (or <see cref="WithAccent"/>) for customizing. The user's Windows accent color is not read; pass it to WithAccent.</remarks>
public sealed class WindowsSkUiColorScheme : SkUiColorScheme
{
    /// <summary>Shared light instance.</summary>
    public static WindowsSkUiColorScheme Light { get; } = new(false);

    /// <summary>Shared dark instance.</summary>
    public static WindowsSkUiColorScheme Dark { get; } = new(true);

    /// <summary>Creates a mutable Fluent scheme: dark when <paramref name="isDark"/>.</summary>
    public WindowsSkUiColorScheme(bool isDark)
    {
        if (isDark)
        {
            Accent = Color.FromArgb("#60CDFF");
            DefaultBackground = Color.FromArgb("#202020");
            DefaultForeground = Color.FromArgb("#FFFFFF");
            Muted = Color.FromArgb("#C5C5C5");
            TrackOff = Color.FromArgb("#9A9A9A");
            Disabled = Color.FromArgb("#5A5A5A");
            Error = Color.FromArgb("#FF99A4");
        }
        else
        {
            Accent = Color.FromArgb("#0067C0");
            DefaultBackground = Color.FromArgb("#F3F3F3");
            DefaultForeground = Color.FromArgb("#1A1A1A");
            Muted = Color.FromArgb("#5F5F5F");
            TrackOff = Color.FromArgb("#8A8A8A");
            Disabled = Color.FromArgb("#A19F9D");
            Error = Color.FromArgb("#C42B1C");
        }
    }

    /// <summary>A new scheme like <see cref="Light"/> or <see cref="Dark"/> with another accent (the user's system accent, a brand color).</summary>
    public static WindowsSkUiColorScheme WithAccent(Color accent, bool isDark) => new(isDark) { Accent = accent };
}
