using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// The library's look comparison page, set up from the environment for scripted runs: <c>SKUI_LOOK</c> picks a look by
/// name (as the page's picker lists them), <c>SKUI_APP_STYLES=0</c> shows the platform defaults and <c>SKUI_OVERLAY=1</c>
/// draws each SkiaUi control over the native one.
/// </summary>
public sealed class LookComparisonPage : SkUiLookComparisonPage
{
    public LookComparisonPage()
    {
        if (Environment.GetEnvironmentVariable("SKUI_LOOK") is { Length: > 0 } look)
            SelectLook(look);
        if (Environment.GetEnvironmentVariable("SKUI_APP_STYLES") == "0")
            ShowAppStyles = false;
        if (Environment.GetEnvironmentVariable("SKUI_OVERLAY") == "1")
            Overlay = true;
    }
}
