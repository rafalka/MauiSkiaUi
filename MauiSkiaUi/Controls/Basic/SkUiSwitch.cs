using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn on/off pill switch, similar to MAUI's Switch. <see cref="IsToggled"/> / <see cref="Toggled"/> are MAUI's names
/// for the two-state view of <see cref="SkUiToggleControl.CheckState"/>, the same as <see cref="SkUiToggleControl.IsChecked"/>
/// / <see cref="SkUiToggleControl.CheckedChanged"/>; all of them stay in step.
/// </summary>
public class SkUiSwitch : SkUiToggleControl
{
    private Color _onColor = SkUiColors.Accent;
    private Color _thumbColor = Colors.White;
    private bool _syncingIsToggled;

    /// <summary>Bindable <see cref="IsToggled"/> (two-way by default, as in MAUI).</summary>
    public static readonly BindableProperty IsToggledProperty = BindableProperty.Create(nameof(IsToggled), typeof(bool), typeof(SkUiSwitch),
        false, BindingMode.TwoWay,
        propertyChanged: (view, _, value) => ((SkUiSwitch)view).OnIsToggledPropertyChanged((bool)value));

    /// <summary>Bindable track color while toggled on.</summary>
    public static readonly BindableProperty OnColorProperty = BindableProperty.Create(nameof(OnColor), typeof(Color), typeof(SkUiSwitch), null,
        defaultValueCreator: _ => SkUiColors.Accent,
        propertyChanged: (view, _, value) => ((SkUiSwitch)view).SetOnColor((Color)value));
    /// <summary>Bindable thumb color.</summary>
    public static readonly BindableProperty ThumbColorProperty = BindableProperty.Create(nameof(ThumbColor), typeof(Color), typeof(SkUiSwitch), Colors.White,
        propertyChanged: (view, _, value) => ((SkUiSwitch)view).SetThumbColor((Color)value));

    /// <summary>
    /// Whether the switch is on (MAUI's name for <see cref="SkUiToggleControl.IsChecked"/>): <c>true</c> only for
    /// <see cref="SkUiCheckState.Checked"/>; setting <c>false</c> also clears Indeterminate.
    /// </summary>
    public bool IsToggled
    {
        get => (bool)GetValue(IsToggledProperty);
        set
        {
            // Indeterminate already reads false, so SetValue(false) would change nothing.
            if (!value && CheckState == SkUiCheckState.Indeterminate)
                CheckState = SkUiCheckState.Unchecked;
            else
                SetValue(IsToggledProperty, value);
        }
    }

    /// <summary>Raised when <see cref="IsToggled"/> changes, including from a tap, after <see cref="SkUiToggleControl.CheckedChanged"/>.</summary>
    public event EventHandler<ToggledEventArgs>? Toggled;

    /// <summary>Track color while toggled on.</summary>
    public Color OnColor { get => (Color)GetValue(OnColorProperty); set => SetValue(OnColorProperty, value); }
    /// <summary>Thumb (knob) color.</summary>
    public Color ThumbColor { get => (Color)GetValue(ThumbColorProperty); set => SetValue(ThumbColorProperty, value); }

    /// <summary>Sets <see cref="IsToggled"/>.</summary>
    public SkUiSwitch SetIsToggled(bool value) { SetIsChecked(value); return this; }
    /// <summary>Sets the on-color (same as the property setter).</summary>
    public SkUiSwitch SetOnColor(Color value) { ArgumentNullException.ThrowIfNull(value); if (WriteBindable(OnColorProperty, value)) return this; _onColor = value; InvalidatePaint(); return this; }
    /// <summary>Sets the thumb color (same as the property setter).</summary>
    public SkUiSwitch SetThumbColor(Color value) { ArgumentNullException.ThrowIfNull(value); if (WriteBindable(ThumbColorProperty, value)) return this; _thumbColor = value; InvalidatePaint(); return this; }

    /// <summary>The visual state while on (MAUI's <c>Switch.SwitchOnVisualState</c>).</summary>
    public const string SwitchOnVisualState = "On";

    /// <summary>The visual state while off (MAUI's <c>Switch.SwitchOffVisualState</c>; also Indeterminate).</summary>
    public const string SwitchOffVisualState = "Off";

    /// <summary>As MAUI's Switch: the common states, then <c>On</c> / <c>Off</c> while enabled.</summary>
    protected override void ChangeVisualState()
    {
        base.ChangeVisualState();
        if (IsVisualStateEnabled)
            VisualStateManager.GoToState(this, IsToggled ? SwitchOnVisualState : SwitchOffVisualState);
    }

    private void OnIsToggledPropertyChanged(bool value)
    {
        if (!_syncingIsToggled)
            CommitState(SkUiCheckStates.FromIsChecked(value));
    }

    private protected override void SyncBindableState()
    {
        var isToggled = IsChecked;
        if ((bool)GetValue(IsToggledProperty) == isToggled)
            return;
        _syncingIsToggled = true;
        try
        {
            SetValue(IsToggledProperty, isToggled);
        }
        finally
        {
            _syncingIsToggled = false;
        }
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
        SkUiToggleDrawing.DrawSwitch(canvas, (float)Width, (float)Height, IsRightToLeft, ToggleVisual, _onColor, _thumbColor, IsEnabled);
}
