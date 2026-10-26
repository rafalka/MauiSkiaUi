using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A drawn checkbox, similar to MAUI's CheckBox.</summary>
public class SkUiCheckBox : SkUiToggleControl
{
    private Color _color = SkUiColors.Accent;
    private SkUiCommandSlot<SkUiCheckBox>? _command;

    /// <summary>Bindable <see cref="Command"/>.</summary>
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SkUiCheckBox), null,
        propertyChanged: (view, _, value) => ((SkUiCheckBox)view).CommandSlot.SetCommand((ICommand?)value));

    /// <summary>Bindable <see cref="CommandParameter"/>.</summary>
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SkUiCheckBox), null,
        propertyChanged: (view, _, value) => ((SkUiCheckBox)view).CommandSlot.SetParameter(value));

    /// <summary>
    /// Runs when <see cref="SkUiToggleControl.IsChecked"/> changes, from a tap or from code, after
    /// <see cref="SkUiToggleControl.CheckedChanged"/> (MAUI 10's <c>CheckBox.Command</c>). While it cannot execute, the
    /// check box looks and acts disabled, as MAUI's (whose <c>IsEnabled</c> then reads <c>false</c>; here it keeps its value).
    /// </summary>
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    /// <summary>Sets <see cref="Command"/>; command notifications use a weak target.</summary>
    public SkUiCheckBox SetCommand(ICommand? value) { Command = value; return this; }

    /// <summary>Sets <see cref="CommandParameter"/>.</summary>
    public SkUiCheckBox SetCommandParameter(object? value) { CommandParameter = value; return this; }

    private SkUiCommandSlot<SkUiCheckBox> CommandSlot => _command ??= new(this, static box => box.OnCommandStateChanged());

    /// <inheritdoc />
    protected override bool CanReceiveTap => _command is not { Command: not null, CanExecute: false };

    private void OnCommandStateChanged()
    {
        // A press held while the command stops executing ends without a toggle, as when IsEnabled turns false.
        if (!CanReceiveTap) CancelGestures();
        ChangeVisualState();
        InvalidatePaint();
        RevalidateFocus();
    }

    private protected override void RaiseCheckedChanged(bool isChecked)
    {
        base.RaiseCheckedChanged(isChecked);
        _command?.Execute();
    }

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
    /// <remarks>At least the look's <see cref="SkUiLook.DefaultCheckBoxMinimumWidth"/> wide unless the check box sets a <c>MinimumWidthRequest</c>.</remarks>
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureCheckBox(widthConstraint, heightConstraint, MinimumWidthRequest >= 0);

    /// <inheritdoc />
    protected override SkUiTransitionKind TransitionKind => SkUiTransitionKind.CheckBox;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiToggleDrawing.DrawCheckBox(canvas, (float)Width, (float)Height, IsRightToLeft, ToggleVisual, _color, IsVisualStateEnabled);

    /// <summary>The box: as large as the control's smaller side, at its start, from the top.</summary>
    protected internal override void AddSkeletonBones(SkUiSkeletonBuilder bones)
    {
        var size = Math.Min(bones.Width, bones.Height);
        bones.Add(SkUiSkeletonPartKind.CheckBox, SKRect.Create(IsRightToLeft ? bones.Width - size : 0, 0, size, size));
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.CheckBox;
    }
}
