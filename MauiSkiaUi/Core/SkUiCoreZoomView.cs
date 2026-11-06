namespace MauiSkiaUi.Core;

/// <summary>
/// Pinch, pan, double tap and wheel zoom of its content (Core analogue of <see cref="SkUiZoomView"/>): the content's composite-time scale and offset change,
/// so nothing re-records. It owns the content's <c>Scale</c>, <c>TranslationX</c> and <c>TranslationY</c>. At scale 1 it takes no drag, so it can sit in a scroll view
/// (with <see cref="IsWheelZoomEnabled"/> off, so the wheel scrolls); a double tap zooms in at the tap and back out.
/// </summary>
public class SkUiCoreZoomView : SkUiCoreContentView, ISkUiZoomTarget, ISkUiWheelHandler
{
    private readonly SkUiZoomState _zoom;
    private SkUiCoreNode? _zoomedContent;

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

    /// <summary>Whether the wheel zooms. Default <c>true</c>; off, it passes to what is around, as a scroll view holding this view needs.</summary>
    public bool IsWheelZoomEnabled { get => _zoom.IsWheelEnabled; set => SetIsWheelZoomEnabled(value); }

    /// <summary>Sets <see cref="IsWheelZoomEnabled"/>.</summary>
    public SkUiCoreZoomView SetIsWheelZoomEnabled(bool value)
    {
        if (_zoom.IsWheelEnabled == value) return this;
        _zoom.IsWheelEnabled = value;
        OnPropertyChanged(nameof(IsWheelZoomEnabled));
        return this;
    }

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

    /// <summary>Back to scale 1 (or the nearest the limits allow), centered.</summary>
    public void ResetZoom() => _zoom.Reset();

    /// <summary>Zooms to <paramref name="scale"/>, keeping the point at <paramref name="focus"/> (in this view's coordinates) where it is.</summary>
    public void ZoomTo(double scale, Point focus) => _zoom.ZoomAbout(scale, focus);

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        base.ArrangeContent(size);
        _zoom.Reclamp();
    }

    /// <inheritdoc />
    protected override void OnContentChanged()
    {
        base.OnContentChanged();
        // What left is not zoomed any more, and what came starts at rest.
        if (_zoomedContent is { } previous)
        {
            previous.SetScale(1);
            previous.SetTranslationX(0);
            previous.SetTranslationY(0);
        }
        _zoomedContent = Content;
        _zoom.Reset();
    }

    Rect ISkUiZoomTarget.ZoomContentFrame => Content is { } content ? content.Frame : new Rect(Point.Zero, Frame.Size);

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
