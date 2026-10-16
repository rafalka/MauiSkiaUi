namespace MauiSkiaUiDemo;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		// The demo's pages are light: keep native controls (and system chrome) light in dark mode too, or MAUI's styles draw
		// their text white on the white pages. Drawn controls take their colors from SkUiColorScheme, not the system theme.
		UserAppTheme = AppTheme.Light;
		_ = DemoFonts.PreloadAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}