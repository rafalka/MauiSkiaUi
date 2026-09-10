namespace MauiSkiaUi;

/// <summary>Shared default palette used by SkiaUi controls for fills, tracks, and chrome.</summary>
public static class SkUiColors
{
    /// <summary>Primary accent used for button fills, toggle on-state, and interactive chrome.</summary>
    public static readonly Color Accent = Color.FromArgb("#087F83");

    /// <summary>Neutral outline/ring when a toggle is unchecked.</summary>
    public static readonly Color Muted = Color.FromArgb("#8A9A9C");

    /// <summary>Switch track color while toggled off.</summary>
    public static readonly Color TrackOff = Color.FromArgb("#C5D4D6");

    /// <summary>Button fill when disabled or otherwise unable to receive a tap.</summary>
    public static readonly Color Disabled = Color.FromArgb("#596467");
}
