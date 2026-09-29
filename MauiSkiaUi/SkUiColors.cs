namespace MauiSkiaUi;

/// <summary>
/// Convenience accessors for the active <see cref="SkUiColorScheme.Current"/> tokens.
/// Prefer <see cref="SkUiColorScheme"/> when replacing packs or mutating individual colors.
/// </summary>
public static class SkUiColors
{
    /// <summary>Primary accent from the current color scheme.</summary>
    public static Color Accent => SkUiColorScheme.Current.Accent;

    /// <summary>Default surface background from the current color scheme.</summary>
    public static Color DefaultBackground => SkUiColorScheme.Current.DefaultBackground;

    /// <summary>Default text/icon foreground from the current color scheme.</summary>
    public static Color DefaultForeground => SkUiColorScheme.Current.DefaultForeground;

    /// <summary>Neutral outline/ring from the current color scheme.</summary>
    public static Color Muted => SkUiColorScheme.Current.Muted;

    /// <summary>Switch track-off color from the current color scheme.</summary>
    public static Color TrackOff => SkUiColorScheme.Current.TrackOff;

    /// <summary>Disabled fill from the current color scheme.</summary>
    public static Color Disabled => SkUiColorScheme.Current.Disabled;

    /// <summary>Linear mix of two colors (<paramref name="amount"/> 0 = <paramref name="from"/>, 1 = <paramref name="to"/>).</summary>
    internal static Color Mix(Color from, Color to, float amount = 0.5f) => new(
        from.Red + (to.Red - from.Red) * amount,
        from.Green + (to.Green - from.Green) * amount,
        from.Blue + (to.Blue - from.Blue) * amount,
        from.Alpha + (to.Alpha - from.Alpha) * amount);
}
