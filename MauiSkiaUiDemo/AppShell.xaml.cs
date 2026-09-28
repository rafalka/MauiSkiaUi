namespace MauiSkiaUiDemo;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		// Hierarchical routes for per-control demos (pushed from the Components gallery).
		foreach (var demo in ComponentDemos.All)
			Routing.RegisterRoute(demo.Route, demo.PageType);
	}

	protected override void OnNavigated(ShellNavigatedEventArgs args)
	{
		base.OnNavigated(args);
		if (DemoTrace.LogPath is not null)
			DemoTrace.Enqueue($"navigated {args.Source} -> {args.Current?.Location}");
	}
}
