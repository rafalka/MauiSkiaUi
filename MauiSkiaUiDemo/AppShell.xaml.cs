namespace MauiSkiaUiDemo;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("stress", typeof(StressPage));
		Routing.RegisterRoute("primitives", typeof(MainPage));
		Routing.RegisterRoute("composition", typeof(ControlsPage));
		foreach (var demo in ComponentDemos.All)
			Routing.RegisterRoute(demo.Route, demo.PageType);
	}
}
