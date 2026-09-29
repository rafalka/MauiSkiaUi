using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>Drawn checkbox (Core analogue of <c>SkUiCheckBox</c>).</summary>
public class SkUiCoreCheckBox : SkUiCoreToggleControl
{
    private Color _color = SkUiColors.Accent;

    /// <summary>Fill/checkmark color while checked; unchecked draws a neutral outline.</summary>
    public Color Color
    {
        get => _color;
        set => SetColor(value);
    }

    /// <summary>Sets the checked fill/checkmark color.</summary>
    public SkUiCoreCheckBox SetColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _color, value, nameof(Color))) return this;
        InvalidatePaint();
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureCheckBox(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        var size = (float)Math.Min(Frame.Width, Frame.Height);
        var on = CheckState != SkUiCheckState.Unchecked;
        var fillColor = on ? _color : Colors.White;
        var borderColor = on ? _color : SkUiColors.Muted;
        // RTL: the glyph sits at the start (right) edge; the glyph itself is not mirrored.
        var rtlSave = canvas.Save();
        if (IsRightToLeft) canvas.Translate((float)(Frame.Width) - size, 0);
        SkUiLook.Current.DrawCheckBox(canvas, size, CheckState, ToSkColor(fillColor), ToSkColor(borderColor));
        canvas.RestoreToCount(rtlSave);
    }
}
