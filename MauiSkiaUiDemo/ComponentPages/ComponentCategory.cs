namespace MauiSkiaUiDemo;

/// <summary>The gallery page a component demo belongs to (one flyout entry per category).</summary>
public enum ComponentCategory
{
    /// <summary>Leaf, non-layout, non-shape controls (SkUiView, SkUiLabel, SkUiButton, SkUiImage, ...).</summary>
    BasicControls,
    /// <summary>Composition hosts and multi/single-child layouts (SkUiContentView, SkUiLayout, SkUiGrid, ...).</summary>
    Layouts,
    /// <summary>Shape primitives drawn directly with SkiaSharp (SkUiBox, SkUiEllipse, SkUiLine, ...).</summary>
    Graphics,
    /// <summary>SkUiScrollView and the virtualizing collection view controls.</summary>
    ScrollingAndCollections,
    /// <summary>Core-layer nodes under <c>SkUiCoreHost</c>.</summary>
    Core
}

/// <summary>Display metadata for a <see cref="ComponentCategory"/>.</summary>
public static class ComponentCategoryInfo
{
    /// <summary>All categories in flyout order.</summary>
    public static readonly IReadOnlyList<ComponentCategory> Order =
    [
        ComponentCategory.BasicControls,
        ComponentCategory.Layouts,
        ComponentCategory.Graphics,
        ComponentCategory.ScrollingAndCollections,
        ComponentCategory.Core
    ];

    /// <summary>Maps a category to its gallery page and flyout title.</summary>
    public static string Title(ComponentCategory category) => category switch
    {
        ComponentCategory.BasicControls => "Basic controls",
        ComponentCategory.Layouts => "Layouts",
        ComponentCategory.Graphics => "Graphics",
        ComponentCategory.ScrollingAndCollections => "Scrolling & collections",
        ComponentCategory.Core => "Core",
        _ => category.ToString()
    };

    /// <summary>A line shown above the category's demos, or <see langword="null"/>.</summary>
    public static string? Intro(ComponentCategory category) => category switch
    {
        ComponentCategory.Core => "Lightweight Core nodes (no MAUI View). Previews use SkUiCoreHost.",
        _ => null
    };

    /// <summary>Shell route of the category's gallery page.</summary>
    public static string Route(ComponentCategory category) => category switch
    {
        ComponentCategory.BasicControls => "basic-controls",
        ComponentCategory.Layouts => "layouts",
        ComponentCategory.Graphics => "graphics",
        ComponentCategory.ScrollingAndCollections => "scrolling",
        ComponentCategory.Core => "core",
        _ => category.ToString().ToLowerInvariant()
    };
}
