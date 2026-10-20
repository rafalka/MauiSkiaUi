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
    /// (pickers) show their popups with (<see cref="SkUiOptions.PopupPresenter"/>, the same as <see cref="SkUiPopups.Presenter"/>)
    /// the look (<see cref="SkUiOptions.Look"/>, the same as <see cref="SkUiLook.Current"/>) and SkiaUi's own texts
    /// (<see cref="SkUiOptions.Strings"/>, the same as <see cref="SkUiStrings.Current"/>), and whether UI tests find every drawn
    /// <c>AutomationId</c> (<see cref="SkUiOptions.ExposeAutomationIds"/>).
    /// </summary>
    public static MauiAppBuilder UseSkiaUi(this MauiAppBuilder builder, Action<SkUiOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new SkUiOptions();
        configure(options);
        if (options.PopupPresenter is { } presenter)
            SkUiPopups.Presenter = presenter;
        if (options.Look is { } look)
            SkUiLook.Current = look;
        if (options.Strings is { } strings)
            SkUiStrings.Current = strings;
        if (options.ExposeAutomationIds is { } exposeAutomationIds)
            SkUiAccessibility.ExposeAutomationIds = exposeAutomationIds;
        return builder.UseSkiaUi();
    }
}
#endif
