using ImagePosition = Microsoft.Maui.Controls.Button.ButtonContentLayout.ImagePosition;

namespace MauiSkiaUi;

/// <summary>
/// Places a button's image next to its text (MAUI's <see cref="Button.ContentLayout"/>), for the buttons of both layers.
/// As MAUI's buttons on every platform: the image keeps its intrinsic size and is scaled down uniformly (never up) to fit
/// the content area, the text gets the space left beside it, and image and text are placed as one group; the spacing
/// applies only when there is text. Left and Right follow the reading direction (Left is the start side in RTL).
/// </summary>
internal static class SkUiButtonImageLayout
{
    /// <summary>The button's text, measured without padding.</summary>
    internal interface IText
    {
        /// <summary>Whether there is text to show.</summary>
        bool HasText { get; }

        /// <summary>The text's size (no padding) when wrapped to <paramref name="widthConstraint"/>.</summary>
        Size MeasureText(double widthConstraint);
    }

    /// <summary>The image and the text placed in a button.</summary>
    /// <param name="Image">Where the image is drawn (aspect kept).</param>
    /// <param name="TextInset">The text's slot as an inset from the button's edges.</param>
    internal readonly record struct Placement(Rect Image, Thickness TextInset);

    /// <summary>The button's content size: padding, image, spacing and text.</summary>
    public static Size Measure(IText text, Size image, Button.ButtonContentLayout layout, Thickness padding, double widthConstraint, double heightConstraint)
    {
        var spacing = Spacing(text, image, layout);
        var width = Math.Max(0, widthConstraint - padding.HorizontalThickness);
        var height = Math.Max(0, heightConstraint - padding.VerticalThickness);
        if (IsHorizontal(layout))
        {
            var fitted = Fit(image, width - spacing, height);
            var textSize = text.HasText ? text.MeasureText(Math.Max(0, width - fitted.Width - spacing)) : Size.Zero;
            return new Size(padding.HorizontalThickness + fitted.Width + spacing + textSize.Width,
                padding.VerticalThickness + Math.Max(fitted.Height, textSize.Height));
        }
        else
        {
            var fitted = Fit(image, width, height - spacing);
            var textSize = text.HasText ? text.MeasureText(width) : Size.Zero;
            return new Size(padding.HorizontalThickness + Math.Max(fitted.Width, textSize.Width),
                padding.VerticalThickness + fitted.Height + spacing + textSize.Height);
        }
    }

    /// <summary>
    /// Places the image and the text in a <paramref name="width"/> × <paramref name="height"/> button: the group by the
    /// text alignments inside the padding (centered for buttons), the image centered across the text.
    /// </summary>
    public static Placement Arrange(IText text, Size image, Button.ButtonContentLayout layout, Thickness padding, double width, double height,
        TextAlignment horizontal, TextAlignment vertical, bool rightToLeft)
    {
        var spacing = Spacing(text, image, layout);
        var contentWidth = Math.Max(0, width - padding.HorizontalThickness);
        var contentHeight = Math.Max(0, height - padding.VerticalThickness);
        Rect imageRect, textRect;
        if (IsHorizontal(layout))
        {
            var fitted = Fit(image, contentWidth - spacing, contentHeight);
            var available = Math.Max(0, contentWidth - fitted.Width - spacing);
            var textSize = text.HasText ? text.MeasureText(available) : Size.Zero;
            // A hair wider than the lines, so drawing in the tight slot breaks them as measured.
            var textWidth = Math.Min(available, textSize.Width + TextSlack);
            var groupWidth = fitted.Width + spacing + textWidth;
            var groupHeight = Math.Max(fitted.Height, textSize.Height);
            var x = padding.Left + Offset(contentWidth - groupWidth, horizontal);
            var y = padding.Top + Offset(contentHeight - groupHeight, vertical);
            var imageFirst = layout.Position == ImagePosition.Left;
            imageRect = new Rect(imageFirst ? x : x + textWidth + spacing, y + (groupHeight - fitted.Height) / 2, fitted.Width, fitted.Height);
            textRect = new Rect(imageFirst ? x + fitted.Width + spacing : x, y + (groupHeight - textSize.Height) / 2, textWidth, textSize.Height);
        }
        else
        {
            var fitted = Fit(image, contentWidth, contentHeight - spacing);
            var textSize = text.HasText ? text.MeasureText(contentWidth) : Size.Zero;
            var groupWidth = Math.Max(fitted.Width, Math.Min(contentWidth, textSize.Width));
            var groupHeight = fitted.Height + spacing + textSize.Height;
            var x = padding.Left + Offset(contentWidth - groupWidth, horizontal);
            var y = padding.Top + Offset(contentHeight - groupHeight, vertical);
            var imageFirst = layout.Position == ImagePosition.Top;
            imageRect = new Rect(x + (groupWidth - fitted.Width) / 2, imageFirst ? y : y + textSize.Height + spacing, fitted.Width, fitted.Height);
            // The lines keep the text alignment across the content width.
            textRect = new Rect(padding.Left, imageFirst ? y + fitted.Height + spacing : y, contentWidth, textSize.Height);
        }
        if (rightToLeft)
        {
            imageRect = Mirror(imageRect, width);
            textRect = Mirror(textRect, width);
        }
        return new Placement(imageRect, new Thickness(textRect.Left, textRect.Top, width - textRect.Right, height - textRect.Bottom));
    }

    /// <summary>
    /// <paramref name="image"/> scaled down uniformly to fit <paramref name="width"/> × <paramref name="height"/> (never up;
    /// unbounded sides do not limit it).
    /// </summary>
    public static Size Fit(Size image, double width, double height)
    {
        if (image.Width <= 0 || image.Height <= 0)
            return Size.Zero;
        var scale = Math.Clamp(Math.Min(Math.Max(0, width) / image.Width, Math.Max(0, height) / image.Height), 0, 1);
        return new Size(image.Width * scale, image.Height * scale);
    }

    private const double TextSlack = 0.01;

    private static bool IsHorizontal(Button.ButtonContentLayout layout) => layout.Position is ImagePosition.Left or ImagePosition.Right;

    /// <summary>The spacing between image and text: only with both, never negative.</summary>
    private static double Spacing(IText text, Size image, Button.ButtonContentLayout layout) =>
        text.HasText && image.Width > 0 && image.Height > 0 && double.IsFinite(layout.Spacing) ? Math.Max(0, layout.Spacing) : 0;

    private static double Offset(double free, TextAlignment alignment) =>
        free <= 0 ? 0 : alignment == TextAlignment.Center ? free / 2 : alignment == TextAlignment.End ? free : 0;

    private static Rect Mirror(Rect rect, double width) => new(width - rect.Right, rect.Y, rect.Width, rect.Height);
}
