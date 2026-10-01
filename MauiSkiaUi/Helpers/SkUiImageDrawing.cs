using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>Image drawing shared by the images and image buttons of both layers.</summary>
internal static class SkUiImageDrawing
{
    /// <summary>
    /// Draws <paramref name="image"/> into <paramref name="area"/> through <see cref="SkUiLook.DrawImage"/>.
    /// <see cref="Aspect.Center"/> draws at the source size (one source pixel per DIP) also when the decoder reduced the
    /// image: the look draws the decoded pixels on a canvas scaled back up to the source size.
    /// </summary>
    public static void Draw(SKCanvas canvas, SKImage image, Size sourceSize, SKRect area, Aspect aspect)
    {
        var scale = aspect == Aspect.Center && sourceSize.Width > 0 ? (float)(image.Width / sourceSize.Width) : 1f;
        if (scale == 1f && area.Left == 0 && area.Top == 0)
        {
            SkUiLook.Current.DrawImage(canvas, image, area.Width, area.Height, aspect);
            return;
        }
        var saveCount = canvas.Save();
        canvas.Translate(area.Left, area.Top);
        if (scale != 1f)
            canvas.Scale(1 / scale);
        SkUiLook.Current.DrawImage(canvas, image, area.Width * scale, area.Height * scale, aspect);
        canvas.RestoreToCount(saveCount);
    }
}
