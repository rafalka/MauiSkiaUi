namespace MauiSkiaUi;

/// <summary>
/// A scroller or list whose viewport views drawn over it can cover: <see cref="SkUiScrollView"/> (and so
/// <see cref="SkUiVirtualScrollView"/>), <see cref="Core.SkUiCoreScrollView"/> and <see cref="SkUiCollectionView"/>. For app
/// helpers that pass bars' sizes and the safe area on to whichever of them a page shows, without knowing its type.
/// </summary>
public interface ISkUiViewportInsets
{
    /// <summary>
    /// The parts of the viewport covered by views drawn over it, in DIPs on each physical edge: the content scrolls under them,
    /// and nothing is covered at the ends of the range (see <see cref="SkUiScrollView.ViewportInsets"/>).
    /// </summary>
    Thickness ViewportInsets { get; set; }
}
