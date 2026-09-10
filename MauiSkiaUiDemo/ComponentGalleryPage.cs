namespace MauiSkiaUiDemo;

/// <summary>Gallery of every dedicated component demo, grouped by <see cref="ComponentCategory"/>.</summary>
public sealed class ComponentGalleryPage : ContentPage
{
    private bool _navigating;

    public ComponentGalleryPage()
    {
        Title = "SkiaUi / Components";
        Background = DemoColors.PageBackground;
        var rows = new Grid { RowSpacing = 8, Padding = new Thickness(16, 8, 16, 24) };
        var groups = ComponentDemos.All.ToLookup(demo => demo.Category);
        foreach (var category in ComponentCategoryInfo.Order)
        {
            var demos = groups[category];
            if (!demos.Any()) continue;
            var headerRow = rows.RowDefinitions.Count;
            rows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var header = new Label
            {
                Text = ComponentCategoryInfo.Title(category), TextColor = DemoColors.Ink,
                FontFamily = DemoFonts.OpenSansSemibold, FontSize = 15, Margin = new Thickness(0, headerRow == 0 ? 0 : 12, 0, 4),
                AutomationId = "Group" + category
            };
            rows.Add(header, 0, headerRow);
            foreach (var demo in demos)
            {
                var row = rows.RowDefinitions.Count;
                rows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var button = new Button
                {
                    Text = demo.Name, Background = Colors.White, TextColor = DemoColors.Ink,
                    FontFamily = DemoFonts.OpenSansSemibold, HeightRequest = 52, CornerRadius = 4,
                    BorderColor = DemoColors.Border, BorderWidth = 1, AutomationId = "Open" + demo.Name
                };
                button.Clicked += async (_, _) => await Navigate(demo.Route);
                rows.Add(button, 0, row);
            }
        }
        var root = new Grid { RowDefinitions = [new(GridLength.Star)] };
        root.Add(new ScrollView { Content = rows, AutomationId = "ComponentCatalog" });
        Content = root;
        ToolbarItems.Add(new ToolbarItem("Composition", null, async () => await Navigate("composition")));
        ToolbarItems.Add(new ToolbarItem("Stress", null, async () => await Navigate("stress")));
        ToolbarItems.Add(new ToolbarItem("Primitives", null, async () => await Navigate("primitives")));
    }

    private async Task Navigate(string route)
    {
        if (_navigating) return;
        _navigating = true;
        try { await Shell.Current.GoToAsync(route); }
        finally { _navigating = false; }
    }
}
