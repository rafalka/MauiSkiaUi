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
    private SkUiPressAnimator? _press;
    private SkUiCommandSlot<SkUiCoreButton>? _command;
    private Color? _disabledFillColor;

    /// <summary>Creates a centered button in the color scheme's button colors (white on the accent by default) whose text does not wrap (as MAUI's Button).</summary>
    public SkUiCoreButton()
    {
        SetLineBreakMode(Microsoft.Maui.LineBreakMode.NoWrap);
        SetTextColor(SkUiColors.ButtonForeground);
        SetHorizontalTextAlignment(TextAlignment.Center);
        SetVerticalTextAlignment(TextAlignment.Center);
        InitFillColor(SkUiColors.ButtonBackground);
        SetBorderColor(SkUiColors.ButtonBorder);
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
        get => _command?.Command;
        set => SetCommand(value);
    }

    /// <summary>Parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter
    {
        get => _command?.Parameter;
        set => SetCommandParameter(value);
    }

    /// <summary>The fill while the button is disabled (or its command cannot execute); see <see cref="SkUiButton.DisabledFillColor"/>.</summary>
    public Color? DisabledFillColor
    {
        get => _disabledFillColor;
        set => SetDisabledFillColor(value);
    }

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

    /// <summary>The shown image's cache entry (tests: leases).</summary>
    internal SkUiCachedImage? CachedImage => _image?.Entry;

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
    protected override Thickness DefaultPadding => SkUiLook.Current.DefaultButtonPadding;
    /// <inheritdoc />
    protected override double DefaultBorderWidth => SkUiLook.Current.DefaultButtonBorderWidth;

    /// <inheritdoc />
    private protected override bool ClipsTextToInset => true;

    /// <inheritdoc />
    private protected override bool HasSkeletonPlaceholder => false;

    /// <summary>A button's bone is its chrome's shape, with or without a fill.</summary>
    protected internal override void AddSkeletonBones(SkUiSkeletonBuilder bones) => bones.AddShape(bones.Bounds, PressEffectCornerRadii);

    /// <summary>Sets fill color.</summary>
    public new SkUiCoreButton SetFillColor(Color value) { base.SetFillColor(value); return this; }

    /// <summary>Sets <see cref="DisabledFillColor"/> (<c>null</c>: the default rule).</summary>
    public SkUiCoreButton SetDisabledFillColor(Color? value)
    {
        if (!SetProperty(ref _disabledFillColor, value, nameof(DisabledFillColor))) return this;
        InvalidatePaint();
        return this;
    }

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

    /// <summary>
    /// Sets the tap command. While it cannot execute, the button is disabled as with <see cref="SkUiCoreNode.IsEnabled"/>
    /// <c>false</c> (it takes taps without acting, draws disabled, is not focusable). CanExecuteChanged is listened to weakly,
    /// so long-lived commands do not retain this node.
    /// </summary>
    public SkUiCoreButton SetCommand(ICommand? value)
    {
        if (ReferenceEquals(Command, value)) return this;
        CommandSlot.SetCommand(value);
        OnPropertyChanged(nameof(Command));
        return this;
    }

    /// <summary>Sets the command parameter.</summary>
    public SkUiCoreButton SetCommandParameter(object? value)
    {
        if (Equals(CommandParameter, value)) return this;
        CommandSlot.SetParameter(value);
        OnPropertyChanged(nameof(CommandParameter));
        return this;
    }

    private SkUiCommandSlot<SkUiCoreButton> CommandSlot => _command ??= new(this, static button => button.OnTapStateChanged());

    /// <summary>
    /// Convenience: assigns a <see cref="SkUiCoreCommand"/> that invokes <paramref name="execute"/>.
    /// Prefer <see cref="SetCommand"/> when the caller already owns an <see cref="ICommand"/>.
    /// </summary>
    public SkUiCoreButton SetClicked(Action? execute)
    {
        SetCommand(execute is null ? null : new SkUiCoreCommand(execute));
        return this;
    }

    /// <summary>Whether there is no command or it can execute (as of its last change).</summary>
    private bool CanExecuteCommand => _command is not { Command: not null, CanExecute: false };

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var size = ImageSize is { Width: > 0 } image
            ? SkUiButtonImageLayout.Measure(this, image, _contentLayout, Padding, widthConstraint, heightConstraint)
            : base.MeasureContent(widthConstraint, heightConstraint);
        if (!_minimumHeightExplicit)
            size = new Size(size.Width, Math.Max(size.Height, DefaultMinimumHeight));
        return size;
    }

    /// <summary>
    /// The minimum height used until a minimum height is set: the look's <see cref="SkUiLook.DefaultButtonMinimumHeight"/>.
    /// A subclass overrides it for its own size of button, as it does <see cref="DefaultCornerRadius"/> and
    /// <see cref="DefaultPadding"/>.
    /// </summary>
    protected virtual double DefaultMinimumHeight => SkUiLook.Current.DefaultButtonMinimumHeight;

    /// <summary>
    /// Draws the rounded fill, border and press feedback (<see cref="SkUiLook.DrawButton"/>) registered as
    /// <see cref="SkUiCoreNode.PaintBackground"/>. Subclasses may call or re-register this painter.
    /// </summary>
    protected void PaintButtonBackground(SKCanvas canvas)
    {
        var enabled = IsInteractionEnabled;
        var fill = ButtonFill(enabled);
        SkUiLook.Current.DrawButton(canvas, new SkUiButtonPaint(new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height), CornerRadii,
            fill.Color, ToSkColor(BorderColor), (float)BorderWidth,
            _press?.Visual ?? SkUiPressVisual.None, enabled)
        {
            FillPaint = fill.Gradient
        });
    }

    /// <summary>The fill: a set <see cref="SkUiCoreNode.Background"/> (solid or gradient), else <see cref="SkUiCoreLabel.FillColor"/>; disabled, see <see cref="DisabledFillColor"/>.</summary>
    private SkUiFill ButtonFill(bool enabled) =>
        SkUiButtonFill.Resolve(enabled, Background is { } background ? SkUiFill.From(background) : null, FillColor, !IsFillColorSet, _disabledFillColor);

    /// <inheritdoc />
    private protected override double ContentOpacity =>
        !(IsInteractionEnabled) && SkUiButtonFill.DimsContent(ButtonFill(false), _disabledFillColor) ? SkUiLook.Current.DisabledButtonContentOpacity : 1;

    /// <inheritdoc />
    internal override SKPath? CreateShadowOutline(float width, float height) =>
        ReferenceEquals(PaintBackground, _buttonPainter) ? ChromeShadowOutline(width, height, CornerRadii, ButtonFill(IsInteractionEnabled).ToPaint()) : null;

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
    protected override void OnPressedChanged()
    {
        (_press ??= new SkUiPressAnimator(this)).SetPressed(IsPressed, PressPosition);
        InvalidatePaint();
        (IsPressed ? Pressed : Released)?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.Button;
    }
}
