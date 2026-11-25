namespace MauiSkiaUi;

/// <summary>A node that can leave its subtree out of the tab order while it stays focusable by code (a tab view's panes out of view).</summary>
internal interface ISkUiKeyboardScope
{
    /// <summary>Whether Tab goes into the subtree now.</summary>
    bool TakesTabStops { get; }
}
