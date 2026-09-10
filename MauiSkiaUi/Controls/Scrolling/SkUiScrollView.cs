using System.Diagnostics;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MauiSkiaUi;

/// <summary>A single-surface scroller with clamped offsets, tap cancellation, wheel input, and inertial fling.</summary>
public class SkUiScrollView : SkUiContentView
{
    private ScrollOrientation orientation = ScrollOrientation.Vertical;
    private Size extent;
    private Size viewport;
    private long? pointer;
    private Point startPosition;
    private Point lastPosition;
    private TimeSpan lastTime;
    private Point velocity;
    private bool dragging;
    private IDisposable? motion;
    private TaskCompletionSource? scrollCompletion;

    /// <summary>Creates a scroller whose motion stops when its surface unloads.</summary>
    public SkUiScrollView() => Unloaded += (_, _) => CancelInteraction();

    /// <summary>Bindable enabled scroll axes.</summary>
    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(ScrollOrientation), typeof(SkUiScrollView), ScrollOrientation.Vertical,
        propertyChanged: (view, _, value) => ((SkUiScrollView)view).SetOrientation((ScrollOrientation)value));
    /// <summary>Enabled axes; Neither disables scrolling.</summary>
    public ScrollOrientation Orientation { get => orientation; set => SetValue(OrientationProperty, value); }
    /// <summary>Current horizontal offset in DIPs.</summary>
    public double ScrollX { get; private set; }
    /// <summary>Current vertical offset in DIPs.</summary>
    public double ScrollY { get; private set; }
    /// <summary>Measured scrollable content extent, including padding.</summary>
    public Size ContentSize => extent;
    /// <summary>Raised after a clamped offset changes.</summary>
    public event EventHandler<ScrolledEventArgs>? Scrolled;
    /// <summary>Sets orientation without bindable write-back.</summary>
    public SkUiScrollView SetOrientation(ScrollOrientation value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        CancelInteraction();
        orientation = value;
        InvalidateMeasureOverride();
        return this;
    }
    private bool Horizontal => orientation is ScrollOrientation.Horizontal or ScrollOrientation.Both;
    private bool Vertical => orientation is ScrollOrientation.Vertical or ScrollOrientation.Both;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        extent = base.MeasureContent(Horizontal ? double.PositiveInfinity : widthConstraint, Vertical ? double.PositiveInfinity : heightConstraint);
        return new Size(Math.Min(widthConstraint, extent.Width), Math.Min(heightConstraint, extent.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        viewport = size;
        SetOffset(ScrollX, ScrollY);
        ArrangeScrolledContent();
    }

    private void ArrangeScrolledContent() => Content?.Arrange(new Rect(
        Padding.Left - ScrollX, Padding.Top - ScrollY,
        Math.Max(0, Math.Max(extent.Width, viewport.Width) - Padding.HorizontalThickness),
        Math.Max(0, Math.Max(extent.Height, viewport.Height) - Padding.VerticalThickness)));

    /// <summary>Clamps and sets an offset without remeasuring content or writing bindable properties.</summary>
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
        return motion = AnimationClock.Start(progress => SetOffset(
            startX + (horizontalOffset - startX) * progress,
            startY + (verticalOffset - startY) * progress), duration, Easing.CubicOut);
    }

    /// <summary>Scrolls immediately or animates over 300 ms. A superseding gesture, scroll, or unload cancels the task.</summary>
    public Task ScrollToAsync(double horizontalOffset, double verticalOffset, bool animated = true)
    {
        ScrollTo(ScrollX, ScrollY);
        if (!animated) { SetOffset(horizontalOffset, verticalOffset); return Task.CompletedTask; }
        if (!double.IsFinite(horizontalOffset) || !double.IsFinite(verticalOffset)) throw new ArgumentOutOfRangeException(nameof(horizontalOffset));
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scrollCompletion = completion;
        var startX = ScrollX;
        var startY = ScrollY;
        motion = AnimationClock.Start(progress =>
        {
            SetOffset(startX + (horizontalOffset - startX) * progress, startY + (verticalOffset - startY) * progress);
            if (progress >= 1) { scrollCompletion = null; completion.TrySetResult(); }
        }, TimeSpan.FromMilliseconds(300), Easing.CubicOut);
        return completion.Task;
    }

    private void SetOffset(double horizontalOffset, double verticalOffset)
    {
        if (!double.IsFinite(horizontalOffset) || !double.IsFinite(verticalOffset)) throw new ArgumentOutOfRangeException(nameof(horizontalOffset));
        var nextX = Horizontal ? Math.Clamp(horizontalOffset, 0, Math.Max(0, extent.Width - viewport.Width)) : 0;
        var nextY = Vertical ? Math.Clamp(verticalOffset, 0, Math.Max(0, extent.Height - viewport.Height)) : 0;
        if (nextX == ScrollX && nextY == ScrollY) return;
        ScrollX = nextX;
        ScrollY = nextY;
        ArrangeScrolledContent();
        OnPropertyChanged(nameof(ScrollX));
        OnPropertyChanged(nameof(ScrollY));
        InvalidatePaint();
        Scrolled?.Invoke(this, new ScrolledEventArgs(ScrollX, ScrollY));
    }

    private void StopMotion()
    {
        motion?.Dispose();
        motion = null;
        scrollCompletion?.TrySetCanceled();
        scrollCompletion = null;
    }

    private void CancelInteraction()
    {
        StopMotion();
        CancelContentTouch();
        pointer = null;
        dragging = false;
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
        pointer = null;
        dragging = false;
        ScrollX = ScrollY = 0;
        extent = Size.Zero;
        base.OnContentChanged();
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        if (Parent is null) CancelInteraction();
        base.OnParentSet();
    }

    /// <inheritdoc />
    public override bool Touch(SkUiTouchEvent touch)
    {
        if (!IsVisible || InputTransparent || !IsEnabled)
        {
            StopMotion();
            pointer = null;
            dragging = false;
            return base.Touch(touch);
        }
        if (orientation == ScrollOrientation.Neither) return base.Touch(touch);
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
            if (pointer is not null || !new Rect(Point.Zero, viewport).Contains(touch.Position)) return false;
            StopMotion();
            pointer = touch.Id;
            startPosition = lastPosition = touch.Position;
            lastTime = now;
            velocity = Point.Zero;
            dragging = false;
            base.Touch(touch);
            return true;
        }
        if (pointer != touch.Id) return false;
        if (touch.Action == SkUiTouchAction.Moved)
        {
            var distanceX = Horizontal ? touch.Position.X - startPosition.X : 0;
            var distanceY = Vertical ? touch.Position.Y - startPosition.Y : 0;
            if (!dragging && distanceX * distanceX + distanceY * distanceY > 100)
            {
                dragging = true;
                base.Touch(touch with { Action = SkUiTouchAction.Cancelled });
            }
            if (dragging)
            {
                var deltaX = Horizontal ? lastPosition.X - touch.Position.X : 0;
                var deltaY = Vertical ? lastPosition.Y - touch.Position.Y : 0;
                var seconds = (now - lastTime).TotalSeconds;
                if (seconds > 0) velocity = new Point(Math.Clamp(deltaX / seconds, -3000, 3000), Math.Clamp(deltaY / seconds, -3000, 3000));
                SetOffset(ScrollX + deltaX, ScrollY + deltaY);
            }
            else base.Touch(touch);
            lastPosition = touch.Position;
            lastTime = now;
            return true;
        }
        if (touch.Action is SkUiTouchAction.Released or SkUiTouchAction.Cancelled)
        {
            pointer = null;
            if (!dragging) base.Touch(touch);
            else if (touch.Action == SkUiTouchAction.Released && (now - lastTime).TotalMilliseconds <= 100)
                StartFling(velocity);
            dragging = false;
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
        motion = AnimationClock.Start(progress =>
        {
            var time = duration * (progress - progress * progress / 2);
            var previous = new Point(ScrollX, ScrollY);
            SetOffset(startX + speed.X * time, startY + speed.Y * time);
            if (progress > 0 && previous == new Point(ScrollX, ScrollY)) StopMotion();
        }, TimeSpan.FromSeconds(duration));
    }
}