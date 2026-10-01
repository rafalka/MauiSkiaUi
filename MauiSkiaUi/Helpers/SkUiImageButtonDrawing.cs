using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>Measure and drawing shared by the image buttons of both layers.</summary>
internal static class SkUiImageButtonDrawing
{
    /// <summary>The button's image painter (its <c>PaintImage</c>).</summary>
    internal interface IImage
    {
        void Paint(SKCanvas canvas, SKRect area);
    }

    /// <summary>The image's intrinsic size plus the padding.</summary>
    public static Size Measure(Size image, Thickness padding) =>
        new(image.Width + padding.HorizontalThickness, image.Height + padding.VerticalThickness);

    /// <summary>The image inside the padding, clipped to the rounded bounds.</summary>
    public static void PaintContent(SKCanvas canvas, IImage image, float width, float height, Thickness padding, CornerRadius radii, ref SkUiRoundedClip clip)
    {
        var area = new SKRect((float)padding.Left, (float)padding.Top, width - (float)padding.Right, height - (float)padding.Bottom);
        if (area.Width <= 0 || area.Height <= 0)
            return;
        if (!SkUiCornerRadii.HasAny(radii))
        {
            image.Paint(canvas, area);
            return;
        }
        var saveCount = canvas.Save();
        canvas.ClipPath(clip.Get(width, height, radii), antialias: true);
        image.Paint(canvas, area);
        canvas.RestoreToCount(saveCount);
    }

    /// <summary>Press / disabled feedback over the whole button, then the border on top.</summary>
    public static void PaintOverlay(SKCanvas canvas, float width, float height, CornerRadius radii, SKColor borderColor, float borderWidth,
        SkUiPressVisual press, bool isEnabled)
    {
        var bounds = new SKRect(0, 0, width, height);
        var look = SkUiLook.Current;
        look.DrawPressOverlay(canvas, new SkUiPressOverlayPaint(bounds, radii, press, isEnabled));
        if (borderWidth > 0 && borderColor.Alpha != 0)
            look.DrawRoundedBox(canvas, bounds, radii, SKColors.Transparent, borderColor, borderWidth);
    }
}
