using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>Drawn checkbox (Core analogue of <c>SkUiCheckBox</c>).</summary>
public class SkUiCoreCheckBox : SkUiCoreToggleControl
{
    private Color _color = SkUiColors.Accent;
    private SkUiCommandSlot<SkUiCoreCheckBox>? _command;

    /// <summary>
    /// Runs when <see cref="SkUiCoreToggleControl.IsChecked"/> changes, from a tap or from code, after
    /// <see cref="SkUiCoreToggleControl.CheckedChanged"/> (MAUI 10's <c>CheckBox.Command</c>). While it cannot execute, the
    /// check box looks and acts disabled.
    /// </summary>
    public ICommand? Command
    {
        get => _command?.Command;
        set => SetCommand(value);
    }

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter
    {
        get => _command?.Parameter;
        set => SetCommandParameter(value);
    }

    /// <summary>Sets <see cref="Command"/>; command notifications use a weak target.</summary>
    public SkUiCoreCheckBox SetCommand(ICommand? value)
    {
        if (ReferenceEquals(Command, value)) return this;
        CommandSlot.SetCommand(value);
        OnPropertyChanged(nameof(Command));
        return this;
    }

    /// <summary>Sets <see cref="CommandParameter"/>.</summary>
    public SkUiCoreCheckBox SetCommandParameter(object? value)
    {
        if (Equals(CommandParameter, value)) return this;
        CommandSlot.SetParameter(value);
        OnPropertyChanged(nameof(CommandParameter));
        return this;
    }

    private SkUiCommandSlot<SkUiCoreCheckBox> CommandSlot => _command ??= new(this, static box => box.OnTapStateChanged());

    /// <inheritdoc />
    internal override bool CanReceiveTap => _command is not { Command: not null, CanExecute: false };

    private protected override void RaiseCheckedChanged(bool isChecked)
    {
        base.RaiseCheckedChanged(isChecked);
        _command?.Execute();
    }

    /// <summary>Fill/checkmark color while checked; unchecked draws a neutral outline.</summary>
    public Color Color
    {
        get => _color;
        set => SetColor(value);
    }

    /// <summary>Sets the checked fill/checkmark color.</summary>
    public SkUiCoreCheckBox SetColor(Color value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _color, value, nameof(Color))) return this;
        InvalidatePaint();
        return this;
    }

    /// <inheritdoc />
    /// <remarks>At least the look's <see cref="SkUiLook.DefaultCheckBoxMinimumWidth"/> wide unless the check box sets a <see cref="SkUiCoreNode.MinimumWidth"/> (0 too).</remarks>
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiLook.Current.MeasureCheckBox(widthConstraint, heightConstraint, HasMinimumWidth);

    /// <inheritdoc />
    protected override SkUiTransitionKind TransitionKind => SkUiTransitionKind.CheckBox;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiToggleDrawing.DrawCheckBox(canvas, (float)Frame.Width, (float)Frame.Height, IsRightToLeft, ToggleVisual, _color, IsInteractionEnabled);

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
