namespace MauiSkiaUi.Core;

/// <summary>
/// Pinch, pan, double tap and wheel zoom of its content (Core analogue of <see cref="SkUiZoomView"/>): the content's composite-time scale and offset change,
/// so nothing re-records. At scale 1 it takes no drag, so it can sit in a scroll view; a double tap zooms in at the tap and back out.
/// </summary>
public class SkUiCoreZoomView : SkUiCoreContentView, ISkUiZoomTarget, ISkUiWheelHandler
{
    private readonly SkUiZoomState _zoom;

    /// <summary>Creates a zoom view.</summary>
    public SkUiCoreZoomView()
    {
        _zoom = new SkUiZoomState(this);
        SetClipToBounds(true);
        PinchUpdated += (_, args) => _zoom.OnPinch(args);
        DoubleTapped += (_, args) => _zoom.OnDoubleTap(args.Position);
    }

    /// <summary>The scale changed.</summary>
    public event EventHandler? ZoomChanged;

    /// <summary>The content's scale: 1 fills the view.</summary>
    public double ZoomScale => _zoom.Scale;

    /// <summary>The smallest scale. Default 1.</summary>
    public double MinimumZoomScale { get => _zoom.MinimumScale; set => SetMinimumZoomScale(value); }

    /// <summary>The largest scale. Default 5.</summary>
    public double MaximumZoomScale { get => _zoom.MaximumScale; set => SetMaximumZoomScale(value); }

    /// <summary>The scale a double tap zooms to. Default 2.5.</summary>
    public double DoubleTapZoomScale { get => _zoom.DoubleTapScale; set => SetDoubleTapZoomScale(value); }

    /// <summary>Sets <see cref="MinimumZoomScale"/> (finite and positive).</summary>
    public SkUiCoreZoomView SetMinimumZoomScale(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        _zoom.MinimumScale = value;
        _zoom.Reclamp();
        OnPropertyChanged(nameof(MinimumZoomScale));
        return this;
    }

    /// <summary>Sets <see cref="MaximumZoomScale"/> (finite and positive).</summary>
    public SkUiCoreZoomView SetMaximumZoomScale(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        _zoom.MaximumScale = value;
        _zoom.Reclamp();
        OnPropertyChanged(nameof(MaximumZoomScale));
        return this;
    }

    /// <summary>Sets <see cref="DoubleTapZoomScale"/> (finite and positive).</summary>
    public SkUiCoreZoomView SetDoubleTapZoomScale(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        _zoom.DoubleTapScale = value;
        OnPropertyChanged(nameof(DoubleTapZoomScale));
        return this;
    }

    /// <summary>Back to scale 1, centered.</summary>
    public void ResetZoom() => _zoom.Reset();

    /// <summary>Zooms to <paramref name="scale"/>, keeping the point at <paramref name="focus"/> (in this view's coordinates) where it is.</summary>
    public void ZoomTo(double scale, Point focus) => _zoom.ZoomAbout(scale, focus);

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        base.ArrangeContent(size);
        _zoom.Reclamp();
    }

    Size ISkUiZoomTarget.ZoomViewport => Frame.Size;

    void ISkUiZoomTarget.ApplyZoom(double scale, double x, double y)
    {
        if (Content is not { } content)
            return;
        content.SetScale(scale);
        content.SetTranslationX(x);
        content.SetTranslationY(y);
    }

    private void OnPanned(object? sender, SkUiPanUpdatedEventArgs args) => _zoom.OnPan(args);

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

    Point ISkUiWheelHandler.OnWheel(double deltaX, double deltaY) => _zoom.OnWheel(deltaX, deltaY);
}
