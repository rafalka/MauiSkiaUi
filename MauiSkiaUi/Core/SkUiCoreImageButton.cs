using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core image with intrinsic taps, <see cref="ICommand"/>, and pressed/disabled tint overlay (Core analogue of
/// <c>SkUiImageButton</c>): the image sits inside <see cref="Padding"/>, is clipped to <see cref="CornerRadii"/>, and the
/// border is drawn inside the bounds.
/// </summary>
public class SkUiCoreImageButton : SkUiCoreImage, ISkUiBorderElement, SkUiImageButtonDrawing.IImage
{
    private SkUiCommandSlot<SkUiCoreImageButton>? _command;
    private SkUiChromeState _chrome;
    private Thickness _padding;
    private bool _isPressed;
    private SkUiPressAnimator? _press;
    private Color? _disabledFillColor;

    /// <summary>Creates an image button with a press/disabled tint overlay painter.</summary>
    public SkUiCoreImageButton() => SetPaintOverlay(PaintButtonOverlay);

    /// <summary>Raised on a completed tap (in addition to <see cref="Command"/>).</summary>
    public event EventHandler? Clicked;

    /// <summary>Raised when a press starts (MAUI order: <c>Pressed</c>, <c>Released</c>, then <c>Clicked</c> for a tap).</summary>
    public event EventHandler? Pressed;

    /// <summary>Raised when a press ends: released, cancelled (e.g. a scroll took over) or moved out.</summary>
    public event EventHandler? Released;

    /// <summary>Optional command executed on a completed tap.</summary>
    public ICommand? Command
    {
        get => _command?.Command;
        set => SetCommand(value);
    }

    /// <summary>Parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter
    {
        get => _command?.Parameter;
        set => SetCommandParameter(value);
    }

    /// <summary>Per-corner radii in DIPs: they clip the image, the press / disabled tint and the border.</summary>
    public CornerRadius CornerRadii
    {
        get => _chrome.Radii;
        set => SetCornerRadii(value);
    }

    /// <summary>Border color; the border is drawn inside the bounds, over the image.</summary>
    public Color BorderColor
    {
        get => _chrome.BorderColor;
        set => SetBorderColor(value);
    }

    /// <summary>Border width in DIPs (0: no border).</summary>
    public double BorderWidth
    {
        get => _chrome.BorderWidth;
        set => SetBorderWidth(value);
    }

    /// <summary>Space between the bounds and the image; adds to the intrinsic size.</summary>
    public Thickness Padding
    {
        get => _padding;
        set => SetPadding(value);
    }

    /// <summary>Whether an eligible pointer is currently pressed inside this button.</summary>
    public bool IsPressed => _isPressed;

    /// <inheritdoc cref="SkUiCoreImage.SetImage" />
    public new SkUiCoreImageButton SetImage(SKImage? image, bool ownsImage = true)
    {
        base.SetImage(image, ownsImage);
        return this;
    }

    /// <inheritdoc cref="SkUiCoreImage.SetAspect" />
    public new SkUiCoreImageButton SetAspect(Aspect value)
    {
        base.SetAspect(value);
        return this;
    }

    /// <summary>
    /// Sets the tap command. While it cannot execute, the button is disabled as with <see cref="SkUiCoreNode.IsEnabled"/>
    /// <c>false</c> (it takes taps without acting, draws disabled, is not focusable). CanExecuteChanged is listened to weakly,
    /// so long-lived commands do not retain this node.
    /// </summary>
    public SkUiCoreImageButton SetCommand(ICommand? value)
    {
        if (ReferenceEquals(Command, value)) return this;
        CommandSlot.SetCommand(value);
        OnPropertyChanged(nameof(Command));
        return this;
    }

    /// <summary>Sets the command parameter.</summary>
    public SkUiCoreImageButton SetCommandParameter(object? value)
    {
        if (Equals(CommandParameter, value)) return this;
        CommandSlot.SetParameter(value);
        OnPropertyChanged(nameof(CommandParameter));
        return this;
    }

    private SkUiCommandSlot<SkUiCoreImageButton> CommandSlot => _command ??= new(this, static button => button.OnTapStateChanged());

    /// <summary>Sets the per-corner radii in DIPs.</summary>
    public SkUiCoreImageButton SetCornerRadii(CornerRadius value)
    {
        var changed = value != _chrome.Radii;
        _chrome.SetRadii(value);
        if (!changed) return this;
        OnPropertyChanged(nameof(CornerRadii));
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets all four <see cref="CornerRadii"/> to <paramref name="value"/> DIPs.</summary>
    public SkUiCoreImageButton SetCornerRadius(double value)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The corner radius must be finite and non-negative.");
        return SetCornerRadii(new CornerRadius(value));
    }

    /// <summary>Sets the border color.</summary>
    public SkUiCoreImageButton SetBorderColor(Color value)
    {
        if (!_chrome.SetBorderColor(value)) return this;
        OnPropertyChanged(nameof(BorderColor));
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the border width in DIPs.</summary>
    public SkUiCoreImageButton SetBorderWidth(double value)
    {
        if (!_chrome.SetBorderWidth(value)) return this;
        OnPropertyChanged(nameof(BorderWidth));
        InvalidatePaint();
        return this;
    }

    /// <summary>The fill behind the image while the button is disabled (or its command cannot execute); see <see cref="SkUiImageButton.DisabledFillColor"/>.</summary>
    public Color? DisabledFillColor
    {
        get => _disabledFillColor;
        set => SetDisabledFillColor(value);
    }

    /// <summary>Sets <see cref="DisabledFillColor"/> (<c>null</c>: the background).</summary>
    public SkUiCoreImageButton SetDisabledFillColor(Color? value)
    {
        if (!SetProperty(ref _disabledFillColor, value, nameof(DisabledFillColor))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the padding around the image.</summary>
    public SkUiCoreImageButton SetPadding(Thickness value)
    {
        if (!SetProperty(ref _padding, value, nameof(Padding))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Convenience: assigns a <see cref="SkUiCoreCommand"/> that invokes <paramref name="execute"/>.</summary>
    public SkUiCoreImageButton SetClicked(Action? execute)
    {
        SetCommand(execute is null ? null : new SkUiCoreCommand(execute));
        return this;
    }

    /// <summary>Whether there is no command or it can execute (as of its last change).</summary>
    private bool CanExecuteCommand => _command is not { Command: not null, CanExecute: false };

    /// <inheritdoc />
    internal override CornerRadius PressEffectCornerRadii => _chrome.Radii;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiImageButtonDrawing.Measure(base.MeasureContent(widthConstraint, heightConstraint), _padding);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiImageButtonDrawing.PaintContent(canvas, this, (float)Frame.Width, (float)Frame.Height, _padding, ref _chrome);

    /// <summary>The <see cref="SkUiCoreNode.Background"/> (solid or gradient) fills the rounded bounds; disabled, <see cref="DisabledFillColor"/> when set.</summary>
    protected override void OnPaintBackground(SKCanvas canvas) =>
        _chrome.DrawFill(canvas, (float)Frame.Width, (float)Frame.Height, _chrome.Radii, ButtonFill);

    /// <summary>The fill behind the image (<see cref="SkUiButtonFill"/>, the rule of every button).</summary>
    private SkUiFill ButtonFill =>
        SkUiButtonFill.Resolve(IsInteractionEnabled, Background is { } background ? SkUiFill.From(background) : null, Colors.Transparent, false, _disabledFillColor);

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        _chrome.ReleaseClip();
    }

    /// <inheritdoc />
    internal override SKPath? CreateShadowOutline(float width, float height) =>
        PaintBackground is null ? _chrome.ShadowOutline(width, height, _chrome.Radii, _disabledFillColor is not null && !IsInteractionEnabled ? ButtonFill.ToPaint() : Background) : null;

    /// <summary>
    /// Draws press / disabled feedback (<see cref="SkUiLook.DrawPressOverlay"/>) and the border, registered as
    /// <see cref="SkUiCoreNode.PaintOverlay"/>. Subclasses may call or re-register this painter.
    /// </summary>
    protected void PaintButtonOverlay(SKCanvas canvas) =>
        SkUiImageButtonDrawing.PaintOverlay(canvas, (float)Frame.Width, (float)Frame.Height, _chrome, _press?.Visual ?? SkUiPressVisual.None, IsInteractionEnabled);

    void SkUiImageButtonDrawing.IImage.Paint(SKCanvas canvas, SKRect area) => PaintImage(canvas, area);

    /// <inheritdoc />
    internal override bool HasIntrinsicTap => true;

    /// <inheritdoc />
    internal override bool CanReceiveTap => CanExecuteCommand;

    /// <inheritdoc />
    internal override bool RunsTappedCommand => false;

    /// <inheritdoc />
    internal override void OnIntrinsicTap(SkUiTappedEventArgs args)
    {
        if (!CanExecuteCommand)
            return;
        Clicked?.Invoke(this, EventArgs.Empty);
        _command?.Execute();
    }

    /// <inheritdoc />
    internal override void OnGesturePressedChanged(bool pressed) => SetPressed(pressed);

    private void SetPressed(bool value)
    {
        if (!SetProperty(ref _isPressed, value, nameof(IsPressed))) return;
        (_press ??= new SkUiPressAnimator(this)).SetPressed(value, PressPosition);
        InvalidatePaint();
        (value ? Pressed : Released)?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.Button;
    }
}
