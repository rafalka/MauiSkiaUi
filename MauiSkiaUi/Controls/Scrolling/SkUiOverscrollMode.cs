namespace MauiSkiaUi;

/// <summary>What a drawn scroller does when it is dragged or flung past an edge of its content.</summary>
public enum SkUiOverscrollMode
{
    /// <summary>The look's choice (<see cref="SkUiLook.DefaultOverscroll"/>; the default look follows the platform).</summary>
    Default,

    /// <summary>Stop at the edge.</summary>
    None,

    /// <summary>The content follows the drag past the edge with growing resistance and springs back (iOS).</summary>
    Bounce,

    /// <summary>The content stretches away from the pulled edge and relaxes back (Android 12 and later).</summary>
    Stretch
}
