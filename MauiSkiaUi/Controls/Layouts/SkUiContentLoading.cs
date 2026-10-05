namespace MauiSkiaUi;

/// <summary>When a <see cref="SkUiContentView"/> attaches its content (<see cref="SkUiContentView.ContentLoading"/>).</summary>
public enum SkUiContentLoading
{
    /// <summary>At once, as MAUI's <c>ContentView</c> (the default).</summary>
    Immediate,

    /// <summary>
    /// When the view is first shown (<see cref="SkUiView.IsShown"/>: it and its ancestors are visible on a live
    /// surface), optionally after <see cref="SkUiContentView.ContentLoadingDelay"/>. Content from a template is
    /// created then.
    /// </summary>
    WhenShown
}
