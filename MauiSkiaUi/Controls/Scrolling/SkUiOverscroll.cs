using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// How a scroller shows overscroll through its generic children transform: <see cref="SkUiOverscrollMode.Bounce"/> moves
/// the children offset past its range, <see cref="SkUiOverscrollMode.Stretch"/> scales the children away from the pulled
/// edge. Shared by the UI thread (drags) and the render-thread animations (fling bounce, spring-back), so both draw and
/// hit-test the same.
/// </summary>
internal static class SkUiOverscroll
{
    /// <summary>
    /// How much a stretch scales the content: by this fraction of the overscroll distance over the viewport length (a 100 DIP
    /// overscroll of an 800 DIP viewport stretches it by about 4 %).
    /// </summary>
    internal const float StretchFactor = 0.3f;

    /// <summary>Resistance of the rubber band (UIScrollView's constant).</summary>
    private const double RubberBandConstant = 0.55;

    /// <summary>
    /// Writes the scroll offset (<paramref name="x"/>, <paramref name="y"/>, within range) and the overscroll past the
    /// start (negative) or end (positive) of each axis into the children offset and scale. A stretch scales about the
    /// pulled edge, which the UI side sets as <see cref="SkUiRenderProps.ChildrenScaleOrigin"/>.
    /// </summary>
    public static void Apply(ref SkUiRenderProps props, float x, float y, float overscrollX, float overscrollY,
        SkUiOverscrollMode mode, float width, float height)
    {
        props.ChildrenOffsetX = x;
        props.ChildrenOffsetY = y;
        props.ChildrenScaleX = props.ChildrenScaleY = 1;
        if (mode == SkUiOverscrollMode.Bounce)
        {
            props.ChildrenOffsetX += overscrollX;
            props.ChildrenOffsetY += overscrollY;
        }
        else if (mode == SkUiOverscrollMode.Stretch)
        {
            if (width > 0)
                props.ChildrenScaleX = 1 + StretchFactor * Math.Abs(overscrollX) / width;
            if (height > 0)
                props.ChildrenScaleY = 1 + StretchFactor * Math.Abs(overscrollY) / height;
        }
    }

    /// <summary>
    /// The distance shown for a pull of <paramref name="pull"/> DIPs past an edge of a viewport <paramref name="length"/>
    /// long (UIScrollView's rubber band): it follows the finger at first, then resists more and more, never reaching the
    /// viewport length.
    /// </summary>
    public static double RubberBand(double pull, double length)
    {
        if (pull == 0 || length <= 0)
            return 0;
        return Math.Sign(pull) * (1 - 1 / (Math.Abs(pull) * RubberBandConstant / length + 1)) * length;
    }

    /// <summary>The pull that shows <paramref name="shown"/> DIPs (inverse of <see cref="RubberBand"/>).</summary>
    public static double InverseRubberBand(double shown, double length)
    {
        if (shown == 0 || length <= 0)
            return 0;
        var fraction = Math.Min(Math.Abs(shown) / length, 0.99);
        return Math.Sign(shown) * (1 / (1 - fraction) - 1) * length / RubberBandConstant;
    }
}
