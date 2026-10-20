using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// A drawn node's resolved <see cref="ISkUiRenderable.TreeLook"/>, valid until <see cref="SkUiLook.TreeLookVersion"/> moves
/// (a look set or a drawn parent changed). Read on the UI thread only.
/// </summary>
internal struct SkUiTreeLookCache
{
    private SkUiLook? _look;
    private int _version; // TreeLookVersion + 1 it was resolved at; 0: never

    /// <summary>The look <paramref name="node"/> draws with when it or an ancestor sets one, else <c>null</c>.</summary>
    public SkUiLook? Get(ISkUiRenderable node)
    {
        var version = SkUiLook.TreeLookVersion + 1;
        if (_version != version)
        {
            _look = node.LookOverride ?? node.RenderParent?.TreeLook;
            _version = version;
        }
        return _look;
    }

    /// <summary>
    /// <paramref name="node"/>'s drawn parent changed: looks resolve again, and when it now draws with another look its
    /// subtree forgets its cached measures and re-records (as when a look is set).
    /// </summary>
    public void ParentChanged(ISkUiRenderable node)
    {
        if (!SkUiLook.AnyTreeLook)
            return;
        var before = _look; // the look it last measured or drew with
        SkUiLook.TreeChanged();
        if (!ReferenceEquals(before, Get(node)))
            SkUiRenderInvalidation.MarkLookChanged(node);
    }
}
