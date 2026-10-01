using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn checkbox, similar to MAUI's CheckBox.</summary>
public class SkUiCheckBox : SkUiToggleControl
{
    private Color _color = SkUiColors.Accent;

    /// <summary>Bindable checkmark/fill color while checked.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiCheckBox), null,
        defaultValueCreator: _ => SkUiColors.Accent,
        validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiCheckBox)view).OnColorChanged((Color)value));

    /// <summary>Fill/checkmark color while checked; unchecked always draws a neutral outline.</summary>
    public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <summary>Sets the color (same as the property setter).</summary>
    public SkUiCheckBox SetColor(Color value) { ArgumentNullException.ThrowIfNull(value); Color = value; return this; }
    private void OnColorChanged(Color value) { _color = value; InvalidatePaint(); }

    /// <summary>The <c>CommonStates</c> state while checked (MAUI's <c>CheckBox.IsCheckedVisualState</c>).</summary>
    public const string IsCheckedVisualState = "IsChecked";

    /// <summary>
    /// As MAUI's CheckBox: while enabled and checked, <c>IsChecked</c> when the <c>CommonStates</c> group defines it, else
    /// <c>Normal</c>; otherwise the common states (Indeterminate counts as unchecked).
    /// </summary>
    protected override void ChangeVisualState()
    {
        if (IsVisualStateEnabled && IsChecked)
            VisualStateManager.GoToState(this, HasCommonState(IsCheckedVisualState) ? IsCheckedVisualState : VisualStateManager.CommonStates.Normal);
        else
            base.ChangeVisualState();
    }

    private bool HasCommonState(string name)
    {
        if (!this.HasVisualStateGroups())
            return false;
        foreach (var group in VisualStateManager.GetVisualStateGroups(this))
        {
            if (group.Name != "CommonStates")
                continue;
            foreach (var state in group.States)
                if (state.Name == name)
                    return true;
            return false;
        }
        return false;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureCheckBox(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override SkUiTransitionKind TransitionKind => SkUiTransitionKind.CheckBox;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiToggleDrawing.DrawCheckBox(canvas, (float)Width, (float)Height, IsRightToLeft, ToggleVisual, _color, IsEnabled);
}
