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
    public bool IsVisible;

    /// <summary>Clips the node's own content, children and overlay to its layout rectangle.</summary>
    public bool ClipToBounds;

    /// <summary>Translation applied to children only (scroll offset), subtracted from child positions.</summary>
    public float ChildrenOffsetX, ChildrenOffsetY;

    /// <summary>Optional rectangular clip for children (local coordinates, before children offset); empty = none.</summary>
    public SKRect ChildrenClipRect;

    /// <summary>Optional shaped clip for children (e.g. rounded border). Treated as immutable once committed.</summary>
    public SKPath? ChildrenClipPath;

    /// <summary>Seconds per full revolution of the content picture about its center; 0 = static.</summary>
    public float ContentSpinPeriod;

    /// <summary>
    /// Ink overflow outside the layout rectangle (Left/Top/Right/Bottom as positive amounts), e.g. shadows (FR-20).
    /// Culling, recording cull rects and opacity layers use <see cref="VisualBounds"/>.
    /// </summary>
    public SKRect Overflow;

    public static SkUiRenderProps Default => new()
    {
        ScaleX = 1, ScaleY = 1, AnchorX = 0.5f, AnchorY = 0.5f, Opacity = 1, IsVisible = true, ClipToBounds = true
    };

    /// <summary>Local layout rectangle.</summary>
    public readonly SKRect Bounds => new(0, 0, Width, Height);

    /// <summary>Local ink rectangle including <see cref="Overflow"/>.</summary>
    public readonly SKRect VisualBounds => new(-Overflow.Left, -Overflow.Top, Width + Overflow.Right, Height + Overflow.Bottom);

    /// <summary>True when the node paints nothing and its subtree can be skipped.</summary>
    public readonly bool IsSkipped => !IsVisible || Opacity <= 0 || Width <= 0 || Height <= 0;

    /// <summary>Parent-space matrix: layout offset, then render transform about the anchor.</summary>
    public readonly SKMatrix Matrix
    {
        get
        {
            if (TranslationX == 0 && TranslationY == 0 && Rotation == 0 && ScaleX == 1 && ScaleY == 1)
                return SKMatrix.CreateTranslation(X, Y);
            var anchorX = Width * AnchorX;
            var anchorY = Height * AnchorY;
            return SKMatrix.CreateTranslation(X + TranslationX + anchorX, Y + TranslationY + anchorY)
                .PreConcat(SKMatrix.CreateRotationDegrees(Rotation))
                .PreConcat(SKMatrix.CreateScale(ScaleX, ScaleY))
                .PreConcat(SKMatrix.CreateTranslation(-anchorX, -anchorY));
        }
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
    ChildrenOffsetY
}

internal static class SkUiRenderPropertyCount
{
    public const int Value = 8;
}
