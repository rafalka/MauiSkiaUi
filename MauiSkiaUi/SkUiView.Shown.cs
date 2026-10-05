namespace MauiSkiaUi;

public partial class SkUiView
{
    /// <summary>
    /// Whether the view is shown: it and every drawn ancestor are <see cref="VisualElement.IsVisible"/>, and the top of
    /// its drawn tree is on a live surface. Position does not count: a view scrolled out of a scroll view, clipped or
    /// fully transparent is still shown. Computed on demand, or kept up to date while <see cref="IsShownChanged"/> has
    /// handlers.
    /// </summary>
    public bool IsShown => SkUiShownTracker.IsShown(this);

    /// <summary>
    /// Raised when <see cref="IsShown"/> changes: this view or an ancestor is shown or hidden, the view moves into or out
    /// of a shown tree, or the tree's surface is attached or released. Handlers run on the UI thread, inside the change.
    /// Free while no view has handlers; with handlers, only their branch of the tree is walked on a change.
    /// </summary>
    public event EventHandler? IsShownChanged
    {
        add => SkUiShownTracker.Add(this, value);
        remove => SkUiShownTracker.Remove(this, value);
    }

    /// <summary>Whether a surface draws this tree (only the frame renderer subscribes to <see cref="RenderRootDirty"/>).</summary>
    internal bool HasLiveSurface => RenderRootDirty is not null;
}
