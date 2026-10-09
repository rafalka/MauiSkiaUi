namespace MauiSkiaUi;

/// <summary>How pull-to-refresh shows its indicator (<see cref="SkUiLook.DefaultRefreshStyle"/>).</summary>
public enum SkUiRefreshStyle
{
    /// <summary>The look's choice (<see cref="SkUiLook.DefaultRefreshStyle"/>; the default look follows the platform).</summary>
    Default,

    /// <summary>
    /// Android's: a round badge comes down over the content with the pull and rests below the top while refreshing; the
    /// content stays where it is.
    /// </summary>
    Overlay,

    /// <summary>
    /// iOS's: the content moves down with the pull and the indicator shows in the space above it; while refreshing the
    /// content stays down by <see cref="SkUiLook.RefreshRestDistance"/>. A scroller moves its content only with
    /// <see cref="SkUiOverscrollMode.Bounce"/>; with other modes the indicator draws over the top of the content.
    /// </summary>
    Inline
}

/// <summary>Who sets <c>IsRefreshing</c> back to <c>false</c> when a refresh is done.</summary>
public enum SkUiRefreshCompletion
{
    /// <summary>The app (MAUI's <c>RefreshView</c> rule): it sets <c>IsRefreshing</c> to <c>false</c> when the refresh is done.</summary>
    Manual,

    /// <summary>
    /// The view, when the refresh's work is done: the command, if it cannot execute right after it ran (an async command
    /// such as the MVVM Toolkit's <c>AsyncRelayCommand</c>, busy while it runs) until it can again, and every deferral taken
    /// in <c>Refreshing</c> (<see cref="SkUiRefreshingEventArgs.GetDeferral"/>). A command that can still execute right
    /// after it ran is done then. Setting <c>IsRefreshing</c> to <c>false</c> still ends a refresh at once.
    /// </summary>
    Automatic
}

/// <summary>Arguments of a refresh view's or a collection view's <c>Refreshing</c> event.</summary>
public sealed class SkUiRefreshingEventArgs : EventArgs
{
    private readonly Func<SkUiRefreshDeferral>? _defer;

    internal SkUiRefreshingEventArgs(Func<SkUiRefreshDeferral>? defer) => _defer = defer;

    /// <summary>
    /// With <see cref="SkUiRefreshCompletion.Automatic"/> completion, keeps the refresh running until the returned deferral
    /// completes (<see cref="SkUiRefreshDeferral.Complete"/> or <c>Dispose</c>, from any thread). With
    /// <see cref="SkUiRefreshCompletion.Manual"/> completion the deferral does nothing: the app ends the refresh.
    /// </summary>
    public SkUiRefreshDeferral GetDeferral() => _defer?.Invoke() ?? SkUiRefreshDeferral.None;
}

/// <summary>Keeps a refresh running until it completes (<see cref="SkUiRefreshingEventArgs.GetDeferral"/>).</summary>
public sealed class SkUiRefreshDeferral : IDisposable
{
    internal static readonly SkUiRefreshDeferral None = new(null);

    private Action? _complete;

    internal SkUiRefreshDeferral(Action? complete) => _complete = complete;

    /// <summary>The work is done (once; later calls do nothing). Any thread.</summary>
    public void Complete() => Interlocked.Exchange(ref _complete, null)?.Invoke();

    /// <summary>Same as <see cref="Complete"/>.</summary>
    public void Dispose() => Complete();
}
