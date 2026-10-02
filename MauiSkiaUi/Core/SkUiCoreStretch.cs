namespace MauiSkiaUi.Core;

/// <summary>
/// How a Core shape's geometry is fitted into its bounds, mirroring MAUI's <c>Stretch</c> (the <c>Aspect</c> of a shape)
/// without depending on MAUI Controls types. Values match MAUI's.
/// </summary>
public enum SkUiCoreStretch
{
    /// <summary>The geometry keeps its coordinates; it only moves to bring an edge that sticks out back into the bounds.</summary>
    None = 0,
    /// <summary>The geometry is scaled to fill the bounds on both axes.</summary>
    Fill = 1,
    /// <summary>The geometry is scaled uniformly to fit inside the bounds, centered.</summary>
    Uniform = 2,
    /// <summary>The geometry is scaled uniformly to cover the bounds, from the top-left corner.</summary>
    UniformToFill = 3
}
