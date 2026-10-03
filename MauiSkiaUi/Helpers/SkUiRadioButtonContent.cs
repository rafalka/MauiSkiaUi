using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// <see cref="SkUiRadioButton"/>'s content beside its circle: the circle (sized by the look) at the start, the
/// look's spacing, then the content (text drawn here, or a drawn view the control arranges), everything inside the
/// padding and the border. Without content the circle fills the padded bounds, as before content existed.
/// Rectangles are left-to-right; the control mirrors what it draws itself (circle, text), while drawn child views are
/// mirrored by their own arrange. Core radio buttons stay a bare circle: compose labels and borders around them.
/// </summary>
internal sealed class SkUiRadioButtonContent(object owner)
{
    private readonly SkUiTextLayout _layout = new(owner);
    private SKPaint? _paint;

    /// <summary>The circle, spacing and content in a padded slot (left-to-right).</summary>
    /// <param name="Circle">The circle's square.</param>
    /// <param name="Content">The content's slot (empty without content).</param>
    internal readonly record struct Placement(Rect Circle, Rect Content);

    /// <summary>The space around circle and content: the padding plus the border width.</summary>
    public static Thickness Inset(Thickness padding, double borderWidth) =>
        borderWidth > 0
            ? new Thickness(padding.Left + borderWidth, padding.Top + borderWidth, padding.Right + borderWidth, padding.Bottom + borderWidth)
            : padding;

    /// <summary>The circle's size from the look.</summary>
    public static Size CircleSize(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureRadioButton(widthConstraint, heightConstraint);

    /// <summary>The width left for the content when the control is measured at <paramref name="widthConstraint"/>.</summary>
    public static double ContentWidthConstraint(double widthConstraint, Size circle, Thickness inset) =>
        Math.Max(0, widthConstraint - inset.HorizontalThickness - circle.Width - SkUiLook.Current.DefaultRadioButtonContentSpacing);

    /// <summary>The height left for the content when the control is measured at <paramref name="heightConstraint"/>.</summary>
    public static double ContentHeightConstraint(double heightConstraint, Thickness inset) =>
        Math.Max(0, heightConstraint - inset.VerticalThickness);

    /// <summary>The control's size: the circle alone, or circle, spacing and <paramref name="content"/> side by side.</summary>
    public static Size Measure(Size circle, Thickness inset, Size? content) => content is { } size
        ? new Size(inset.HorizontalThickness + circle.Width + SkUiLook.Current.DefaultRadioButtonContentSpacing + size.Width,
            inset.VerticalThickness + Math.Max(circle.Height, size.Height))
        : new Size(inset.HorizontalThickness + circle.Width, inset.VerticalThickness + circle.Height);

    /// <summary>Places the circle and the content in a <paramref name="width"/> × <paramref name="height"/> control.</summary>
    public static Placement Arrange(double width, double height, Thickness inset, bool hasContent)
    {
        var inner = new Rect(inset.Left, inset.Top, Math.Max(0, width - inset.HorizontalThickness), Math.Max(0, height - inset.VerticalThickness));
        if (!hasContent)
        {
            // As without content: the circle as large as the slot allows, at the start, from the top.
            var side = Math.Min(inner.Width, inner.Height);
            return new Placement(new Rect(inner.X, inner.Y, side, side), Rect.Zero);
        }
        var circle = CircleSize(inner.Width, inner.Height);
        var diameter = Math.Min(Math.Min(circle.Width, circle.Height), Math.Min(inner.Width, inner.Height));
        var circleRect = new Rect(inner.X + (circle.Width - diameter) / 2, inner.Y + (inner.Height - diameter) / 2, diameter, diameter);
        var start = inner.X + circle.Width + SkUiLook.Current.DefaultRadioButtonContentSpacing;
        return new Placement(circleRect, new Rect(start, inner.Y, Math.Max(0, inner.Right - start), inner.Height));
    }

    /// <summary><paramref name="rect"/> mirrored in a <paramref name="width"/>-wide control (right-to-left).</summary>
    public static Rect Mirror(Rect rect, double width) => new(width - rect.Right, rect.Y, rect.Width, rect.Height);

    /// <summary>The text style of string content: wrapped words, the control's font and spacing.</summary>
    public static SkUiTextStyle TextStyle(string? fontFamily, double fontSize, FontAttributes attributes, double characterSpacing, SkUiTextDirection direction) =>
        new(SkUiTypefaces.Resolve(fontFamily, attributes), fontSize, LineBreakMode.WordWrap, null, -1, -1, characterSpacing, direction,
            SkUiTextRendering.Default, attributes);

    /// <summary>Forgets the broken lines (text, font or direction changed).</summary>
    public void Invalidate() => _layout.Invalidate();

    /// <summary>The text's size wrapped to <paramref name="widthConstraint"/>.</summary>
    public Size MeasureText(string text, in SkUiTextStyle style, double widthConstraint) =>
        _layout.Measure(text, style, default, widthConstraint);

    /// <summary>Draws the text at the start of <paramref name="area"/>, centered vertically next to the circle.</summary>
    public void DrawText(SKCanvas canvas, string text, in SkUiTextStyle style, Rect area, SKColor color)
    {
        if (text.Length == 0 || area.Width <= 0 || area.Height <= 0)
            return;
        var paint = _paint ??= new SKPaint { IsAntialias = true };
        paint.Color = color;
        var save = canvas.Save();
        canvas.Translate((float)area.X, (float)area.Y);
        _layout.Draw(canvas, text, style, default, area.Width, area.Height, TextAlignment.Start, TextAlignment.Center, paint);
        canvas.RestoreToCount(save);
    }

    /// <summary>Draws the circle in <paramref name="circle"/> (already mirrored) through the shared toggle drawing.</summary>
    public static void DrawCircle(SKCanvas canvas, Rect circle, SkUiToggleVisual visual, Color color, bool enabled)
    {
        if (circle.Width <= 0 || circle.Height <= 0)
            return;
        var save = canvas.Save();
        canvas.Translate((float)circle.X, (float)circle.Y);
        SkUiToggleDrawing.DrawRadioButton(canvas, (float)circle.Width, (float)circle.Height, rightToLeft: false, visual, color, enabled);
        canvas.RestoreToCount(save);
    }
}
