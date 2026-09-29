namespace MauiSkiaUi.Core;

/// <summary>
/// Shared toggle state and tap-to-toggle behavior for Core Switch / CheckBox / RadioButton. <see cref="CheckState"/>
/// is the state (<see cref="SkUiCheckState.Indeterminate"/> included); <see cref="IsChecked"/> is its two-state view.
/// </summary>
public abstract class SkUiCoreToggleControl : SkUiCoreNode
{
    private SkUiCheckState _state;
    private bool _isThreeState;
    private bool _isPressed;

    /// <summary>Unchecked, checked or indeterminate.</summary>
    public SkUiCheckState CheckState
    {
        get => _state;
        set => SetCheckState(value);
    }

    /// <summary>
    /// Whether the control is checked: <c>true</c> only for <see cref="SkUiCheckState.Checked"/>; setting it sets
    /// Checked or Unchecked.
    /// </summary>
    public bool IsChecked
    {
        get => SkUiCheckStates.IsChecked(_state);
        set => SetIsChecked(value);
    }

    /// <summary>
    /// Whether taps also reach <see cref="SkUiCheckState.Indeterminate"/> (Unchecked → Checked → Indeterminate →
    /// Unchecked). Without it, taps go between Checked and Unchecked, and an app-set Indeterminate goes to Checked.
    /// </summary>
    public bool IsThreeState
    {
        get => _isThreeState;
        set => SetIsThreeState(value);
    }

    /// <summary>Whether an eligible pointer is currently pressed inside this control.</summary>
    public bool IsPressed => _isPressed;

    /// <summary>Raised when <see cref="IsChecked"/> changes, including from a tap.</summary>
    public event EventHandler<bool>? CheckedChanged;

    /// <summary>Raised when <see cref="CheckState"/> changes, including from a tap.</summary>
    public event EventHandler<SkUiCheckState>? CheckStateChanged;

    /// <summary>Sets the state and raises the change events.</summary>
    public SkUiCoreToggleControl SetCheckState(SkUiCheckState value)
    {
        var wasChecked = IsChecked;
        if (!SetProperty(ref _state, value, nameof(CheckState))) return this;
        InvalidatePaint();
        // Both property notifications first, then the events: handlers see IsChecked bindings already updated.
        var checkedChanged = wasChecked != IsChecked;
        if (checkedChanged)
            OnPropertyChanged(nameof(IsChecked));
        CheckStateChanged?.Invoke(this, value);
        if (checkedChanged)
            CheckedChanged?.Invoke(this, IsChecked);
        return this;
    }

    /// <summary>Sets Checked or Unchecked.</summary>
    public SkUiCoreToggleControl SetIsChecked(bool value) => SetCheckState(SkUiCheckStates.FromIsChecked(value));

    /// <summary>Sets <see cref="IsThreeState"/>.</summary>
    public SkUiCoreToggleControl SetIsThreeState(bool value)
    {
        SetProperty(ref _isThreeState, value, nameof(IsThreeState));
        return this;
    }

    /// <summary>Called on a completed tap; default moves to the next state (see <see cref="IsThreeState"/>).</summary>
    protected virtual void OnToggled() => SetCheckState(SkUiCheckStates.Next(_state, _isThreeState));

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
