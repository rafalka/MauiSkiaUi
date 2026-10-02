using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// The rounded chrome of the labels, buttons and image buttons of both layers: per-corner radii, border color and width,
/// and the cached clip path. Each control keeps one, sets it from its own properties (bindables or Core setters) and
/// draws its fill, border and clip through it, so both layers draw the same chrome.
/// </summary>
internal struct SkUiChromeState
{
    private CornerRadius _radii;
    private Color? _borderColor;
    private double _borderWidth;
    private SkUiRoundedClip _clip;

    /// <summary>The stored radii (see <see cref="ResolveRadii"/> for the ones drawn).</summary>
    public readonly CornerRadius Radii => _radii;

    /// <summary>Whether the radii were set; until then <see cref="ResolveRadii"/> returns the control's default.</summary>
    public bool HasExplicitRadii { readonly get; private set; }

    /// <summary>Border color; transparent until set.</summary>
    public readonly Color BorderColor => _borderColor ?? Colors.Transparent;

    /// <summary>Border width in DIPs.</summary>
    public readonly double BorderWidth => _borderWidth;

    /// <summary>Whether a border is drawn.</summary>
    public readonly bool HasBorder => _borderWidth > 0 && BorderColor.Alpha > 0;

    /// <summary>The radii drawn: the stored ones once set, else <paramref name="defaultRadius"/> on every corner.</summary>
    public readonly CornerRadius ResolveRadii(double defaultRadius) => HasExplicitRadii ? _radii : new CornerRadius(defaultRadius);

    /// <summary>Stores validated radii and marks them explicit; returns whether the drawn radii may have changed.</summary>
    public bool SetRadii(CornerRadius value)
    {
        SkUiCornerRadii.Validate(value, nameof(value));
        var changed = _radii != value || !HasExplicitRadii;
        _radii = value;
        HasExplicitRadii = true;
        return changed;
    }

    /// <summary>Stores the border color; returns whether it changed.</summary>
    public bool SetBorderColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (BorderColor == value) return false;
        _borderColor = value;
        return true;
    }

    /// <summary>Stores a validated border width; returns whether it changed.</summary>
    public bool SetBorderWidth(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        if (_borderWidth == value) return false;
        _borderWidth = value;
        return true;
    }

    /// <summary>Draws the rounded fill and the border inside <paramref name="width"/> × <paramref name="height"/> through the look.</summary>
    public readonly void Draw(SKCanvas canvas, float width, float height, CornerRadius radii, SKColor fill) =>
        SkUiLook.Current.DrawRoundedBox(canvas, new SKRect(0, 0, width, height), radii, fill, ToSkColor(BorderColor), (float)_borderWidth);

    /// <summary>Draws only the border (over content), as image buttons do.</summary>
    public readonly void DrawBorder(SKCanvas canvas, float width, float height, CornerRadius radii)
    {
        if (HasBorder)
            SkUiLook.Current.DrawRoundedBox(canvas, new SKRect(0, 0, width, height), radii, SKColors.Transparent, ToSkColor(BorderColor), (float)_borderWidth);
    }

    /// <summary>
    /// Clips what follows to the rounded bounds and returns the canvas save count to restore, or -1 when the corners are
    /// square and nothing was clipped.
    /// </summary>
    public int ClipToRadii(SKCanvas canvas, float width, float height, CornerRadius radii)
    {
        if (!SkUiCornerRadii.HasAny(radii))
            return -1;
        var saveCount = canvas.Save();
        canvas.ClipPath(_clip.Get(width, height, radii), antialias: true);
        return saveCount;
    }

    /// <summary>Restores a clip from <see cref="ClipToRadii"/>.</summary>
    public static void EndClip(SKCanvas canvas, int saveCount)
    {
        if (saveCount >= 0)
            canvas.RestoreToCount(saveCount);
    }

    private static SKColor ToSkColor(Color color) => new(
        (byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), (byte)(color.Alpha * 255));
}
