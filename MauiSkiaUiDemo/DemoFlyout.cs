namespace MauiSkiaUiDemo;

/// <summary>One flyout entry: a root page of the demo shell.</summary>
/// <param name="Title">Flyout and page title.</param>
/// <param name="Route">Shell route (<c>SKUI_DEMO_ROUTE</c> opens it at launch).</param>
/// <param name="Create">Factory that builds the page when it is first shown.</param>
public sealed record DemoFlyoutEntry(string Title, string Route, Func<Page> Create);

/// <summary>A captioned group of flyout entries.</summary>
public sealed record DemoFlyoutGroup(string Title, IReadOnlyList<DemoFlyoutEntry> Entries);

/// <summary>The demo shell's flyout: every root page, grouped. <see cref="AppShell"/> builds its items from it.</summary>
public static class DemoFlyout
{
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
}
