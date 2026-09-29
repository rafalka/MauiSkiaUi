using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn radio button, similar to MAUI's RadioButton. <see cref="GroupName"/> is exposed for app-level
/// bookkeeping only; unlike MAUI's <c>RadioButton</c>, this control does not automatically uncheck other
/// radio buttons sharing the same group/parent — apps must clear siblings themselves (e.g. in the checked-changed event).
/// </summary>
public class SkUiRadioButton : SkUiToggleControl
{
    private Color _color = SkUiColors.Accent;
    private string? _groupName;

    /// <summary>Bindable dot/ring color while checked.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiRadioButton), null,
        defaultValueCreator: _ => SkUiColors.Accent,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).SetColor((Color)value));
    /// <summary>Bindable group name for app-level mutual exclusion (see remarks).</summary>
    public static readonly BindableProperty GroupNameProperty = BindableProperty.Create(nameof(GroupName), typeof(string), typeof(SkUiRadioButton), null,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).SetGroupName((string?)value));

    /// <summary>Ring/dot color while checked.</summary>
    public Color Color { get => _color; set => SetValue(ColorProperty, value); }
    /// <summary>Group name for app-level mutual exclusion; not enforced by this control.</summary>
    public string? GroupName { get => _groupName; set => SetValue(GroupNameProperty, value); }

    /// <summary>Sets the color without bindable write-back.</summary>
    public SkUiRadioButton SetColor(Color value) { ArgumentNullException.ThrowIfNull(value); _color = value; InvalidatePaint(); return this; }
    /// <summary>Sets the group name without bindable write-back.</summary>
    public SkUiRadioButton SetGroupName(string? value) { _groupName = value; return this; }

    /// <summary>Unlike the shared toggle base, a tap only selects (matching MAUI's RadioButton); it never unchecks.</summary>
    protected override SkUiCheckState NextCheckState() => SkUiCheckState.Checked;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureRadioButton(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override SkUiTransitionKind TransitionKind => SkUiTransitionKind.RadioButton;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiToggleDrawing.DrawRadioButton(canvas, (float)Width, (float)Height, IsRightToLeft, ToggleVisual, _color, IsEnabled);
}
