namespace MauiSkiaUi;

/// <summary>
/// Edges of a drawn scroller's content (physical: left and right also in right-to-left layouts), for
/// <see cref="SkUiScrollView.PullEdges"/> and <see cref="SkUiPullReleasedEventArgs.Edge"/>.
/// </summary>
[Flags]
public enum SkUiScrollEdges
{
    /// <summary>No edge.</summary>
    None = 0,

    /// <summary>The start of the vertical axis.</summary>
    Top = 1,

    /// <summary>The end of the vertical axis.</summary>
    Bottom = 2,

    /// <summary>The left end of the horizontal axis.</summary>
    Left = 4,

    /// <summary>The right end of the horizontal axis.</summary>
    Right = 8,

    /// <summary>Every edge.</summary>
    All = Top | Bottom | Left | Right
}

/// <summary>
/// What a drawn scroller shows past the edges of its content (<see cref="SkUiScrollView.Overscrolled"/>,
/// <see cref="Core.SkUiCoreScrollView.Overscrolled"/>). Each scroller reuses one instance for every report: read it in the
/// handler, do not keep it.
/// </summary>
public sealed class SkUiOverscrolledEventArgs : EventArgs
{
    internal SkUiOverscrolledEventArgs()
    {
    }

    /// <summary>Distance shown past the left (negative) or right (positive) edge, in DIPs; 0 when within the content.</summary>
    public double OverscrollX { get; internal set; }

    /// <summary>Distance shown past the top (negative) or bottom (positive) edge, in DIPs; 0 when within the content.</summary>
    public double OverscrollY { get; internal set; }

    /// <summary>
    /// A finger (or mouse) drags the content past the edge; <c>false</c> while a fling bounces past it or the content
    /// springs back.
    /// </summary>
    public bool IsDragging { get; internal set; }
}

/// <summary>
/// A drag released with the content past an edge (<see cref="SkUiScrollView.PullReleased"/>,
/// <see cref="Core.SkUiCoreScrollView.PullReleased"/>), before the content springs back.
/// </summary>
public sealed class SkUiPullReleasedEventArgs(SkUiScrollEdges edge, double distance) : EventArgs
{
    /// <summary>The edge the content was pulled past (one edge; a scroller of both axes reports each).</summary>
    public SkUiScrollEdges Edge { get; } = edge;

    /// <summary>How far the content was shown past the edge, in DIPs (positive).</summary>
    public double Distance { get; } = distance;
}
