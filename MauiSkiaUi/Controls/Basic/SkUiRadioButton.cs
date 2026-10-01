using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn radio button, similar to MAUI's RadioButton. Checking one unchecks the others of its group, with MAUI's
/// rules: radio buttons without a <see cref="GroupName"/> are grouped with their siblings in the same parent; radio
/// buttons with a <see cref="GroupName"/> are grouped with every radio button of that name on the page.
/// MAUI's <see cref="RadioButtonGroup"/> <c>GroupName</c> / <c>SelectedValue</c> on an <see cref="SkUiLayout"/> name its radio
/// buttons and bind the group's selected <see cref="Value"/>.
/// </summary>
public class SkUiRadioButton : SkUiToggleControl
{
    private Color _color = SkUiColors.Accent;
    private string? _groupName;
    private object? _value;

    /// <summary>Bindable dot/ring color while checked.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiRadioButton), null,
        defaultValueCreator: _ => SkUiColors.Accent,
        validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnColorChanged((Color)value));
    /// <summary>Bindable <see cref="GroupName"/>.</summary>
    public static readonly BindableProperty GroupNameProperty = BindableProperty.Create(nameof(GroupName), typeof(string), typeof(SkUiRadioButton), null,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnGroupNameChanged((string?)value));
    /// <summary>Bindable <see cref="Value"/>.</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(object), typeof(SkUiRadioButton), null,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnValueChanged(value));

    /// <summary>Ring/dot color while checked.</summary>
    public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    /// <summary>
    /// The group whose radio buttons exclude each other across the page; <c>null</c> or empty groups the radio button
    /// with its siblings in the same parent.
    /// </summary>
    public string? GroupName { get => (string?)GetValue(GroupNameProperty); set => SetValue(GroupNameProperty, value); }
    /// <summary>The value this radio button stands for: the group layout's <see cref="RadioButtonGroup.SelectedValueProperty"/> while it is checked.</summary>
    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Sets the color (same as the property setter).</summary>
    public SkUiRadioButton SetColor(Color value) { ArgumentNullException.ThrowIfNull(value); Color = value; return this; }
    private void OnColorChanged(Color value) { _color = value; InvalidatePaint(); }

    /// <summary>Sets the group name (same as the property setter).</summary>
    public SkUiRadioButton SetGroupName(string? value)
    {
        GroupName = value;
        return this;
    }

    private void OnGroupNameChanged(string? value)
    {
        var old = _groupName;
        if (old == value) return;
        _groupName = value;
        SkUiRadioGroups.OnGroupNameChanged(this, old);
    }

    /// <summary>Sets <see cref="Value"/> (same as the property setter).</summary>
    public SkUiRadioButton SetRadioValue(object? value)
    {
        Value = value;
        return this;
    }

    private void OnValueChanged(object? value)
    {
        if (Equals(_value, value)) return;
        _value = value;
        if (IsChecked)
            SkUiRadioGroups.OnSelectionChanged(this);
    }

    /// <summary>Unlike the shared toggle base, a tap only selects (matching MAUI's RadioButton); it never unchecks.</summary>
    protected override SkUiCheckState NextCheckState() => SkUiCheckState.Checked;

    private protected override void OnCheckStateApplied(SkUiCheckState oldState, SkUiCheckState newState)
    {
        if (newState == SkUiCheckState.Checked)
            SkUiRadioGroups.OnChecked(this);
    }

    /// <summary>The visual state while checked (MAUI's <c>RadioButton.CheckedVisualState</c>).</summary>
    public const string CheckedVisualState = "Checked";

    /// <summary>The visual state while not checked (MAUI's <c>RadioButton.UncheckedVisualState</c>; also Indeterminate).</summary>
    public const string UncheckedVisualState = "Unchecked";

    /// <summary>As MAUI's RadioButton: <c>Checked</c> / <c>Unchecked</c> (in any group that defines them), then the common states.</summary>
    protected override void ChangeVisualState()
    {
        VisualStateManager.GoToState(this, IsChecked ? CheckedVisualState : UncheckedVisualState);
        base.ChangeVisualState();
    }

    /// <summary>A change made by the group (exclusion, selected value): written back like a user change.</summary>
    internal void SetCheckedByGroup(bool value) => CommitState(SkUiCheckStates.FromIsChecked(value));

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureRadioButton(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override SkUiTransitionKind TransitionKind => SkUiTransitionKind.RadioButton;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiToggleDrawing.DrawRadioButton(canvas, (float)Width, (float)Height, IsRightToLeft, ToggleVisual, _color, IsEnabled);
}
