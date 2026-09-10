namespace MauiSkiaUiDemo;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		_ = DemoFonts.PreloadAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}