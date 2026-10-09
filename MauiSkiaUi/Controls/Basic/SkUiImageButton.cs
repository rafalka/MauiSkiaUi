using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn image with intrinsic taps, commands, and pressed/disabled tint, similar to MAUI's ImageButton: the image sits
/// inside <see cref="Padding"/>, is clipped to <see cref="CornerRadii"/> (or the uniform, MAUI-compatible <see cref="CornerRadius"/>), and the border is drawn
/// inside the bounds.
/// </summary>
public class SkUiImageButton : SkUiImage, SkUiImageButtonDrawing.IImage
{
    private ICommand? _command;
    private object? _commandParameter;
    private SkUiChromeState _chrome;
    private bool _syncingCornerRadius;
    private Thickness _padding;

    /// <summary>Bindable command executed on a valid release.</summary>
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SkUiImageButton), null,
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).OnCommandChanged((ICommand?)value));
    /// <summary>Bindable command argument.</summary>
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SkUiImageButton), null,
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).OnCommandParameterChanged(value));
    /// <summary>Bindable per-corner radii (0: square).</summary>
    public static readonly BindableProperty CornerRadiiProperty = BindableProperty.Create(nameof(CornerRadii), typeof(CornerRadius), typeof(SkUiImageButton), default(CornerRadius),
        validateValue: SkUiValidate.CornerRadii,
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).OnCornerRadiiChanged((CornerRadius)value));
    /// <summary>Bindable uniform corner radius (MAUI ImageButton's <c>int</c> <c>CornerRadius</c>): sets all four <see cref="CornerRadii"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(int), typeof(SkUiImageButton), 0,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).OnCornerRadiusChanged((int)value));
    /// <summary>Bindable <see cref="BorderColor"/>.</summary>
    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(SkUiImageButton), Colors.Transparent,
        validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).OnBorderColorChanged((Color)value));
    /// <summary>Bindable <see cref="BorderWidth"/>.</summary>
    public static readonly BindableProperty BorderWidthProperty = BindableProperty.Create(nameof(BorderWidth), typeof(double), typeof(SkUiImageButton), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).OnBorderWidthChanged((double)value));
    /// <summary>Bindable <see cref="Padding"/>.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(SkUiImageButton), default(Thickness),
        propertyChanged: (view, _, value) => ((SkUiImageButton)view).OnPaddingChanged((Thickness)value));

    /// <summary>Raised for a valid enabled tap, even without a command.</summary>
    public event EventHandler? Clicked;
    /// <summary>Raised when a press starts (MAUI order: <c>Pressed</c>, <c>Released</c>, then <c>Clicked</c> for a tap).</summary>
    public event EventHandler? Pressed;
    /// <summary>Raised when a press ends: released, cancelled (e.g. a scroll took over) or moved out.</summary>
    public event EventHandler? Released;
    /// <summary>Command; CanExecute also controls tap eligibility and the disabled tint.</summary>
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    /// <summary>Command argument.</summary>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    /// <summary>Per-corner radii in DIPs: they clip the image, the press / disabled tint and the border.</summary>
    public CornerRadius CornerRadii { get => (CornerRadius)GetValue(CornerRadiiProperty); set => SetValue(CornerRadiiProperty, value); }
    /// <summary>
    /// Uniform view of <see cref="CornerRadii"/>, an <c>int</c> as MAUI ImageButton's <c>CornerRadius</c>: setting it sets all
    /// four corners, and it reads the top-left radius, rounded (whichever of the two is set last wins); use
    /// <see cref="CornerRadii"/> for fractional or per-corner radii.
    /// </summary>
    public int CornerRadius { get => (int)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    /// <summary>Border color; the border is drawn inside the bounds, over the image.</summary>
    public Color BorderColor { get => (Color)GetValue(BorderColorProperty); set => SetValue(BorderColorProperty, value); }
    /// <summary>Border width in DIPs (0: no border).</summary>
    public double BorderWidth { get => (double)GetValue(BorderWidthProperty); set => SetValue(BorderWidthProperty, value); }
    /// <summary>Space between the bounds and the image; adds to the intrinsic size.</summary>
    public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <summary>Sets command; command notifications use a weak target.</summary>
    public SkUiImageButton SetCommand(ICommand? value)
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
    private SkUiWeakListener<SkUiImageButton>? _commandListener;
    /// <summary>Sets command argument (same as the property setter).</summary>
    public SkUiImageButton SetCommandParameter(object? value) { CommandParameter = value; return this; }
    private void OnCommandParameterChanged(object? value) { _commandParameter = value; UpdateState(); }
    /// <summary>Sets the per-corner radii (same as the property setter).</summary>
    public SkUiImageButton SetCornerRadii(CornerRadius value) { SkUiCornerRadii.Validate(value, nameof(value)); CornerRadii = value; return this; }
    private void OnCornerRadiiChanged(CornerRadius value)
    {
        if (!_chrome.SetRadii(value)) return;
        SyncCornerRadiusProperty();
        InvalidatePaint();
    }
    /// <summary>Sets all four corner radii to <paramref name="value"/> (same as the property setter).</summary>
    public SkUiImageButton SetCornerRadius(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); CornerRadius = value; return this; }
    private void OnCornerRadiusChanged(int value) { if (!_syncingCornerRadius) CornerRadii = new CornerRadius(value); }

    /// <summary>Keeps the <see cref="CornerRadius"/> store at the top-left radius, rounded, so its bindings see <see cref="CornerRadii"/> changes.</summary>
    private void SyncCornerRadiusProperty()
    {
        var uniform = (int)Math.Round(_chrome.Radii.TopLeft);
        if ((int)GetValue(CornerRadiusProperty) == uniform) return;
        _syncingCornerRadius = true; // the store follows the radii; it must not set all four corners back
        try { SetValue(CornerRadiusProperty, uniform); }
        finally { _syncingCornerRadius = false; }
    }
    /// <summary>Sets the border color (same as the property setter).</summary>
    public SkUiImageButton SetBorderColor(Color value) { ArgumentNullException.ThrowIfNull(value); BorderColor = value; return this; }
    private void OnBorderColorChanged(Color value) { if (_chrome.SetBorderColor(value)) InvalidatePaint(); }
    /// <summary>Sets the border width (same as the property setter).</summary>
    public SkUiImageButton SetBorderWidth(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); BorderWidth = value; return this; }
    private void OnBorderWidthChanged(double value) { if (_chrome.SetBorderWidth(value)) InvalidatePaint(); }
    /// <summary>Sets the padding (same as the property setter).</summary>
    public SkUiImageButton SetPadding(Thickness value) { Padding = value; return this; }
    private void OnPaddingChanged(Thickness value) { if (_padding == value) return; _padding = value; InvalidateMeasureOverride(); }

    /// <summary>Creates an image button with a press/disabled tint overlay painter.</summary>
    public SkUiImageButton() => SetPaintOverlay(PaintButtonOverlay);

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

    private SkUiPressAnimator? _press;

    private void UpdateState()
    {
        ChangeVisualState();
        InvalidatePaint();
        RevalidateFocus();
    }

    /// <summary>MAUI's image button states: <c>Pressed</c> while an enabled press is held, otherwise the common states.</summary>
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
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        RaiseTapped(args);
        Clicked?.Invoke(this, EventArgs.Empty);
        if (_command?.CanExecute(_commandParameter) == true) _command.Execute(_commandParameter);
    }

    /// <inheritdoc />
    internal override CornerRadius PressEffectCornerRadii => _chrome.Radii;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        SkUiImageButtonDrawing.Measure(base.MeasureContent(widthConstraint, heightConstraint), _padding);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) =>
        SkUiImageButtonDrawing.PaintContent(canvas, this, (float)Width, (float)Height, _padding, ref _chrome);

    /// <summary>The background (solid or gradient) fills the rounded bounds.</summary>
    protected override void OnPaintBackground(SKCanvas canvas)
    {
        if (SkUiCornerRadii.HasAny(_chrome.Radii))
            _chrome.DrawFill(canvas, (float)Width, (float)Height, _chrome.Radii, ResolveBackgroundFill() ?? default);
        else
            base.OnPaintBackground(canvas);
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        _chrome.ReleaseClip();
    }

    /// <inheritdoc />
    internal override SKPath? CreateShadowOutline(float width, float height) =>
        PaintBackground is null ? _chrome.ShadowOutline(width, height, _chrome.Radii, ResolveBackgroundPaint()) : null;

    /// <summary>
    /// Draws press / disabled feedback (<see cref="SkUiLook.DrawPressOverlay"/>) and the border, registered as
    /// <see cref="SkUiView.PaintOverlay"/>. Subclasses may call or re-register this painter.
    /// </summary>
    protected void PaintButtonOverlay(SKCanvas canvas) =>
        SkUiImageButtonDrawing.PaintOverlay(canvas, (float)Width, (float)Height, _chrome, _press?.Visual ?? SkUiPressVisual.None, IsEnabled && CanReceiveTap);

    void SkUiImageButtonDrawing.IImage.Paint(SKCanvas canvas, SKRect area) => PaintImage(canvas, area);

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.Button;
    }
}
