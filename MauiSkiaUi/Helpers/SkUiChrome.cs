using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Compatibility façade that forwards to <see cref="SkUiLook.Current"/>. Prefer calling the look
/// API directly from new code.
/// </summary>
internal static class SkUiChrome
{
    internal static SKPath CreateRoundRectPath(SKRect bounds, float radius) =>
        SkUiLook.Current.CreateRoundRectPath(bounds, radius);

    internal static SKPath CreateRoundRectPath(SKRect bounds, CornerRadius radii) =>
        SkUiLook.Current.CreateRoundRectPath(bounds, radii);

    internal static void DrawRoundedBox(SKCanvas canvas, SKRect bounds, float radius, SKColor fill, SKColor border, float width) =>
        SkUiLook.Current.DrawRoundedBox(canvas, bounds, radius, fill, border, width);

    internal static void DrawRoundedBox(SKCanvas canvas, SKRect bounds, CornerRadius radii, SKColor fill, SKColor border, float width) =>
        SkUiLook.Current.DrawRoundedBox(canvas, bounds, radii, fill, border, width);

    internal static void DrawSwitch(SKCanvas canvas, SKRect bounds, bool isChecked, SKColor track, SKColor thumb) =>
        SkUiLook.Current.DrawSwitch(canvas, bounds, isChecked, track, thumb);

    internal static void DrawCheckBox(SKCanvas canvas, float size, bool isChecked, SKColor fill, SKColor border) =>
        SkUiLook.Current.DrawCheckBox(canvas, size, isChecked, fill, border);

    internal static void DrawRadioButton(SKCanvas canvas, float size, bool isChecked, SKColor ring, SKColor dot) =>
        SkUiLook.Current.DrawRadioButton(canvas, size, isChecked, ring, dot);

    internal static void DrawActivityIndicator(SKCanvas canvas, float width, float height, float sweepStart, SKPaint paint) =>
        SkUiLook.Current.DrawActivityIndicator(canvas, width, height, sweepStart, paint);

    internal static SKRect ComputeImageDestination(float viewWidth, float viewHeight, float imageWidth, float imageHeight, Aspect aspect) =>
        SkUiLook.Current.ComputeImageDestination(viewWidth, viewHeight, imageWidth, imageHeight, aspect);

    internal static void DrawImage(SKCanvas canvas, SKImage image, float viewWidth, float viewHeight, Aspect aspect) =>
        SkUiLook.Current.DrawImage(canvas, image, viewWidth, viewHeight, aspect);

    internal static void DrawPressTint(SKCanvas canvas, SKRect bounds, float cornerRadius, bool disabled, bool pressed) =>
        SkUiLook.Current.DrawPressTint(canvas, bounds, cornerRadius, disabled, pressed);
}
