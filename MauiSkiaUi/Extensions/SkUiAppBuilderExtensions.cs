#if ANDROID || IOS || MACCATALYST || WINDOWS
using Microsoft.Maui.Hosting;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace MauiSkiaUi;

/// <summary>Registers the SkiaSharp surfaces and the standalone SkiaUi handler.</summary>
public static class SkUiAppBuilderExtensions
{
    /// <summary>Enables XAML and standalone hosting for every SkUiView subclass.</summary>
    public static MauiAppBuilder UseSkiaUi(this MauiAppBuilder builder)
    {
        builder.UseSkiaSharp();
        builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<SkUiView, SkUiViewHandler>());
        return builder;
    }
}
#endif