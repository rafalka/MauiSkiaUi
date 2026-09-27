using MauiSkiaUi.Rendering;

namespace MauiSkiaUi.Core;

/// <summary>
/// Multi-child Core layout base: child attach/detach, padding, paint order, and reverse hit-testing.
/// Layouts such as absolute and stack panels derive from this type.
/// </summary>
public abstract class SkUiCorePanel : SkUiCoreNode
{
    private readonly List<ISkUiCoreNode> _children = [];
    private Thickness _padding;

    /// <summary>Creates a panel; panels do not clip children by default.</summary>
    protected SkUiCorePanel() => InitClipToBounds(false);

    /// <summary>Children in insertion order (paint back-to-front; hit-test front-to-back).</summary>
    public IReadOnlyList<ISkUiCoreNode> Children => _children;

    /// <summary>Inner padding in DIPs.</summary>
    public Thickness Padding
    {
        get => _padding;
        set => SetPadding(value);
    }

    /// <summary>Sets padding; invalidates measure when the value changes.</summary>
    public SkUiCorePanel SetPadding(Thickness value)
    {
        if (!SetProperty(ref _padding, value, nameof(Padding))) return this;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Appends a child; the child must be an unparented <see cref="SkUiCoreNode"/>.</summary>
    public SkUiCorePanel Add(ISkUiCoreNode child)
    {
        InsertChild(_children.Count, child);
        return this;
    }

    /// <summary>Removes a child if present.</summary>
    public bool Remove(ISkUiCoreNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        var index = _children.IndexOf(child);
        if (index < 0) return false;
        RemoveChildAt(index);
        return true;
    }

    /// <summary>Removes all children.</summary>
    public void Clear()
    {
        for (var index = _children.Count - 1; index >= 0; index--)
            RemoveChildAt(index);
    }

    /// <summary>Inserts and attaches a child at <paramref name="index"/>.</summary>
    protected void InsertChild(int index, ISkUiCoreNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent is not null)
            throw new InvalidOperationException("Child already has a parent.");
        if (child is not SkUiCoreNode coreChild)
            throw new ArgumentException("Core layouts accept SkUiCoreNode instances only.", nameof(child));
        if (_children.Contains(child))
            throw new InvalidOperationException("Child is already in this layout.");
        if (WouldCreateParentCycle(child))
            throw new InvalidOperationException("Child cannot create a parent cycle.");
        if (index < 0 || index > _children.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        coreChild.AttachTo(this);
        _children.Insert(index, child);
        SkUiDiagnostics.NotifyChildAdded(this, child, index);
        OnChildrenChanged();
        InvalidateRender(SkUiRenderDirty.Children);
        InvalidateMeasure();
    }

    /// <summary>Detaches and removes the child at <paramref name="index"/>.</summary>
    protected void RemoveChildAt(int index)
    {
        var child = _children[index];
        _children.RemoveAt(index);
        SkUiDiagnostics.NotifyChildRemoved(this, child, index);
        if (child is SkUiCoreNode coreChild)
            coreChild.AttachTo(null);
        OnChildRemoved(child);
        OnChildrenChanged();
        InvalidateRender(SkUiRenderDirty.Children);
        InvalidateMeasure();
    }

    /// <summary>Called after a child is detached from this panel.</summary>
    protected virtual void OnChildRemoved(ISkUiCoreNode child) { }

    /// <summary>Called after the child collection changes (add/remove/clear).</summary>
    protected virtual void OnChildrenChanged() { }

    /// <inheritdoc />
    internal override IReadOnlyList<IVisualTreeElement> VisualChildren => _children;

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        foreach (var child in _children)
            if (child is ISkUiRenderable renderable)
                children.Add(renderable);
    }

    /// <inheritdoc />
    public override bool Touch(SkUiTouchEvent touch)
    {
        for (var index = _children.Count - 1; index >= 0; index--)
        {
            var child = _children[index];
            if (!child.IsVisible) continue;
            if (!TryMapFromParent(child, touch.Position, out var position)
                || position.X < 0 || position.Y < 0 || position.X >= child.Frame.Width || position.Y >= child.Frame.Height)
                continue;
            if (child.Touch(touch with { Position = position }))
                return true;
        }
        return false;
    }
}
