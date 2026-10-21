using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Gallery of the dedicated component demos of one <see cref="ComponentCategory"/> (one flyout entry per category).</summary>
public sealed class ComponentGalleryPage : ContentPage
{
    private bool _navigating;

    // Shown while the page's shorter side cannot fit the SkUi and MAUI previews side by side: the shorter side, not the
    // width, so a phone in landscape (wide, but no height left for the editors) still shows it.
    private readonly SkUiLabel _narrowNote = new()
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
        var rows = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 8, 16, 24) };
        if (ComponentCategoryInfo.Intro(category) is { } intro)
            rows.Children.Add(new SkUiLabel
            {
                Text = intro, TextColor = DemoColors.Caption, FontFamily = DemoFonts.OpenSansRegular, FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8), AutomationId = "GalleryIntro"
            });
        foreach (var demo in ComponentDemos.All.Where(demo => demo.Category == category))
        {
            // The control's type, then what the page shows and which MAUI control it replaces.
            var text = new FormattedString { Spans = { new Span { Text = demo.DisplayName, FontFamily = DemoFonts.OpenSansSemibold, FontSize = 15 } } };
            if (demo.Description is { Length: > 0 } description)
                text.Spans.Add(new Span
                {
                    Text = "\n" + description, FontFamily = DemoFonts.OpenSansRegular, FontSize = 12, TextColor = DemoColors.Caption
                });
            var button = new SkUiButton
            {
                FormattedText = text, Background = Colors.White, TextColor = DemoColors.Ink, LineBreakMode = LineBreakMode.WordWrap,
                HorizontalTextAlignment = TextAlignment.Start, Padding = new Thickness(16, 8), MinimumHeightRequest = 52, CornerRadius = 4,
                BorderColor = DemoColors.Border, BorderWidth = 1, AutomationId = "Open" + demo.Name
            };
            button.Clicked += async (_, _) => await Navigate(demo.Route);
            rows.Children.Add(button);
        }
        // One drawn surface: the note above the scrolling list of demos.
        var catalog = new SkUiScrollView { Content = rows, AutomationId = "ComponentCatalog" };
        Grid.SetRow(catalog, 1);
        Content = new SkUiGrid
        {
            Background = DemoColors.PageBackground, RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)],
            Children = { _narrowNote, catalog }
        };
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
