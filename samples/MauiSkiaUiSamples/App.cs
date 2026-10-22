using MauiSkiaUiTour;

namespace MauiSkiaUiSamples;

public sealed class App : Application
{
    // The pages use a light palette: keep system chrome (navigation bar title, toolbar) light too in dark mode.
    public App() => UserAppTheme = AppTheme.Light;

    protected override Window CreateWindow(IActivationState? activationState) => new(new AppShell()) { Title = "SkiaUi Samples" };
}

/// <summary>Flyout: an overview of every example, then one page per section (like the demo app's gallery sections).</summary>
public sealed class AppShell : Shell
{
    public AppShell()
    {
        Title = "SkiaUi Samples";
        FlyoutBehavior = FlyoutBehavior.Flyout;
        // Navigation bar colors (no XAML styles in this app): readable titles and toolbar items on every platform.
        SetBackgroundColor(this, SampleColors.Surface);
        SetForegroundColor(this, SampleColors.Accent);
        SetTitleColor(this, SampleColors.Ink);
        Add("Overview", "overview", () => new SampleListPage(section: null));
        foreach (var section in SampleCatalog.Sections)
            Add(section.DisplayName(), "section-" + section, () => new SampleListPage(section));
        // Scripted smoke run: SKUI_TOUR=1 opens every page and example (scripts/page_tour.sh).
        PageTour.StartIfRequested(this, () =>
        [
            new("overview", () => GoToAsync("//overview")),
            .. SampleCatalog.Sections.Select(section => new PageTour.Step("section-" + section, () => GoToAsync("//section-" + section))),
            .. SampleCatalog.All.Select(entry => new PageTour.Step(entry.Info.Title,
                async () =>
                {
                    await GoToAsync("//overview");
                    await Navigation.PushAsync(entry.Create());
                },
                () => Navigation.PopAsync()))
        ]);
    }

    private void Add(string title, string route, Func<Page> create) =>
        Items.Add(new FlyoutItem
        {
            Title = title,
            Items = { new ShellContent { Title = title, Route = route, ContentTemplate = new DataTemplate(create) } }
        });
}
