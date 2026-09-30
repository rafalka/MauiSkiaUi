using MauiSkiaUi;

namespace MauiSkiaUiSamples;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaUi()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", SampleFonts.Regular);
                fonts.AddFont("OpenSans-Semibold.ttf", SampleFonts.Semibold);
                fonts.AddFont("RobotoMono-Regular.ttf", SampleFonts.Mono);
            });
        return builder.Build();
    }
}
