namespace MauiSkiaUi.Core;

/// <summary>Shared boolean toggle state and tap-to-toggle behavior for Core Switch/CheckBox/RadioButton.</summary>
public abstract class SkUiCoreToggleControl : SkUiCoreNode
{
    private bool _isChecked;
    private bool _isPressed;

    /// <summary>Whether the control is checked/toggled on.</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => SetIsChecked(value);
    }

    /// <summary>Whether an eligible pointer is currently pressed inside this control.</summary>
    public bool IsPressed => _isPressed;

    /// <summary>Raised whenever <see cref="IsChecked"/> changes, including from a tap.</summary>
    public event EventHandler<bool>? CheckedChanged;

    /// <summary>Sets checked state and raises <see cref="CheckedChanged"/> when it changes.</summary>
    public SkUiCoreToggleControl SetIsChecked(bool value)
    {
        if (!SetProperty(ref _isChecked, value, nameof(IsChecked))) return this;
        InvalidatePaint();
        CheckedChanged?.Invoke(this, value);
        return this;
    }

    /// <summary>Called on a completed tap; default toggles <see cref="IsChecked"/>.</summary>
    protected virtual void OnToggled() => SetIsChecked(!_isChecked);


    /// <inheritdoc />
    internal override bool HasIntrinsicTap => true;

    /// <inheritdoc />
    internal override void OnIntrinsicTap(SkUiTappedEventArgs args)
    {
        OnToggled();
    }

    /// <inheritdoc />
    internal override void OnGesturePressedChanged(bool pressed) => SetPressed(pressed);

    private void SetPressed(bool value)
    {
        if (!SetProperty(ref _isPressed, value, nameof(IsPressed))) return;
        InvalidatePaint();
    }
}
