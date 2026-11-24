namespace MauiSkiaUi;

/// <summary>A segmented control's or a tab view's selected index changed, from the user or the app.</summary>
/// <param name="previousIndex">The index selected before (-1: none).</param>
/// <param name="currentIndex">The index selected now (-1: none).</param>
public sealed class SkUiSelectedIndexChangedEventArgs(int previousIndex, int currentIndex) : EventArgs
{
    /// <summary>The index selected before (-1: none).</summary>
    public int PreviousIndex { get; } = previousIndex;

    /// <summary>The index selected now (-1: none).</summary>
    public int CurrentIndex { get; } = currentIndex;
}
