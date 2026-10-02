namespace MauiSkiaUiDemo;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		// Hierarchical routes for per-control demos (pushed from the Components gallery).
		foreach (var demo in ComponentDemos.All)
			Routing.RegisterRoute(demo.Route, demo.PageType);
		// Scripted runs: SKUI_DEMO_ROUTE=effects-stress opens a flyout page at launch.
		if (Environment.GetEnvironmentVariable("SKUI_DEMO_ROUTE") is { Length: > 0 } route)
			Loaded += async (_, _) =>
			{
				try { await GoToAsync("//" + route); }
				catch (Exception ex) { Console.WriteLine($"[Demo] route '{route}': {ex.Message}"); }
			};
	}

	protected override void OnNavigated(ShellNavigatedEventArgs args)
	{
		base.OnNavigated(args);
		if (DemoTrace.LogPath is not null)
			DemoTrace.Enqueue($"navigated {args.Source} -> {args.Current?.Location}");
	}
}
