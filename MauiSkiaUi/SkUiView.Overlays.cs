using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

public partial class SkUiView
{
    /// <summary>What only a surface root with an overlay or a modal pane needs: one field on every view, made on first use.</summary>
    private sealed class SurfaceRootState
    {
        public SkUiSurfaceOverlay? Overlay;
        public List<ISkUiRenderable>? ModalPanes;
    }

    private SurfaceRootState? _surfaceRoot;

    internal SkUiSurfaceOverlay? OverlayIfAny => _surfaceRoot?.Overlay;

    private List<ISkUiRenderable>? ModalPaneList => _surfaceRoot?.ModalPanes;

    /// <summary>
    /// A surface root's overlay layer, created on first use: views drawn above everything else in the surface (the sheets that
    /// <see cref="SkUiSheet.ShowAsync{TResult}"/> and <see cref="SkUiSheetPopupPresenter"/> open), laid out in the root's whole frame and
    /// hit, read and focused before the root's own content. Only a surface root has one.
    /// </summary>
    internal SkUiSurfaceOverlay SurfaceOverlay
    {
        get
        {
            if (OverlayIfAny is { } overlay)
                return overlay;
            overlay = (_surfaceRoot ??= new SurfaceRootState()).Overlay = new SkUiSurfaceOverlay();
            AttachChild(overlay);
            // Created after the root's last layout pass (an open sheet joining the surface): laid out now, as the next pass may not come
            // (an unarranged layer has no bounds: screen readers would read nothing on it).
            if (_laidOut)
                ArrangeSurfaceOverlay(Frame.Size);
            return overlay;
        }
    }

    /// <summary>The overlay layer while this view is a surface root that has one with views in it, else <c>null</c>.</summary>
    private SkUiSurfaceOverlay? ShownSurfaceOverlay => OverlayIfAny is { HasViews: true } overlay && SkiaParent is null ? overlay : null;

    /// <summary>
    /// <see cref="SkiaChildren"/> and, on a surface root, its overlay layer: for the walks that start at a root (the animation
    /// clock, native views attached when the handler connects, what shows).
    /// </summary>
    internal IEnumerable<ISkUiView> SkiaChildrenWithOverlay => OverlayIfAny is { } overlay && SkiaParent is null ? SkiaChildren.Append(overlay) : SkiaChildren;

    /// <summary>Lays the overlay layer out in the root's frame (local coordinates) after the root's own content.</summary>
    private void ArrangeSurfaceOverlay(Size size)
    {
        if (OverlayIfAny is not { } overlay || SkiaParent is not null)
            return;
        IView view = overlay;
        view.Measure(size.Width, size.Height);
        view.Arrange(new Rect(0, 0, size.Width, size.Height));
        overlay.ReplaceHoisted();
    }

    /// <summary>
    /// The topmost modal pane of this surface root (an open modal sheet), or <c>null</c>: only it and what is inside it are read by
    /// screen readers and take keyboard focus; the rest of the surface leaves the semantics tree.
    /// </summary>
    internal ISkUiRenderable? ModalPane => ModalPaneList is { Count: > 0 } panes ? panes[^1] : null;

    /// <summary>Makes <paramref name="pane"/> this surface root's topmost modal pane (again, when it was one below).</summary>
    internal void AddModalPane(ISkUiRenderable pane)
    {
        var state = _surfaceRoot ??= new SurfaceRootState();
        var panes = state.ModalPanes ??= [];
        panes.Remove(pane);
        panes.Insert(SkUiSurfaceOverlay.IndexByOpenOrder(panes, pane), pane);
        OnModalPanesChanged();
    }

    /// <summary>Takes <paramref name="pane"/> out of this surface root's modal panes.</summary>
    internal void RemoveModalPane(ISkUiRenderable pane)
    {
        if (ModalPaneList?.Remove(pane) == true)
            OnModalPanesChanged();
    }

    private void OnModalPanesChanged()
    {
        // Focus outside the new topmost pane is let go of (it moves in where the pane asks); the tree is built again.
        _focusManager?.Validate();
        SemanticsOwner?.Invalidate();
    }
}

/// <summary>
/// A surface root's overlay layer (<see cref="SkUiView.SurfaceOverlay"/>): views placed over the whole surface, last opened on top. It
/// lets pointers through itself (what it holds takes them) and keeps the root's measure out of its own.
/// </summary>
internal sealed class SkUiSurfaceOverlay : SkUiView
{
    private readonly List<SkUiView> _views = [];
    private readonly List<SkUiView> _shown = [];

    /// <summary>
    /// Open sheets placed in layouts and drawn here (on this UI thread): while there are none, drawn views skip the check that keeps
    /// them out of their layout's drawn children.
    /// </summary>
    [ThreadStatic] internal static int HoistedCount;

    /// <summary>Whether the layer shows a view (its own, or an open sheet drawn here).</summary>
    public bool HasViews => _shown.Count > 0;

    /// <summary>The layer's own views (<see cref="Add"/>), back to front.</summary>
    public IReadOnlyList<SkUiView> Views => _views;

    /// <summary>What the layer shows, back to front: its own views and the sheets drawn here, in the order they were opened.</summary>
    public IReadOnlyList<SkUiView> Shown => _shown;

    /// <summary>Places <paramref name="view"/> on top of the layer, as its own child.</summary>
    public void Add(SkUiView view)
    {
        ValidateChild(view);
        _views.Add(view);
        _shown.Add(view);
        AttachChild(view);
        // The root lists the layer among its drawn children only while it shows views.
        SkiaParent?.InvalidateRender(SkUiRenderDirty.Children);
    }

    /// <summary>Takes <paramref name="view"/> off the layer; returns whether it was on it.</summary>
    public bool Remove(SkUiView view)
    {
        if (!_views.Remove(view))
            return false;
        _shown.Remove(view);
        DetachChild(view); // detached: its pictures are reset, its gestures cancelled, focus inside it let go of
        AfterShownRemoved();
        return true;
    }

    /// <summary>
    /// Draws <paramref name="sheet"/>, an open sheet placed in a layout, on top of the layer: its layout keeps it as a child (bindings,
    /// look, measure) but no longer draws it. Its render nodes start again, as the compositor lets go of what its layout drew.
    /// </summary>
    public void Hoist(SkUiView sheet)
    {
        var parent = sheet.SkiaParent;
        _shown.Insert(IndexByOpenOrder(_shown, sheet), sheet); // last, unless it was hidden and is shown again under later sheets
        HoistedCount++;
        Rendering.SkUiRenderInvalidation.ResetSubtree(sheet);
        parent?.InvalidateRender(SkUiRenderDirty.Children);
        InvalidateRender(SkUiRenderDirty.Children);
        SkiaParent?.InvalidateRender(SkUiRenderDirty.Children);
    }

    /// <summary>Draws <paramref name="sheet"/> in its layout again (it closed, or left the surface).</summary>
    public void Unhoist(SkUiView sheet)
    {
        if (!_shown.Remove(sheet))
            return;
        HoistedCount--;
        Rendering.SkUiRenderInvalidation.ResetSubtree(sheet);
        sheet.SkiaParent?.InvalidateRender(SkUiRenderDirty.Children);
        AfterShownRemoved();
    }

    /// <summary>
    /// Where <paramref name="item"/> goes in <paramref name="list"/>, kept in the order sheets opened (<see cref="SkUiSheet.OpenedAt"/>):
    /// above every sheet opened before it; what is not a sheet counts as opened last.
    /// </summary>
    internal static int IndexByOpenOrder<T>(List<T> list, T item) where T : class
    {
        var order = OpenOrderOf(item);
        var index = list.Count;
        while (index > 0 && OpenOrderOf(list[index - 1]) > order)
            index--;
        return index;
    }

    private static long OpenOrderOf(object item) => item is SkUiSheet sheet ? sheet.OpenedAt : long.MaxValue;

    /// <summary>Moves <paramref name="view"/>, shown here, on top of every other (a sheet opened again while it was closing).</summary>
    public void BringToFront(SkUiView view)
    {
        if (_shown.Count == 0 || ReferenceEquals(_shown[^1], view) || !_shown.Remove(view))
            return;
        _shown.Add(view);
        InvalidateRender(SkUiRenderDirty.Children);
    }

    private void AfterShownRemoved()
    {
        // Empty, the layer leaves the root's drawn children and the compositor lets its render node go: it starts again from a new one
        // when it shows the next view (else that view would sync into a node nothing draws).
        if (!HasViews)
            Rendering.SkUiRenderInvalidation.ResetSubtree(this);
        InvalidateRender(SkUiRenderDirty.Children);
        SkiaParent?.InvalidateRender(SkUiRenderDirty.Children);
    }

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren => _views;

    /// <inheritdoc />
    internal override void AddRenderChildren(List<Rendering.ISkUiRenderable> children)
    {
        foreach (var view in _shown)
            children.Add(view);
    }

    /// <summary>
    /// The surface was laid out: the sheets drawn here take their place again, where their layouts are now (a layout around one moved).
    /// </summary>
    internal void ReplaceHoisted()
    {
        if (_shown.Count == _views.Count)
            return;
        foreach (var view in _shown)
            if (!ReferenceEquals(view.SkiaParent, this))
                view.InvalidateRender(SkUiRenderDirty.Props);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Pinned to the root's own space at its origin: over the whole surface root, outside its children clip and its scrolling (a root
    /// that is a scroller draws its sheets where it shows, not where its content scrolled to).
    /// </remarks>
    internal override void OnGetRenderProps(ref Rendering.SkUiRenderProps props)
    {
        base.OnGetRenderProps(ref props);
        props.Pinned = true;
        props.X = 0;
        props.Y = 0;
    }

    /// <inheritdoc />
    internal override bool HitsSelfAt(Point local) => false;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        foreach (var view in _views)
            ((IView)view).Measure(widthConstraint, heightConstraint);
        return new Size(double.IsFinite(widthConstraint) ? widthConstraint : 0, double.IsFinite(heightConstraint) ? heightConstraint : 0);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        foreach (var view in _views)
        {
            ((IView)view).Measure(size.Width, size.Height);
            ((IView)view).Arrange(new Rect(0, 0, size.Width, size.Height));
        }
    }
}
