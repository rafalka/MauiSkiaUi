using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Drawn radio button (Core analogue of <c>SkUiRadioButton</c>): a circle that a tap checks (never unchecks). It does not
/// group itself: checking it leaves other radio buttons alone. Group radio buttons with
/// <see cref="SkUiCoreRadioButtons.Group"/>, or uncheck them with <see cref="SkUiCoreRadioButtons.Uncheck"/> /
/// <see cref="SkUiCoreRadioButtons.UncheckRadioButtons"/>.
/// </summary>
public class SkUiCoreRadioButton : SkUiCoreToggleControl
{
    private Color _color = SkUiColors.Accent;

    /// <summary>Ring/dot color while checked.</summary>
    public Color Color
    {
        get => _color;
        set => SetColor(value);
    }

    /// <summary>Sets the ring/dot color.</summary>
    public SkUiCoreRadioButton SetColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _color, value, nameof(Color))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>A tap only selects (never unchecks), matching MAUI RadioButton.</summary>
    protected override void OnToggled() => SetCheckState(SkUiCheckState.Checked);

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureRadioButton(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override SkUiTransitionKind TransitionKind => SkUiTransitionKind.RadioButton;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiToggleDrawing.DrawRadioButton(canvas, (float)Frame.Width, (float)Frame.Height, IsRightToLeft, ToggleVisual, _color, enabled: true);

    /// <summary>The circle: as large as the control's smaller side, at its start, from the top.</summary>
    protected internal override void AddSkeletonBones(SkUiSkeletonBuilder bones)
    {
        var size = Math.Min(bones.Width, bones.Height);
        bones.Add(SkUiSkeletonPartKind.RadioButton, SKRect.Create(IsRightToLeft ? bones.Width - size : 0, 0, size, size));
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.RadioButton;
    }
}
