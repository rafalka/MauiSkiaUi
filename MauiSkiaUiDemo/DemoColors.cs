using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Demo app palette shared by gallery, stress, and component pages.</summary>
public static class DemoColors
{
    /// <summary>Primary body text and strong labels.</summary>
    public static readonly Color Ink = Color.FromArgb("#202A2C");

    /// <summary>Interactive accent; mirrors <see cref="SkUiColors.Accent"/>.</summary>
    public static readonly Color Accent = SkUiColors.Accent;

    /// <summary>Page and surface background.</summary>
    public static readonly Color PageBackground = Color.FromArgb("#F4F6F6");

    /// <summary>Secondary / caption text.</summary>
    public static readonly Color Caption = Color.FromArgb("#526164");

    /// <summary>Card and button border.</summary>
    public static readonly Color Border = SkUiColors.TrackOff;

    /// <summary>First alternate sample color in multi-cell demos.</summary>
    public static readonly Color SampleA = Color.FromArgb("#A12842");

    /// <summary>Second alternate sample color in multi-cell demos.</summary>
    public static readonly Color SampleB = Color.FromArgb("#285C9C");

    /// <summary>Soft content-view backdrop.</summary>
    public static readonly Color SoftSurface = Color.FromArgb("#DCE8EA");

    /// <summary>Property-check pass indicator.</summary>
    public static readonly Color Pass = Color.FromArgb("#14633D");

    /// <summary>Property-check fail indicator (same hue as <see cref="SampleA"/>).</summary>
    public static readonly Color Fail = SampleA;

    /// <summary>Stress-page alternating row fill.</summary>
    public static readonly Color StressAlt = Color.FromArgb("#E5EEEE");

    /// <summary>Main-page tap alternate fill.</summary>
    public static readonly Color TapAlternate = Color.FromArgb("#C54150");
}
