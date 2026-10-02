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
    public static void PaintContent(SKCanvas canvas, IImage image, float width, float height, Thickness padding, ref SkUiChromeState chrome)
    {
        var area = new SKRect((float)padding.Left, (float)padding.Top, width - (float)padding.Right, height - (float)padding.Bottom);
        if (area.Width <= 0 || area.Height <= 0)
            return;
        var saveCount = chrome.ClipToRadii(canvas, width, height, chrome.Radii);
        image.Paint(canvas, area);
        SkUiChromeState.EndClip(canvas, saveCount);
    }

    /// <summary>Press / disabled feedback over the whole button, then the border on top.</summary>
    public static void PaintOverlay(SKCanvas canvas, float width, float height, in SkUiChromeState chrome, SkUiPressVisual press, bool isEnabled)
    {
        SkUiLook.Current.DrawPressOverlay(canvas, new SkUiPressOverlayPaint(new SKRect(0, 0, width, height), chrome.Radii, press, isEnabled));
        chrome.DrawBorder(canvas, width, height, chrome.Radii);
    }
}
