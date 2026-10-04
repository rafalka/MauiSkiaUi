namespace MauiSkiaUi;

/// <summary>
/// Shared toggle state and intrinsic tap-to-toggle behavior for Switch / CheckBox / RadioButton.
/// <see cref="CheckState"/> is the state (<see cref="SkUiCheckState.Indeterminate"/> included); <see cref="IsChecked"/>
/// is its MAUI-compatible two-state view. A tap moves to the next state and writes it back to the bindable
/// properties, so two-way bindings see user changes, as with MAUI's controls. State changes and presses animate with
/// the look's transitions (<see cref="TransitionKind"/>); subclasses draw <see cref="ToggleVisual"/>.
/// </summary>
public abstract class SkUiToggleControl : SkUiView
{
    private SkUiCheckState _state;
    private bool _isThreeState;
    private bool _syncingIsChecked;
    private SkUiToggleAnimator? _transition;
    private SkUiPressAnimator? _press;

    /// <summary>Bindable <see cref="CheckState"/> (two-way by default).</summary>
    public static readonly BindableProperty CheckStateProperty = BindableProperty.Create(nameof(CheckState), typeof(SkUiCheckState), typeof(SkUiToggleControl),
        SkUiCheckState.Unchecked, BindingMode.TwoWay,
        propertyChanged: (view, _, value) => ((SkUiToggleControl)view).OnCheckStatePropertyChanged((SkUiCheckState)value));

    /// <summary>Bindable <see cref="IsChecked"/> (two-way by default, as in MAUI).</summary>
    public static readonly BindableProperty IsCheckedProperty = BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(SkUiToggleControl),
        false, BindingMode.TwoWay,
        propertyChanged: (view, _, value) => ((SkUiToggleControl)view).OnIsCheckedPropertyChanged((bool)value));

    /// <summary>Bindable <see cref="IsThreeState"/>.</summary>
    public static readonly BindableProperty IsThreeStateProperty = BindableProperty.Create(nameof(IsThreeState), typeof(bool), typeof(SkUiToggleControl), false,
        propertyChanged: (view, _, value) => ((SkUiToggleControl)view).OnIsThreeStateChanged((bool)value));

    /// <summary>Unchecked, checked or indeterminate.</summary>
    public SkUiCheckState CheckState { get => (SkUiCheckState)GetValue(CheckStateProperty); set => SetValue(CheckStateProperty, value); }

    /// <summary>
    /// Whether the control is checked (MAUI-compatible view of <see cref="CheckState"/>): <c>true</c> only for
    /// <see cref="SkUiCheckState.Checked"/>; setting it sets Checked or Unchecked (<c>false</c> also clears
    /// Indeterminate). A binding that pushes <c>false</c> while Indeterminate leaves the state: it only mirrors it.
    /// </summary>
    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set
        {
            // Indeterminate already reads false, so SetValue(false) would change nothing.
            if (!value && _state == SkUiCheckState.Indeterminate)
                CheckState = SkUiCheckState.Unchecked;
            else
                SetValue(IsCheckedProperty, value);
        }
    }

    /// <summary>
    /// Whether taps also reach <see cref="SkUiCheckState.Indeterminate"/> (Unchecked → Checked → Indeterminate →
    /// Unchecked). Without it, taps go between Checked and Unchecked, and an Indeterminate set by the app goes to Checked.
    /// </summary>
    public bool IsThreeState { get => (bool)GetValue(IsThreeStateProperty); set => SetValue(IsThreeStateProperty, value); }

    /// <summary>Raised when <see cref="IsChecked"/> changes, including from a tap (MAUI's event arguments).</summary>
    public event EventHandler<CheckedChangedEventArgs>? CheckedChanged;

    /// <summary>Raised when <see cref="CheckState"/> changes, including from a tap.</summary>
    public event EventHandler<SkUiCheckState>? CheckStateChanged;

    /// <summary>Sets the state, as <see cref="CheckState"/> does (both bindable properties follow).</summary>
    public SkUiToggleControl SetCheckState(SkUiCheckState value)
    {
        CheckState = value;
        return this;
    }

    /// <summary>Sets Checked or Unchecked.</summary>
    public SkUiToggleControl SetIsChecked(bool value)
    {
        IsChecked = value;
        return this;
    }

    /// <summary>Sets <see cref="IsThreeState"/> (same as the property setter).</summary>
    public SkUiToggleControl SetIsThreeState(bool value)
    {
        IsThreeState = value;
        return this;
    }

    private void OnIsThreeStateChanged(bool value)
    {
        _isThreeState = value;
    }

    /// <summary>Which look transition animates state changes (<see cref="SkUiLook.GetTransition"/>).</summary>
    protected virtual SkUiTransitionKind TransitionKind => SkUiTransitionKind.CheckBox;

    /// <summary>What to draw: the state, the transition towards it and the press amount.</summary>
    protected SkUiToggleVisual ToggleVisual =>
        _transition?.Visual(_state, _press?.Pressed ?? 0) ?? SkUiToggleVisual.Settled(_state, _press?.Pressed ?? 0);

    /// <summary>The state a tap moves to (radio buttons only ever select).</summary>
    protected virtual SkUiCheckState NextCheckState() => SkUiCheckStates.Next(_state, _isThreeState);

    /// <inheritdoc />
    protected override bool HandlesTap => true;

    /// <inheritdoc />
    protected override void OnPressedChanged()
    {
        (_press ??= new SkUiPressAnimator(this, ripple: false)).SetPressed(IsPressed, PressPosition);
        InvalidatePaint();
    }

    /// <inheritdoc />
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        base.OnTapped(args);
        CommitState(NextCheckState());
    }

    /// <summary>A user change: applies <paramref name="value"/> and writes it back to both bindable properties.</summary>
    private protected void CommitState(SkUiCheckState value) => ApplyState(value);

    /// <summary>
    /// Keeps a subclass's other bindable views of the state (Switch <c>IsToggled</c>) in step, with the state already
    /// applied; called on every write-back, before the change events.
    /// </summary>
    private protected virtual void SyncBindableState() { }

    /// <summary>Runs after a state change is applied and written back, before the change events (radio group exclusion).</summary>
    private protected virtual void OnCheckStateApplied(SkUiCheckState oldState, SkUiCheckState newState) { }

    /// <summary>Raises <see cref="CheckedChanged"/>; subclasses add their MAUI-named events (Switch <c>Toggled</c>).</summary>
    private protected virtual void RaiseCheckedChanged(bool isChecked) => CheckedChanged?.Invoke(this, new CheckedChangedEventArgs(isChecked));

    private void OnCheckStatePropertyChanged(SkUiCheckState value) => ApplyState(value);

    private void OnIsCheckedPropertyChanged(bool value)
    {
        if (_syncingIsChecked)
            return;
        ApplyState(SkUiCheckStates.FromIsChecked(value));
    }

    /// <summary>Keeps the <see cref="IsCheckedProperty"/> store (and its bindings) in step with the state.</summary>
    private void SyncIsCheckedProperty()
    {
        var isChecked = SkUiCheckStates.IsChecked(_state);
        if ((bool)GetValue(IsCheckedProperty) == isChecked)
            return;
        _syncingIsChecked = true;
        try
        {
            SetValue(IsCheckedProperty, isChecked);
        }
        finally
        {
            _syncingIsChecked = false;
        }
    }

    /// <summary>
    /// Applies <paramref name="value"/> and updates both bindable properties (and so their bindings) before the change
    /// events run, so handlers see the new state everywhere.
    /// </summary>
    private void ApplyState(SkUiCheckState value)
    {
        var old = _state;
        _state = value;
        // Re-enters OnCheckStatePropertyChanged with the state already applied: that call only syncs.
        SetValue(CheckStateProperty, value);
        SyncIsCheckedProperty();
        SyncBindableState();
        if (old == value)
            return;
        (_transition ??= new SkUiToggleAnimator(this, TransitionKind)).Changed(old, value);
        InvalidatePaint();
        OnCheckStateApplied(old, value);
        ChangeVisualState();
        CheckStateChanged?.Invoke(this, value);
        var isChecked = SkUiCheckStates.IsChecked(value);
        if (SkUiCheckStates.IsChecked(old) != isChecked)
            RaiseCheckedChanged(isChecked);
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.CheckState = _state;
    }
}
