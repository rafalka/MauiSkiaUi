using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Switch, CheckBox and RadioButton drawing shared by the SkUi* and Core layers: resolves the colors (disabled
/// dimming), applies right-to-left placement and calls the look with the control's <see cref="SkUiToggleVisual"/>.
/// </summary>
internal static class SkUiToggleDrawing
{
    public static void DrawSwitch(SKCanvas canvas, float width, float height, bool rightToLeft, SkUiToggleVisual visual,
        Color onColor, Color thumbColor, bool enabled)
    {
        var offTrack = SkUiColors.TrackOff;
        if (!enabled)
        {
            onColor = onColor.MultiplyAlpha(0.5f);
            offTrack = offTrack.MultiplyAlpha(0.5f);
            thumbColor = thumbColor.MultiplyAlpha(0.7f);
        }
        // RTL: the thumb travels the other way (as native RTL switches).
        var save = canvas.Save();
        if (rightToLeft) canvas.Scale(-1, 1, width / 2, 0);
        SkUiLook.Current.DrawSwitch(canvas, new SkUiSwitchPaint(new SKRect(0, 0, width, height), visual,
            ToSkColor(onColor), ToSkColor(offTrack), ToSkColor(thumbColor), enabled));
        canvas.RestoreToCount(save);
    }

    public static void DrawCheckBox(SKCanvas canvas, float width, float height, bool rightToLeft, SkUiToggleVisual visual,
        Color color, bool enabled)
    {
        var size = Math.Min(width, height);
        var background = Colors.White;
        var border = SkUiColors.Muted;
        if (!enabled)
        {
            color = color.MultiplyAlpha(0.5f);
            background = background.MultiplyAlpha(0.5f);
            border = border.MultiplyAlpha(0.5f);
        }
        // RTL: the glyph sits at the start (right) edge; the glyph itself is not mirrored.
        var save = canvas.Save();
        if (rightToLeft) canvas.Translate(width - size, 0);
        SkUiLook.Current.DrawCheckBox(canvas, new SkUiCheckBoxPaint(size, visual, ToSkColor(color), ToSkColor(background), ToSkColor(border), enabled));
        canvas.RestoreToCount(save);
    }

    public static void DrawRadioButton(SKCanvas canvas, float width, float height, bool rightToLeft, SkUiToggleVisual visual,
        Color color, bool enabled)
    {
        var size = Math.Min(width, height);
        var ring = SkUiColors.Muted;
        if (!enabled)
        {
            color = color.MultiplyAlpha(0.5f);
            ring = ring.MultiplyAlpha(0.5f);
        }
        var save = canvas.Save();
        if (rightToLeft) canvas.Translate(width - size, 0);
        SkUiLook.Current.DrawRadioButton(canvas, new SkUiRadioButtonPaint(size, visual, ToSkColor(color), ToSkColor(ring), enabled));
        canvas.RestoreToCount(save);
    }

    internal static SKColor ToSkColor(Color color) => new(
        (byte)(color.Red * 255), (byte)(color.Green * 255),
        (byte)(color.Blue * 255), (byte)(color.Alpha * 255));
}
