namespace MauiSkiaUi.Core;

/// <summary>
/// Which components of a Core absolute-layout child's bounds are proportional to the layout slot (0–1),
/// mirroring MAUI <c>AbsoluteLayoutFlags</c> without depending on MAUI Controls types.
/// </summary>
[Flags]
public enum SkUiCoreAbsoluteLayoutFlags
{
    /// <summary>All components are absolute DIPs.</summary>
    None = 0,
    /// <summary><c>X</c> is proportional to the layout width.</summary>
    X = 1 << 0,
    /// <summary><c>Y</c> is proportional to the layout height.</summary>
    Y = 1 << 1,
    /// <summary><c>Width</c> is proportional to the layout width.</summary>
    Width = 1 << 2,
    /// <summary><c>Height</c> is proportional to the layout height.</summary>
    Height = 1 << 3,
    /// <summary><see cref="X"/> and <see cref="Y"/>.</summary>
    Position = X | Y,
    /// <summary><see cref="Width"/> and <see cref="Height"/>.</summary>
    Size = Width | Height,
    /// <summary>All components are proportional.</summary>
    All = Position | Size
}
