using System.ComponentModel;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Base Core node: DIP layout, paint, and touch with no MAUI control overhead.
/// Implements <see cref="INotifyPropertyChanged"/>; fluent <c>Set*</c> methods are the single apply path
/// and raise notifications via <see cref="SetProperty{T}"/>.
/// </summary>
public class SkUiCoreNode : ISkUiCoreNode, INotifyPropertyChanged
{
    private bool _measureDirty = true;
    private bool _arrangeDirty = true;
    private Size _lastConstraint;
    private Size _desiredSize;
    private Rect _frame;
    private ISkUiCoreNode? _parent;
    private bool _isVisible = true;
    private double _width = double.NaN;
    private double _height = double.NaN;
    private double _minimumWidth;
    private double _minimumHeight;
    private double _maximumWidth = double.PositiveInfinity;
    private double _maximumHeight = double.PositiveInfinity;
    private Thickness _margin;
    private LayoutAlignment _horizontalAlignment = LayoutAlignment.Fill;
    private LayoutAlignment _verticalAlignment = LayoutAlignment.Fill;
    private int _updateDepth;
    private bool _paintPending;
    private bool _layoutPending;
    private SkUiAnimationClock? _rootClock;
    private SkUiAnimationClock? _ownedClock;

    /// <inheritdoc />
    public ISkUiCoreNode? Parent => _parent;

    /// <inheritdoc />
    public Rect Frame => _frame;

    /// <inheritdoc />
    public Size DesiredSize => _desiredSize;

    /// <inheritdoc cref="ISkUiCoreNode.IsVisible" />
    public bool IsVisible
    {
        get => _isVisible;
        set => SetIsVisible(value);
    }

    /// <summary>Explicit width in DIPs, or <see cref="double.NaN"/> for unconstrained.</summary>
    public double Width
    {
        get => _width;
        set => SetWidth(value);
    }

    /// <summary>Explicit height in DIPs, or <see cref="double.NaN"/> for unconstrained.</summary>
    public double Height
    {
        get => _height;
        set => SetHeight(value);
    }

    /// <summary>Minimum width in DIPs.</summary>
    public double MinimumWidth
    {
        get => _minimumWidth;
        set => SetMinimumWidth(value);
    }

    /// <summary>Minimum height in DIPs.</summary>
    public double MinimumHeight
    {
        get => _minimumHeight;
        set => SetMinimumHeight(value);
    }

    /// <summary>Maximum width in DIPs.</summary>
    public double MaximumWidth
    {
        get => _maximumWidth;
        set => SetMaximumWidth(value);
    }

    /// <summary>Maximum height in DIPs.</summary>
    public double MaximumHeight
    {
        get => _maximumHeight;
        set => SetMaximumHeight(value);
    }

    /// <summary>Outer margin in DIPs applied during measure/arrange.</summary>
    public Thickness Margin
    {
        get => _margin;
        set => SetMargin(value);
    }

    /// <summary>
    /// Horizontal alignment within the arrange slot given by a parent layout (same idea as MAUI
    /// <c>HorizontalOptions</c> / <c>ComputeFrame</c>). Default is <see cref="LayoutAlignment.Fill"/>.
    /// </summary>
    public LayoutAlignment HorizontalAlignment
    {
        get => _horizontalAlignment;
        set => SetHorizontalAlignment(value);
    }

    /// <summary>
    /// Vertical alignment within the arrange slot given by a parent layout (same idea as MAUI
    /// <c>VerticalOptions</c> / <c>ComputeFrame</c>). Default is <see cref="LayoutAlignment.Fill"/>.
    /// </summary>
    public LayoutAlignment VerticalAlignment
    {
        get => _verticalAlignment;
        set => SetVerticalAlignment(value);
    }

    /// <summary>Raised when this node or a descendant needs another surface frame.</summary>
    public event EventHandler? PaintInvalidated;

    /// <summary>Raised when measure/arrange must run again for this subtree.</summary>
    public event EventHandler? MeasureInvalidated;

    /// <summary>
    /// Shared animation clock: walks to a host-bound root clock when present; otherwise a locally owned clock.
    /// <see cref="SkUiCoreHost"/> binds its <see cref="SkUiView.AnimationClock"/> onto the Core root.
    /// </summary>
    public SkUiAnimationClock AnimationClock
    {
        get
        {
            for (SkUiCoreNode? node = this; node is not null; node = node._parent as SkUiCoreNode)
            {
                if (node._rootClock is { } root)
                    return root;
            }
            return _ownedClock ??= new SkUiAnimationClock();
        }
    }

    /// <summary>Called when the inherited animation root changes (host reparent / clock rebind).</summary>
    protected virtual void OnAnimationRootChanged() { }

    /// <summary>Binds the host surface clock onto a Core tree root (cleared when the host detaches content).</summary>
    internal void BindAnimationClock(SkUiAnimationClock? clock)
    {
        if (ReferenceEquals(_rootClock, clock)) return;
        _rootClock = clock;
        OnAnimationRootChanged();
        PropagateAnimationRootChanged();
    }

    /// <summary>Notifies descendants that <see cref="AnimationClock"/> may have changed.</summary>
    private void PropagateAnimationRootChanged()
    {
        if (this is SkUiCorePanel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is SkUiCoreNode coreChild)
                {
                    coreChild.OnAnimationRootChanged();
                    coreChild.PropagateAnimationRootChanged();
                }
            }
        }
        else if (this is SkUiCoreContentView { Content: SkUiCoreNode content })
        {
            content.OnAnimationRootChanged();
            content.PropagateAnimationRootChanged();
        }
    }

    /// <summary>Attaches this node under <paramref name="parent"/>; the previous parent must be cleared first.</summary>
    internal void AttachTo(ISkUiCoreNode? parent)
    {
        if (_parent is not null && parent is not null && !ReferenceEquals(_parent, parent))
            throw new InvalidOperationException("A Core node already has a parent.");
        SetProperty(ref _parent, parent, nameof(Parent));
        OnAnimationRootChanged();
    }

    /// <summary>Sets visibility; raises <see cref="System.ComponentModel.INotifyPropertyChanged"/> when changed.</summary>
    public SkUiCoreNode SetIsVisible(bool value)
    {
        if (!SetProperty(ref _isVisible, value, nameof(IsVisible))) return this;
        OnIsVisibleChanged();
        InvalidateMeasure();
        return this;
    }

    /// <summary>Called after <see cref="IsVisible"/> changes.</summary>
    protected virtual void OnIsVisibleChanged() { }

    /// <summary>Sets an explicit width (<see cref="double.NaN"/> clears it).</summary>
    public SkUiCoreNode SetWidth(double value)
    {
        if (!SetProperty(ref _width, value, nameof(Width))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets an explicit height (<see cref="double.NaN"/> clears it).</summary>
    public SkUiCoreNode SetHeight(double value)
    {
        if (!SetProperty(ref _height, value, nameof(Height))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets minimum width in DIPs.</summary>
    public SkUiCoreNode SetMinimumWidth(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _minimumWidth, value, nameof(MinimumWidth))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets minimum height in DIPs.</summary>
    public SkUiCoreNode SetMinimumHeight(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (!SetProperty(ref _minimumHeight, value, nameof(MinimumHeight))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets maximum width in DIPs.</summary>
    public SkUiCoreNode SetMaximumWidth(double value)
    {
        if (value < 0 || double.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (!SetProperty(ref _maximumWidth, value, nameof(MaximumWidth))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets maximum height in DIPs.</summary>
    public SkUiCoreNode SetMaximumHeight(double value)
    {
        if (value < 0 || double.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (!SetProperty(ref _maximumHeight, value, nameof(MaximumHeight))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets outer margin in DIPs.</summary>
    public SkUiCoreNode SetMargin(Thickness value)
    {
        if (!SetProperty(ref _margin, value, nameof(Margin))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets horizontal alignment within the parent's arrange slot.</summary>
    public SkUiCoreNode SetHorizontalAlignment(LayoutAlignment value)
    {
        if (!SetProperty(ref _horizontalAlignment, value, nameof(HorizontalAlignment))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets vertical alignment within the parent's arrange slot.</summary>
    public SkUiCoreNode SetVerticalAlignment(LayoutAlignment value)
    {
        if (!SetProperty(ref _verticalAlignment, value, nameof(VerticalAlignment))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Coalesces layout and paint notifications until the matching <see cref="EndUpdating"/>.</summary>
    public void StartUpdating() => _updateDepth++;

    /// <summary>Ends an update batch and propagates at most one invalidation.</summary>
    public void EndUpdating()
    {
        if (_updateDepth == 0)
            throw new InvalidOperationException("No update batch is active.");
        if (--_updateDepth == 0)
            FlushInvalidation();
    }

    /// <inheritdoc />
    public void InvalidateMeasure()
    {
        _measureDirty = true;
        _arrangeDirty = true;
        _layoutPending = true;
        InvalidatePaint();
    }

    /// <inheritdoc />
    public void InvalidatePaint()
    {
        _paintPending = true;
        if (_updateDepth == 0)
            FlushInvalidation();
    }

    private void FlushInvalidation()
    {
        var layout = _layoutPending;
        var paint = _paintPending;
        _layoutPending = _paintPending = false;
        if (_parent is SkUiCoreNode coreParent)
        {
            if (layout) coreParent.InvalidateMeasure();
            else if (paint) coreParent.InvalidatePaint();
            return;
        }

        // Root of a Core tree (typically owned by SkUiCoreHost).
        if (layout) MeasureInvalidated?.Invoke(this, EventArgs.Empty);
        if (paint) PaintInvalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Size Measure(double widthConstraint, double heightConstraint)
    {
        var constraint = new Size(widthConstraint, heightConstraint);
        if (!_measureDirty && constraint == _lastConstraint)
            return _desiredSize;

        Size desired;
        if (!_isVisible)
        {
            desired = Size.Zero;
            _lastConstraint = constraint;
            _measureDirty = false;
            _arrangeDirty = true;
        }
        else
        {
            var width = Math.Max(0, widthConstraint - _margin.HorizontalThickness);
            var height = Math.Max(0, heightConstraint - _margin.VerticalThickness);
            if (!double.IsNaN(_width)) width = Math.Min(width, _width);
            if (!double.IsNaN(_height)) height = Math.Min(height, _height);
            width = Math.Min(width, _maximumWidth);
            height = Math.Min(height, _maximumHeight);

            var content = MeasureContent(width, height);
            var desiredWidth = ResolveDimension(widthConstraint, _width, content.Width, _minimumWidth, _maximumWidth) + _margin.HorizontalThickness;
            var desiredHeight = ResolveDimension(heightConstraint, _height, content.Height, _minimumHeight, _maximumHeight) + _margin.VerticalThickness;
            desired = new Size(desiredWidth, desiredHeight);
            _lastConstraint = constraint;
            _measureDirty = false;
            _arrangeDirty = true;
        }

        if (_desiredSize != desired)
        {
            _desiredSize = desired;
            OnPropertyChanged(nameof(DesiredSize));
        }

        return _desiredSize;
    }

    /// <summary>Measures intrinsic content without margin, in DIPs.</summary>
    protected virtual Size MeasureContent(double widthConstraint, double heightConstraint) => Size.Zero;

    /// <inheritdoc />
    public void Arrange(Rect bounds)
    {
        if (!_arrangeDirty && bounds == _frame)
            return;

        var x = bounds.X + _margin.Left;
        var y = bounds.Y + _margin.Top;
        var width = Math.Max(0, bounds.Width - _margin.HorizontalThickness);
        var height = Math.Max(0, bounds.Height - _margin.VerticalThickness);
        if (!double.IsNaN(_width)) width = Math.Min(width, _width);
        if (!double.IsNaN(_height)) height = Math.Min(height, _height);
        var frame = new Rect(x, y, width, height);
        if (_frame != frame)
        {
            _frame = frame;
            OnPropertyChanged(nameof(Frame));
        }
        ArrangeContent(_frame.Size);
        _arrangeDirty = false;
        InvalidatePaint();
    }

    /// <summary>Arranges hosted content in this node's local coordinate system.</summary>
    protected virtual void ArrangeContent(Size size) { }

    /// <inheritdoc />
    public void Paint(SKCanvas canvas)
    {
        if (!_isVisible || _frame.Width <= 0 || _frame.Height <= 0)
            return;
        var save = canvas.Save();
        try
        {
            canvas.ClipRect(new SKRect(0, 0, (float)_frame.Width, (float)_frame.Height));
            if (_paintBackground is { } paintBackground)
                paintBackground(canvas);
            OnPaintContent(canvas);
            _paintOverlay?.Invoke(canvas);
        }
        finally
        {
            canvas.RestoreToCount(save);
        }
    }

    private Action<SKCanvas>? _paintBackground;
    private Action<SKCanvas>? _paintOverlay;

    /// <summary>
    /// Background-layer painter. When unset, the Background phase is a no-op.
    /// Prefer this over subclassing for chrome customization.
    /// </summary>
    public Action<SKCanvas>? PaintBackground
    {
        get => _paintBackground;
        set => SetPaintBackground(value);
    }

    /// <summary>
    /// Overlay-layer painter drawn after <see cref="OnPaintContent"/>. <c>null</c> means no overlay.
    /// </summary>
    public Action<SKCanvas>? PaintOverlay
    {
        get => _paintOverlay;
        set => SetPaintOverlay(value);
    }

    /// <summary>Sets the Background painter; <c>null</c> draws no background.</summary>
    public SkUiCoreNode SetPaintBackground(Action<SKCanvas>? value)
    {
        if (!SetProperty(ref _paintBackground, value, nameof(PaintBackground))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Sets the Overlay painter; <c>null</c> draws no overlay.</summary>
    public SkUiCoreNode SetPaintOverlay(Action<SKCanvas>? value)
    {
        if (!SetProperty(ref _paintOverlay, value, nameof(PaintOverlay))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Content paint phase (structural). Always virtual — not replaceable by a delegate.</summary>
    protected virtual void OnPaintContent(SKCanvas canvas) { }

    /// <inheritdoc />
    public virtual bool Touch(SkUiTouchEvent touch) => false;

    /// <summary>Paints a child translated to its <see cref="Frame"/> origin.</summary>
    protected static void PaintChild(ISkUiCoreNode child, SKCanvas canvas)
    {
        var save = canvas.Save();
        try
        {
            canvas.Translate((float)child.Frame.X, (float)child.Frame.Y);
            child.Paint(canvas);
        }
        finally
        {
            canvas.RestoreToCount(save);
        }
    }

    /// <summary>Converts a MAUI graphics color to Skia.</summary>
    protected static SKColor ToSkColor(Color color) => new(
        (byte)(color.Red * 255), (byte)(color.Green * 255),
        (byte)(color.Blue * 255), (byte)(color.Alpha * 255));

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Updates <paramref name="field"/> when the value changes and raises <see cref="PropertyChanged"/>.
    /// Returns <c>false</c> when the value is unchanged (no notification).
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Raises <see cref="PropertyChanged"/> for <paramref name="propertyName"/>.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static double ResolveDimension(double constraint, double explicitSize, double content, double minimum, double maximum)
    {
        var value = !double.IsNaN(explicitSize) ? explicitSize : content;
        if (!double.IsInfinity(constraint))
            value = Math.Min(value, constraint);
        value = Math.Max(value, minimum);
        value = Math.Min(value, maximum);
        return Math.Max(0, value);
    }
}
