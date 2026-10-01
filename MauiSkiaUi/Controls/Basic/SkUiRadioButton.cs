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
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).SetColor((Color)value));
    /// <summary>Bindable <see cref="GroupName"/>.</summary>
    public static readonly BindableProperty GroupNameProperty = BindableProperty.Create(nameof(GroupName), typeof(string), typeof(SkUiRadioButton), null,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).SetGroupName((string?)value));
    /// <summary>Bindable <see cref="Value"/>.</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(object), typeof(SkUiRadioButton), null,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).SetRadioValue(value));

    /// <summary>Ring/dot color while checked.</summary>
    public Color Color { get => _color; set => SetValue(ColorProperty, value); }
    /// <summary>
    /// The group whose radio buttons exclude each other across the page; <c>null</c> or empty groups the radio button
    /// with its siblings in the same parent.
    /// </summary>
    public string? GroupName { get => _groupName; set => SetValue(GroupNameProperty, value); }
    /// <summary>The value this radio button stands for: the group layout's <see cref="RadioButtonGroup.SelectedValueProperty"/> while it is checked.</summary>
    public object? Value { get => _value; set => SetValue(ValueProperty, value); }

    /// <summary>Sets the color without bindable write-back.</summary>
    public SkUiRadioButton SetColor(Color value) { ArgumentNullException.ThrowIfNull(value); _color = value; InvalidatePaint(); return this; }

    /// <summary>Sets the group name without bindable write-back.</summary>
    public SkUiRadioButton SetGroupName(string? value)
    {
        var old = _groupName;
        if (old == value) return this;
        _groupName = value;
        SkUiRadioGroups.OnGroupNameChanged(this, old);
        return this;
    }

    /// <summary>Sets <see cref="Value"/> without bindable write-back.</summary>
    public SkUiRadioButton SetRadioValue(object? value)
    {
        if (Equals(_value, value)) return this;
        _value = value;
        if (IsChecked)
            SkUiRadioGroups.OnSelectionChanged(this);
        return this;
    }

    /// <summary>Unlike the shared toggle base, a tap only selects (matching MAUI's RadioButton); it never unchecks.</summary>
    protected override SkUiCheckState NextCheckState() => SkUiCheckState.Checked;

    private protected override void OnCheckStateApplied(SkUiCheckState oldState, SkUiCheckState newState)
    {
        if (newState == SkUiCheckState.Checked)
            SkUiRadioGroups.OnChecked(this);
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
