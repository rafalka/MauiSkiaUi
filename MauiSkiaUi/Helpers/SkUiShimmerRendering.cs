using MauiSkiaUi.Core;
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
    double PulseOpacity)
{
    public const double DefaultWaveWidth = 200;
    public const double DefaultDuration = 1000;
    public const double DefaultPulseOpacity = 0.4;

    public static SkUiShimmerSettings Default { get; } = new(SkUiShimmerEffect.Wave, null, null, DefaultWaveWidth, SkUiShimmerWaveDirection.Default,
        SkUiShimmerWaveRegion.Shimmer, DefaultDuration, SkUiShimmerRepeatMode.Default, null, DefaultPulseOpacity);

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
/// The parts that animate a shimmer's skeleton, built from the engine's generic primitives: the shimmer draws its children
/// into one layer (<see cref="SkUiRenderProps.ChildrenLayer"/>), the bones first, then these internal nodes over them: a tint
/// in the bone color (drawn source-in), a color-shift rectangle (composited source-atop, its opacity on a render timeline)
/// and the wave band (drawn source-atop, so it needs no layer of its own; its translation on a render timeline, anchored to
/// the surface for the surface-wide region). The
/// pulse is a timeline of the shimmer's children opacity (<see cref="Pulse"/>). Nothing is recorded per frame. Shared by
/// both shimmer layers.
/// </summary>
internal sealed class SkUiShimmerParts(ISkUiRenderable owner, Action<SkUiRenderDirty> invalidate)
{
    private SkUiShimmerPart? _tint;
    private SkUiShimmerPart? _shift;
    private SkUiShimmerPart? _wave;
    private Key? _key;

    private readonly record struct Key(SkUiShimmerSettings Settings, bool Shown, Size Size, Size Surface, bool RightToLeft, double Tilt, bool Animated);

    /// <summary>The timeline of the shimmer's children opacity (the pulse), or <c>null</c>.</summary>
    public SkUiRenderTimeline? Pulse { get; private set; }

    /// <summary>Appends the parts (after the skeleton) to the shimmer's render children; hidden parts stay, not drawn.</summary>
    public void AddTo(List<ISkUiRenderable> children)
    {
        if (_tint is not null) children.Add(_tint);
        if (_shift is not null) children.Add(_shift);
        if (_wave is not null) children.Add(_wave);
    }

    /// <summary>
    /// Lays the parts out for a shimmer of <paramref name="size"/>; with <paramref name="shown"/> <c>false</c> (the content
    /// shows) they are hidden. Timelines are made again only when what they depend on changed, so an unchanged layout commits
    /// nothing new.
    /// </summary>
    public void Update(in SkUiShimmerSettings settings, bool shown, Size size, bool rightToLeft)
    {
        var look = SkUiLook.Current;
        var animated = !SkUiMotion.IsMotionReduced && settings.Effect != SkUiShimmerEffect.None;
        var region = settings.WaveRegion == SkUiShimmerWaveRegion.Surface ? SurfaceSize() : size;
        var key = new Key(settings, shown, size, region, rightToLeft, look.ShimmerWaveTilt, animated);
        if (key == _key)
            return;
        _key = key;
        var bounds = new Rect(0, 0, size.Width, size.Height);
        var pass = settings.Duration / 1000;
        var reverse = settings.Reverses;
        var easing = settings.EffectiveEasing;

        // The bones in one color (their alpha kept); a transparent bone color keeps their own colors.
        var tinted = shown && settings.BoneColor is not { Alpha: 0 };
        Part(ref _tint, SkUiShimmerPartKind.Tint, tinted).Configure(settings.BoneColor, SKBlendMode.SrcOver, null, false, bounds, 0);

        Pulse = shown && animated && settings.Effect == SkUiShimmerEffect.Pulse
            ? new SkUiRenderTimeline([SkUiRenderProperty.ChildrenOpacity], [(float)settings.PulseOpacity], [1], pass, reverse, easing)
            : null;

        var shifts = shown && animated && settings.Effect == SkUiShimmerEffect.Color;
        if (shifts || _shift is not null)
            Part(ref _shift, SkUiShimmerPartKind.Shift, shifts).Configure(settings.HighlightColor, SKBlendMode.SrcATop,
                shifts ? new SkUiRenderTimeline([SkUiRenderProperty.Opacity], [0], [1], pass, reverse, easing) : null, false, bounds, 0);

        var waves = shown && animated && settings.Effect == SkUiShimmerEffect.Wave && size.Width > 0 && size.Height > 0;
        if (waves || _wave is not null)
        {
            var wave = Part(ref _wave, SkUiShimmerPartKind.Wave, waves);
            if (!waves)
            {
                wave.Configure(settings.HighlightColor, SKBlendMode.SrcOver, null, false, wave.Frame, 0);
                return;
            }
            // The band crosses the region (the shimmer, or the surface it is on) in one pass: it starts just before one edge
            // and ends just past the other, turned to the direction it moves.
            var along = Direction(settings.WaveDirection, rightToLeft, key.Tilt);
            var across = new SKPoint(-along.Y, along.X);
            var width = settings.WaveWidth;
            var halfAlong = (Math.Abs(region.Width * along.X) + Math.Abs(region.Height * along.Y)) / 2;
            var length = Math.Abs(region.Width * across.X) + Math.Abs(region.Height * across.Y);
            var band = new Rect(region.Width / 2 - width / 2, region.Height / 2 - length / 2, width, length);
            var travel = (float)(halfAlong + width / 2);
            var timeline = new SkUiRenderTimeline([SkUiRenderProperty.TranslationX, SkUiRenderProperty.TranslationY],
                [-along.X * travel, -along.Y * travel], [along.X * travel, along.Y * travel], pass, reverse, easing);
            wave.Configure(settings.HighlightColor, SKBlendMode.SrcOver, timeline, settings.WaveRegion == SkUiShimmerWaveRegion.Surface, band,
                Math.Atan2(along.Y, along.X) * 180 / Math.PI);
        }
    }

    private SkUiShimmerPart Part(ref SkUiShimmerPart? part, SkUiShimmerPartKind kind, bool visible)
    {
        if (part is null)
        {
            part = new SkUiShimmerPart(kind);
            switch (owner)
            {
                case SkUiCoreNode node: part.AttachTo(node); break;
                case SkUiView view: part.HostOwner = view; break;
            }
            invalidate(SkUiRenderDirty.Children);
        }
        part.SetIsVisible(visible);
        return part;
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
            SkUiCoreNode core => core.Frame.Size,
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
}

internal enum SkUiShimmerPartKind
{
    Tint,
    Shift,
    Wave
}

/// <summary>
/// One internal node of a shimmer (<see cref="SkUiShimmerParts"/>): a rectangle or the look's wave band, recorded once, with
/// the generic composite-time properties that make it an effect (a blend mode, a timeline, surface anchoring). Its colors
/// come from the look at recording time while the shimmer sets none, so look and color scheme changes reach it.
/// </summary>
internal sealed class SkUiShimmerPart : SkUiCoreNode
{
    private readonly SkUiShimmerPartKind _kind;
    private Color? _color;
    private SKBlendMode _blendMode = SKBlendMode.SrcOver;
    private SkUiRenderTimeline? _timeline;
    private bool _surfaceAnchored;

    public SkUiShimmerPart(SkUiShimmerPartKind kind)
    {
        _kind = kind;
        InitClipToBounds(false);
    }

    /// <summary>
    /// Placed by the shimmer in physical coordinates (the wave's direction already follows the layout's direction): pinned
    /// nodes are not mirrored in right-to-left layouts.
    /// </summary>
    internal override bool IsPinned => true;

    /// <summary>Sets what the part draws and how it is composited, then places it.</summary>
    public void Configure(Color? color, SKBlendMode blendMode, SkUiRenderTimeline? timeline, bool surfaceAnchored, Rect frame, double rotation)
    {
        if (!Equals(_color, color))
        {
            _color = color;
            InvalidatePaint();
        }
        if (_blendMode != blendMode || !ReferenceEquals(_timeline, timeline) || _surfaceAnchored != surfaceAnchored)
        {
            (_blendMode, _timeline, _surfaceAnchored) = (blendMode, timeline, surfaceAnchored);
            InvalidateRender(SkUiRenderDirty.Props);
        }
        SetRotation(rotation);
        Arrange(frame);
    }

    /// <inheritdoc />
    protected override void OnPaintBackground(SKCanvas canvas) { }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var colors = SkUiLook.Current.GetShimmerColors();
        var bounds = new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height);
        switch (_kind)
        {
            case SkUiShimmerPartKind.Tint:
            {
                // Source-in: what the bones drew (alpha, antialiased edges) in one color.
                using var paint = new SKPaint { Color = SkUiShapePainter.ToSkColor(_color ?? colors.Bone), BlendMode = SKBlendMode.SrcIn };
                canvas.DrawRect(bounds, paint);
                break;
            }
            case SkUiShimmerPartKind.Shift:
            {
                using var paint = new SKPaint { Color = SkUiShapePainter.ToSkColor(_color ?? colors.Highlight) };
                canvas.DrawRect(bounds, paint);
                break;
            }
            default:
            {
                // Source-atop in the recording itself: over the bones only, with no layer of its own per frame.
                using var shader = SkUiLook.Current.CreateShimmerWaveShader(new SkUiShimmerWavePaint(bounds, SkUiShapePainter.ToSkColor(_color ?? colors.Highlight)));
                using var paint = new SKPaint { Shader = shader, BlendMode = SKBlendMode.SrcATop };
                canvas.DrawRect(bounds, paint);
                break;
            }
        }
    }

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        base.OnGetRenderProps(ref props);
        props.BlendMode = _blendMode;
        props.Timeline = _timeline;
        props.SurfaceAnchored = _surfaceAnchored;
    }
}
