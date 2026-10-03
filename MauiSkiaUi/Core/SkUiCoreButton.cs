using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Minimal Core button: rounded fill, single-line label, intrinsic tap handling, and optional
/// <see cref="ICommand"/>. Like MAUI's Button, it can show an image (<see cref="ImageSource"/>) beside its text, placed
/// by <see cref="ContentLayout"/>.
/// </summary>
public class SkUiCoreButton : SkUiCoreLabel, SkUiButtonImageLayout.IText
{
    private SkUiImageSource? _imageSource;
    private SkUiImageSlot? _image;
    private Button.ButtonContentLayout _contentLayout = SkUiButton.DefaultContentLayout;
    private bool _minimumHeightExplicit;
    private bool _isPressed;
    private SkUiPressAnimator? _press;
    private ICommand? _command;
    private object? _commandParameter;
    private SkUiWeakListener<SkUiCoreButton>? _commandListener;

    /// <summary>Creates a centered white-on-accent button whose text does not wrap (as MAUI's Button).</summary>
    public SkUiCoreButton()
    {
        SetLineBreakMode(Microsoft.Maui.LineBreakMode.NoWrap);
        SetTextColor(Colors.White);
        SetPadding(new Thickness(6));
        SetHorizontalTextAlignment(TextAlignment.Center);
        SetVerticalTextAlignment(TextAlignment.Center);
        SetFillColor(SkUiColors.Accent);
        _buttonPainter = PaintButtonBackground;
        SetPaintBackground(_buttonPainter);
    }

    private readonly Action<SKCanvas> _buttonPainter;

    /// <summary>Raised on a completed tap inside the button bounds (in addition to <see cref="Command"/>).</summary>
    public event EventHandler? Clicked;

    /// <summary>Raised when a press starts (MAUI order: <c>Pressed</c>, <c>Released</c>, then <c>Clicked</c> for a tap).</summary>
    public event EventHandler? Pressed;

    /// <summary>Raised when a press ends: released, cancelled (e.g. a scroll took over) or moved out.</summary>
    public event EventHandler? Released;

    /// <summary>Optional command executed on a completed tap.</summary>
    public ICommand? Command
    {
        get => _command;
        set => SetCommand(value);
    }

    /// <summary>Parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter
    {
        get => _commandParameter;
        set => SetCommandParameter(value);
    }

    /// <summary>Whether an eligible pointer is currently pressed inside this button.</summary>
    public bool IsPressed => _isPressed;

    /// <summary>
    /// Image drawn beside the text (see <see cref="SkUiButton.ImageSource"/>): intrinsic size, scaled down (never up) to
    /// fit inside <see cref="SkUiCoreLabel.Padding"/>, not tinted.
    /// </summary>
    public SkUiImageSource? ImageSource
    {
        get => _imageSource;
        set => SetImageSource(value);
    }

    /// <summary>
    /// Where the image sits relative to the text, and the spacing between them (MAUI's type; default: left, 10 DIPs). See
    /// <see cref="SkUiButton.ContentLayout"/>.
    /// </summary>
    public Button.ButtonContentLayout ContentLayout
    {
        get => _contentLayout;
        set => SetContentLayout(value);
    }

    /// <summary>Sets <see cref="ImageSource"/> (<c>null</c>: no image).</summary>
    public SkUiCoreButton SetImageSource(SkUiImageSource? value)
    {
        if (!SetProperty(ref _imageSource, value, nameof(ImageSource))) return this;
        if (value is not null || _image is not null)
            (_image ??= new SkUiImageSlot(this, InvalidateMeasure)).Load(value, default);
        return this;
    }

    /// <summary>Sets <see cref="ContentLayout"/>.</summary>
    public SkUiCoreButton SetContentLayout(Button.ButtonContentLayout value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!SetProperty(ref _contentLayout, value, nameof(ContentLayout))) return this;
        if (ImageSize.Width > 0) InvalidateMeasure();
        return this;
    }

    /// <summary>The loaded image's size in DIPs (zero without one or while it loads).</summary>
    internal Size ImageSize => _image?.DisplayedSize ?? Size.Zero;

    /// <summary>The load of the current <see cref="ImageSource"/> (completed when idle; tests).</summary>
    internal Task ImageLoadingTask => _image?.LoadingTask ?? Task.CompletedTask;

    /// <summary>Where the image and the text sit in the arranged button.</summary>
    private SkUiButtonImageLayout.Placement ImagePlacement => SkUiButtonImageLayout.Arrange(this, ImageSize, _contentLayout, Padding, Frame.Width, Frame.Height,
        HorizontalTextAlignment, VerticalTextAlignment, IsRightToLeft);

    /// <inheritdoc />
    private protected override Thickness TextInset => ImageSize.Width > 0 ? ImagePlacement.TextInset : base.TextInset;

    /// <inheritdoc />
    private protected override bool HasIcon => ImageSize.Width > 0;

    /// <inheritdoc />
    private protected override void PaintIcon(SKCanvas canvas)
    {
        if (_image is null) return;
        var area = ImagePlacement.Image;
        if (area.Width > 0 && area.Height > 0)
            _image.Paint(canvas, new SKRect((float)area.Left, (float)area.Top, (float)area.Right, (float)area.Bottom), Aspect.AspectFit);
    }

    bool SkUiButtonImageLayout.IText.HasText => HasText;

    Size SkUiButtonImageLayout.IText.MeasureText(double widthConstraint) => MeasureText(widthConstraint);

    /// <inheritdoc />
    protected override double DefaultCornerRadius => SkUiLook.Current.DefaultButtonCornerRadius;

    /// <inheritdoc />
    private protected override bool ClipsTextToInset => true;

    /// <summary>Sets fill color.</summary>
    public new SkUiCoreButton SetFillColor(Color value) { base.SetFillColor(value); return this; }

    /// <summary>Sets border color.</summary>
    public new SkUiCoreButton SetBorderColor(Color value) { base.SetBorderColor(value); return this; }

    /// <summary>Sets border width in DIPs.</summary>
    public new SkUiCoreButton SetBorderWidth(double value) { base.SetBorderWidth(value); return this; }

    /// <summary>Sets all four corner radii in DIPs (app-explicit, so look swaps do not replace them).</summary>
    public new SkUiCoreButton SetCornerRadius(double value) { base.SetCornerRadius(value); return this; }

    /// <summary>Sets the per-corner radii in DIPs (app-explicit, so look swaps do not replace them).</summary>
    public new SkUiCoreButton SetCornerRadii(CornerRadius value) { base.SetCornerRadii(value); return this; }

    /// <inheritdoc />
    public override SkUiCoreNode SetMinimumHeight(double value)
    {
        _minimumHeightExplicit = true;
        return base.SetMinimumHeight(value);
    }

    /// <summary>Sets the tap command. CanExecuteChanged is listened to weakly, so long-lived commands do not retain this node.</summary>
    public SkUiCoreButton SetCommand(ICommand? value)
    {
        if (ReferenceEquals(_command, value)) return this;
        if (!SetProperty(ref _command, value, nameof(Command))) return this;
        // A long-lived command must not keep the node alive.
        (_commandListener ??= new(this, static (button, change) =>
        {
            if (change.Kind == SkUiChangeKind.CanExecute) button.InvalidatePaint();
        })).Listen(value);
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the command parameter.</summary>
    public SkUiCoreButton SetCommandParameter(object? value)
    {
        if (!SetProperty(ref _commandParameter, value, nameof(CommandParameter))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>
    /// Convenience: assigns a <see cref="SkUiCoreCommand"/> that invokes <paramref name="execute"/>.
    /// Prefer <see cref="SetCommand"/> when the caller already owns an <see cref="ICommand"/>.
    /// </summary>
    public SkUiCoreButton SetClicked(Action? execute)
    {
        SetCommand(execute is null ? null : new SkUiCoreCommand(execute));
        return this;
    }

    private bool CanExecuteCommand => _command?.CanExecute(_commandParameter) ?? true;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var size = ImageSize is { Width: > 0 } image
            ? SkUiButtonImageLayout.Measure(this, image, _contentLayout, Padding, widthConstraint, heightConstraint)
            : base.MeasureContent(widthConstraint, heightConstraint);
        if (!_minimumHeightExplicit)
        {
            var min = SkUiLook.Current.DefaultButtonMinimumHeight;
            size = new Size(size.Width, Math.Max(size.Height, min));
        }
        return size;
    }

    /// <summary>
    /// Draws the rounded fill, border and press feedback (<see cref="SkUiLook.DrawButton"/>) registered as
    /// <see cref="SkUiCoreNode.PaintBackground"/>. Subclasses may call or re-register this painter.
    /// </summary>
    protected void PaintButtonBackground(SKCanvas canvas)
    {
        var enabled = CanExecuteCommand;
        var fill = ButtonFill(enabled);
        SkUiLook.Current.DrawButton(canvas, new SkUiButtonPaint(new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height), CornerRadii,
            fill.Color, ToSkColor(BorderColor), (float)BorderWidth,
            _press?.Visual ?? SkUiPressVisual.None, enabled)
        {
            FillPaint = fill.Gradient
        });
    }

    /// <summary>The fill: a set <see cref="SkUiCoreNode.Background"/> (solid or gradient), else <see cref="SkUiCoreLabel.FillColor"/>; disabled, the disabled color.</summary>
    private SkUiFill ButtonFill(bool enabled) =>
        !enabled ? SkUiFill.From(SkUiColors.Disabled) : Background is { } background ? SkUiFill.From(background) : SkUiFill.From(FillColor);

    /// <inheritdoc />
    internal override SKPath? CreateShadowOutline(float width, float height) =>
        ReferenceEquals(PaintBackground, _buttonPainter) ? ChromeShadowOutline(width, height, CornerRadii, ButtonFill(CanExecuteCommand).ToPaint()) : null;

    /// <inheritdoc />
    internal override bool HasIntrinsicTap => CanExecuteCommand;

    /// <inheritdoc />
    internal override void OnIntrinsicTap(SkUiTappedEventArgs args)
    {
        if (!CanExecuteCommand)
            return;
        Clicked?.Invoke(this, EventArgs.Empty);
        if (_command?.CanExecute(_commandParameter) == true)
            _command.Execute(_commandParameter);
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
}
