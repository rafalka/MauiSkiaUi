using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Drawn on/off pill switch (Core analogue of <c>SkUiSwitch</c>). <see cref="IsToggled"/> / <see cref="Toggled"/> are
/// MAUI's names for <see cref="SkUiCoreToggleControl.IsChecked"/> / <see cref="SkUiCoreToggleControl.CheckedChanged"/>.
/// </summary>
public class SkUiCoreSwitch : SkUiCoreToggleControl
{
    private Color _onColor = SkUiColors.Accent;
    private Color _thumbColor = Colors.White;

    /// <summary>Whether the switch is on (MAUI's name for <see cref="SkUiCoreToggleControl.IsChecked"/>).</summary>
    public bool IsToggled
    {
        get => IsChecked;
        set => SetIsToggled(value);
    }

    /// <summary>Raised when <see cref="IsToggled"/> changes, including from a tap, after <see cref="SkUiCoreToggleControl.CheckedChanged"/>.</summary>
    public event EventHandler<ToggledEventArgs>? Toggled;

    /// <summary>Track color while toggled on.</summary>
    public Color OnColor
    {
        get => _onColor;
        set => SetOnColor(value);
    }

    /// <summary>Thumb (knob) color.</summary>
    public Color ThumbColor
    {
        get => _thumbColor;
        set => SetThumbColor(value);
    }

    /// <summary>Sets <see cref="IsToggled"/> (Checked or Unchecked).</summary>
    public SkUiCoreSwitch SetIsToggled(bool value)
    {
        SetIsChecked(value);
        return this;
    }

    /// <summary>Sets the on-track color.</summary>
    public SkUiCoreSwitch SetOnColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _onColor, value, nameof(OnColor))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the thumb color.</summary>
    public SkUiCoreSwitch SetThumbColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _thumbColor, value, nameof(ThumbColor))) return this;
        InvalidatePaint();
        return this;
    }

    private protected override void OnCheckStateApplied(SkUiCheckState oldState, SkUiCheckState newState)
    {
        if (SkUiCheckStates.IsChecked(oldState) != SkUiCheckStates.IsChecked(newState))
            OnPropertyChanged(nameof(IsToggled));
    }

    private protected override void RaiseCheckedChanged(bool isChecked)
    {
        base.RaiseCheckedChanged(isChecked);
        Toggled?.Invoke(this, new ToggledEventArgs(isChecked));
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureSwitch(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override SkUiTransitionKind TransitionKind => SkUiTransitionKind.Switch;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiToggleDrawing.DrawSwitch(canvas, (float)Frame.Width, (float)Frame.Height, IsRightToLeft, ToggleVisual, _onColor, _thumbColor, enabled: true);
}
