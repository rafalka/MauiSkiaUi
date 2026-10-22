using MauiSkiaUi;
using MauiSkiaUiTour;

namespace MauiSkiaUiDemo;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		var items = new Dictionary<DemoFlyoutEntry, ShellItem>();
		foreach (var group in DemoFlyout.Groups)
			foreach (var entry in group.Entries)
			{
				var item = new FlyoutItem
				{
					Title = entry.Title,
					Items = { new ShellContent { Title = entry.Title, Route = entry.Route, ContentTemplate = new DataTemplate(entry.Create) } }
				};
				Items.Add(item);
				items.Add(entry, item);
			}
		// Shell's own flyout list has no group captions: draw the groups ourselves. The header is opaque: on iOS the list
		// scrolls under it.
		FlyoutBackgroundColor = Colors.White;
		FlyoutHeaderBehavior = FlyoutHeaderBehavior.Fixed;
		FlyoutHeader = new SkUiLabel
		{
			Text = Title, TextColor = DemoColors.Ink, FontFamily = DemoFonts.OpenSansSemibold, FontSize = 18, Padding = new Thickness(20, 16, 20, 4),
			Background = Colors.White
		};
		FlyoutContent = new DemoFlyoutView(this, items);
		// Hierarchical routes for per-control demos (pushed from the category galleries).
		foreach (var demo in ComponentDemos.All)
			Routing.RegisterRoute(demo.Route, demo.PageType);
		// Scripted smoke run: SKUI_TOUR=1 opens every flyout page and component demo (scripts/page_tour.sh).
		PageTour.StartIfRequested(this, () =>
		[
			.. DemoFlyout.Groups.SelectMany(group => group.Entries).Select(entry => new PageTour.Step(entry.Title, () => GoToAsync("//" + entry.Route))),
			.. ComponentDemos.All.Select(demo => new PageTour.Step(demo.Route,
				async () =>
				{
					await GoToAsync("//" + DemoFlyout.Groups[0].Entries[0].Route);
					await GoToAsync(demo.Route);
				},
				() => GoToAsync("..")))
		]);
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
