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

    /// <summary>
    /// Enables XAML and standalone hosting for every SkUiView subclass, with app-wide options: the popup presenter drawn controls
    /// (pickers) show their popups with (<see cref="SkUiOptions.PopupPresenter"/>, the same as <see cref="SkUiPopups.Presenter"/>).
    /// </summary>
    public static MauiAppBuilder UseSkiaUi(this MauiAppBuilder builder, Action<SkUiOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new SkUiOptions();
        configure(options);
        if (options.PopupPresenter is { } presenter)
            SkUiPopups.Presenter = presenter;
        return builder.UseSkiaUi();
    }
}
#endif
