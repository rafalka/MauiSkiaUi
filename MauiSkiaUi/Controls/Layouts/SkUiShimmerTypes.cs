using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>How a loading shimmer animates its skeleton (all on the render thread, the skeleton recorded once).</summary>
public enum SkUiShimmerEffect
{
    /// <summary>A band of <c>HighlightColor</c> moves across the bones in <c>WaveDirection</c> (default).</summary>
    Wave,

    /// <summary>The bones fade between <c>PulseOpacity</c> and fully opaque.</summary>
    Pulse,

    /// <summary>The bones' color moves between <c>BoneColor</c> and <c>HighlightColor</c>.</summary>
    Color,

    /// <summary>The bones stand still in <c>BoneColor</c> (what every effect shows while the system reduces motion).</summary>
    None
}

/// <summary>What of its skeleton a shimmer animates.</summary>
public enum SkUiShimmerMode
{
    /// <summary>
    /// Everything the skeleton draws is a bone: the skeleton's silhouette is drawn in <c>BoneColor</c> and animated as one
    /// (default). Build the skeleton from plain shapes, or from real views whose shapes stand for the content.
    /// </summary>
    Silhouette,

    /// <summary>
    /// Only the <see cref="SkUiShimmerBone"/>s (<c>SkUiCoreShimmerBone</c>s) of the skeleton are bones and animate; everything
    /// else draws as it is: cards, dividers, backgrounds, a Cancel button between the bones. No layer per frame.
    /// </summary>
    Bones
}

/// <summary>
/// How a view draws in a skeleton made from a shimmer's content (a shimmer without <c>SkeletonContent</c>, while it is
/// loading): the attached <c>SkUiShimmer.Skeleton</c> (Core: <c>SkUiCoreShimmer.SkeletonProperty</c>).
/// </summary>
public enum SkUiSkeleton
{
    /// <summary>
    /// The control's own bones: a label a bar per line, images, boxes and buttons their shape, toggles, sliders and progress
    /// bars their outline. A view with children (layouts, borders and content views with content, scrollers) is not a bone:
    /// its children are, and its own drawing (a card's background and border) draws as it is in <c>Mode</c>
    /// <see cref="SkUiShimmerMode.Bones"/> and not at all in a <see cref="SkUiShimmerMode.Silhouette"/> (default).
    /// </summary>
    Default,

    /// <summary>The view and everything in it are one bone, its rectangle (with its corner radii and its clip).</summary>
    Bone,

    /// <summary>
    /// The view and everything in it draw as they are (a heading known before the data, an icon). In a silhouette they are
    /// tinted with the bones.
    /// </summary>
    Keep,

    /// <summary>The view and everything in it draw nothing; the view keeps its space.</summary>
    Hidden,

    /// <summary>The view draws nothing of its own (not even a card's chrome in <c>Bones</c> mode); its children draw their bones.</summary>
    Children
}

/// <summary>What a part of a control's skeleton stands for (<see cref="SkUiSkeletonPart"/>): the look turns it into bones.</summary>
public enum SkUiSkeletonPartKind
{
    /// <summary>A shape in its bounds and corner radii: a box, an image, a button's or a field's chrome.</summary>
    Shape,

    /// <summary>A line of text: its bounds are the line (its width and line spacing); by default a rounded bar across it.</summary>
    TextLine,

    /// <summary>A switch in its bounds (the track).</summary>
    Switch,

    /// <summary>A check box: its bounds are the box.</summary>
    CheckBox,

    /// <summary>A radio button: its bounds are the circle.</summary>
    RadioButton,

    /// <summary>A slider in its bounds (the track along the middle).</summary>
    Slider,

    /// <summary>A progress bar in its bounds.</summary>
    ProgressBar,

    /// <summary>An activity indicator in its bounds (the circle centered in them).</summary>
    ActivityIndicator
}

/// <summary>A part of a control in a skeleton made from content, in the control's coordinates (<see cref="SkUiLook.AddSkeletonBones"/>).</summary>
/// <param name="Kind">What the part stands for.</param>
/// <param name="Bounds">Where it is in the control.</param>
/// <param name="CornerRadius">Its corner radii (shapes).</param>
public readonly record struct SkUiSkeletonPart(SkUiSkeletonPartKind Kind, SKRect Bounds, CornerRadius CornerRadius = default);

/// <summary>A bone of a skeleton made from content: a rectangle with corner radii (a circle when they are half its size).</summary>
/// <param name="Bounds">The bone, in the control's coordinates.</param>
/// <param name="CornerRadius">Its corner radii; larger radii than it allows are scaled down.</param>
public readonly record struct SkUiSkeletonBone(SKRect Bounds, CornerRadius CornerRadius = default);

/// <summary>Where a shimmer's wave moves.</summary>
public enum SkUiShimmerWaveDirection
{
    /// <summary>
    /// The reading direction, tilted by the look (<see cref="SkUiLook.ShimmerWaveTilt"/>): left to right, right to left in
    /// right-to-left layouts.
    /// </summary>
    Default,

    /// <summary>From the left edge to the right edge.</summary>
    LeftToRight,

    /// <summary>From the right edge to the left edge.</summary>
    RightToLeft,

    /// <summary>From the top edge to the bottom edge.</summary>
    TopToBottom,

    /// <summary>From the bottom edge to the top edge.</summary>
    BottomToTop
}

/// <summary>What one pass of a shimmer's effect does when it ends.</summary>
public enum SkUiShimmerRepeatMode
{
    /// <summary>The effect's own: a wave starts again from its start; a pulse and a color shift go back (there and back).</summary>
    Default,

    /// <summary>Every pass starts again from the start (a color shift then jumps back to <c>BoneColor</c>).</summary>
    Restart,

    /// <summary>Every other pass runs backwards: a wave sweeps back, a pulse fades back in.</summary>
    Reverse
}

/// <summary>The area one wave crosses in a pass.</summary>
public enum SkUiShimmerWaveRegion
{
    /// <summary>The shimmer's own bounds: the wave crosses each shimmer in one pass (default).</summary>
    Shimmer,

    /// <summary>
    /// The drawn surface the shimmer is on: every shimmer with this region and the same timing shows one wave crossing the
    /// whole surface, as if they were one (separate cards or rows of one loading page).
    /// </summary>
    Surface
}

/// <summary>A shimmer's default colors (<see cref="SkUiLook.GetShimmerColors"/>).</summary>
/// <param name="Bone">The bones' color while <c>BoneColor</c> is not set.</param>
/// <param name="Highlight">The wave's (and a color shift's) color while <c>HighlightColor</c> is not set.</param>
public readonly record struct SkUiShimmerColors(Color Bone, Color Highlight);

/// <summary>
/// A shimmer's wave band (<see cref="SkUiLook.CreateShimmerWaveShader"/>): the band, in the band's own coordinates (centered on
/// the origin; x across it, in the direction the wave moves; y along it), and the wave's color. The shader is made once and
/// the band filled with it is moved across the bones on the render thread, drawn only where bones are.
/// </summary>
/// <param name="Bounds">The band: <c>WaveWidth</c> wide, long enough to cross the region at the wave's angle.</param>
/// <param name="Color">The wave's color (<c>HighlightColor</c>, else the look's).</param>
public readonly record struct SkUiShimmerWavePaint(SKRect Bounds, SKColor Color);
