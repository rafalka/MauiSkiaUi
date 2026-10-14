using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>The effect settings of <see cref="SkUiShimmer"/> and <see cref="Core.SkUiCoreShimmer"/> (validated by the controls).</summary>
internal readonly record struct SkUiShimmerSettings(
    SkUiShimmerEffect Effect,
    Color? BoneColor,
    Color? HighlightColor,
    double WaveWidth,
    SkUiShimmerWaveDirection WaveDirection,
    SkUiShimmerWaveRegion WaveRegion,
    double Duration,
    SkUiShimmerRepeatMode RepeatMode,
    Easing? Easing,
    double PulseOpacity,
    SkUiShimmerMode Mode)
{
    public const double DefaultWaveWidth = 200;
    public const double DefaultDuration = 1000;
    public const double DefaultPulseOpacity = 0.4;

    public static SkUiShimmerSettings Default { get; } = new(SkUiShimmerEffect.Wave, null, null, DefaultWaveWidth, SkUiShimmerWaveDirection.Default,
        SkUiShimmerWaveRegion.Shimmer, DefaultDuration, SkUiShimmerRepeatMode.Default, null, DefaultPulseOpacity, SkUiShimmerMode.Silhouette);

    /// <summary>Whether a pass reverses: the setting, else the effect's own (a wave restarts, a pulse and a color shift go back).</summary>
    public bool Reverses => RepeatMode switch
    {
        SkUiShimmerRepeatMode.Restart => false,
        SkUiShimmerRepeatMode.Reverse => true,
        _ => Effect is SkUiShimmerEffect.Pulse or SkUiShimmerEffect.Color
    };

    /// <summary>The curve of a pass: the setting, else the effect's own (a wave moves evenly, a pulse and a color shift ease in and out).</summary>
    public Easing EffectiveEasing => Easing ?? (Effect == SkUiShimmerEffect.Wave ? Microsoft.Maui.Easing.Linear : Microsoft.Maui.Easing.SinInOut);

    public static bool IsValidWaveWidth(double value) => double.IsFinite(value) && value > 0;

    public static bool IsValidDuration(double value) => double.IsFinite(value) && value > 0;

    public static bool IsValidPulseOpacity(double value) => value is >= 0 and <= 1;

    public static void ThrowIfInvalidWaveWidth(double value, string name)
    {
        if (!IsValidWaveWidth(value))
            throw new ArgumentOutOfRangeException(name, value, "The wave width must be finite and positive.");
    }

    public static void ThrowIfInvalidDuration(double value, string name)
    {
        if (!IsValidDuration(value))
            throw new ArgumentOutOfRangeException(name, value, "The animation duration must be finite and positive (milliseconds).");
    }

    public static void ThrowIfInvalidPulseOpacity(double value, string name)
    {
        if (!IsValidPulseOpacity(value))
            throw new ArgumentOutOfRangeException(name, value, "The pulse opacity must be between 0 and 1.");
    }
}

/// <summary>
/// A shimmer as its drawables see it: the effect, its resolved colors, its timing from the shared clock and the wave's
/// geometry in the region it crosses. Made on the UI thread by <see cref="SkUiShimmerStyles"/> and immutable (no native
/// objects), so the render thread reads it freely; the shimmer's drawable and its bones' drawables share one.
/// </summary>
internal sealed class SkUiShimmerStyle
{
    private readonly SKMatrix _bandRotation;
    private readonly SKPoint _center;

    public SkUiShimmerStyle(in SkUiShimmerSettings settings, SkUiShimmerColors colors, Size region, bool rightToLeft, double tilt, bool animated)
    {
        Effect = settings.Effect;
        Animated = animated && settings.Effect != SkUiShimmerEffect.None;
        Bone = SkUiShapePainter.ToSkColor(settings.BoneColor ?? colors.Bone);
        Highlight = SkUiShapePainter.ToSkColor(settings.HighlightColor ?? colors.Highlight);
        Tints = settings.BoneColor is not { Alpha: 0 };
        Region = settings.WaveRegion;
        PassSeconds = settings.Duration / 1000;
        Reverse = settings.Reverses;
        Easing = settings.EffectiveEasing;
        PulseOpacity = (float)settings.PulseOpacity;

        // The band crosses the region in one pass: it starts just before one edge and ends just past the other, turned to
        // the direction it moves (x across the band, along the motion).
        var along = SkUiShimmerStyles.Direction(settings.WaveDirection, rightToLeft, tilt);
        var width = (float)settings.WaveWidth;
        var halfAlong = (float)(Math.Abs(region.Width * along.X) + Math.Abs(region.Height * along.Y)) / 2;
        var length = (float)(Math.Abs(region.Width * along.Y) + Math.Abs(region.Height * along.X));
        Band = new SKRect(-width / 2, -length / 2, width / 2, length / 2);
        Along = along;
        Travel = halfAlong + width / 2;
        _center = new SKPoint((float)region.Width / 2, (float)region.Height / 2);
        _bandRotation = SKMatrix.CreateRotationDegrees((float)(Math.Atan2(along.Y, along.X) * 180 / Math.PI));
        Waves = Animated && Effect == SkUiShimmerEffect.Wave && region.Width > 0 && region.Height > 0 && length > 0;
    }

    public SkUiShimmerEffect Effect { get; }

    /// <summary>Whether it moves: an effect other than <see cref="SkUiShimmerEffect.None"/> while the system does not reduce motion.</summary>
    public bool Animated { get; }

    /// <summary>Whether the wave is drawn (an animated wave over a region of some size).</summary>
    public bool Waves { get; }

    /// <summary>The bones' color.</summary>
    public SKColor Bone { get; }

    public SKColor Highlight { get; }

    /// <summary>Whether a silhouette is drawn in <see cref="Bone"/> (a transparent bone color keeps the bones' own colors).</summary>
    public bool Tints { get; }

    public SkUiShimmerWaveRegion Region { get; }

    public double PassSeconds { get; }

    public bool Reverse { get; }

    public Easing Easing { get; }

    public float PulseOpacity { get; }

    /// <summary>The wave band in its own coordinates: centered on the origin, x along the motion.</summary>
    public SKRect Band { get; }

    /// <summary>The motion as a unit vector (y down).</summary>
    public SKPoint Along { get; }

    /// <summary>How far the band's center goes either side of the region's center.</summary>
    public float Travel { get; }

    /// <summary>
    /// The eased position within the current pass at <paramref name="now"/>, 0–1; it follows the shared clock, so every
    /// shimmer with the same timing is in phase. 0 while not animated.
    /// </summary>
    public float Progress(TimeSpan now)
    {
        if (!Animated)
            return 0;
        var passes = now.TotalSeconds / PassSeconds;
        double position;
        if (Reverse)
        {
            var cycle = passes % 2;
            position = cycle <= 1 ? cycle : 2 - cycle;
        }
        else
        {
            position = passes % 1;
        }
        return (float)Easing.Ease(position);
    }

    /// <summary>The band's coordinates to the region's at <paramref name="progress"/>.</summary>
    public SKMatrix BandMatrix(float progress)
    {
        var position = -Travel + 2 * Travel * progress;
        return _bandRotation.PostConcat(SKMatrix.CreateTranslation(_center.X + Along.X * position, _center.Y + Along.Y * position));
    }

    /// <summary>The bones' color at <paramref name="progress"/>: a color shift moves from <paramref name="bone"/> to the highlight.</summary>
    public SKColor ColorAt(SKColor bone, float progress) => Animated && Effect == SkUiShimmerEffect.Color ? Lerp(bone, Highlight, progress) : bone;

    /// <summary>The bones' opacity at <paramref name="progress"/>: a pulse goes from <see cref="PulseOpacity"/> to opaque.</summary>
    public float OpacityAt(float progress) => Animated && Effect == SkUiShimmerEffect.Pulse ? PulseOpacity + (1 - PulseOpacity) * progress : 1;

    /// <summary>The wave band's shader from the look; the caller owns it.</summary>
    public SKShader CreateWaveShader() => SkUiLook.Current.CreateShimmerWaveShader(new SkUiShimmerWavePaint(Band, Highlight));

    /// <summary>Draws the band in <paramref name="paint"/> (shader set) over what is drawn already, inside the canvas's clips.</summary>
    public void DrawBand(SKCanvas canvas, in SkUiRenderDrawContext context, float progress, SKPaint paint)
    {
        var region = Region == SkUiShimmerWaveRegion.Surface ? context.Surface : context.Space;
        var band = region.PreConcat(BandMatrix(progress));
        var save = canvas.Save();
        canvas.SetMatrix(in band);
        canvas.DrawRect(Band, paint);
        canvas.RestoreToCount(save);
    }

    /// <summary>The band's turn: from its own coordinates (x along the motion) to the region's.</summary>
    public SKMatrix BandRotation => _bandRotation;

    /// <summary>
    /// The band's coordinates to the canvas's current ones at <paramref name="progress"/>; <c>false</c> when the canvas
    /// cannot be inverted (nothing to draw).
    /// </summary>
    public bool TryGetBandToLocal(SKCanvas canvas, in SkUiRenderDrawContext context, float progress, out SKMatrix bandToLocal)
    {
        var region = Region == SkUiShimmerWaveRegion.Surface ? context.Surface : context.Space;
        bandToLocal = default;
        if (!canvas.TotalMatrix.TryInvert(out var toLocal))
            return false;
        bandToLocal = toLocal.PreConcat(region).PreConcat(BandMatrix(progress));
        return true;
    }

    /// <summary>Whether the band (placed by <paramref name="bandToLocal"/>) misses <paramref name="local"/>.</summary>
    public bool BandMisses(in SKMatrix bandToLocal, SKRect local)
    {
        if (!bandToLocal.TryInvert(out var toBand))
            return true;
        var inBand = toBand.MapRect(local);
        return inBand.Right <= Band.Left || inBand.Left >= Band.Right;
    }

    private static SKColor Lerp(SKColor from, SKColor to, float amount) => new(
        (byte)(from.Red + (to.Red - from.Red) * amount + 0.5f), (byte)(from.Green + (to.Green - from.Green) * amount + 0.5f),
        (byte)(from.Blue + (to.Blue - from.Blue) * amount + 0.5f), (byte)(from.Alpha + (to.Alpha - from.Alpha) * amount + 0.5f));
}

/// <summary>
/// A shimmer's current <see cref="SkUiShimmerStyle"/>, made again only when something it depends on changed (settings, size,
/// direction, the look's colors, reduced motion), so an unchanged layout commits nothing new. Shared by both shimmer layers.
/// </summary>
internal sealed class SkUiShimmerStyles(ISkUiRenderable owner)
{
    private Key? _key;

    private readonly record struct Key(SkUiShimmerSettings Settings, SkUiShimmerColors Colors, Size Region, bool RightToLeft, double Tilt, bool Animated);

    /// <summary>The style last made (<c>null</c> before the first <see cref="Update"/>).</summary>
    public SkUiShimmerStyle? Current { get; private set; }

    /// <summary>The style for a shimmer of <paramref name="size"/>; returns whether it is a new one.</summary>
    public bool Update(in SkUiShimmerSettings settings, Size size, bool rightToLeft)
    {
        var look = SkUiLook.Current;
        var region = settings.WaveRegion == SkUiShimmerWaveRegion.Surface ? SurfaceSize() : size;
        var key = new Key(settings, look.GetShimmerColors(), region, rightToLeft, look.ShimmerWaveTilt, !SkUiMotion.IsMotionReduced);
        if (Current is not null && key == _key)
            return false;
        _key = key;
        Current = new SkUiShimmerStyle(settings, key.Colors, region, rightToLeft, key.Tilt, key.Animated);
        return true;
    }

    /// <summary>The size of the drawn surface the shimmer is on (its root), for the surface-wide wave.</summary>
    private Size SurfaceSize()
    {
        var node = owner;
        while (node.RenderParent is { } parent)
            node = parent;
        return node switch
        {
            SkUiView view => new Size(view.Width, view.Height),
            Core.SkUiCoreNode core => core.Frame.Size,
            _ => Size.Zero
        };
    }

    /// <summary>The wave's motion as a unit vector (y down); the default reads in the layout's direction, tilted so the top leads.</summary>
    internal static SKPoint Direction(SkUiShimmerWaveDirection direction, bool rightToLeft, double tilt)
    {
        switch (direction)
        {
            case SkUiShimmerWaveDirection.LeftToRight: return new SKPoint(1, 0);
            case SkUiShimmerWaveDirection.RightToLeft: return new SKPoint(-1, 0);
            case SkUiShimmerWaveDirection.TopToBottom: return new SKPoint(0, 1);
            case SkUiShimmerWaveDirection.BottomToTop: return new SKPoint(0, -1);
            default:
                var angle = Math.Clamp(tilt, -80, 80) * Math.PI / 180;
                // A motion turned down by the tilt leans the band's top forward.
                return new SKPoint((float)(rightToLeft ? -Math.Cos(angle) : Math.Cos(angle)), (float)Math.Sin(angle));
        }
    }

    /// <summary>
    /// Has the bones of <paramref name="shimmer"/>'s skeleton record their render properties again (the style or the mode
    /// changed); nested shimmers keep theirs.
    /// </summary>
    public static void InvalidateBones(ISkUiRenderable shimmer)
    {
        var children = new List<ISkUiRenderable>();
        shimmer.GetRenderChildren(children);
        for (var index = 0; index < children.Count; index++)
        {
            var child = children[index];
            if (child is ISkUiShimmerBone bone)
                bone.OnShimmerChanged();
            else if (child is not ISkUiShimmer)
                child.GetRenderChildren(children);
        }
    }

    /// <summary>The nearest shimmer above <paramref name="node"/> in the drawn tree, of either layer.</summary>
    public static ISkUiShimmer? Find(ISkUiRenderable node)
    {
        for (var parent = node.RenderParent; parent is not null; parent = parent.RenderParent)
            if (parent is ISkUiShimmer shimmer)
                return shimmer;
        return null;
    }
}

/// <summary>A shimmer of either layer, as its bones see it.</summary>
internal interface ISkUiShimmer : ISkUiRenderable
{
    /// <summary>
    /// The style its bones animate with (<see cref="SkUiShimmerMode.Bones"/>), made again if the look's colors changed;
    /// <c>null</c> while the shimmer draws a silhouette (the bones stand still and are tinted with everything else).
    /// </summary>
    SkUiShimmerStyle? GetBoneStyle();
}

/// <summary>A bone of either layer.</summary>
internal interface ISkUiShimmerBone
{
    /// <summary>Its shimmer's style or mode changed: it records its render properties again.</summary>
    void OnShimmerChanged();
}

/// <summary>
/// What a shimmer draws over its skeleton in <see cref="SkUiShimmerMode.Silhouette"/>, inside its children layer: everything
/// the bones drew in the bone color (source-in), then the wave band (source-atop: over the bones only) or the color shift,
/// and the pulse (destination-in: the group's alpha). One layer per frame, whatever the effect.
/// </summary>
internal sealed class SkUiShimmerSilhouette(SkUiShimmerStyle style) : SkUiRenderDrawable
{
    private readonly SKShader? _shader = style.Waves ? style.CreateWaveShader() : null;

    public SkUiShimmerStyle Style => style;

    public override bool IsAnimated => style.Animated;

    public override void Draw(SKCanvas canvas, in SkUiRenderDrawContext context)
    {
        var paint = context.Paint;
        var progress = style.Progress(context.Now);
        if (style.Tints)
        {
            paint.Color = style.ColorAt(style.Bone, progress);
            paint.BlendMode = SKBlendMode.SrcIn;
            canvas.DrawRect(context.Bounds, paint);
        }
        else if (style.Animated && style.Effect == SkUiShimmerEffect.Color)
        {
            // The bones' own colors, shifting to the highlight.
            paint.Color = style.Highlight.WithAlpha((byte)(style.Highlight.Alpha * progress));
            paint.BlendMode = SKBlendMode.SrcATop;
            canvas.DrawRect(context.Bounds, paint);
        }
        if (_shader is not null)
        {
            paint.Color = SKColors.White;
            paint.BlendMode = SKBlendMode.SrcATop;
            paint.Shader = _shader;
            style.DrawBand(canvas, context, progress, paint);
            paint.Shader = null;
        }
        var opacity = style.OpacityAt(progress);
        if (opacity < 1)
        {
            paint.Color = SKColors.Black.WithAlpha((byte)(255 * opacity));
            paint.BlendMode = SKBlendMode.DstIn;
            canvas.DrawRect(context.Bounds, paint);
        }
    }

    public override void Dispose() => _shader?.Dispose();
}

/// <summary>
/// A bone that draws itself (<see cref="SkUiShimmerBone"/> / <see cref="Core.SkUiCoreShimmerBone"/>): its shape in its color,
/// shifted or pulsed, and the part of the wave band that crosses it, positioned in its shimmer's coordinates so the band
/// moves across every bone as one. No layer: one fill per bone and frame. While the band crosses a bone that is only moved
/// relative to its shimmer (the usual case) and has one corner radius, the fill is the bone color with the band over it in
/// one shader, moved by translating the canvas against the shape; otherwise the band is drawn clipped to the shape.
/// </summary>
internal sealed class SkUiShimmerBoneDrawable : SkUiRenderDrawable
{
    /// <summary>How close the bone's axes must be to the band's for the one-draw path.</summary>
    private const float AxisTolerance = 1e-3f;

    private readonly SkUiShimmerStyle? _style;
    private readonly SKColor _color;
    private readonly SKRoundRect? _shape;
    private readonly float _radius = -1; // one radius for every corner; -1: several
    private readonly SKShader? _band;    // the look's band, in the band's coordinates
    private readonly SKShader? _turned;  // the band turned to its angle
    private readonly SKShader? _filled;  // the bone color with the turned band over it

    /// <param name="style">The shimmer's style; <c>null</c>: the bone stands still.</param>
    /// <param name="color">The bone's color.</param>
    /// <param name="size">The bone's size.</param>
    /// <param name="radii">Its corner radii.</param>
    public SkUiShimmerBoneDrawable(SkUiShimmerStyle? style, SKColor color, SKSize size, CornerRadius radii)
    {
        _style = style;
        _color = color;
        if (SkUiCornerRadii.HasAny(radii))
        {
            _shape = new SKRoundRect();
            _shape.SetRectRadii(SKRect.Create(size), [
                Radius(radii.TopLeft), Radius(radii.TopRight), Radius(radii.BottomRight), Radius(radii.BottomLeft)]);
            // Uniform corners are drawn as a rectangle with radii (the shape's radii, already scaled down to fit).
            if (_shape.Type is SKRoundRectType.Simple or SKRoundRectType.Oval)
                _radius = _shape.Radii[0].X == _shape.Radii[0].Y ? _shape.Radii[0].X : -1;
        }
        else
        {
            _radius = 0;
        }
        if (style is not { Waves: true })
            return;
        _band = style.CreateWaveShader();
        if (_radius < 0)
            return;
        _turned = _band.WithLocalMatrix(style.BandRotation);
        using var fill = SKShader.CreateColor(color);
        _filled = SKShader.CreateCompose(fill, _turned, SKBlendMode.SrcOver);
    }

    private static SKPoint Radius(double radius) => new((float)radius, (float)radius);

    public override bool IsAnimated => _style is { Animated: true };

    public override void Draw(SKCanvas canvas, in SkUiRenderDrawContext context)
    {
        var paint = context.Paint;
        var bounds = context.Bounds;
        var progress = _style?.Progress(context.Now) ?? 0;
        var color = _style?.ColorAt(_color, progress) ?? _color;
        var opacity = _style?.OpacityAt(progress) ?? 1;
        paint.Color = opacity < 1 ? color.WithAlpha((byte)(color.Alpha * opacity)) : color;
        if (_band is null || !_style!.TryGetBandToLocal(canvas, context, progress, out var bandToLocal) || _style.BandMisses(bandToLocal, bounds))
        {
            DrawShape(canvas, bounds, paint);
            return;
        }
        if (_filled is not null && IsTurnedLike(bandToLocal, _style.BandRotation))
        {
            // The band only moved against the bone: move the canvas by the band's offset and the shape back, so the shader
            // (the bone color under the turned band) lands where the band is. One draw, no clip.
            paint.Color = SKColors.White;
            paint.Shader = _filled;
            var save = canvas.Save();
            canvas.Translate(bandToLocal.TransX, bandToLocal.TransY);
            var shifted = bounds;
            shifted.Offset(-bandToLocal.TransX, -bandToLocal.TransY);
            canvas.DrawRoundRect(shifted, _radius, _radius, paint);
            canvas.RestoreToCount(save);
            paint.Shader = null;
            return;
        }
        DrawShape(canvas, bounds, paint);
        var clip = canvas.Save();
        if (_shape is not null)
            canvas.ClipRoundRect(_shape, antialias: true);
        else
            canvas.ClipRect(bounds, antialias: true);
        canvas.Concat(in bandToLocal);
        paint.Color = SKColors.White;
        paint.Shader = _band;
        canvas.DrawRect(_style.Band, paint);
        paint.Shader = null;
        canvas.RestoreToCount(clip);
    }

    private void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint)
    {
        if (_shape is null)
            canvas.DrawRect(bounds, paint);
        else if (_radius >= 0)
            canvas.DrawRoundRect(bounds, _radius, _radius, paint);
        else
            canvas.DrawRoundRect(_shape, paint);
    }

    /// <summary>Whether <paramref name="matrix"/> turns and moves as <paramref name="turn"/> does, with no scale or perspective.</summary>
    private static bool IsTurnedLike(in SKMatrix matrix, in SKMatrix turn) =>
        Math.Abs(matrix.ScaleX - turn.ScaleX) < AxisTolerance && Math.Abs(matrix.SkewX - turn.SkewX) < AxisTolerance
        && Math.Abs(matrix.SkewY - turn.SkewY) < AxisTolerance && Math.Abs(matrix.ScaleY - turn.ScaleY) < AxisTolerance
        && matrix.Persp0 == 0 && matrix.Persp1 == 0;

    public override void Dispose()
    {
        _shape?.Dispose();
        _filled?.Dispose();
        _turned?.Dispose();
        _band?.Dispose();
    }
}

/// <summary>
/// A node's current drawable, kept while its key stays the same (committing the same frame twice commits the same object).
/// Committed to the compositor, which disposes it once a commit replaces it or the node goes: never disposed here.
/// </summary>
internal struct SkUiDrawableSlot<TKey> where TKey : notnull
{
    private SkUiRenderDrawable? _drawable;
    private TKey? _key;

    public SkUiRenderDrawable Get<TState>(TKey key, TState state, Func<TState, SkUiRenderDrawable> create)
    {
        if (_drawable is not null && EqualityComparer<TKey>.Default.Equals(key, _key!))
            return _drawable;
        _key = key;
        return _drawable = create(state);
    }

    /// <summary>Forgets the drawable (the node was reset; the compositor disposes it with the render node).</summary>
    public void Forget()
    {
        _drawable = null;
        _key = default;
    }
}

/// <summary>What a bone's drawable depends on.</summary>
internal readonly record struct SkUiShimmerBoneKey(SkUiShimmerStyle? Style, SKColor Color, float Width, float Height, CornerRadius Radii)
{
    /// <summary>
    /// The drawable of <paramref name="bone"/> (<paramref name="color"/>: its own, else its shimmer's or the look's), from
    /// <paramref name="slot"/> while nothing it depends on changed; <c>null</c> while it has no size.
    /// </summary>
    public static SkUiRenderDrawable? GetDrawable(ref SkUiDrawableSlot<SkUiShimmerBoneKey> slot, ISkUiRenderable bone, Color? color,
        CornerRadius radii, float width, float height)
    {
        if (width <= 0 || height <= 0)
            return null;
        var style = SkUiShimmerStyles.Find(bone)?.GetBoneStyle();
        var fill = color is not null ? SkUiShapePainter.ToSkColor(color)
            : style?.Bone ?? SkUiShapePainter.ToSkColor(SkUiLook.Current.GetShimmerColors().Bone);
        var key = new SkUiShimmerBoneKey(style, fill, width, height, radii);
        return slot.Get(key, key, static key => new SkUiShimmerBoneDrawable(key.Style, key.Color, new SKSize(key.Width, key.Height), key.Radii));
    }
}
