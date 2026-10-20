namespace MauiSkiaUi;

/// <summary>
/// A label, button or image button's border and corners, on either layer (<see cref="SkUiLabel"/> and <see cref="SkUiButton"/>,
/// <see cref="SkUiImageButton"/>, <see cref="Core.SkUiCoreLabel"/> and <see cref="Core.SkUiCoreButton"/>,
/// <see cref="Core.SkUiCoreImageButton"/>). The border is drawn inside the bounds.
/// </summary>
/// <remarks>
/// The SkUi controls also have MAUI's <c>int</c> <c>CornerRadius</c>, a uniform view of <see cref="CornerRadii"/>; the Core layer
/// has only the radii. Setting a property is the same as setting it on the control: an invalid value is ignored with a warning on
/// the SkUi layer and throws on the Core layer.
/// </remarks>
public interface ISkUiBorderElement
{
    /// <summary>The border color.</summary>
    Color BorderColor { get; set; }

    /// <summary>The border width in DIPs (0: no border).</summary>
    double BorderWidth { get; set; }

    /// <summary>Per-corner radii in DIPs of the fill and border; radii larger than the control allows are scaled down.</summary>
    CornerRadius CornerRadii { get; set; }
}
