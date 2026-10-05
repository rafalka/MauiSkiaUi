namespace MauiSkiaUi;

/// <summary>
/// One animated change of what a container shows (<see cref="SkUiStateContainer"/>, <see cref="SkUiAlternateContentView"/>):
/// <c>before</c> runs on the shown views, the change happens, <c>after</c> runs on the new ones. Views that left get
/// their own opacity, translation, rotation and scale back, and new ones end at the animation's end values, exactly,
/// also when the change is cancelled. While the host has not been drawn, it only changes.
/// </summary>
internal static class SkUiStateTransition
{
    public static async Task RunAsync(SkUiView host, Func<IEnumerable<SkUiView>> shownViews, Action change,
        SkUiViewAnimation? before, SkUiViewAnimation? after, CancellationToken token)
    {
        var animate = host.RenderState.HasCommitted && (before is not null || after is not null);
        var shown = animate ? shownViews().ToDictionary(view => view, SkUiViewAnimation.Snapshot.Of) : [];
        var incoming = new Dictionary<SkUiView, SkUiViewAnimation.Snapshot>();
        try
        {
            if (before is not null && shown.Count > 0)
                await Task.WhenAll(shown.Keys.Select(before.RunAsync)).WaitAsync(token);
            change();
            if (animate && after is not null)
            {
                foreach (var view in shownViews())
                {
                    // A view that stays (the same state) starts the second animation from its own values again.
                    if (shown.TryGetValue(view, out var own))
                        own.Restore(view);
                    incoming[view] = SkUiViewAnimation.Snapshot.Of(view);
                }
                if (incoming.Count > 0)
                    await Task.WhenAll(incoming.Keys.Select(after.RunAsync)).WaitAsync(token);
            }
        }
        finally
        {
            foreach (var (view, own) in shown)
                if (!incoming.ContainsKey(view))
                    own.Restore(view);
            foreach (var (view, own) in incoming)
                after!.ApplyEnd(view, own);
        }
    }
}
