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
    ScrollingAndCollections
}

/// <summary>Display metadata for a <see cref="ComponentCategory"/>.</summary>
public static class ComponentCategoryInfo
{
    /// <summary>Gallery section titles, in display order.</summary>
    public static readonly IReadOnlyList<ComponentCategory> Order =
        [ComponentCategory.BasicControls, ComponentCategory.Layouts, ComponentCategory.Graphics, ComponentCategory.ScrollingAndCollections];

    /// <summary>Maps a category to its gallery section title.</summary>
    public static string Title(ComponentCategory category) => category switch
    {
        ComponentCategory.BasicControls => "Basic controls",
        ComponentCategory.Layouts => "Layouts",
        ComponentCategory.Graphics => "Graphics",
        ComponentCategory.ScrollingAndCollections => "Scrolling & collections",
        _ => category.ToString()
    };
}
