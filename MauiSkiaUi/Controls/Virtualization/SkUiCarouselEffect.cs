namespace MauiSkiaUi;

/// <summary>
/// How a <see cref="SkUiCarouselView"/> transforms its items as they scroll (<see cref="SkUiCarouselView.ItemEffect"/>): from
/// an item's position relative to the carousel's current place, a placement, scale, rotation, tilt in perspective, opacity
/// and drawing order. <see cref="SkUiCoverFlowEffect"/> and <see cref="SkUiScaleEffect"/> are built in; derive for others.
/// </summary>
/// <remarks>
/// <para>
/// <b>Evaluated on the render thread, every frame.</b> The compositor places each item from the scroller's offset as it is
/// drawn, so effects follow flings and snaps without UI-thread work and nothing is recorded again while the carousel
/// scrolls; the UI thread evaluates the same transform for hit-testing. An override of
/// <see cref="GetItemTransform"/> must therefore be fast, allocation-free and safe to call from any thread: compute from its
/// arguments and the effect's own settings only (no bindable properties, views or other UI state). Settings changed later
/// apply from the next frame; call <see cref="OnChanged"/> from their setters so the carousel draws one.
/// </para>
/// <para>
/// Positions and translations are physical: a positive position is an item after the current place to the right (below in
/// a vertical carousel), also in right-to-left layouts, so effects look the same either way.
/// </para>
/// </remarks>
public abstract class SkUiCarouselEffect
{
    // Weak: an effect shared through app resources keeps no carousel alive.
    private readonly SkUiWeakEvent _changed = new();

    /// <summary>Raised when a setting changed (<see cref="OnChanged"/>): carousels using the effect draw a frame with it.</summary>
    internal event EventHandler? Changed { add => _changed.Add(value); remove => _changed.Remove(value); }

    /// <summary>
    /// The transform of an item at <paramref name="position"/>: 0 at the carousel's current place (where
    /// <see cref="SkUiCarouselView.SnapPointsAlignment"/> lines items up), 1 one item after it (to the right, or below), -1 one
    /// item before; fractional while scrolling. Translations add to where the layout puts the item.
    /// </summary>
    /// <param name="position">The item's distance from the current place, in items (item length plus spacing).</param>
    /// <param name="metrics">The item's size, the spacing and the viewport.</param>
    public abstract SkUiCarouselItemTransform GetItemTransform(double position, in SkUiCarouselItemMetrics metrics);

    /// <summary>
    /// How far from the current place, in items, an item can still show with this effect (effects that pull items closer
    /// show more of them than the layout does); 0 (default): only items the layout puts in the viewport. The carousel
    /// creates the items up to this distance.
    /// </summary>
    public virtual double GetVisibleRange(in SkUiCarouselItemMetrics metrics) => 0;

    /// <summary>
    /// How far the eye is from the items for <see cref="SkUiCarouselItemTransform.Tilt"/>, in DIPs: smaller distances show a
    /// stronger perspective. Default: three times the larger side of an item.
    /// </summary>
    public virtual double GetPerspective(in SkUiCarouselItemMetrics metrics) => 3 * Math.Max(metrics.ItemLength, metrics.ItemThickness);

    /// <summary>A setting changed: carousels using the effect draw it from the next frame.</summary>
    protected void OnChanged() => _changed.Raise(this, EventArgs.Empty);

    /// <summary>Sets <paramref name="field"/> and raises <see cref="OnChanged"/> when <paramref name="value"/> differs.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not finite or outside [<paramref name="minimum"/>, <paramref name="maximum"/>].</exception>
    private protected void SetSetting(ref double field, double value, double minimum, double maximum, string name)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(name, value, $"The value must be between {minimum} and {maximum}.");
        // Exact: an unchanged setting needs no frame.
        if (field == value)
            return;
        field = value;
        OnChanged();
    }
}

/// <summary>What a <see cref="SkUiCarouselEffect"/> knows about the items it transforms, along and across the carousel's axis.</summary>
/// <param name="ItemLength">An item's size along the scroll axis, in DIPs.</param>
/// <param name="ItemThickness">An item's size across the scroll axis, in DIPs.</param>
/// <param name="ItemSpacing">The gap between items along the axis, in DIPs.</param>
/// <param name="ViewportLength">The carousel's viewport along the axis, in DIPs.</param>
/// <param name="IsHorizontal">Whether the carousel scrolls horizontally.</param>
public readonly record struct SkUiCarouselItemMetrics(double ItemLength, double ItemThickness, double ItemSpacing, double ViewportLength, bool IsHorizontal)
{
    /// <summary>The distance from one item to the next: <see cref="ItemLength"/> plus <see cref="ItemSpacing"/>.</summary>
    public double Stride => ItemLength + ItemSpacing;
}

/// <summary>
/// An item's transform from a <see cref="SkUiCarouselEffect"/>, about the item's center. <see cref="Identity"/> leaves the item
/// as laid out.
/// </summary>
public readonly record struct SkUiCarouselItemTransform
{
    /// <summary>The item as laid out: no translation, scale 1, opaque, depth 0.</summary>
    public static SkUiCarouselItemTransform Identity => new();

    /// <summary>Creates <see cref="Identity"/>.</summary>
    public SkUiCarouselItemTransform() { }

    /// <summary>Moves the item along the scroll axis, in DIPs (positive: right, or down).</summary>
    public double Translation { get; init; }

    /// <summary>Moves the item across the scroll axis, in DIPs (positive: down in a horizontal carousel, right in a vertical one).</summary>
    public double CrossTranslation { get; init; }

    /// <summary>Scales the item about its center (1: as laid out).</summary>
    public double Scale { get; init; } = 1;

    /// <summary>Turns the item in its plane about its center, in degrees (clockwise).</summary>
    public double Rotation { get; init; }

    /// <summary>
    /// Turns the item in perspective about its center line across the axis, in degrees: in a horizontal carousel about the
    /// vertical line, positive turning its right edge away; in a vertical one about the horizontal line, its bottom edge
    /// away. Kept within ±80°.
    /// </summary>
    public double Tilt { get; init; }

    /// <summary>The item's opacity, 0–1 (multiplies its own; 0 does not draw it, but it is still hit-tested).</summary>
    public double Opacity { get; init; } = 1;

    /// <summary>Drawing order: items with a larger value are drawn over the others (equal values keep the layout order).</summary>
    public double ZIndex { get; init; }
}

/// <summary>
/// Cover flow: the current item faces the viewer, the others turn towards it in perspective, smaller, and stack closely on
/// either side, the nearer ones over the farther ones (the item spacing and the layout's gaps do not show). Set
/// <see cref="SkUiCarouselView.ItemExtent"/> to a part of the width (about half) so the side items have room.
/// </summary>
public sealed class SkUiCoverFlowEffect : SkUiCarouselEffect
{
    private double _rotationAngle = 50;
    private double _sideItemScale = 0.8;
    private double _neighborOffset = 0.6;
    private double _sideItemSpacing = 0.18;
    private double _visibleSideItems = 3;

    /// <summary>How far side items turn, in degrees (default 50; 0–80).</summary>
    public double RotationAngle { get => _rotationAngle; set => SetSetting(ref _rotationAngle, value, 0, 80, nameof(RotationAngle)); }

    /// <summary>The scale of side items (default 0.8; 0.1–1).</summary>
    public double SideItemScale { get => _sideItemScale; set => SetSetting(ref _sideItemScale, value, 0.1, 1, nameof(SideItemScale)); }

    /// <summary>
    /// How far the centers of the current item's neighbors are from its center, in item lengths (default 0.6; 0–2): smaller
    /// values tuck them further behind it.
    /// </summary>
    public double NeighborOffset { get => _neighborOffset; set => SetSetting(ref _neighborOffset, value, 0, 2, nameof(NeighborOffset)); }

    /// <summary>The distance between further side items' centers, in item lengths (default 0.18; 0–2).</summary>
    public double SideItemSpacing { get => _sideItemSpacing; set => SetSetting(ref _sideItemSpacing, value, 0, 2, nameof(SideItemSpacing)); }

    /// <summary>How many side items show on either side (default 3; 0–20): farther ones fade out over one more item.</summary>
    public double VisibleSideItems { get => _visibleSideItems; set => SetSetting(ref _visibleSideItems, value, 0, 20, nameof(VisibleSideItems)); }

    /// <inheritdoc />
    public override SkUiCarouselItemTransform GetItemTransform(double position, in SkUiCarouselItemMetrics metrics)
    {
        var distance = Math.Abs(position);
        var near = Math.Min(distance, 1);
        var length = metrics.ItemLength;
        // Where the item's center shows: the neighbors at NeighborOffset, the others SideItemSpacing apart beyond them.
        var center = near * _neighborOffset * length + Math.Max(0, distance - 1) * _sideItemSpacing * length;
        var visible = _visibleSideItems;
        return new SkUiCarouselItemTransform
        {
            Translation = Math.Sign(position) * center - position * metrics.Stride,
            Scale = 1 - (1 - _sideItemScale) * near,
            Tilt = Math.Clamp(position, -1, 1) * _rotationAngle,
            Opacity = Math.Clamp(visible + 1 - distance, 0, 1),
            ZIndex = -distance
        };
    }

    /// <inheritdoc />
    public override double GetVisibleRange(in SkUiCarouselItemMetrics metrics) => _visibleSideItems + 1;
}

/// <summary>
/// Side items shrink and fade as they leave the current place, and move closer so the gaps between them stay
/// <see cref="SkUiCarouselView.ItemSpacing"/> (as Material's carousels): with peeking neighbors
/// (<see cref="SkUiCarouselView.PeekAreaInsets"/>) more of them shows.
/// </summary>
public sealed class SkUiScaleEffect : SkUiCarouselEffect
{
    private double _sideItemScale = 0.85;
    private double _sideItemOpacity = 0.6;
    private bool _keepsSpacing = true;

    /// <summary>The scale of side items, reached one item from the current place (default 0.85; 0.1–1).</summary>
    public double SideItemScale { get => _sideItemScale; set => SetSetting(ref _sideItemScale, value, 0.1, 1, nameof(SideItemScale)); }

    /// <summary>The opacity of side items, reached one item from the current place (default 0.6; 0–1).</summary>
    public double SideItemOpacity { get => _sideItemOpacity; set => SetSetting(ref _sideItemOpacity, value, 0, 1, nameof(SideItemOpacity)); }

    /// <summary>Whether side items move closer so the gaps between scaled items stay the item spacing (default <c>true</c>); <c>false</c>: they scale in place.</summary>
    public bool KeepsSpacing
    {
        get => _keepsSpacing;
        set
        {
            if (_keepsSpacing == value)
                return;
            _keepsSpacing = value;
            OnChanged();
        }
    }

    /// <inheritdoc />
    public override SkUiCarouselItemTransform GetItemTransform(double position, in SkUiCarouselItemMetrics metrics)
    {
        var distance = Math.Abs(position);
        var near = Math.Min(distance, 1);
        var shrink = 1 - _sideItemScale;
        var translation = 0d;
        if (_keepsSpacing)
        {
            // The center's distance is the integral of (spacing + length · scale) over the positions passed: within the
            // first item the scale shrinks linearly, beyond it stays the side scale.
            var length = metrics.ItemLength;
            var center = metrics.ItemSpacing * distance + length * (near - shrink * near * near / 2) + Math.Max(0, distance - 1) * length * _sideItemScale;
            translation = Math.Sign(position) * center - position * metrics.Stride;
        }
        return new SkUiCarouselItemTransform
        {
            Translation = translation,
            Scale = 1 - shrink * near,
            Opacity = 1 - (1 - _sideItemOpacity) * near,
            ZIndex = -distance
        };
    }

    /// <inheritdoc />
    public override double GetVisibleRange(in SkUiCarouselItemMetrics metrics)
    {
        if (!_keepsSpacing || metrics.Stride <= 0)
            return 0;
        // Side items move closer: the farthest one that reaches into the viewport from the current place.
        var side = metrics.ItemLength * _sideItemScale + metrics.ItemSpacing;
        return side <= 0 ? 0 : 1 + Math.Ceiling(Math.Max(0, metrics.ViewportLength - metrics.ItemLength / 2) / side);
    }
}
