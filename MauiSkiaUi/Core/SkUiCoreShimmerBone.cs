using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core bone of a loading skeleton (the twin of <see cref="SkUiShimmerBone"/>): a rectangle with optional rounded corners
/// that animates by itself inside an <see cref="SkUiCoreShimmer"/> (or an <see cref="SkUiShimmer"/>) with
/// <see cref="SkUiShimmerMode.Bones"/>, while the nodes around it draw as they are. Measures 40 × 40 DIPs unless sized.
/// </summary>
public class SkUiCoreShimmerBone : SkUiCoreNode, ISkUiShimmerBone
{
    private Color? _color;
    private CornerRadius _cornerRadius;
    private SkUiDrawableSlot<SkUiShimmerBoneKey> _drawable;

    /// <summary>The bone's color (<c>null</c>, the default: the shimmer's bone color, else the look's).</summary>
    public Color? Color { get => _color; set => SetColor(value); }

    /// <summary>Per-corner radii in DIPs. Larger radii than the bone allows are scaled down.</summary>
    public CornerRadius CornerRadius { get => _cornerRadius; set => SetCornerRadius(value); }

    /// <summary>Sets <see cref="Color"/>.</summary>
    public SkUiCoreShimmerBone SetColor(Color? value)
    {
        if (SetProperty(ref _color, value, nameof(Color))) InvalidateRender(SkUiRenderDirty.Props);
        return this;
    }

    /// <summary>Sets the corner radii (a number converts to the same radius on every corner).</summary>
    public SkUiCoreShimmerBone SetCornerRadius(CornerRadius value)
    {
        SkUiCornerRadii.Validate(value, nameof(value));
        if (SetProperty(ref _cornerRadius, value, nameof(CornerRadius))) InvalidateRender(SkUiRenderDirty.Props);
        return this;
    }

    void ISkUiShimmerBone.OnShimmerChanged() => InvalidateRender(SkUiRenderDirty.Props);

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(40, 40);

    /// <summary>The bone records nothing: the compositor draws it (its background too is not drawn).</summary>
    protected override void OnPaintBackground(SKCanvas canvas) { }

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        base.OnGetRenderProps(ref props);
        props.Drawable = SkUiShimmerBoneKey.GetDrawable(ref _drawable, this, _color, _cornerRadius, props.Width, props.Height);
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        _drawable.Forget();
    }
}
