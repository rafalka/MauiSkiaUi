namespace MauiSkiaUiDemo;

/// <summary>The gallery section a component belongs to.</summary>
public enum ComponentCategory
{
    /// <summary>Leaf, non-layout, non-shape controls (SkUiView, SkUiLabel, SkUiButton, SkUiImage, ...).</summary>
    BasicControls,
    /// <summary>Composition hosts and multi/single-child layouts (SkUiContentView, SkUiLayout, SkUiGrid, ...).</summary>
    Layouts,
    /// <summary>Shape primitives drawn directly with SkiaSharp (SkUiBox, SkUiEllipse, SkUiLine, ...).</summary>
    Graphics,
    /// <summary>SkUiScrollView and, later, virtualizing collection view controls.</summary>
    ScrollingAndCollections,
    /// <summary>Core-layer nodes under <c>SkUiCoreHost</c> (separate flyout gallery).</summary>
    Core
}

/// <summary>Display metadata for a <see cref="ComponentCategory"/>.</summary>
public static class ComponentCategoryInfo
{
    /// <summary>All categories that have demos (Components + Core flyouts).</summary>
    public static readonly IReadOnlyList<ComponentCategory> Order =
    [
        ComponentCategory.BasicControls,
        ComponentCategory.Layouts,
        ComponentCategory.Graphics,
        ComponentCategory.ScrollingAndCollections,
        ComponentCategory.Core
    ];

    /// <summary>Sections shown on the MAUI-compatible Components gallery (excludes Core).</summary>
    public static readonly IReadOnlyList<ComponentCategory> MauiCompatibleOrder =
    [
        ComponentCategory.BasicControls,
        ComponentCategory.Layouts,
        ComponentCategory.Graphics,
        ComponentCategory.ScrollingAndCollections
    ];

    /// <summary>Maps a category to its gallery section title.</summary>
    public static string Title(ComponentCategory category) => category switch
    {
        ComponentCategory.BasicControls => "Basic controls",
        ComponentCategory.Layouts => "Layouts",
        ComponentCategory.Graphics => "Graphics",
        ComponentCategory.ScrollingAndCollections => "Scrolling & collections",
        ComponentCategory.Core => "Core",
        _ => category.ToString()
    };
}
