namespace MauiSkiaUi;

/// <summary>
/// Where an align stack (<see cref="SkUiHorizontalAlignStackLayout"/>, <see cref="Core.SkUiCoreHorizontalAlignStackLayout"/>)
/// places its centered children. Centered children never overlap the start or end children: when the chosen center leaves
/// them no room, they move just enough to clear them.
/// </summary>
public enum SkUiCenterStrategy
{
    /// <summary>Centered in the layout, whatever the start and end children are (a title stays centered on a bar).</summary>
    Parent,

    /// <summary>Centered in the space left between the start and end children.</summary>
    AvailableSpace,

    /// <summary>Centered in the layout when they fit there; otherwise centered in the space between the start and end children.</summary>
    ParentOrAvailableSpace,

    /// <summary>
    /// <see cref="AvailableSpace"/> when there are children at both the start and the end side; with children on one side only (or
    /// none), <see cref="ParentOrAvailableSpace"/>.
    /// </summary>
    AvailableSpaceWhenBothSides
}
