using SkiaSharp;
using MauiShapes = Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUi;

/// <summary>A shape of either layer as the outline of a border.</summary>
internal interface ISkUiShapeOutline
{
    /// <summary>The fill rule of the geometry.</summary>
    WindingMode Winding { get; }

    /// <summary>The geometry placed in <paramref name="bounds"/> without the shape's own stroke inset.</summary>
    PathF GetOutline(Rect bounds);

    /// <summary>A rectangle or rounded rectangle: its radii, so the border draws it with the look's rounded geometry.</summary>
    bool TryGetRoundRect(out CornerRadius radii);
}

/// <summary>
/// The geometry of a border of either layer for its <c>StrokeShape</c> (or, without one, a rectangle rounded by its
/// <c>CornerRadius</c>): the outline the stroke is centered on, inset by half the stroke so the stroke stays inside the
/// bounds (as MAUI's Android and Windows borders), filled by the background; and the content clip at the stroke's inner
/// edge. Both are cached until the size, stroke, shape (<see cref="Invalidate"/>) or look change.
/// </summary>
internal sealed class SkUiBorderGeometry
{
    private SKPath? _outline;
    private SKPath? _clip;
    private Key _outlineKey;
    private Key _clipKey;
    private int _version;

    private readonly record struct Key(float Width, float Height, double Thickness, IShape? Shape, CornerRadius Radii, SkUiLook Look, int Version);

    /// <summary>Drops the cached paths after the stroke shape changed inside (one of its properties).</summary>
    public void Invalidate() => _version++;

    /// <summary>The outline the fill and the stroke follow; owned by this cache (recording copies it).</summary>
    public SKPath Outline(IShape? shape, CornerRadius radii, float width, float height, double thickness)
    {
        var key = new Key(width, height, thickness, shape, radii, SkUiLook.Current, _version);
        if (_outline is null || key != _outlineKey)
        {
            _outline?.Dispose();
            var inset = (float)thickness / 2;
            _outline = Create(shape, radii, new SKRect(inset, inset, Math.Max(inset, width - inset), Math.Max(inset, height - inset)));
            _outlineKey = key;
        }
        return _outline;
    }

    /// <summary>
    /// The content clip: inside the stroke's inner edge. Committed to the compositor, so it is never disposed here (the
    /// render thread may still draw with it; the GC finalizes it).
    /// </summary>
    public SKPath ContentClip(IShape? shape, CornerRadius radii, float width, float height, double thickness)
    {
        var key = new Key(width, height, thickness, shape, radii, SkUiLook.Current, _version);
        if (_clip is not null && key == _clipKey)
            return _clip;
        _clipKey = key;
        var t = (float)thickness;
        if (RoundRectRadii(shape, radii) is { } rounded)
        {
            // Concentric with the stroke: inset by the whole stroke, radii by half of it.
            var inner = new SKRect(t, t, Math.Max(t, width - t), Math.Max(t, height - t));
            return _clip = SkUiLook.Current.CreateRoundRectPath(inner, Shrink(rounded, t / 2));
        }
        var outline = Outline(shape, radii, width, height, thickness);
        if (t <= 0)
            return _clip = new SKPath(outline);
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = t, StrokeJoin = SKStrokeJoin.Round };
        using var strokeArea = paint.GetFillPath(outline);
        return _clip = outline.Op(strokeArea, SKPathOp.Difference) ?? new SKPath(outline);
    }

    private static SKPath Create(IShape? shape, CornerRadius radii, SKRect bounds)
    {
        if (RoundRectRadii(shape, radii) is { } rounded)
            return SkUiLook.Current.CreateRoundRectPath(bounds, rounded);
        var rect = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        return shape switch
        {
            ISkUiShapeOutline own => SkUiShapeGeometry.ToSkia(own.GetOutline(rect), own.Winding),
            // MAUI's ellipse insets itself by its own stroke and is placed only on device builds; draw it directly.
            MauiShapes.Ellipse => SkUiShapeGeometry.ToSkia(SkUiShapeGeometry.Ellipse(rect)),
            MauiShapes.Path { Data: var data } => SkUiShapeGeometry.ToSkia(shape.PathForBounds(rect), data switch
            {
                MauiShapes.PathGeometry { FillRule: MauiShapes.FillRule.Nonzero } or MauiShapes.GeometryGroup { FillRule: MauiShapes.FillRule.Nonzero } => WindingMode.NonZero,
                _ => WindingMode.EvenOdd
            }),
            _ => SkUiShapeGeometry.ToSkia(shape!.PathForBounds(rect))
        };
    }

    /// <summary>The radii of a border drawn as a rounded rectangle (no shape, or a rectangle shape of either layer or MAUI's).</summary>
    private static CornerRadius? RoundRectRadii(IShape? shape, CornerRadius radii) => shape switch
    {
        null => radii,
        ISkUiShapeOutline own => own.TryGetRoundRect(out var rounded) ? rounded : null,
        MauiShapes.RoundRectangle roundRectangle => roundRectangle.CornerRadius,
        MauiShapes.Rectangle rectangle => new CornerRadius(Math.Max(rectangle.RadiusX, rectangle.RadiusY)),
        _ => null
    };

    private static CornerRadius Shrink(CornerRadius radii, double inset) => new(
        Math.Max(0, radii.TopLeft - inset), Math.Max(0, radii.TopRight - inset),
        Math.Max(0, radii.BottomLeft - inset), Math.Max(0, radii.BottomRight - inset));
}
