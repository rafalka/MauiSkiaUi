using Microsoft.Maui.Controls;

namespace MauiSkiaUi;

/// <summary>Shared boolean toggle state and intrinsic tap-to-toggle behavior for Switch/CheckBox/RadioButton.</summary>
public abstract class SkUiToggleControl : SkUiView
{
    private bool isChecked;

    /// <summary>Bindable checked/toggled state.</summary>
    public static readonly BindableProperty IsCheckedProperty = BindableProperty.Create(nameof(IsChecked), typeof(bool), typeof(SkUiToggleControl), false,
        propertyChanged: (view, _, value) => ((SkUiToggleControl)view).SetIsChecked((bool)value));

    /// <summary>Whether the control is checked/toggled on.</summary>
    public bool IsChecked { get => isChecked; set => SetValue(IsCheckedProperty, value); }

    /// <summary>Raised whenever <see cref="IsChecked"/> changes, including from a tap.</summary>
    public event EventHandler<bool>? CheckedChanged;

    /// <summary>Sets checked state without bindable write-back.</summary>
    public SkUiToggleControl SetIsChecked(bool value)
    {
        if (isChecked == value) return this;
        isChecked = value;
        InvalidatePaint();
        CheckedChanged?.Invoke(this, value);
        return this;
    }

    /// <inheritdoc />
    protected override bool HandlesTap => true;
    /// <inheritdoc />
    protected override void OnPressedChanged() => InvalidatePaint();

    /// <inheritdoc />
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        base.OnTapped(args);
        SetIsChecked(!isChecked);
    }
}
