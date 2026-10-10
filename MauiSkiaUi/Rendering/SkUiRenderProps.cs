using SkiaSharp;

namespace MauiSkiaUi.Rendering;

/// <summary>
/// Composite-time properties of one render node, in DIPs, relative to the parent's children space.
/// These are applied by the compositor without re-recording the node's pictures, so changing them
/// (offset, transform, opacity, scroll offset) is cheap and animatable on the render thread.
/// </summary>
internal record struct SkUiRenderProps
{
    public float X, Y, Width, Height;
    public float TranslationX, TranslationY, Rotation, ScaleX, ScaleY, AnchorX, AnchorY;
    public float Opacity;

    /// <summary>
    /// Turns the node about its vertical axis through the anchor, in perspective, in degrees (positive: its right edge goes
    /// away); 0 = none. Applied before <see cref="ScaleX"/> / <see cref="ScaleY"/> and <see cref="Rotation"/>.
    /// </summary>
    public float RotationY;

    /// <summary>Turns the node about its horizontal axis through the anchor, in perspective, in degrees (positive: its bottom edge goes away); 0 = none.</summary>
    public float RotationX;

    /// <summary>How far the eye is from the node for <see cref="RotationX"/> / <see cref="RotationY"/>, in DIPs (at least the node's size).</summary>
    public float CameraDistance;

    /// <summary>
    /// A transform of the node from an ancestor scroller's offset, evaluated on the render thread every frame (a carousel's
    /// item effect); <c>null</c> = none. The UI side writes the same placement (not the opacity) into the node's transform
    /// for hit-testing and immediate painting. Immutable once committed.
    /// </summary>
    public SkUiItemEffectLink? ItemEffect;

    /// <summary>
    /// Children are drawn back to front by the depth of their <see cref="ItemEffect"/> at the current scroll offset (a
    /// carousel whose effect overlaps items), instead of in their order.
    /// </summary>
    public bool SortsChildrenByDepth;
    public bool IsVisible;

    /// <summary>Clips the node's own content, children and overlay to its layout rectangle.</summary>
    public bool ClipToBounds;

    /// <summary>Translation applied to children only (scroll offset), subtracted from child positions.</summary>
    public float ChildrenOffsetX, ChildrenOffsetY;

    /// <summary>
    /// Scale applied to children only, about <see cref="ChildrenScaleOrigin"/>, after <see cref="ChildrenOffsetX"/> /
    /// <see cref="ChildrenOffsetY"/> (a scroller's stretch overscroll); 1 = none.
    /// </summary>
    public float ChildrenScaleX, ChildrenScaleY;

    /// <summary>Fixed point of <see cref="ChildrenScaleX"/> / <see cref="ChildrenScaleY"/>, in local coordinates.</summary>
    public SKPoint ChildrenScaleOrigin;

    /// <summary>
    /// The node is placed in its parent's local coordinates instead of the parent's children space: the parent's children
    /// offset, scale and clip (<see cref="ChildrenClipRect"/>, <see cref="ChildrenClipPath"/>) do not apply to it, only the
    /// parent's own clips (scroll bars, also in a gutter beside a scroller's viewport; pinned headers). Pinned children draw
    /// after (above) their unpinned siblings.
    /// </summary>
    public bool Pinned;

    /// <summary>
    /// Makes <see cref="TranslationX"/> / <see cref="TranslationY"/> follow an ancestor's children offset on the render thread
    /// (scroll-linked placement); <c>null</c> = none. Immutable once committed.
    /// </summary>
    public SkUiRenderLink? Link;

    /// <summary>Optional rectangular clip for children (local coordinates, before children offset); empty = none.</summary>
    public SKRect ChildrenClipRect;

    /// <summary>Optional shaped clip for children (e.g. rounded border). Treated as immutable once committed.</summary>
    public SKPath? ChildrenClipPath;

    /// <summary>Seconds per full revolution of the content picture about its center; 0 = static.</summary>
    public float ContentSpinPeriod;

    /// <summary>
    /// Seconds per loop of a horizontal content slide (marquee); 0 = static. The compositor shifts the content picture
    /// by <see cref="ContentSlideDistance"/> per period and draws a second copy one distance behind, so a picture one
    /// distance wide repeats seamlessly (indeterminate progress bars).
    /// </summary>
    public float ContentSlidePeriod;

    /// <summary>Horizontal DIPs the content moves per <see cref="ContentSlidePeriod"/> (negative = right to left).</summary>
    public float ContentSlideDistance;

    /// <summary>Clip for sliding content (e.g. rounded bar ends); <c>null</c> = the layout rectangle. Treated as immutable once committed.</summary>
    public SKPath? ContentClipPath;

    /// <summary>
    /// Ink overflow outside the layout rectangle (Left/Top/Right/Bottom as positive amounts), e.g. shadows (FR-20).
    /// Culling, recording cull rects and opacity layers use <see cref="VisualBounds"/>.
    /// </summary>
    public SKRect Overflow;

    /// <summary>
    /// Shaped clip of the whole node: its content, children and overlay (MAUI's <c>Clip</c>), local coordinates; <c>null</c>
    /// = none. Paint only: hit-testing keeps the layout rectangle. Treated as immutable once committed.
    /// </summary>
    public SKPath? ClipPath;

    /// <summary>Drop shadow drawn before the node, outside its clips (FR-20); <c>null</c> = none. Immutable once committed.</summary>
    public SkUiRenderShadow? Shadow;

    public static SkUiRenderProps Default => new()
    {
        ScaleX = 1, ScaleY = 1, AnchorX = 0.5f, AnchorY = 0.5f, Opacity = 1, IsVisible = true, ClipToBounds = true,
        ChildrenScaleX = 1, ChildrenScaleY = 1
    };

    /// <summary>Local layout rectangle.</summary>
    public readonly SKRect Bounds => new(0, 0, Width, Height);

    /// <summary>Local ink rectangle including <see cref="Overflow"/>.</summary>
    public readonly SKRect VisualBounds => new(-Overflow.Left, -Overflow.Top, Width + Overflow.Right, Height + Overflow.Bottom);

    /// <summary>Everything the node draws: <see cref="VisualBounds"/> and its <see cref="Shadow"/>.</summary>
    public readonly SKRect InkBounds => Shadow is { } shadow ? SKRect.Union(VisualBounds, shadow.Bounds) : VisualBounds;

    /// <summary>
    /// The bounds of the node's opacity layer: the layout rectangle when it clips to it and casts no shadow, else everything
    /// it draws.
    /// </summary>
    public readonly SKRect LayerBounds => ClipToBounds && Shadow is null ? Bounds : InkBounds;

    /// <summary>True when the node paints nothing and its subtree can be skipped.</summary>
    public readonly bool IsSkipped => !IsVisible || Opacity <= 0 || Width <= 0 || Height <= 0;

    /// <summary>Parent-space matrix: layout offset, then render transform about the anchor.</summary>
    public readonly SKMatrix Matrix => GetMatrix(TranslationX, TranslationY);

    /// <summary><see cref="Matrix"/> with another translation (a <see cref="Link"/> evaluated on the render thread).</summary>
    public readonly SKMatrix GetMatrix(float translationX, float translationY)
    {
        // Exact comparisons: identity values are the common case and skip the matrix products.
        if (translationX == 0 && translationY == 0 && Rotation == 0 && ScaleX == 1 && ScaleY == 1 && RotationX == 0 && RotationY == 0)
            return SKMatrix.CreateTranslation(X, Y);
        var anchorX = Width * AnchorX;
        var anchorY = Height * AnchorY;
        var matrix = SKMatrix.CreateTranslation(X + translationX + anchorX, Y + translationY + anchorY)
            .PreConcat(SKMatrix.CreateRotationDegrees(Rotation))
            .PreConcat(SKMatrix.CreateScale(ScaleX, ScaleY));
        if (RotationX != 0 || RotationY != 0)
            matrix = matrix.PreConcat(Perspective(RotationX, RotationY, Math.Max(CameraDistance, Math.Max(Width, Height))));
        return matrix.PreConcat(SKMatrix.CreateTranslation(-anchorX, -anchorY));
    }

    /// <summary>
    /// Turns about the vertical (<paramref name="rotationY"/>) and horizontal (<paramref name="rotationX"/>) axes through the
    /// origin, seen from <paramref name="distance"/> DIPs in front: a projective 2D matrix (what a 3D rotation shows).
    /// </summary>
    internal static SKMatrix Perspective(float rotationX, float rotationY, float distance)
    {
        var x = rotationX * MathF.PI / 180;
        var y = rotationY * MathF.PI / 180;
        var d = Math.Max(1, distance);
        // x' = x·cos(y) / w, y' = y·cos(x) / w with w = 1 + (x·sin(y) + y·sin(x)) / d: the far edge shrinks towards the axis.
        return new SKMatrix(MathF.Cos(y), 0, 0, 0, MathF.Cos(x), 0, MathF.Sin(y) / d, MathF.Sin(x) / d, 1);
    }

    /// <summary>Whether children are scaled (<see cref="ChildrenScaleX"/> / <see cref="ChildrenScaleY"/> other than 1).</summary>
    public readonly bool HasChildrenScale => ChildrenScaleX != 1 || ChildrenScaleY != 1;

    /// <summary>Applies the children transform (scale about its origin, then the children offset) to <paramref name="canvas"/>.</summary>
    public readonly void TransformChildren(SKCanvas canvas)
    {
        if (HasChildrenScale)
            canvas.Scale(ChildrenScaleX, ChildrenScaleY, ChildrenScaleOrigin.X, ChildrenScaleOrigin.Y);
        if (ChildrenOffsetX != 0 || ChildrenOffsetY != 0)
            canvas.Translate(-ChildrenOffsetX, -ChildrenOffsetY);
    }

    /// <summary>Children space to local coordinates: the children scale about its origin after the children offset.</summary>
    public readonly SKMatrix ChildrenMatrix
    {
        get
        {
            var offset = SKMatrix.CreateTranslation(-ChildrenOffsetX, -ChildrenOffsetY);
            return HasChildrenScale
                ? SKMatrix.CreateScale(ChildrenScaleX, ChildrenScaleY, ChildrenScaleOrigin.X, ChildrenScaleOrigin.Y).PreConcat(offset)
                : offset;
        }
    }

    /// <summary>Maps a local point into the children space (inverse of <see cref="TransformChildren"/>); a pinned child uses <paramref name="local"/>.</summary>
    public readonly SKPoint MapToChildren(SKPoint local)
    {
        if (HasChildrenScale && ChildrenScaleX != 0 && ChildrenScaleY != 0)
            local = new SKPoint(
                ChildrenScaleOrigin.X + (local.X - ChildrenScaleOrigin.X) / ChildrenScaleX,
                ChildrenScaleOrigin.Y + (local.Y - ChildrenScaleOrigin.Y) / ChildrenScaleY);
        return new SKPoint(local.X + ChildrenOffsetX, local.Y + ChildrenOffsetY);
    }

    /// <summary>Reads an animatable property.</summary>
    public readonly float Get(SkUiRenderProperty property) => property switch
    {
        SkUiRenderProperty.TranslationX => TranslationX,
        SkUiRenderProperty.TranslationY => TranslationY,
        SkUiRenderProperty.Rotation => Rotation,
        SkUiRenderProperty.ScaleX => ScaleX,
        SkUiRenderProperty.ScaleY => ScaleY,
        SkUiRenderProperty.Opacity => Opacity,
        SkUiRenderProperty.ChildrenOffsetX => ChildrenOffsetX,
        SkUiRenderProperty.ChildrenOffsetY => ChildrenOffsetY,
        SkUiRenderProperty.ChildrenScaleX => ChildrenScaleX,
        SkUiRenderProperty.ChildrenScaleY => ChildrenScaleY,
        _ => throw new ArgumentOutOfRangeException(nameof(property))
    };

    /// <summary>Writes an animatable property.</summary>
    public void Set(SkUiRenderProperty property, float value)
    {
        switch (property)
        {
            case SkUiRenderProperty.TranslationX: TranslationX = value; break;
            case SkUiRenderProperty.TranslationY: TranslationY = value; break;
            case SkUiRenderProperty.Rotation: Rotation = value; break;
            case SkUiRenderProperty.ScaleX: ScaleX = value; break;
            case SkUiRenderProperty.ScaleY: ScaleY = value; break;
            case SkUiRenderProperty.Opacity: Opacity = value; break;
            case SkUiRenderProperty.ChildrenOffsetX: ChildrenOffsetX = value; break;
            case SkUiRenderProperty.ChildrenOffsetY: ChildrenOffsetY = value; break;
            case SkUiRenderProperty.ChildrenScaleX: ChildrenScaleX = value; break;
            case SkUiRenderProperty.ChildrenScaleY: ChildrenScaleY = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(property));
        }
    }

    /// <summary>Bit mask of animatable properties whose values differ between two snapshots.</summary>
    public static int ChangedAnimatable(in SkUiRenderProps a, in SkUiRenderProps b)
    {
        var mask = 0;
        for (var property = 0; property < SkUiRenderPropertyCount.Value; property++)
            if (a.Get((SkUiRenderProperty)property) != b.Get((SkUiRenderProperty)property))
                mask |= 1 << property;
        return mask;
    }
}

/// <summary>Render-thread animatable properties.</summary>
internal enum SkUiRenderProperty
{
    TranslationX,
    TranslationY,
    Rotation,
    ScaleX,
    ScaleY,
    Opacity,
    ChildrenOffsetX,
    ChildrenOffsetY,
    ChildrenScaleX,
    ChildrenScaleY
}

internal static class SkUiRenderPropertyCount
{
    public const int Value = 10;
}

/// <summary>
/// Scroll-linked placement (<see cref="SkUiRenderProps.Link"/>): per axis, the node's translation is
/// <c>clamp(Factor · offset + Base, Min, Max)</c>, with <c>offset</c> the children offset of <see cref="Source"/>, an ancestor
/// render node. The compositor evaluates it every frame, so a node placed by a scroll offset (a scroll bar thumb, a pinned
/// header, parallax) follows render-thread flings without UI-thread work; the UI side writes the same value into the
/// node's translation for hit-testing and immediate painting.
/// </summary>
internal sealed record SkUiRenderLink(
    SkUiRenderNode Source,
    float FactorX, float BaseX, float MinX, float MaxX,
    float FactorY, float BaseY, float MinY, float MaxY)
{
    /// <summary>The translation for an ancestor children offset.</summary>
    public SKPoint Evaluate(float offsetX, float offsetY) => new(
        Math.Clamp(FactorX * offsetX + BaseX, MinX, Math.Max(MinX, MaxX)),
        Math.Clamp(FactorY * offsetY + BaseY, MinY, Math.Max(MinY, MaxY)));
}

/// <summary>
/// A carousel item's transform from its scroller's offset (<see cref="SkUiRenderProps.ItemEffect"/>): the item's position
/// in items from the carousel's current place is <c>Factor · offset + Base</c> along the scroll axis, and
/// <see cref="Effect"/> turns it into a placement, scale, tilt, opacity and depth. The compositor evaluates it every frame
/// against <see cref="Source"/>'s children offset (possibly render-thread animated), so effects follow flings without
/// UI-thread work.
/// </summary>
internal sealed class SkUiItemEffectLink(SkUiRenderNode source, bool horizontal, float factor, float @base, SkUiCarouselEffect effect, SkUiCarouselItemMetrics metrics)
{
    /// <summary>The largest tilt applied, in degrees.</summary>
    private const double MaxTilt = 80;

    /// <summary>The scroller whose children offset places the item.</summary>
    public SkUiRenderNode Source { get; } = source;

    public bool Horizontal { get; } = horizontal;

    public float Factor { get; } = factor;

    public float Base { get; } = @base;

    public SkUiCarouselEffect Effect { get; } = effect;

    public SkUiCarouselItemMetrics Metrics { get; } = metrics;

    /// <summary>The item's position in items from the current place for a children offset.</summary>
    public double Position(float offsetX, float offsetY) => Factor * (Horizontal ? offsetX : offsetY) + Base;

    /// <summary>The effect's transform for a children offset.</summary>
    public SkUiCarouselItemTransform Evaluate(float offsetX, float offsetY) => Effect.GetItemTransform(Position(offsetX, offsetY), Metrics);

    /// <summary>
    /// Writes the transform's placement into <paramref name="props"/> (replacing the node's own translation, scale, rotation
    /// and tilt: carousel items have none of their own) and returns its opacity, which the caller applies: the UI side keeps
    /// the node's opacity, so an item faded out is still recorded.
    /// </summary>
    public double Apply(ref SkUiRenderProps props, float offsetX, float offsetY)
    {
        var transform = Evaluate(offsetX, offsetY);
        var along = (float)transform.Translation;
        var across = (float)transform.CrossTranslation;
        props.TranslationX = Horizontal ? along : across;
        props.TranslationY = Horizontal ? across : along;
        props.ScaleX = props.ScaleY = (float)Math.Max(0, transform.Scale);
        props.Rotation = (float)transform.Rotation;
        // Past 90° the item would turn its back; near it, its far edge would cross the eye.
        var tilt = (float)Math.Clamp(transform.Tilt, -MaxTilt, MaxTilt);
        props.RotationY = Horizontal ? tilt : 0;
        props.RotationX = Horizontal ? 0 : tilt;
        props.CameraDistance = (float)Effect.GetPerspective(Metrics);
        return transform.Opacity;
    }
}
