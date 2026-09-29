using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Drawn radio button (Core analogue of <c>SkUiRadioButton</c>).
/// <see cref="GroupName"/> is for app-level bookkeeping only — mutual exclusion is not enforced.
/// </summary>
public class SkUiCoreRadioButton : SkUiCoreToggleControl
{
    private Color _color = SkUiColors.Accent;
    private string? _groupName;

    /// <summary>Ring/dot color while checked.</summary>
    public Color Color
    {
        get => _color;
        set => SetColor(value);
    }

    /// <summary>Group name for app-level mutual exclusion; not enforced by this control.</summary>
    public string? GroupName
    {
        get => _groupName;
        set => SetGroupName(value);
    }

    /// <summary>Sets the ring/dot color.</summary>
    public SkUiCoreRadioButton SetColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _color, value, nameof(Color))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the group name without enforcing mutual exclusion.</summary>
    public SkUiCoreRadioButton SetGroupName(string? value)
    {
        SetProperty(ref _groupName, value, nameof(GroupName));
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
}
