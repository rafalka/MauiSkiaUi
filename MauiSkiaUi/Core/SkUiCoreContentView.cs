using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>Single-child Core host with optional padding (Core analogue of <c>SkUiContentView</c>).</summary>
public class SkUiCoreContentView : SkUiCoreNode
{
    private SkUiCoreNode? _content;
    private Thickness _padding;

    /// <summary>Hosted child, or <c>null</c>.</summary>
    public SkUiCoreNode? Content
    {
        get => _content;
        set => SetContent(value);
    }

    /// <summary>Inset around the hosted child in DIPs.</summary>
    public Thickness Padding
    {
        get => _padding;
        set => SetPadding(value);
    }

    /// <summary>Sets padding; invalidates measure when the value changes.</summary>
    public SkUiCoreContentView SetPadding(Thickness value)
    {
        if (!SetProperty(ref _padding, value, nameof(Padding))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Replaces the hosted child; the previous child is detached.</summary>
    public SkUiCoreContentView SetContent(SkUiCoreNode? value)
    {
        if (ReferenceEquals(_content, value)) return this;
        if (value is not null)
        {
            if (value.Parent is not null && !ReferenceEquals(value.Parent, this))
                throw new InvalidOperationException("Child already has a parent.");
            if (WouldCreateParentCycle(value))
                throw new InvalidOperationException("Content cannot create a parent cycle.");
        }

        var previous = _content;
        if (previous is not null)
            previous.AttachTo(null);
        _content = value;
        if (_content is not null && _content.Parent is null)
            _content.AttachTo(this);
        OnPropertyChanged(nameof(Content));
        OnContentChanged();
        InvalidateMeasure();
        return this;
    }

    /// <summary>Called after <see cref="Content"/> is replaced.</summary>
    protected virtual void OnContentChanged() { }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var size = _content?.Measure(
            Math.Max(0, widthConstraint - _padding.HorizontalThickness),
            Math.Max(0, heightConstraint - _padding.VerticalThickness)) ?? Size.Zero;
        return new Size(size.Width + _padding.HorizontalThickness, size.Height + _padding.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        _content?.Arrange(new Rect(
            _padding.Left,
            _padding.Top,
            Math.Max(0, size.Width - _padding.HorizontalThickness),
            Math.Max(0, size.Height - _padding.VerticalThickness)));
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (_content is { } child)
            PaintChild(child, canvas);
    }

    /// <inheritdoc />
    public override bool Touch(SkUiTouchEvent touch)
    {
        if (_content is not { IsVisible: true } child)
            return false;
        var frame = child.Frame;
        if (touch.Position.X < frame.X || touch.Position.Y < frame.Y
            || touch.Position.X >= frame.Right || touch.Position.Y >= frame.Bottom)
            return false;
        return child.Touch(new SkUiTouchEvent(
            touch.Id,
            touch.Action,
            new Point(touch.Position.X - frame.X, touch.Position.Y - frame.Y),
            touch.Timestamp,
            touch.WheelDelta));
    }
}
