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

    public static CornerRadius Validate(CornerRadius radii, string name)
    {
        if (radii.TopLeft < 0 || radii.TopRight < 0 || radii.BottomRight < 0 || radii.BottomLeft < 0)
            throw new ArgumentOutOfRangeException(name, radii, "Corner radii must be non-negative.");
        return radii;
    }
}
