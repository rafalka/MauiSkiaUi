using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// The library's look comparison page, set up from the environment for scripted runs: <c>SKUI_LOOK</c> picks a look by
/// name (as the page's picker lists them), <c>SKUI_APP_STYLES=0</c> shows the platform defaults and <c>SKUI_OVERLAY=1</c>
/// draws each SkiaUi control over the native one, <c>SKUI_LOOK_COLORS=1</c> makes the look's color scheme current and
/// <c>SKUI_TEXT_RENDERING</c> (<c>Auto</c>, <c>Shaped</c>, <c>Simple</c>) sets <see cref="SkUiTextOptions.DefaultRendering"/>.
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
        if (Environment.GetEnvironmentVariable("SKUI_LOOK_COLORS") == "1")
            UseLookColors = true;
        if (Enum.TryParse<SkUiTextRendering>(Environment.GetEnvironmentVariable("SKUI_TEXT_RENDERING"), ignoreCase: true, out var rendering))
            SkUiTextOptions.DefaultRendering = rendering;
    }
}
