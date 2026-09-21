namespace MauiSkiaUi.Core;

/// <summary>Sizing mode for a Core grid track (row height or column width).</summary>
public enum SkUiCoreGridUnitType
{
    /// <summary>Fixed size in DIPs.</summary>
    Absolute = 0,

    /// <summary>Size to content.</summary>
    Auto = 1,

    /// <summary>Share remaining space proportionally.</summary>
    Star = 2
}
