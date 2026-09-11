using System.Diagnostics;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A single-surface scroller with clamped offsets, tap cancellation, wheel input, and inertial fling.
/// Hosted content is recorded into an <see cref="SKPicture"/> when it changes; scroll offset only
/// translates that cache so offset-only frames avoid re-recording the subtree.
/// </summary>
public class SkUiScrollView : SkUiContentView
{
    private ScrollOrientation _orientation = ScrollOrientation.Vertical;
    private Size _extent;
    private Size _viewport;
    private long? _pointer;
    private Point _startPosition;
    private Point _lastPosition;
    private TimeSpan _lastTime;
    private Point _velocity;
    private bool _dragging;
    private IDisposable? _motion;
    private TaskCompletionSource? _scrollCompletion;
    private SKPicture? _contentPicture;
    private bool _contentPictureDirty = true;
    private SkUiView? _contentPictureSource;
    private int _contentPictureRebuilds;

    /// <summary>Creates a scroller whose motion stops when its surface unloads.</summary>
    public SkUiScrollView() => Unloaded += (_, _) => CancelInteraction();

    /// <summary>Bindable enabled scroll axes.</summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(ScrollOrientation), typeof(SkUiScrollView), ScrollOrientation.Vertical,
        propertyChanged: (view, _, value) => ((SkUiScrollView)view).SetOrientation((ScrollOrientation)value));
    /// <summary>Enabled axes; Neither disables scrolling.</summary>
    public ScrollOrientation Orientation { get => _orientation; set => SetValue(OrientationProperty, value); }
    /// <summary>Current horizontal offset in DIPs.</summary>
    public double ScrollX { get; private set; }
    /// <summary>Current vertical offset in DIPs.</summary>
    public double ScrollY { get; private set; }
    /// <summary>Measured scrollable content extent, including padding.</summary>
    public Size ContentSize => _extent;
    /// <summary>Raised after a clamped offset changes.</summary>
    public event EventHandler<ScrolledEventArgs>? Scrolled;

    /// <summary>How many times the content <see cref="SKPicture"/> has been rebuilt (stress / tests).</summary>
    internal int ContentPictureRebuilds => _contentPictureRebuilds;

    /// <summary>Whether a reusable content picture is currently held.</summary>
    internal bool HasContentPicture => _contentPicture is not null && !_contentPictureDirty;

    /// <summary>Sets orientation without bindable write-back.</summary>
    public SkUiScrollView SetOrientation(ScrollOrientation value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        CancelInteraction();
        _orientation = value;
        InvalidateMeasureOverride();
        return this;
    }
    private bool Horizontal => _orientation is ScrollOrientation.Horizontal or ScrollOrientation.Both;
    private bool Vertical => _orientation is ScrollOrientation.Vertical or ScrollOrientation.Both;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        _extent = base.MeasureContent(Horizontal ? double.PositiveInfinity : widthConstraint, Vertical ? double.PositiveInfinity : heightConstraint);
        return new Size(Math.Min(widthConstraint, _extent.Width), Math.Min(heightConstraint, _extent.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        _viewport = size;
        // Keep content arranged at its layout origin; scroll offset is applied in paint/touch only so
        // SetOffset does not rearrange the hosted subtree on every frame.
        ArrangeContentAtOrigin();
        SetOffset(ScrollX, ScrollY);
    }

    /// <summary>Arranges content in the padded slot without baking scroll offset into <see cref="IView.Frame"/>.</summary>
    private void ArrangeContentAtOrigin()
    {
        InvalidateContentPicture();
        Content?.Arrange(new Rect(
            Padding.Left, Padding.Top,
            Math.Max(0, Math.Max(_extent.Width, _viewport.Width) - Padding.HorizontalThickness),
            Math.Max(0, Math.Max(_extent.Height, _viewport.Height) - Padding.VerticalThickness)));
    }

    /// <summary>Clamps and sets an offset without remeasuring or rearranging content.</summary>
    public SkUiScrollView ScrollTo(double horizontalOffset, double verticalOffset)
    {
        StopMotion();
        SetOffset(horizontalOffset, verticalOffset);
        return this;
    }

    /// <summary>Starts a clock-driven scroll; disposing the returned handle cancels it.</summary>
    public IDisposable AnimateScrollTo(double horizontalOffset, double verticalOffset, TimeSpan duration)
    {
        StopMotion();
        var startX = ScrollX;
        var startY = ScrollY;
        return _motion = AnimationClock.Start(progress => SetOffset(
            startX + (horizontalOffset - startX) * progress,
            startY + (verticalOffset - startY) * progress), duration, Easing.CubicOut);
    }

    /// <summary>Scrolls immediately or animates over 300 ms. A superseding gesture, scroll, or unload cancels the task.</summary>
    public Task ScrollToAsync(double horizontalOffset, double verticalOffset, bool animated = true)
    {
        ScrollTo(ScrollX, ScrollY);
        if (!animated) { SetOffset(horizontalOffset, verticalOffset); return Task.CompletedTask; }
        EnsureFiniteOffsets(horizontalOffset, verticalOffset);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _scrollCompletion = completion;
        var startX = ScrollX;
        var startY = ScrollY;
        _motion = AnimationClock.Start(progress =>
        {
            SetOffset(startX + (horizontalOffset - startX) * progress, startY + (verticalOffset - startY) * progress);
            if (progress >= 1) { _scrollCompletion = null; completion.TrySetResult(); }
        }, TimeSpan.FromMilliseconds(300), Easing.CubicOut);
        return completion.Task;
    }

    private void SetOffset(double horizontalOffset, double verticalOffset)
    {
        EnsureFiniteOffsets(horizontalOffset, verticalOffset);
        var nextX = Horizontal ? Math.Clamp(horizontalOffset, 0, Math.Max(0, _extent.Width - _viewport.Width)) : 0;
        var nextY = Vertical ? Math.Clamp(verticalOffset, 0, Math.Max(0, _extent.Height - _viewport.Height)) : 0;
        if (nextX == ScrollX && nextY == ScrollY) return;
        ScrollX = nextX;
        ScrollY = nextY;
        OnPropertyChanged(nameof(ScrollX));
        OnPropertyChanged(nameof(ScrollY));
        // Offset-only: keep the content picture; root RecordFrame will DrawPicture + translate.
        InvalidatePaint();
        SyncOverlayDescendants(this);
        Scrolled?.Invoke(this, new ScrolledEventArgs(ScrollX, ScrollY));
    }

    /// <summary>
    /// Native overlays are positioned from arranged frames; when scroll offset changes without rearrange,
    /// push updated root-relative bounds to any hosted <see cref="SkUiMauiContentView"/> descendants.
    /// </summary>
    private static void SyncOverlayDescendants(SkUiView node)
    {
        if (node is SkUiMauiContentView overlay)
            overlay.NotifyAncestorScrollOffsetChanged();
        foreach (var child in node.SkiaChildren)
            if (child is SkUiView view)
                SyncOverlayDescendants(view);
    }

    /// <summary>Throws with the name of the first non-finite offset so call sites can see which argument failed.</summary>
    private static void EnsureFiniteOffsets(double horizontalOffset, double verticalOffset)
    {
        if (!double.IsFinite(horizontalOffset))
            throw new ArgumentOutOfRangeException(nameof(horizontalOffset));
        if (!double.IsFinite(verticalOffset))
            throw new ArgumentOutOfRangeException(nameof(verticalOffset));
    }

    private void StopMotion()
    {
        _motion?.Dispose();
        _motion = null;
        _scrollCompletion?.TrySetCanceled();
        _scrollCompletion = null;
    }

    private void CancelInteraction()
    {
        StopMotion();
        CancelContentTouch();
        _pointer = null;
        _dragging = false;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if ((propertyName == nameof(IsEnabled) && !IsEnabled)
            || (propertyName == nameof(IsVisible) && !IsVisible)
            || (propertyName == nameof(InputTransparent) && InputTransparent)) CancelInteraction();
    }

    /// <inheritdoc />
    protected override void OnContentChanged()
    {
        StopMotion();
        _pointer = null;
        _dragging = false;
        ScrollX = ScrollY = 0;
        _extent = Size.Zero;
        DetachContentPictureSource();
        InvalidateContentPicture();
        base.OnContentChanged();
        AttachContentPictureSource();
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        if (Parent is null)
        {
            CancelInteraction();
            InvalidateContentPicture();
        }
        base.OnParentSet();
    }

    private void AttachContentPictureSource()
    {
        if (Content is not SkUiView view)
            return;
        _contentPictureSource = view;
        view.PaintInvalidated += OnContentPaintInvalidated;
    }

    private void DetachContentPictureSource()
    {
        if (_contentPictureSource is null)
            return;
        _contentPictureSource.PaintInvalidated -= OnContentPaintInvalidated;
        _contentPictureSource = null;
    }

    private void OnContentPaintInvalidated(object? sender, EventArgs args) => InvalidateContentPicture();

    /// <summary>Drops the cached content picture so the next paint rebuilds it from the live subtree.</summary>
    private void InvalidateContentPicture()
    {
        _contentPictureDirty = true;
        _contentPicture?.Dispose();
        _contentPicture = null;
    }

    /// <summary>
    /// Ensures the content picture matches the arranged content. Recording uses the same
    /// <see cref="SkUiView.PaintChild"/> path as a live paint so pixels stay identical.
    /// </summary>
    private void EnsureContentPicture()
    {
        if (!_contentPictureDirty && _contentPicture is not null)
            return;
        _contentPicture?.Dispose();
        _contentPicture = null;
        if (Content is null || _extent.Width <= 0 || _extent.Height <= 0)
        {
            _contentPictureDirty = false;
            return;
        }

        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)_extent.Width, (float)_extent.Height));
        PaintChild(Content, canvas);
        _contentPicture = recorder.EndRecording();
        _contentPictureDirty = false;
        _contentPictureRebuilds++;
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (Content is null)
            return;
        EnsureContentPicture();
        var save = canvas.Save();
        try
        {
            canvas.ClipRect(new SKRect(0, 0, (float)_viewport.Width, (float)_viewport.Height));
            canvas.Translate((float)-ScrollX, (float)-ScrollY);
            if (_contentPicture is not null)
                canvas.DrawPicture(_contentPicture);
            else
                PaintChild(Content, canvas);
        }
        finally
        {
            canvas.RestoreToCount(save);
        }
    }

    /// <summary>Maps a viewport-local point into content-local space including scroll offset.</summary>
    private SkUiTouchEvent MapToContent(SkUiTouchEvent touch) =>
        touch with { Position = new Point(touch.Position.X + ScrollX, touch.Position.Y + ScrollY) };

    /// <inheritdoc />
    public override bool Touch(SkUiTouchEvent touch)
    {
        if (!IsVisible || InputTransparent || !IsEnabled)
        {
            StopMotion();
            _pointer = null;
            _dragging = false;
            return base.Touch(touch);
        }
        if (_orientation == ScrollOrientation.Neither) return base.Touch(MapToContent(touch));
        if (touch.Action == SkUiTouchAction.Wheel)
        {
            // SkUiTouchEvent carries a single WheelDelta with no axis indicator, so a plain wheel always
            // scrolls the vertical axis when it is enabled (Vertical or Both), and only scrolls horizontally
            // when the horizontal axis is the sole enabled one. Both does not distinguish a horizontal-wheel
            // gesture (e.g. shift+wheel) from a vertical one; that requires a richer wheel event and is deferred.
            ScrollTo(ScrollX - (Horizontal && !Vertical ? touch.WheelDelta : 0), ScrollY - (Vertical ? touch.WheelDelta : 0));
            return true;
        }
        var now = touch.Timestamp ?? TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
        if (touch.Action == SkUiTouchAction.Pressed)
        {
            if (_pointer is not null || !new Rect(Point.Zero, _viewport).Contains(touch.Position)) return false;
            StopMotion();
            _pointer = touch.Id;
            _startPosition = _lastPosition = touch.Position;
            _lastTime = now;
            _velocity = Point.Zero;
            _dragging = false;
            base.Touch(MapToContent(touch));
            return true;
        }
        if (_pointer != touch.Id) return false;
        if (touch.Action == SkUiTouchAction.Moved)
        {
            var distanceX = Horizontal ? touch.Position.X - _startPosition.X : 0;
            var distanceY = Vertical ? touch.Position.Y - _startPosition.Y : 0;
            if (!_dragging && distanceX * distanceX + distanceY * distanceY > 100)
            {
                _dragging = true;
                base.Touch(MapToContent(touch) with { Action = SkUiTouchAction.Cancelled });
            }
            if (_dragging)
            {
                var deltaX = Horizontal ? _lastPosition.X - touch.Position.X : 0;
                var deltaY = Vertical ? _lastPosition.Y - touch.Position.Y : 0;
                var seconds = (now - _lastTime).TotalSeconds;
                if (seconds > 0) _velocity = new Point(Math.Clamp(deltaX / seconds, -3000, 3000), Math.Clamp(deltaY / seconds, -3000, 3000));
                SetOffset(ScrollX + deltaX, ScrollY + deltaY);
            }
            else base.Touch(MapToContent(touch));
            _lastPosition = touch.Position;
            _lastTime = now;
            return true;
        }
        if (touch.Action is SkUiTouchAction.Released or SkUiTouchAction.Cancelled)
        {
            _pointer = null;
            if (!_dragging) base.Touch(MapToContent(touch));
            else if (touch.Action == SkUiTouchAction.Released && (now - _lastTime).TotalMilliseconds <= 100)
                StartFling(_velocity);
            _dragging = false;
            return true;
        }
        return true;
    }

    private void StartFling(Point speed)
    {
        var magnitude = Math.Max(Math.Abs(speed.X), Math.Abs(speed.Y));
        if (magnitude < 40) return;
        var duration = Math.Min(1, magnitude / 3000);
        var startX = ScrollX;
        var startY = ScrollY;
        _motion = AnimationClock.Start(progress =>
        {
            var time = duration * (progress - progress * progress / 2);
            var previous = new Point(ScrollX, ScrollY);
            SetOffset(startX + speed.X * time, startY + speed.Y * time);
            if (progress > 0 && previous == new Point(ScrollX, ScrollY)) StopMotion();
        }, TimeSpan.FromSeconds(duration));
    }
}
