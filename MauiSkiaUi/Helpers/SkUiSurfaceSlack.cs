namespace MauiSkiaUi;

/// <summary>
/// The length of a Windows GPU surface's swap chain panel for a slot of a given length. WinUI does not synchronize a swap chain's
/// presents with its own layout, so a swap chain resized along with its slot shows at the new size a frame or two before or after
/// the layout, and where it is shorter than what XAML shows, nothing but the window's backdrop shows. So the panel is kept longer
/// than the slot and clipped to it, and changes only when the slot leaves <see cref="MinExcess"/> to <see cref="MaxExcess"/> of slack.
/// Changing it can still flash the whole surface for a frame (SKSwapChainPanel presents an empty buffer on a size change), so a panel
/// that had to change takes more slack than a new one: a surface that never resizes pays little, and one being dragged back and forth
/// settles on a length that fits the whole drag.
/// </summary>
internal static class SkUiSurfaceSlack
{
    /// <summary>The least slack (DIPs) a panel keeps; a resize by more than this in one layout pass can still show a gap.</summary>
    internal const double MinExcess = 96;

    /// <summary>The slack (DIPs) of a surface's first panel.</summary>
    internal const double FirstExcess = 2 * MinExcess;

    /// <summary>The slack (DIPs) of a panel replacing one that no longer fit.</summary>
    internal const double ResizedExcess = 4 * MinExcess;

    /// <summary>The most slack (DIPs) a panel keeps, which bounds the memory it costs.</summary>
    internal const double MaxExcess = 8 * MinExcess;

    /// <summary>The panel's length for a slot of <paramref name="slot"/> DIPs, given its <paramref name="current"/> length (NaN: none yet).</summary>
    internal static double PanelLength(double current, double slot)
    {
        if (slot <= 0)
            return 0;
        if (current >= slot + MinExcess && current <= slot + MaxExcess)
            return current;
        return current > 0 ? slot + ResizedExcess : slot + FirstExcess;
    }
}
