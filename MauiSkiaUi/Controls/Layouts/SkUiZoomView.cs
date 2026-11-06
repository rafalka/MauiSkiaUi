namespace MauiSkiaUi;

/// <summary>
/// Pinch, pan, double tap and wheel zoom of its content: the content's composite-time scale and offset change, so nothing re-records. It owns the content's
/// <c>Scale</c>, <c>TranslationX</c> and <c>TranslationY</c>. At scale 1 it takes no drag, so it can sit in a scroll view (with <see cref="IsWheelZoomEnabled"/> off, so
/// the wheel scrolls); a double tap zooms in at the tap and back out. Same engine as <see cref="Core.SkUiCoreZoomView"/>.
/// </summary>
public class SkUiZoomView : SkUiContentView, ISkUiZoomTarget, ISkUiWheelHandler
{
    /// <summary>Bindable <see cref="MinimumZoomScale"/>.</summary>
    public static readonly BindableProperty MinimumZoomScaleProperty = BindableProperty.Create(nameof(MinimumZoomScale), typeof(double), typeof(SkUiZoomView), 1.0,
        validateValue: SkUiValidate.FinitePositive,
        propertyChanged: static (view, _, value) =>
        {
            var zoom = ((SkUiZoomView)view).Zoom;
            zoom.MinimumScale = (double)value;
            zoom.Reclamp();
        });

    /// <summary>Bindable <see cref="MaximumZoomScale"/>.</summary>
    public static readonly BindableProperty MaximumZoomScaleProperty = BindableProperty.Create(nameof(MaximumZoomScale), typeof(double), typeof(SkUiZoomView), 5.0,
        validateValue: SkUiValidate.FinitePositive,
        propertyChanged: static (view, _, value) =>
        {
            var zoom = ((SkUiZoomView)view).Zoom;
            zoom.MaximumScale = (double)value;
            zoom.Reclamp();
        });

    /// <summary>Bindable <see cref="DoubleTapZoomScale"/>.</summary>
    public static readonly BindableProperty DoubleTapZoomScaleProperty = BindableProperty.Create(nameof(DoubleTapZoomScale), typeof(double), typeof(SkUiZoomView), 2.5,
        validateValue: SkUiValidate.FinitePositive,
        propertyChanged: static (view, _, value) => ((SkUiZoomView)view).Zoom.DoubleTapScale = (double)value);

    /// <summary>Bindable <see cref="IsWheelZoomEnabled"/>.</summary>
    public static readonly BindableProperty IsWheelZoomEnabledProperty = BindableProperty.Create(nameof(IsWheelZoomEnabled), typeof(bool), typeof(SkUiZoomView), true,
        propertyChanged: static (view, _, value) => ((SkUiZoomView)view).Zoom.IsWheelEnabled = (bool)value);

    private SkUiZoomState? _zoom;
    private View? _zoomedContent;

    // Created on first use: an implicit style sets properties inside the base constructor.
    private SkUiZoomState Zoom => _zoom ??= new SkUiZoomState(this);

    /// <summary>Creates a zoom view.</summary>
    public SkUiZoomView()
    {
        if (!IsSet(IsClippedToBoundsProperty))
            IsClippedToBounds = true;
        PinchUpdated += (_, args) => Zoom.OnPinch(args);
        DoubleTapped += (_, args) => Zoom.OnDoubleTap(args.Position);
    }

    /// <summary>The scale changed.</summary>
    public event EventHandler? ZoomChanged;

    /// <summary>The content's scale: 1 fills the view.</summary>
    public double ZoomScale => Zoom.Scale;

    /// <summary>The smallest scale. Default 1.</summary>
    public double MinimumZoomScale { get => (double)GetValue(MinimumZoomScaleProperty); set => SetValue(MinimumZoomScaleProperty, value); }

    /// <summary>The largest scale. Default 5.</summary>
    public double MaximumZoomScale { get => (double)GetValue(MaximumZoomScaleProperty); set => SetValue(MaximumZoomScaleProperty, value); }

    /// <summary>The scale a double tap zooms to. Default 2.5.</summary>
    public double DoubleTapZoomScale { get => (double)GetValue(DoubleTapZoomScaleProperty); set => SetValue(DoubleTapZoomScaleProperty, value); }

    /// <summary>Whether the wheel zooms. Default <c>true</c>; off, it passes to what is around, as a scroll view holding this view needs.</summary>
    public bool IsWheelZoomEnabled { get => (bool)GetValue(IsWheelZoomEnabledProperty); set => SetValue(IsWheelZoomEnabledProperty, value); }

    /// <summary>Sets <see cref="IsWheelZoomEnabled"/>.</summary>
    public SkUiZoomView SetIsWheelZoomEnabled(bool value)
    {
        IsWheelZoomEnabled = value;
        return this;
    }

    /// <summary>Sets <see cref="MinimumZoomScale"/> (finite and positive).</summary>
    public SkUiZoomView SetMinimumZoomScale(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        MinimumZoomScale = value;
        return this;
    }

    /// <summary>Sets <see cref="MaximumZoomScale"/> (finite and positive).</summary>
    public SkUiZoomView SetMaximumZoomScale(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        MaximumZoomScale = value;
        return this;
    }

    /// <summary>Sets <see cref="DoubleTapZoomScale"/> (finite and positive).</summary>
    public SkUiZoomView SetDoubleTapZoomScale(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        DoubleTapZoomScale = value;
        return this;
    }

    /// <summary>Back to scale 1 (or the nearest the limits allow), centered.</summary>
    public void ResetZoom() => Zoom.Reset();

    /// <summary>Zooms to <paramref name="scale"/>, keeping the point at <paramref name="focus"/> (in this view's coordinates) where it is.</summary>
    public void ZoomTo(double scale, Point focus) => Zoom.ZoomAbout(scale, focus);

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        base.ArrangeContent(size);
        Zoom.Reclamp();
    }

    /// <inheritdoc />
    protected override void OnContentChanged()
    {
        base.OnContentChanged();
        // What left is not zoomed any more, and what came starts at rest.
        if (_zoomedContent is { } previous)
        {
            previous.Scale = 1;
            previous.TranslationX = 0;
            previous.TranslationY = 0;
        }
        _zoomedContent = Content as View;
        Zoom.Reset();
    }

    Rect ISkUiZoomTarget.ZoomContentFrame => Content is View content ? content.Frame : new Rect(0, 0, Width, Height);

    void ISkUiZoomTarget.ApplyZoom(double scale, double x, double y)
    {
        if (Content is not View content)
            return;
        content.Scale = scale;
        content.TranslationX = x;
        content.TranslationY = y;
    }

    private void OnPanned(object? sender, SkUiPanUpdatedEventArgs args) => Zoom.OnPan(args);

    void ISkUiZoomTarget.OnCanPanChanged(bool canPan)
    {
        if (canPan)
            PanUpdated += OnPanned;
        else
            PanUpdated -= OnPanned;
    }

    void ISkUiZoomTarget.OnZoomChanged()
    {
        OnPropertyChanged(nameof(ZoomScale));
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    Point ISkUiWheelHandler.OnWheel(double deltaX, double deltaY) => Zoom.OnWheel(deltaX, deltaY);
}
