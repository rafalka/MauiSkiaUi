using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A bone of a loading skeleton: a rectangle with optional rounded corners (a circle when the radius is half its size) that
/// stands for content still loading. Inside an <see cref="SkUiShimmer"/> with <see cref="SkUiShimmerMode.Bones"/> it animates
/// by itself with the shimmer's effect while everything around it (cards, dividers, buttons) draws as it is; in a
/// silhouette it is one more shape, and outside a shimmer it stands still. Measures 40 × 40 DIPs unless sized, as MAUI's
/// <c>BoxView</c>.
/// </summary>
/// <remarks>
/// The bone is drawn by the compositor on the render thread from the shared clock: nothing is recorded per frame, and no
/// layer is drawn. A wave crosses every bone of a shimmer as one band, in the shimmer's coordinates (or the surface's).
/// </remarks>
public class SkUiShimmerBone : SkUiView, ISkUiShimmerBone
{
    private Color? _color;
    private CornerRadius _cornerRadius;
    private SkUiBoneSlot _drawable;

    /// <summary>Bindable <see cref="Color"/>.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiShimmerBone), null,
        propertyChanged: (view, _, value) => ((SkUiShimmerBone)view).OnColorChanged((Color?)value));

    /// <summary>Bindable <see cref="CornerRadius"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(SkUiShimmerBone), default(CornerRadius),
        validateValue: SkUiValidate.CornerRadii,
        propertyChanged: (view, _, value) => ((SkUiShimmerBone)view).OnCornerRadiusChanged((CornerRadius)value));

    /// <summary>The bone's color (<c>null</c>, the default: the shimmer's <see cref="SkUiShimmer.BoneColor"/>, else the look's).</summary>
    public Color? Color { get => (Color?)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <summary>Per-corner radii (<c>"8"</c> or <c>"8,8,0,0"</c> in XAML). Larger radii than the bone allows are scaled down.</summary>
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Sets <see cref="Color"/> (same as the property setter).</summary>
    public SkUiShimmerBone SetColor(Color? value)
    {
        Color = value;
        return this;
    }

    /// <summary>Sets <see cref="CornerRadius"/> (same as the property setter).</summary>
    public SkUiShimmerBone SetCornerRadius(CornerRadius value)
    {
        SkUiCornerRadii.Validate(value, nameof(value));
        CornerRadius = value;
        return this;
    }

    private void OnColorChanged(Color? value)
    {
        _color = value;
        InvalidateRender(SkUiRenderDirty.Props);
    }

    private void OnCornerRadiusChanged(CornerRadius value)
    {
        _cornerRadius = value;
        InvalidateRender(SkUiRenderDirty.Props);
    }

    void ISkUiShimmerBone.OnShimmerChanged() => InvalidateRender(SkUiRenderDirty.Props);

    /// <summary>In a skeleton made from content a bone draws itself, as it is.</summary>
    internal override SkUiSkeleton DefaultSkeleton => SkUiSkeleton.Keep;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(40, 40);

    /// <summary>The bone records nothing: the compositor draws it (its background too is not drawn).</summary>
    protected override void OnPaintBackground(SKCanvas canvas) { }

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        base.OnGetRenderProps(ref props);
        props.Drawable = _drawable.GetSingle(this, _color, _cornerRadius, props.Width, props.Height);
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        _drawable.Forget();
    }
}
