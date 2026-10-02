using MauiSkiaUi.Rendering;

namespace MauiSkiaUi.Core;

/// <summary>Single-child Core host with optional padding (Core analogue of <c>SkUiContentView</c>).</summary>
public class SkUiCoreContentView : SkUiCoreNode
{
    private SkUiCoreNode? _content;
    private Thickness _padding;

    /// <summary>Creates a content view; it does not clip its child by default.</summary>
    public SkUiCoreContentView() => InitClipToBounds(false);

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
        {
            previous.AttachTo(null);
            SkUiDiagnostics.NotifyChildRemoved(this, previous, 0);
        }
        _content = value;
        if (_content is not null && _content.Parent is null)
        {
            _content.AttachTo(this);
            SkUiDiagnostics.NotifyChildAdded(this, _content, 0);
        }
        OnPropertyChanged(nameof(Content));
        OnContentChanged();
        InvalidateRender(SkUiRenderDirty.Children);
        InvalidateMeasure();
        return this;
    }

    /// <summary>Called after <see cref="Content"/> is replaced.</summary>
    protected virtual void OnContentChanged() { }

    /// <summary>The space around the content: the padding (borders add their stroke).</summary>
    private protected virtual Thickness ContentInset => _padding;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var inset = ContentInset;
        var size = _content?.Measure(
            Math.Max(0, widthConstraint - inset.HorizontalThickness),
            Math.Max(0, heightConstraint - inset.VerticalThickness)) ?? Size.Zero;
        return new Size(size.Width + inset.HorizontalThickness, size.Height + inset.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var inset = ContentInset;
        _content?.Arrange(new Rect(
            inset.Left,
            inset.Top,
            Math.Max(0, size.Width - inset.HorizontalThickness),
            Math.Max(0, size.Height - inset.VerticalThickness)));
    }

    /// <inheritdoc />
    internal override IReadOnlyList<IVisualTreeElement> VisualChildren => _content is null ? [] : [_content];

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        if (_content is not null)
            children.Add(_content);
    }

}
