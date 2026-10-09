using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn node of either layer that can say what silhouette its shadow has.</summary>
internal interface ISkUiShadowCaster
{
    /// <summary>
    /// The outline of the node's own opaque fill, in local DIPs, when that fill is its silhouette (MAUI on Android draws such
    /// shadows from the background's shape); <c>null</c> to cast the shadow from everything the node and its children draw.
    /// A new path the caller owns.
    /// </summary>
    SKPath? CreateShadowOutline(float width, float height);
}

/// <summary>
/// The clip and drop shadow of a drawn node of either layer (MAUI's <c>Clip</c> and <c>Shadow</c>), resolved into the
/// composite-time clip path and shadow the compositor draws. Kept while nothing they depend on changes, so committing
/// the same frame twice commits the same objects (no re-record, no new blur). Owners bump the versions on changes.
/// </summary>
internal sealed class SkUiVisualEffects
{
    private SKPath? _clipPath;
    private (IShape? Shape, float Width, float Height, int Version, SkUiLook Look) _clipKey;
    private SkUiShadowStyle? _style;
    private (IShadow? Shadow, float Width, float Height, int Version) _styleKey;
    private SkUiRenderShadow? _shadow;
    private (SkUiShadowStyle? Style, SKPath? Clip, SKRect Source, bool Clipped, int Version, SkUiLook Look) _shadowKey;

    /// <summary>Bumped when the clip shape changed inside (its geometry's properties).</summary>
    public int ClipVersion;

    /// <summary>Bumped when the shadow changed inside (its brush, radius, …).</summary>
    public int ShadowVersion;

    /// <summary>Bumped when the node's paint changed: its fill, and so its shadow's silhouette, may differ.</summary>
    public int PaintVersion;

    /// <summary>
    /// Forgets the clip path and shadow committed so far (the node was reset: it left its drawn parent or its surface).
    /// The compositor disposes them with the node's render node, so they are never committed or read again; the next record
    /// makes new ones.
    /// </summary>
    public void Forget()
    {
        _clipPath = null;
        _clipKey = default;
        _style = null;
        _styleKey = default;
        _shadow = null;
        _shadowKey = default;
    }

    /// <summary>The clip of a node of <paramref name="width"/> × <paramref name="height"/>; <c>null</c> without a clip shape.</summary>
    public SKPath? GetClipPath(IShape? clip, float width, float height)
    {
        if (clip is null)
            return _clipPath = null;
        var key = (clip, width, height, ClipVersion, SkUiLook.Current);
        if (_clipPath is not null && key == _clipKey)
            return _clipPath;
        _clipKey = key;
        // Committed to the compositor, which disposes it once a commit replaces it or its render node goes (the render thread
        // may still clip with it until then): never disposed here.
        return _clipPath = SkUiClipGeometry.CreatePath(clip, width, height);
    }

    /// <summary>The shadow of a node with these <paramref name="props"/> (clip path set), or <c>null</c> when it casts none.</summary>
    public SkUiRenderShadow? GetShadow(IShadow? shadow, ISkUiShadowCaster caster, in SkUiRenderProps props)
    {
        if (shadow is null || props.Width <= 0 || props.Height <= 0)
            return _shadow = null;
        var styleKey = (shadow, props.Width, props.Height, ShadowVersion);
        if (styleKey != _styleKey)
        {
            _styleKey = styleKey;
            _style = SkUiShadowStyle.Create(shadow, props.Bounds);
        }
        if (_style is not { } style)
            return _shadow = null;
        var source = props.ClipToBounds ? props.Bounds : props.VisualBounds;
        var key = (style, props.ClipPath, source, props.ClipToBounds, PaintVersion, SkUiLook.Current);
        if (_shadow is not null && key == _shadowKey)
            return _shadow;
        _shadowKey = key;
        var outline = caster.CreateShadowOutline(props.Width, props.Height);
        if (outline is not null && props.ClipPath is { } clip)
        {
            var intersection = outline.Op(clip, SKPathOp.Intersect);
            outline.Dispose();
            outline = intersection;
        }
        if (props.ClipPath is { } clipped && !source.IntersectsWith(clipped.Bounds))
            source = SKRect.Empty;
        else if (props.ClipPath is { } clipBounds)
            source = SKRect.Intersect(source, clipBounds.Bounds);
        return _shadow = new SkUiRenderShadow(style, outline, source);
    }
}

/// <summary>Clip shapes of either layer as Skia paths (MAUI's <c>Clip</c>; a Core node's clip).</summary>
internal static class SkUiClipGeometry
{
    /// <summary>
    /// <paramref name="shape"/> as a clip of a node of <paramref name="width"/> × <paramref name="height"/>: a MAUI geometry in
    /// its own coordinates with its fill rule (as MAUI's <c>Clip</c>), a shape of either drawn layer placed in the bounds
    /// (without a stroke inset), or any other <see cref="IShape"/>'s <c>PathForBounds</c>.
    /// </summary>
    public static SKPath CreatePath(IShape shape, float width, float height)
    {
        var bounds = new Rect(0, 0, width, height);
        return shape switch
        {
            ISkUiShapeOutline own => SkUiShapeGeometry.ToSkia(own.GetOutline(bounds), own.Winding),
            Microsoft.Maui.Controls.Shapes.Geometry geometry => SkUiShapeGeometry.ToSkia(shape.PathForBounds(bounds), FillRule(geometry)),
            _ => SkUiShapeGeometry.ToSkia(shape.PathForBounds(bounds))
        };
    }

    /// <summary>A path or group geometry's fill rule; the other geometries are single figures.</summary>
    private static WindingMode FillRule(Microsoft.Maui.Controls.Shapes.Geometry geometry) => geometry switch
    {
        Microsoft.Maui.Controls.Shapes.PathGeometry { FillRule: Microsoft.Maui.Controls.Shapes.FillRule.EvenOdd } => WindingMode.EvenOdd,
        Microsoft.Maui.Controls.Shapes.GeometryGroup { FillRule: Microsoft.Maui.Controls.Shapes.FillRule.EvenOdd } => WindingMode.EvenOdd,
        _ => WindingMode.NonZero
    };
}
