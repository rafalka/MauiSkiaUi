using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>One page the search can open.</summary>
/// <param name="Title">Shown on the result row.</param>
/// <param name="Detail">Caption under the title: where the page lives and what it replaces.</param>
/// <param name="Route">Shell route: relative for component demos (pushed), <c>//route</c> for flyout pages.</param>
/// <param name="Keywords">Extra text the query matches besides the title and detail (type names, MAUI counterparts).</param>
public sealed record DemoSearchItem(string Title, string Detail, string Route, string Keywords = "")
{
    /// <summary>Whether every whitespace-separated word of <paramref name="query"/> occurs in the item (ignoring case).</summary>
    public bool Matches(string query)
    {
        foreach (var word in query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (!Title.Contains(word, StringComparison.OrdinalIgnoreCase) && !Detail.Contains(word, StringComparison.OrdinalIgnoreCase)
                && !Keywords.Contains(word, StringComparison.OrdinalIgnoreCase))
                return false;
        return true;
    }

    /// <summary>Search items for every component demo, in gallery order.</summary>
    public static IEnumerable<DemoSearchItem> ForComponentDemos() =>
        ComponentDemos.All.Select(demo =>
        {
            var category = ComponentCategoryInfo.Title(demo.Category);
            var detail = demo.Description is { Length: > 0 } description ? category + " · " + description : category;
            return new DemoSearchItem(demo.Title, detail, demo.Route, demo.ComponentType.Name + " " + demo.Counterpart);
        });
}

/// <summary>
/// Finds a demo by name: every component demo plus the given flyout pages, filtered as the query is typed (all words must
/// match the control's type, title, category or MAUI counterpart). Enter opens the first result.
/// </summary>
public sealed class DemoSearchPage : ContentPage
{
    private readonly List<(DemoSearchItem Item, SkUiButton Row)> _rows = [];
    private readonly SkUiLabel _summary = new()
    {
        TextColor = DemoColors.Caption, FontFamily = DemoFonts.OpenSansRegular, FontSize = 12, Margin = new Thickness(16, 0, 16, 4),
        AutomationId = "DemoSearchSummary"
    };
    private bool _navigating;

    /// <param name="pages">Flyout pages to search besides the component demos.</param>
    public DemoSearchPage(IEnumerable<DemoSearchItem> pages)
    {
        Title = "Search";
        Background = DemoColors.PageBackground;
        SearchBar = new SkUiSearchBar
        {
            Placeholder = "Control, MAUI name or page", FontFamily = DemoFonts.OpenSansRegular, Margin = new Thickness(16, 12, 16, 4),
            AutomationId = "DemoSearchBar"
        };
        SemanticProperties.SetDescription(SearchBar, "Search demos");
        SearchBar.TextChanged += (_, args) => Filter(args.NewTextValue ?? "");
        SearchBar.SearchButtonPressed += async (_, _) =>
        {
            if (Results.FirstOrDefault() is { } first)
                await Navigate(first.Route);
        };
        var rows = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 4, 16, 24) };
        foreach (var item in DemoSearchItem.ForComponentDemos().Concat(pages))
        {
            var text = new FormattedString
            {
                Spans =
                {
                    new Span { Text = item.Title, FontFamily = DemoFonts.OpenSansSemibold, FontSize = 15 },
                    new Span { Text = "\n" + item.Detail, FontFamily = DemoFonts.OpenSansRegular, FontSize = 12, TextColor = DemoColors.Caption }
                }
            };
            // Same row look as the category galleries.
            var row = new SkUiButton
            {
                FormattedText = text, Background = Colors.White, TextColor = DemoColors.Ink, LineBreakMode = LineBreakMode.WordWrap,
                HorizontalTextAlignment = TextAlignment.Start, Padding = new Thickness(16, 8), MinimumHeightRequest = 52, CornerRadius = 4,
                BorderColor = DemoColors.Border, BorderWidth = 1, AutomationId = "Find" + item.Route.TrimStart('/')
            };
            row.Clicked += async (_, _) => await Navigate(item.Route);
            rows.Children.Add(row);
            _rows.Add((item, row));
        }
        var results = new SkUiScrollView { Content = rows, AutomationId = "DemoSearchResults" };
        Grid.SetRow(_summary, 1);
        Grid.SetRow(results, 2);
        Content = new SkUiGrid
        {
            Background = DemoColors.PageBackground, RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star)],
            Children = { SearchBar, _summary, results }
        };
        Filter("");
    }

    /// <summary>The query field.</summary>
    public SkUiSearchBar SearchBar { get; }

    /// <summary>The items the current query shows, in display order.</summary>
    public IEnumerable<DemoSearchItem> Results => _rows.Where(row => row.Row.IsVisible).Select(row => row.Item);

    internal string Summary => _summary.Text ?? "";

    private void Filter(string query)
    {
        var count = 0;
        foreach (var (item, row) in _rows)
        {
            row.IsVisible = item.Matches(query);
            if (row.IsVisible) count++;
        }
        _summary.Text = count switch
        {
            0 => $"No demo matches \"{query.Trim()}\".",
            1 => "1 demo",
            _ => $"{count} demos"
        };
    }

    private async Task Navigate(string route)
    {
        if (_navigating) return;
        _navigating = true;
        try { await Shell.Current.GoToAsync(route); }
        finally { _navigating = false; }
    }
}
