namespace MauiSkiaUiSamples;

public static class SampleFonts
{
    public const string Regular = "OpenSansRegular";
    public const string Semibold = "OpenSansSemibold";
    public const string Mono = "RobotoMono";

    /// <summary>
    /// The system monospace family, for drawn (SkiaUi) text: always installed, so no font registration is needed.
    /// </summary>
    public static string SystemMono =>
        OperatingSystem.IsAndroid() ? "monospace" : OperatingSystem.IsWindows() ? "Consolas" : "Menlo";
}

public static class SampleColors
{
    /// <summary>Navigation bar colors for a page (set per page: Mac Catalyst ignores them on the Shell alone).</summary>
    public static void ApplyNavigationBar(Page page)
    {
        Shell.SetBackgroundColor(page, Surface);
        Shell.SetForegroundColor(page, Accent);
        Shell.SetTitleColor(page, Ink);
    }

    public static readonly Color Page = Color.FromArgb("#F5F7F8");
    public static readonly Color Surface = Colors.White;
    public static readonly Color Ink = Color.FromArgb("#1F2933");
    public static readonly Color Caption = Color.FromArgb("#5B6770");
    public static readonly Color Border = Color.FromArgb("#D9E0E4");
    public static readonly Color Accent = Color.FromArgb("#087F83");
    public static readonly Color Code = Color.FromArgb("#9A3412");
}
