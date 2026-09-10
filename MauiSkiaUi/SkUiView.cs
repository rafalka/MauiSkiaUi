using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;
using SkiaSharp;
using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>Base for Skia-drawn views, with layout that does not require a handler.</summary>
public class SkUiView : View, ISkUiView
{
    private bool measureDirty = true;
    private bool arrangeDirty = true;
    private Rect lastArrangeBounds;
    private Size lastConstraint;
    private Size measuredSize;
    private bool hwAccelerated;
    private int updateDepth;
    private bool paintPending;
    private bool layoutPending;
    private long? pressedPointer;
    private Point pressPosition;
    private bool tapCancelled;
    private SkUiAnimationClock? animationClock;
    private ICommand? tappedCommand;
    private object? tappedCommandParameter;

    /// <summary>Bindable opt-in tap command.</summary>
    public static readonly BindableProperty TappedCommandProperty = BindableProperty.Create(
        nameof(TappedCommand), typeof(ICommand), typeof(SkUiView), null,
        propertyChanged: (view, _, value) => ((SkUiView)view).SetTappedCommand((ICommand?)value));
    /// <summary>Bindable tap command parameter.</summary>
    public static readonly BindableProperty TappedCommandParameterProperty = BindableProperty.Create(
        nameof(TappedCommandParameter), typeof(object), typeof(SkUiView), null,
        propertyChanged: (view, _, value) => ((SkUiView)view).SetTappedCommandParameter(value));
    /// <summary>Optional command; passive nodes participate when it can execute.</summary>
    public ICommand? TappedCommand { get => tappedCommand; set => SetValue(TappedCommandProperty, value); }
    /// <summary>Parameter supplied to the tap command.</summary>
    public object? TappedCommandParameter { get => tappedCommandParameter; set => SetValue(TappedCommandParameterProperty, value); }
    /// <summary>Sets the tap command without bindable write-back.</summary>
    public SkUiView SetTappedCommand(ICommand? value) { tappedCommand = value; return this; }
    /// <summary>Sets the tap parameter without bindable write-back.</summary>
    public SkUiView SetTappedCommandParameter(object? value) { tappedCommandParameter = value; return this; }
    /// <summary>Whether an eligible captured pointer is currently pressed inside this node.</summary>
    public bool IsPressed { get; private set; }

    private void SetPressed(bool value)
    {
        if (IsPressed == value) return;
        IsPressed = value;
        OnPropertyChanged(nameof(IsPressed));
        OnPressedChanged();
        InvalidatePaint();
    }

    /// <summary>Updates intrinsic control feedback when the shared pointer state changes.</summary>
    protected virtual void OnPressedChanged() { }

    /// <summary>Whether an intrinsic tap action is currently enabled.</summary>
    protected virtual bool CanReceiveTap => true;

    /// <summary>Raised when this node or a descendant needs another surface frame.</summary>
    public event EventHandler? PaintInvalidated;

    /// <summary>Opts this node into single taps. MAUI GestureRecognizers are not used.</summary>
    public event EventHandler<SkUiTappedEventArgs>? Tapped;

    /// <summary>The clock shared by this node and its surface-owning ancestor.</summary>
    public SkUiAnimationClock AnimationClock => SkiaParent?.AnimationClock ?? (animationClock ??= new());

    internal SkUiView? SkiaParent => Parent as SkUiView;

    /// <summary>Selects GPU rendering for a standalone node. Set before attaching a handler.</summary>
    public bool HwAccelerated
    {
        get => hwAccelerated;
        set
        {
            if (Handler is not null && value != hwAccelerated)
                throw new InvalidOperationException("HwAccelerated must be set before handler creation.");
            hwAccelerated = value;
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        var constraint = new Size(widthConstraint, heightConstraint);
        if (!measureDirty && constraint == lastConstraint)
            return measuredSize;

        IView view = this;
        var width = Math.Max(0, widthConstraint - Margin.HorizontalThickness);
        var height = Math.Max(0, heightConstraint - Margin.VerticalThickness);
        var maximumWidth = double.IsNaN(view.MaximumWidth) ? double.PositiveInfinity : view.MaximumWidth;
        var maximumHeight = double.IsNaN(view.MaximumHeight) ? double.PositiveInfinity : view.MaximumHeight;
        var content = MeasureContent(
            Math.Min(width, double.IsNaN(view.Width) ? maximumWidth : view.Width),
            Math.Min(height, double.IsNaN(view.Height) ? maximumHeight : view.Height));
        measuredSize = IsVisible
            ? new Size(
                LayoutManager.ResolveConstraints(width, view.Width, content.Width, view.MinimumWidth, maximumWidth) + Margin.HorizontalThickness,
                LayoutManager.ResolveConstraints(height, view.Height, content.Height, view.MinimumHeight, maximumHeight) + Margin.VerticalThickness)
            : Size.Zero;
        lastConstraint = constraint;
        measureDirty = false;
        arrangeDirty = true;
        return measuredSize;
    }

    /// <summary>Measures intrinsic content without margins, in DIPs.</summary>
    protected virtual Size MeasureContent(double widthConstraint, double heightConstraint) => Size.Zero;

    /// <inheritdoc />
    protected override Size ArrangeOverride(Rect bounds)
    {
        if (!arrangeDirty && bounds == lastArrangeBounds)
            return Frame.Size;
        var previousSize = Frame.Size;
        Frame = this.ComputeFrame(bounds);
        if (arrangeDirty || previousSize != Frame.Size)
            ArrangeContent(Frame.Size);
        lastArrangeBounds = bounds;
        arrangeDirty = false;
        Handler?.PlatformArrange(Frame);
        InvalidatePaint();
        return Frame.Size;
    }

    /// <summary>Arranges hosted content in this node's local coordinate system.</summary>
    protected virtual void ArrangeContent(Size size) { }

    /// <inheritdoc />
    protected override void InvalidateMeasureOverride()
    {
        measureDirty = true;
        arrangeDirty = true;
        layoutPending = true;
        InvalidatePaint();
    }

    /// <summary>Coalesces layout and paint notifications until the matching EndUpdating call.</summary>
    public void StartUpdating() => updateDepth++;

    /// <summary>Ends an update batch and propagates at most one invalidation.</summary>
    public void EndUpdating()
    {
        if (updateDepth == 0)
            throw new InvalidOperationException("No update batch is active.");
        if (--updateDepth == 0)
            FlushInvalidation();
    }

    /// <summary>Requests a redraw without invalidating measured sizes.</summary>
    public void InvalidatePaint()
    {
        paintPending = true;
        if (updateDepth == 0)
            FlushInvalidation();
    }

    private void FlushInvalidation()
    {
        var invalidateLayout = layoutPending;
        var invalidatePaint = paintPending;
        layoutPending = paintPending = false;
        if (invalidateLayout)
        {
            if (SkiaParent is { } parent)
                ((IView)parent).InvalidateMeasure();
            else
                base.InvalidateMeasureOverride();
        }
        if (invalidatePaint)
        {
            PaintInvalidated?.Invoke(this, EventArgs.Empty);
            if (!invalidateLayout)
                SkiaParent?.InvalidatePaint();
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if ((propertyName == nameof(IsEnabled) && !IsEnabled)
            || (propertyName == nameof(IsVisible) && !IsVisible)
            || (propertyName == nameof(InputTransparent) && InputTransparent))
        {
            pressedPointer = null;
            SetPressed(false);
        }
        switch (propertyName)
        {
            case nameof(WidthRequest):
            case nameof(HeightRequest):
            case nameof(MinimumWidthRequest):
            case nameof(MinimumHeightRequest):
            case nameof(MaximumWidthRequest):
            case nameof(MaximumHeightRequest):
            case nameof(Margin):
            case nameof(HorizontalOptions):
            case nameof(VerticalOptions):
            case nameof(IsVisible):
                InvalidateMeasureOverride();
                break;
            case nameof(Background):
            case nameof(BackgroundColor):
            case nameof(Opacity):
            case nameof(TranslationX):
            case nameof(TranslationY):
            case nameof(Rotation):
            case nameof(Scale):
            case nameof(ScaleX):
            case nameof(ScaleY):
            case nameof(AnchorX):
            case nameof(AnchorY):
            case nameof(ZIndex):
                InvalidatePaint();
                break;
        }
    }

    /// <inheritdoc />
    public void Paint(SKCanvas canvas)
    {
        if (!IsVisible || Opacity <= 0 || Width <= 0 || Height <= 0)
            return;
        var saveCount = canvas.Save();
        try
        {
            canvas.Concat(RenderMatrix);
            if (canvas.QuickReject(new SKRect(0, 0, (float)Width, (float)Height)))
                return;
            canvas.ClipRect(new SKRect(0, 0, (float)Width, (float)Height));
            if (Opacity < 1)
            {
                using var alpha = new SKPaint { Color = SKColors.White.WithAlpha((byte)(255 * Opacity)) };
                canvas.SaveLayer(alpha);
            }
            OnPaintBackground(canvas);
            OnPaintContent(canvas);
            OnPaintOverlay(canvas);
        }
        finally
        {
            canvas.RestoreToCount(saveCount);
        }
    }

    /// <summary>Paints a solid MAUI background before content. Other brush types are deferred.</summary>
    protected virtual void OnPaintBackground(SKCanvas canvas)
    {
        var color = Background is SolidColorBrush brush ? brush.Color : BackgroundColor;
        if (color is null)
            return;
        using var paint = new SKPaint { Color = ToSkColor(color) };
        canvas.DrawRect(0, 0, (float)Width, (float)Height, paint);
    }

    /// <summary>Paints this node's content and, for containers, its hosted children.</summary>
    protected virtual void OnPaintContent(SKCanvas canvas) { }

    /// <summary>Paints chrome above content and children.</summary>
    protected virtual void OnPaintOverlay(SKCanvas canvas) { }

    /// <summary>Converts a MAUI color to its Skia RGBA representation.</summary>
    protected static SKColor ToSkColor(Color color) => new(
        (byte)(color.Red * 255), (byte)(color.Green * 255),
        (byte)(color.Blue * 255), (byte)(color.Alpha * 255));

    private SKMatrix RenderMatrix
    {
        get
        {
            var anchorX = (float)(Width * AnchorX);
            var anchorY = (float)(Height * AnchorY);
            return SKMatrix.CreateTranslation((float)TranslationX + anchorX, (float)TranslationY + anchorY)
                .PreConcat(SKMatrix.CreateRotationDegrees((float)Rotation))
                .PreConcat(SKMatrix.CreateScale((float)(Scale * ScaleX), (float)(Scale * ScaleY)))
                .PreConcat(SKMatrix.CreateTranslation(-anchorX, -anchorY));
        }
    }

    internal static bool MapPoint(ISkUiView child, Point position, out Point local)
    {
        var point = new SKPoint((float)(position.X - child.Frame.X), (float)(position.Y - child.Frame.Y));
        if (child is SkUiView node)
        {
            if (!node.RenderMatrix.TryInvert(out var inverse))
            {
                local = default;
                return false;
            }
            point = inverse.MapPoint(point);
        }
        local = new Point(point.X, point.Y);
        return true;
    }

    internal static void PaintChild(ISkUiView child, SKCanvas canvas)
    {
        var saveCount = canvas.Save();
        try
        {
            canvas.Translate((float)child.Frame.X, (float)child.Frame.Y);
            child.Paint(canvas);
        }
        finally
        {
            canvas.RestoreToCount(saveCount);
        }
    }

    internal void ValidateChild(ISkUiView child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent is not null || child.Handler is not null)
            throw new InvalidOperationException("A hosted view must be unparented and have no handler.");
        for (IElement? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
            if (ReferenceEquals(ancestor, child))
                throw new InvalidOperationException("A SkiaUi tree cannot contain a cycle.");
        if (child is not Element)
            throw new ArgumentException("Hosted views must also be MAUI Elements for XAML ownership.", nameof(child));
    }

    internal void AttachChild(ISkUiView child)
    {
        AddLogicalChild((Element)child);
        InvalidateMeasureOverride();
    }

    internal void DetachChild(ISkUiView child)
    {
        RemoveLogicalChild((Element)child);
        InvalidateMeasureOverride();
    }

    /// <inheritdoc />
    public virtual bool Touch(SkUiTouchEvent touch)
    {
        if (touch.Action == SkUiTouchAction.Pressed)
        {
            if (pressedPointer is not null || InputTransparent || !IsVisible)
                return false;
            if (!IsEnabled || !CanReceiveTap)
                return true;
            if (Tapped is null && !HandlesTap && !(tappedCommand?.CanExecute(tappedCommandParameter) ?? false))
                return false;
            pressedPointer = touch.Id;
            pressPosition = touch.Position;
            tapCancelled = false;
            SetPressed(true);
            return true;
        }
        if (pressedPointer != touch.Id)
            return false;

        var deltaX = touch.Position.X - pressPosition.X;
        var deltaY = touch.Position.Y - pressPosition.Y;
        tapCancelled |= deltaX * deltaX + deltaY * deltaY > 100;
        SetPressed(!tapCancelled && new Rect(0, 0, Width, Height).Contains(touch.Position));
        if (touch.Action is SkUiTouchAction.Released or SkUiTouchAction.Cancelled)
        {
            pressedPointer = null;
            SetPressed(false);
            if (touch.Action == SkUiTouchAction.Released && !tapCancelled && IsEnabled && CanReceiveTap && IsVisible && !InputTransparent
                && new Rect(0, 0, Width, Height).Contains(touch.Position))
                OnTapped(new SkUiTappedEventArgs(touch.Position));
        }
        return true;
    }

    /// <summary>Allows a control to participate in taps without an event subscriber.</summary>
    protected virtual bool HandlesTap => false;

    /// <summary>Raises a classified single tap.</summary>
    protected virtual void OnTapped(SkUiTappedEventArgs args)
    {
        Tapped?.Invoke(this, args);
        if (tappedCommand?.CanExecute(tappedCommandParameter) == true)
            tappedCommand.Execute(tappedCommandParameter);
    }
}

/// <summary>A classified tap in local DIPs.</summary>
public sealed class SkUiTappedEventArgs(Point position) : EventArgs
{
    /// <summary>The release position relative to the tapped node.</summary>
    public Point Position { get; } = position;
}