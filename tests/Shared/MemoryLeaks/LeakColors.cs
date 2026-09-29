namespace MauiSkiaUi.LeakTests;

/// <summary>Colors for leak scenarios and the device test page (no dependency on the demo app).</summary>
public static class LeakColors
{
    public static readonly Color Ink = Color.FromArgb("#202A2C");
    public static readonly Color Caption = Color.FromArgb("#526164");
    public static readonly Color Accent = SkUiColors.Accent;
    public static readonly Color Border = SkUiColors.TrackOff;
    public static readonly Color PageBackground = Color.FromArgb("#F4F6F6");
    public static readonly Color Surface = Color.FromArgb("#DCE8EA");
    public static readonly Color SurfaceAlt = Color.FromArgb("#E5EEEE");
    public static readonly Color SampleA = Color.FromArgb("#A12842");
    public static readonly Color SampleB = Color.FromArgb("#285C9C");
    public static readonly Color Pass = Color.FromArgb("#14633D");
    public static readonly Color Fail = SampleA;
}
