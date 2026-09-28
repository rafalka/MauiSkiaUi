using MauiSkiaUi.Rendering;

namespace MauiSkiaUi.Core;

/// <summary>
/// MAUI-compatible shell that hosts a single Core tree root.
/// This is the only Core-related type that derives from <see cref="SkUiView"/>; use it to place
/// Core content under <see cref="SkUiScrollView"/>, layouts, or a standalone surface.
/// </summary>
public class SkUiCoreHost : SkUiView, IVisualTreeElement
{
    private SkUiCoreNode? _content;

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
            _content.BindAnimationClock(null);
            _content.HostOwner = null;
            SkUiRenderInvalidation.ResetSubtree(_content);
            SkUiGestureSet.CancelSubtree(_content);
            SkUiDiagnostics.NotifyChildRemoved(this, _content, 0);
        }

        _content = value;

        if (_content is not null)
        {
            _content.HostOwner = this;
            _content.MeasureInvalidated += OnContentMeasureInvalidated;
            _content.BindAnimationClock(AnimationClock);
            SkUiDiagnostics.NotifyChildAdded(this, _content, 0);
        }

        InvalidateRender(SkUiRenderDirty.Children);
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

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged() => _content?.NotifyFlowDirectionChanged();

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

    // The host's Core root is not a MAUI logical child; expose it to diagnostics tools (Live Visual Tree,
    // automation agents) by re-implementing IVisualTreeElement.
    IReadOnlyList<IVisualTreeElement> IVisualTreeElement.GetVisualChildren() => _content is null ? [] : [_content];

    IVisualTreeElement? IVisualTreeElement.GetVisualParent() => Parent;

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        if (_content is not null)
            children.Add(_content);
    }

}
