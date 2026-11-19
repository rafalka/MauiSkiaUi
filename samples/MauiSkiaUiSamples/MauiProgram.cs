using CommunityToolkit.Maui;
using MauiSkiaUi;
using MauiSkiaUi.CommunityToolkit;
using MauiSkiaUi.Markdown.Math;
using MauiSkiaUiTour;
#if MAUI_DEVFLOW
using Microsoft.Maui.DevFlow.Agent;
using SkiaUiDevFlow;
#endif

namespace MauiSkiaUiSamples;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        PageTour.HookExceptionsIfRequested();
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            // Drawn pickers open their lists in the toolkit's popups (the SkiaUi.Maui.CommunityToolkit package), app-wide.
            .UseMauiCommunityToolkit()
            .UseSkiaUi()
            .UseSkiaUiCommunityToolkit()
            // Formulas in Markdown views (the SkiaUi.Maui.Markdown.Math package), app-wide.
            .UseSkiaUiMarkdownMath()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", SampleFonts.Regular);
                fonts.AddFont("OpenSans-Semibold.ttf", SampleFonts.Semibold);
                fonts.AddFont("RobotoMono-Regular.ttf", SampleFonts.Mono);
                // Only ConfigureFonts, no SkUiFonts.Register: drawn text resolves these aliases too (AppFontsSample).
                fonts.AddFont("Lobster-Regular.ttf", "Lobster");
            });
#if MAUI_DEVFLOW
        // Debug: DevFlow drives the app; SkiaUi's extension lists and taps the drawn controls (docs/DevFlow.md).
        builder.AddMauiDevFlowAgent(SkiaUiDevFlowExtension.Register);
#endif
        return builder.Build();
    }
}
