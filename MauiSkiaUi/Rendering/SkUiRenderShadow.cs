using SkiaSharp;

namespace MauiSkiaUi.Rendering;

/// <summary>
/// The look of a node's drop shadow (FR-20): its color or gradient, opacity, offset and blur, in the node's local DIPs.
/// A node keeps one while its shadow and size stay the same, so the native shader and blur filters are made once.
/// Immutable once committed: the render thread draws with it.
/// </summary>
internal sealed class SkUiShadowStyle : IDisposable
{
    /// <summary>The look of <paramref name="shadow"/> (a gradient maps onto <paramref name="bounds"/>), or <c>null</c> when it draws nothing.</summary>
    public static SkUiShadowStyle? Create(IShadow shadow, SKRect bounds)
    {
        var opacity = float.IsFinite(shadow.Opacity) ? Math.Clamp(shadow.Opacity, 0, 1) : 1;
        if (opacity <= 0 || !SkUiShapePainter.TryCreate(shadow.Paint, bounds, out var color, out var shader))
            return null;
        var radius = float.IsFinite(shadow.Radius) ? Math.Max(0, shadow.Radius) : 0;
        var offset = shadow.Offset;
        return new SkUiShadowStyle(color, shader, (byte)Math.Round(255 * opacity),
            double.IsFinite(offset.X) ? (float)offset.X : 0, double.IsFinite(offset.Y) ? (float)offset.Y : 0,
            SKMaskFilter.ConvertRadiusToSigma(radius));
    }

    private bool _disposed;

    private SkUiShadowStyle(SKColor color, SKShader? shader, byte alpha, float offsetX, float offsetY, float sigma)
    {
        Color = color;
        Shader = shader;
        Alpha = alpha;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Sigma = sigma;
        if (sigma > 0)
        {
            Blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigma);
            LayerBlur = SKImageFilter.CreateBlur(sigma, sigma);
        }
    }

    /// <summary>The solid color (white under a gradient <see cref="Shader"/>).</summary>
    public SKColor Color { get; }

    /// <summary>The gradient, mapped onto the node's bounds; <c>null</c> for a solid shadow.</summary>
    public SKShader? Shader { get; }

    /// <summary>MAUI's <c>Shadow.Opacity</c>, multiplied into the color.</summary>
    public byte Alpha { get; }

    /// <summary>Offset of the shadow from the node, in DIPs.</summary>
    public float OffsetX { get; }

    /// <inheritdoc cref="OffsetX" />
    public float OffsetY { get; }

    /// <summary>Gaussian sigma of the blur, from MAUI's <c>Shadow.Radius</c> as Android and Skia convert a blur radius.</summary>
    public float Sigma { get; }

    /// <summary>How far the blur spreads past its source, in DIPs (three sigmas cover all but a fraction of a percent).</summary>
    public float Spread => Sigma * 3;

    /// <summary>The blur of an outline shadow; <c>null</c> without blur.</summary>
    public SKMaskFilter? Blur { get; }

    /// <summary>The blur of a shadow from the node's content; <c>null</c> without blur.</summary>
    public SKImageFilter? LayerBlur { get; }

    /// <summary>
    /// Releases the native shader and filters (render thread, once no committed frame uses this style). Idempotent: shadows
    /// of one node share their style, so a teardown may release it from several props.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Shader?.Dispose();
        Blur?.Dispose();
        LayerBlur?.Dispose();
    }

    /// <summary>Sets <paramref name="paint"/> to the shadow's color or gradient and opacity.</summary>
    public void Apply(SKPaint paint)
    {
        if (Shader is { } shader)
        {
            paint.Color = SKColors.White.WithAlpha(Alpha);
            paint.Shader = shader;
        }
        else
        {
            paint.Color = Color.WithAlpha((byte)(Color.Alpha * Alpha / 255));
        }
    }
}

/// <summary>
/// A node's drop shadow as the compositor draws it, in local DIPs: the <see cref="Style"/>, and what casts it, either the
/// <see cref="Outline"/> of an opaque fill (blurred directly, cheap and cached by Skia) or, without one, whatever the node
/// and its children draw inside <see cref="Source"/> (their alpha, rasterized once by the compositor and reused until the
/// subtree changes). Drawn before the node and outside its own clips; it changes neither layout nor hit-testing.
/// Immutable once committed.
/// </summary>
internal sealed class SkUiRenderShadow(SkUiShadowStyle style, SKPath? outline, SKRect source)
{
    /// <inheritdoc cref="SkUiShadowStyle" />
    public SkUiShadowStyle Style { get; } = style;

    /// <summary>The silhouette of an opaque fill (clipped by the node's clip); <c>null</c>: the shadow of the drawn content.</summary>
    public SKPath? Outline { get; } = outline;

    /// <summary>Where the node draws (its visual bounds); the content shadow is taken from this area.</summary>
    public SKRect Source { get; } = source;

    /// <summary>The area the shadow casts from before the offset: the outline's or the source's bounds, grown by the blur.</summary>
    public SKRect CastArea
    {
        get
        {
            var area = Outline?.Bounds ?? Source;
            area.Inflate(Style.Spread, Style.Spread);
            return area;
        }
    }

    /// <summary>Where the shadow lands, for culling and opacity layers.</summary>
    public SKRect Bounds
    {
        get
        {
            var area = CastArea;
            area.Offset(Style.OffsetX, Style.OffsetY);
            return area;
        }
    }
}

/// <summary>
/// Draws <see cref="SkUiRenderShadow"/>s for the compositor and the immediate painter, so both produce the same pixels.
/// </summary>
internal static class SkUiShadowPainter
{
    /// <summary>Largest raster a content shadow is cached at, per side, in pixels (larger ones are cached at a lower resolution).</summary>
    internal const int MaxCacheSize = 2048;

    /// <summary>Blurs and fills the outline at the shadow's offset.</summary>
    public static void DrawOutline(SKCanvas canvas, SkUiRenderShadow shadow, SKPaint paint)
    {
        var style = shadow.Style;
        paint.Reset();
        paint.IsAntialias = true;
        style.Apply(paint);
        paint.MaskFilter = style.Blur;
        var save = canvas.Save();
        canvas.Translate(style.OffsetX, style.OffsetY);
        canvas.DrawPath(shadow.Outline!, paint);
        canvas.RestoreToCount(save);
        paint.Reset();
    }

    /// <summary>
    /// The shadow of what <paramref name="drawBody"/> draws: its alpha blurred and filled with the shadow's color or
    /// gradient, at the offset unless <paramref name="applyOffset"/> is <c>false</c> (a cached raster is offset when drawn).
    /// </summary>
    public static void DrawFromContent<TState>(SKCanvas canvas, SkUiRenderShadow shadow, SKPaint paint, TState state,
        Action<SKCanvas, TState> drawBody, bool applyOffset = true)
    {
        var style = shadow.Style;
        var area = shadow.CastArea;
        var save = canvas.Save();
        if (applyOffset)
            canvas.Translate(style.OffsetX, style.OffsetY);
        // The outer layer keeps the color fill (SrcIn) from reaching what is already drawn below.
        canvas.SaveLayer(area, null);
        paint.Reset();
        paint.ImageFilter = style.LayerBlur;
        canvas.SaveLayer(area, paint);
        paint.Reset();
        drawBody(canvas, state);
        canvas.Restore();
        style.Apply(paint);
        paint.BlendMode = SKBlendMode.SrcIn;
        canvas.DrawRect(area, paint);
        canvas.RestoreToCount(save);
        paint.Reset();
    }

    /// <summary>
    /// Rasterizes the content shadow (without the offset) at <paramref name="scale"/> pixels per DIP, at most
    /// <see cref="MaxCacheSize"/> pixels per side; <c>null</c> when it covers no pixels. The image is drawn by
    /// <see cref="DrawCached"/>.
    /// </summary>
    public static SKImage? Rasterize<TState>(SkUiRenderShadow shadow, float scale, SKPaint paint, TState state, Action<SKCanvas, TState> drawBody)
    {
        var area = shadow.CastArea;
        scale = Math.Min(scale, MaxCacheSize / Math.Max(1, Math.Max(area.Width, area.Height)));
        var width = (int)Math.Ceiling(area.Width * scale);
        var height = (int)Math.Ceiling(area.Height * scale);
        if (width <= 0 || height <= 0)
            return null;
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        if (surface is null)
            return null;
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(width / area.Width, height / area.Height);
        canvas.Translate(-area.Left, -area.Top);
        DrawFromContent(canvas, shadow, paint, state, drawBody, applyOffset: false);
        return surface.Snapshot();
    }

    /// <summary>Draws a raster from <see cref="Rasterize"/> where the shadow lands.</summary>
    public static void DrawCached(SKCanvas canvas, SkUiRenderShadow shadow, SKImage image) =>
        canvas.DrawImage(image, shadow.Bounds, new SKSamplingOptions(SKFilterMode.Linear));
}
