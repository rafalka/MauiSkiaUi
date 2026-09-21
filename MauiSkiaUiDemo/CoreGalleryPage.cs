namespace MauiSkiaUiDemo;

/// <summary>Launch page for Core-layer demos (hosted via <see cref="MauiSkiaUi.Core.SkUiCoreHost"/>).</summary>
public sealed class CoreGalleryPage : ContentPage
{
    private bool _navigating;

    public CoreGalleryPage()
    {
        Title = "SkiaUi / Core";
        Background = DemoColors.PageBackground;
        var rows = new Grid { RowSpacing = 8, Padding = new Thickness(16, 8, 16, 24) };

        var introRow = rows.RowDefinitions.Count;
        rows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        rows.Add(new Label
        {
            Text = "Lightweight Core nodes (no MAUI View). Previews use SkUiCoreHost.",
            TextColor = DemoColors.Caption,
            FontFamily = DemoFonts.OpenSansRegular,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 8),
            AutomationId = "CoreGalleryIntro"
        }, 0, introRow);

        var demos = ComponentDemos.All.Where(demo => demo.Category == ComponentCategory.Core).ToList();
        foreach (var demo in demos)
        {
            var row = rows.RowDefinitions.Count;
            rows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var button = new Button
            {
                Text = demo.Name,
                Background = Colors.White,
                TextColor = DemoColors.Ink,
                FontFamily = DemoFonts.OpenSansSemibold,
                HeightRequest = 52,
                CornerRadius = 4,
                BorderColor = DemoColors.Border,
                BorderWidth = 1,
                AutomationId = "Open" + demo.Name
            };
            button.Clicked += async (_, _) => await Navigate(demo.Route);
            rows.Add(button, 0, row);
        }

        var root = new Grid { RowDefinitions = [new(GridLength.Star)] };
        root.Add(new ScrollView { Content = rows, AutomationId = "CoreCatalog" });
        Content = root;
    }

    private async Task Navigate(string route)
    {
        if (_navigating) return;
        _navigating = true;
        try { await Shell.Current.GoToAsync(route); }
        finally { _navigating = false; }
    }
}
