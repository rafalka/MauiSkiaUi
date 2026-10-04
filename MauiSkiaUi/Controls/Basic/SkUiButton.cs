using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn text button with intrinsic taps, commands, and press/disabled feedback. Like MAUI's Button, it can show an
/// image (<see cref="ImageSource"/>) beside its text, placed by <see cref="ContentLayout"/>.
/// </summary>
public class SkUiButton : SkUiLabel, SkUiButtonImageLayout.IText
{
    private ICommand? _command;
    private object? _commandParameter;
    private Color _fillColor = SkUiColors.Accent;
    private SkUiPressAnimator? _press;
    private ImageSource? _imageSource;
    private SkUiWeakListener<SkUiButton>? _imageSourceListener; // a shared image source must not keep the button alive
    private SkUiImageSlot? _image;
    private Button.ButtonContentLayout _contentLayout = DefaultContentLayout;

    /// <summary>MAUI Button's default <see cref="ContentLayout"/>: the image on the left, 10 DIPs from the text (before the bindable properties, which use it).</summary>
    internal static readonly Button.ButtonContentLayout DefaultContentLayout = new(Button.ButtonContentLayout.ImagePosition.Left, 10);

    /// <summary>Bindable command executed on a valid release.</summary>
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SkUiButton), null, propertyChanged: (view, _, value) => ((SkUiButton)view).OnCommandChanged((ICommand?)value));
    /// <summary>Bindable command argument.</summary>
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SkUiButton), null, propertyChanged: (view, _, value) => ((SkUiButton)view).OnCommandParameterChanged(value));
    /// <summary>Bindable button fill.</summary>
    public static readonly BindableProperty FillColorProperty = BindableProperty.Create(nameof(FillColor), typeof(Color), typeof(SkUiButton), null,
        defaultValueCreator: _ => SkUiColors.Accent,
        validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiButton)view).OnFillColorChanged((Color)value));
    /// <summary>Bindable <see cref="ImageSource"/>.</summary>
    public static readonly BindableProperty ImageSourceProperty = BindableProperty.Create(nameof(ImageSource), typeof(ImageSource), typeof(SkUiButton), null,
        propertyChanged: (view, _, value) => ((SkUiButton)view).OnImageSourceChanged((ImageSource?)value));
    /// <summary>Bindable <see cref="ContentLayout"/> (MAUI's default: the image on the left, 10 DIPs from the text).</summary>
    public static readonly BindableProperty ContentLayoutProperty = BindableProperty.Create(nameof(ContentLayout), typeof(Button.ButtonContentLayout), typeof(SkUiButton), DefaultContentLayout,
        validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiButton)view).OnContentLayoutChanged((Button.ButtonContentLayout)value));


    private readonly Action<SKCanvas> _buttonPainter;

    /// <summary>Creates a centered, padded button (its defaults are the <c>Default*</c> overrides below).</summary>
    public SkUiButton()
    {
        _buttonPainter = PaintButtonBackground;
        SetPaintBackground(_buttonPainter);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var size = ImageSize is { Width: > 0 } image
            ? SkUiButtonImageLayout.Measure(this, image, _contentLayout, Padding, widthConstraint, heightConstraint)
            : base.MeasureContent(widthConstraint, heightConstraint);
        // MinimumHeightRequest default is -1; only then resolve the active look's button minimum.
        if (MinimumHeightRequest < 0)
        {
            var min = SkUiLook.Current.DefaultButtonMinimumHeight;
            size = new Size(size.Width, Math.Max(size.Height, min));
        }
        return size;
    }

    /// <inheritdoc />
    protected override Color DefaultTextColor => Colors.White;
    /// <inheritdoc />
    protected override TextAlignment DefaultTextAlignment => TextAlignment.Center;
    /// <inheritdoc />
    protected override Thickness DefaultPadding => new(18, 12);
    /// <inheritdoc />
    protected override double DefaultCornerRadius => SkUiLook.Current.DefaultButtonCornerRadius;
    /// <inheritdoc />
    protected override LineBreakMode DefaultLineBreakMode => LineBreakMode.NoWrap;
    /// <inheritdoc />
    private protected override bool ClipsTextToInset => true;

    /// <summary>Raised for a valid enabled tap, even without a command.</summary>
    public event EventHandler? Clicked;
    /// <summary>Raised when a press starts (MAUI order: <c>Pressed</c>, <c>Released</c>, then <c>Clicked</c> for a tap).</summary>
    public event EventHandler? Pressed;
    /// <summary>Raised when a press ends: released, cancelled (e.g. a scroll took over) or moved out.</summary>
    public event EventHandler? Released;
    /// <summary>Command; CanExecute also controls tap eligibility and disabled appearance.</summary>
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    /// <summary>Command argument.</summary>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    /// <summary>Button background fill.</summary>
    public Color FillColor { get => (Color)GetValue(FillColorProperty); set => SetValue(FillColorProperty, value); }
    /// <summary>
    /// Image drawn beside the text (MAUI's <c>ImageSource</c>), loaded through the shared image loader and cache (files,
    /// <c>MauiImage</c> resources, <see cref="FontImageSource"/> glyphs, URIs, streams). It keeps its intrinsic size and
    /// is scaled down (never up) to fit inside <see cref="SkUiLabel.Padding"/>; it is not tinted.
    /// </summary>
    public ImageSource? ImageSource { get => (ImageSource?)GetValue(ImageSourceProperty); set => SetValue(ImageSourceProperty, value); }
    /// <summary>
    /// Where the image sits relative to the text, and the spacing between them (MAUI's type; XAML: <c>"Top, 10"</c>).
    /// Image and text are placed as one group by the text alignments (centered by default); the spacing applies only
    /// when there is text. <c>Left</c> is the start side: the right in right-to-left layouts.
    /// </summary>
    public Button.ButtonContentLayout ContentLayout { get => (Button.ButtonContentLayout)GetValue(ContentLayoutProperty); set => SetValue(ContentLayoutProperty, value); }

    /// <summary>Sets command; command notifications use a weak target.</summary>
    public SkUiButton SetCommand(ICommand? value)
    {
        Command = value;
        return this;
    }

    private void OnCommandChanged(ICommand? value)
    {
        if (_command == value) return;
        _command = value;
        // A long-lived command must not keep the button alive.
        (_commandListener ??= new(this, static (button, change) =>
        {
            if (change.Kind == SkUiChangeKind.CanExecute) button.UpdateState();
        })).Listen(value);
        UpdateState();
    }
    private SkUiWeakListener<SkUiButton>? _commandListener;
    /// <summary>Sets command argument (same as the property setter).</summary>
    public SkUiButton SetCommandParameter(object? value) { CommandParameter = value; return this; }
    private void OnCommandParameterChanged(object? value) { _commandParameter = value; UpdateState(); }
    /// <summary>Sets all four corner radii (same as the property setter).</summary>
    public new SkUiButton SetCornerRadius(int value) { base.SetCornerRadius(value); return this; }
    /// <summary>Sets the per-corner radii (same as the property setter).</summary>
    public new SkUiButton SetCornerRadii(CornerRadius value) { base.SetCornerRadii(value); return this; }
    /// <summary>Sets fill (same as the property setter).</summary>
    public SkUiButton SetFillColor(Color value) { ArgumentNullException.ThrowIfNull(value); FillColor = value; return this; }
    private void OnFillColorChanged(Color value) { _fillColor = value; InvalidatePaint(); }
    /// <summary>Sets the image (same as the property setter).</summary>
    public SkUiButton SetImageSource(ImageSource? value) { ImageSource = value; return this; }

    private void OnImageSourceChanged(ImageSource? value)
    {
        if (ReferenceEquals(_imageSource, value)) return;
        _imageSource = value;
        (_imageSourceListener ??= new(this, static (button, change) =>
        {
            if (SkUiMauiImageSources.AffectsImage(change.PropertyName))
                button.LoadImage();
        })).Listen(value);
        LoadImage();
    }

    private void LoadImage()
    {
        if (_imageSource is null && _image is null) return;
        (_image ??= new SkUiImageSlot(this, InvalidateMeasureOverride)).Load(SkUiMauiImageSources.Convert(_imageSource), default);
    }

    /// <summary>Sets the content layout (same as the property setter).</summary>
    public SkUiButton SetContentLayout(Button.ButtonContentLayout value) { ArgumentNullException.ThrowIfNull(value); ContentLayout = value; return this; }
    private void OnContentLayoutChanged(Button.ButtonContentLayout value)
    {
        _contentLayout = value;
        if (ImageSize.Width > 0) InvalidateMeasureOverride();
    }

    /// <summary>The loaded image's size in DIPs (zero without one or while it loads).</summary>
    internal Size ImageSize => _image?.DisplayedSize ?? Size.Zero;

    /// <summary>The shown image's cache entry (tests: leases).</summary>
    internal SkUiCachedImage? CachedImage => _image?.Entry;

    /// <summary>The load of the current <see cref="ImageSource"/> (completed when idle; tests).</summary>
    internal Task ImageLoadingTask => _image?.LoadingTask ?? Task.CompletedTask;

    /// <summary>Where the image and the text sit in the arranged button.</summary>
    private SkUiButtonImageLayout.Placement ImagePlacement => SkUiButtonImageLayout.Arrange(this, ImageSize, _contentLayout, Padding, Width, Height,
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

    /// <summary>Sets border color (same as the property setter).</summary>
    public new SkUiButton SetBorderColor(Color value) { base.SetBorderColor(value); return this; }
    /// <summary>Sets border width (same as the property setter).</summary>
    public new SkUiButton SetBorderWidth(double value) { base.SetBorderWidth(value); return this; }
    /// <inheritdoc />
    protected override bool HandlesTap => true;
    /// <inheritdoc />
    protected override bool CanReceiveTap => _command?.CanExecute(_commandParameter) ?? true;
    /// <inheritdoc />
    protected override void OnPressedChanged()
    {
        (_press ??= new SkUiPressAnimator(this)).SetPressed(IsPressed, PressPosition);
        InvalidatePaint();
        (IsPressed ? Pressed : Released)?.Invoke(this, EventArgs.Empty);
    }
    private void UpdateState()
    {
        ChangeVisualState();
        InvalidatePaint();
        RevalidateFocus();
    }
    /// <summary>MAUI's button states: <c>Pressed</c> while an enabled press is held, otherwise the common states.</summary>
    protected override void ChangeVisualState()
    {
        if (IsVisualStateEnabled && IsPressed)
            VisualStateManager.GoToState(this, PressedVisualState);
        else
            base.ChangeVisualState();
    }

    /// <summary>The name of the visual state while pressed (MAUI's <c>ButtonElement.PressedVisualState</c>).</summary>
    public const string PressedVisualState = "Pressed";

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled)) UpdateState();
    }
    /// <summary>
    /// Raises the shared Tapped event and Clicked, then executes Command. TappedCommand (inherited from SkUiView)
    /// is intentionally not executed here to avoid running two commands from a single tap; use Command instead.
    /// </summary>
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        RaiseTapped(args);
        Clicked?.Invoke(this, EventArgs.Empty);
        if (_command?.CanExecute(_commandParameter) == true) _command.Execute(_commandParameter);
    }
    /// <summary>
    /// Draws the rounded fill, border and press feedback (<see cref="SkUiLook.DrawButton"/>) registered as
    /// <see cref="SkUiView.PaintBackground"/>. Subclasses may call or re-register this painter.
    /// </summary>
    protected void PaintButtonBackground(SKCanvas canvas)
    {
        var enabled = IsEnabled && CanReceiveTap;
        var fill = ButtonFill(enabled);
        SkUiLook.Current.DrawButton(canvas, new SkUiButtonPaint(new SKRect(0, 0, (float)Width, (float)Height), CornerRadii,
            fill.Color, ToSkColor(BorderColor), (float)BorderWidth, _press?.Visual ?? SkUiPressVisual.None, enabled)
        {
            FillPaint = fill.Gradient
        });
    }

    /// <summary>The fill: a set <see cref="VisualElement.Background"/> (solid or gradient), else <see cref="FillColor"/>; disabled, the disabled color.</summary>
    private SkUiFill ButtonFill(bool enabled) => enabled ? ResolveBackgroundFill() ?? SkUiFill.From(_fillColor) : SkUiFill.From(SkUiColors.Disabled);

    /// <inheritdoc />
    internal override SKPath? CreateShadowOutline(float width, float height) =>
        ReferenceEquals(PaintBackground, _buttonPainter) ? ChromeShadowOutline(width, height, CornerRadii, ButtonFill(IsEnabled && CanReceiveTap).ToPaint()) : null;

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.Button;
    }
}
