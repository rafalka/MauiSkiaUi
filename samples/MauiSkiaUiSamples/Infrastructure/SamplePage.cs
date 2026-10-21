using MauiSkiaUi;

namespace MauiSkiaUiSamples;

/// <summary>
/// Base page of an example: the summary, collapsible "How to" and "Things to know" panels, a Source toolbar button
/// (the example's own files, <see cref="SampleInfo.SourceFiles"/>), and the live example below (<see cref="SampleContent"/>,
/// the content of a XAML page deriving from it). The page scrolls as a whole; an app-like example that needs the page's
/// height (a list with a detail pane) fills what the description leaves instead (<c>fillsPage</c>). The page is one drawn
/// surface: a drawn example joins it, a MAUI view is hosted in it (<see cref="SkUiMauiContentView"/>).
/// </summary>
[ContentProperty(nameof(SampleContent))]
public abstract class SamplePage : ContentPage
{
    private readonly SkUiContentView _content = new();

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

        var about = new SkUiVerticalStackLayout
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
            Content = new SkUiScrollView
            {
                Background = SampleColors.Page,
                Content = new SkUiVerticalStackLayout { Children = { about, _content } }
            };
            return;
        }
        _content.Padding = new Thickness(0, 8, 0, 0);
        var description = new SkUiScrollView { Content = about };
        Grid.SetRow(_content, 1);
        var page = new SkUiGrid
        {
            Background = SampleColors.Page,
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
            Children = { description, _content }
        };
        // The description takes at most a third of the page, so the example keeps the rest.
        page.SizeChanged += (_, _) => description.MaximumHeightRequest = Math.Max(120, page.Height / 3);
        Content = page;
    }

    /// <summary>The live example, below the description.</summary>
    public View? SampleContent
    {
        get;
        set
        {
            field = value;
            _content.Content = value as ISkUiView ?? (value is null ? null : new SkUiMauiContentView { Content = value });
        }
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

    /// <summary>A header that expands <paramref name="body"/> (collapsed at first, so the example stays in view).</summary>
    private static SkUiBorder Collapsible(string title, SkUiView body)
    {
        body.Margin = new Thickness(0, 8, 0, 4);
        var chevron = new SkUiLabel { Text = "▸", FontSize = 14, TextColor = SampleColors.Accent, VerticalOptions = LayoutOptions.Center };
        var expander = new SkUiExpander
        {
            Header = new SkUiHorizontalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    chevron,
                    new SkUiLabel { Text = title, FontFamily = SampleFonts.Semibold, FontSize = 14, TextColor = SampleColors.Accent }
                }
            },
            Content = body
        };
        expander.ExpandedChanged += (_, args) => chevron.Text = args.IsExpanded ? "▾" : "▸";
        return new SkUiBorder
        {
            BackgroundColor = SampleColors.Surface,
            Stroke = SampleColors.Border,
            StrokeThickness = 1,
            CornerRadius = 8,
            Padding = new Thickness(12, 10),
            Content = expander
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
