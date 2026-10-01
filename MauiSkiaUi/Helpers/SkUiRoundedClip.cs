using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// The rounded-rect path a label or button clips its text to, cached per size, corner radii and look (rebuilt only when one
/// changes). Recording copies the path into the picture, so replacing it is safe.
/// </summary>
internal struct SkUiRoundedClip
{
    private SKPath? _path;
    private (float Width, float Height, CornerRadius Radii, SkUiLook Look) _key;

    public SKPath Get(float width, float height, CornerRadius radii)
    {
        var look = SkUiLook.Current;
        var key = (width, height, radii, look);
        if (_path is null || key != _key)
        {
            _path?.Dispose();
            _path = look.CreateRoundRectPath(new SKRect(0, 0, width, height), radii);
            _key = key;
        }
        return _path;
    }
}

/// <summary>Corner-radius helpers shared by the labels and buttons of both layers.</summary>
internal static class SkUiCornerRadii
{
    public static bool HasAny(CornerRadius radii) =>
        radii.TopLeft > 0 || radii.TopRight > 0 || radii.BottomRight > 0 || radii.BottomLeft > 0;

    /// <summary>Every radius finite and not negative (<c>NaN</c> or infinity would reach the rounded-path geometry).</summary>
    public static bool IsValid(CornerRadius radii) =>
        IsValid(radii.TopLeft) && IsValid(radii.TopRight) && IsValid(radii.BottomRight) && IsValid(radii.BottomLeft);

    private static bool IsValid(double radius) => double.IsFinite(radius) && radius >= 0;

    public static CornerRadius Validate(CornerRadius radii, string name)
    {
        if (!IsValid(radii))
            throw new ArgumentOutOfRangeException(name, radii, "Corner radii must be finite and not negative.");
        return radii;
    }
}
