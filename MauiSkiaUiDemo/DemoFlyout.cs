namespace MauiSkiaUiDemo;

/// <summary>One flyout entry: a root page of the demo shell.</summary>
/// <param name="Title">Flyout and page title.</param>
/// <param name="Route">Shell route (<c>SKUI_DEMO_ROUTE</c> opens it at launch).</param>
/// <param name="Create">Factory that builds the page when it is first shown.</param>
/// <param name="Icon">Path markup of a 16×16 outline icon drawn before the title in the flyout, or <see langword="null"/>.</param>
public sealed record DemoFlyoutEntry(string Title, string Route, Func<Page> Create, string? Icon = null);

/// <summary>A captioned group of flyout entries.</summary>
public sealed record DemoFlyoutGroup(string Title, IReadOnlyList<DemoFlyoutEntry> Entries);

/// <summary>
/// The demo shell's flyout: <see cref="Search"/> on top, then every other root page, grouped. <see cref="AppShell"/> builds
/// its items from it.
/// </summary>
public static class DemoFlyout
{
    /// <summary>The search page: the flyout's top row, outside the groups so it is not the start page.</summary>
    public static DemoFlyoutEntry Search { get; } = new("Search demos", "search", () => new DemoSearchPage(SearchablePages()),
        Icon: "M 12 6.5 A 5.5 5.5 0 1 1 1 6.5 A 5.5 5.5 0 1 1 12 6.5 M 10.5 10.5 L 15 15");

    /// <summary>Groups in flyout order; the first entry is the start page.</summary>
    public static IReadOnlyList<DemoFlyoutGroup> Groups { get; } =
    [
        new("Controls",
        [
            .. ComponentCategoryInfo.Order.Select(category => new DemoFlyoutEntry(
                ComponentCategoryInfo.Title(category), ComponentCategoryInfo.Route(category), () => new ComponentGalleryPage(category)))
        ]),
        new("Showcase",
        [
            new("Composition", "composition", () => new ControlsPage()),
            new("Native nesting", "nesting", () => new NativeNestingPage()),
            new("Accessibility", "accessibility", () => new AccessibilityPage()),
            new("Primitives", "primitives", () => new MainPage())
        ]),
        new("Look",
        [
            new("Look & colors", "look", () => new LookAndColorSchemePage()),
            new("Looks side by side", "looks", () => new LooksSideBySidePage()),
            new("Look comparison", "look-comparison", () => new LookComparisonPage())
        ]),
        new("Performance",
        [
            new("Stress", "stress", () => new StressPage()),
            new("Effects stress", "effects-stress", () => new EffectsStressPage())
        ])
    ];

    /// <summary>Every flyout entry: the grouped pages in flyout order, then <see cref="Search"/>.</summary>
    public static IEnumerable<DemoFlyoutEntry> Entries => Groups.SelectMany(group => group.Entries).Append(Search);

    // Every grouped page, captioned with its group.
    private static IEnumerable<DemoSearchItem> SearchablePages() =>
        Groups.SelectMany(group => group.Entries.Select(entry => new DemoSearchItem(entry.Title, group.Title + " · Page", "//" + entry.Route)));
}
