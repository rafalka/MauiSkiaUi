using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using MauiSkiaUi;
using MauiSkiaUi.CommunityToolkit;
using MauiSkiaUiTour;
#if MAUI_DEVFLOW
using Microsoft.Maui.DevFlow.Agent;
#endif

namespace MauiSkiaUiDemo;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		PageTour.HookExceptionsIfRequested();
		DemoTrace.StartIfRequested();
		EditorHelp.Preload();
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.UseSkiaUi(options => options.Look = DefaultSkUiLook.ForCurrentPlatform())
			// Drawn pickers show their lists in the toolkit's popups.
			.UseSkiaUiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", DemoFonts.OpenSansRegular);
				fonts.AddFont("OpenSans-Semibold.ttf", DemoFonts.OpenSansSemibold);
				fonts.AddFont("Lobster-Regular.ttf", DemoFonts.Lobster);
				fonts.AddFont("RobotoMono-Regular.ttf", DemoFonts.RobotoMono);
			});

#if MAUI_DEVFLOW
		builder.AddMauiDevFlowAgent(SkiaUiDevFlowExtension.Register);
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
