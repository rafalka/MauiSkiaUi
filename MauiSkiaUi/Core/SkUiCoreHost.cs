using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// MAUI-compatible shell that hosts a single Core tree root.
/// This is the only Core-related type that derives from <see cref="SkUiView"/>; use it to place
/// Core content under <see cref="SkUiScrollView"/>, layouts, or a standalone surface.
/// </summary>
public class SkUiCoreHost : SkUiView
{
    private SkUiCoreNode? _content;
    private long? _capturedPointer;
    private ISkUiCoreNode? _capturedNode;

    /// <summary>Root of the hosted Core tree, or <c>null</c>.</summary>
    public SkUiCoreNode? Content => _content;

    /// <summary>Sets the Core root; previous root is detached from this host's invalidation hooks.</summary>
    public SkUiCoreHost SetContent(SkUiCoreNode? value)
    {
        if (ReferenceEquals(_content, value)) return this;

        if (value is not null)
        {
            if (value.Parent is not null)
                throw new InvalidOperationException("Core root must be unparented.");
            if (value.HostOwner is not null && !ReferenceEquals(value.HostOwner, this))
                throw new InvalidOperationException("Core root is already hosted by another SkUiCoreHost.");
        }

        if (_content is not null)
        {
            _content.MeasureInvalidated -= OnContentMeasureInvalidated;
            _content.PaintInvalidated -= OnContentPaintInvalidated;
            _content.BindAnimationClock(null);
            _content.HostOwner = null;
        }

        _capturedPointer = null;
        _capturedNode = null;
        _content = value;

        if (_content is not null)
        {
            _content.HostOwner = this;
            _content.MeasureInvalidated += OnContentMeasureInvalidated;
            _content.PaintInvalidated += OnContentPaintInvalidated;
            _content.BindAnimationClock(AnimationClock);
        }

        InvalidateMeasureOverride();
        return this;
    }

    /// <inheritdoc />
    protected override void OnAnimationRootChanged(bool subtreeDetached = false)
    {
        base.OnAnimationRootChanged(subtreeDetached);
        if (subtreeDetached || Parent is null)
            _content?.BindAnimationClock(null);
        else
            _content?.BindAnimationClock(AnimationClock);
    }

    private void OnContentMeasureInvalidated(object? sender, EventArgs e) => InvalidateMeasureOverride();
    private void OnContentPaintInvalidated(object? sender, EventArgs e) => InvalidatePaint();

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        _content?.Measure(widthConstraint, heightConstraint) ?? Size.Zero;

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        if (_content is null) return;

        var desired = _content.DesiredSize;
        if (desired.Width <= 0 && desired.Height <= 0)
            desired = _content.Measure(size.Width, size.Height);

        var slot = new Rect(0, 0, size.Width, size.Height);
        if (_content is SkUiCoreNode node)
            slot = SkUiCoreAbsoluteLayout.AlignInSlot(
                slot, desired, node.HorizontalAlignment, node.VerticalAlignment);

        _content.Arrange(slot);
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (_content is null) return;
        var save = canvas.Save();
        try
        {
            canvas.Translate((float)_content.Frame.X, (float)_content.Frame.Y);
            _content.Paint(canvas);
        }
        finally
        {
            canvas.RestoreToCount(save);
        }
    }

    /// <inheritdoc />
    public override bool Touch(SkUiTouchEvent touch)
    {
        if (_content is null || !_content.IsVisible)
            return false;

        if (_capturedPointer == touch.Id && _capturedNode is not null)
        {
            if (!IsUnderHostedRoot(_capturedNode))
            {
                try
                {
                    _capturedNode.Touch(new SkUiTouchEvent(
                        touch.Id, SkUiTouchAction.Cancelled, Point.Zero, touch.Timestamp, touch.WheelDelta));
                }
                finally
                {
                    _capturedPointer = null;
                    _capturedNode = null;
                }
                return false;
            }

            if (!TryMapToNode(touch.Position, _capturedNode, out var local))
                local = touch.Position;
            var delivered = _capturedNode.Touch(new SkUiTouchEvent(touch.Id, touch.Action, local, touch.Timestamp, touch.WheelDelta));
            if (touch.Action is SkUiTouchAction.Released or SkUiTouchAction.Cancelled)
            {
                _capturedPointer = null;
                _capturedNode = null;
            }
            return delivered;
        }

        if (touch.Action != SkUiTouchAction.Pressed)
            return false;

        // Hit-test in the root's local space (root Frame may be nonzero after arrange).
        var rootLocal = new Point(
            touch.Position.X - _content.Frame.X,
            touch.Position.Y - _content.Frame.Y);
        if (!HitTest(_content, rootLocal, out var target, out var targetLocal))
            return false;

        var handled = target.Touch(new SkUiTouchEvent(touch.Id, touch.Action, targetLocal, touch.Timestamp, touch.WheelDelta));
        if (handled)
        {
            _capturedPointer = touch.Id;
            _capturedNode = target;
        }
        return handled;
    }

    private bool IsUnderHostedRoot(ISkUiCoreNode node)
    {
        if (_content is null) return false;
        if (ReferenceEquals(node, _content)) return true;
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, _content))
                return true;
        }
        return false;
    }

    private static bool HitTest(ISkUiCoreNode root, Point position, out ISkUiCoreNode target, out Point local)
    {
        if (root is SkUiCorePanel panel)
        {
            for (var index = panel.Children.Count - 1; index >= 0; index--)
            {
                var child = panel.Children[index];
                if (!child.IsVisible) continue;
                var frame = child.Frame;
                if (position.X < frame.X || position.Y < frame.Y
                    || position.X >= frame.Right || position.Y >= frame.Bottom)
                    continue;
                var childPos = new Point(position.X - frame.X, position.Y - frame.Y);
                if (HitTest(child, childPos, out target, out local))
                    return true;
            }
        }
        else if (root is SkUiCoreContentView { Content: { IsVisible: true } content })
        {
            var frame = content.Frame;
            if (position.X >= frame.X && position.Y >= frame.Y
                && position.X < frame.Right && position.Y < frame.Bottom)
            {
                var childPos = new Point(position.X - frame.X, position.Y - frame.Y);
                if (HitTest(content, childPos, out target, out local))
                    return true;
            }
        }

        if (position.X >= 0 && position.Y >= 0 && position.X < root.Frame.Width && position.Y < root.Frame.Height)
        {
            target = root;
            local = position;
            return true;
        }

        target = null!;
        local = default;
        return false;
    }

    private static bool TryMapToNode(Point hostLocal, ISkUiCoreNode node, out Point local)
    {
        var x = 0.0;
        var y = 0.0;
        for (ISkUiCoreNode? current = node; current is not null; current = current.Parent)
        {
            x += current.Frame.X;
            y += current.Frame.Y;
        }
        local = new Point(hostLocal.X - x, hostLocal.Y - y);
        return true;
    }
}
