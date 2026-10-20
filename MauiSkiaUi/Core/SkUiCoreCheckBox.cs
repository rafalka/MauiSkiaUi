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
    /// <remarks>At least the look's <see cref="SkUiLook.DefaultCheckBoxMinimumWidth"/> wide unless the check box sets a <see cref="SkUiCoreNode.MinimumWidth"/> (0 too).</remarks>
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureCheckBox(widthConstraint, heightConstraint, HasMinimumWidth);

    /// <inheritdoc />
    protected override SkUiTransitionKind TransitionKind => SkUiTransitionKind.CheckBox;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiToggleDrawing.DrawCheckBox(canvas, (float)Frame.Width, (float)Frame.Height, IsRightToLeft, ToggleVisual, _color, enabled: true);

    /// <summary>The box: as large as the control's smaller side, at its start, from the top.</summary>
    protected internal override void AddSkeletonBones(SkUiSkeletonBuilder bones)
    {
        var size = Math.Min(bones.Width, bones.Height);
        bones.Add(SkUiSkeletonPartKind.CheckBox, SKRect.Create(IsRightToLeft ? bones.Width - size : 0, 0, size, size));
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.CheckBox;
    }
}
