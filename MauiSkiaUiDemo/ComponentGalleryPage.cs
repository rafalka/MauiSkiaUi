namespace MauiSkiaUiDemo;

/// <summary>Gallery of the dedicated component demos of one <see cref="ComponentCategory"/> (one flyout entry per category).</summary>
public sealed class ComponentGalleryPage : ContentPage
{
    private bool _navigating;

    // Shown while the page's shorter side cannot fit the SkUi and MAUI previews side by side: the shorter side, not the
    // width, so a phone in landscape (wide, but no height left for the editors) still shows it.
    private readonly Label _narrowNote = new()
    {
        Text = "This demo works best on wide devices like tablets or desktops.",
        TextColor = DemoColors.Caption, Background = DemoColors.SoftSurface, FontFamily = DemoFonts.OpenSansRegular, FontSize = 12,
        Padding = new Thickness(16, 8), IsVisible = false, AutomationId = "NarrowScreenNote"
    };

    public ComponentGalleryPage(ComponentCategory category)
    {
        Category = category;
        Title = ComponentCategoryInfo.Title(category);
        Background = DemoColors.PageBackground;
        var rows = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 8, 16, 24) };
        if (ComponentCategoryInfo.Intro(category) is { } intro)
            rows.Add(new Label
            {
                Text = intro, TextColor = DemoColors.Caption, FontFamily = DemoFonts.OpenSansRegular, FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8), AutomationId = "GalleryIntro"
            });
        foreach (var demo in ComponentDemos.All.Where(demo => demo.Category == category))
        {
            var button = new Button
            {
                Text = demo.Name, Background = Colors.White, TextColor = DemoColors.Ink,
                FontFamily = DemoFonts.OpenSansSemibold, HeightRequest = 52, CornerRadius = 4,
                BorderColor = DemoColors.Border, BorderWidth = 1, AutomationId = "Open" + demo.Name
            };
            button.Clicked += async (_, _) => await Navigate(demo.Route);
            rows.Add(button);
        }
        var root = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        root.Add(_narrowNote);
        root.Add(new ScrollView { Content = rows, AutomationId = "ComponentCatalog" }, 0, 1);
        Content = root;
        SizeChanged += (_, _) => UpdateNarrowNote(Width, Height);
    }

    /// <summary>The category whose demos this page lists.</summary>
    public ComponentCategory Category { get; }

    internal bool ShowsNarrowNote => _narrowNote.IsVisible;

    internal void UpdateNarrowNote(double width, double height) =>
        _narrowNote.IsVisible = width > 0 && height > 0 && Math.Min(width, height) < ComponentDemoPage.SideBySideMinWidth;

    private async Task Navigate(string route)
    {
        if (_navigating) return;
        _navigating = true;
        try { await Shell.Current.GoToAsync(route); }
        finally { _navigating = false; }
    }
}
