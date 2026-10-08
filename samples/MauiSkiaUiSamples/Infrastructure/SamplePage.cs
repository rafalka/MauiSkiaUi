using MauiSkiaUi;

namespace MauiSkiaUiSamples;

/// <summary>
/// Base page of an example: the summary, collapsible "How to" and "Things to know" panels, a Source toolbar button
/// (the example's own files, <see cref="SampleInfo.SourceFiles"/>), and the live example below (<see cref="SampleContent"/>,
/// the content of a XAML page deriving from it). The page scrolls as a whole; an app-like example that needs the page's
/// height (a list with a detail pane) fills what the description leaves instead (<c>fillsPage</c>).
/// </summary>
[ContentProperty(nameof(SampleContent))]
public abstract class SamplePage : ContentPage
{
    private readonly ContentView _content = new();

    /// <param name="info">The example's description.</param>
    /// <param name="fillsPage">
    /// The example fills the page below the description (which scrolls on its own, at most a third of the page tall) instead
    /// of scrolling with it: for examples with their own scrolling and layout across the whole page.
    /// </param>
    protected SamplePage(SampleInfo info, bool fillsPage = false)
    {
        Title = info.Title;
        BackgroundColor = SampleColors.Page;
        SampleColors.ApplyNavigationBar(this);
        ToolbarItems.Add(new ToolbarItem("Source", null, async () => await Navigation.PushAsync(new SourcePage(info))));

        var about = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(16, 12, 16, 4),
            Children =
            {
                SampleText.Paragraph(info.Summary, 15),
                Collapsible("How to", SampleText.List(info.HowTo, numbered: true)),
                Collapsible("Things to know", SampleText.List(info.ThingsToKnow, numbered: false))
            }
        };
        if (!fillsPage)
        {
            _content.Padding = new Thickness(16, 8, 16, 24);
            Content = new ScrollView { Content = new VerticalStackLayout { Children = { about, _content } } };
            return;
        }
        _content.Padding = new Thickness(0, 8, 0, 0);
        var description = new ScrollView { Content = about };
        var page = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
        page.Add(description);
        page.Add(_content, 0, 1);
        // The description takes at most a third of the page, so the example keeps the rest.
        page.SizeChanged += (_, _) => description.MaximumHeightRequest = Math.Max(120, page.Height / 3);
        Content = page;
    }

    /// <summary>The live example, below the description.</summary>
    public View? SampleContent
    {
        get => _content.Content;
        set => _content.Content = value;
    }

    /// <summary>
    /// A look to show the example with. Looks are app-wide (<see cref="SkUiLook.Current"/>): an app sets its look once at
    /// startup; here each page picks the look when it appears (<see cref="SampleLooks"/>). The example's look stays while
    /// its Source page is on top, and the list pages restore the app's look.
    /// </summary>
    protected virtual SkUiLook? Look => null;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SampleLooks.Show(Look);
    }

    /// <summary>A header that shows or hides <paramref name="body"/> (collapsed at first, so the example stays in view).</summary>
    private static View Collapsible(string title, View body)
    {
        body.IsVisible = false;
        body.Margin = new Thickness(0, 8, 0, 4);
        var chevron = new Label { Text = "▸", FontSize = 14, TextColor = SampleColors.Accent, VerticalOptions = LayoutOptions.Center };
        var header = new HorizontalStackLayout
        {
            Spacing = 6,
            Children =
            {
                chevron,
                new Label { Text = title, FontFamily = SampleFonts.Semibold, FontSize = 14, TextColor = SampleColors.Accent }
            }
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            body.IsVisible = !body.IsVisible;
            chevron.Text = body.IsVisible ? "▾" : "▸";
        };
        header.GestureRecognizers.Add(tap);
        return new Border
        {
            BackgroundColor = SampleColors.Surface,
            Stroke = SampleColors.Border,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(12, 10),
            Content = new VerticalStackLayout { Children = { header, body } }
        };
    }
}

/// <summary>
/// Which look is current: the example's while an example (or its source) is shown, the app's otherwise. Pages call
/// <see cref="Show"/> when they appear, which also covers switching sections from the flyout.
/// </summary>
public static class SampleLooks
{
    private static SkUiLook? _appLook;

    /// <summary>Makes <paramref name="look"/> current, or the app's look for <c>null</c>.</summary>
    public static void Show(SkUiLook? look)
    {
        _appLook ??= SkUiLook.Current;
        var target = look ?? _appLook;
        if (!ReferenceEquals(SkUiLook.Current, target))
            SkUiLook.Current = target;
    }
}
