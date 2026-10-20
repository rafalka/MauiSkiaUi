using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// The fill rule of the buttons and image buttons of both layers (one function, so the layers draw the same): enabled, the
/// background, else the fill color; disabled, <c>DisabledFillColor</c> when set, else what the app set (a background or a
/// fill color) drawn as set, as MAUI draws a disabled button's background (so a <c>Disabled</c> visual state's setters
/// show); only the color scheme's default fill turns into the scheme's <see cref="SkUiColorScheme.Disabled"/>, and a
/// transparent one stays transparent (the look dims the content instead: <see cref="DimsContent"/>).
/// </summary>
internal static class SkUiButtonFill
{
    /// <summary>The fill to draw.</summary>
    /// <param name="enabled">The button is enabled and its command can execute.</param>
    /// <param name="background">The resolved background, <c>null</c> when none is set.</param>
    /// <param name="fill">The fill color (image buttons: transparent).</param>
    /// <param name="fillIsDefault">The fill color is the control's default (the scheme's), not one the app set.</param>
    /// <param name="disabledFill">The button's <c>DisabledFillColor</c> (<c>null</c>: the rule above).</param>
    public static SkUiFill Resolve(bool enabled, SkUiFill? background, Color fill, bool fillIsDefault, Color? disabledFill)
    {
        if (!enabled && disabledFill is not null)
            return SkUiFill.From(disabledFill);
        if (background is { } set)
            return set;
        var resolved = SkUiFill.From(fill);
        return enabled || !fillIsDefault || !resolved.IsVisible ? resolved : SkUiFill.From(SkUiColors.Disabled);
    }

    /// <summary>
    /// Whether a disabled button's text and image are drawn at the look's <see cref="SkUiLook.DisabledButtonContentOpacity"/>:
    /// a button without a visible fill (a text button), unless its <c>DisabledFillColor</c> is set.
    /// </summary>
    /// <param name="fill">The disabled fill (<see cref="Resolve"/>).</param>
    /// <param name="disabledFill">The button's <c>DisabledFillColor</c>.</param>
    public static bool DimsContent(in SkUiFill fill, Color? disabledFill) => disabledFill is null && !fill.IsVisible;

    /// <summary>
    /// Starts a layer the following drawing is composited from at <paramref name="opacity"/> (none at 1 or more); returns
    /// the save count to restore (<see cref="SkUiChromeState.EndClip"/>), or -1.
    /// </summary>
    public static int BeginContentOpacity(SKCanvas canvas, double opacity)
    {
        if (opacity >= 1) return -1;
        // Only disabled text buttons get here: a paint per recording, copied by the recorder.
        using var paint = new SKPaint { Color = SKColors.Black.WithAlpha((byte)Math.Round(255 * Math.Clamp(opacity, 0, 1))) };
        return canvas.SaveLayer(paint);
    }
}
